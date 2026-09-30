// ============================================================================
// FloorThemeConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Specifies optional puzzle gold and safe freeze-room collapse priorities.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Keep puzzle gold one-shot and outside required/Greedy Door quotas.
//   - Preserve the exit, live escape routes and full telegraphs under freeze priority.
//   - Check real Manager spawning and cosmetic-only hand scaling.
// DEPENDENCIES:
//   NUnit, Core, Floor, Player read-only state and existing Floor test fixtures.
// USAGE NOTES:
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
        public void PuzzleGoldIsOneShotAndDoesNotPayRequiredOrGreedyQuota()
        {
            var f = FloorCakeRulesTests.Start(hooks: new FloorCakeHooks(greedyDoor: true));
            var required = f.State.ActiveCakeAnchors.Select(a => a.Id).ToArray();
            Assert.That(f.Controller.RegisterPuzzleReward(900, 9000, new Vector3(60f, 1f, 0f)), Is.True);
            Assert.That(f.Controller.RegisterPuzzleReward(901, 9000, new Vector3(60f, 1f, 0f)), Is.False);
            Assert.That(f.Controller.SolvePuzzle(900, 5, 9000, out _), Is.False);
            Assert.That(f.Controller.SolvePuzzle(900, 6, 9000, out var reward), Is.True);
            Assert.That(reward.Position, Is.EqualTo(new Vector3(60f, 1f, 0f)));
            Assert.That(f.Controller.SolvePuzzle(900, 6, 9000, out _), Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), 9000, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(f.Controller.Collect(new EntityId(1), 9000, PickupKind.GoldenCake, 1, out var fact), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(PickupKind.GoldenCake));
            Assert.That(f.Controller.Collect(new EntityId(1), 9000, PickupKind.GoldenCake, 2, out _), Is.False);
            Assert.That(f.State.CakeCount, Is.Zero); Assert.That(f.State.GoldenCakeCount, Is.EqualTo(1));
            Assert.That(f.State.ActiveCakeAnchors.Select(a => a.Id), Is.EqualTo(required));
            Assert.That(f.State.CollapseStarted, Is.False); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            FloorCakeRulesTests.CollectRequired(f);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked), "Puzzle gold cannot satisfy Greedy Door.");
            f.Controller.Reset();
            Assert.That(f.Controller.SolvePuzzle(900, 6, 9000, out _), Is.False);
        }

        [Test]
        public void SolvedRewardCreatesOneEnabledPickupAndPublishesOneGoldenFact()
        {
            var root = new GameObject("Puzzle gold fixture");
            var config = ScriptableObject.CreateInstance<FloorConfig>();
            var visual = ScriptableObject.CreateInstance<FloorDriverConfig>();
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
                Assert.That(manager.SolvePuzzle(900, 6, 9000), Is.True);
                Assert.That(manager.SolvePuzzle(900, 6, 9000), Is.False);
                var pickup = root.GetComponentsInChildren<CakePickup>().Single(p => p.AnchorId == 9000);
                Assert.That(pickup.Kind, Is.EqualTo(PickupKind.GoldenCake));
                Assert.That(pickup.GetComponent<Collider>().enabled, Is.True);
                manager.Collect(new EntityId(1), 9000, PickupKind.GoldenCake);
                manager.Collect(new EntityId(1), 9000, PickupKind.GoldenCake);
                Assert.That(facts, Is.EqualTo(1)); Assert.That(pickup.gameObject.activeSelf, Is.False);
                foreach (var a in manager.ReadOnlyState.ActiveCakeAnchors.ToArray()) manager.Collect(new EntityId(1), a.Id, PickupKind.Cake);
                Assert.That(root.GetComponentsInChildren<CakePickup>(true).Count(p => p.AnchorId == 9000), Is.EqualTo(1));
                Assert.That(pickup.gameObject.activeSelf, Is.False, "Collapse must not resurrect collected puzzle gold.");
            }
            finally { manager.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); Object.DestroyImmediate(visual); }
        }

        [TestCase(false)] [TestCase(true)]
        public void FreezePriorityKeepsEscapeRouteAndFullTelegraph(bool shuffled)
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
            Assert.That(first.Select(f => f.RoomId), Is.EqualTo(new[] { 3 }));
            Assert.That(state.RoomPhases[3], Is.EqualTo(RoomPhase.Telegraph));
            controller.Tick(config.TelegraphDuration - .1f, 2);
            Assert.That(state.RoomPhases[3], Is.EqualTo(RoomPhase.Telegraph));
            controller.Tick(.2f, 3); Assert.That(state.RoomPhases[3], Is.EqualTo(RoomPhase.Tearing));
            controller.Tick(10000f, 4);
            foreach (int id in new[] { 4, 5, 6 }) Assert.That(state.RoomPhases[id], Is.EqualTo(RoomPhase.Open));
            player.Position = new Vector3(60f, 1f, 0f);
            controller.Tick(0f, 5); Assert.That(state.RoomPhases[5], Is.EqualTo(RoomPhase.Telegraph));
            controller.Tick(10000f, 6); Assert.That(state.RoomPhases[6], Is.EqualTo(RoomPhase.Open));
        }

        [Test]
        public void HandLookOnlyScalesArt()
        {
            Assert.That(FloorHandPresenter.VisualScale("shadow-hands", 2f, 1.15f), Is.EqualTo(2f));
            Assert.That(FloorHandPresenter.VisualScale("gloved-shadow-hands", 2f, 1.15f), Is.EqualTo(2.3f).Within(.001f));
            Assert.That(FloorHandPresenter.VisualScale("unknown", 2f, 1.15f), Is.EqualTo(2f));
        }
        private sealed class Level : IReadOnlyLevelState
        { public bool IsReady => true; public LevelGraph Graph => FloorCakeRulesTests.Graph(); }
    }
}
