// ============================================================================
// FloorCakeLoopTests.cs
// ============================================================================
// PURPOSE:
//   Regresses the collect-all exit race, Passage-only gold and stable path guidance.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Measure exact room durations, first-floor budget and growing concurrency.
//   - Require every registered reward before collapse and accurate live counts.
//   - Walk a doorway with retained identity and no stale backwards bearing.
//   - Check path-ranked golden targets, hysteresis and invalidation.
// DEPENDENCIES:
//   NUnit, Core, Domain Floor/Player value state and managed fixture helpers.
// USAGE NOTES:
//   Pure managed fixtures; clocks, paths and player positions are injected.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorCakeLoopTests
    {
        [Test]
        public void FirstSevenRoomFloorClosesInSixtySecondsAndEveryRoomTakesTen()
        {
            var sample = Schedule(7);
            Assert.That(sample.total, Is.EqualTo(60d).Within(.011d));
            Assert.That(sample.peak, Is.EqualTo(1));
        }

        [Test]
        public void LargerFloorsGrowLogarithmicallyWhileConcurrentRoomsIncrease()
        {
            var first = Schedule(7); var medium = Schedule(28); var large = Schedule(112);
            Assert.That(medium.total, Is.EqualTo(60d * (1d + .5d * Math.Log(4d))).Within(.011d));
            Assert.That(large.total, Is.EqualTo(60d * (1d + .5d * Math.Log(16d))).Within(.011d));
            Assert.That(medium.total, Is.LessThan(first.total * 4d));
            Assert.That(large.total - medium.total, Is.EqualTo(medium.total - first.total).Within(.02d));
            Assert.That(medium.peak, Is.GreaterThan(first.peak));
            Assert.That(large.peak, Is.GreaterThan(medium.peak));
        }

        private static (double total, int peak) Schedule(int rooms)
        {
            var graph = LevelGraphUtility.Build(Enumerable.Range(1, rooms).Select(i =>
                new LevelRoom(i, new Vector3(i * 10f, 2f, 0f), new Vector3(8f, 4f, 8f))).ToArray(),
                Enumerable.Range(1, rooms - 1).Select(i => new LevelEdge(i, i, i + 1, true)).ToArray(),
                new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, new Vector3(10f, 0f, 0f)) }, rooms, new Vector3(rooms * 10f, 0f, 0f));
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = graph.ExitPosition };
            var state = new FloorBehaviorState(); var controller = new FloorController(state, FloorCakeRulesTests.Config(), new System.Random(7));
            controller.Initialize(graph, new[] { player }, requiredCakeCount: 1);
            Assert.That(controller.Tick(10000f, 1), Is.Empty);
            Assert.That(state.CollapseStarted, Is.False);
            Assert.That(controller.ContactExit(player.Id, 1, out _), Is.False);
            Assert.That(controller.Collect(player.Id, 1, PickupKind.Cake, 2, out _), Is.True);
            Assert.That(state.ExitState, Is.EqualTo(ExitState.Open)); Assert.That(state.CollapseStarted, Is.True);
            var start = new Dictionary<int, double>(); var stages = new Dictionary<int, List<RoomPhase>>();
            int peak = 0; double elapsed = 0d;
            for (int tick = 0; tick < 30000; tick++)
            {
                float dt = tick == 0 ? 0f : .01f; elapsed += dt;
                foreach (var fact in controller.Tick(dt, tick + 3))
                {
                    Assert.That(fact.RoomId, Is.Not.EqualTo(rooms));
                    if (fact.Phase == RoomPhase.Telegraph) { start.Add(fact.RoomId, elapsed); stages.Add(fact.RoomId, new List<RoomPhase>()); }
                    stages[fact.RoomId].Add(fact.Phase);
                    if (fact.Phase == RoomPhase.Closed)
                        Assert.That(elapsed - start[fact.RoomId], Is.EqualTo(10d).Within(.011d));
                }
                peak = Math.Max(peak, state.RoomPhases.Values.Count(p => p != RoomPhase.Open && p != RoomPhase.Closed));
                if (state.RoomPhases.Count(p => p.Value == RoomPhase.Closed) == rooms - 1) break;
            }
            Assert.That(start.Keys, Is.EqualTo(Enumerable.Range(1, rooms - 1)), "Farthest from exit first; ids break ties.");
            foreach (var sequence in stages.Values) Assert.That(sequence, Is.EqualTo(new[] {
                RoomPhase.Telegraph, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed }));
            Assert.That(state.RoomPhases[rooms], Is.EqualTo(RoomPhase.Open));
            Assert.That(controller.DrainCakeLosses(), Is.Empty);
            return (elapsed, peak);
        }

        [Test]
        public void PassageGoldAloneExtendsLiveCountAndLastGoldStartsRace()
        {
            var f = FloorCakeRulesTests.Passage(); var id = new EntityId(1);
            var gold = new LevelAnchor(999, 7, CakeAnchorType.Risk, new Vector3(70f, 0f, 0f));
            Assert.That(f.Controller.ActivatePocket(7), Is.True);
            Assert.That(f.Controller.RegisterPassageReward(gold), Is.True);
            Assert.That(f.Controller.RegisterPassageReward(gold), Is.False);
            foreach (var anchor in f.State.ActiveCakeAnchors.Where(a => a.Id != gold.Id).ToArray())
            {
                Assert.That(f.Controller.Collect(id, anchor.Id, PickupKind.Cake, 1, out _), Is.True);
                var display = f.Controller.Snapshot();
                Assert.That(display.TotalCakes - display.Collected + display.TotalGoldenCakes - display.Golden,
                    Is.EqualTo(f.State.ActiveCakeAnchors.Count));
            }
            Assert.That(f.State.CollapseStarted, Is.False);
            Assert.That(f.Controller.Tick(10000f, 2), Is.Empty);
            Assert.That(f.Controller.Collect(id, gold.Id, PickupKind.Cake, 3, out _), Is.False);
            Assert.That(f.Controller.Collect(id, gold.Id, PickupKind.GoldenCake, 3, out var fact), Is.True);
            Assert.That(fact.GoldenCount, Is.EqualTo(1)); Assert.That(fact.CakeCount, Is.EqualTo(18));
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open)); Assert.That(f.State.CollapseStarted, Is.True);
            Assert.That(f.Controller.RegisterPassageReward(new LevelAnchor(998, 7, CakeAnchorType.Risk, gold.Position)), Is.False);
            Assert.That(f.Controller.Snapshot().TotalGoldenCakes - f.Controller.Snapshot().Golden, Is.Zero);
        }

        [Test]
        public void ReachableCakeIdentitySurvivesSmallDistanceChangesButNotLargeMarginOrCollection()
        {
            var f = FloorCakeRulesTests.Start(round: 1); var ids = f.State.ActiveCakeAnchors.Take(2).Select(a => a.Id).ToArray();
            void Select(float a, float b) => f.Controller.SelectCue(new[] {
                new FloorPathCandidate(ids[0], a, Vector3.right), new FloorPathCandidate(ids[1], b, Vector3.left) });
            Select(10f, 11f); AssertTarget(f.Controller, ids[0]);
            Select(11f, 10f); AssertTarget(f.Controller, ids[0]);
            Select(11f, 7f); AssertTarget(f.Controller, ids[1]);
            Select(5f, float.PositiveInfinity); AssertTarget(f.Controller, ids[0]);
            f.Controller.Collect(new EntityId(1), ids[0], PickupKind.Cake, 1, out _);
            Select(1f, 10f); AssertTarget(f.Controller, ids[1]);
        }

        [Test]
        public void GoldenSenseRanksReachablePathsAndRetainsIdentityUntilWorseOrCollected()
        {
            var f = FloorCakeRulesTests.Passage();
            foreach (int id in new[] { 998, 999 }) Assert.That(f.Controller.RegisterPassageReward(
                new LevelAnchor(id, 7, CakeAnchorType.Risk, new Vector3(70f, 0f, id - 998))), Is.True);
            int Select(float a, float b)
            {
                Assert.That(f.Controller.TryGoldenTarget(new[] { new FloorPathCandidate(998, a, Vector3.left),
                    new FloorPathCandidate(999, b, Vector3.right) }, out var target), Is.True);
                return target.Id;
            }
            Assert.That(Select(30f, 10f), Is.EqualTo(999));
            Assert.That(Select(10f, 11f), Is.EqualTo(999));
            Assert.That(Select(4f, 11f), Is.EqualTo(998));
            Assert.That(Select(float.PositiveInfinity, 11f), Is.EqualTo(999));
            f.Controller.Collect(new EntityId(1), 999, PickupKind.GoldenCake, 1, out _);
            Assert.That(Select(5f, 1f), Is.EqualTo(998));
            Assert.That(f.Controller.TryGoldenTarget(Array.Empty<FloorPathCandidate>(), out _), Is.False);
            Assert.That(f.Controller.TryGoldenTarget(Vector3.zero, out _), Is.False);
        }

        [Test]
        public void DoorwayCrossingRefreshesRouteWithoutFlippingBehindOrChangingCake()
        {
            var graph = LevelGraphUtility.Build(new[] {
                new LevelRoom(1, new Vector3(-2f, 2f, 0f), new Vector3(4f, 4f, 4f)),
                new LevelRoom(2, new Vector3(2f, 2f, 0f), new Vector3(4f, 4f, 4f)) },
                new[] { new LevelEdge(1, 1, 2, true) }, new[] {
                new LevelAnchor(101, 2, CakeAnchorType.Flow, new Vector3(3f, 0f, 0f)),
                new LevelAnchor(102, 2, CakeAnchorType.Flow, new Vector3(3f, 0f, 1f)) }, 2, new Vector3(3f, 0f, 0f));
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Vector3.left * 2f };
            var state = new FloorBehaviorState(); var controller = new FloorController(state, FloorCakeRulesTests.Config(), new System.Random(7));
            controller.Initialize(graph, new[] { player }, requiredCakeCount: 1);
            var presenter = new FloorPresenter(); var history = new FloorDriverState(); int crossings = 0;
            var corners = new[] { Vector3.left * 2f, Vector3.zero, Vector3.right * 3f };
            foreach (float x in new[] { -2f, -1f, -.1f, .1f, .5f, 1.5f, 2.5f })
            {
                player.Position = Vector3.right * x;
                if (controller.RefreshCueForRoomChange()) { crossings++; Assert.That(controller.ConsumeCueDue(), Is.True); }
                var path = presenter.PathCandidate(history, 101, player.Position, player.Position, corners[2], corners, 1f);
                Assert.That(Vector3.Dot(path.Direction, Vector3.right), Is.GreaterThan(.999f));
                controller.SelectCue(new[] { path, new FloorPathCandidate(102, path.Length - .1f, Vector3.forward) });
                // Establish 101 before the noisy competing path is admitted.
                if (x == -2f) controller.SelectCue(new[] { path });
                AssertTarget(controller, 101);
            }
            Assert.That(crossings, Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(101)] [TestCase(998)] [TestCase(2001)]
        public void SharedPathMathSkipsPassedDoorwayForExitCakeGoldAndKeyIdentity(int identity)
        {
            var presenter = new FloorPresenter(); var state = new FloorDriverState();
            var corners = new[] { Vector3.left * 3f, Vector3.zero, Vector3.forward * 4f };
            foreach (float z in new[] { .1f, .5f, 1.5f, 3.5f })
            {
                var origin = Vector3.forward * z;
                var path = presenter.PathCandidate(state, identity, origin, origin, corners[2], corners, 1f);
                Assert.That(path.AnchorId, Is.EqualTo(identity));
                Assert.That(Vector3.Dot(path.Direction, Vector3.forward), Is.GreaterThan(.999f));
            }
        }

        private static void AssertTarget(FloorController controller, int id)
        { Assert.That(controller.TryWhiteGuidance(false, out var target), Is.True); Assert.That(target.AnchorId, Is.EqualTo(id)); }
    }
}
