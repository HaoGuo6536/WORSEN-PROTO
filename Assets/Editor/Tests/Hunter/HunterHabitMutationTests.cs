// ============================================================================
// HunterHabitMutationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies observable habits and run overrides with injected time and topology.
//   Fixtures distinguish ordinary approach from authoritative chase/catch state,
//   and ensure neither shared profile assets nor unrelated tunables are changed.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Cover habit conditions, disabled entries, attack suppression and one-shot facts.
//   - Cover mutation validation, effective behavior, floor reset and new-life isolation.
// DEPENDENCIES:
//   - Hunter logic, Core contracts, Player/Level/Floor views, NUnit and reflection.
// USAGE NOTES:
//   No navigation bake or scene simulation; coordinator runs these in Edit Mode.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterHabitMutationTests
    {
        private HunterProfile _profile;
        private HunterController _controller;
        private HunterBehaviorState _state;
        private PlayerBehaviorState _player;
        private FloorView _floor;
        private sealed class LevelView : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = new LevelGraph(new[] {
                new LevelRoom(1, new Vector3(0, 2, 0), new Vector3(8, 4, 8)),
                new LevelRoom(2, new Vector3(0, 2, 8), new Vector3(8, 4, 8)) },
                new[] { new LevelEdge(1, 1, 2, true) }, Array.Empty<LevelAnchor>(), 2, Vector3.forward * 8);
        }
        private sealed class FloorView : IReadOnlyFloorState
        {
            public bool IsReady => true;
            public int CakeCount => 0;
            public int RequiredCakeCount => 1;
            public int GoldenCakeCount => 0;
            public ExitState ExitState => ExitState.Locked;
            public IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; } = new Dictionary<int, RoomPhase>();
            public IReadOnlyList<LevelAnchor> ActiveCakeAnchors { get; set; } = Array.Empty<LevelAnchor>();
        }
        private void Tune(string field, object value) => typeof(HunterProfile).GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_profile, value);
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>(); Tune("_sensorIntervalTicks", 1);
            _state = new HunterBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, SprintSpeed = 8f, Position = Vector3.forward * 12 };
            _floor = new FloorView();
            _controller = new HunterController(_state, _profile, new System.Random(11), _player, new LevelView());
            _controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward); _controller.SetFloorView(_floor);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_profile); }
        private HunterTickResult Tick(bool visible, long tick, float dt = 0.1f)
            => _controller.Tick(new SightProbe(visible, false, false), dt, tick);
        private void Cake(Vector3 position) => _floor.ActiveCakeAnchors = new[] { new LevelAnchor(10, 1, default, position) };
        private void Disable(HunterHabitKind kind) => Tune("_habits", new[] { new HunterHabitData(kind, false) });
        private HunterHabitFact Take(HunterHabitKind kind)
        {
            Assert.That(_controller.TryTakeHabit(out HunterHabitFact fact), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(kind)); Assert.That(fact.Hunter, Is.EqualTo(_state.Id));
            Assert.That(_controller.TryTakeHabit(out _), Is.False); return fact;
        }
        [Test] public void ThresholdPausesOncePerConnectedRoomCrossingAndCanRepeatInReverse()
        {
            Tick(false, 0); Assert.That(_controller.TryTakeHabit(out _), Is.False);
            _controller.CommitPose(Vector3.forward * 5, Vector3.forward, Vector3.forward);
            Assert.That(Tick(false, 1).HoldPosition, Is.True);
            HunterHabitFact fact = Take(HunterHabitKind.ThresholdPause);
            Assert.That(fact.Tick, Is.EqualTo(1)); Assert.That(fact.Position, Is.EqualTo(Vector3.forward * 5));
            Assert.That(Tick(false, 2, 0.2f).HoldPosition, Is.True);
            Assert.That(Tick(false, 3, 0.2f).HoldPosition, Is.False);
            Assert.That(_controller.TryTakeHabit(out _), Is.False);
            _controller.CommitPose(Vector3.zero, Vector3.back, Vector3.back);
            Assert.That(Tick(false, 4).HoldPosition, Is.True); Take(HunterHabitKind.ThresholdPause);
        }
        [Test] public void SharedBoundaryDoesNotReplayThresholdWhileStationary()
        {
            Tick(false, 0);
            _controller.CommitPose(Vector3.forward * 5, Vector3.zero, Vector3.forward);
            Tick(false, 1); Take(HunterHabitKind.ThresholdPause);
            _controller.CommitPose(Vector3.forward * 4, Vector3.zero, Vector3.back);
            Tick(false, 2); Take(HunterHabitKind.ThresholdPause);
            for (int i = 3; i <= 10; i++)
            {
                HunterTickResult result = Tick(false, i, 0.2f);
                Assert.That(_controller.TryTakeHabit(out _), Is.False);
                if (i >= 4) Assert.That(result.HoldPosition, Is.False);
            }
        }
        [Test] public void DisabledThresholdAndActiveChaseNeverPauseOrDropBelief()
        {
            Disable(HunterHabitKind.ThresholdPause); Tick(false, 0);
            _controller.CommitPose(Vector3.forward * 5, Vector3.zero, Vector3.forward);
            Assert.That(Tick(false, 1).HoldPosition, Is.False); Assert.That(_controller.TryTakeHabit(out _), Is.False);
            Tune("_habits", new[] { new HunterHabitData(HunterHabitKind.ThresholdPause) });
            Tick(true, 2); _controller.SetChaseActive(true);
            _controller.CommitPose(Vector3.zero, Vector3.zero, Vector3.forward);
            HunterTickResult result = Tick(false, 3);
            Assert.That(result.HoldPosition, Is.False); Assert.That(_state.BeliefConfidence, Is.GreaterThan(0f));
            Assert.That(_controller.TryTakeHabit(out _), Is.False);
        }
        [Test] public void BeliefLossReusesOneDeliberationAndRetainsLastKnownLookTarget()
        {
            Tick(true, 0); Vector3 known = _player.Position;
            _player.Position = Vector3.right * 50;
            Assert.That(Tick(false, 1).HoldPosition, Is.True);
            Assert.That(_controller.LookAtMemory, Is.True); Assert.That(_controller.LookTarget, Is.EqualTo(known));
            Assert.That(Take(HunterHabitKind.TurnToFace).Position, Is.EqualTo(known));
            Assert.That(_controller.TryTakeDeliberation(out Vector3 candidate), Is.True);
            Assert.That(candidate, Is.EqualTo(known)); Assert.That(_controller.TryTakeDeliberation(out _), Is.False);
            for (int i = 2; i <= 100; i++) Tick(false, i);
            Assert.That(_controller.TryTakeHabit(out _), Is.False);
        }
        [Test] public void SearchLooksAtMemoryWithoutFollowingHiddenPlayer()
        {
            Tune("_beliefFreshSeconds", 0f); Tick(true, 0); Vector3 known = _player.Position;
            _player.Position = Vector3.left * 40;
            for (int i = 1; i <= 8; i++) Tick(false, i);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.SearchLastKnown));
            Assert.That(_state.IsDeliberating, Is.False); Assert.That(_controller.LookAtMemory, Is.True);
            Assert.That(_controller.LookTarget, Is.EqualTo(known));
        }
        [Test] public void DisabledTurnNeverEmitsTheLossHabit()
        {
            Disable(HunterHabitKind.TurnToFace); Tick(true, 0); Tick(false, 1);
            Assert.That(_controller.TryTakeHabit(out _), Is.False); Assert.That(_state.IsDeliberating, Is.False);
        }
        [TestCase(15f, true)] [TestCase(15.01f, false)]
        public void CakeUsesRemovedAnchorPositionAndRadiusOnce(float distance, bool expected)
        {
            Cake(Vector3.right * distance); Tick(false, 0);
            _floor.ActiveCakeAnchors = Array.Empty<LevelAnchor>(); Tick(false, 1);
            Assert.That(_controller.TryTakeHabit(out HunterHabitFact fact), Is.EqualTo(expected));
            if (expected) { Assert.That(fact.Kind, Is.EqualTo(HunterHabitKind.CakeReaction)); Assert.That(fact.Position.x, Is.EqualTo(distance)); }
            Tick(false, 2); Assert.That(_controller.TryTakeHabit(out _), Is.False);
        }
        [Test] public void DisabledCakeNeverFiresAndCakeDuringChaseNeverStopsMovement()
        {
            Disable(HunterHabitKind.CakeReaction); Cake(Vector3.one); Tick(false, 0);
            _floor.ActiveCakeAnchors = Array.Empty<LevelAnchor>(); Tick(false, 1);
            Assert.That(_controller.TryTakeHabit(out _), Is.False);
            Tune("_habits", new[] { new HunterHabitData(HunterHabitKind.CakeReaction) });
            Cake(Vector3.one); Tick(true, 2); _floor.ActiveCakeAnchors = Array.Empty<LevelAnchor>();
            HunterTickResult result = Tick(true, 3); Take(HunterHabitKind.CakeReaction);
            Assert.That(result.HoldPosition, Is.False); Assert.That(result.Speed, Is.GreaterThan(0f));
            Assert.That(_state.PlayerVisible, Is.True);
        }
        [TestCase(HunterLungePhase.Windup)] [TestCase(HunterLungePhase.Active)] [TestCase(HunterLungePhase.Recovery)]
        public void EveryLungePhaseSuppressesAllHabits(HunterLungePhase phase)
        {
            Cake(Vector3.one); Tick(true, 0);
            typeof(HunterBehaviorState).GetProperty(nameof(HunterBehaviorState.LungePhase)).SetValue(_state, phase);
            _floor.ActiveCakeAnchors = Array.Empty<LevelAnchor>();
            _controller.CommitPose(Vector3.forward * 5, Vector3.zero, Vector3.forward); Tick(false, 1, 0.01f);
            Assert.That(_controller.TryTakeHabit(out _), Is.False); Assert.That(_controller.TryTakeDeliberation(out _), Is.False);
        }
        [Test] public void CatchSuppressesHabitsEvenAfterPlayerDeath()
        {
            Cake(Vector3.one); Tick(true, 0); _controller.SetCatchActive(true);
            _floor.ActiveCakeAnchors = Array.Empty<LevelAnchor>(); _player.Health = 0f;
            _controller.CommitPose(Vector3.forward * 5, Vector3.zero, Vector3.forward);
            Assert.That(Tick(false, 1).HoldPosition, Is.True); Assert.That(_controller.ShouldProbe(1), Is.False);
            Assert.That(_controller.TryTakeHabit(out _), Is.False); Assert.That(_controller.TryTakeDeliberation(out _), Is.False);
        }
        [TestCase(HunterTunable.Acceleration, 30f)] [TestCase(HunterTunable.TurnRate, 100f)]
        [TestCase(HunterTunable.ChaseSpeedMultiplier, 1.5f)] [TestCase(HunterTunable.ActionCommitmentSeconds, 2f)]
        [TestCase(HunterTunable.LossSeconds, 5f)] [TestCase(HunterTunable.LossDistance, 20f)]
        [TestCase(HunterTunable.ThresholdPauseEnabled, 0f)] [TestCase(HunterTunable.TurnToFaceEnabled, 0f)]
        [TestCase(HunterTunable.CakeReactionEnabled, 0f)]
        public void MutationChangesExactlyOneValueAndSurvivesFloorReset(HunterTunable changed, float value)
        {
            var before = new Dictionary<HunterTunable, float>();
            foreach (HunterTunable tunable in Enum.GetValues(typeof(HunterTunable))) before[tunable] = _controller.Effective(tunable);
            var mutation = new HunterMutation(changed, value, "uneven-breath");
            Assert.That(_controller.ApplyMutation(mutation, out HunterMutationFact fact), Is.True);
            Assert.That(fact.Hunter, Is.EqualTo(_state.Id)); Assert.That(fact.TellId, Is.EqualTo("uneven-breath"));
            Assert.That(fact.ArchetypeKey, Is.EqualTo(_profile.ArchetypeKey));
            Assert.That(_controller.ApplyMutation(mutation, out _), Is.False);
            _controller.Reset(new EntityId(-2), Vector3.zero, Vector3.forward); _controller.SetFloorView(new FloorView());
            foreach (var pair in before) Assert.That(_controller.Effective(pair.Key), Is.EqualTo(pair.Key == changed ? value : pair.Value));
            var fresh = new HunterController(new HunterBehaviorState(), _profile, new System.Random(11), _player, new LevelView());
            Assert.That(fresh.Effective(changed), Is.EqualTo(before[changed]), "Shared asset and new-run state remain unchanged.");
        }
        [Test] public void MutatedSpeedAndLossRulesReachTheirConsumers()
        {
            Assert.That(_controller.ApplyMutation(new HunterMutation(HunterTunable.ChaseSpeedMultiplier, 1.5f, "steps"), out _), Is.True);
            Assert.That(Tick(true, 0).Speed, Is.EqualTo(12f));
            Assert.That(_controller.ApplyMutation(new HunterMutation(HunterTunable.LossSeconds, 5f, "breath"), out _), Is.True);
            Assert.That(_state.LossSeconds, Is.EqualTo(5f)); Assert.That(_state.LossDistance, Is.EqualTo(_profile.LossDistance));
        }
        [Test] public void ManagerPublishesExactlyOneTellForOneAcceptedMutation()
        {
            var root = new GameObject("Mutation publication fixture");
            try
            {
                var manager = root.AddComponent<HunterManager>();
                typeof(HunterManager).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, _controller);
                int facts = 0; HunterMutationFact observed = default;
                manager.OnMutation += fact => { facts++; observed = fact; };
                var mutation = new HunterMutation(HunterTunable.LossDistance, 20f, "uneven-breath");
                Assert.That(manager.ApplyMutation(mutation), Is.True);
                Assert.That(manager.ApplyMutation(mutation), Is.False);
                Assert.That(manager.ApplyMutation(new HunterMutation(HunterTunable.LossDistance, -1f, "bad")), Is.False);
                Assert.That(facts, Is.EqualTo(1)); Assert.That(observed.TellId, Is.EqualTo("uneven-breath"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [TestCase(HunterTunable.Acceleration, -1f, "tell")]
        [TestCase(HunterTunable.TurnRate, 0f, "tell")]
        [TestCase(HunterTunable.ChaseSpeedMultiplier, float.NaN, "tell")]
        [TestCase(HunterTunable.ChaseSpeedMultiplier, float.PositiveInfinity, "tell")]
        [TestCase(HunterTunable.ChaseSpeedMultiplier, float.MaxValue, "tell")]
        [TestCase(HunterTunable.LossSeconds, 3f, "")]
        [TestCase(HunterTunable.LossSeconds, 3f, null)]
        [TestCase(HunterTunable.ActionCommitmentSeconds, 0.01f, "tell")]
        [TestCase(HunterTunable.CakeReactionEnabled, 0.5f, "tell")]
        [TestCase((HunterTunable)999, 1f, "tell")]
        public void InvalidMutationIsRejectedWithoutFact(HunterTunable tunable, float value, string tell)
        {
            Assert.That(_controller.ApplyMutation(new HunterMutation(tunable, value, tell), out HunterMutationFact fact), Is.False);
            Assert.That(fact.Hunter.IsValid, Is.False); Assert.That(_profile.ChaseSpeedMultiplier, Is.EqualTo(1.12f));
        }
    }
}
