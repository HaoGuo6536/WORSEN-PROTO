// ============================================================================
// ProceduralTemplateUtility.cs
// ============================================================================
// PURPOSE:
//   Provides the exact coordinate and boundary model shared by template admission,
//   seeded placement and shell construction. Door centers follow the authored
//   boundary-cell span, and quarter turns rotate cells and authored metre data.
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
//   All current art sockets use 4m frames with 3.2m clear throats, including span 1.
//   Placement occupancy uses 1m subcells; authored tiles are never rescaled.
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
            => Rotate(local, room.Turns) + new Vector3(room.Offset.x * 2f + room.SubcellOffset.x + origin.x,
                0f, room.Offset.y * 2f + room.SubcellOffset.y + origin.y);
        public static float SocketWidth(ProceduralTemplateDoor door)
        {
            if (door.Span < 1 || door.Span > 2) throw new ArgumentException("Door span must be 1 or 2 boundary cells.");
            return door.Span * 2f;
        }
        public static Vector3 Door(ProceduralTemplateDoor door)
        {
            var normal = Direction(door.Side);
            float shift = SocketWidth(door) * .5f - 1f;
            return new Vector3(door.Cell.x * 2f + 1f + normal.x + (normal.x == 0 ? shift : 0f),
                0f, door.Cell.y * 2f + 1f + normal.y + (normal.y == 0 ? shift : 0f));
        }
        public static IEnumerable<Vector2Int> OccupiedCells(ProceduralTemplateRoom room)
        {
            foreach (var cell in room.Template.Footprint)
            {
                var corner = (Cell(cell, room.Turns) + room.Offset) * 2 + room.SubcellOffset;
                for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++)
                    yield return corner + new Vector2Int(x, z);
            }
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
        public static Bounds[] Volumes(IEnumerable<Vector2Int> cells, Vector2 origin, float height, float cellSize = 2f)
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
                result.Add(new Bounds(new Vector3(origin.x + (start.x + width * .5f) * cellSize, height * .5f,
                    origin.y + (start.y + depth * .5f) * cellSize), new Vector3(width * cellSize, height, depth * cellSize)));
            }
            return result.ToArray();
        }
    }
}
