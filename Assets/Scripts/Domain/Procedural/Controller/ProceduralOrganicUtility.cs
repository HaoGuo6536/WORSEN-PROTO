// ============================================================================
// ProceduralOrganicUtility.cs
// ============================================================================
// PURPOSE:
//   Refines coarse reservations into connected two-metre room footprints.
//   Offset chambers, round enclosures, L/T plans and bent four-metre corridors keep
//   graph identity and portal positions, so collapse and pocket routes stay stable.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Carve bounded footprints without crossing reserved room or gap boundaries.
//   - Connect every portal and internal cell seam with ordinary walking space.
//   - Retain candidate density and publish non-overlapping rectangular cell facts.
//   - Describe deterministic safe spawn candidates and manifest shape records.
// DEPENDENCIES:
//   - Own configuration and Core immutable graph values only.
// USAGE NOTES:
//   Hub, pocket, challenge and gap-facing reservations retain their authored
//   footprints. Organic content requires shared challenge pacing to be wired.
//   Circular leaf rooms keep tiled floors behind their curved enclosure, like
//   floor beneath other interior walls. Candidate/spawn sockets exclude that void.
//   All enumeration affecting a seed is sorted; no hash iteration chooses content.
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
    public static class ProceduralOrganicUtility
    {
        public static void Apply(ProceduralLayout layout, ProceduralConfig config, System.Random random)
        {
            var tuning = config.Organic;
            if (tuning == null) return;
            if (config.Challenges == null || config.RoomSize != 12f || config.DoorWidth != 3.2f || config.DoorOffset != 2f)
                throw new ArgumentException("Organic refinement needs challenge pacing, 12m reservations and the 2m/3.2m kit contract.");
            foreach (float value in new[] { tuning.HallwayProbability, tuning.SmallRoomProbability, tuning.IrregularProbability, tuning.RoundProbability })
                if (!(value >= 0f && value <= 1f)) throw new ArgumentException("Invalid organic shape probability.");
            var rooms = layout.Graph.Rooms.ToArray();
            var anchors = layout.Graph.Anchors.ToArray();
            var shapes = new List<ProceduralOrganicRoom>();
            int spawnRoomId = ProceduralFootprintUtility.At(layout, layout.PlayerSpawnPosition).Id;
            foreach (var module in layout.Modules)
            {
                // Preserve a spacious optional challenge/threshold host in the walking loop.
                if (module.RoomId == 6 || module.RoomId == spawnRoomId || module.RoomId == layout.Graph.ExitRoomId || module.PocketId != 0 ||
                    module.Kind == ProceduralModuleKind.MerchantRefuge || module.TraversalObstacles ||
                    module.Cells.Any(c => Directions().Any(d => layout.GapCells.Contains(c + d)))) continue;
                var room = rooms[module.RoomId - 1];
                var portals = layout.Doors.Where(d => d.FromRoomId == room.Id || d.ToRoomId == room.Id).ToArray();
                ProceduralRoomShape shape = random.NextDouble() < tuning.HallwayProbability ? ProceduralRoomShape.Hallway :
                    random.NextDouble() < tuning.IrregularProbability ?
                        (random.Next(2) == 0 ? ProceduralRoomShape.LShape : ProceduralRoomShape.TShape) : ProceduralRoomShape.Rectangle;
                Vector3 roundCenter = default, roundFacing = default;
                if (shape != ProceduralRoomShape.Hallway && module.Cells.Count == 1 && portals.Length == 1 &&
                    random.NextDouble() < tuning.RoundProbability)
                {
                    shape = ProceduralRoomShape.Round;
                    var door = portals[0]; var delta = door.Center - room.Center;
                    roundFacing = door.AlongX ? Vector3.forward * Math.Sign(delta.z) : Vector3.right * Math.Sign(delta.x);
                    roundCenter = door.Center - roundFacing * 8f;
                }
                var tiles = new HashSet<Vector2Int>();
                foreach (var cell in module.Cells)
                {
                    var origin = cell * 6 - new Vector2Int(3, 3);
                    var local = new HashSet<Vector2Int>();
                    int width = random.NextDouble() < tuning.SmallRoomProbability ? 2 : random.Next(2) == 0 ? 4 : 6;
                    int depth = width == 2 ? 3 : width;
                    int x = random.Next(7 - width), z = random.Next(7 - depth);
                    var junction = new Vector2Int(Math.Min(4, x), Math.Min(4, z));
                    if (shape == ProceduralRoomShape.Round)
                    {
                        junction = new Vector2Int(Mathf.RoundToInt((roundCenter.x - layout.Origin.x) / 2f) - origin.x - 1,
                            Mathf.RoundToInt((roundCenter.z - layout.Origin.y) / 2f) - origin.y - 1);
                        Rect(local, junction.x - 1, junction.y - 1, 4, 4);
                    }
                    else if (shape == ProceduralRoomShape.Rectangle) Rect(local, x, z, width, depth);
                    else if (shape == ProceduralRoomShape.LShape)
                    { Rect(local, 0, 0, 2, 6); Rect(local, 0, 0, 6, 2); junction = Vector2Int.zero; }
                    else if (shape == ProceduralRoomShape.TShape)
                    { Rect(local, 0, 4, 6, 2); Rect(local, 2, 0, 2, 6); junction = new Vector2Int(2, 2); }
                    else
                    {
                        junction = new Vector2Int(3, 2);
                        Join(local, new Vector2Int(0, 0), junction, true);
                        Join(local, junction, new Vector2Int(4, 4), false);
                    }
                    foreach (var direction in Directions())
                        if (module.Cells.Contains(cell + direction))
                            Join(local, Port(direction, 3), junction, direction.x == 0);
                    var coarse = new Bounds(new Vector3(layout.Origin.x + cell.x * 12f, room.Center.y,
                        layout.Origin.y + cell.y * 12f), new Vector3(12f, room.Size.y, 12f));
                    foreach (var door in layout.Doors.Where(d => (d.FromRoomId == room.Id || d.ToRoomId == room.Id) && coarse.Contains(d.Center)))
                    {
                        var delta = door.Center - coarse.center;
                        var direction = door.AlongX ? new Vector2Int(0, Math.Sign(delta.z)) : new Vector2Int(Math.Sign(delta.x), 0);
                        int along = Mathf.RoundToInt(((door.AlongX ? delta.x : delta.z) + 6f) / 2f);
                        Join(local, Port(direction, along), junction, direction.x == 0);
                    }
                    foreach (var tile in local) tiles.Add(origin + tile);
                }
                var sorted = tiles.OrderBy(t => t.x).ThenBy(t => t.y).ToArray();
                var cells = Merge(sorted, layout.Origin, room.Size.y);
                var bounds = cells[0]; foreach (var cell in cells.Skip(1)) bounds.Encapsulate(cell);
                rooms[module.RoomId - 1] = new LevelRoom(room.Id, bounds.center, bounds.size, cells: cells, pocket: room.Pocket);
                var organic = new ProceduralOrganicRoom(room.Id, shape, Array.AsReadOnly(sorted), roundCenter, roundFacing);
                shapes.Add(organic);
                var available = sorted.Select(t => Center(layout, t, config.AnchorHeight)).Where(p => Clear(organic, p)).ToList();
                var selected = new List<Vector3>();
                foreach (int index in Enumerable.Range(0, anchors.Length).Where(i => anchors[i].RoomId == room.Id))
                {
                    // Maximal separation on supported tile centers preserves density even in closets.
                    var point = available.OrderByDescending(p => selected.Count == 0 ? (p - room.Center).sqrMagnitude :
                        selected.Min(s => (p - s).sqrMagnitude)).ThenBy(p => p.x).ThenBy(p => p.z).First();
                    var old = anchors[index];
                    anchors[index] = new LevelAnchor(old.Id, old.RoomId, old.Type, point);
                    selected.Add(point); available.Remove(point);
                }
            }
            layout.OrganicRooms = shapes.AsReadOnly();
            layout.Graph = LevelGraphUtility.Build(rooms, layout.Graph.Edges, anchors, layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
        }

        public static Vector3 Center(ProceduralLayout layout, Vector2Int tile, float y)
            => new Vector3(layout.Origin.x + tile.x * 2f + 1f, y, layout.Origin.y + tile.y * 2f + 1f);

        public static IReadOnlyList<Vector3> SpawnCandidates(ProceduralLayout layout, IReadOnlyList<Vector3> legacy)
        {
            if (layout.OrganicRooms.Count == 0) return legacy;
            var result = new List<Vector3>();
            foreach (var point in legacy)
            {
                int roomId = ProceduralFootprintUtility.At(layout, point).Id;
                if (roomId != 0 && layout.OrganicRooms.All(r => r.RoomId != roomId || Clear(r, point))) result.Add(point);
            }
            foreach (var shape in layout.OrganicRooms)
            {
                var anchors = layout.Graph.Anchors.Where(a => a.RoomId == shape.RoomId).ToArray();
                result.AddRange(shape.Tiles.Select(t => Center(layout, t, layout.PlayerSpawnPosition.y))
                    .Where(p => Clear(shape, p))
                    .OrderByDescending(p => anchors.Min(a => (p - a.Position).sqrMagnitude)).Take(3));
            }
            return result.Distinct().ToArray();
        }

        public static bool Clear(ProceduralOrganicRoom room, Vector3 point)
        {
            if (room.Shape != ProceduralRoomShape.Round) return true;
            var delta = point - room.RoundCenter; delta.y = 0f;
            var tangent = new Vector3(room.RoundFacing.z, 0f, -room.RoundFacing.x);
            float forward = Vector3.Dot(delta, room.RoundFacing), across = Mathf.Abs(Vector3.Dot(delta, tangent));
            return delta.sqrMagnitude <= 3.3f * 3.3f || (forward >= 0f && forward <= 8f && across <= 1.3f);
        }

        public static string Manifest(ProceduralLayout layout)
        {
            var text = new StringBuilder();
            foreach (var room in layout.OrganicRooms)
            {
                text.Append("|Organic:").Append(room.RoomId).Append(',').Append(room.Shape);
                if (room.Shape == ProceduralRoomShape.Round)
                    foreach (float value in new[] { room.RoundCenter.x, room.RoundCenter.z, room.RoundFacing.x, room.RoundFacing.z })
                        text.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
                foreach (var tile in room.Tiles) text.Append(';').Append(tile.x.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(tile.y.ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private static Vector2Int Port(Vector2Int direction, int along) => direction.x == 0 ?
            new Vector2Int(along - 1, direction.y > 0 ? 4 : 0) : new Vector2Int(direction.x > 0 ? 4 : 0, along - 1);
        private static void Join(HashSet<Vector2Int> cells, Vector2Int from, Vector2Int to, bool xFirst)
        {
            Rect(cells, from.x, from.y, 2, 2);
            while (from != to)
            {
                if ((xFirst && from.x != to.x) || from.y == to.y) from.x += Math.Sign(to.x - from.x);
                else from.y += Math.Sign(to.y - from.y);
                Rect(cells, from.x, from.y, 2, 2);
            }
        }
        private static void Rect(HashSet<Vector2Int> cells, int x, int z, int width, int depth)
        { for (int i = x; i < x + width; i++) for (int j = z; j < z + depth; j++) cells.Add(new Vector2Int(i, j)); }
        private static IEnumerable<Vector2Int> Directions()
        { yield return Vector2Int.right; yield return Vector2Int.up; yield return Vector2Int.left; yield return Vector2Int.down; }
        private static Bounds[] Merge(IReadOnlyList<Vector2Int> sorted, Vector2 origin, float height)
        {
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
