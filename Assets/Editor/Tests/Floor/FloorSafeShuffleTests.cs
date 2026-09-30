// ============================================================================
// FloorSafeShuffleTests.cs
// ============================================================================
// PURPOSE:
//   Verifies shuffled collapse preserves changing escape routes and floor-local Wax Heart.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor.
// KEY RESPONSIBILITIES:
//   - Check deterministic seeded variation, directed routes, moving players and safe exits.
//   - Check the actual round, 0.75 timer scale and one Wax Heart before Wax Ward.
// DEPENDENCIES:
//   Core, Floor, Player read-only state, NUnit and transient FloorConfig.
// USAGE NOTES:
//   Edit Mode pure controller tests; time, positions and randomness are explicit.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorSafeShuffleTests
    {
        private FloorConfig config;
        private PlayerBehaviorState player;
        private LevelGraph graph;
        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<FloorConfig>(); Set(config, "_useRoomCakeDensity", false);
            player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Position(1) };
            graph = LevelGraphUtility.Build(Enumerable.Range(1, 5).Select(i => new LevelRoom(i, Position(i), Vector3.one * 8f)).ToArray(),
                new[] { new LevelEdge(1, 1, 2, true), new LevelEdge(2, 2, 5, true), new LevelEdge(3, 1, 3, true),
                    new LevelEdge(4, 3, 5, true), new LevelEdge(5, 4, 5, true) },
                new[] { new LevelAnchor(11, 1, CakeAnchorType.Flow, Position(1)) }, 5, Position(5));
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);
        private FloorController Start(int seed, out FloorBehaviorState state, bool shuffled = true, bool fast = false, bool wax = false)
        {
            state = new FloorBehaviorState(); var c = new FloorController(state, config, new System.Random(seed));
            c.Initialize(graph, new[] { player }, 1, fast, shuffled, round: 7, waxHeart: wax);
            Assert.That(state.Round, Is.EqualTo(7)); c.Collect(player.Id, 11, PickupKind.Cake, 0, out _); return c;
        }
        [Test]
        public void ShuffleIsSeededVariesOutsideRouteAndNeverClosesExitOrCurrentShortestRoute()
        {
            var sequences = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                var a = Start(seed, out var state); var b = Start(seed, out _);
                var sequence = a.Tick(1000f, 1).Where(f => f.Phase == RoomPhase.Closed).Select(f => f.RoomId).ToArray();
                Assert.That(sequence, Is.EqualTo(b.Tick(1000f, 1).Where(f => f.Phase == RoomPhase.Closed).Select(f => f.RoomId)));
                Assert.That(sequence, Is.EquivalentTo(new[] { 3, 4 })); sequences.Add(string.Join(",", sequence));
                foreach (int room in new[] { 1, 2, 5 }) Assert.That(state.RoomPhases[room], Is.EqualTo(RoomPhase.Open));
                player.Position = Position(2); a.Tick(100f, 2);
                Assert.That(state.RoomPhases[1], Is.EqualTo(RoomPhase.Closed)); Assert.That(state.RoomPhases[2], Is.EqualTo(RoomPhase.Open));
                player.Position = Position(5); a.Tick(100f, 3);
                Assert.That(state.RoomPhases[2], Is.EqualTo(RoomPhase.Closed)); Assert.That(state.RoomPhases[5], Is.EqualTo(RoomPhase.Open));
                player.Position = Position(1);
            }
            Assert.That(sequences.Count, Is.GreaterThan(1));
        }
        [Test]
        public void MovingIntoAWarningRoomFreezesItsRemainingTransitionsAndUnknownOccupancyFailsClosed()
        {
            var c = Start(3, out var state); var first = c.Tick(0f, 1).Single();
            player.Position = Position(first.RoomId); c.Tick(100f, 2);
            Assert.That(state.RoomPhases[first.RoomId], Is.EqualTo(RoomPhase.Telegraph));
            player.Position = Vector3.one * 10000f; c.Tick(100f, 3);
            Assert.That(state.RoomPhases[first.RoomId], Is.EqualTo(RoomPhase.Telegraph));
            player.Position = Position(5); c.Tick(100f, 4);
            Assert.That(state.RoomPhases[first.RoomId], Is.EqualTo(RoomPhase.Closed));
        }
        [Test]
        public void FasterCollapseScalesAllScheduledPhasesToThreeQuarters()
        {
            var ordinary = Start(7, out _, shuffled: false); var fast = Start(7, out _, shuffled: false, fast: true);
            foreach (float dt in new[] { 6f, 2f, 6f, 14f, 14f })
            {
                var normalFacts = ordinary.Tick(dt, 1).Select(f => (f.RoomId, f.Phase));
                Assert.That(fast.Tick(dt * .75f, 1).Select(f => (f.RoomId, f.Phase)), Is.EqualTo(normalFacts));
            }
        }
        [Test]
        public void WaxHeartBreaksOnlyFirstGrabPreservesWardAndRearmsOnlyAtFloorInitialization()
        {
            var c = Start(7, out var state, wax: true);
            var memory = (FloorHandBehaviorState)Get(state, "Hands"); var hands = new FloorHandController(memory, config);
            var probe = new FloorHandProbe(1, 0, Position(1), 0f, true, Vector3.right);
            hands.ArmWaxWard(player.Id);
            hands.Tick(player.Id, true, probe, 0f, 1, out _);
            hands.Tick(player.Id, true, probe, config.HandWarningDuration, 2, out var broken);
            Assert.That(broken.Kind, Is.EqualTo(CollapseHandEventKind.Escaped)); Assert.That(memory.WaxHeartAvailable, Is.False);
            Assert.That(memory.WaxWards.Contains(player.Id), Is.True);
            hands.Tick(player.Id, true, probe, config.HandCooldown, 3, out _);
            hands.Tick(player.Id, true, probe, 0f, 4, out _);
            hands.Tick(player.Id, true, probe, config.HandWarningDuration, 5, out broken);
            Assert.That(broken.Kind, Is.EqualTo(CollapseHandEventKind.Escaped)); Assert.That(memory.WaxWards, Is.Empty);
            hands.Tick(player.Id, true, probe, config.HandCooldown, 6, out _);
            hands.Tick(player.Id, true, probe, 0f, 7, out _);
            hands.Tick(player.Id, true, probe, config.HandWarningDuration, 8, out var grab);
            Assert.That(grab.Kind, Is.EqualTo(CollapseHandEventKind.Grabbed));
            c.Initialize(graph, new[] { player }, 1, waxHeart: true); Assert.That(memory.WaxHeartAvailable, Is.True);
            c.Initialize(graph, new[] { player }, 1); Assert.That(memory.WaxHeartAvailable, Is.False);
        }
        private static Vector3 Position(int room) => new Vector3(room * 10f, 0f, 0f);
        private static object Get(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    }
}
