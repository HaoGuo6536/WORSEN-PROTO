// ============================================================================
// ProceduralRoomManifestSetup.cs
// ============================================================================
// PURPOSE:
//   Reads the art workers' nested-array JSON contract into Unity-serializable data.
//   Parsing is strict about array dimensions and numbers, then runs the same pure
//   validator used by runtime generation before any catalogue asset is published.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Procedural.
// KEY RESPONSIBILITIES:
//   - Parse kit and room manifests without repairing malformed source tokens.
//   - Publish validated snapshots into the selected content asset.
//   - Reject malformed traversal metadata and preserve room-local vault endpoints.
// DEPENDENCIES:
//   - Domain.Procedural, bounded local JSON reader and UnityEditor.
// USAGE NOTES:
//   Parsing is side-effect free and testable with stub manifest strings.
//   closedWith accepts an array of placements, or one placement object.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JToken = Worsen.Editor.Procedural.ProceduralManifestJsonSetup.Value;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Core;

namespace Worsen.Editor.Procedural
{
    public static class ProceduralRoomManifestSetup
    {
        [Serializable] private sealed class Snapshot { public ProceduralTemplateCatalogue[] _catalogues; }
        public static void Publish(ProceduralRoomCatalogueData asset, ProceduralTemplateCatalogue[] catalogues)
        {
            foreach (var catalogue in catalogues) ProceduralTemplateValidationUtility.Validate(catalogue);
            Undo.RecordObject(asset, "Import room catalogues");
            // JsonUtility on both sides: EditorJsonUtility expects its own wrapped format and silently
            // wrote zero catalogues, so no theme template or kit ever reached a build (owner playtest).
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Snapshot { _catalogues = catalogues }), asset);
            if (new SerializedObject(asset).FindProperty("_catalogues").arraySize != catalogues.Length)
                throw new InvalidOperationException("Room catalogue publication lost catalogues.");
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        }
        public static ProceduralTemplateCatalogue Parse(string kitJson, string roomsJson)
        {
            var kit = ProceduralManifestJsonSetup.Parse(kitJson); var rooms = ProceduralManifestJsonSetup.Parse(roomsJson);
            var result = new ProceduralTemplateCatalogue
            {
                Theme = Text(rooms, "theme"), Module = Number(rooms["module"]), WallHeight = Number(kit["wallHeight"]),
                Kit = Array(kit["pieces"]).Select(p => new ProceduralKitPiece
                { Id = Text(p, "id"), File = Text(p, "file"), Kind = Text(p, "kind"), Size = Point(p["size"]),
                    TraversalKind = Traversal(p), Collision = Collision(p) }).ToArray(),
                Templates = Array(rooms["templates"]).Select(t => new ProceduralRoomTemplate
                {
                    Id = Text(t, "id"), Kind = Text(t, "kind"), Shape = Text(t, "shape"), SizeClass = Text(t, "sizeClass"),
                    Height = Number(t["height"]), Gimmick = Text(t, "gimmick"), MinRound = Integer(t["minRound"]), Weight = Number(t["weight"]),
                    Footprint = Array(t["footprint"]).Select(Cell).ToArray(),
                    Doors = Array(t["doors"]).Select(d => new ProceduralTemplateDoor
                    {
                        Cell = Cell(d["cell"]), Side = Text(d, "side"), Span = d["span"] == null ? 1 : Integer(d["span"]),
                        ClosedWith = d["closedWith"] == null ? System.Array.Empty<ProceduralTemplatePiece>() :
                            d["closedWith"].Members != null ? new[] { Placement(d["closedWith"]) } : Array(d["closedWith"]).Select(Placement).ToArray()
                    }).ToArray(),
                    Cake = Points(t["anchors"]?["cake"]), GoldenCake = Points(t["anchors"]?["goldenCake"]),
                    Light = Points(t["anchors"]?["light"]), HunterSpawn = Points(t["anchors"]?["hunterSpawn"]),
                    Pieces = Array(t["pieces"]).Select(Placement).ToArray()
                }).ToArray()
            };
            if (Text(kit, "theme") != result.Theme) throw new ArgumentException("Kit and room themes differ.");
            ProceduralTemplateValidationUtility.Validate(result);
            return result;
        }
        private static ProceduralTemplatePiece Placement(JToken value)
        {
            var traversal = Traversal(value);
            bool endpoints = value["endpointA"] != null || value["endpointB"] != null;
            if ((traversal == TraversalSurfaceKind.Vault) != endpoints)
                throw new ArgumentException("Vault placements require paired endpoints; untagged placements cannot carry them.");
            return new ProceduralTemplatePiece { Id = Text(value, "id"), Position = Point(value["pos"]), RotY = Number(value["rotY"]),
                TraversalKind = traversal, Collision = Collision(value), HasEndpoints = endpoints,
                EndpointA = endpoints ? Point(value["endpointA"]) : default, EndpointB = endpoints ? Point(value["endpointB"]) : default };
        }
        private static TraversalSurfaceKind Traversal(JToken value)
        {
            if (value["traversal"] == null) return TraversalSurfaceKind.None;
            if (Text(value, "traversal") != "vault") throw new ArgumentException("Unknown traversal kind.");
            return TraversalSurfaceKind.Vault;
        }
        private static bool Collision(JToken value)
        {
            var token = value["collision"];
            if (token == null && value["traversal"] == null) return true;
            if (token == null || token.IsString || (token.Text != "true" && token.Text != "false"))
                throw new ArgumentException("Expected collision boolean.");
            return token.Text == "true";
        }
        private static Vector3[] Points(JToken value) => value == null ? System.Array.Empty<Vector3>() : Array(value).Select(Point).ToArray();
        private static Vector3 Point(JToken value)
        { var a = Array(value); if (a.Count != 3) throw new ArgumentException("Expected three coordinates."); return new Vector3(Number(a[0]), Number(a[1]), Number(a[2])); }
        private static Vector2Int Cell(JToken value)
        { var a = Array(value); if (a.Count != 2) throw new ArgumentException("Expected two cell coordinates."); return new Vector2Int(Integer(a[0]), Integer(a[1])); }
        private static List<JToken> Array(JToken value) => value?.Items ?? throw new ArgumentException("Expected a manifest array.");
        private static string Text(JToken value, string key) => value?[key]?.IsString == true ? value[key].Text : throw new ArgumentException("Expected string " + key);
        private static int Integer(JToken value) => value?.IsInteger == true && int.TryParse(value.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result) ? result : throw new ArgumentException("Expected an integer.");
        private static float Number(JToken value)
        {
            if (value == null || value.IsString || !float.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)) throw new ArgumentException("Expected a number.");
            if (!ProceduralTemplateUtility.Finite(result)) throw new ArgumentException("Expected a finite number.");
            return result;
        }
    }
}
