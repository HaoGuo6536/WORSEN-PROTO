// ============================================================================
// ProceduralTemplateUtility.cs
// ============================================================================
// PURPOSE:
//   Provides the exact coordinate and boundary model shared by template admission,
//   seeded placement and shell construction. Door centers are cell-edge midpoints,
//   not tile corners, and quarter turns rotate both cells and authored metre data.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Transform cells, points and door normals without engine calls.
//   - Enumerate exposed edges and test authored anchor clearance.
//   - Merge tiles into nonoverlapping rectangles for Core room consumers.
// DEPENDENCIES:
//   - Own definitions and Unity value types only.
// USAGE NOTES:
//   The origin is a corner: a rotated cell's lower corner is not its rotated center.
//   Boundary clearance is horizontal, allowing floor-level gameplay anchors.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralTemplateUtility
    {
        public static Vector2Int Direction(string side)
        {
            switch (side)
            {
                case "N": return Vector2Int.up;
                case "E": return Vector2Int.right;
                case "S": return Vector2Int.down;
                case "W": return Vector2Int.left;
                default: throw new ArgumentException("Door side must be N, E, S or W.");
            }
        }
        public static Vector2Int Rotate(Vector2Int value, int turns)
        {
            for (int i = 0; i < turns; i++) value = new Vector2Int(value.y, -value.x);
            return value;
        }
        public static Vector3 Rotate(Vector3 value, int turns)
        {
            for (int i = 0; i < turns; i++) value = new Vector3(value.z, value.y, -value.x);
            return value;
        }
        public static Vector2Int Cell(Vector2Int value, int turns)
        {
            var doubled = Rotate(value * 2 + Vector2Int.one, turns) - Vector2Int.one;
            return new Vector2Int(doubled.x / 2, doubled.y / 2);
        }
        public static Vector3 Point(ProceduralTemplateRoom room, Vector3 local, Vector2 origin)
            => Rotate(local, room.Turns) + new Vector3(room.Offset.x * 2f + origin.x, 0f, room.Offset.y * 2f + origin.y);
        public static Vector3 Door(ProceduralTemplateDoor door)
        {
            var normal = Direction(door.Side);
            return new Vector3(door.Cell.x * 2f + 1f + normal.x, 0f, door.Cell.y * 2f + 1f + normal.y);
        }
        public static IEnumerable<Vector2Int> Directions()
        { yield return Vector2Int.up; yield return Vector2Int.right; yield return Vector2Int.down; yield return Vector2Int.left; }
        public static IEnumerable<(Vector3 center, Vector2Int normal)> Boundary(ProceduralRoomTemplate room)
        {
            var cells = new HashSet<Vector2Int>(room.Footprint);
            foreach (var cell in room.Footprint)
            foreach (var direction in Directions())
                if (!cells.Contains(cell + direction))
                    yield return (new Vector3(cell.x * 2f + 1f + direction.x, 0f, cell.y * 2f + 1f + direction.y), direction);
        }
        public static bool Inside(ProceduralRoomTemplate room, Vector3 point, float clearance = 0f)
        {
            if (!Finite(point) || point.y < 0f || point.y > room.Height ||
                !room.Footprint.Any(c => point.x >= c.x * 2f && point.x <= c.x * 2f + 2f &&
                    point.z >= c.y * 2f && point.z <= c.y * 2f + 2f)) return false;
            foreach (var edge in Boundary(room))
            {
                var along = edge.normal.x == 0 ? Vector3.right : Vector3.forward;
                var relative = point - edge.center; relative.y = 0f;
                float t = Mathf.Clamp(Vector3.Dot(relative, along), -1f, 1f);
                if ((relative - along * t).sqrMagnitude < clearance * clearance - .00001f) return false;
            }
            return true;
        }
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        public static Bounds[] Volumes(IEnumerable<Vector2Int> cells, Vector2 origin, float height)
        {
            var sorted = cells.OrderBy(c => c.x).ThenBy(c => c.y).ToArray();
            var remaining = new HashSet<Vector2Int>(sorted); var result = new List<Bounds>();
            foreach (var start in sorted)
            {
                if (!remaining.Contains(start)) continue;
                int width = 1, depth = 1;
                while (remaining.Contains(start + new Vector2Int(width, 0))) width++;
                while (Enumerable.Range(0, width).All(x => remaining.Contains(start + new Vector2Int(x, depth)))) depth++;
                for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++) remaining.Remove(start + new Vector2Int(x, z));
                result.Add(new Bounds(new Vector3(origin.x + start.x * 2f + width, height * .5f, origin.y + start.y * 2f + depth),
                    new Vector3(width * 2f, height, depth * 2f)));
            }
            return result.ToArray();
        }
    }
}
