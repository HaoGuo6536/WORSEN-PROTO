// ============================================================================
// ProceduralSpawnUtility.cs
// ============================================================================
// PURPOSE:
//   Rejects first-contact positions visible from the player or too near the exit.
//   Spawn-moving effects can reuse exactly the generator's check, including the
//   effective distance policy recorded for unusually small floors.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Measure shortest hunter-walkable portal hops, never straight-line distance.
//   - Trace a planar sight segment through known room walls and portal apertures.
//   - Relax only distance, one hop at a time, with an explicit audit trail.
// DEPENDENCIES:
//   - Core graph values and Procedural layout/config only; no physics or siblings.
// USAGE NOTES:
//   Conservative room-shell approximation: ignore interior obstacles and height,
//   treat every portal as open, and widen optional apertures to the ordinary width
//   if larger. Ambiguous corner crossings count as visible. This can reject safe
//   positions but never relies on a torch, prop or closed door for initial cover.
//   Exit exclusion, player-room exclusion and occlusion never relax. Exhaustion
//   throws with the relaxation trail so the bounded generation journal records it.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralSpawnUtility
    {
        public static bool Validate(ProceduralLayout layout, Vector3 candidate, float doorWidth,
            int minimumRooms, out string reason)
        {
            if (layout?.Graph == null || minimumRooms < 1 || !(doorWidth > 0f) || float.IsInfinity(doorWidth))
                throw new ArgumentException("Spawn validation requires a graph, positive width and hop minimum.");
            var room = layout.Graph.Rooms.FirstOrDefault(r => r.Bounds.Contains(candidate));
            var player = layout.Graph.Rooms.FirstOrDefault(r => r.Bounds.Contains(layout.PlayerSpawnPosition));
            if (room.Id == 0 || player.Id == 0) { reason = "outside-room"; return false; }
            if (room.Id == layout.Graph.ExitRoomId || room.Id == player.Id)
            { reason = "exit-or-player-room"; return false; }
            int hops = LevelGraphUtility.TopologicalDistancesFrom(layout.Graph,
                layout.Graph.ExitRoomId, TraversalAccess.Hunter)[room.Id];
            if (hops < minimumRooms) { reason = "path-too-short-or-unreachable"; return false; }
            if (Visible(layout, player, room.Id, candidate, doorWidth)) { reason = "visible"; return false; }
            reason = string.Empty; return true;
        }

        public static IReadOnlyList<Vector3> Select(ProceduralLayout layout, ProceduralConfig config,
            IReadOnlyList<Vector3> candidates, out int minimumRooms, out string report)
        {
            if (config.MinimumHunterSpawnRooms < 1 || config.MinimumHunterSpawnRooms > 256)
                throw new ArgumentException("Spawn hop minimum must be between 1 and 256.");
            report = "room-shell-open-portals;";
            for (minimumRooms = config.MinimumHunterSpawnRooms; minimumRooms >= 1; minimumRooms--)
            {
                int minimum = minimumRooms;
                var valid = candidates.Where(p => Validate(layout, p, config.DoorWidth, minimum, out _)).ToArray();
                report += "rooms=" + minimum + (valid.Length > 0 ? ":accepted" : ":none;");
                if (valid.Length > 0) return Array.AsReadOnly(valid);
            }
            throw new InvalidOperationException("No occluded non-exit hunter spawn; " + report);
        }

        private static bool Visible(ProceduralLayout layout, LevelRoom current, int target,
            Vector3 end, float doorWidth)
        {
            Vector3 start = layout.PlayerSpawnPosition, delta = end - start;
            float previous = -1f;
            for (int step = 0; step < layout.Graph.Rooms.Count; step++)
            {
                if (current.Id == target) return true;
                var bounds = current.Bounds;
                float tx = delta.x == 0f ? float.PositiveInfinity :
                    ((delta.x > 0f ? bounds.max.x : bounds.min.x) - start.x) / delta.x;
                float tz = delta.z == 0f ? float.PositiveInfinity :
                    ((delta.z > 0f ? bounds.max.z : bounds.min.z) - start.z) / delta.z;
                // Numerical/corner ambiguity must not manufacture occlusion.
                if (Mathf.Abs(tx - tz) < 0.00001f) return true;
                float t = Mathf.Min(tx, tz);
                if (t <= previous || t >= 1f) return true;
                Vector3 crossing = start + delta * t;
                bool alongX = tz < tx;
                int next = 0;
                foreach (var door in layout.Doors)
                {
                    if (door.AlongX != alongX) continue;
                    int neighbor = door.FromRoomId == current.Id ? door.ToRoomId :
                        door.ToRoomId == current.Id ? door.FromRoomId : 0;
                    if (neighbor == 0) continue;
                    float plane = alongX ? crossing.z - door.Center.z : crossing.x - door.Center.x;
                    float along = alongX ? crossing.x - door.Center.x : crossing.z - door.Center.z;
                    float width = door.IsOptional ? Mathf.Max(1.8f, doorWidth) : doorWidth;
                    if (Mathf.Abs(plane) < 0.0001f && Mathf.Abs(along) <= width * 0.5f + 0.0001f)
                    { next = neighbor; break; }
                }
                if (next == 0) return false;
                current = layout.Graph.Rooms.First(r => r.Id == next);
                previous = t;
            }
            return true;
        }
    }
}
