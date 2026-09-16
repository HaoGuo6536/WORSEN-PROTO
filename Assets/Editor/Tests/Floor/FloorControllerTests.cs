// ============================================================================
// FloorControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies collection and collapse rules against explicit level and player data.
//   Seeded selection, exact clock boundaries and confirmed terminal outcomes are exercised
//   without navigation queries, scene objects or a running Unity editor.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Cover Horror progression and Shift-to-run while preserving fixture motion intent.
//   - Verify deterministic placement, counters, cues and ordered room transitions.
//   - Reject invalid contacts and verify a fresh initialization clears prior life.
// DEPENDENCIES:
//   - Core level/floor values and Domain Floor pure classes.
//   - Domain Player read-only interface implemented by an immutable fixture.
//   - NUnit and System reflection for assertions and passive config construction.
// USAGE NOTES:
//   Config fixtures use an uninitialized managed object plus private field writes;
//   only pure getters are read, and no ScriptableObject engine API is invoked.
//   Randomness and elapsed seconds are supplied explicitly to every simulation.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    public sealed class FloorControllerTests
    {
        [Test]
        public void GeneratedCountUsesEveryAnchorWithoutChangingAuthoredConfig()
        {
            var config = Config(2);
            var state = new FloorBehaviorState();
            var controller = new FloorController(state, config, new System.Random(7));
            controller.Initialize(SingleRoom(7), Players(), 7);
            Assert.That(state.ActiveCakeAnchors.Count, Is.EqualTo(7));
            Assert.That(config.RequiredCakeCount, Is.EqualTo(2));
            foreach (var anchor in state.ActiveCakeAnchors.ToArray())
                Assert.That(controller.Collect(new EntityId(1), anchor.Id, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(state.ExitState, Is.EqualTo(ExitState.Open));
            controller.Initialize(SingleRoom(7), Players());
            Assert.That(state.ActiveCakeAnchors.Count, Is.EqualTo(2));
        }

        [Test]
        public void WeightedSelectionHasExactSeededOrderIndependentOfAuthoredInputOrder()
        {
            var anchors = new[]
            {
                Anchor(105, 1, CakeAnchorType.Vertical), Anchor(104, 1, CakeAnchorType.Risk),
                Anchor(103, 1, CakeAnchorType.Detour), Anchor(102, 1, CakeAnchorType.Precision),
                Anchor(101, 1, CakeAnchorType.Flow)
            };
            var first = Start(Graph(new[] { 1 }, new LevelEdge[0], anchors, 1), Config(5), 1337);
            var second = Start(Graph(new[] { 1 }, new LevelEdge[0], anchors.Reverse().ToArray(), 1), Config(5), 1337);

            var expected = new[] { 101, 102, 103, 105, 104 };
            CollectionAssert.AreEqual(expected, first.State.ActiveCakeAnchors.Select(anchor => anchor.Id));
            CollectionAssert.AreEqual(expected, second.State.ActiveCakeAnchors.Select(anchor => anchor.Id));
            Assert.That(first.State.ActiveCakeAnchors.Select(anchor => anchor.Id).Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void CollectionAcceptsLivingRegisteredPlayerOnceAndPublishesCommittedCounters()
        {
            var fixture = Start(SingleRoom(2), Config(2));

            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 27, out var fact), Is.True);
            Assert.That(fact.PlayerId, Is.EqualTo(new EntityId(1)));
            Assert.That(fact.AnchorId, Is.EqualTo(101));
            Assert.That(fact.Kind, Is.EqualTo(PickupKind.Cake));
            Assert.That(fact.CakeCount, Is.EqualTo(1));
            Assert.That(fact.GoldenCount, Is.Zero);
            Assert.That(fact.Tick, Is.EqualTo(27));
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 28, out _), Is.False);
            Assert.That(fixture.State.CakeCount, Is.EqualTo(1));
            Assert.That(fixture.State.ExitState, Is.EqualTo(ExitState.Locked));
            CollectionAssert.AreEqual(new[] { 102 }, fixture.State.ActiveCakeAnchors.Select(anchor => anchor.Id));
        }

        [Test]
        public void CollectionRejectsInvalidForeignDeadPlayerUnknownAnchorAndUnknownKind()
        {
            var players = new IReadOnlyPlayerState[] { new PlayerFixture(1), new PlayerFixture(2, false), null };
            var fixture = Start(SingleRoom(1), Config(1), players: players);

            Assert.That(fixture.Controller.Collect(EntityId.None, 101, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(99), 101, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(2), 101, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(1), 999, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, (PickupKind)77, 1, out _), Is.False);
            Assert.That(fixture.State.CakeCount, Is.Zero);
        }

        [Test]
        public void InsufficientAuthoredAnchorsFailExplicitlyWithoutReadyFloorOrDummyAnchors()
        {
            var state = new FloorBehaviorState();
            var controller = new FloorController(state, Config(3), new System.Random(1));

            var error = Assert.Throws<InvalidOperationException>(() => controller.Initialize(SingleRoom(2), Players()));

            StringAssert.Contains("3 weighted reachable anchors", error.Message);
            StringAssert.Contains("graph has 2", error.Message);
            Assert.That(state.IsReady, Is.False);
            Assert.That(state.ActiveCakeAnchors, Is.Empty);
        }

        [Test]
        public void ZeroWeightAnchorsAreExcludedAndCannotSatisfyRequiredCount()
        {
            var config = Config(1);
            Set(config, "_flowWeight", 0f);
            var graph = Graph(new[] { 1 }, new LevelEdge[0], new[]
            { Anchor(101, 1, CakeAnchorType.Flow), Anchor(102, 1, CakeAnchorType.Risk) }, 1);
            var fixture = Start(graph, config);

            CollectionAssert.AreEqual(new[] { 102 }, fixture.State.ActiveCakeAnchors.Select(anchor => anchor.Id));
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 1, out _), Is.False);
            var controller = new FloorController(new FloorBehaviorState(), config, new System.Random(1));
            Assert.Throws<InvalidOperationException>(() => controller.Initialize(SingleRoom(1), Players()));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidWeightsAreRejectedEvenWhenThatAnchorTypeIsNotPresent(float weight)
        {
            var config = Config(1);
            Set(config, "_verticalWeight", weight);
            var controller = new FloorController(new FloorBehaviorState(), config, new System.Random(1));

            Assert.Throws<ArgumentException>(() => controller.Initialize(SingleRoom(1), Players()));
        }

        [TestCase("_collapseInterval", 0f)]
        [TestCase("_telegraphDuration", -1f)]
        [TestCase("_directionCueInterval", float.NaN)]
        [TestCase("_collapseInterval", float.PositiveInfinity)]
        public void NonpositiveOrNonfiniteClockConfigurationIsRejected(string field, float value)
        {
            var config = Config(1);
            Set(config, field, value);
            var controller = new FloorController(new FloorBehaviorState(), config, new System.Random(1));

            Assert.Throws<ArgumentException>(() => controller.Initialize(SingleRoom(1), Players()));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveRequiredCountIsRejected(int count)
        {
            var controller = new FloorController(new FloorBehaviorState(), Config(count), new System.Random(1));

            Assert.Throws<ArgumentException>(() => controller.Initialize(SingleRoom(1), Players()));
        }

        [Test]
        public void RequiredAnchorsExcludeDisconnectedAndHunterOnlyRoutesToExit()
        {
            var graph = Graph(new[] { 1, 2, 3 }, new[] { new LevelEdge(1, 2, 3, false, TraversalAccess.Hunter) },
                new[] { Anchor(101, 1), Anchor(102, 2), Anchor(103, 3) }, 3);
            var fixture = Start(graph, Config(1));

            CollectionAssert.AreEqual(new[] { 103 }, fixture.State.ActiveCakeAnchors.Select(anchor => anchor.Id));
            CollectAll(fixture);
            AssertPhases(fixture.Controller.Tick(0f, 5), new[] { 1 }, new[] { RoomPhase.Telegraph });
        }

        [Test]
        public void DirectedRouteToExitDoesNotMakeCakeReachableFromPlayerInExitRoom()
        {
            var graph = Graph(new[] { 1, 2 }, new[] { new LevelEdge(1, 2, 1, false, TraversalAccess.Player) },
                new[] { Anchor(101, 2) }, 1);
            var state = new FloorBehaviorState();
            var controller = new FloorController(state, Config(1), new System.Random(1));

            Assert.Throws<InvalidOperationException>(() => controller.Initialize(graph,
                new IReadOnlyPlayerState[] { new PlayerFixture(1, position: graph.ExitPosition) }));

            Assert.That(state.IsReady, Is.False);
            Assert.That(state.ActiveCakeAnchors, Is.Empty);
        }

        [Test]
        public void PlayerBeforeOneWayExitRouteCanCollectReachableCake()
        {
            var graph = Graph(new[] { 1, 2 }, new[] { new LevelEdge(1, 2, 1, false, TraversalAccess.Player) },
                new[] { Anchor(101, 2) }, 1);
            var fixture = Start(graph, Config(1), players: new IReadOnlyPlayerState[]
                { new PlayerFixture(1, position: new Vector3(20f, 0f, 0f)) });

            CollectionAssert.AreEqual(new[] { 101 }, fixture.State.ActiveCakeAnchors.Select(anchor => anchor.Id));
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(fixture.State.ExitState, Is.EqualTo(ExitState.Open));
        }

        [Test]
        public void InitializationRejectsPlayersOutsideEveryAuthoredRoom()
        {
            var state = new FloorBehaviorState();
            var controller = new FloorController(state, Config(1), new System.Random(1));

            Assert.Throws<InvalidOperationException>(() => controller.Initialize(SingleRoom(1),
                new IReadOnlyPlayerState[] { new PlayerFixture(1, position: new Vector3(-100f, 0f, 0f)) }));

            Assert.That(state.IsReady, Is.False);
        }

        [Test]
        public void CollapsePrioritizesDisconnectedThenFarthestRoomsAndBreaksDistanceTiesById()
        {
            var graph = Graph(new[] { 30, 5, 40, 10, 20 }, new[]
            {
                new LevelEdge(1, 20, 10, false), new LevelEdge(2, 30, 10, false),
                new LevelEdge(3, 40, 20, false)
            }, new[] { Anchor(101, 10) }, 10);
            var fixture = Start(graph, Config(1));
            CollectAll(fixture);

            var facts = fixture.Controller.Tick(70f, 90);

            CollectionAssert.AreEqual(new[] { 5, 40, 20, 30, 10 }, facts.Where(fact => fact.Phase == RoomPhase.Telegraph).Select(fact => fact.RoomId));
            CollectionAssert.AreEqual(new[] { 5, 40, 20, 30, 10 }, facts.Where(fact => fact.Phase == RoomPhase.Closed).Select(fact => fact.RoomId));
            Assert.That(fixture.State.RoomPhases.Values.All(phase => phase == RoomPhase.Closed), Is.True);
        }

        [Test]
        public void LockedExitDoesNotAdvanceCollapseAndFirstTelegraphStartsWhenCollectionFinishes()
        {
            var fixture = Start(SingleRoom(1), Config(1));

            Assert.That(fixture.Controller.Tick(100f, 1), Is.Empty);
            Assert.That(fixture.State.RoomPhases[1], Is.EqualTo(RoomPhase.Open));
            CollectAll(fixture);
            AssertPhases(fixture.Controller.Tick(0f, 2), new[] { 1 }, new[] { RoomPhase.Telegraph });
        }

        [Test]
        public void CollapseHasCrackingTearingEncroachingAndConsumedStagesAtExactBoundaries()
        {
            var fixture = Start(SingleRoom(1), Config(1)); CollectAll(fixture);
            AssertPhases(fixture.Controller.Tick(0f, 1), new[] { 1 }, new[] { RoomPhase.Telegraph });
            Assert.That(fixture.Controller.Tick(5.5f, 2), Is.Empty);
            AssertPhases(fixture.Controller.Tick(0.5f, 3), new[] { 1 }, new[] { RoomPhase.Tearing });
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 3, out _), Is.True);
            AssertPhases(fixture.Controller.Tick(2f, 4), new[] { 1 }, new[] { RoomPhase.Encroaching });
            AssertPhases(fixture.Controller.Tick(6f, 5), new[] { 1 }, new[] { RoomPhase.Closed });
            Assert.That(fixture.Controller.Tick(0f, 6), Is.Empty);
        }
        [Test]
        public void RoomSpacingNeverStartsNeighborBeforePreviousConsumption()
        {
            var config = Config(1); Set(config, "_collapseInterval", 6f);
            var graph = Graph(new[] { 3, 2, 1 }, new[] { new LevelEdge(1, 1, 3, true), new LevelEdge(2, 2, 3, true) },
                new[] { Anchor(101, 3) }, 3);
            var fixture = Start(graph, config); CollectAll(fixture);
            var facts = fixture.Controller.Tick(13.9f, 1);
            Assert.That(facts.All(fact => fact.RoomId == 1), Is.True);
            var rest = fixture.Controller.Tick(28.1f, 2);
            Assert.That(rest.Count(fact => fact.Phase == RoomPhase.Closed), Is.EqualTo(3));
            Assert.That(fixture.State.RoomPhases.Values.All(phase => phase == RoomPhase.Closed), Is.True);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaTimeIsRejected(float dt)
        {
            var fixture = Start(SingleRoom(1), Config(1));

            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Controller.Tick(dt, 1));
        }

        [Test]
        public void GoldenCakeWalletIsSeparateAndEachOriginalAnchorCanBeCollectedAgainOnce()
        {
            var fixture = Start(SingleRoom(2), Config(2));
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 1, out _), Is.False);
            CollectAll(fixture);

            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 3, out var fact), Is.True);
            Assert.That(fact.CakeCount, Is.EqualTo(2));
            Assert.That(fact.GoldenCount, Is.EqualTo(1));
            Assert.That(fact.Kind, Is.EqualTo(PickupKind.GoldenCake));
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 4, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 4, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(1), 102, PickupKind.GoldenCake, 5, out _), Is.True);
            Assert.That(fixture.State.CakeCount, Is.EqualTo(2));
            Assert.That(fixture.State.GoldenCakeCount, Is.EqualTo(2));
            Assert.That(fixture.State.ActiveCakeAnchors, Is.Empty);
            Assert.That(fixture.State.ExitState, Is.EqualTo(ExitState.Open));
        }

        [Test]
        public void GoldenCakeCannotBeCollectedInsideAClosedRoom()
        {
            var fixture = Start(SingleRoom(1), Config(1));
            CollectAll(fixture);
            fixture.Controller.Tick(14f, 2);

            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 3, out _), Is.False);
            Assert.That(fixture.State.GoldenCakeCount, Is.Zero);
        }

        [Test]
        public void ExitAcceptsImmediateOpenContactOnlyOnceAndStopsFurtherGameplay()
        {
            var fixture = Start(SingleRoom(1), Config(1));
            Assert.That(fixture.Controller.ContactExit(new EntityId(1), 1, out _), Is.False);
            CollectAll(fixture);
            Assert.That(fixture.Controller.ContactExit(new EntityId(99), 2, out _), Is.False);

            Assert.That(fixture.Controller.ContactExit(new EntityId(1), 3, out var fact), Is.True);

            Assert.That(fact.PlayerId, Is.EqualTo(new EntityId(1)));
            Assert.That(fact.Tick, Is.EqualTo(3));
            Assert.That(fixture.Controller.ContactExit(new EntityId(1), 4, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 4, out _), Is.False);
            Assert.That(fixture.Controller.Tick(100f, 4), Is.Empty);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.False);
        }

        [Test]
        public void ConsumedRoomDoesNotDirectlyKillALivingPlayer()
        {
            var fixture = Start(SingleRoom(1), Config(1)); CollectAll(fixture); fixture.Controller.Tick(14f, 2);
            Assert.That(fixture.Controller.ContactLethalRoom(new EntityId(1), 1, 2, out _), Is.False);
            Assert.That(fixture.State.IsReady, Is.True);
        }
        [Test]
        public void ConfirmedDeadRegisteredPlayerEndsOnceOnlyInAnActiveHazardRoom()
        {
            var player = new PlayerFixture(1);
            var fixture = Start(SingleRoom(1), Config(1), players:new[]{player}); CollectAll(fixture);
            player.IsAlive = false;
            Assert.That(fixture.Controller.ContactLethalRoom(player.Id, 1, 1, out _), Is.False);
            fixture.Controller.Tick(6f, 2);
            Assert.That(fixture.Controller.ContactLethalRoom(player.Id, 1, 2, out var fact), Is.True);
            Assert.That(fact.PlayerId, Is.EqualTo(player.Id));
            Assert.That(fixture.Controller.ContactLethalRoom(player.Id, 1, 2, out _), Is.False);
        }
        [Test]
        public void OptionalCracksDoNotChangeGameplayScheduleAndDeduplicate()
        {
            var fixture = Start(SingleRoom(1), Config(1));
            Assert.That(fixture.Controller.TelegraphOptionalRoom(1), Is.True);
            Assert.That(fixture.Controller.TelegraphOptionalRoom(1), Is.False);
            Assert.That(fixture.Controller.TelegraphOptionalRoom(99), Is.False);
            Assert.That(fixture.State.RoomPhases[1], Is.EqualTo(RoomPhase.Open));
            CollectAll(fixture);
            AssertPhases(fixture.Controller.Tick(0f, 1), new[]{1},new[]{RoomPhase.Telegraph});
        }

        [Test]
        public void CueUsesActualSuppliedPathLengthInsteadOfEuclideanAnchorDistance()
        {
            var graph = Graph(new[] { 1 }, new LevelEdge[0], new[]
            {
                new LevelAnchor(101, 1, CakeAnchorType.Flow, new Vector3(1f, 0f, 0f)),
                new LevelAnchor(102, 1, CakeAnchorType.Flow, new Vector3(100f, 0f, 0f))
            }, 1);
            var fixture = Start(graph, Config(2));

            var cue = fixture.Controller.SelectCue(new[]
            { new FloorPathCandidate(101, 50f, Vector3.left), new FloorPathCandidate(102, 10f, Vector3.right) });

            Assert.That(cue.HasCue, Is.True);
            Assert.That(cue.CueDirection, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void CueBreaksEqualPathLengthsByAnchorIdAndDropsCollectedCandidates()
        {
            var fixture = Start(SingleRoom(2), Config(2));
            var paths = new[] { new FloorPathCandidate(102, 5f, Vector3.right), new FloorPathCandidate(101, 5f, Vector3.left) };

            Assert.That(fixture.Controller.SelectCue(paths).CueDirection, Is.EqualTo(Vector3.left));
            fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 1, out _);
            Assert.That(fixture.Controller.SelectCue(paths).CueDirection, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void CueHasNoEuclideanFallbackForMissingInvalidOrUnknownPaths()
        {
            var fixture = Start(SingleRoom(1), Config(1));

            Assert.That(fixture.Controller.SelectCue(null).HasCue, Is.False);
            Assert.That(fixture.Controller.SelectCue(new FloorPathCandidate[0]).HasCue, Is.False);
            Assert.That(fixture.Controller.SelectCue(new[]
            {
                new FloorPathCandidate(101, float.PositiveInfinity, Vector3.right),
                new FloorPathCandidate(101, float.NaN, Vector3.right),
                new FloorPathCandidate(101, -1f, Vector3.right),
                new FloorPathCandidate(101, 1f, new Vector3(float.NaN, 0f, 0f)),
                new FloorPathCandidate(999, 1f, Vector3.right)
            }).HasCue, Is.False);
        }

        [Test]
        public void ExitCueReplacesCakesAndNeverPointsAtGoldenCakes()
        {
            var fixture = Start(SingleRoom(1), Config(1));
            Assert.That(fixture.Controller.SelectCue(new[] { new FloorPathCandidate(0, 1f, Vector3.back) }).HasCue, Is.False);
            CollectAll(fixture);
            Assert.That(fixture.Controller.SelectCue(new[] { new FloorPathCandidate(101, 1f, Vector3.right) }).HasCue, Is.False);

            var cue = fixture.Controller.SelectCue(new[]
            { new FloorPathCandidate(101, 1f, Vector3.right), new FloorPathCandidate(0, 30f, Vector3.back) });

            Assert.That(cue.HasCue, Is.True);
            Assert.That(cue.CueDirection, Is.EqualTo(Vector3.back));
            Assert.That(cue.Exit, Is.EqualTo(ExitState.Open));
        }

        [Test]
        public void CueCadenceStartsImmediatelyThenRepeatsAtHalfSecondAndRefreshesOnOpening()
        {
            var fixture = Start(SingleRoom(1), Config(1));
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.True);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.False);
            fixture.Controller.Tick(0.25f, 1);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.False);
            fixture.Controller.Tick(0.25f, 2);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.True);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.False);
            CollectAll(fixture);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.True);
            Assert.That(fixture.Controller.ConsumeCueDue(), Is.False);
        }

        [Test]
        public void CuePlayerUsesLowestLivingIdentityRegardlessOfRegistrationOrder()
        {
            var fixture = Start(SingleRoom(1), Config(1), players: new IReadOnlyPlayerState[]
            { new PlayerFixture(9), null, new PlayerFixture(1, false), new PlayerFixture(3) });

            Assert.That(fixture.Controller.CuePlayer().Id, Is.EqualTo(new EntityId(3)));
        }

        [Test]
        public void ReinitializeClearsCountersTerminalStateCueAndContactDeduplication()
        {
            var fixture = Start(SingleRoom(1), Config(1));
            CollectAll(fixture);
            fixture.Controller.Collect(new EntityId(1), 101, PickupKind.GoldenCake, 2, out _);
            fixture.Controller.SelectCue(new[] { new FloorPathCandidate(0, 2f, Vector3.right) });
            Assert.That(fixture.Controller.ContactExit(new EntityId(1), 3, out _), Is.True);

            fixture.Controller.Initialize(SingleRoom(1), new IReadOnlyPlayerState[] { new PlayerFixture(2) });

            Assert.That(fixture.State.IsReady, Is.True);
            Assert.That(fixture.State.CakeCount, Is.Zero);
            Assert.That(fixture.State.GoldenCakeCount, Is.Zero);
            Assert.That(fixture.State.RequiredCakeCount, Is.EqualTo(1));
            Assert.That(fixture.State.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(fixture.State.RoomPhases[1], Is.EqualTo(RoomPhase.Open));
            Assert.That(fixture.Controller.Snapshot().HasCue, Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(1), 101, PickupKind.Cake, 4, out _), Is.False);
            Assert.That(fixture.Controller.Collect(new EntityId(2), 101, PickupKind.Cake, 4, out _), Is.True);
            Assert.That(fixture.Controller.Collect(new EntityId(2), 101, PickupKind.GoldenCake, 5, out _), Is.True);
            Assert.That(fixture.Controller.ContactExit(new EntityId(2), 6, out _), Is.True);
        }

        private static Fixture Start(LevelGraph graph, FloorConfig config, int seed = 1337, IReadOnlyList<IReadOnlyPlayerState> players = null)
        {
            var state = new FloorBehaviorState();
            var controller = new FloorController(state, config, new System.Random(seed));
            controller.Initialize(graph, players ?? new IReadOnlyPlayerState[] { new PlayerFixture(1, position: graph.ExitPosition) });
            return new Fixture(state, controller);
        }

        private static void CollectAll(Fixture fixture)
        {
            foreach (var anchor in fixture.State.ActiveCakeAnchors.ToArray())
                Assert.That(fixture.Controller.Collect(new EntityId(1), anchor.Id, PickupKind.Cake, 1, out _), Is.True);
        }

        private static FloorConfig Config(int required)
        {
            var config = (FloorConfig)FormatterServices.GetUninitializedObject(typeof(FloorConfig));
            Set(config, "_requiredCakeCount", required);
            Set(config, "_flowWeight", 5f);
            Set(config, "_precisionWeight", 3f);
            Set(config, "_detourWeight", 2f);
            Set(config, "_riskWeight", 1f);
            Set(config, "_verticalWeight", 2f);
            Set(config, "_collapseInterval", 12f);
            Set(config, "_telegraphDuration", 6f);
            Set(config, "_directionCueInterval", 0.5f);
            return config;
        }

        private static void Set(FloorConfig config, string field, object value)
        {
            typeof(FloorConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        }

        private static IReadOnlyPlayerState[] Players() => new IReadOnlyPlayerState[] { new PlayerFixture(1) };

        private static LevelGraph SingleRoom(int anchors)
        {
            return Graph(new[] { 1 }, new LevelEdge[0], Enumerable.Range(101, anchors).Select(id => Anchor(id, 1)).ToArray(), 1);
        }

        private static LevelAnchor Anchor(int id, int room, CakeAnchorType type = CakeAnchorType.Flow)
            => new LevelAnchor(id, room, type, new Vector3(id, 0f, room));

        private static LevelGraph Graph(int[] rooms, LevelEdge[] edges, LevelAnchor[] anchors, int exit)
        {
            return LevelGraphUtility.Build(rooms.Select(id => new LevelRoom(id, new Vector3(id * 10f, 2f, 0f), new Vector3(8f, 4f, 8f))).ToArray(),
                edges, anchors, exit, new Vector3(exit * 10f, 0f, 0f));
        }

        private static void AssertPhases(IReadOnlyList<RoomPhaseChangedFact> facts, int[] rooms, RoomPhase[] phases)
        {
            CollectionAssert.AreEqual(rooms, facts.Select(fact => fact.RoomId));
            CollectionAssert.AreEqual(phases, facts.Select(fact => fact.Phase));
        }

        private sealed class Fixture
        {
            public Fixture(FloorBehaviorState state, FloorController controller) { State = state; Controller = controller; }
            public FloorBehaviorState State { get; }
            public FloorController Controller { get; }
        }

        private sealed class PlayerFixture : IReadOnlyPlayerState
        {
            public PlayerFixture(int id, bool alive = true, Vector3? position = null)
            {
                Id = new EntityId(id);
                IsAlive = alive;
                Position = position ?? new Vector3(10f, 0f, 0f);
            }
            public EntityId Id { get; }
            public Vector3 Position { get; }
            public Vector3 Velocity => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public float HeadingDegrees => 0f;
            public float SprintSpeed => 8f;
            public float MaxDesignSpeed => 12f;
            public float Health => IsAlive ? 100f : 0f;
            public float MaxHealth => 100f;
            public bool IsAlive { get; set; }
            public bool LookBack => false;
            public MovementState MovementState => Worsen.Core.MovementState.Ground;
            public long Tick => 0;
            public IReadOnlyList<NoiseEvent> RecentNoises => Array.Empty<NoiseEvent>();
            public InventorySnapshot Inventory => default;
        }
    }
}
