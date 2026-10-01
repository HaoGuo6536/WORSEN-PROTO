// ============================================================================
// ProceduralFreezeUtility.cs
// ============================================================================
// PURPOSE:
//   Stages a doorway cake beside an already validated hunter position.
//   It favors a removable room behind the player, never the exit or a chokepoint,
//   so Floor can prefer early collapse without destroying the escape topology.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Use an authored template cake, or replace a fallback socket, and prioritize its hunter.
//   - Spend a shared gimmick slot only after the first-contact check succeeds.
// DEPENDENCIES:
//   - Core graph utility and own generation data only.
// USAGE NOTES:
//   Floor still chooses required cakes and collapse order. No hunter is invented,
//   and hearing playback/patrol ownership remains outside Procedural.
// ============================================================================
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralFreezeUtility
    {
        public static void Apply(ProceduralLayout layout, ProceduralConfig config)
        {
            var tuning = config.Challenges;
            if (tuning == null || layout.RoundIndex < tuning.FreezeFirstRound ||
                layout.Modules.Any(m => m.Kind == ProceduralModuleKind.MerchantRefuge)) return;
            var occupied = ProceduralGimmickUtility.Rooms(layout);
            if (occupied.Count >= layout.GimmickBudget) return;
            if (tuning.FreezeFirstRound < 1 || !(tuning.AudibleDistance > 0f) || float.IsInfinity(tuning.AudibleDistance))
                throw new ArgumentException("Invalid freeze-room settings.");
            for (int i = 0; i < layout.Doors.Count; i++)
            {
                var door = layout.Doors[i];
                if (door.IsOptional) continue;
                foreach (int roomId in new[] { door.FromRoomId, door.ToRoomId })
                {
                    int behind = roomId == door.FromRoomId ? door.ToRoomId : door.FromRoomId;
                    if (layout.UsesTemplates && !layout.TemplateRooms.Any(r => r.RoomId == roomId && r.Template.Gimmick == "freeze")) continue;
                    if (behind == layout.Graph.ExitRoomId || roomId == layout.Graph.ExitRoomId || occupied.Contains(roomId) ||
                        layout.OrganicRooms.Any(r => r.RoomId == roomId)) continue;
                    var reduced = LevelGraphUtility.Build(layout.Graph.Rooms.Where(r => r.Id != behind).ToArray(),
                        layout.Graph.Edges.Where(e => e.FromRoomId != behind && e.ToRoomId != behind).ToArray(),
                        layout.Graph.Anchors.Where(a => a.RoomId != behind).ToArray(), layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
                    var escape = LevelGraphUtility.DistancesTo(reduced, reduced.ExitRoomId, TraversalAccess.Player);
                    if (layout.Modules.Any(m => m.PocketId == 0 && m.RoomId != behind && escape[m.RoomId] < 0)) continue;
                    var hunters = layout.HunterSpawnPositions.Where(p => ProceduralFootprintUtility.At(layout, p).Id == roomId &&
                        Vector3.Distance(p, door.Center) <= tuning.AudibleDistance &&
                        ProceduralSpawnUtility.Validate(layout, p, config.DoorWidth, layout.MinimumHunterSpawnRooms, out _)).ToArray();
                    var old = layout.Graph.Anchors.FirstOrDefault(a => a.RoomId == roomId && a.Position.y <= config.AnchorHeight);
                    if (hunters.Length == 0 || old.Id == 0) continue;
                    var cell = ProceduralFootprintUtility.Volumes(layout, layout.Graph.Rooms[roomId - 1])
                        .First(r => r.Bounds.Contains(door.Center));
                    var axis = door.AlongX ? Vector3.forward : Vector3.right;
                    float sign = Vector3.Dot(cell.Center - door.Center, axis) > 0f ? 1f : -1f;
                    var position = door.Center + axis * (sign * config.CandidatePerimeterInset) + Vector3.up * config.AnchorHeight;
                    if (layout.UsesTemplates) position = old.Position;
                    if (layout.Graph.Anchors.Any(a => a.Id != old.Id && Vector3.Distance(a.Position, position) < 0.01f)) continue;
                    var anchors = layout.Graph.Anchors.Select(a => a.Id == old.Id ?
                        new LevelAnchor(a.Id, roomId, CakeAnchorType.Flow, position) : a).ToArray();
                    layout.Graph = LevelGraphUtility.Build(layout.Graph.Rooms, layout.Graph.Edges, anchors,
                        layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
                    layout.FreezeRooms = Array.AsReadOnly(new[] { new ProceduralFreezePlan(i, roomId, behind, old.Id, hunters[0]) });
                    layout.HunterSpawnPositions = Array.AsReadOnly(new[] { hunters[0] }.Concat(layout.HunterSpawnPositions.Where(p => p != hunters[0])).ToArray());
                    return;
                }
            }
        }
        public static string Manifest(ProceduralLayout layout) => string.Concat(layout.FreezeRooms.Select(p =>
            "|Freeze:" + p.DoorIndex + "," + p.RoomId + "," + p.BehindRoomId + "," + p.AnchorId + "," +
            string.Join(",", new[] { p.Hunter.x, p.Hunter.y, p.Hunter.z }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))));
    }
}
