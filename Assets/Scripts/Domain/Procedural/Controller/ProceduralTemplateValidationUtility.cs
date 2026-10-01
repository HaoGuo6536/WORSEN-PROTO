// ============================================================================
// ProceduralTemplateValidationUtility.cs
// ============================================================================
// PURPOSE:
//   Rejects malformed art manifests before they become playable room content.
//   Validation is pure and is repeated at generation so a stale or hand-edited
//   catalogue cannot bypass connectivity, socket, anchor or enclosure admission.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Validate kit identity, dimensions and complete per-theme catalogue coverage.
//   - Check connected footprints, boundary sockets and gameplay anchor density.
//   - Reject overlapping walls and incomplete straight or curved enclosures.
//   - Admit only collision-bearing low vaults with opposite supported landing endpoints.
// DEPENDENCIES:
//   - Own definitions and coordinate utility only; no file or engine access.
// USAGE NOTES:
//   Legacy arcs use chord pivots; the Castle masonry kit uses radial pivots and
//   explicit tangent piers. Admission follows their authored inner wall faces.
//   The decal kind explicitly denotes render-only, non-collision geometry.
//   Cake density is provisionally two per nine cells, rounded up, minimum two.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralTemplateValidationUtility
    {
        public static void Validate(ProceduralTemplateCatalogue catalogue)
        {
            if (catalogue == null) Fail("Missing catalogue.");
            float height = catalogue.Theme == "castle" ? 7f : catalogue.Theme == "hospital" ? 3.6f :
                catalogue.Theme == "school" ? 3.8f : catalogue.Theme == "basement" ? 3.2f : 0f;
            if (height == 0f || catalogue.Module != 2f || catalogue.WallHeight != height) Fail("Theme/module/wall height mismatch.");
            if (catalogue.Kit == null || catalogue.Kit.Length == 0 || catalogue.Kit.Length > 256 ||
                catalogue.Kit.Any(p => p == null || !Identifier(p.Id)) ||
                catalogue.Kit.Select(p => p.Id).Distinct().Count() != catalogue.Kit.Length) Fail("Invalid kit identities.");
            var kinds = new[] { "wall", "door", "window", "arc", "corner", "pillar", "floor", "ceiling", "trim", "prop", "pipe", "duct", "decal" };
            foreach (var piece in catalogue.Kit)
            {
                if (!kinds.Contains(piece.Kind) || !ProceduralTemplateUtility.Finite(piece.Size) ||
                    piece.Size.x <= 0f || piece.Size.y < 0f || (piece.Size.y == 0f && piece.Kind != "decal") || piece.Size.z <= 0f ||
                    string.IsNullOrEmpty(piece.File) || piece.File.Contains("/") || piece.File.Contains("\\") ||
                    piece.File != char.ToUpperInvariant(catalogue.Theme[0]) + catalogue.Theme.Substring(1) + "_" + piece.Id + ".fbx")
                    Fail("Invalid kit piece " + piece.Id);
                if (Wall(piece) && piece.Size.y != height) Fail("Wall piece has the wrong theme height: " + piece.Id);
                if (piece.TraversalKind != TraversalSurfaceKind.None &&
                    (piece.TraversalKind != TraversalSurfaceKind.Vault || piece.Kind != "prop" || !piece.Collision ||
                    piece.Size.y < .35f || piece.Size.y > 1.2f || piece.Size.x < 1f || piece.Size.z < .3f || piece.Size.z > .6f))
                    Fail("Invalid vault kit metadata: " + piece.Id);
            }
            if (catalogue.Templates == null || catalogue.Templates.Length < 10 || catalogue.Templates.Length > 256 ||
                catalogue.Templates.Any(t => t == null || !Identifier(t.Id)) ||
                catalogue.Templates.Select(t => t.Id).Distinct().Count() != catalogue.Templates.Length) Fail("Catalogue needs at least ten unique templates.");
            foreach (var room in catalogue.Templates) ValidateRoom(catalogue, room);
            var rooms = catalogue.Templates.Where(t => t.Kind == "room").ToArray();
            var halls = catalogue.Templates.Where(t => t.Kind == "hallway" || t.Kind == "junction").ToArray();
            if (!rooms.Any(t => t.SizeClass == "closet") || rooms.Count(t => t.SizeClass == "small") < 2 ||
                rooms.Count(t => t.SizeClass == "medium") < 2 || !rooms.Any(t => t.SizeClass == "medium" && (t.Shape == "L" || t.Shape == "round")) ||
                !rooms.Any(t => t.SizeClass == "large") || !rooms.Any(t => t.SizeClass == "hall") ||
                halls.Length < 2 || !halls.Any(t => t.Shape == "rect") || !halls.Any(t => t.Shape != "rect" || t.Kind == "junction") ||
                rooms.Count(t => t.Gimmick != "none") < 1 || rooms.Count(t => t.Gimmick != "none") > 2)
                Fail("Catalogue is missing its required room sizes, hallway variants or one/two gimmicks.");
        }

        public static void ValidateRoom(ProceduralTemplateCatalogue catalogue, ProceduralRoomTemplate room)
        {
            if (room == null || !Identifier(room.Id) || !room.Id.StartsWith(catalogue.Theme + "_", StringComparison.Ordinal) ||
                !new[] { "room", "hallway", "junction" }.Contains(room.Kind) ||
                !new[] { "rect", "L", "T", "round", "irregular" }.Contains(room.Shape) ||
                !new[] { "none", "puzzle", "freeze", "traversal" }.Contains(room.Gimmick) ||
                room.MinRound < (room.Gimmick == "none" ? 1 : 3) ||
                !ProceduralTemplateUtility.Finite(room.Weight) || room.Weight <= 0f || !ProceduralTemplateUtility.Finite(room.Height) ||
                room.Height < catalogue.WallHeight || room.Height > catalogue.WallHeight * 2f ||
                room.Height > catalogue.WallHeight && room.MinRound < 3)
                Fail("Invalid template metadata.");
            if (room.Footprint == null || room.Footprint.Length == 0 || room.Footprint.Length > 4096 ||
                room.Footprint.Any(c => c.x < 0 || c.y < 0 || c.x > 256 || c.y > 256) ||
                room.Footprint.Min(c => c.x) != 0 || room.Footprint.Min(c => c.y) != 0 ||
                room.Footprint.Distinct().Count() != room.Footprint.Length) Fail(room.Id + ": invalid footprint.");
            int area = room.Footprint.Length;
            string size = area <= 4 ? "closet" : area <= 9 ? "small" : area <= 20 ? "medium" : area <= 40 ? "large" : "hall";
            if (room.SizeClass != size) Fail(room.Id + ": size class disagrees with area.");
            var cells = new HashSet<Vector2Int>(room.Footprint);
            var reached = new HashSet<Vector2Int> { room.Footprint[0] }; var queue = new Queue<Vector2Int>(); queue.Enqueue(room.Footprint[0]);
            while (queue.Count != 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in ProceduralTemplateUtility.Directions())
                    if (cells.Contains(cell + direction) && reached.Add(cell + direction)) queue.Enqueue(cell + direction);
            }
            if (reached.Count != area) Fail(room.Id + ": footprint is not four-connected.");
            if (room.Shape == "rect" && (cells.Max(c => c.x) + 1) * (cells.Max(c => c.y) + 1) != area)
                Fail(room.Id + ": rect footprint is not rectangular.");
            if (room.Kind == "hallway" && cells.Any(c => cells.Contains(c + Vector2Int.right) && cells.Contains(c + Vector2Int.up) &&
                cells.Contains(c + Vector2Int.one) && Enumerable.Range(0, 3).All(x => Enumerable.Range(0, 3).All(z => cells.Contains(c + new Vector2Int(x, z))))))
                Fail(room.Id + ": hallway is wider than two cells.");
            if (room.Doors == null || room.Doors.Length < (room.Kind == "room" && size == "closet" ? 1 : 2) ||
                room.Doors.Any(d => d == null) || room.Doors.Select(d => (d.Cell, d.Side)).Distinct().Count() != room.Doors.Length)
                Fail(room.Id + ": missing or duplicate door sockets.");
            foreach (var door in room.Doors)
            {
                ProceduralTemplateUtility.SocketWidth(door); // Validate the complete authored span, not only its first cell.
                var normal = ProceduralTemplateUtility.Direction(door.Side);
                var tangent = normal.x == 0 ? Vector2Int.right : Vector2Int.up;
                for (int i = 0; i < door.Span; i++)
                    if (!cells.Contains(door.Cell + tangent * i) || cells.Contains(door.Cell + tangent * i + normal))
                        Fail(room.Id + ": door span is not on a boundary edge.");
            }
            if (room.Kind == "hallway" && room.Doors.Select(d => ProceduralTemplateUtility.Door(d)).Distinct().Count() < 2)
                Fail(room.Id + ": hallway needs distinct ends.");
            if (room.Cake == null || room.GoldenCake == null || room.Light == null || room.HunterSpawn == null ||
                room.GoldenCake.Length > 1 || room.Light.Length == 0 ||
                (room.Kind == "room" && room.Cake.Length < (size == "closet" ? 1 : Math.Max(2, (area * 2 + 8) / 9))) ||
                (room.Kind == "room" && area >= 10 && room.HunterSpawn.Length == 0)) Fail(room.Id + ": insufficient gameplay anchors.");
            foreach (var group in new[] { room.Cake, room.GoldenCake, room.Light, room.HunterSpawn })
                if (group.Distinct().Count() != group.Length || group.Any(p => !ProceduralTemplateUtility.Inside(room, p, .6f)))
                    Fail(room.Id + ": anchor outside footprint or less than 0.6m from a wall.");
            if (room.Pieces == null || room.Pieces.Length == 0 || room.Pieces.Length > 16384) Fail(room.Id + ": no piece placements.");
            var kit = catalogue.Kit.ToDictionary(p => p.Id);
            foreach (var placement in room.Pieces.Concat(room.Doors.SelectMany(d => d.ClosedWith ?? Array.Empty<ProceduralTemplatePiece>())))
            {
                if (placement == null || !kit.TryGetValue(placement.Id ?? "", out var piece) ||
                    !ProceduralTemplateUtility.Finite(placement.Position) || !ProceduralTemplateUtility.Finite(placement.RotY))
                    Fail(room.Id + ": unknown piece or nonfinite placement.");
                piece = kit[placement.Id];
                if (placement.TraversalKind != piece.TraversalKind ||
                    placement.HasEndpoints != (placement.TraversalKind == TraversalSurfaceKind.Vault))
                    Fail(room.Id + ": inconsistent vault traversal metadata.");
                if (placement.TraversalKind == TraversalSurfaceKind.Vault)
                {
                    if (!piece.Collision || !placement.Collision || placement.Position.y != 0f ||
                        !ProceduralTemplateUtility.Finite(placement.EndpointA) || !ProceduralTemplateUtility.Finite(placement.EndpointB) ||
                        !ProceduralTemplateUtility.Inside(room, placement.EndpointA, .3f) ||
                        !ProceduralTemplateUtility.Inside(room, placement.EndpointB, .3f)) Fail(room.Id + ": invalid vault landing.");
                    var a = YawPoint(placement.EndpointA - placement.Position, -placement.RotY);
                    var b = YawPoint(placement.EndpointB - placement.Position, -placement.RotY);
                    if (Math.Abs(a.y) > .001f || Math.Abs(b.y) > .001f || Math.Abs(a.x) > .001f || Math.Abs(b.x) > .001f ||
                        a.z * b.z >= 0f || Math.Abs(a.z) < piece.Size.z * .5f + .5f ||
                        Math.Abs(b.z) < piece.Size.z * .5f + .5f) Fail(room.Id + ": vault endpoints must straddle the depth faces.");
                }
                if (Wall(piece) && placement.Position.y != 0f) Fail(room.Id + ": wall pivot must be at floor level.");
                if ((piece.Kind == "wall" || piece.Kind == "door" || piece.Kind == "window") &&
                    !new[] { 0f, 90f, 180f, 270f }.Contains(placement.RotY)) Fail(room.Id + ": wall rotation is not a quarter turn.");
            }
            bool masonry = CastleMasonry(catalogue);
            var walls = room.Pieces.Where(p => Wall(kit[p.Id]))
                .SelectMany(p => ShellSegments(p, kit[p.Id], masonry && room.Shape == "round")).ToArray();
            foreach (var door in room.Doors)
            {
                var alternatives = door.ClosedWith ?? Array.Empty<ProceduralTemplatePiece>();
                if (alternatives.Any(p => !Wall(kit[p.Id]))) Fail(room.Id + ": closedWith must use wall pieces.");
                var existing = room.Pieces.Where(p => Wall(kit[p.Id]) && kit[p.Id].Kind != "door").ToArray();
                for (int i = 0; i < alternatives.Length; i++)
                {
                    var combined = existing.Concat(alternatives.Where((p, index) => index != i))
                        .SelectMany(p => ShellSegments(p, kit[p.Id], masonry && room.Shape == "round")).ToArray();
                    foreach (var line in ShellSegments(alternatives[i], kit[alternatives[i].Id], masonry && room.Shape == "round"))
                    {
                        var axis = (line.b - line.a).normalized;
                        foreach (var other in combined)
                        {
                            if (Mathf.Abs(Vector3.Cross(axis, other.a - line.a).y) > .001f ||
                                Mathf.Abs(Vector3.Cross(axis, other.b - line.a).y) > .001f) continue;
                            float a = Vector3.Dot(other.a - line.a, axis), b = Vector3.Dot(other.b - line.a, axis);
                            if (Mathf.Min((line.b - line.a).magnitude, Mathf.Max(a, b)) - Mathf.Max(0f, Mathf.Min(a, b)) > .01f)
                                Fail(room.Id + ": closedWith overlaps another wall piece.");
                        }
                    }
                }
            }
            foreach (var anchor in room.Cake.Concat(room.GoldenCake).Concat(room.Light).Concat(room.HunterSpawn))
                if (walls.Any(w => Distance(anchor, w.a, w.b) < .6f - .001f)) Fail(room.Id + ": anchor too close to an authored wall.");
            for (int a = 0; a < walls.Length; a++) for (int b = a + 1; b < walls.Length; b++)
            {
                var u = walls[a].b - walls[a].a; var v = walls[b].b - walls[b].a;
                float cross = Vector3.Cross(u, v).y;
                if (Mathf.Abs(cross) > .001f)
                {
                    var delta = walls[b].a - walls[a].a;
                    float t = Vector3.Cross(delta, v).y / cross, s = Vector3.Cross(delta, u).y / cross;
                    if (t > .001f && t < .999f && s > .001f && s < .999f) Fail(room.Id + ": crossing wall pieces.");
                }
                var axis = (walls[a].b - walls[a].a).normalized;
                if (Mathf.Abs(Vector3.Cross(axis, walls[b].b - walls[b].a).y) > .001f ||
                    Mathf.Abs(Vector3.Cross(axis, walls[b].a - walls[a].a).y) > .001f) continue;
                float lo = Vector3.Dot(walls[b].a - walls[a].a, axis), hi = Vector3.Dot(walls[b].b - walls[a].a, axis);
                if (Mathf.Min((walls[a].b - walls[a].a).magnitude, Mathf.Max(lo, hi)) - Mathf.Max(0f, Mathf.Min(lo, hi)) > .01f)
                    Fail(room.Id + ": overlapping wall pieces.");
            }
            var closure = walls.ToList();
            foreach (var door in room.Doors)
            {
                var center = ProceduralTemplateUtility.Door(door);
                var tangent = ProceduralTemplateUtility.Direction(door.Side).x == 0 ? Vector3.right : Vector3.forward;
                // An authored door frame already closes its wall-plane interval.
                if (!HasDoorFrame(room, kit, door) && !walls.Any(w => Distance(center, w.a, w.b) < .01f))
                    closure.Add((center - tangent * 2f, center + tangent * 2f));
            }
            if (room.Shape == "round")
            {
                if (masonry) JoinMasonryCorners(closure);
                if (!room.Pieces.Any(p => kit[p.Id].Kind == "arc")) Fail(room.Id + ": round rooms require arc pieces.");
                foreach (var line in closure)
                    foreach (var end in new[] { line.a, line.b })
                        if (closure.Count(other => (other.a - end).sqrMagnitude < .0001f || (other.b - end).sqrMagnitude < .0001f) != 2)
                            Fail(room.Id + ": round enclosure has an open or branching seam.");
                foreach (var anchor in room.Cake.Concat(room.GoldenCake).Concat(room.Light).Concat(room.HunterSpawn))
                    if (!InPolygon(anchor, closure) || closure.Any(w => Distance(anchor, w.a, w.b) < .6f - .001f))
                        Fail(room.Id + ": anchor outside curved enclosure/clearance.");
                RoundBoundary(catalogue, new ProceduralTemplateRoom { Template = room }, Vector2.zero);
            }
            else foreach (var edge in ProceduralTemplateUtility.Boundary(room))
            {
                var tangent = edge.normal.x == 0 ? Vector3.right : Vector3.forward;
                var intervals = (masonry ? MasonryIntervals(room, kit, edge.center, tangent) : closure.Where(w => Mathf.Abs(Vector3.Cross(tangent, w.a - edge.center).y) < .001f &&
                        Mathf.Abs(Vector3.Cross(tangent, w.b - edge.center).y) < .001f)
                    .Select(w => (a: Vector3.Dot(w.a - edge.center, tangent), b: Vector3.Dot(w.b - edge.center, tangent)))
                    .Select(w => (lo: Mathf.Min(w.a, w.b), hi: Mathf.Max(w.a, w.b)))).OrderBy(w => w.lo);
                float covered = -1f;
                foreach (var interval in intervals)
                {
                    if (interval.hi < covered) continue;
                    if (interval.lo > covered + .001f) break;
                    covered = Mathf.Max(covered, interval.hi);
                    if (covered >= 1f - .001f) break;
                }
                if (covered < 1f - .001f) Fail(room.Id + ": walls do not enclose the footprint.");
            }
        }
        public static bool Wall(ProceduralKitPiece piece) => piece.Kind == "wall" || piece.Kind == "window" || piece.Kind == "door" || piece.Kind == "arc";
        public static IReadOnlyList<Vector3> PresentationBoundary(ProceduralLayout layout, int roomId)
        {
            var template = layout.TemplateRooms.FirstOrDefault(r => r.RoomId == roomId);
            if (template != null) return RoundBoundary(layout.TemplateCatalogue, template, layout.Origin);
            var organic = layout.OrganicRooms.FirstOrDefault(r => r.RoomId == roomId && r.Shape == ProceduralRoomShape.Round);
            if (organic.RoomId == 0) return null;
            var side = new Vector3(organic.RoundFacing.z, 0f, -organic.RoundFacing.x);
            var polygon = new List<Vector3>();
            // Same 10-degree shell and four-metre vestibule as OrganicShellPresenter.
            for (int angle = 30; angle <= 330; angle += 10)
                polygon.Add(organic.RoundCenter + 4f * (organic.RoundFacing * Mathf.Cos(angle * Mathf.Deg2Rad) + side * Mathf.Sin(angle * Mathf.Deg2Rad)));
            polygon.Add(organic.RoundCenter + organic.RoundFacing * 8f - side * 2f);
            polygon.Add(organic.RoundCenter + organic.RoundFacing * 8f + side * 2f);
            return polygon.AsReadOnly();
        }
        public static Vector3[] RoundBoundary(ProceduralTemplateCatalogue catalogue, ProceduralTemplateRoom room, Vector2 origin)
        {
            if (room.Template.Shape != "round") return null;
            var kit = catalogue.Kit.ToDictionary(p => p.Id);
            bool masonry = CastleMasonry(catalogue);
            var lines = room.Template.Pieces.Where(p => Wall(kit[p.Id]))
                .SelectMany(p => ShellSegments(p, kit[p.Id], masonry)).ToList();
            foreach (var door in room.Template.Doors)
            {
                var center = ProceduralTemplateUtility.Door(door);
                var tangent = ProceduralTemplateUtility.Direction(door.Side).x == 0 ? Vector3.right : Vector3.forward;
                if (!HasDoorFrame(room.Template, kit, door) && !lines.Any(w => Distance(center, w.a, w.b) < .01f))
                    lines.Add((center - tangent * 2f, center + tangent * 2f));
            }
            if (masonry) JoinMasonryCorners(lines);
            if (lines.Count == 0) return Array.Empty<Vector3>();
            var points = new List<Vector3> { lines[0].a }; var end = lines[0].b; lines.RemoveAt(0);
            while (lines.Count != 0)
            {
                points.Add(end);
                int index = lines.FindIndex(l => (l.a - end).sqrMagnitude < .0001f || (l.b - end).sqrMagnitude < .0001f);
                if (index < 0) Fail(room.Template.Id + ": round shell must be a single closed loop.");
                end = (lines[index].a - end).sqrMagnitude < .0001f ? lines[index].b : lines[index].a;
                lines.RemoveAt(index);
            }
            if ((end - points[0]).sqrMagnitude >= .0001f) Fail(room.Template.Id + ": unclosed round shell.");
            return points.Select(p => ProceduralTemplateUtility.Point(room, p, origin)).ToArray();
        }
        public static (Vector3 a, Vector3 b) Segment(ProceduralTemplatePiece placement, ProceduralKitPiece piece)
        {
            float width = piece.Size.x;
            if (piece.Kind == "arc")
            {
                float radius = piece.Id == "wall_arc_r4" ? 4f : piece.Id == "wall_arc_r6" ? 6f : piece.Id == "wall_arc_r8" ? 8f : 0f;
                if (radius == 0f) Fail("Unknown arc radius.");
                float angle = radius == 4f ? 30f : radius == 6f ? 20f : 15f;
                width = 2f * radius * Mathf.Sin(angle * .5f * Mathf.Deg2Rad);
            }
            // Unity's positive yaw maps +X towards -Z. Quaternion.Euler calls
            // native code even though Quaternion itself is a managed value type.
            double yaw = placement.RotY * (Math.PI / 180d);
            var half = new Vector3((float)Math.Cos(yaw), 0f, -(float)Math.Sin(yaw)) * (width * .5f);
            var center = placement.Position; center.y = 0f;
            return (center - half, center + half);
        }
        private static bool CastleMasonry(ProceduralTemplateCatalogue catalogue) => catalogue.Theme == "castle" &&
            catalogue.Kit.Any(p => p.Id == "wall_2m" && Math.Abs(p.Size.z - .8f) < .00001f);
        private static bool HasDoorFrame(ProceduralRoomTemplate room, Dictionary<string, ProceduralKitPiece> kit,
            ProceduralTemplateDoor door) => room.Pieces.Any(p => kit[p.Id].Kind == "door" &&
                (door.ClosedWith ?? Array.Empty<ProceduralTemplatePiece>()).Any(c =>
                    (p.Position - c.Position).sqrMagnitude < .0001f && p.RotY == c.RotY));
        private static Vector3 YawPoint(Vector3 point, float yaw)
        {
            double radians = yaw * Math.PI / 180d;
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return new Vector3(point.x * c + point.z * s, point.y, -point.x * s + point.z * c);
        }
        private static IEnumerable<(Vector3 a, Vector3 b)> ShellSegments(ProceduralTemplatePiece p,
            ProceduralKitPiece piece, bool masonry)
        {
            if (!masonry) { yield return Segment(p, piece); yield break; }
            Vector3 At(float x, float z) => new Vector3(p.Position.x, 0f, p.Position.z) + YawPoint(new Vector3(x, 0f, z), p.RotY);
            if (piece.Id == "wall_round_tangent_r4")
            {
                foreach (int sign in new[] { -1, 1 })
                    yield return (At(sign * 2f, -.4f), At(sign * 1.8f, 3.6f * (float)Math.Cos(Math.PI / 6d) - 4.4f));
            }
            else if (piece.Kind == "arc")
            {
                float radius = piece.Id == "wall_arc_r4" ? 4f : piece.Id == "wall_arc_r6" ? 6f : piece.Id == "wall_arc_r8" ? 8f : 0f;
                if (radius == 0f) Fail("Unknown arc radius.");
                double sweep = (radius == 4f ? 30d : radius == 6f ? 20d : 15d) * Math.PI / 180d;
                Vector3 End(double angle) => At((radius - .4f) * (float)Math.Sin(angle), (radius - .4f) * (float)Math.Cos(angle) - radius);
                for (int i = 0; i < 4; i++) yield return (End(-sweep / 2d + sweep * i / 4d), End(-sweep / 2d + sweep * (i + 1) / 4d));
            }
            else yield return (At(-piece.Size.x * .5f, -.4f), At(piece.Size.x * .5f, -.4f));
        }
        private static void JoinMasonryCorners(List<(Vector3 a, Vector3 b)> lines)
        {
            // Only perpendicular straight end faces can meet through wall thickness.
            // Never bridge an arc seam or a collinear missing wall.
            for (int i = 0; i < lines.Count; i++) for (int j = i + 1; j < lines.Count; j++)
            {
                var u = lines[i].b - lines[i].a; var v = lines[j].b - lines[j].a;
                if (Math.Abs(Vector3.Dot(u.normalized, v.normalized)) > .0001f ||
                    (Math.Abs(u.x) > .0001f && Math.Abs(u.z) > .0001f) ||
                    (Math.Abs(v.x) > .0001f && Math.Abs(v.z) > .0001f)) continue;
                float cross = Vector3.Cross(u, v).y;
                if (Math.Abs(cross) < .001f) continue;
                var intersection = lines[i].a + u * (Vector3.Cross(lines[j].a - lines[i].a, v).y / cross);
                bool a = (intersection - lines[i].a).sqrMagnitude <= .16001f, b = (intersection - lines[i].b).sqrMagnitude <= .16001f;
                bool c = (intersection - lines[j].a).sqrMagnitude <= .16001f, d = (intersection - lines[j].b).sqrMagnitude <= .16001f;
                if (!(a || b) || !(c || d)) continue;
                lines[i] = a ? (intersection, lines[i].b) : (lines[i].a, intersection);
                lines[j] = c ? (intersection, lines[j].b) : (lines[j].a, intersection);
            }
        }
        private static IEnumerable<(float lo, float hi)> MasonryIntervals(ProceduralRoomTemplate room,
            Dictionary<string, ProceduralKitPiece> kit, Vector3 center, Vector3 tangent)
        {
            // Intersect the grid boundary with each full-height slab. Perpendicular
            // slab end faces close the 0.8m re-entrant corner, not a fictitious gap.
            foreach (var p in room.Pieces.Where(p => Wall(kit[p.Id])))
            {
                var local = YawPoint(center - p.Position, -p.RotY);
                var direction = YawPoint(tangent, -p.RotY);
                float lo = float.NegativeInfinity, hi = float.PositiveInfinity;
                bool Clip(float value, float delta, float half)
                {
                    if (Math.Abs(delta) < .00001f) return Math.Abs(value) <= half + .00001f;
                    float a = (-half - value) / delta, b = (half - value) / delta;
                    lo = Math.Max(lo, Math.Min(a, b)); hi = Math.Min(hi, Math.Max(a, b)); return lo <= hi;
                }
                if (Clip(local.x, direction.x, kit[p.Id].Size.x * .5f) && Clip(local.z, direction.z, .4f))
                    yield return (lo, hi);
            }
        }
        private static float Distance(Vector3 point, Vector3 a, Vector3 b)
        { point.y = 0f; var delta = b - a; return (point - a - delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude)).magnitude; }
        private static bool InPolygon(Vector3 point, IEnumerable<(Vector3 a, Vector3 b)> lines)
        {
            bool inside = false;
            foreach (var line in lines)
                if ((line.a.z > point.z) != (line.b.z > point.z) && point.x <
                    (line.b.x - line.a.x) * (point.z - line.a.z) / (line.b.z - line.a.z) + line.a.x) inside = !inside;
            return inside;
        }
        private static bool Identifier(string id) => !string.IsNullOrEmpty(id) && id.All(c =>
            c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_');
        private static void Fail(string reason) => throw new ArgumentException(reason);
    }
}
