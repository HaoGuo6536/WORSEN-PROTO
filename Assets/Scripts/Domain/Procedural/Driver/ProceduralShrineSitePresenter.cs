// ============================================================================
// ProceduralShrineSitePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes kind-free shrine sockets on supported ground in a generated floor.
//   Recorded Passage edges supply special sockets on the connected side of gaps;
//   ordinary sockets stay clear of objectives, actors, portals and built geometry.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Produce deterministic, separated placement candidates without random draws.
//   - Reserve the larger of the shrine and supplied navigation agent envelopes.
//   - Record candidate positions, room ownership, gap flags and facing in the manifest.
//   - Admit gap sockets only when their straight crossing reaches an identified pocket.
//   - Consume exactly one authored socket per designated template shrine room.
// DEPENDENCIES:
//   - Own layout/config/block definitions and Core graph values only.
// USAGE NOTES:
//   Ground sockets retain the original room identity across storeys. The Driver
//   filters these candidates with native navigation before the Manager publishes
//   them. Shrine owns kind/count selection and must fit the reserved envelope.
//   Unit collision rotations are inverted by conjugation to keep support managed.
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
    public sealed class ProceduralShrineSitePresenter
    {
        public IReadOnlyList<ProceduralShrineSite> Build(ProceduralLayout layout, ProceduralConfig config,
            IReadOnlyList<ProceduralBlock> blocks, float navigationRadius = 0f, float navigationHeight = 0f)
        {
            var size = config.ShrineSiteEnvelope;
            if (!ProceduralTemplateUtility.Finite(navigationRadius) || navigationRadius < 0f ||
                !ProceduralTemplateUtility.Finite(navigationHeight) || navigationHeight < 0f)
                throw new ArgumentException("Invalid shrine navigation dimensions.");
            size = new Vector3(Math.Max(size.x, navigationRadius * 2f), Math.Max(size.y, navigationHeight), Math.Max(size.z, navigationRadius * 2f));
            foreach (float value in new[] { size.x, size.y, size.z, config.ShrineSiteInset, config.ShrineSiteClearance })
                if (!(value > 0f) || float.IsInfinity(value)) throw new ArgumentException("Invalid shrine site dimensions.");
            if (config.ShrineSiteInset <= Mathf.Max(size.x, size.z) * .5f || config.ShrineSiteInset >= config.RoomSize * .5f)
                throw new ArgumentException("Shrine site inset must contain the envelope inside a cell.");
            if (float.IsNaN(config.ShrineSiteLateralFraction) || config.ShrineSiteLateralFraction < 0f || config.ShrineSiteLateralFraction > .5f)
                throw new ArgumentException("Shrine lateral fraction must fit within half a cell.");
            var result = new List<ProceduralShrineSite>();
            var reachable = LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, TraversalAccess.Player);
            var forbidden = layout.Graph.Anchors.Select(a => a.Position).Concat(layout.HunterSpawnPositions)
                .Concat(layout.Puzzles.Select(p => p.Reward.Position))
                .Concat(new[] { layout.PlayerSpawnPosition, layout.Graph.ExitPosition })
                .Concat(layout.Doors.Select(d => d.Center)).ToArray();
            var obstacles = blocks.Where(b => b.HasCollision).Select(WorldBounds).Concat(layout.Interactables
                .Where(p => p.State.Kind == InteractableKind.KnockableProp).Select(p => new Bounds(p.State.Position, p.Size))).ToArray();
            if (layout.UsesTemplates)
            {
                foreach (var placed in layout.TemplateRooms.Where(r => r.PocketId == 0 && r.Template.Kind == "shrine").OrderBy(r => r.RoomId))
                {
                    if (placed.Template.ShrineSockets.Length != 1) throw new ArgumentException("Shrine room needs exactly one socket.");
                    var socket = placed.Template.ShrineSockets[0];
                    var rotated = ProceduralTemplateUtility.Rotate(socket.ModelEnvelope, placed.Turns);
                    size = new Vector3(Math.Max(Math.Abs(rotated.x), navigationRadius * 2f), Math.Max(rotated.y, navigationHeight), Math.Max(Math.Abs(rotated.z), navigationRadius * 2f));
                    var position = ProceduralTemplateUtility.Point(placed, socket.Position, layout.Origin);
                    var facing = ProceduralTemplateUtility.Rotate(socket.Facing, placed.Turns);
                    bool gap = new ProceduralPassagePresenter().Destination(layout, placed.RoomId, position, facing) != 0;
                    int previousCount = result.Count;
                    Add(placed.RoomId, position, gap, facing);
                    if (!gap || result.Count != previousCount + 1) throw new InvalidOperationException("Authored shrine socket or Passage pocket failed admission in room " + placed.RoomId);
                }
                return result.AsReadOnly();
            }
            foreach (var gap in layout.GapSites)
            {
                var facing = gap.Landing - gap.Edge; facing.y = 0f; facing.Normalize();
                var tangent = new Vector3(facing.z, 0f, -facing.x);
                foreach (float offset in new[] { 0f, -config.RoomSize * config.ShrineSiteLateralFraction, config.RoomSize * config.ShrineSiteLateralFraction })
                    Add(gap.RoomId, gap.Edge - facing * config.ShrineSiteInset + tangent * offset, true, facing);
            }
            foreach (var room in layout.Graph.Rooms.OrderBy(r => r.Id))
            {
                if (room.Id == layout.Graph.ExitRoomId) continue;
                foreach (var cell in room.Cells)
                foreach (var normal in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                foreach (float sign in new[] { -1f, 1f })
                {
                    var tangent = new Vector3(normal.z, 0f, -normal.x);
                    var position = cell.center; position.y = cell.min.y;
                    float depth = normal.x == 0f ? cell.size.z : cell.size.x;
                    float width = normal.x == 0f ? cell.size.x : cell.size.z;
                    if (depth <= config.ShrineSiteInset * 2f) continue;
                    position += normal * (depth * .5f - config.ShrineSiteInset) + tangent * (width * config.ShrineSiteLateralFraction * sign);
                    Add(room.Id, position, false, -normal);
                }
            }
            return result.AsReadOnly();

            void Add(int roomId, Vector3 position, bool gapEdge, Vector3 facing)
            {
                int destination = gapEdge ? new ProceduralPassagePresenter().Destination(layout, roomId, position, facing) : 0;
                if (gapEdge && destination == 0) return;
                var room = layout.Graph.Rooms.Single(r => r.Id == roomId);
                if (room.Pocket || reachable[roomId] < 0 || !room.ContainsXZ(position) || facing.sqrMagnitude == 0f) return;
                if (layout.OrganicRooms.Any(r => r.RoomId == roomId && !ProceduralOrganicUtility.Clear(r, position))) return;
                if (forbidden.Any(p => DistanceXZ(p, position) < config.ShrineSiteClearance) ||
                    result.Any(s => DistanceXZ(s.Position, position) < config.ShrineSiteClearance)) return;
                var body = new Bounds(position + Vector3.up * (size.y * .5f), size);
                if (obstacles.Any(b => Overlaps(b, body))) return;
                foreach (float x in new[] { -size.x * .5f, size.x * .5f })
                foreach (float z in new[] { -size.z * .5f, size.z * .5f })
                {
                    var foot = position + new Vector3(x, -.01f, z);
                    if (!room.ContainsXZ(foot) || !blocks.Any(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Floor && Supports(b, foot))) return;
                }
                result.Add(new ProceduralShrineSite(roomId, position, gapEdge, facing, destination));
            }
        }

        public string Manifest(IReadOnlyList<ProceduralShrineSite> sites, ProceduralConfig config)
        {
            var text = new StringBuilder("|shrine-sites-v2");
            Values(config.ShrineSiteInset, config.ShrineSiteClearance, config.ShrineSiteLateralFraction, config.ShrineSiteEnvelope.x,
                config.ShrineSiteEnvelope.y, config.ShrineSiteEnvelope.z);
            foreach (var site in sites)
            {
                text.Append("|ShrineSite:").Append(site.RoomId).Append(',').Append(site.GapEdge ? 1 : 0);
                text.Append(',').Append(site.DestinationPocketRoomId);
                Values(site.Position.x, site.Position.y, site.Position.z, site.Facing.x, site.Facing.y, site.Facing.z);
            }
            return text.ToString();
            void Values(params float[] values)
            { foreach (float value in values) text.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture)); }
        }

        private static bool Supports(ProceduralBlock block, Vector3 foot)
        {
            var q = block.Rotation;
            var p = new Quaternion(-q.x, -q.y, -q.z, q.w) * (foot - block.Center);
            return Math.Abs(p.x) <= block.Size.x * .5f && Math.Abs(p.y) <= block.Size.y * .5f && Math.Abs(p.z) <= block.Size.z * .5f;
        }
        private static float DistanceXZ(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        private static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x &&
            a.min.y < b.max.y && a.max.y > b.min.y && a.min.z < b.max.z && a.max.z > b.min.z;
        private static Bounds WorldBounds(ProceduralBlock block)
        {
            var bounds = new Bounds(block.Center, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                bounds.Encapsulate(block.Center + block.Rotation * new Vector3(
                    (corner & 1) == 0 ? -block.Size.x : block.Size.x,
                    (corner & 2) == 0 ? -block.Size.y : block.Size.y,
                    (corner & 4) == 0 ? -block.Size.z : block.Size.z) * .5f);
            return bounds;
        }
    }
}
