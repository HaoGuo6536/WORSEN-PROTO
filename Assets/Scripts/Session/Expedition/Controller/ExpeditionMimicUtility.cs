// ============================================================================
// ExpeditionMimicUtility.cs
// ============================================================================
// PURPOSE:
//   Finds false collection sites between the generated walking-route cakes.
//   No anchor or pickup is created or removed: Mimics occupy the gaps in a line,
//   with native route evidence supplied by Floor's existing path query boundary.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Exclude spawn/pocket/closing rooms, real cakes and already occupied sites.
//   - Admit only adjacent, straight, reachable cake-route segments with a free midpoint.
// DEPENDENCIES:
//   - Core graph values and an injected complete-path-length callback only.
// USAGE NOTES:
//   No engine calls or randomness. Stable anchor order determines allocation order.
//   The 1cm tolerance compares native path geometry, not a designer spacing value.
//   Spacing is the generator's configured RouteCakeSpacing, never a second default.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public static class ExpeditionMimicUtility
    {
        public static IReadOnlyList<Vector3> Sites(LevelGraph graph, Vector3 spawn, float spacing,
            IReadOnlyList<Vector3> occupied, IReadOnlyDictionary<int, RoomPhase> phases,
            Func<Vector3, Vector3, float> pathLength)
        {
            var sites = new List<Vector3>();
            if (graph == null || pathLength == null || !(spacing > 0f) || float.IsInfinity(spacing)) return sites;
            for (int i = 1; i < graph.Anchors.Count; i++)
            {
                var a = graph.Anchors[i - 1]; var b = graph.Anchors[i];
                if (a.RoomId != b.RoomId || b.Id != a.Id + 1) continue;
                var delta = b.Position - a.Position;
                if (Mathf.Abs(delta.y) > .01f || Mathf.Abs(delta.magnitude - spacing) > .01f ||
                    Mathf.Abs(delta.x) > .01f && Mathf.Abs(delta.z) > .01f) continue;
                LevelRoom room = default;
                foreach (var candidate in graph.Rooms) if (candidate.Id == a.RoomId) { room = candidate; break; }
                if (room.Cells == null || room.Pocket || room.ContainsXZ(spawn) ||
                    phases != null && (!phases.TryGetValue(room.Id, out var phase) || phase != RoomPhase.Open)) continue;
                var midpoint = (a.Position + b.Position) * .5f;
                // Hunter roots stand on the walking plane rather than at pickup height.
                midpoint.y = room.Bounds.min.y;
                if (!room.ContainsXZ(midpoint)) continue;
                bool blocked = false;
                foreach (var cake in graph.Anchors)
                    if (HorizontalSquared(cake.Position - midpoint) < spacing * spacing * .24f) { blocked = true; break; }
                if (occupied != null) foreach (var other in occupied)
                    if (HorizontalSquared(other - midpoint) < spacing * spacing) { blocked = true; break; }
                foreach (var other in sites)
                    if (HorizontalSquared(other - midpoint) < spacing * spacing) { blocked = true; break; }
                if (blocked) continue;
                // Reject bends, disconnected islands and snapped points that shorten
                // the segment. A plausible line in the graph alone is not nav evidence.
                float left = pathLength(a.Position, midpoint), right = pathLength(midpoint, b.Position);
                if (!Finite(left) || !Finite(right) || Mathf.Abs(left - spacing * .5f) > .01f ||
                    Mathf.Abs(right - spacing * .5f) > .01f || !Finite(pathLength(spawn, midpoint))) continue;
                sites.Add(midpoint);
            }
            return sites;
        }
        private static float HorizontalSquared(Vector3 value) => value.x * value.x + value.z * value.z;
        private static bool Finite(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
