// ============================================================================
// FloorCollapseFrontUtility.cs
// ============================================================================
// PURPOSE:
//   Defines one room-wide advancing plane for fog geometry and hand reach.
//   Clipping each footprint cell against the same plane preserves notches and
//   prevents the former simultaneous room-wide hand reveal.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Map staged progress to continuous consumption and select an escape-facing axis.
//   - Clip consumed cells and probe their advancing front without engine queries.
//   - Build inward-only perimeter walls and compute contact bounce velocity.
// DEPENDENCIES:
//   - Core room values, own probe definitions and Unity value math only.
// USAGE NOTES:
//   Stateless. Time remains phase progress supplied by FloorManager. Tearing uses
//   the existing 12% travel share; Encroaching completes the other 88%.
//   Closed walls seal all exposed edges (including every doorway) because LevelGraph
//   carries no aperture dimensions. Existing walls hide redundant sealed surfaces.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Floor
{
    public static class FloorCollapseFrontUtility
    {
        public static float Consumption(RoomPhase phase, float progress)
        {
            if (phase == RoomPhase.Closed) return 1f;
            progress = float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : Mathf.Clamp01(progress);
            if (phase == RoomPhase.Tearing) return .12f * progress;
            return phase == RoomPhase.Encroaching ? .12f + .88f * progress : 0f;
        }
        public static Vector3 Direction(Vector3 toward)
            => Mathf.Abs(toward.x) >= Mathf.Abs(toward.z)
                ? (toward.x < 0f ? Vector3.left : Vector3.right)
                : (toward.z < 0f ? Vector3.back : Vector3.forward);
        public static float Plane(Bounds room, Vector3 direction, float progress, float exponent = 1f)
        {
            int axis = direction.x != 0f ? 0 : 2;
            float start = direction[axis] > 0f ? room.min[axis] : room.max[axis];
            float end = direction[axis] > 0f ? room.max[axis] : room.min[axis];
            return Mathf.Lerp(start, end, Mathf.Pow(Mathf.Clamp01(progress), Mathf.Max(.25f, exponent)));
        }
        public static bool Clip(Bounds cell, Vector3 direction, float plane, out Bounds consumed)
        {
            int axis = direction.x != 0f ? 0 : 2;
            Vector3 low = cell.min, high = cell.max;
            if (direction[axis] > 0f) high[axis] = Mathf.Clamp(plane, low[axis], high[axis]);
            else low[axis] = Mathf.Clamp(plane, low[axis], high[axis]);
            consumed = new Bounds((low + high) * .5f, high - low);
            return high[axis] - low[axis] > .0001f;
        }
        public static FloorHandProbe Probe(LevelRoom room, Vector3 direction, float consumption, float exponent,
            Vector3 feet, float reach)
        {
            if (consumption <= 0f || feet.y < room.Bounds.min.y || feet.y >= room.Bounds.max.y) return default;
            // No reaching into an absent cell in an L-shaped room.
            if (!room.ContainsXZ(feet) && feet.x >= room.Bounds.min.x && feet.x <= room.Bounds.max.x &&
                feet.z >= room.Bounds.min.z && feet.z <= room.Bounds.max.z) return default;
            int axis = direction.x != 0f ? 0 : 2;
            float plane = Plane(room.Bounds, direction, consumption, exponent);
            bool overtaken = room.ContainsXZ(feet) && (feet[axis] - plane) * direction[axis] <= 0f;
            float nearest = float.PositiveInfinity;
            Vector3 closest = default;
            foreach (var cell in room.Cells)
            {
                if (plane < cell.min[axis] || plane > cell.max[axis] || feet.y < cell.min.y || feet.y >= cell.max.y) continue;
                Vector3 point = new Vector3(Mathf.Clamp(feet.x, cell.min.x, cell.max.x), feet.y,
                    Mathf.Clamp(feet.z, cell.min.z, cell.max.z));
                point[axis] = plane;
                float distance = (feet - point).magnitude;
                if (distance < nearest) { nearest = distance; closest = point; }
            }
            if (float.IsInfinity(nearest) || (!overtaken && nearest > reach)) return default;
            // One logical front per room: travelling tangentially or crossing a cell seam
            // must not restart the warning. Penetrating the fog never counts as escape.
            return new FloorHandProbe(room.Id, 0, closest, overtaken ? 0f : nearest, true,
                direction, overtaken ? nearest : 0f, false, feet);
        }
        public static Vector3 Bounce(FloorHandProbe probe, float contactDistance, float speed)
            => probe.Available && probe.Closed && probe.Distance <= contactDistance
                ? probe.Outward.normalized * Mathf.Max(0f, speed) : Vector3.zero;

        public static List<Bounds> ClosedWalls(LevelRoom room, float thickness)
        {
            var walls = new List<Bounds>();
            foreach (var cell in room.Cells)
                for (int axis = 0; axis <= 2; axis += 2)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int across = 2 - axis;
                        float plane = side < 0 ? cell.min[axis] : cell.max[axis];
                        var spans = new List<Vector2> { new Vector2(cell.min[across], cell.max[across]) };
                        foreach (var other in room.Cells)
                        {
                            if (!(side > 0 ? other.min[axis] <= plane && other.max[axis] > plane :
                                other.min[axis] < plane && other.max[axis] >= plane)) continue;
                            // Only remove fully covered vertical seams. Different-height cells
                            // retain their full seal rather than leaving an unblocked opening.
                            if (other.min.y > cell.min.y || other.max.y < cell.max.y) continue;
                            for (int i = spans.Count - 1; i >= 0; i--)
                            {
                                var span = spans[i];
                                if (other.min[across] >= span.y || other.max[across] <= span.x) continue;
                                spans.RemoveAt(i);
                                if (other.min[across] > span.x) spans.Add(new Vector2(span.x, other.min[across]));
                                if (other.max[across] < span.y) spans.Add(new Vector2(other.max[across], span.y));
                            }
                        }
                        foreach (var span in spans)
                        {
                            Vector3 size = cell.size, center = cell.center;
                            size[axis] = Mathf.Min(Mathf.Max(.01f, thickness), cell.size[axis]);
                            size[across] = span.y - span.x;
                            center[axis] = plane - side * size[axis] * .5f;
                            center[across] = (span.x + span.y) * .5f;
                            walls.Add(new Bounds(center, size));
                        }
                    }
            return walls;
        }
    }
}
