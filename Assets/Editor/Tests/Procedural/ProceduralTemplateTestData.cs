// ============================================================================
// ProceduralTemplateTestData.cs
// ============================================================================
// PURPOSE:
//   Supplies complete synthetic manifest catalogues while art is authored elsewhere.
//   The fixture uses only contract data and explicitly tiled wall placements, never
//   repository assets or a Unity scene, so failures isolate admission and placement.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Build complete test-only room and kit manifests with stable identities.
// DEPENDENCIES:
//   - Domain.Procedural and Unity value types.
// USAGE NOTES:
//   This is a fixture builder, not shipped fallback content.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    internal static class ProceduralTemplateTestData
    {
        public static ProceduralTemplateCatalogue Catalogue()
        {
            var catalogue = new ProceduralTemplateCatalogue { Theme = "castle", WallHeight = 7f,
                Kit = new[] { new ProceduralKitPiece { Id = "wall_2m", File = "Castle_wall_2m.fbx", Kind = "wall", Size = new Vector3(2f, 7f, .5f) } } };
            catalogue.Templates = new[] {
                Room("closet", 2, 2), Room("small_a", 3, 2), Room("small_b", 3, 3),
                Room("medium", 4, 4), Room("medium_l", 4, 4, bend: true), Room("large", 6, 6), Room("landmark", 8, 6),
                Room("hallway", 2, 4, "hallway"), Room("bend", 4, 4, "hallway", true), Room("gimmick", 4, 4, gimmick: "freeze") };
            return catalogue;
        }
        private static ProceduralRoomTemplate Room(string id, int width, int depth, string kind = "room", bool bend = false, string gimmick = "none")
        {
            var cells = new List<Vector2Int>();
            for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++) if (!bend || x < 2 || z < 2) cells.Add(new Vector2Int(x, z));
            int area = cells.Count;
            var template = new ProceduralRoomTemplate { Id = "castle_" + id, Kind = kind, Shape = bend ? "L" : "rect",
                SizeClass = area <= 4 ? "closet" : area <= 9 ? "small" : area <= 20 ? "medium" : area <= 40 ? "large" : "hall",
                Height = 7f, MinRound = gimmick == "none" ? 1 : 3, Weight = 1f, Gimmick = gimmick, Footprint = cells.ToArray(),
                Cake = cells.Take(kind == "room" ? Math.Max(2, (area * 2 + 8) / 9) : 1).Select(c => new Vector3(c.x * 2f + 1f, 0f, c.y * 2f + 1f)).ToArray(),
                Light = new[] { new Vector3(1f, 2.6f, 1f) }, HunterSpawn = new[] { new Vector3(1f, 0f, 1f), new Vector3(width * 2f - 1f, 0f, 1f) },
                Doors = new[] { new ProceduralTemplateDoor { Cell = new Vector2Int(1, 0), Side = "S" },
                    new ProceduralTemplateDoor { Cell = new Vector2Int(0, 1), Side = "W" },
                    new ProceduralTemplateDoor { Cell = new Vector2Int(width - 1, 1), Side = "E" },
                    new ProceduralTemplateDoor { Cell = new Vector2Int(1, depth - 1), Side = "N" } } };
            template.Pieces = ProceduralTemplateUtility.Boundary(template).Select(edge => new ProceduralTemplatePiece
                { Id = "wall_2m", Position = edge.center, RotY = edge.normal.x == 0 ? (edge.normal.y > 0 ? 0f : 180f) : (edge.normal.x > 0 ? 90f : 270f) }).ToArray();
            return template;
        }
        public static (string kit, string rooms) Json(ProceduralTemplateCatalogue c)
        {
            string N(float v) => v.ToString("R", CultureInfo.InvariantCulture);
            string Q(string v) => "\"" + v + "\"";
            string A(IEnumerable<string> v) => "[" + string.Join(",", v) + "]";
            string V(Vector3 v) => "[" + N(v.x) + "," + N(v.y) + "," + N(v.z) + "]";
            string C(Vector2Int v) => "[" + v.x + "," + v.y + "]";
            string kit = "{\"theme\":" + Q(c.Theme) + ",\"wallHeight\":" + N(c.WallHeight) + ",\"pieces\":" + A(c.Kit.Select(p =>
                "{\"id\":" + Q(p.Id) + ",\"file\":" + Q(p.File) + ",\"kind\":" + Q(p.Kind) + ",\"size\":" + V(p.Size) + "}")) + "}";
            string rooms = "{\"theme\":" + Q(c.Theme) + ",\"module\":" + N(c.Module) + ",\"templates\":" + A(c.Templates.Select(t =>
                "{\"id\":" + Q(t.Id) + ",\"kind\":" + Q(t.Kind) + ",\"sizeClass\":" + Q(t.SizeClass) + ",\"shape\":" + Q(t.Shape) +
                ",\"height\":" + N(t.Height) + ",\"gimmick\":" + Q(t.Gimmick) + ",\"minRound\":" + t.MinRound + ",\"weight\":" + N(t.Weight) +
                ",\"footprint\":" + A(t.Footprint.Select(C)) + ",\"doors\":" + A(t.Doors.Select(d => "{\"cell\":" + C(d.Cell) + ",\"side\":" + Q(d.Side) + "}")) +
                ",\"anchors\":{\"cake\":" + A(t.Cake.Select(V)) + ",\"goldenCake\":" + A(t.GoldenCake.Select(V)) + ",\"light\":" + A(t.Light.Select(V)) +
                ",\"hunterSpawn\":" + A(t.HunterSpawn.Select(V)) + "},\"pieces\":" + A(t.Pieces.Select(p => "{\"id\":" + Q(p.Id) + ",\"pos\":" + V(p.Position) + ",\"rotY\":" + N(p.RotY) + "}")) + "}")) + "}";
            return (kit, rooms);
        }
    }
}
