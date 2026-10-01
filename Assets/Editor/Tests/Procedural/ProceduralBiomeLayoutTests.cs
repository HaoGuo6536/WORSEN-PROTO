// ============================================================================
// ProceduralBiomeLayoutTests.cs
// ============================================================================
// PURPOSE:
//   Exercises mixed furnished floors through placement and managed physical admission.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Measure template/furnished shares without omitting failed seed attempts.
//   - Assert per-room identity, transitions, shrine sockets and optional puzzle lanes.
//   - Emit layout coordinates for a headless plan review, not a Unity screenshot.
// DEPENDENCIES:
//   - NUnit, Core and production Procedural presenters/parser.
// USAGE NOTES:
//   Bounded retries mirror the existing seam sweep; native bake remains a Unity gate.
// ============================================================================
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralBiomeLayoutTests
    {
        [TestCase(1, 0)] [TestCase(1, 7)] [TestCase(2, 0)] [TestCase(2, 7)]
        [TestCase(3, 0)] [TestCase(3, 7)] [TestCase(4, 0)] [TestCase(4, 7)]
        [TestCase(5, 0)] [TestCase(5, 7)] [TestCase(7, 0)] [TestCase(7, 7)]
        [TestCase(6, 0)] [TestCase(6, 7)] [TestCase(8, 0)] [TestCase(8, 7)]
        [TestCase(9, 0)] [TestCase(9, 7)]
        [TestCase(10, 0)] [TestCase(10, 7)]
        public void SeedSampleAdmitsFurnishedBiomesShrinesAndPuzzles(int round, int seed)
            => CheckFloor(round, seed, round < 3 ? 0 : round < 6 ? 1 : round < 10 ? 2 : 3);

        [Test]
        public void MoreShrinesKeepsFourTerminalDestinations() => CheckFloor(10, 23, 4);

        private static void CheckFloor(int round, int seed, int shrineCount)
        {
            var config = Config(); var driver = ProceduralTemplateSeamPresenterTests.Driver();
            ProceduralLayout admitted = null; string reason = ""; int retries = 0;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int current = seed + attempt * ProceduralGenerationController.SeedStride;
                if (!new ProceduralTemplateController(config, new System.Random(ProceduralController.LayoutSeed(current, round)))
                    .TryGenerate(current, round, ProceduralBiomeUtilityTests.Theme("castle"), false, 1, out var layout, out reason, seed, shrineCount)) Assert.Fail(reason);
                try
                {
                    var blocks = new ProceduralTemplateGeometryPresenter().Build(layout, config, driver);
                    var puzzles = new ProceduralPuzzleLayoutPresenter();
                    ProceduralTemplateSeamPresenterTests.Set(layout, "Puzzles", puzzles.Build(layout, config.Challenges, blocks, new System.Random(current)));
                    new ProceduralCakeLinePresenter().Apply(layout, config, blocks, .5f, 2f);
                    new ProceduralNavFallbackPresenter().ValidateTemplate(layout, blocks, .5f, 2f);
                    var shrines = new ProceduralShrineSitePresenter().Build(layout, config, blocks, .5f, 2f);
                    Assert.That(shrines.Count, Is.EqualTo(shrineCount));
                    foreach (var shrine in shrines)
                    {
                        var room = layout.TemplateRooms.Single(r => r.RoomId == shrine.RoomId);
                        Assert.That(room.Template.Kind, Is.EqualTo("shrine"));
                        Assert.That(shrine.Position, Is.EqualTo(ProceduralTemplateUtility.Point(room, room.Template.ShrineSockets[0].Position, layout.Origin)));
                        Assert.That(shrine.GapEdge, Is.True);
                        Assert.That(layout.Graph.Edges.Any(e => e.FromRoomId == shrine.DestinationPocketRoomId || e.ToRoomId == shrine.DestinationPocketRoomId), Is.False);
                    }
                    Assert.That(layout.Puzzles.Count, Is.EqualTo(layout.TemplateRooms.Count(r => r.Template.Kind == "puzzle")));
                    foreach (var puzzle in layout.Puzzles)
                    {
                        var room = layout.TemplateRooms.Single(r => r.RoomId == puzzle.RoomId);
                        Assert.That(puzzle.Origin, Is.EqualTo(ProceduralTemplateUtility.Point(room, room.Template.PuzzleSockets.Origin, layout.Origin)));
                        for (int i = 0; i < 3; i++) Assert.That((puzzles.Point(puzzle, config.Challenges, i) -
                            ProceduralTemplateUtility.Point(room, room.Template.PuzzleSockets.Steps[i], layout.Origin)).magnitude, Is.LessThan(.001f));
                        Assert.That(layout.Graph.Anchors.Any(a => a.Id == puzzle.Reward.Id), Is.False);
                    }
                    admitted = layout; retries = attempt; break;
                }
                catch (InvalidOperationException error) { Assert.Fail(error.Message); }
            }
            TestContext.Progress.WriteLine("BIOME_SAMPLE round=" + round + " seed=" + seed + " template=" + (admitted?.TemplateRooms.Count ?? 0) +
                " organicFallbackNeeded=" + (admitted == null ? 1 : 0) + " furnished=" + (admitted?.TemplateRooms.Count(r => r.Template.FurnishingVersion == 1) ?? 0) + " retries=" + retries + " reason=" + reason);
            Assert.That(admitted, Is.Not.Null, reason);
            Assert.That(retries, Is.Zero, "The sample must not hide an organic-fallback attempt behind a retry: " + reason);
            Assert.That(new ProceduralTemplateController(config, new System.Random(ProceduralController.LayoutSeed(admitted.Seed, round)))
                .TryGenerate(admitted.Seed, round, ProceduralBiomeUtilityTests.Theme("castle"), false, 1, out var replay, out _, seed, shrineCount), Is.True);
            Assert.That(ProceduralTemplateController.Manifest(replay), Is.EqualTo(ProceduralTemplateController.Manifest(admitted)));
            var map = admitted.RoomThemes; ProceduralBiomeUtilityTests.AssertContiguous(admitted.Graph, map);
            Assert.That(map.Values.Select(t => t.Id).Distinct().Count(), round == 1 ? Is.EqualTo(1) : Is.InRange(2, Math.Min(round, 4)));
            foreach (var door in admitted.Doors.Where(d => map[d.FromRoomId].Id != map[d.ToRoomId].Id))
            {
                var to = admitted.TemplateRooms.Single(r => r.RoomId == door.ToRoomId);
                Assert.That(to.Template.Transition, Is.Not.Null);
                Assert.That(to.Template.Transition.CompatibleThemes, Does.Contain(map[door.FromRoomId].Id));
            }
            foreach (var room in admitted.TemplateRooms) Assert.That(room.Catalogue.Theme, Is.EqualTo(map[room.RoomId].Id));
            string output = "Logs/AgentValidation/PLAN-026/biome-layout"; Directory.CreateDirectory(output);
            File.WriteAllLines(output + "/round-" + round + "-seed-" + seed + ".tsv", admitted.TemplateRooms.SelectMany(r =>
                ProceduralTemplateUtility.OccupiedCells(r).Select(c => string.Join("\t", r.RoomId, r.Catalogue.Theme, r.Template.Kind, r.Template.Id,
                    c.x.ToString(CultureInfo.InvariantCulture), c.y.ToString(CultureInfo.InvariantCulture), r.PocketId))));
            var drawnBlocks = new ProceduralTemplateGeometryPresenter().Build(admitted, config, driver);
            File.WriteAllLines(output + "/round-" + round + "-seed-" + seed + ".blocks.tsv", drawnBlocks.Select(b =>
            {
                var right = b.Rotation * Vector3.right;
                return string.Join("\t", b.RoomId, b.Kind, F(b.Center.x), F(b.Center.y), F(b.Center.z),
                    F(b.Size.x), F(b.Size.y), F(b.Size.z), F((float)Math.Atan2(right.z, right.x)));
            }));
            var markers = new ProceduralShrineSitePresenter().Build(admitted, config, drawnBlocks, .5f, 2f).Select(s =>
                string.Join("\t", s.RoomId, "shrine", F(s.Position.x), F(s.Position.z), "1", "1")).ToList();
            foreach (var plan in admitted.Puzzles)
            {
                markers.Add(string.Join("\t", plan.RoomId, "lane", F(plan.Origin.x), F(plan.Origin.z),
                    F(plan.AlongX ? config.Challenges.LaneLength : config.Challenges.LaneWidth),
                    F(plan.AlongX ? config.Challenges.LaneWidth : config.Challenges.LaneLength)));
                markers.Add(string.Join("\t", plan.RoomId, "goal", F(plan.Reward.Position.x), F(plan.Reward.Position.z), ".5", ".5"));
            }
            File.WriteAllLines(output + "/round-" + round + "-seed-" + seed + ".markers.tsv", markers);
        }
        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        internal static ProceduralConfig Config()
        {
            var c = ProceduralTemplateSeamPresenterTests.Config("Castle"); var data = ProceduralTemplateSeamPresenterTests.Empty<ProceduralRoomCatalogueData>();
            Field(data, "_catalogues", new[] { "Castle", "Hospital", "School", "Basement" }.Select(ProceduralFurnishedUtilityTests.Read).ToArray());
            Field(c, "_roomCatalogue", data); Field(c, "_themes", ProceduralBiomeUtilityTests.Themes()); Field(c, "_challenges", Challenges());
            Field(c, "_initialRoomCount", 7); Field(c, "_roomsPerRound", 2);
            Field(c, "_roomSize", 20f); Field(c, "_shrineSiteEnvelope", new Vector3(1.2f, 2.4f, 1.2f));
            Field(c, "_shrineSiteInset", 2f); Field(c, "_shrineSiteClearance", 2f); return c;
        }
        internal static ProceduralChallengeConfig Challenges()
        {
            var c = ProceduralTemplateSeamPresenterTests.Empty<ProceduralChallengeConfig>();
            Field(c, "_puzzleFirstRound", 3); Field(c, "_freezeFirstRound", 3); Field(c, "_gimmickFirstRound", 3); Field(c, "_gimmickFullRound", 8);
            Field(c, "_gimmickInitialBudget", 1); Field(c, "_gimmickMaximumBudget", 3);
            Field(c, "_laneLength", 8f); Field(c, "_laneWidth", 1.6f); Field(c, "_cageHeight", 2.5f); Field(c, "_panelThickness", .1f);
            Field(c, "_contactHeight", .25f); Field(c, "_vaultHeight", 1f); Field(c, "_clearance", .8f); Field(c, "_nearbyRadius", 6f);
            Field(c, "_sequenceSeconds", 6f); Field(c, "_segmentSeconds", 1.5f); Field(c, "_movingSpeed", 2f); return c;
        }
        private static void Field(object target, string name, object value) => ProceduralTemplateSeamPresenterTests.Field(target, name, value);
    }
}
