// ============================================================================
// ProceduralOrganicShellPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts carved two-metre footprints into solid floors, roofs and boundaries.
//   Only exposed edges receive walls; portals cut the same 3.2m aperture used by
//   graph and spawn admission, including entrances on either leg of a hallway.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Tile organic rooms, cut ordinary doors and enclose circular leaf chambers.
//   - Keep every generated block owned by the original collapse room.
// DEPENDENCIES:
//   - Own immutable plans/configs and Unity value types; no engine calls.
// USAGE NOTES:
//   Round chambers use r4/30-degree sectors, each approximated by three collision
//   chords; excess tiled corner floor stays sealed behind those walls.
//   Coarse challenge/gap/pocket rooms remain with the legacy shell presenter.
//   Wall planes are shifted half a thickness inward to avoid coplanar shared faces.
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralOrganicShellPresenter
    {
        public IReadOnlyList<ProceduralBlock> Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            var blocks = new List<ProceduralBlock>();
            foreach (var room in layout.OrganicRooms)
            {
                float height = layout.Graph.Rooms[room.RoomId - 1].Size.y;
                var tiles = new HashSet<Vector2Int>(room.Tiles);
                var doors = layout.Doors.Where(d => d.FromRoomId == room.RoomId || d.ToRoomId == room.RoomId).ToArray();
                if (room.Shape == ProceduralRoomShape.Round) AddRound(blocks, room, height, driver.WallThickness);
                foreach (var tile in room.Tiles)
                {
                    var floor = ProceduralOrganicUtility.Center(layout, tile, -driver.FloorThickness * .5f);
                    blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Floor, floor,
                        new Vector3(2f, driver.FloorThickness, 2f)));
                    blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Ceiling,
                        new Vector3(floor.x, height + driver.CeilingThickness * .5f, floor.z),
                        new Vector3(2f, driver.CeilingThickness, 2f)));
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    {
                        if (tiles.Contains(tile + direction)) continue;
                        bool alongX = direction.x == 0;
                        var normal = new Vector3(direction.x, 0f, direction.y);
                        var center = new Vector3(floor.x, 0f, floor.z) + normal;
                        // The curved enclosure owns this boundary, not its tiled
                        // bounding box. Only the external door frame stays rectilinear.
                        if (room.Shape == ProceduralRoomShape.Round && !doors.Any(d => d.AlongX == alongX &&
                            Mathf.Abs(alongX ? d.Center.z - center.z : d.Center.x - center.x) < .001f)) continue;
                        float middle = alongX ? center.x : center.z;
                        float cursor = middle - 1f, end = middle + 1f;
                        foreach (var door in doors.Where(d => d.AlongX == alongX &&
                            Mathf.Abs(alongX ? d.Center.z - center.z : d.Center.x - center.x) < .001f)
                            .OrderBy(d => alongX ? d.Center.x : d.Center.z))
                        {
                            float opening = alongX ? door.Center.x : door.Center.z;
                            float low = Mathf.Max(cursor, opening - config.DoorWidth * .5f);
                            float high = Mathf.Min(end, opening + config.DoorWidth * .5f);
                            if (high <= low) continue;
                            Add(cursor, low, 0f); Add(low, high, config.DoorHeight); cursor = high;
                        }
                        Add(cursor, end, 0f);
                        void Add(float low, float high, float bottom)
                        {
                            if (high <= low) return;
                            var point = center + (alongX ? Vector3.right : Vector3.forward) * ((low + high) * .5f - middle)
                                - normal * (driver.WallThickness * .5f);
                            point.y = (height + bottom) * .5f;
                            blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall, point,
                                alongX ? new Vector3(high - low, height - bottom, driver.WallThickness) :
                                    new Vector3(driver.WallThickness, height - bottom, high - low)));
                        }
                    }
                }
            }
            return blocks.AsReadOnly();
        }

        private static void AddRound(List<ProceduralBlock> blocks, ProceduralOrganicRoom room, float height, float thickness)
        {
            var side = new Vector3(room.RoundFacing.z, 0f, -room.RoundFacing.x);
            Vector3 Point(float angle) => room.RoundCenter + 4f * (room.RoundFacing * Mathf.Cos(angle * Mathf.Deg2Rad) +
                side * Mathf.Sin(angle * Mathf.Deg2Rad));
            // The missing sixty-degree sector is a 4m mouth for the ordinary vestibule.
            for (int angle = 30; angle < 330; angle += 10) Wall(Point(angle), Point(angle + 10));
            Wall(Point(30), room.RoundCenter + room.RoundFacing * 8f + side * 2f);
            Wall(Point(330), room.RoundCenter + room.RoundFacing * 8f - side * 2f);
            void Wall(Vector3 a, Vector3 b)
            {
                var along = b - a;
                blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall,
                    (a + b) * .5f + Vector3.up * (height * .5f),
                    new Vector3(along.magnitude + thickness * .1f, height, thickness),
                    rotation: Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up)));
            }
        }
    }
}
