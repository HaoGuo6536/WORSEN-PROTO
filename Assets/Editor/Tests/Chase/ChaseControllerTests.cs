// ============================================================================
// ChaseControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies chase identity and timing with independently controlled hunter sources.
//   Tests explicitly cover both loss conditions and grace reacquisition so
//   feedback and measurements cannot count temporary occlusion as extra chases.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Exercise boundaries, duplicate sources, removal, catch idempotence and reset.
// DEPENDENCIES:
//   - Chase/Hunter pure rules, Player fixtures, Level read-only contract, Core facts and NUnit.
// USAGE NOTES:
//   Edit Mode tests construct a temporary config; no scene mutation or Play Mode.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Chase
{
    public sealed class ChaseControllerTests
    {
        private const float Dt = 1f / 60f;
        private ChaseConfig _config;
        private ChaseBehaviorState _state;
        private ChaseController _controller;
        private PlayerBehaviorState _player;
        private HunterFixture _hunter;
        private long _tick;
        private sealed class UnavailableLevelFixture : IReadOnlyLevelState
        {
            public bool IsReady => false;
            public LevelGraph Graph => null;
        }
        private sealed class HunterFixture : IReadOnlyHunterState
        {
            public EntityId Id { get; set; } = new EntityId(-1);
            public EntityId TargetId { get; set; } = new EntityId(1);
            public Vector3 Position { get; set; } = Vector3.forward * 10f;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public bool PlayerVisible { get; set; } = true;
            public Vector3 LastKnownPosition => Vector3.zero;
            public long LastKnownTick => 0;
            public float BeliefConfidence => 1f;
            public long Tick => 0;
            public bool IsActive { get; set; } = true;
        }
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ChaseConfig>();
            _state = new ChaseBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Forward = Vector3.forward };
            _hunter = new HunterFixture(); _tick = 0;
            _controller = new ChaseController(_state, _config, _player);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_config); }
        private ChaseTickResult Step(params IReadOnlyHunterState[] hunters)
            => _controller.Tick(hunters.Length == 0 ? new[] { _hunter } : hunters, Dt, ++_tick);
        private void Confirm() { for (int i = 0; i < 18; i++) Step(); Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Confirmed)); }
        private void Lose()
        {
            _hunter.PlayerVisible = false; _hunter.Position = Vector3.forward * 15f;
            for (int i = 0; i < 150; i++) Step();
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Lost));
        }
        [Test] public void ConfirmationRequiresContinuousPointThreeSeconds()
        {
            for (int i = 0; i < 17; i++) Assert.That(Step().Started, Is.False);
            Assert.That(Step().Started, Is.True); Assert.That(_state.ChaseId, Is.EqualTo(1));
            Assert.That(Step().Started, Is.False);
        }
        [Test] public void AlternatingHuntersCannotStitchConfirmation()
        {
            var other = new HunterFixture { Id = new EntityId(-2), PlayerVisible = false };
            for (int i = 0; i < 9; i++) Step(_hunter, other);
            _hunter.PlayerVisible = false; other.PlayerVisible = true;
            for (int i = 0; i < 9; i++) Step(_hunter, other);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.None));
            for (int i = 0; i < 9; i++) Step(_hunter, other);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Confirmed));
            Assert.That(_state.HunterId, Is.EqualTo(other.Id));
        }
        [Test] public void DuplicateSourceCannotAccelerateConfirmation()
        {
            for (int i = 0; i < 9; i++) Step(_hunter, _hunter);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.None));
        }
        [Test] public void BriefOcclusionRestartsConfirmation()
        {
            for (int i = 0; i < 17; i++) Step();
            _hunter.PlayerVisible = false; Step(); _hunter.PlayerVisible = true;
            Assert.That(Step().Started, Is.False);
        }
        [Test] public void LossRequiresTimeAndStrictlyGreaterDistance()
        {
            Confirm(); _hunter.PlayerVisible = false; _hunter.Position = Vector3.forward * 14f;
            for (int i = 0; i < 151; i++) Step();
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Confirmed));
            _hunter.Position = Vector3.forward * 14.001f;
            Assert.That(Step().Lost, Is.True);
        }
        [Test] public void DistanceAloneCannotEndChase()
        {
            Confirm(); _hunter.Position = Vector3.forward * 100f;
            for (int i = 0; i < 200; i++) Step();
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Confirmed));
        }
        [Test] public void AnotherConfirmedSourceWithinLossRadiusRetainsAggregate()
        {
            var other = new HunterFixture { Id = new EntityId(-2), Position = Vector3.right * 10f };
            for (int i = 0; i < 18; i++) Step(_hunter, other);
            _hunter.PlayerVisible = false; _hunter.Position = Vector3.forward * 50f; other.PlayerVisible = false;
            for (int i = 0; i < 160; i++) Step(_hunter, other);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Confirmed));
        }
        [Test] public void GraceLossEndsOnceAndOnlyAfterFullInterval()
        {
            Confirm(); int id = _state.ChaseId; Lose();
            for (int i = 0; i < 89; i++) Assert.That(Step().Ended, Is.False);
            ChaseTickResult end = Step();
            Assert.That(end.Ended, Is.True); Assert.That(end.Fact.EndReason, Is.EqualTo(ChaseEndReason.Lost));
            Assert.That(end.Fact.ChaseId, Is.EqualTo(id)); Assert.That(Step().Ended, Is.False);
        }
        [Test] public void ReacquisitionDuringGracePreservesIdentityWithoutNewStart()
        {
            Confirm(); int id = _state.ChaseId; Lose(); _hunter.PlayerVisible = true;
            ChaseTickResult resumed = default;
            for (int i = 0; i < 18; i++) { resumed = Step(); Assert.That(resumed.Started, Is.False); }
            Assert.That(resumed.PhaseChanged, Is.True);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Confirmed)); Assert.That(_state.ChaseId, Is.EqualTo(id));
        }
        [Test] public void DifferentHunterCanReacquireSameGraceIdentity()
        {
            Confirm(); int id = _state.ChaseId; Lose();
            var other = new HunterFixture { Id = new EntityId(-2) };
            for (int i = 0; i < 18; i++) Step(_hunter, other);
            Assert.That(_state.ChaseId, Is.EqualTo(id)); Assert.That(_state.HunterId, Is.EqualTo(other.Id));
        }
        [TestCase(-4f, 1f)]
        [TestCase(4f, 0.5f)]
        [TestCase(-20f, 0f)]
        [TestCase(40f, 0f)]
        public void ProximityClampsAndWeightsRearAndFront(float z, float expected)
        { _hunter.Position = Vector3.forward * z; Confirm(); Assert.That(_state.Closeness, Is.EqualTo(expected).Within(0.00001f)); }
        [Test] public void AcceptedCatchEndsExactlyOnceAndRetainsActualReason()
        {
            Confirm(); var hit = new HunterHit(_hunter.Id, _player.Id, 25, _tick, _hunter.Position, ChaseEndReason.Lunge);
            ChaseTickResult end = _controller.RecordCatch(hit);
            Assert.That(end.Ended, Is.True); Assert.That(end.Fact.EndReason, Is.EqualTo(ChaseEndReason.Lunge));
            Assert.That(_controller.RecordCatch(hit).Ended, Is.False);
        }
        [Test] public void PreConfirmationHitRemainsASeparateFactWithoutManufacturingAChase()
        {
            HunterProfile profile = ScriptableObject.CreateInstance<HunterProfile>();
            try
            {
                _player.Position = Vector3.forward * 3f; _player.SprintSpeed = 8f;
                var hunterState = new HunterBehaviorState();
                var hunter = new HunterController(hunterState, profile, new System.Random(37),
                    _player, new UnavailableLevelFixture());
                hunter.Reset(_hunter.Id, Vector3.zero, Vector3.forward);
                var sources = new IReadOnlyHunterState[] { hunterState };
                for (int tick = 0; tick < 15; tick++)
                {
                    hunter.Tick(new SightProbe(true, false, false), Dt, tick);
                    _controller.Tick(sources, Dt, tick);
                }
                // Match Session order: Hunter contacts happen before Chase's tick.
                hunter.Tick(new SightProbe(true, false, false), Dt, 15);
                Assert.That(hunter.TryAcceptContact(_player.Id, out HunterHit hit), Is.True);
                Assert.That(hit.Hunter, Is.EqualTo(_hunter.Id));
                Assert.That(hit.Target, Is.EqualTo(_player.Id));
                Assert.That(hit.Damage, Is.EqualTo(profile.LungeDamage));
                Assert.That(hit.Reason, Is.EqualTo(ChaseEndReason.Lunge));
                ChaseTickResult result = _controller.RecordCatch(hit);
                Assert.That(result.Started || result.Lost || result.Ended || result.PhaseChanged, Is.False);
                Assert.That(_state.Phase, Is.EqualTo(ChasePhase.None));
                Assert.That(_state.ChaseId, Is.Zero);
                Assert.That(_state.HasActiveChase, Is.False);
                // The separate accepted-hit fact remains available for Session telemetry.
                Assert.That(hit.Tick, Is.EqualTo(15));
                Assert.That(_controller.Tick(sources, Dt, 15).Started, Is.False);
                Assert.That(_controller.Tick(sources, Dt, 16).Started, Is.False);
                Assert.That(_controller.Tick(sources, Dt, 17).Started, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        [Test] public void WrongTargetCannotEndActiveChase()
        {
            Confirm();
            Assert.That(_controller.RecordCatch(new HunterHit(_hunter.Id, new EntityId(2), 25, _tick, Vector3.zero)).Ended, Is.False);
            Assert.That(_state.HasActiveChase, Is.True);
        }
        [Test] public void MissingSourceLosesWithoutRetainingStaleSight()
        {
            Confirm();
            for (int i = 0; i < 150; i++) _controller.Tick(Array.Empty<IReadOnlyHunterState>(), Dt, ++_tick);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.Lost));
        }
        [Test] public void DuplicateTickDoesNotAdvanceSourceTimers()
        {
            _controller.Tick(new[] { _hunter }, Dt, 1);
            for (int i = 0; i < 100; i++) _controller.Tick(new[] { _hunter }, Dt, 1);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.None));
        }
        [Test] public void ResetClearsIdentityAndSourceTimers()
        {
            Confirm(); _controller.Reset(); _tick = 0;
            Assert.That(_state.ChaseId, Is.Zero); Assert.That(_state.Closeness, Is.Zero);
            Assert.That(_state.Phase, Is.EqualTo(ChasePhase.None));
            Assert.That(Step().Started, Is.False);
        }
    }
}
