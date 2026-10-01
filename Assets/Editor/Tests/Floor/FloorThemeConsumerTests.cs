// ============================================================================
// FloorThemeConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Specifies Passage-only gold and deterministic collapse despite legacy theme hooks.
//   Controller and Manager cases preserve one-shot reward and pickup lifecycle coverage.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Reject puzzle gold and require each one-shot Passage reward before collapse.
//   - Preserve the exit and full ten-second phases without freeze-room priority.
//   - Check real Manager spawning and cosmetic-only hand scaling.
// DEPENDENCIES:
//   NUnit, Core, Floor, Player read-only state and existing Floor test fixtures.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Edit Mode; temporary objects only. The coordinator runs these in Unity.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorThemeConsumerTests
    {
        [Test]
        public void PuzzleCannotMintGoldAndPassageRewardIsOneShotAndCollectionGated()
        {
            var f = FloorCakeRulesTests.Passage();
            var required = f.State.ActiveCakeAnchors.Select(a => a.Id).ToArray();
            Assert.That(f.Controller.RegisterPuzzleReward(900, 9000, new Vector3(60f, 1f, 0f)), Is.True);
            Assert.That(f.Controller.RegisterPuzzleReward(901, 9000, new Vector3(60f, 1f, 0f)), Is.False);
            Assert.That(f.Controller.SolvePuzzle(900, 5, 9000, out _), Is.False);
            Assert.That(f.Controller.SolvePuzzle(900, 6, 9000, out var puzzleReward), Is.False);
            Assert.That(puzzleReward.Id, Is.Zero);
            Assert.That(f.Controller.SolvePuzzle(900, 6, 9000, out _), Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), 9000, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), 9000, PickupKind.GoldenCake, 1, out _), Is.False);
            Assert.That(f.Controller.Snapshot().TotalGoldenCakes, Is.Zero);
            Assert.That(f.State.ActiveCakeAnchors.Select(a => a.Id), Is.EqualTo(required));
            var reward = new LevelAnchor(9001, 7, CakeAnchorType.Risk, new Vector3(70f, 1f, 0f));
            Assert.That(f.Controller.RegisterPassageReward(new LevelAnchor(9002, 6, CakeAnchorType.Risk, new Vector3(60f, 1f, 0f))), Is.False);
            Assert.That(f.Controller.ActivatePocket(7), Is.True);
            Assert.That(f.Controller.RegisterPassageReward(reward), Is.True);
            Assert.That(f.Controller.RegisterPassageReward(reward), Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), reward.Id, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), reward.Id, PickupKind.GoldenCake, 1, out var fact), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(PickupKind.GoldenCake));
            Assert.That(f.Controller.Collect(new EntityId(1), reward.Id, PickupKind.GoldenCake, 2, out _), Is.False);
            Assert.That(f.State.CakeCount, Is.Zero); Assert.That(f.State.GoldenCakeCount, Is.EqualTo(1));
            Assert.That(f.State.ActiveCakeAnchors.Select(a => a.Id), Is.EqualTo(required));
            Assert.That(f.State.CollapseStarted, Is.False); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            FloorCakeRulesTests.CollectRequired(f);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open), "All physical cakes have been collected.");
            Assert.That(f.State.CollapseStarted, Is.True);
            f.Controller.Tick(10000f, 3);
            Assert.That(f.Controller.DrainCakeLosses(), Is.Empty);
            Assert.That(f.Controller.Snapshot().TotalGoldenCakes, Is.EqualTo(1));
            f.Controller.Reset();
            Assert.That(f.Controller.SolvePuzzle(900, 6, 9000, out _), Is.False);
        }

        [Test]
        public void PassageCreatesOneEnabledPickupAndPublishesOneGoldenFactWithoutPuzzleGold()
        {
            var root = new GameObject("Passage gold fixture");
            var config = ScriptableObject.CreateInstance<FloorConfig>();
            var visual = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
            var driver = root.AddComponent<FloorDriver>(); FloorCakeRulesTests.Set(driver, "_config", visual);
            var manager = root.AddComponent<FloorManager>();
            try
            {
                FloorCakeRulesTests.Set(config, "_useRoomCakeDensity", false);
                FloorCakeRulesTests.Set(config, "_requiredCakeCount", 6);
                manager.Initialize(config, new Level(), new[] { new FloorCakeRulesTests.Player() }, new System.Random(7));
                int facts = 0; manager.OnPickupCollected += f => { if (f.Kind == PickupKind.GoldenCake) facts++; };
                Assert.That(manager.RegisterPuzzleReward(900, 9000, new Vector3(60f, 1f, 0f)), Is.True);
                Assert.That(root.GetComponentsInChildren<CakePickup>().Any(p => p.AnchorId == 9000), Is.False);
                Assert.That(manager.SolvePuzzle(900, 6, 9000), Is.False);
                Assert.That(manager.SolvePuzzle(900, 6, 9000), Is.False);
                manager.Collect(new EntityId(1), 9000, PickupKind.GoldenCake);
                Assert.That(facts, Is.Zero);
                Assert.That(root.GetComponentsInChildren<CakePickup>(true).Any(p => p.AnchorId == 9000), Is.False);
                var reward = new LevelAnchor(9001, 7, CakeAnchorType.Risk, new Vector3(70f, 1f, 0f));
                Assert.That(manager.ActivatePocket(7), Is.True);
                Assert.That(manager.RegisterPassageReward(reward), Is.True);
                Assert.That(manager.RegisterPassageReward(reward), Is.False);
                var pickup = root.GetComponentsInChildren<CakePickup>().Single(p => p.AnchorId == reward.Id);
                Assert.That(pickup.Kind, Is.EqualTo(PickupKind.GoldenCake));
                Assert.That(pickup.GetComponent<Collider>().enabled, Is.True);
                manager.Collect(new EntityId(1), reward.Id, PickupKind.GoldenCake);
                manager.Collect(new EntityId(1), reward.Id, PickupKind.GoldenCake);
                Assert.That(facts, Is.EqualTo(1)); Assert.That(pickup.gameObject.activeSelf, Is.False);
                int losses = 0;
                manager.OnCakeLost += (anchor, room, kind, tick) => losses++;
                foreach (var a in manager.ReadOnlyState.ActiveCakeAnchors.ToArray()) manager.Collect(new EntityId(1), a.Id, PickupKind.Cake);
                Assert.That(manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                manager.Tick(10000f, 4);
                Assert.That(manager.ReadOnlyState.RoomPhases[7], Is.EqualTo(RoomPhase.Closed));
                Assert.That(losses, Is.Zero);
                Assert.That(facts, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<CakePickup>(true).Count(p => p.AnchorId == reward.Id), Is.EqualTo(1));
                Assert.That(pickup.gameObject.activeSelf, Is.False, "Collapse must not resurrect collected Passage gold.");
            }
            finally { manager.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); Object.DestroyImmediate(visual); }
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacyFreezeAndShuffleHooksCannotChangeFarthestFirstOrderOrFullTelegraph(bool shuffled)
        {
            var graph = FloorCakeRulesTests.Graph(); var state = new FloorBehaviorState();
            var config = FloorCakeRulesTests.Config();
            var player = new PlayerBehaviorState { Id = new EntityId(1), Position = new Vector3(40f, 1f, 0f), Health = 100f };
            var controller = new FloorController(state, config, new System.Random(7));
            controller.Initialize(graph, new[] { player }, shuffledCollapse: shuffled,
                preferredAnchors: new[] { 42 }, earlyCollapseRooms: new[] { 3, 5, 6 });
            Assert.That(state.ActiveCakeAnchors.Any(a => a.Id == 42), Is.True);
            foreach (var a in state.ActiveCakeAnchors.ToArray()) controller.Collect(player.Id, a.Id, PickupKind.Cake, 0, out _);
            var first = controller.Tick(0f, 1);
            Assert.That(first.Select(f => (f.RoomId, f.Phase)), Is.EqualTo(new[] { (1, RoomPhase.Telegraph) }));
            controller.Tick(config.CollapseTelegraphSeconds - .1f, 2);
            Assert.That(state.RoomPhases[1], Is.EqualTo(RoomPhase.Telegraph));
            controller.Tick(.2f, 3); Assert.That(state.RoomPhases[1], Is.EqualTo(RoomPhase.Tearing));
            var rest = controller.Tick(10000f, 4);
            Assert.That(rest.Where(f => f.Phase == RoomPhase.Telegraph).Select(f => f.RoomId), Is.EqualTo(new[] { 2, 3, 4, 5 }));
            foreach (int id in new[] { 1, 2, 3, 4, 5 }) Assert.That(state.RoomPhases[id], Is.EqualTo(RoomPhase.Closed));
            Assert.That(state.RoomPhases[6], Is.EqualTo(RoomPhase.Open));
            Assert.That(controller.DrainCakeLosses(), Is.Empty);
        }

        [Test]
        public void HandLookOnlyScalesArt()
        {
            Assert.That(FloorHandPresenter.VisualScale("shadow-hands", 2f, 1.15f), Is.EqualTo(2f));
            Assert.That(FloorHandPresenter.VisualScale("gloved-shadow-hands", 2f, 1.15f), Is.EqualTo(2.3f).Within(.001f));
            Assert.That(FloorHandPresenter.VisualScale("unknown", 2f, 1.15f), Is.EqualTo(2f));
        }
        private sealed class Level : IReadOnlyLevelState
        { public bool IsReady => true; public LevelGraph Graph { get; } = FloorCakeRulesTests.Passage().Graph; }
    }
}
