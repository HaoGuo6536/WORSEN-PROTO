// ============================================================================
// FloorGuidanceControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies curse-gated Mimic guidance using immutable facts and an explicit clock.
//   False cakes must never acquire pickup credit or alter hands and optional rewards.
//   These regressions run without scene objects, physics or navigation.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Cover pose exclusion, curse presence, timed windows and deterministic duplicates.
//   - Preserve Blind Faith, real gold guidance, target identity and snapshot immutability.
//   - Regress reset, stale facts, invalid inputs, hands, optional rewards and no bail.
// DEPENDENCIES:
//   - Core contracts, Domain Floor pure classes and NUnit.
//   - Existing FloorCakeRulesTests managed config/graph fixtures and reflection only.
// USAGE NOTES:
//   No live engine objects. Seed, delta time and fact ticks are explicit.
//   Session-to-Floor event routing is a separate coordinator integration requirement.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorGuidanceControllerTests
    {
        private static readonly EntityId Player = new EntityId(1);
        private static readonly EntityId Hunter = new EntityId(-7);
        private static readonly GuidanceTarget White = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.back, Vector3.back * 10f, 11);
        private static readonly GuidanceTarget Gold = new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.right, Vector3.right * 10f, 12);
        private static readonly GuidanceTarget[] Normal = { White, Gold };
        private static ActiveEffects Curse => new ActiveEffects(new[] { new ActiveEffect(FloorGuidanceController.FaithlessArrow, EffectKind.Curse, 1) });
        private static FloorGuidanceController Controller(bool cursed = false)
        {
            var controller = new FloorGuidanceController(new FloorGuidanceBehaviorState());
            if (cursed) controller.SetActiveEffects(Curse);
            return controller;
        }
        private static MimicFact Fact(MimicFactKind kind, long tick = 1, float seconds = 2f, bool golden = false,
            EntityId? hunter = null, EntityId? player = null, Vector3? position = null)
            => new MimicFact(hunter ?? Hunter, player ?? Player, kind, tick, position ?? Vector3.forward * 4f, seconds, golden);
        private static void Window(FloorGuidanceController c, EntityId? hunter = null, Vector3? position = null)
        {
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.Pose, hunter: hunter, position: position)), Is.True);
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow, hunter: hunter)), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void OrdinaryOrGoldenPoseNeverGuidesWithoutActiveCurse(bool golden)
        {
            var c = Controller(); c.ReceiveMimic(Fact(MimicFactKind.Pose, golden: golden));
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow)), Is.False);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            c.SetActiveEffects(Curse);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal), "Curse presence alone does not invent a window.");
        }

        [TestCase(false)] [TestCase(true)]
        public void ActiveWindowReplacesOnlyWhiteAndNeverUsesCakeIdentity(bool golden)
        {
            var c = Controller(true); c.ReceiveMimic(Fact(MimicFactKind.Pose, golden: golden));
            c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow));
            var targets = c.Apply(Normal, Player, Vector3.up * 3f);
            Assert.That(targets.Count, Is.EqualTo(2)); Assert.That(targets[0].Kind, Is.EqualTo(GuidanceKind.WhiteArrow));
            Assert.That(targets[0].EntityId, Is.EqualTo(Hunter)); Assert.That(targets[0].AnchorId, Is.EqualTo(-1));
            Assert.That(targets[0].WorldDirection, Is.EqualTo(Vector3.forward));
            Assert.That(targets[0].TargetPosition, Is.EqualTo(Vector3.forward * 4f)); Assert.That(targets[0].IsFallback, Is.True);
            Assert.That(targets[1], Is.EqualTo(Gold)); Assert.That(Normal[0], Is.EqualTo(White));
            c.ReceiveMimic(Fact(MimicFactKind.PoseRemoved, 2));
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            Assert.That(targets[0].EntityId, Is.EqualTo(Hunter), "Later removal cannot mutate an earlier snapshot.");
        }

        [Test]
        public void WindowExpiresExactlyWithInjectedTimeAndDuplicateFactCannotExtendIt()
        {
            var c = Controller(true); Window(c);
            Assert.That(c.Tick(1f), Is.False);
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow)), Is.False);
            Assert.That(c.Apply(Normal, Player, Vector3.zero)[0].EntityId, Is.EqualTo(Hunter));
            Assert.That(c.Tick(1f), Is.True);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            Assert.That(c.Tick(0f), Is.False);
        }

        [Test] public void PopulationIsAbsoluteCappedAndSurvivesSpentPoseWithoutGuidingAtIt()
        {
            var c = Controller(true);
            c.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(FloorGuidanceController.FaithlessArrow, EffectKind.Curse, 1),
                new ActiveEffect(new EffectId("mimic-more-mimics"), EffectKind.Curse, 99) }));
            Window(c);
            Assert.That(c.ReceiveMimic(new MimicFact(Hunter, Player, MimicFactKind.Population, 1, Vector3.forward * 4, extraCount: 99)), Is.True);
            Assert.That(c.ExtraMimicCount, Is.EqualTo(3));
            c.ReceiveMimic(new MimicFact(Hunter, Player, MimicFactKind.PoseRemoved, 2, Vector3.forward * 4, extraCount: 3));
            Assert.That(c.ExtraMimicCount, Is.EqualTo(3));
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow, 3)), Is.False);
            c.ReceiveMimic(new MimicFact(Hunter, Player, MimicFactKind.Population, 4, Vector3.forward * 4, extraCount: 2));
            Assert.That(c.ExtraMimicCount, Is.EqualTo(2));
            c.SetActiveEffects(null); Assert.That(c.ExtraMimicCount, Is.Zero);
            c.Reset(); Assert.That(c.ExtraMimicCount, Is.Zero);
        }

        [Test]
        public void RemovingCurseImmediatelyRestoresTruthAndReaddingDoesNotReviveWindow()
        {
            var c = Controller(true); Window(c); c.SetActiveEffects(null);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            c.SetActiveEffects(Curse);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow, 2)), Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void TypeWideCurseAdmitsAllInstancesAndSelectionIsOrderIndependent(bool reversed)
        {
            var c = Controller(true); var hunters = new[] { new EntityId(-8), Hunter };
            if (reversed) Array.Reverse(hunters);
            foreach (var hunter in hunters) Window(c, hunter);
            Assert.That(c.Apply(Normal, Player, Vector3.zero)[0].EntityId, Is.EqualTo(new EntityId(-8)));
            c.ReceiveMimic(Fact(MimicFactKind.PoseRemoved, 2, hunter: new EntityId(-8)));
            Assert.That(c.Apply(Normal, Player, Vector3.zero)[0].EntityId, Is.EqualTo(Hunter));
            Assert.That(c.Apply(Normal, new EntityId(2), Vector3.zero), Is.SameAs(Normal));
        }

        [Test]
        public void NearestWindowWinsButCannotRestoreSuppressedOrMissingWhiteArrow()
        {
            var c = Controller(true); Window(c); Window(c, new EntityId(-8), Vector3.right);
            Assert.That(c.Apply(Normal, Player, Vector3.zero)[0].EntityId, Is.EqualTo(new EntityId(-8)));
            Assert.That(c.Apply(new[] { Gold }, Player, Vector3.zero), Is.EqualTo(new[] { Gold }));
            Assert.That(c.Apply(Array.Empty<GuidanceTarget>(), Player, Vector3.zero), Is.Empty);
            var blind = FloorCakeRulesTests.Start(hooks: new FloorCakeHooks(blindFaith: true));
            Assert.That(c.Apply(blind.Controller.GuidanceTargets(false), Player, Vector3.zero), Is.Empty);
        }

        [Test]
        public void ResetClearsFactsEffectsAndWatermarksForNextFloor()
        {
            var c = Controller(true); Window(c); c.Tick(1f); c.Reset();
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
            c.SetActiveEffects(Curse); Window(c);
            Assert.That(c.Tick(1f), Is.False);
            Assert.That(c.Apply(Normal, Player, Vector3.zero)[0].EntityId, Is.EqualTo(Hunter));
        }

        [Test]
        public void RemovedPoseRejectsOlderResurrectionOrWindowWithoutPose()
        {
            var c = Controller(true);
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow)), Is.False);
            Window(c); c.ReceiveMimic(Fact(MimicFactKind.PoseRemoved, 3));
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.Pose, 2)), Is.False);
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow, 4)), Is.False);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
        }

        [TestCase(-1f)] [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidWindowDurationsCannotLie(float seconds)
        {
            var c = Controller(true); c.ReceiveMimic(Fact(MimicFactKind.Pose));
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow, seconds: seconds)), Is.False);
            Assert.That(c.Apply(Normal, Player, Vector3.zero), Is.SameAs(Normal));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaTimeIsRejected(float dt)
        { Assert.Throws<ArgumentOutOfRangeException>(() => Controller().Tick(dt)); }

        [Test]
        public void InvalidOrCoincidentPositionsAndForeignPlayersCannotPublishFalseDirection()
        {
            var c = Controller(true);
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.Pose, hunter: EntityId.None)), Is.False);
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.Pose, position: new Vector3(float.NaN, 0, 0))), Is.False);
            Window(c);
            Assert.That(c.Apply(Normal, Player, Vector3.forward * 4f), Is.SameAs(Normal));
            Assert.That(c.Apply(Normal, Player, new Vector3(float.PositiveInfinity, 0, 0)), Is.SameAs(Normal));
            Assert.That(c.ReceiveMimic(Fact(MimicFactKind.FaithlessWindow, 2, player: new EntityId(2))), Is.False);
        }

        [Test]
        public void MimicGuidanceCannotMintPuzzleGoldOrDelayCompletedExit()
        {
            var f = FloorCakeRulesTests.Start(hooks: new FloorCakeHooks(greedyDoor: true, goldenSense: true));
            var c = Controller(true); Window(c);
            var initial = f.Controller.Snapshot(); var anchors = f.State.ActiveCakeAnchors.ToArray();
            Assert.That(f.Controller.RegisterPuzzleReward(99, 99, new Vector3(60f, 0f, 0f)), Is.True);
            Assert.That(f.Controller.SolvePuzzle(99, 6, 99, out _), Is.False);
            Assert.That(f.Controller.Collect(Player, 99, PickupKind.GoldenCake, 1, out _), Is.False);
            Assert.That(f.Controller.Collect(Player, Hunter.Value, PickupKind.Cake, 1, out _), Is.False);
            Assert.That(f.State.ActiveCakeAnchors, Is.EqualTo(anchors)); Assert.That(f.State.CakeCount, Is.Zero);
            FloorCakeRulesTests.CollectRequired(f);
            f.Controller.SelectCue(new[] { new FloorPathCandidate(0, 2f, Vector3.back) });
            Assert.That(c.Apply(f.Controller.GuidanceTargets(false, Gold), Player, Vector3.zero)[0].EntityId, Is.EqualTo(Hunter));
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(f.Controller.Snapshot().TotalGoldenCakes, Is.EqualTo(initial.TotalGoldenCakes));
            var hands = new FloorHandController(new FloorHandBehaviorState(), FloorCakeRulesTests.Config());
            var probe = new FloorHandProbe(1, 7, Vector3.zero, .5f, true, Vector3.back);
            Assert.That(hands.Tick(Player, true, probe, 0f, 1, out var warning), Is.True);
            Assert.That(warning.Kind, Is.EqualTo(CollapseHandEventKind.Warning));
            Assert.That(hands.Tick(Player, true, probe, .7f, 2, out var grab), Is.True);
            Assert.That(grab.Kind, Is.EqualTo(CollapseHandEventKind.Grabbed));
            Assert.That(hands.Tick(Player, true, probe, 1.4f, 3, out var hit), Is.True);
            Assert.That(hit.Kind, Is.EqualTo(CollapseHandEventKind.Hit)); Assert.That(hit.Damage, Is.EqualTo(25f));
            Assert.That(f.State.GoldenCakeCount, Is.Zero);
        }

        [Test]
        public void PocketCannotCloseUntilItsRegisteredGoldIsCollected()
        {
            var config = FloorCakeRulesTests.Config();
            var graph = LevelGraphUtility.Build(new[] {
                new LevelRoom(1, new Vector3(10f, 2f, 0f), new Vector3(8f, 4f, 8f)),
                new LevelRoom(2, new Vector3(20f, 2f, 0f), new Vector3(8f, 4f, 8f), pocket: true),
                new LevelRoom(3, new Vector3(30f, 2f, 0f), new Vector3(8f, 4f, 8f)) },
                new[] { new LevelEdge(1, 1, 3, true) },
                new[] { new LevelAnchor(101, 1, CakeAnchorType.Flow, new Vector3(10f, 0f, 0f)) }, 3, new Vector3(30f, 0f, 0f));
            var player = new Worsen.Domain.Player.PlayerBehaviorState { Id = Player, Position = graph.ExitPosition, Health = 100f };
            var state = new FloorBehaviorState(); var floor = new FloorController(state, config, new System.Random(7));
            floor.Initialize(graph, new[] { player }, requiredCakeCount: 1,
                cakeHooks: new FloorCakeHooks(greedyDoor: true), optionalGoldenCakeCount: 2);
            foreach (int id in new[] { 998, 999 })
                Assert.That(floor.RegisterPassageReward(new LevelAnchor(id, 2, CakeAnchorType.Risk, new Vector3(20f, 0f, 0f))), Is.True);
            Assert.That(floor.Collect(Player, 999, PickupKind.GoldenCake, 1, out _), Is.True);
            Assert.That(state.CakeCount, Is.Zero); Assert.That(state.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(floor.ActivatePocket(2), Is.True);
            var facts = floor.Tick(100f, 2);
            Assert.That(facts, Is.Empty);
            var losses = floor.DrainCakeLosses();
            Assert.That(losses, Is.Empty);
            Assert.That(floor.Collect(Player, 101, PickupKind.Cake, 3, out _), Is.True);
            Assert.That(state.CollapseStarted, Is.False);
            Assert.That(floor.Tick(100f, 3), Is.Empty); Assert.That(floor.DrainCakeLosses(), Is.Empty);
            Assert.That(floor.Collect(Player, 998, PickupKind.GoldenCake, 3, out _), Is.True);
            Assert.That(state.CollapseStarted, Is.True);
            Assert.That(state.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(floor.Tick(100f, 4).Where(f => f.RoomId == 2).Select(f => f.Phase), Is.EqualTo(new[] {
                RoomPhase.Telegraph, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed }));
            Assert.That(floor.RegisterPassageReward(new LevelAnchor(997, 2, CakeAnchorType.Risk, new Vector3(20f, 0f, 0f))), Is.False);
            Assert.That(floor.Collect(Player, 998, PickupKind.GoldenCake, 3, out _), Is.False);
            Assert.That(state.GoldenCakeCount, Is.EqualTo(2)); Assert.That(state.RoomPhases[3], Is.EqualTo(RoomPhase.Open));
        }

        [Test]
        public void FloorHasNoBailSurfaceAndLockedContactNeverCompletesEvenAfterLongWait()
        {
            foreach (var type in new[] { typeof(FloorManager), typeof(FloorController), typeof(FloorDriver), typeof(FloorExitDoor), typeof(FloorExitVolume) })
                Assert.That(type.GetMembers(BindingFlags.Public | BindingFlags.Instance).Any(m => m.Name.IndexOf("Bail", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, type.Name);
            var f = FloorCakeRulesTests.Start();
            for (int tick = 1; tick <= 10; tick++)
            {
                Assert.That(f.Controller.ContactExit(Player, tick, out _), Is.False);
                f.Controller.Tick(100f, tick);
            }
            Assert.That(f.State.CakeCount, Is.Zero); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            FloorCakeRulesTests.CollectRequired(f);
            Assert.That(f.Controller.ContactExit(Player, 11, out _), Is.True);
            Assert.That(f.Controller.ContactExit(Player, 12, out _), Is.False);
        }
    }
}
