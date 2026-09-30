// ============================================================================
// ProceduralInteractablePresenter.cs
// ============================================================================
// PURPOSE:
//   Describes generated doors, existing vault barriers, corner props and doorway
//   mark slots without engine access. It also gives the existing Environment torch
//   sockets stable Light identities, avoiding a second set of decorative flames.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Sample ordinary doors with injected randomness, always excluding exit links.
//   - Keep prop envelopes clear of shell geometry, objective anchors and main routes.
//   - Produce stable Core snapshots and a culture-independent construction manifest.
//   - Keep props on occupied cells and suppress legacy light sockets over L-shaped voids.
// DEPENDENCIES:
//   - Core contracts and own layout/config values; no Presentation dependency.
// USAGE NOTES:
//   Doors start open and retract visually; they add no static navigation blocker.
//   Partition identities reference the existing vault/window sill SurfaceId, not
//   duplicate geometry. Light sockets mirror EnvironmentPresenter.BuildSlots' fixed
//   legacy placement contract until the coordinator moves that contract into Core.
//   Environment must bind those identities and apply Lit changes to its existing flames.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralInteractablePresenter
    {
        public IReadOnlyList<ProceduralInteractablePlan> Build(ProceduralLayout layout, ProceduralConfig config,
            ProceduralDriverConfig driver, IReadOnlyList<ProceduralBlock> blocks, System.Random random)
        {
            Vector3 size = driver.KnockablePropSize;
            foreach (float value in new[] { size.x, size.y, size.z, driver.KnockablePropInset, driver.ThresholdMarkThickness })
                if (!(value > 0f) || float.IsInfinity(value)) throw new ArgumentException("Invalid interactable dimensions.");
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            if (driver.KnockablePropInset - radius <= driver.WallThickness * 0.5f ||
                driver.KnockablePropInset + radius >= config.CandidatePerimeterInset || size.y >= config.DoorHeight)
                throw new ArgumentException("Props must fit in the outer corner pockets, away from required cake routes.");
            var result = new List<ProceduralInteractablePlan>();
            for (int index = 0; index < layout.Doors.Count; index++)
            {
                var door = layout.Doors[index]; int edge = 1001 + index;
                if (door.IsOptional) continue;
                bool sampled = random.NextDouble() < config.OrdinaryDoorFraction;
                bool exit = door.FromRoomId == layout.Graph.ExitRoomId || door.ToRoomId == layout.Graph.ExitRoomId;
                if (sampled && !exit)
                    result.Add(new ProceduralInteractablePlan(new InteractableState(100000 + edge, InteractableKind.Door,
                        door.FromRoomId, door.Center + Vector3.up * (config.DoorHeight * 0.5f), InteractableStateValue.Open, edge),
                        door.AlongX ? new Vector3(config.DoorWidth, config.DoorHeight, driver.WallThickness) :
                        new Vector3(driver.WallThickness, config.DoorHeight, config.DoorWidth)));
                result.Add(new ProceduralInteractablePlan(new InteractableState(300000 + edge, InteractableKind.ThresholdMark,
                    door.FromRoomId, door.Center + Vector3.up * (driver.ThresholdMarkThickness * 0.5f), InteractableStateValue.Inactive, edge),
                    door.AlongX ? new Vector3(config.DoorWidth, driver.ThresholdMarkThickness, driver.WallThickness) :
                    new Vector3(driver.WallThickness, driver.ThresholdMarkThickness, config.DoorWidth)));
            }
            foreach (var block in blocks.Where(b => b.TraversalKind == TraversalSurfaceKind.Vault))
                result.Add(new ProceduralInteractablePlan(new InteractableState(200000 + block.SurfaceId,
                    InteractableKind.Partition, block.RoomId, block.Center, InteractableStateValue.Inactive), block.Size, block.SurfaceId));
            foreach (var room in layout.Graph.Rooms)
            {
                var portals = layout.Doors.Where(d => d.FromRoomId == room.Id || d.ToRoomId == room.Id).Select(d => d.Center).ToArray();
                AddLights(result, room, portals);
                var volumes = ProceduralFootprintUtility.Volumes(layout, room);
                result.RemoveAll(p => p.State.RoomId == room.Id && p.State.Kind == InteractableKind.Light &&
                    (!volumes.Any(v => v.Bounds.Contains(p.State.Position)) || !blocks.Any(b =>
                        b.Kind == ProceduralSurfaceKind.Wall && new Bounds(b.Center, b.Size + Vector3.one * 0.65f).Contains(p.State.Position))));
                int start = random.Next(volumes.Count * 4), count = 0;
                for (int index = 0; index < volumes.Count * 4 && count < config.KnockablePropsPerRoom; index++)
                {
                    int socket = (start + index) % (volumes.Count * 4), corner = socket % 4;
                    var bounds = volumes[socket / 4].Bounds;
                    var position = new Vector3(corner % 2 == 0 ? bounds.min.x + driver.KnockablePropInset : bounds.max.x - driver.KnockablePropInset,
                        size.y * 0.5f, corner < 2 ? bounds.min.z + driver.KnockablePropInset : bounds.max.z - driver.KnockablePropInset);
                    var envelope = new Bounds(position, size);
                    if (blocks.Any(b => b.HasCollision && Overlaps(b, envelope)) ||
                        portals.Any(p => HorizontalDistance(p, position) < config.DoorWidth * 0.5f + radius + driver.NavSampleRadius) ||
                        layout.Graph.Anchors.Concat(layout.PocketAnchors).Any(a => a.RoomId == room.Id && HorizontalDistance(a.Position, position) < radius + driver.NavSampleRadius)) continue;
                    result.Add(new ProceduralInteractablePlan(new InteractableState(500000 + room.Id * 100 + socket,
                        InteractableKind.KnockableProp, room.Id, position, InteractableStateValue.Inactive), size));
                    count++;
                }
                if (count != config.KnockablePropsPerRoom) throw new InvalidOperationException("No safe prop sockets in room " + room.Id);
            }
            return Array.AsReadOnly(result.OrderBy(plan => plan.State.Id).ToArray());
        }

        public string Manifest(IReadOnlyList<ProceduralInteractablePlan> plans)
        {
            var text = new StringBuilder("|interactables-v1");
            foreach (var plan in plans)
            {
                var item = plan.State;
                text.Append("|I:").Append(item.Id).Append(',').Append((int)item.Kind).Append(',').Append(item.RoomId)
                    .Append(',').Append(item.EdgeId).Append(',').Append((int)item.Value).Append(',').Append(plan.SurfaceId);
                foreach (float value in new[] { item.Position.x, item.Position.y, item.Position.z, plan.Size.x, plan.Size.y, plan.Size.z })
                    text.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private static void AddLights(List<ProceduralInteractablePlan> result, LevelRoom room, Vector3[] portals)
        {
            var bounds = room.Bounds;
            if (bounds.size.x < 5f || bounds.size.z < 5f || bounds.size.y < 3.4f) return;
            int torchCount = 0, decorCount = 0, start = (room.Id & int.MaxValue) % 8;
            for (int n = 0; n < 8; n++)
            {
                int index = (start + n) % 8, wall = index / 2;
                float offset = index % 2 == 0 ? -0.28f : 0.28f;
                Vector3 position = bounds.center; position.y = bounds.min.y + 2.75f;
                if (wall == 0 || wall == 2)
                { position.x += bounds.size.x * offset; position.z = wall == 0 ? bounds.min.z + 0.3f : bounds.max.z - 0.3f; }
                else
                { position.z += bounds.size.z * offset; position.x = wall == 1 ? bounds.max.x - 0.3f : bounds.min.x + 0.3f; }
                if (portals.Any(p => HorizontalDistance(p, position) < 2.1f)) continue;
                bool torch = n % 2 == 0 && torchCount < 2;
                if (!torch && decorCount >= 2) { if (torchCount >= 2) continue; torch = true; }
                if (torch)
                {
                    result.Add(new ProceduralInteractablePlan(new InteractableState(400000 + room.Id * 10 + index,
                        InteractableKind.Light, room.Id, position, InteractableStateValue.Lit), Vector3.zero));
                    torchCount++;
                }
                else decorCount++;
                if (torchCount == 2 && decorCount == 2) break;
            }
        }
        private static bool Overlaps(ProceduralBlock block, Bounds envelope)
        {
            var bounds = new Bounds(block.Center, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                bounds.Encapsulate(block.Center + block.Rotation * new Vector3(
                    (corner & 1) == 0 ? -block.Size.x : block.Size.x,
                    (corner & 2) == 0 ? -block.Size.y : block.Size.y,
                    (corner & 4) == 0 ? -block.Size.z : block.Size.z) * 0.5f);
            return bounds.min.x < envelope.max.x && bounds.max.x > envelope.min.x &&
                bounds.min.y < envelope.max.y && bounds.max.y > envelope.min.y &&
                bounds.min.z < envelope.max.z && bounds.max.z > envelope.min.z;
        }
        private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
