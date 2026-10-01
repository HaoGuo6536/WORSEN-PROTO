// ============================================================================
// ProceduralTemplateGeometryPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies template piece commands, closed sockets and per-piece art fallback.
//   Transient kit objects exercise the existing Driver without baking navigation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check authored wall/arc identities, tiled support and unused socket closure.
//   - Check missing art stays primitive and available art retains metre transforms.
//   - Check compound pieces instantiate once and zero-height decals remain finite.
// DEPENDENCIES:
//   - NUnit, Core, Domain.Procedural and temporary Unity objects.
// USAGE NOTES:
//   No persistent assets or scene writes; coordinator runs in Edit Mode.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralTemplateGeometryPresenterTests
    {
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void EveryAuthoredFloorAndCeilingPlacementBuildsOnceAtItsExactPivot(string theme)
        {
            var catalogue = ProceduralTemplateSeamPresenterTests.Read(theme);
            int authored = 0, built = 0;
            foreach (var template in catalogue.Templates)
            for (int turn = 0; turn < 4; turn++)
            {
                var room = new ProceduralTemplateRoom { RoomId = 1, Template = template, Turns = turn, Offset = new Vector2Int(3, -7) };
                var blocks = new ProceduralTemplateGeometryPresenter().Build(catalogue, room,
                    ProceduralTemplateSeamPresenterTests.Config(theme), ProceduralTemplateSeamPresenterTests.Driver());
                var expected = template.Pieces.Where(p => catalogue.Kit.Any(k => k.Id == p.Id && (k.Kind == "floor" || k.Kind == "ceiling"))).ToArray();
                var actual = blocks.Where(b => b.HasRenderer && b.PieceId != null &&
                    catalogue.Kit.Any(k => k.Id == b.PieceId && (k.Kind == "floor" || k.Kind == "ceiling"))).ToArray();
                authored += expected.Length; built += actual.Length;
                Assert.That(actual.Length, Is.EqualTo(expected.Length), template.Id);
                foreach (var p in expected)
                    Assert.That(actual.Count(b => b.PieceId == p.Id && b.PiecePosition == ProceduralTemplateUtility.Point(room, p.Position, Vector2.zero)),
                        Is.EqualTo(1), template.Id + "/" + p.Id);
                Assert.That(blocks.Where(b => b.Kind == ProceduralSurfaceKind.Floor && b.Role == ProceduralBlockRole.CollisionOnly)
                    .All(b => Math.Abs(b.Center.y + b.Size.y * .5f) < .0001f), Is.True, "No lowered round-room floor.");
            }
            TestContext.WriteLine("PLACEMENTS theme=" + theme + " authored=" + authored + " built=" + built + " turns=4");
        }
        [Test]
        public void TilesAndWallCommandsPreserveKitIdentityAndOnlyOpenedSocketsAreCut()
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var catalogue = ProceduralTemplateTestData.Catalogue();
                var room = new ProceduralTemplateRoom { RoomId = 1, Template = catalogue.Templates[3], OpenDoors = new[] { 0 } };
                var blocks = new ProceduralTemplateGeometryPresenter().Build(catalogue, room, config, driver);
                Assert.That(blocks.Count(b => b.Kind == ProceduralSurfaceKind.Floor && b.HasCollision), Is.EqualTo(room.Template.Footprint.Length));
                Assert.That(blocks.Any(b => b.PieceId == "wall_2m"), Is.True);
                for (int i = 0; i < room.Template.Doors.Length; i++)
                {
                    var point = ProceduralTemplateUtility.Door(room.Template.Doors[i]) + Vector3.up;
                    bool blocked = blocks.Any(b => b.HasCollision && new Bounds(Vector3.zero, b.Size).Contains(Quaternion.Inverse(b.Rotation) * (point - b.Center)));
                    Assert.That(blocked, Is.EqualTo(i != 0), "Socket " + i);
                }
            }
            finally { Object.DestroyImmediate(config); Object.DestroyImmediate(driver); }
        }
        [TestCase("wall_arc_r4", 4f, 30f)] [TestCase("wall_arc_r6", 6f, 20f)] [TestCase("wall_arc_r8", 8f, 15f)]
        public void RoundWallCommandsRetainTheAuthoredArc(string id, float radius, float angle)
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>(); var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var catalogue = new ProceduralTemplateCatalogue { Kit = new[] { new ProceduralKitPiece { Id = id, Kind = "arc", Size = new Vector3(2f, 7f, .5f) } } };
                var template = new ProceduralRoomTemplate { Height = 7f, Shape = "round", Pieces = new[] { new ProceduralTemplatePiece { Id = id, RotY = 15f } } };
                var block = new ProceduralTemplateGeometryPresenter().Build(catalogue, new ProceduralTemplateRoom { RoomId = 1, Template = template }, config, driver).Single();
                Assert.That(block.PieceId, Is.EqualTo(id)); Assert.That(block.Size.x, Is.EqualTo(2f * radius * Mathf.Sin(angle * .5f * Mathf.Deg2Rad)).Within(.001f));
                Assert.That(block.HasCollision, Is.True);
            }
            finally { Object.DestroyImmediate(config); Object.DestroyImmediate(driver); }
        }
        [TestCase(false)] [TestCase(true)]
        public void DriverUsesAvailablePrefabAndOnlyMissingPieceUsesPrimitive(bool available)
        {
            var root = new GameObject("Kit fixture"); var catalogue = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.name = "Test kit";
            var driver = root.AddComponent<ProceduralDriver>();
            var state = (ProceduralDriverState)Field(driver, "_state").GetValue(driver);
            state.Root = new GameObject("Generated test root"); state.Root.transform.SetParent(root.transform);
            state.Catalogue = catalogue; state.ThemeId = "castle";
            Field(catalogue, "_pieces").SetValue(catalogue, new[] { new ProceduralRoomCatalogueData.KitAsset { Theme = "castle", Id = "wall_2m", Prefab = available ? prefab : null } });
            try
            {
                var pivot = new Vector3(8f, 0f, 12f); var rotation = Quaternion.Euler(0f, 90f, 0f);
                var block = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, pivot + Vector3.up * 3.5f, new Vector3(2f, 7f, .5f), rotation: rotation,
                    pieceId: "wall_2m", piecePosition: pivot);
                typeof(ProceduralDriver).GetMethod("CreateBlock", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, new object[] { block, null, 0 });
                var item = state.Fragments[1].Single();
                Assert.That(item.GetComponent<Collider>().enabled, Is.True);
                Assert.That(item.GetComponent<Renderer>().enabled, Is.EqualTo(!available));
                if (available)
                {
                    var visual = item.transform.GetChild(0);
                    Assert.That(Vector3.Distance(visual.position, pivot), Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(visual.lossyScale, Vector3.one), Is.LessThan(.001f));
                    Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
                }
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); Object.DestroyImmediate(catalogue); }
        }
        [TestCase(false)] [TestCase(true)]
        public void CompoundKitUsesOneVisualAndOnlyItsPartsCollide(bool available)
        {
            var root = new GameObject("Compound fixture"); var catalogue = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var driver = root.AddComponent<ProceduralDriver>();
            var state = (ProceduralDriverState)Field(driver, "_state").GetValue(driver);
            state.Root = new GameObject("Generated compound"); state.Root.transform.SetParent(root.transform);
            state.Catalogue = catalogue; state.ThemeId = "castle";
            Field(catalogue, "_pieces").SetValue(catalogue, new[] { new ProceduralRoomCatalogueData.KitAsset {
                Theme = "castle", Id = "wall_round_tangent_r4", Prefab = available ? prefab : null } });
            var commands = new[] {
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up * 3.5f, new Vector3(4.4f, 7f, 1.7f),
                    role: ProceduralBlockRole.KitVisual, pieceId: "wall_round_tangent_r4", piecePosition: Vector3.zero),
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, new Vector3(-2f, 3.5f, -.4f), new Vector3(.4f, 7f, 1.7f),
                    role: ProceduralBlockRole.KitCollision, pieceId: "wall_round_tangent_r4"),
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, new Vector3(2f, 3.5f, -.4f), new Vector3(.4f, 7f, 1.7f),
                    role: ProceduralBlockRole.KitCollision, pieceId: "wall_round_tangent_r4") };
            try
            {
                foreach (var block in commands)
                    typeof(ProceduralDriver).GetMethod("CreateBlock", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, new object[] { block, null, 0 });
                var parts = state.Fragments[1];
                Assert.That(parts.Sum(p => p.transform.childCount), Is.EqualTo(available ? 1 : 0));
                Assert.That(parts[0].GetComponent<Renderer>().enabled, Is.False);
                Assert.That(parts[0].GetComponent<Collider>(), Is.Null);
                foreach (var part in parts.Skip(1))
                {
                    Assert.That(part.GetComponent<Collider>().enabled, Is.True);
                    Assert.That(part.GetComponent<Renderer>().enabled, Is.EqualTo(!available));
                }
                if (available)
                {
                    var visual = parts[0].transform.GetChild(0);
                    Assert.That(Vector3.Distance(visual.position, Vector3.zero), Is.LessThan(1e-4f), "Visual sits at the kit origin (float tolerance).");
                    Assert.That((visual.lossyScale - Vector3.one).magnitude, Is.LessThan(.001f));
                    Assert.That(visual.GetComponent<Collider>().enabled, Is.False);
                }
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); Object.DestroyImmediate(catalogue); }
        }
        [Test]
        public void ZeroHeightDecalKeepsFiniteUnitArtScaleAndNoCollider()
        {
            var root = new GameObject("Decal fixture"); var catalogue = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var driver = root.AddComponent<ProceduralDriver>();
            var state = (ProceduralDriverState)Field(driver, "_state").GetValue(driver);
            state.Root = new GameObject("Generated decal"); state.Root.transform.SetParent(root.transform);
            state.Catalogue = catalogue; state.ThemeId = "school";
            Field(catalogue, "_pieces").SetValue(catalogue, new[] { new ProceduralRoomCatalogueData.KitAsset { Theme = "school", Id = "decal", Prefab = prefab } });
            try
            {
                var block = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up * .01f, new Vector3(2f, 0f, 2f),
                    role: ProceduralBlockRole.VisualOnly, pieceId: "decal");
                typeof(ProceduralDriver).GetMethod("CreateBlock", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(driver, new object[] { block, null, 0 });
                var item = state.Fragments[1].Single(); var visual = item.transform.GetChild(0);
                Assert.That(item.GetComponent<Collider>(), Is.Null);
                Assert.That((visual.lossyScale - Vector3.one).magnitude, Is.LessThan(.001f));
                Assert.That(visual.position, Is.EqualTo(block.PiecePosition));
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); Object.DestroyImmediate(catalogue); }
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
