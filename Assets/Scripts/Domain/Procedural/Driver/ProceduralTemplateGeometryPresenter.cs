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
//   - Preserve offset frames, radial arcs and separate tangent-pier collision.
//   - Keep decals and opened leaves out of collision and navigation.
//   - Emit authored low props as Vault traversal blocks without rotating endpoints twice.
// DEPENDENCIES:
//   - Own catalogue/layout/config and pure coordinate/validation utilities.
// USAGE NOTES:
//   A changed wall fragment uses primitives, never a stretched wall prefab.
//   Authored props use their manifest bounding boxes for collision and navigation.
//   ClosedWith alternatives are room-local placements, like the pieces array.
//   Span controls the socket centre, not the authored 4m frame/3.2m aperture.
//   Compound pieces have one kit visual and bounded primitive fallback parts.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

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
                        new Vector3(2f, driver.FloorThickness, 2f), rotation: Yaw(room.Turns * 90f + (floor?.RotY ?? 0f)),
                        pieceId: floor?.Id ?? "floor_2x2", piecePosition: floor == null ? center - Vector3.up * driver.FloorThickness : World(floor.Position)));
                    center.y = room.Template.Height;
                    blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Ceiling, center + Vector3.up * (driver.CeilingThickness * .5f),
                        new Vector3(2f, driver.CeilingThickness, 2f), rotation: Yaw(room.Turns * 90f + (ceiling?.RotY ?? 0f)),
                        pieceId: ceiling?.Id ?? "ceiling_2x2", piecePosition: ceiling == null ? center : World(ceiling.Position)));
                }
                var placements = room.Template.Pieces.ToList();
                foreach (int i in Enumerable.Range(0, room.Template.Doors.Length).Where(i => !room.OpenDoors.Contains(i)))
                {
                    var door = room.Template.Doors[i];
                    if (door.ClosedWith != null) placements.AddRange(door.ClosedWith);
                }
                for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
                {
                    var placement = placements[placementIndex];
                    var piece = kit[placement.Id];
                    if (piece.Kind == "floor" || piece.Kind == "ceiling") continue; // One exact tile per footprint cell.
                    float yaw = room.Turns * 90f + placement.RotY;
                    if (layout.TemplateCatalogue.Theme == "castle" && kit.TryGetValue("wall_2m", out var masonry) &&
                        Math.Abs(masonry.Size.z - .8f) < .00001f && (piece.Kind == "arc" || piece.Id == "wall_round_tangent_r4"))
                    {
                        Masonry(blocks, room.RoomId, piece, World(placement.Position), yaw);
                        continue;
                    }
                    if (!ProceduralTemplateValidationUtility.Wall(piece))
                    {
                        var pivot = World(placement.Position);
                        var rotation = Yaw(yaw);
                        bool visual = piece.Kind == "decal";
                        if (Leaf(piece.Id))
                        {
                            int socket = LeafSocket(room.Template, placement, kit);
                            if (socket >= 0)
                            {
                                if (!room.OpenDoors.Contains(socket)) continue; // Replaced by closedWith, not a leaf buried in a wall.
                                visual = true;
                                if (piece.Id != "door_double_porthole_4m")
                                {
                                    // Hospital's paired leaves are already modelled open. Other
                                    // meshes pivot around their outer stile, never around their centre.
                                    var center = SocketPivot(room.Template.Doors[socket], kit);
                                    float along = (Inverse(Yaw(placement.RotY)) * (placement.Position - center)).x;
                                    float sign = along > .001f ? 1f : -1f;
                                    var hinge = pivot + rotation * Vector3.right * (sign * piece.Size.x * .5f);
                                    rotation = Yaw(yaw - sign * 90f);
                                    pivot = hinge - rotation * Vector3.right * (sign * piece.Size.x * .5f);
                                }
                            }
                        }
                        blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall,
                            pivot + Vector3.up * (piece.Size.y * .5f), piece.Size,
                            surfaceId: placement.TraversalKind == TraversalSurfaceKind.Vault ? checked(1000000 + room.RoomId * 16384 + placementIndex) : 0,
                            traversalKind: placement.TraversalKind,
                            endpointA: placement.HasEndpoints ? World(placement.EndpointA) : default,
                            endpointB: placement.HasEndpoints ? World(placement.EndpointB) : default, rotation: rotation,
                            role: visual ? ProceduralBlockRole.VisualOnly : ProceduralBlockRole.Solid,
                            pieceId: piece.Id, piecePosition: pivot));
                        continue;
                    }
                    if (piece.Kind == "door")
                    {
                        int socket = FrameSocket(room.Template, placement, piece, kit);
                        bool open = socket < 0 || room.OpenDoors.Contains(socket); // Interior arches stay arches.
                        if (!open && room.Template.Doors[socket].ClosedWith?.Length > 0) continue;
                        Frame(blocks, room.RoomId, World(placement.Position), yaw, piece.Size, config.DoorWidth,
                            config.DoorHeight, open, open ? piece.Id : null);
                        continue;
                    }
                    var line = ProceduralTemplateValidationUtility.Segment(placement, piece);
                    Vector3 a = World(line.a), b = World(line.b); var tangent = (b - a).normalized;
                    float length = Vector3.Distance(a, b), cursor = 0f;
                    var openings = room.OpenDoors.Select(i => room.Template.Doors[i])
                        .Where(d => Math.Abs(Vector3.Dot(tangent, ProceduralTemplateUtility.Rotate(
                            new Vector3(ProceduralTemplateUtility.Direction(d.Side).x, 0f, ProceduralTemplateUtility.Direction(d.Side).y), room.Turns))) < .001f)
                        .Select(d => World(ProceduralTemplateUtility.Door(d)))
                        .Where(p => Math.Abs(Vector3.Cross(tangent, p - a).y) <= piece.Size.z * .5f + .001f)
                        .Select(p => Vector3.Dot(p - a, tangent))
                        .Where(p => p + config.DoorWidth * .5f > 0f && p - config.DoorWidth * .5f < length).OrderBy(p => p).ToArray();
                    if (openings.Length == 0)
                    {
                        Add(0f, length, 0f, piece.Id, World(placement.Position));
                        continue;
                    }
                    foreach (float opening in openings)
                    {
                        float low = Mathf.Max(cursor, opening - config.DoorWidth * .5f), high = Mathf.Min(length, opening + config.DoorWidth * .5f);
                        if (high <= low) continue;
                        Add(cursor, low, 0f);
                        Add(low, high, config.DoorHeight); cursor = high;
                    }
                    Add(cursor, length, 0f);
                    void Add(float low, float high, float bottom, string id = null, Vector3? pivot = null)
                    {
                        if (high - low <= .0001f) return;
                        float height = room.Template.Height - bottom;
                        if (height <= .0001f) return;
                        blocks.Add(new ProceduralBlock(room.RoomId, ProceduralSurfaceKind.Wall,
                            a + tangent * ((low + high) * .5f) + Vector3.up * ((room.Template.Height + bottom) * .5f),
                            new Vector3(high - low, height, piece.Kind == "arc" ? driver.WallThickness : piece.Size.z),
                            rotation: Yaw(yaw), pieceId: id, piecePosition: pivot));
                    }
                }
                // Legacy socket-only gaps still need a shell. Authored frames or
                // closedWith replacements already own their complete wall interval.
                for (int i = 0; i < room.Template.Doors.Length; i++)
                {
                    var door = room.Template.Doors[i];
                    var center = World(ProceduralTemplateUtility.Door(door));
                    bool covered = blocks.Any(b => b.RoomId == room.RoomId && b.HasCollision && b.Kind == ProceduralSurfaceKind.Wall &&
                        Contains(b, center + Vector3.up * (room.Template.Height - .1f)));
                    if (covered || door.ClosedWith?.Length > 0 || room.Template.Pieces.Any(p => kit[p.Id].Kind == "door" &&
                        FrameSocket(room.Template, p, kit[p.Id], kit) == i)) continue;
                    var normal = ProceduralTemplateUtility.Direction(door.Side);
                    float yaw = normal.x == 0 ? (normal.y > 0 ? 0f : 180f) : (normal.x > 0 ? 90f : 270f);
                    Frame(blocks, room.RoomId, center, yaw + room.Turns * 90f,
                        new Vector3(4f, room.Template.Height, driver.WallThickness), config.DoorWidth, config.DoorHeight, room.OpenDoors.Contains(i));
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
                    if (bounds.min.x <= exit.max.x && bounds.max.x >= exit.min.x && bounds.min.y <= exit.max.y &&
                        bounds.max.y >= exit.min.y && bounds.min.z <= exit.max.z && bounds.max.z >= exit.min.z)
                        throw new InvalidOperationException("Template exit-door envelope intersects kit collision.");
                }
            }
            return blocks.AsReadOnly();
        }
        private static Quaternion Yaw(float degrees)
        {
            double half = degrees * Math.PI / 360d;
            return new Quaternion(0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half));
        }
        private static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        private static bool Contains(ProceduralBlock block, Vector3 point)
        {
            var p = Inverse(block.Rotation) * (point - block.Center);
            return Math.Abs(p.x) <= block.Size.x * .5f + .001f && Math.Abs(p.y) <= block.Size.y * .5f + .001f &&
                Math.Abs(p.z) <= block.Size.z * .5f + .001f;
        }
        private static bool Leaf(string id) => id == "door_iron_strapped" || id == "door_double_porthole_4m" ||
            id == "prop_classroom_door_leaf" || id == "prop_bulkhead_leaf";
        private static Vector3 SocketPivot(ProceduralTemplateDoor door, Dictionary<string, ProceduralKitPiece> kit)
        {
            var closed = door.ClosedWith;
            if (closed == null || closed.Length == 0) return ProceduralTemplateUtility.Door(door);
            float width = closed.Sum(p => kit[p.Id].Size.x);
            return closed.Aggregate(Vector3.zero, (sum, p) => sum + p.Position * kit[p.Id].Size.x) / width;
        }
        private static int LeafSocket(ProceduralRoomTemplate room, ProceduralTemplatePiece p, Dictionary<string, ProceduralKitPiece> kit)
        {
            for (int i = 0; i < room.Doors.Length; i++)
            {
                var normal = ProceduralTemplateUtility.Direction(room.Doors[i].Side);
                var delta = p.Position - SocketPivot(room.Doors[i], kit);
                float across = delta.x * normal.x + delta.z * normal.y;
                float along = normal.x == 0 ? delta.x : delta.z;
                if (Math.Abs(across) < .001f && Math.Abs(along) < 2f && Math.Abs(delta.y) < .001f) return i;
            }
            return -1;
        }
        private static int FrameSocket(ProceduralRoomTemplate room, ProceduralTemplatePiece p, ProceduralKitPiece piece,
            Dictionary<string, ProceduralKitPiece> kit)
        {
            var inverse = Inverse(Yaw(p.RotY));
            for (int i = 0; i < room.Doors.Length; i++)
            {
                var door = room.Doors[i];
                var delta = inverse * (SocketPivot(door, kit) - p.Position);
                var normal = ProceduralTemplateUtility.Direction(door.Side);
                var localNormal = inverse * new Vector3(normal.x, 0f, normal.y);
                if (Math.Abs(localNormal.x) < .001f && Math.Abs(delta.x) < .001f &&
                    Math.Abs(delta.z) <= (door.ClosedWith?.Length > 0 ? .001f : piece.Size.z * .5f + .001f)) return i;
            }
            return -1;
        }
        private static void Frame(List<ProceduralBlock> blocks, int roomId, Vector3 pivot, float yaw, Vector3 size,
            float width, float height, bool open, string id = null)
        {
            var rotation = Yaw(yaw);
            if (!open)
            {
                blocks.Add(new ProceduralBlock(roomId, ProceduralSurfaceKind.Wall, pivot + Vector3.up * (size.y * .5f), size, rotation: rotation));
                return;
            }
            width = Math.Min(width, size.x); height = Math.Min(height, size.y);
            if (id != null) blocks.Add(new ProceduralBlock(roomId, ProceduralSurfaceKind.Wall, pivot + Vector3.up * (size.y * .5f), size,
                role: ProceduralBlockRole.KitVisual, rotation: rotation, pieceId: id, piecePosition: pivot));
            void Part(float x, float bottom, float w, float h)
            {
                if (w <= .0001f || h <= .0001f) return;
                blocks.Add(new ProceduralBlock(roomId, ProceduralSurfaceKind.Wall,
                    pivot + rotation * new Vector3(x, bottom + h * .5f, 0f), new Vector3(w, h, size.z),
                    role: id == null ? ProceduralBlockRole.Solid : ProceduralBlockRole.KitCollision, rotation: rotation, pieceId: id));
            }
            float jamb = (size.x - width) * .5f;
            Part(-(width + jamb) * .5f, 0f, jamb, size.y);
            Part((width + jamb) * .5f, 0f, jamb, size.y);
            Part(0f, height, width, size.y - height);
        }
        private static void Masonry(List<ProceduralBlock> blocks, int roomId, ProceduralKitPiece piece, Vector3 pivot, float yaw)
        {
            var rotation = Yaw(yaw);
            blocks.Add(new ProceduralBlock(roomId, ProceduralSurfaceKind.Wall, pivot + Vector3.up * (piece.Size.y * .5f), piece.Size,
                role: ProceduralBlockRole.KitVisual, rotation: rotation, pieceId: piece.Id, piecePosition: pivot));
            void Polygon(Vector3[] vertices, float boxYaw)
            {
                var q = Yaw(boxYaw); var inverse = Inverse(q);
                var points = vertices.Select(p => inverse * p).ToArray();
                var min = new Vector3(points.Min(p => p.x), 0f, points.Min(p => p.z));
                var max = new Vector3(points.Max(p => p.x), piece.Size.y, points.Max(p => p.z));
                blocks.Add(new ProceduralBlock(roomId, ProceduralSurfaceKind.Wall, pivot + rotation * (q * ((min + max) * .5f)), max - min,
                    role: ProceduralBlockRole.KitCollision, rotation: Yaw(yaw + boxYaw), pieceId: piece.Id));
            }
            if (piece.Id == "wall_round_tangent_r4")
            {
                // Exact four vertices from the paired transition-pier generator,
                // bounded separately so the middle of the portal is never filled.
                foreach (int sign in new[] { -1, 1 })
                    Polygon(new[] { new Vector3(sign * 2f, 0f, .4f),
                        new Vector3(sign * 2.2f, 0f, 4.4f * (float)Math.Cos(Math.PI / 6d) - 4.4f),
                        new Vector3(sign * 1.8f, 0f, 3.6f * (float)Math.Cos(Math.PI / 6d) - 4.4f),
                        new Vector3(sign * 2f, 0f, -.4f) }, 0f);
                return;
            }
            float radius = piece.Id.EndsWith("r4", StringComparison.Ordinal) ? 4f : piece.Id.EndsWith("r6", StringComparison.Ordinal) ? 6f :
                piece.Id.EndsWith("r8", StringComparison.Ordinal) ? 8f : throw new ArgumentException("Unknown masonry arc radius.");
            double sweep = (radius == 4f ? 30d : radius == 6f ? 20d : 15d) * Math.PI / 180d;
            Vector3 At(float r, double angle) => new Vector3(r * (float)Math.Sin(angle), 0f, r * (float)Math.Cos(angle) - radius);
            for (int i = 0; i < 4; i++)
            {
                double a = -sweep * .5d + sweep * i / 4d, b = -sweep * .5d + sweep * (i + 1) / 4d;
                Polygon(new[] { At(radius - .4f, a), At(radius + .4f, a), At(radius + .4f, b), At(radius - .4f, b) },
                    (float)((a + b) * .5d * 180d / Math.PI));
            }
        }
    }
}
