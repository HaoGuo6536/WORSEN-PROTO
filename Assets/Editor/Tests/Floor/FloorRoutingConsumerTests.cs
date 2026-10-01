// ============================================================================
// FloorRoutingConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies wave-two Floor consumers with explicit graphs and injected effects.
//   Physical Passage registration, rather than future reservations, owns gold totals.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Count Passage gold only on registration and never double-count its pickup.
//   - Publish typed Exit Sense targets only while the exit is open.
//   - Deduplicate Blinder placement and consume its duration/silence policy.
// DEPENDENCIES:
//   - Floor controller/state and Core immutable facts; NUnit and pure fixture configs.
// USAGE NOTES:
//   No native objects, scene imports or wall time. Config comes from the pure cake fixture.
// ============================================================================
using System;
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
    public sealed class FloorRoutingConsumerTests
    {
        [Test]
        public void ExitSensePublishesTypedExitOnlyWhileOpenAndEffectIsPresent()
        {
            var f = FloorCakeRulesTests.Start();
            f.Controller.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("exit-sense"), EffectKind.Upgrade, 1) }));
            Assert.That(f.Controller.GuidanceTargets(true).Any(t => t.Kind == GuidanceKind.ExitThroughWalls), Is.False);
            FloorCakeRulesTests.CollectRequired(f);
            var exit = f.Controller.GuidanceTargets(true).Single(t => t.Kind == GuidanceKind.ExitThroughWalls);
            Assert.That(exit.TargetPosition, Is.EqualTo(f.Graph.ExitPosition));
            f.Controller.SetActiveEffects(default(ActiveEffects));
            Assert.That(f.Controller.GuidanceTargets(true).Any(t => t.Kind == GuidanceKind.ExitThroughWalls), Is.False);
        }

        [Test]
        public void BlinderPoliciesDeduplicateTypePlacementAndDriveTrapBlindnessAndSilence()
        {
            var f = FloorCakeRulesTests.Start(); int original = f.State.Traps.Count;
            var first = new BlinderTrapPolicyFact(new EntityId(-1), 1, 2, true, 6f, true);
            var second = new BlinderTrapPolicyFact(new EntityId(-2), 1, 2, true, 6f, true);
            var added = f.Controller.ReceiveBlinderTrapPolicy(first);
            Assert.That(added.Count, Is.EqualTo(4));
            Assert.That(f.Controller.ReceiveBlinderTrapPolicy(second), Is.Empty);
            Assert.That(f.State.Traps.Count, Is.EqualTo(original + 4));
            Assert.That(f.Controller.TickTraps(100f), Is.Empty);
            Assert.That(f.Controller.SpringTrap(new EntityId(1), added[0].Anchor.Id, 2, out var trap, out _), Is.True);
            Assert.That(f.Controller.TryBlinderHit(trap, out var blind), Is.True);
            Assert.That(blind.Duration, Is.EqualTo(6f)); Assert.That(blind.MuffledDark, Is.True);
            Assert.That(blind.Trap, Is.True);
            Assert.That(f.Controller.ReceiveBlinderTrapPolicy(new BlinderTrapPolicyFact(new EntityId(-1), 0, 3, false, 9f, false)), Is.Empty);
            f.Controller.ReceiveBlinderTrapPolicy(new BlinderTrapPolicyFact(new EntityId(-1), 3, 0, false, 3f, false));
            f.Controller.ReceiveBlinderTrapPolicy(new BlinderTrapPolicyFact(new EntityId(-2), 3, 0, false, 3f, false));
            Assert.That(f.Controller.TickTraps(100f), Is.Not.Empty);
        }

        [Test]
        public void RegisteredPassageGoldEntersSnapshotBeforeCollectionAndIsNeverDoubleCounted()
        {
            var config = FloorCakeRulesTests.Config(); FloorCakeRulesTests.Set(config, "_requiredCakeCount", 1);
            var graph = new LevelGraph(new[] {
                new LevelRoom(1, Vector3.zero, Vector3.one * 8f),
                new LevelRoom(2, Vector3.right * 20f, Vector3.one * 8f, pocket: true) },
                Array.Empty<LevelEdge>(), new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, Vector3.zero) }, 1, Vector3.zero);
            var state = new FloorBehaviorState(); var controller = new FloorController(state, config, new System.Random(1));
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Vector3.zero };
            controller.Initialize(graph, new[] { player }, optionalGoldenCakeCount: 2);
            Assert.That(state.OptionalGoldenCakeCount, Is.Zero);
            Assert.That(controller.Snapshot().OptionalGoldenCakeCount, Is.Zero);
            Assert.That(controller.Snapshot().TotalGoldenCakes, Is.Zero, "Reserved gold is not a physical reward.");
            var reward = new LevelAnchor(101, 2, CakeAnchorType.Risk, Vector3.right * 20f);
            Assert.That(controller.ActivatePocket(2), Is.True);
            Assert.That(controller.RegisterPassageReward(reward), Is.True);
            Assert.That(controller.RegisterPassageReward(reward), Is.False);
            Assert.That(state.OptionalGoldenCakeCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot().OptionalGoldenCakeCount, Is.EqualTo(1));
            Assert.That(controller.Snapshot().TotalGoldenCakes, Is.EqualTo(1));
            Assert.That(controller.Snapshot().Golden, Is.Zero);
            Assert.That(controller.Collect(player.Id, 1, PickupKind.GoldenCake, 1, out _), Is.False);
            Assert.That(controller.Collect(player.Id, 1, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(state.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(controller.Tick(10000f, 1), Is.Empty);
            Assert.That(state.ActiveCakeAnchors.Select(a => a.Id), Is.EqualTo(new[] { reward.Id }));
            Assert.That(controller.DrainCakeLosses(), Is.Empty);
            Assert.That(controller.Collect(player.Id, reward.Id, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(controller.Collect(player.Id, reward.Id, PickupKind.GoldenCake, 1, out _), Is.True);
            Assert.That(controller.Collect(player.Id, reward.Id, PickupKind.GoldenCake, 2, out _), Is.False);
            Assert.That(controller.Snapshot().TotalGoldenCakes, Is.EqualTo(1));
            Assert.That(controller.Snapshot().Golden, Is.EqualTo(1));
            Assert.That(controller.Snapshot().OptionalGoldenCakeCount, Is.EqualTo(1));
            Assert.That(state.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(state.CollapseStarted, Is.True);
            controller.Reset(); Assert.That(state.OptionalGoldenCakeCount, Is.Zero);
        }
    }
}
