// ============================================================================
// FloorCakeLineTests.cs
// ============================================================================
// PURPOSE:
//   Locks in the owner decision that every cake gates escape and collapse.
//   Long collection phases cannot destroy objectives or rewrite remaining totals.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Verify full placement, all-remaining exit gating and delayed collapse.
//   - Verify white guidance remains on cakes until the exit opens.
//   - Preserve trap exclusion, counters and Blind Faith suppression.
// DEPENDENCIES:
//   - NUnit, Core, Floor and the existing pure cake fixture.
// USAGE NOTES:
//   All time and randomness are injected. No native objects or Unity callbacks.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorCakeLineTests
    {
        [Test]
        public void DensityCannotThinLinesAndFinalPhysicalCakeOpensExit()
        {
            var config = FloorCakeRulesTests.Config();
            FloorCakeRulesTests.Set(config, "_useRoomCakeDensity", true);
            FloorCakeRulesTests.Set(config, "_minimumCakesPerRoom", 1);
            FloorCakeRulesTests.Set(config, "_maximumCakesPerRoom", 1);
            FloorCakeRulesTests.Set(config, "_minimumExitRoomCakes", 1);
            FloorCakeRulesTests.Set(config, "_requiredCakeFraction", .6f);
            var f = FloorCakeRulesTests.Start(round: 1, config: config);
            Assert.That(f.State.ActiveCakeAnchors.Count, Is.EqualTo(18));
            Assert.That(f.Controller.Snapshot().TotalCakes, Is.EqualTo(18));
            var anchors = f.State.ActiveCakeAnchors.ToArray();
            foreach (var a in anchors.Take(17))
            {
                Assert.That(f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _), Is.True);
                Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            }
            Assert.That(f.State.CollapseStarted, Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), anchors.Last().Id, PickupKind.Cake, 2, out _), Is.True);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(f.State.CakeCount, Is.EqualTo(18));
            Assert.That(f.State.CollapseStarted, Is.True);
        }

        [Test]
        public void PartialCollectionCannotLoseCakesAndKeepsTotalsFixed()
        {
            var f = FloorCakeRulesTests.Start(round: 1);
            var initial = f.Controller.Snapshot();
            foreach (var a in f.State.ActiveCakeAnchors.Take(6).ToArray()) f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _);
            Assert.That(f.State.CollapseStarted, Is.False); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            f.Controller.Tick(10000f, 2);
            var losses = f.Controller.DrainCakeLosses();
            Assert.That(losses, Is.Empty);
            Assert.That(f.State.ActiveCakeAnchors.Count, Is.EqualTo(12));
            Assert.That(f.State.RoomPhases.Values.All(p => p == RoomPhase.Open), Is.True);
            Assert.That(f.State.RequiredCakeCount, Is.EqualTo(f.State.CakeCount + f.State.ActiveCakeAnchors.Count));
            FloorCakeRulesTests.CollectRequired(f);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(f.Controller.Snapshot().TotalCakes, Is.EqualTo(initial.TotalCakes));
            Assert.That(f.Controller.Snapshot().TotalGoldenCakes, Is.EqualTo(initial.TotalGoldenCakes));
            f.Controller.Tick(1f, 3); Assert.That(f.Controller.DrainCakeLosses(), Is.Empty);
        }

        [Test]
        public void WaitingCannotReplaceLastPickupOrPublishExitGuidance()
        {
            var f = FloorCakeRulesTests.Start(round: 1);
            int total = f.Controller.Snapshot().TotalCakes;
            var trigger = f.State.ActiveCakeAnchors.Take(6).ToArray();
            foreach (var a in trigger.Concat(f.State.ActiveCakeAnchors.Where(a => a.RoomId == f.Graph.ExitRoomId)).Distinct().ToArray())
                Assert.That(f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(f.State.CollapseStarted, Is.False); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(f.State.ActiveCakeAnchors, Is.Not.Empty);
            f.Controller.Tick(10000f, 2);
            Assert.That(f.State.ActiveCakeAnchors, Is.Not.Empty);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(f.State.RequiredCakeCount, Is.GreaterThan(f.State.CakeCount));
            Assert.That(f.Controller.Snapshot().TotalCakes, Is.EqualTo(total));
            Assert.That(f.Controller.SelectCue(new[] { new FloorPathCandidate(0, 2f, Vector3.left) }).HasCue, Is.False);
            FloorCakeRulesTests.CollectRequired(f);
            f.Controller.SelectCue(new[] { new FloorPathCandidate(0, 2f, Vector3.left) });
            Assert.That(f.Controller.TryWhiteGuidance(false, out var target), Is.True);
            Assert.That(target.AnchorId, Is.Zero);
        }

        [Test]
        public void WhiteArrowChoosesNearestRemainingCakeThenExit()
        {
            var f = FloorCakeRulesTests.Start(round: 1);
            foreach (var a in f.State.ActiveCakeAnchors.Take(6).ToArray()) f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _);
            var remaining = f.State.ActiveCakeAnchors.Take(2).ToArray();
            f.Controller.SelectCue(new[] { new FloorPathCandidate(0, .1f, Vector3.back),
                new FloorPathCandidate(remaining[0].Id, 9f, Vector3.left), new FloorPathCandidate(remaining[1].Id, 2f, Vector3.right) });
            Assert.That(f.Controller.TryWhiteGuidance(false, out var target), Is.True);
            Assert.That(target.AnchorId, Is.EqualTo(remaining[1].Id));
            FloorCakeRulesTests.CollectRequired(f);
            f.Controller.SelectCue(new[] { new FloorPathCandidate(remaining[1].Id, .1f, Vector3.right), new FloorPathCandidate(0, 3f, Vector3.back) });
            Assert.That(f.Controller.TryWhiteGuidance(false, out target), Is.True); Assert.That(target.AnchorId, Is.Zero);
        }

        [Test]
        public void BlindFaithCannotSkipRemainingPhysicalCakesOrStartCollapseEarly()
        {
            var f = FloorCakeRulesTests.Start(round: 1, hooks: new FloorCakeHooks(blindFaith: true));
            foreach (var a in f.State.ActiveCakeAnchors.Take(3).ToArray()) f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _);
            Assert.That(f.State.CollapseStarted, Is.False); Assert.That(f.State.CakeCount, Is.EqualTo(3));
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(f.Controller.TryWhiteGuidance(false, out _), Is.False);
            FloorCakeRulesTests.CollectRequired(f); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
        }
    }
}
