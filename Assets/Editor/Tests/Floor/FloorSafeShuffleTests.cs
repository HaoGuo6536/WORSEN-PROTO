// ============================================================================
// FloorSafeShuffleTests.cs
// ============================================================================
// PURPOSE:
//   Verifies deterministic collapse ignores retired shuffle/speed/route protection hooks.
//   Pure fixtures retain exact timing, exit immunity and floor-local Wax Heart coverage.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor.
// KEY RESPONSIBILITIES:
//   - Check farthest-first/id-tied order, moving players, no cake loss and exit immunity.
//   - Check unscaled ten-second phases, the actual round and one Wax Heart before Wax Ward.
// DEPENDENCIES:
//   Core, Floor, Player read-only state, NUnit and managed fixture config.
// USAGE NOTES:
//   Edit Mode pure controller tests; time, positions and randomness are explicit.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorSafeShuffleTests
    {
        private FloorConfig config;
        private PlayerBehaviorState player;
        private LevelGraph graph;
        [SetUp]
        public void Setup()
        {
            config = FloorCakeRulesTests.Config();
            player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Position(1) };
            graph = LevelGraphUtility.Build(Enumerable.Range(1, 5).Select(i => new LevelRoom(i, Position(i), Vector3.one * 8f)).ToArray(),
                new[] { new LevelEdge(1, 1, 2, true), new LevelEdge(2, 2, 5, true), new LevelEdge(3, 1, 3, true),
                    new LevelEdge(4, 3, 5, true), new LevelEdge(5, 4, 5, true) },
                new[] { new LevelAnchor(11, 1, CakeAnchorType.Flow, Position(1)) }, 5, Position(5));
        }

        private FloorController Start(int seed, out FloorBehaviorState state, bool shuffled = true, bool fast = false, bool wax = false)
        {
            state = new FloorBehaviorState(); var c = new FloorController(state, config, new System.Random(seed));
            c.Initialize(graph, new[] { player }, 1, fast, shuffled, round: 7, waxHeart: wax);
            Assert.That(state.Round, Is.EqualTo(7)); c.Collect(player.Id, 11, PickupKind.Cake, 0, out _); return c;
        }
        [Test]
        public void CollapseOrderIsFarthestFirstThenRoomIdAcrossSeedsAndNeverClosesExit()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var a = Start(seed, out var state); var b = Start(seed, out _, shuffled: false);
                var sequence = a.Tick(1000f, 1).Where(f => f.Phase == RoomPhase.Closed).Select(f => f.RoomId).ToArray();
                Assert.That(sequence, Is.EqualTo(b.Tick(1000f, 1).Where(f => f.Phase == RoomPhase.Closed).Select(f => f.RoomId)));
                Assert.That(sequence, Is.EqualTo(new[] { 1, 2, 3, 4 }));
                foreach (int room in sequence) Assert.That(state.RoomPhases[room], Is.EqualTo(RoomPhase.Closed));
                Assert.That(state.RoomPhases[5], Is.EqualTo(RoomPhase.Open));
                Assert.That(a.DrainCakeLosses(), Is.Empty);
            }
        }
        [Test]
        public void MovingIntoWarningRoomOrUnknownOccupancyCannotFreezeTenSecondProgression()
        {
            var c = Start(3, out var state); var first = c.Tick(0f, 1).Single();
            player.Position = Position(first.RoomId); c.Tick(4f, 2);
            Assert.That(state.RoomPhases[first.RoomId], Is.EqualTo(RoomPhase.Tearing));
            player.Position = Vector3.one * 10000f; c.Tick(2f, 3);
            Assert.That(state.RoomPhases[first.RoomId], Is.EqualTo(RoomPhase.Encroaching));
            player.Position = Position(5); c.Tick(4f, 4);
            Assert.That(state.RoomPhases[first.RoomId], Is.EqualTo(RoomPhase.Closed));
            Assert.That(state.RoomPhases[5], Is.EqualTo(RoomPhase.Open));
            Assert.That(c.DrainCakeLosses(), Is.Empty);
            Assert.That(state.CakeCount, Is.EqualTo(1));
        }
        [Test]
        public void FasterCollapseHookCannotScaleAnyScheduledPhase()
        {
            var ordinary = Start(7, out var normalState, shuffled: false);
            var fast = Start(7, out var fastState, shuffled: false, fast: true);
            foreach (float dt in new[] { 0f, 4f, 2f, 4f, 50f })
            {
                var normalFacts = ordinary.Tick(dt, 1).Select(f => (f.RoomId, f.Phase));
                Assert.That(fast.Tick(dt, 1).Select(f => (f.RoomId, f.Phase)), Is.EqualTo(normalFacts));
                Assert.That(fastState.RoomPhases, Is.EquivalentTo(normalState.RoomPhases));
                Assert.That(fast.DrainCakeLosses(), Is.Empty);
            }
            Assert.That(normalState.RoomPhases[4], Is.EqualTo(RoomPhase.Closed));
            Assert.That(fastState.RoomPhases[5], Is.EqualTo(RoomPhase.Open));
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
    }
}
