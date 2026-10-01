// ============================================================================
// ProceduralTemplateStoreyUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises upper galleries on real template content without Unity objects.
//   Uses the organic route and navigation contracts to distinguish hunter ramps
//   from player-only ledges and drops, and checks physical upper cake lines.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Prove the round/probability gate and deterministic gallery selection.
//   - Require supported landings, upper cake lines and hunter-safe area masks.
// DEPENDENCIES:
//   - NUnit, Core and Procedural pure layers; real manifest test helpers.
// USAGE NOTES:
//   Native bidirectional navigation and visual playtesting remain separate gates.
// ============================================================================
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using static Worsen.Tests.Procedural.ProceduralTemplateSeamPresenterTests;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralTemplateStoreyUtilityTests
    {
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void RealTemplatesHaveDeterministicRoundThreeUpperRoutes(string theme)
        {
            var c = Config(theme); Configure(c); var catalogue = c.RoomCatalogue.Catalogues[0];
            int count = 0;
            foreach (var template in catalogue.Templates)
            foreach (int socket in Enumerable.Range(0, template.Doors.Length))
            foreach (int turn in Enumerable.Range(0, 4))
            {
                var a = Layout(catalogue, template, 3, socket, turn); var b = Layout(catalogue, template, 3, socket, turn);
                ProceduralTemplateStoreyUtility.Apply(a, c, new System.Random(17));
                ProceduralTemplateStoreyUtility.Apply(b, c, new System.Random(17));
                Assert.That(ProceduralStoreyUtility.Manifest(a, c), Is.EqualTo(ProceduralStoreyUtility.Manifest(b, c)));
                if (a.Storeys.Count == 0) continue;
                count++;
                var blocks = new ProceduralTemplateGeometryPresenter().Build(a, c, Driver());
                new ProceduralStoreyPresenter().ValidateLandings(a, blocks);
                new ProceduralCakeLinePresenter().Apply(a, c, blocks, .5f, 2f);
                new ProceduralNavFallbackPresenter().ValidateTemplate(a, blocks, .5f, 2f);
                Export(template.Id + "-door" + socket + "-turn" + turn, a, blocks);
                Assert.That(a.Graph.Anchors.Any(p => p.RoomId == 2 && p.Position.y > 3f), Is.True, template.Id);
                Assert.That(a.VerticalRoutes.Single(r => r.Kind == ProceduralVerticalKind.Ramp).Access, Is.EqualTo(TraversalAccess.All));
                Assert.That(a.VerticalRoutes.Where(r => r.Kind != ProceduralVerticalKind.Ramp).All(r => r.Access == TraversalAccess.Player && !r.Bidirectional), Is.True);
                Assert.That(blocks.Where(p => p.Role == ProceduralBlockRole.PlayerOnly).All(p => new ProceduralNavigationPresenter().Area(p) == 1), Is.True);
                foreach (int round in new[] { 1, 2 })
                {
                    var early = Layout(catalogue, template, round); ProceduralTemplateStoreyUtility.Apply(early, c, new System.Random(17));
                    Assert.That(early.Storeys, Is.Empty);
                }
                Field(c, "_storeyProbability", 0f);
                var disabled = Layout(catalogue, template, 3); ProceduralTemplateStoreyUtility.Apply(disabled, c, new System.Random(17));
                Assert.That(disabled.Storeys, Is.Empty); Field(c, "_storeyProbability", 1f);
                TestContext.WriteLine("TEMPLATE_STOREY theme=" + theme + " room=" + template.Id + " origin=" + a.Storeys[0].Origin);
            }
            Assert.That(count, Is.GreaterThan(0), theme + " needs a fitting authored room, not a permanently dead round gate.");
        }
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void RoundThreeSeedSampleAdmitsRealTemplateGalleries(string theme)
        {
            var c = Config(theme); Configure(c); int floors = 0, galleries = 0;
            for (int seed = 0; seed < 8; seed++)
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int current = seed + attempt * ProceduralGenerationController.SeedStride;
                var data = new ProceduralThemeData(theme.ToLowerInvariant(), true, Array.Empty<string>(), "", "", "", "", "", default, default, default, 0f);
                if (!new ProceduralTemplateController(c, new System.Random(ProceduralController.LayoutSeed(current, 3)))
                    .TryGenerate(current, 3, data, false, 1, out var layout, out var reason))
                { TestContext.WriteLine("STOREY_PLACEMENT seed=" + current + " " + reason); continue; }
                try
                {
                    ProceduralStoreyUtility.Validate(layout, c);
                    var blocks = new ProceduralTemplateGeometryPresenter().Build(layout, c, Driver());
                    new ProceduralStoreyPresenter().ValidateLandings(layout, blocks);
                    new ProceduralCakeLinePresenter().Apply(layout, c, blocks, .5f, 2f);
                    new ProceduralNavFallbackPresenter().ValidateTemplate(layout, blocks, .5f, 2f);
                    foreach (var storey in layout.Storeys)
                        Assert.That(layout.Graph.Anchors.Any(a => a.RoomId == storey.RoomId && a.Position.y >= storey.Height), Is.True);
                    if (layout.Storeys.Count > 0) Export(theme + "-round3-seed" + seed, layout, blocks);
                    floors++; galleries += layout.Storeys.Count; break;
                }
                catch (InvalidOperationException error) { TestContext.WriteLine("STOREY_RETRY seed=" + current + " " + error.Message); }
            }
            TestContext.WriteLine("STOREY_SWEEP theme=" + theme + " floors=" + floors + " galleries=" + galleries);
            Assert.That(floors, Is.EqualTo(8)); Assert.That(galleries, Is.GreaterThan(0));
        }
        private static void Export(string name, ProceduralLayout layout, System.Collections.Generic.IReadOnlyList<ProceduralBlock> blocks)
        {
            string directory = Environment.GetEnvironmentVariable("WORSEN_GEOMETRY_EVIDENCE");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            string N(float v) => v.ToString("R", CultureInfo.InvariantCulture);
            File.WriteAllLines(Path.Combine(directory, name + ".tsv"), blocks.Select(b => string.Join("\t", new[] {
                b.Kind.ToString(), b.Role.ToString(), b.PieceId ?? "", N(b.Center.x), N(b.Center.y), N(b.Center.z),
                N(b.Size.x), N(b.Size.y), N(b.Size.z), N(b.Rotation.x), N(b.Rotation.y), N(b.Rotation.z), N(b.Rotation.w) })).Concat(
                layout.Graph.Anchors.Select(a => "Cake\t\t\t" + N(a.Position.x) + "\t" + N(a.Position.y) + "\t" + N(a.Position.z))));
        }
        private static void Configure(ProceduralConfig c)
        {
            Field(c, "_multiFloorStartRound", 3); Field(c, "_storeyProbability", 1f); Field(c, "_storeyHeight", 3.2f);
            Field(c, "_baseLedgeMinimumHeight", .5f); Field(c, "_baseLedgeMaximumHeight", 1.8f); Field(c, "_baseLedgeReach", 1.2f);
        }
        private static ProceduralLayout Layout(ProceduralTemplateCatalogue catalogue, ProceduralRoomTemplate template, int round, int socket = 0, int turn = 0)
        {
            var layout = new ProceduralLayout(); var room = new ProceduralTemplateRoom { RoomId = 2, Template = template,
                OpenDoors = new[] { socket }, Turns = turn };
            var volumes = ProceduralTemplateUtility.Volumes(ProceduralTemplateUtility.OccupiedCells(room), Vector2.zero, template.Height, 1f);
            var bounds = volumes[0]; foreach (var v in volumes) bounds.Encapsulate(v);
            Set(layout, "TemplateRooms", new[] { room }); Set(layout, "TemplateCatalogue", catalogue); Set(layout, "RoundIndex", round);
            Set(layout, "Graph", new LevelGraph(new[] { new LevelRoom(1, new Vector3(-100f, 2f, -100f), new Vector3(12f, 4f, 12f), pocket: true),
                new LevelRoom(2, bounds.center, bounds.size, cells: volumes) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, new Vector3(-100f, 0f, -100f)));
            Set(layout, "Modules", new[] { new ProceduralRoomModule(2, ProceduralModuleKind.TorchGallery, true,
                ProceduralTemplateUtility.OccupiedCells(room).ToArray(), traversalObstacles: false) });
            Set(layout, "Doors", room.OpenDoors.Select(i => template.Doors[i]).Select(d => new ProceduralDoorPlan(2, 3,
                ProceduralTemplateUtility.Point(room, ProceduralTemplateUtility.Door(d), Vector2.zero),
                ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(d.Side), turn).x == 0)).ToArray());
            Set(layout, "PlayerSpawnPosition", new Vector3(-100f, 0f, -104f)); Set(layout, "HunterSpawnPositions", Array.Empty<Vector3>());
            return layout;
        }
    }
}
