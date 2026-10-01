// ============================================================================
// ProceduralCakeLinePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the owner-approved walking-line layout against real theme collision.
//   Pure fixtures prove straight spacing, deterministic identities, route direction
//   and conservative support without pretending to run Unity navigation admission.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Procedural.
// KEY RESPONSIBILITIES:
//   - Exercise every real template and quarter turn in all four themes.
//   - Prove straight evenly spaced runs and collision/footprint clearance.
//   - Cover door-to-door hallways, organic notches, gaps and native representative counts.
// DEPENDENCIES:
//   - NUnit, Core, Procedural and the production manifest parser.
// USAGE NOTES:
//   Owner playtest 2026-09-30 replaces sparse anchors, not authored geometry.
//   Config objects use managed field injection. No Unity lifecycle or native APIs.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralCakeLinePresenterTests
    {
        public static IEnumerable<TestCaseData> Rooms()
        {
            foreach (string theme in new[] { "Castle", "Hospital", "School", "Basement" })
            foreach (var room in Read(theme).Templates)
                yield return new TestCaseData(theme, room.Id);
        }

        [TestCaseSource(nameof(Rooms))]
        public void EveryThemeTemplateProducesDeterministicClearWalkingLines(string theme, string id)
        {
            var catalogue = Read(theme); var template = catalogue.Templates.Single(t => t.Id == id);
            for (int turns = 0; turns < 4; turns++)
            {
                var room = new ProceduralTemplateRoom { RoomId = 1, Template = template, Turns = turns,
                    OpenDoors = Enumerable.Range(0, template.Doors.Length).ToArray() };
                var config = Config(); var driver = Empty<ProceduralDriverConfig>();
                Field(driver, "_floorThickness", .3f); Field(driver, "_ceilingThickness", .3f); Field(driver, "_wallThickness", .3f);
                var blocks = new ProceduralTemplateGeometryPresenter().Build(catalogue, room, config, driver);
                var volumes = ProceduralTemplateUtility.Volumes(ProceduralTemplateUtility.OccupiedCells(room), Vector2.zero, template.Height, 1f);
                var bounds = volumes[0]; foreach (var v in volumes) bounds.Encapsulate(v);
                var logical = new LevelRoom(1, bounds.center, bounds.size, cells: volumes);
                var doors = template.Doors.Select(d => new ProceduralDoorPlan(1, 2,
                    ProceduralTemplateUtility.Point(room, ProceduralTemplateUtility.Door(d), Vector2.zero),
                    ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(d.Side), turns).x == 0)).ToArray();
                var layout = Layout(logical, doors); Property(layout, "TemplateRooms", new[] { room });
                var copy = Layout(logical, doors); Property(copy, "TemplateRooms", new[] { room });
                var presenter = new ProceduralCakeLinePresenter();
                presenter.Apply(layout, config, blocks, .5f, 2f); presenter.Apply(copy, config, blocks, .5f, 2f);
                Assert.That(layout.Graph.Anchors, Is.EqualTo(copy.Graph.Anchors));
                Verify(layout, blocks, config);
                TestContext.WriteLine(theme + "/" + id + " turn=" + turns + " cakes=" + layout.Graph.Anchors.Count + " lines=" + layout.CakeLines.Count);
            }
        }

        [TestCase("Castle", 7)] [TestCase("Hospital", 7)] [TestCase("School", 7)] [TestCase("Basement", 7)]
        [TestCase("Castle", 15)] [TestCase("Hospital", 15)] [TestCase("School", 15)] [TestCase("Basement", 15)]
        public void SeededWholeFloorsReportCakeAndAdmissionCounts(string theme, int rooms)
        {
            var catalogue = Read(theme); var config = Config(); var driver = Empty<ProceduralDriverConfig>();
            Field(driver, "_floorThickness", .3f); Field(driver, "_ceilingThickness", .3f); Field(driver, "_wallThickness", .3f);
            var data = Empty<ProceduralRoomCatalogueData>(); Field(data, "_catalogues", new[] { catalogue });
            Field(config, "_roomCatalogue", data); Field(config, "_initialRoomCount", rooms); Field(config, "_maximumRoomCount", rooms);
            Field(config, "_templatePlacementBudget", 2048); Field(config, "_minimumHunterSpawnRooms", 2);
            Field(config, "_templateExitClearance", 1.6f); Field(config, "_templateExitSpawnDistance", 3.2f);
            var themeData = new ProceduralThemeData(theme.ToLowerInvariant(), true, Array.Empty<string>(), "", "", "", "", "", default, default, default, 0f);
            for (int seed = 0; seed < 3; seed++)
            {
                // Use the same bounded seed successor protocol as production, not
                // an assumption that every first template-placement attempt fits.
                var state = new ProceduralBehaviorState(); var generation = new ProceduralGenerationController(state);
                generation.Begin(seed, 1, 3);
                ProceduralLayout layout; ProceduralBlock[] blocks;
                while (true)
                {
                    if (new ProceduralTemplateController(config, new System.Random(ProceduralController.LayoutSeed(state.AttemptSeed, 1)))
                        .TryGenerate(state.AttemptSeed, 1, themeData, false, 1, out layout, out var reason))
                    {
                        blocks = new ProceduralTemplateGeometryPresenter().Build(layout, config, driver).ToArray();
                        try
                        {
                            new ProceduralCakeLinePresenter().Apply(layout, config, blocks, .5f, 2f);
                            break;
                        }
                        catch (InvalidOperationException error) { reason = error.Message; }
                    }
                    TestContext.WriteLine("CAKE_FLOOR_REJECT theme=" + theme + " seed=" + state.AttemptSeed + " reason=" + reason);
                    Assert.That(generation.Fail(reason, string.Empty), Is.True, reason);
                }
                Assert.That(state.AttemptSeed == seed || state.GenerationManifest.Contains("failed:"), Is.True,
                    "Rejected collision/layout candidates must retain their journal, not disappear as skipped tests.");
                Assert.That(layout.Graph.Rooms.Count, Is.EqualTo(rooms));
                Assert.That(layout.Graph.Anchors.Count, Is.GreaterThan(rooms * 2), "Owner playtest requires common rows, not sparse destinations.");
                Verify(layout, blocks, config);
                var direction = layout.Graph.ExitPosition - layout.PlayerSpawnPosition; direction.y = 0f;
                Assert.That(Vector3.Dot(layout.PlayerSpawnRotation * Vector3.forward, direction.normalized), Is.GreaterThan(.999f));
                TestContext.WriteLine("CAKE_FLOOR theme=" + theme + " seed=" + seed + " acceptedSeed=" + state.AttemptSeed + " rooms=" + rooms +
                    " cakes=" + layout.Graph.Anchors.Count + " lines=" + layout.CakeLines.Count +
                    " nativeCakePathPairs=" + layout.CakeLines.Count);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void RelocatedLinesPreserveRequiredHunterFailureInjection(bool isolated)
        {
            var catalogue = ProceduralTemplateTestData.Catalogue();
            catalogue.Kit = catalogue.Kit.Concat(new[] { new ProceduralKitPiece { Id = "test_obstruction",
                File = "Castle_test_obstruction.fbx", Kind = "prop", Size = isolated ? new Vector3(2.1f, 3f, .1f) : Vector3.one } }).ToArray();
            foreach (var room in catalogue.Templates)
            {
                room.HunterSpawn = new[] { room.HunterSpawn[0] };
                room.Pieces = room.Pieces.Concat(isolated ? new[] {
                    new ProceduralTemplatePiece { Id = "test_obstruction", Position = new Vector3(1f, 0f, 2f) },
                    new ProceduralTemplatePiece { Id = "test_obstruction", Position = new Vector3(2f, 0f, 1f), RotY = 90f }
                } : new[] { new ProceduralTemplatePiece { Id = "test_obstruction", Position = room.HunterSpawn[0] } }).ToArray();
            }
            var config = Config(); var data = Empty<ProceduralRoomCatalogueData>(); var driver = Empty<ProceduralDriverConfig>();
            Field(data, "_catalogues", new[] { catalogue }); Field(config, "_roomCatalogue", data);
            Field(config, "_initialRoomCount", 7); Field(config, "_maximumRoomCount", 15); Field(config, "_templatePlacementBudget", 2048);
            Field(config, "_minimumHunterSpawnRooms", 2); Field(config, "_templateExitClearance", 1.6f); Field(config, "_templateExitSpawnDistance", 3.2f);
            Field(config, "_origin", new Vector2(10000f, 10000f)); Field(config, "_spawnHeight", .1f);
            Field(driver, "_floorThickness", .3f); Field(driver, "_ceilingThickness", .3f); Field(driver, "_wallThickness", .3f);
            for (int attempt = 0; attempt <= 3; attempt++)
            {
                int seed = unchecked(7 + attempt * ProceduralGenerationController.SeedStride);
                Assert.That(new ProceduralTemplateController(config, new System.Random(ProceduralController.LayoutSeed(seed, 1)))
                    .TryGenerate(seed, 1, null, false, 1, out var layout, out var reason), Is.True, reason);
                var blocks = new ProceduralTemplateGeometryPresenter().Build(layout, config, driver).ToArray();
                new ProceduralCakeLinePresenter().Apply(layout, config, blocks, .5f, 2f);
                Assert.That(layout.Graph.Anchors, Is.Not.Empty); Assert.That(layout.HunterSpawnPositions, Is.Not.Empty);
                if (isolated) Assert.DoesNotThrow(() => new ProceduralNavFallbackPresenter().ValidateTemplate(layout, blocks, .5f, 2f));
                else Assert.That(Assert.Throws<InvalidOperationException>(() => new ProceduralNavFallbackPresenter()
                    .ValidateTemplate(layout, blocks, .5f, 2f)).Message, Does.Contain("hunter spawn="));
            }
        }

        [Test]
        public void VaultEndpointsAndCrossingRemainFreeOfPickups()
        {
            var room = new LevelRoom(1, Vector3.up * 2f, new Vector3(20f, 4f, 8f));
            var layout = Layout(room, new[] { new ProceduralDoorPlan(1, 2, new Vector3(-10f, 0f, 0f), false),
                new ProceduralDoorPlan(1, 3, new Vector3(10f, 0f, 0f), false) });
            var blocks = new[] { Floor(1, 0f, 0f, 20f, 8f), new ProceduralBlock(1, ProceduralSurfaceKind.Wall,
                Vector3.up * .5f, new Vector3(.2f, 1f, 2f), 1, TraversalSurfaceKind.Vault,
                Vector3.left, Vector3.right) };
            new ProceduralCakeLinePresenter().Apply(layout, Config(), blocks, .5f, 2f);
            Assert.That(layout.Graph.Anchors, Is.Not.Empty);
            foreach (var cake in layout.Graph.Anchors)
            {
                float x = Mathf.Clamp(cake.Position.x, -1f, 1f);
                Assert.That(Vector3.Distance(new Vector3(x, cake.Position.y, 0f), cake.Position), Is.GreaterThanOrEqualTo(1f));
            }
            Verify(layout, blocks, Config());
        }

        [Test]
        public void KnockableBakeSourcesCannotOccupyCakeLines()
        {
            var room = new LevelRoom(1, Vector3.up * 2f, new Vector3(20f, 4f, 8f));
            var layout = Layout(room, new[] { new ProceduralDoorPlan(1, 2, new Vector3(-10f, 0f, 0f), false),
                new ProceduralDoorPlan(1, 3, new Vector3(10f, 0f, 0f), false) });
            var obstacle = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up, new Vector3(2f, 2f, 2f));
            Property(layout, "Interactables", new[] { new ProceduralInteractablePlan(new InteractableState(1,
                InteractableKind.KnockableProp, 1, obstacle.Center, InteractableStateValue.Inactive), obstacle.Size) });
            var floor = Floor(1, 0f, 0f, 20f, 8f);
            new ProceduralCakeLinePresenter().Apply(layout, Config(), new[] { floor }, .5f, 2f);
            Verify(layout, new[] { floor, obstacle }, Config());
        }

        [Test]
        public void HallwayRunsAlongCentreBetweenDoorwaysRatherThanAcrossWidth()
        {
            var room = new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(24f, 4f, 4f));
            var doors = new[] { new ProceduralDoorPlan(1, 2, new Vector3(-12f, 0f, 0f), false),
                new ProceduralDoorPlan(1, 3, new Vector3(12f, 0f, 0f), false) };
            var layout = Layout(room, doors); var blocks = new[] { Floor(1, 0f, 0f, 24f, 4f) };
            new ProceduralCakeLinePresenter().Apply(layout, Config(), blocks, .5f, 2f);
            Assert.That(layout.Graph.Anchors.Count, Is.GreaterThan(10));
            Assert.That(layout.Graph.Anchors.All(a => Math.Abs(a.Position.z) < .001f), Is.True);
            Assert.That(layout.Graph.Anchors.All(a => Math.Abs(a.Position.x) <= 11f), Is.True, "Door throat remains empty.");
            Verify(layout, blocks, Config());
        }

        [Test]
        public void OrganicLFootprintRoutesAroundMissingQuadrantWithoutGapCakes()
        {
            var volumes = new[] { new Bounds(new Vector3(-3f, 2f, 0f), new Vector3(6f, 4f, 12f)),
                new Bounds(new Vector3(3f, 2f, -3f), new Vector3(6f, 4f, 6f)) };
            var room = new LevelRoom(1, Vector3.up * 2f, new Vector3(12f, 4f, 12f), cells: volumes);
            var layout = Layout(room, new[] { new ProceduralDoorPlan(1, 2, new Vector3(-3f, 0f, 6f), true),
                new ProceduralDoorPlan(1, 3, new Vector3(6f, 0f, -3f), false) });
            var blocks = new[] { Floor(1, -3f, 0f, 6f, 12f), Floor(1, 3f, -3f, 6f, 6f) };
            new ProceduralCakeLinePresenter().Apply(layout, Config(), blocks, .5f, 2f);
            Assert.That(layout.Graph.Anchors.Count, Is.GreaterThan(5));
            Assert.That(layout.Graph.Anchors.All(a => a.Position.x <= -.5f || a.Position.z <= -.5f), Is.True);
            Verify(layout, blocks, Config());
        }

        [Test]
        public void ThinGapAndSealedDoorCannotBeBridgedByAContinuousLine()
        {
            var room = new LevelRoom(1, Vector3.up * 2f, new Vector3(12f, 4f, 4f));
            var layout = Layout(room, new[] { new ProceduralDoorPlan(1, 2, new Vector3(-6f, 0f, 0f), false),
                new ProceduralDoorPlan(1, 3, new Vector3(6f, 0f, 0f), false) });
            var split = new[] { Floor(1, -3.025f, 0f, 5.95f, 4f), Floor(1, 3.025f, 0f, 5.95f, 4f) };
            Assert.Throws<InvalidOperationException>(() => new ProceduralCakeLinePresenter().Apply(layout, Config(), split, .5f, 2f));
            var blocked = new[] { Floor(1, 0f, 0f, 12f, 4f),
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, Vector3.up, new Vector3(.1f, 2f, 4f)) };
            Assert.Throws<InvalidOperationException>(() => new ProceduralCakeLinePresenter().Apply(layout, Config(), blocked, .5f, 2f));
        }

        private static void Verify(ProceduralLayout layout, IReadOnlyList<ProceduralBlock> blocks, ProceduralConfig config)
        {
            Assert.That(layout.CakeLines, Is.Not.Empty);
            Assert.That(layout.Graph.Anchors.Select(a => a.Id).Distinct().Count(), Is.EqualTo(layout.Graph.Anchors.Count));
            Assert.That(layout.CakeLines.SelectMany(l => l.Anchors), Is.EqualTo(layout.Graph.Anchors));
            foreach (var line in layout.CakeLines)
            {
                var direction = line.Anchors.Count > 1 ? line.Anchors[1].Position - line.Anchors[0].Position : Vector3.zero;
                for (int i = 1; i < line.Anchors.Count; i++)
                {
                    var delta = line.Anchors[i].Position - line.Anchors[i - 1].Position;
                    Assert.That(delta.magnitude, Is.EqualTo(config.RouteCakeSpacing).Within(.001f));
                    Assert.That((delta - direction).magnitude, Is.LessThan(.001f));
                }
            }
            foreach (var anchor in layout.Graph.Anchors)
            {
                var room = layout.Graph.Rooms.Single(r => r.Id == anchor.RoomId);
                foreach (float x in new[] { -.5f, 0f, .5f }) foreach (float z in new[] { -.5f, 0f, .5f })
                    Assert.That(room.ContainsXZ(anchor.Position + new Vector3(x, 0f, z)), Is.True, "Standing footprint");
                var torso = anchor.Position; torso.y = 1f;
                foreach (var block in blocks.Where(b => b.HasCollision && b.Kind != ProceduralSurfaceKind.Floor))
                {
                    var q = block.Rotation; var p = new Quaternion(-q.x, -q.y, -q.z, q.w) * (torso - block.Center);
                    Assert.That(Math.Abs(p.x) >= block.Size.x * .5f + .5f - .001f ||
                        Math.Abs(p.z) >= block.Size.z * .5f + .5f - .001f || Math.Abs(p.y) >= block.Size.y * .5f + 1f - .001f,
                        Is.True, "Agent overlaps " + block.PieceId);
                }
            }
            var navigation = new ProceduralNavFallbackPresenter();
            Assert.That(navigation.RequiredPositions(layout).Count(p => p.label.StartsWith("cake")), Is.EqualTo(layout.CakeLines.Count));
            Assert.That(navigation.RequiredPositions(layout, true).Count(p => p.label.StartsWith("cake")), Is.EqualTo(layout.Graph.Anchors.Count));
        }
        private static ProceduralLayout Layout(LevelRoom room, ProceduralDoorPlan[] doors)
        {
            var layout = new ProceduralLayout();
            Property(layout, "Graph", new LevelGraph(new[] { room }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1,
                new Vector3(room.Bounds.min.x + 1f, 0f, room.Bounds.min.z + 1f)));
            Property(layout, "Doors", doors); Property(layout, "HunterSpawnPositions", Array.Empty<Vector3>());
            return layout;
        }
        private static ProceduralBlock Floor(int id, float x, float z, float width, float depth)
            => new ProceduralBlock(id, ProceduralSurfaceKind.Floor, new Vector3(x, -.1f, z), new Vector3(width, .2f, depth));
        private static ProceduralConfig Config()
        {
            var c = Empty<ProceduralConfig>(); Field(c, "_routeCakeSpacing", 1.5f); Field(c, "_anchorHeight", .05f);
            Field(c, "_doorWidth", 3.2f); Field(c, "_doorHeight", 2.8f); Field(c, "_templateExitCakeClearance", 1.5f); return c;
        }
        private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static ProceduralTemplateCatalogue Read(string theme) => ProceduralRoomManifestSetup.Parse(
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Kit/" + theme + "Kit.manifest.json"),
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Rooms/" + theme + "Rooms.manifest.json"));
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
