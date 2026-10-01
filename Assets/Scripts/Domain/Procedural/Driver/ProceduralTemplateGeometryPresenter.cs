// ============================================================================
// ProceduralTemplateGeometryPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts authored kit placements into the same collision commands used by the
//   existing navigation and collapse pipeline. Missing art never removes support:
//   every command carries primitive fallback geometry alongside optional kit identity.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Tile template floors and ceilings and preserve authored prop placements.
//   - Cut only connected sockets and seal all unused openings.
//   - Emit arc-piece identities with conservative chord collision.
// DEPENDENCIES:
//   - Own catalogue/layout/config and pure coordinate/validation utilities.
// USAGE NOTES:
//   A changed wall fragment uses primitives, never a stretched wall prefab.
//   Authored props use their manifest bounding boxes for collision and navigation.
//   ClosedWith alternatives are room-local placements, like the pieces array.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralTemplateGeometryPresenter
    {
        public IReadOnlyList<ProceduralBlock> Build(ProceduralTemplateCatalogue catalogue, ProceduralTemplateRoom room,
            ProceduralConfig config, ProceduralDriverConfig driver)
            => Build(new ProceduralLayout { TemplateCatalogue = catalogue, TemplateRooms = new[] { room } }, config, driver);

        public IReadOnlyList<ProceduralBlock> Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            var blocks = new List<ProceduralBlock>();
            var kit = layout.TemplateCatalogue.Kit.ToDictionary(p => p.Id);
            foreach (var room in layout.TemplateRooms)
            {
                Vector3 World(Vector3 p) => ProceduralTemplateUtility.Point(room, p, layout.Origin);
                foreach (var cell in room.Template.Footprint)
                {
                    var center = World(new Vector3(cell.x * 2f + 1f, 0f, cell.y * 2f + 1f));
                    var floor = room.Template.Pieces.FirstOrDefault(p => kit[p.Id].Kind == "floor" &&
                        Mathf.Abs(p.Position.x - (cell.x * 2f + 1f)) < .001f && Mathf.Abs(p.Position.z - (cell.y * 2f + 1f)) < .001f);
                    var ceiling = room.Template.Pieces.FirstOrDefault(p => kit[p.Id].Kind == "ceiling" &&
                        Mathf.Abs(p.Position.x - (cell.x * 2f + 1f)) < .001f && Mathf.Abs(p.Position.z - (cell.y * 2f + 1f)) < .001f);
                    blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Floor, center - Vector3.up * (driver.FloorThickness * .5f),
                        new Vector3(2f, driver.FloorThickness, 2f), rotation: Quaternion.Euler(0f, room.Turns * 90f + (floor?.RotY ?? 0f), 0f),
                        pieceId: floor?.Id ?? "floor_2x2", piecePosition: floor == null ? center - Vector3.up * driver.FloorThickness : World(floor.Position)));
                    center.y = room.Template.Height;
                    blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Ceiling, center + Vector3.up * (driver.CeilingThickness * .5f),
                        new Vector3(2f, driver.CeilingThickness, 2f), rotation: Quaternion.Euler(0f, room.Turns * 90f + (ceiling?.RotY ?? 0f), 0f),
                        pieceId: ceiling?.Id ?? "ceiling_2x2", piecePosition: ceiling == null ? center : World(ceiling.Position)));
                }
                var placements = room.Template.Pieces.ToList();
                foreach (int i in Enumerable.Range(0, room.Template.Doors.Length).Where(i => !room.OpenDoors.Contains(i)))
                {
                    var door = room.Template.Doors[i];
                    if (door.ClosedWith != null) placements.AddRange(door.ClosedWith);
                }
                foreach (var placement in placements)
                {
                    var piece = kit[placement.Id];
                    if (piece.Kind == "floor" || piece.Kind == "ceiling") continue; // One exact tile per footprint cell.
                    if (!ProceduralTemplateValidationUtility.Wall(piece))
                    {
                        var rotation = Quaternion.Euler(0f, room.Turns * 90f + placement.RotY, 0f);
                        blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall,
                            World(placement.Position) + Vector3.up * (piece.Size.y * .5f), piece.Size, rotation: rotation,
                            pieceId: piece.Id, piecePosition: World(placement.Position)));
                        continue;
                    }
                    var line = ProceduralTemplateValidationUtility.Segment(placement, piece);
                    if (piece.Kind == "door" && room.Template.Doors.Where((d, i) => !room.OpenDoors.Contains(i))
                        .Any(d => d.ClosedWith != null && d.ClosedWith.Length != 0 &&
                            (ProceduralTemplateUtility.Door(d) - placement.Position).sqrMagnitude < .001f)) continue;
                    Vector3 a = World(line.a), b = World(line.b); var tangent = (b - a).normalized;
                    float length = Vector3.Distance(a, b), cursor = 0f;
                    var openings = room.OpenDoors.Select(i => World(ProceduralTemplateUtility.Door(room.Template.Doors[i])))
                        .Where(p => Mathf.Abs(Vector3.Cross(tangent, p - a).y) < .001f)
                        .Select(p => Vector3.Dot(p - a, tangent))
                        .Where(p => p + config.DoorWidth * .5f > 0f && p - config.DoorWidth * .5f < length).OrderBy(p => p).ToArray();
                    if (openings.Length == 0)
                    {
                        Add(0f, length, 0f, piece.Kind == "door" ? null : piece.Id, World(placement.Position));
                        continue;
                    }
                    foreach (float opening in openings)
                    {
                        float low = Mathf.Max(cursor, opening - config.DoorWidth * .5f), high = Mathf.Min(length, opening + config.DoorWidth * .5f);
                        if (high <= low) continue;
                        Add(cursor, low, 0f);
                        Add(low, high, config.DoorHeight, piece.Kind == "door" ? piece.Id : null, World(placement.Position)); cursor = high;
                    }
                    Add(cursor, length, 0f);
                    void Add(float low, float high, float bottom, string id = null, Vector3? pivot = null)
                    {
                        if (high - low <= .0001f) return;
                        float height = room.Template.Height - bottom;
                        blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall,
                            a + tangent * ((low + high) * .5f) + Vector3.up * ((room.Template.Height + bottom) * .5f),
                            new Vector3(high - low, height, driver.WallThickness),
                            rotation: Quaternion.Euler(0f, room.Turns * 90f + placement.RotY, 0f), pieceId: id, piecePosition: pivot));
                    }
                }
                // Door-only gaps have no ordinary wall placements. Seal the socket
                // when unused; open sockets retain jamb/header primitive geometry.
                for (int i = 0; i < room.Template.Doors.Length; i++)
                {
                    var door = room.Template.Doors[i]; var center = World(ProceduralTemplateUtility.Door(door));
                    var normal = ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(door.Side), room.Turns);
                    bool alongX = normal.x == 0;
                    var tangent = alongX ? Vector3.right : Vector3.forward;
                    for (int part = 0; part < 2; part++)
                    {
                        var position = center + tangent * (part == 0 ? -1f : 1f);
                        bool covered = blocks.Any(b => b.RoomId == room.RoomId && b.Kind == ProceduralSurfaceKind.Wall &&
                            Contains(b, position + Vector3.up * (room.OpenDoors.Contains(i) ? room.Template.Height - .1f : 1f)));
                        if (covered) continue;
                        if (!room.OpenDoors.Contains(i))
                            blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall, position + Vector3.up * (room.Template.Height * .5f),
                                new Vector3(2f, room.Template.Height, driver.WallThickness), rotation: Quaternion.Euler(0f,
                                    normal.x == 0 ? (normal.y > 0 ? 0f : 180f) : (normal.x > 0 ? 90f : 270f), 0f),
                                pieceId: "wall_2m", piecePosition: position));
                        else
                        {
                            float height = room.Template.Height - config.DoorHeight;
                            blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall, position + Vector3.up * (config.DoorHeight + height * .5f),
                                alongX ? new Vector3(2f, height, driver.WallThickness) : new Vector3(driver.WallThickness, height, 2f)));
                            float jamb = (4f - config.DoorWidth) * .5f;
                            var jambPosition = center + tangent * ((part == 0 ? -1f : 1f) * (2f - jamb * .5f));
                            blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall,
                                jambPosition + Vector3.up * (config.DoorHeight * .5f), alongX ?
                                new Vector3(jamb, config.DoorHeight, driver.WallThickness) : new Vector3(driver.WallThickness, config.DoorHeight, jamb)));
                        }
                    }
                }
            }
            if (layout.Graph != null)
            {
                var exit = new Bounds(layout.Graph.ExitPosition + Vector3.up * 1.5f,
                    new Vector3(config.TemplateExitClearance * 2f, 3f, config.TemplateExitClearance * 2f));
                foreach (var block in blocks.Where(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Wall))
                {
                    var bounds = new Bounds(block.Center, Vector3.zero);
                    for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                        bounds.Encapsulate(block.Center + block.Rotation * Vector3.Scale(block.Size, new Vector3(x, y, z)) * .5f);
                    if (bounds.Intersects(exit)) throw new InvalidOperationException("Template exit-door envelope intersects kit collision.");
                }
            }
            return blocks.AsReadOnly();
        }
        private static bool Contains(ProceduralBlock block, Vector3 point)
            => new Bounds(Vector3.zero, block.Size).Contains(Quaternion.Inverse(block.Rotation) * (point - block.Center));
    }
}
