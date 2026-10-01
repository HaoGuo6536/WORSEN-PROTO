// ============================================================================
// ExpeditionMimicUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Proves Mimics occupy false walking-route sites rather than real cake anchors
//   or generic hunter spawns. Population reconciliation is absolute per floor,
//   preserving retained duplicates and Nothing extras without recursive growth.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Expedition.
// KEY RESPONSIBILITIES:
//   - Check site geometry, reachability, room exclusion and occupied-site admission.
//   - Check separate Mimic allocation, hard shortfalls and the three-extra cap.
// DEPENDENCIES:
//   - Core graphs, Expedition pure rules, Floor guidance and NUnit.
// USAGE NOTES:
//   Injected lengths represent complete native paths; no live navigation is claimed.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionMimicUtilityTests
    {
        private static LevelGraph Graph(bool pocket = false, bool obstructingCake = false)
        {
            var anchors = new List<LevelAnchor> {
                new LevelAnchor(1, 1, CakeAnchorType.Flow, Vector3.zero),
                new LevelAnchor(2, 1, CakeAnchorType.Flow, Vector3.right * 2) };
            for (int i = 0; i < 8; i++) anchors.Add(new LevelAnchor(3 + i, 2, CakeAnchorType.Flow, new Vector3(12 + i * 2, .2f, 0)));
            if (obstructingCake) anchors.Add(new LevelAnchor(99, 2, CakeAnchorType.Flow, new Vector3(13, .2f, 0)));
            return new LevelGraph(new[] { new LevelRoom(1, Vector3.up * 2, new Vector3(8, 4, 8)),
                new LevelRoom(2, new Vector3(20, 2, 0), new Vector3(20, 4, 8), pocket: pocket) },
                new[] { new LevelEdge(1, 1, 2, true) }, anchors, 2, new Vector3(28, 0, 0));
        }
        private static float Path(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        private static IReadOnlyList<Vector3> Sites(LevelGraph graph, IReadOnlyList<Vector3> occupied = null,
            Func<Vector3, Vector3, float> path = null, RoomPhase phase = RoomPhase.Open) =>
            ExpeditionMimicUtility.Sites(graph, Vector3.zero, 2f, occupied,
                new Dictionary<int, RoomPhase> { [1] = RoomPhase.Open, [2] = phase }, path ?? Path);
        [Test] public void FalseSitesLieBetweenRealCakesOutsideSpawnRoomAndNeverBecomeArrowCandidates()
        {
            var graph = Graph(); var sites = Sites(graph);
            Assert.That(sites.Count, Is.GreaterThanOrEqualTo(4));
            foreach (var site in sites)
            {
                Assert.That(graph.Rooms[0].ContainsXZ(site), Is.False);
                Assert.That(graph.Rooms[1].ContainsXZ(site), Is.True);
                Assert.That(graph.Anchors.Any(a => Path(a.Position, site) < .99f), Is.False);
                Assert.That(graph.Anchors.Count(a => Math.Abs(Path(a.Position, site) - 1) < .001f), Is.EqualTo(2));
            }
            var guidance = new FloorGuidanceController(new FloorGuidanceBehaviorState());
            var white = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.right, graph.Anchors[2].Position, graph.Anchors[2].Id);
            foreach (var golden in new[] { false, true })
            {
                guidance.ReceiveMimic(new MimicFact(new EntityId(-1), new EntityId(1), MimicFactKind.Pose, 1, sites[0], golden: golden));
                Assert.That(guidance.Apply(new[] { white }, new EntityId(1), Vector3.zero)[0], Is.EqualTo(white));
            }
        }
        [Test] public void OccupiedCakesPocketsClosingRoomsAndFailedOrBentRoutesAreRejected()
        {
            Assert.That(Sites(Graph(), new[] { new Vector3(13, 0, 0) }), Has.No.Member(new Vector3(13, 0, 0)));
            Assert.That(Sites(Graph(obstructingCake: true)), Has.No.Member(new Vector3(13, 0, 0)));
            Assert.That(Sites(Graph(true)), Is.Empty);
            Assert.That(Sites(Graph(), phase: RoomPhase.Telegraph), Is.Empty);
            Assert.That(Sites(Graph(), path: (a, b) => float.PositiveInfinity), Is.Empty);
            Assert.That(Sites(Graph(), path: (a, b) => Path(a, b) + 1), Is.Empty);
            Assert.That(Sites(Graph(), path: (a, b) => a == Vector3.zero ? float.PositiveInfinity : Path(a, b)), Is.Empty);
        }
        private static ExpeditionSessionController Begin(out ExpeditionSessionBehaviorState state, params string[] keys)
        {
            state = new ExpeditionSessionBehaviorState(); var c = new ExpeditionSessionController(state); c.Bind(SceneKey.HorrorRun);
            c.Queue(new ProgressionGenerationRequest(1, 17, 5, false,
                new ProgressionEffects(1, 1, 1, 1, 100, 100, keys.Length, activeThreatIds: keys)));
            c.Begin(1); return c;
        }
        [Test] public void RetainedAndNothingMimicsUseSeparateFalseSitesAndNeverHunterPositions()
        {
            var c = Begin(out var state, "mimic", "echo", "mimic");
            var sites = Sites(Graph());
            var requests = c.HunterSpawns("echo", new[] { Vector3.back * 100 }, extraHunters: new[] { "mimic" }, mimicSites: sites);
            Assert.That(state.HunterSpawnShortfall, Is.Zero);
            Assert.That(requests.Select(r => r.ArchetypeKey), Is.EqualTo(new[] { "mimic", "echo", "mimic", "mimic" }));
            Assert.That(requests.Where(r => r.ArchetypeKey == "mimic").Select(r => r.Position), Is.EqualTo(sites.Take(3)));
            Assert.That(requests[1].Position, Is.EqualTo(Vector3.back * 100));
            c.RecordPlayer(new EntityId(1));
            for (int i = 0; i < requests.Count; i++) c.RecordHunter(new EntityId(-1 - i));
            c.Ready();
            Assert.That(c.MissingMimics(99, new[] { "mimic" }), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                var spawn = c.HunterSpawn("mimic", sites[i + 3]);
                Assert.That(spawn.DuplicateIndex, Is.EqualTo(i + 3)); c.RecordHunter(new EntityId(-10 - i));
            }
            Assert.That(c.MissingMimics(99, new[] { "mimic" }), Is.Zero, "Extra actors cannot recursively produce extra trios.");
            Assert.That(c.MissingMimics(1, new[] { "mimic" }), Is.Zero);
        }
        [Test] public void MissingOrDuplicateFalseSitesFailClosedRatherThanUsingHunterPoints()
        {
            var c = Begin(out var state, "mimic", "mimic");
            Assert.That(c.HunterSpawns("echo", new[] { Vector3.zero, Vector3.one }), Is.Empty);
            Assert.That(state.HunterSpawnShortfall, Is.EqualTo(2));
            var site = new Vector3(13, 0, 0);
            Assert.That(c.HunterSpawns("echo", new[] { Vector3.zero, Vector3.one }, mimicSites: new[] { site, site }).Count, Is.EqualTo(1));
            Assert.That(state.HunterSpawnShortfall, Is.EqualTo(1));
            c.RecordPlayer(new EntityId(1)); c.RecordHunter(new EntityId(-1)); Assert.Throws<InvalidOperationException>(c.Ready);
        }
    }
}
