// ============================================================================
// HunterControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Hunter sensing, imperfect memory, planning and committed attack rules.
//   Real profile defaults run against explicit Player and Level values so edge
//   conditions are reproducible without a scene or navigation bake.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify sample thresholds, aged information, failed paths and per-life contacts.
//   - Verify instance speed scaling and phase-normalized attack indicators without shared asset mutation.
// DEPENDENCIES:
//   - Hunter pure logic, Player fixture state, Level read-only contract and NUnit.
// USAGE NOTES:
//   Edit Mode tests construct a temporary profile; no Play Mode or scene mutation.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterControllerTests
    {
        private const float Dt = 1f / 60f;
        private HunterProfile _profile;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private PlayerBehaviorState _player;
        private static readonly SightProbe Visible = new SightProbe(true, false, false);
        private sealed class LevelFixture : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = new LevelGraph(
                new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 80f) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        }
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>();
            _state = new HunterBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Position = Vector3.forward * 10f, Health = 100f, SprintSpeed = 8f };
            _controller = new HunterController(_state, _profile, new System.Random(37), _player, new LevelFixture());
            _controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_profile); }
        [Test]
        public void RunSpeedScalingAffectsPatrolChaseAndLungeAndResetsPerLife()
        {
            _controller.ApplyRunSpeedMultiplier(1.5f);
            HunterTickResult patrol = _controller.Tick(default, Dt, 0);
            Assert.That(patrol.Speed, Is.EqualTo(_profile.PatrolSpeed * 1.5f).Within(0.0001f));
            HunterTickResult chase = _controller.Tick(Visible, Dt, 4);
            Assert.That(chase.Speed, Is.EqualTo(_player.SprintSpeed * _profile.ChaseSpeedMultiplier * 1.5f).Within(0.0001f));
            Assert.That(_controller.LungeSpeed, Is.EqualTo(_profile.LungeSpeed * 1.5f).Within(0.0001f));
            Assert.That(_profile.PatrolSpeed, Is.EqualTo(3f));
            _controller.Reset(new EntityId(-2), Vector3.zero, Vector3.forward);
            Assert.That(_state.RunSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(_controller.LungeSpeed, Is.EqualTo(_profile.LungeSpeed));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRunSpeedCannotReplaceLastValidMultiplier(float invalid)
        {
            _controller.ApplyRunSpeedMultiplier(1.25f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyRunSpeedMultiplier(invalid));
            Assert.That(_state.RunSpeedMultiplier, Is.EqualTo(1.25f));
        }

        [Test]
        public void AttackIndicatorTracksCommittedPositionDirectionAndPhaseProgress()
        {
            HunterAttackSample idle = _controller.AttackSample();
            Assert.That(idle.Phase, Is.Zero);
            Assert.That(idle.Progress, Is.Zero);
            _player.Position = Vector3.forward * 3f;
            _controller.Tick(Visible, Dt, 0);
            _controller.CommitPose(Vector3.right, Vector3.zero, Vector3.left);
            _controller.Tick(Visible, _profile.LungeWindupSeconds * 0.5f, 1);
            HunterAttackSample windup = _controller.AttackSample();
            Assert.That(windup.Hunter, Is.EqualTo(_state.Id));
            Assert.That(windup.Position, Is.EqualTo(Vector3.right));
            Assert.That(windup.Direction, Is.EqualTo(Vector3.forward), "A committed lunge telegraphs its locked direction.");
            Assert.That(windup.Phase, Is.EqualTo(1));
            Assert.That(windup.Progress, Is.EqualTo(0.5f).Within(0.0001f));
            _controller.Tick(Visible, _profile.LungeWindupSeconds * 0.5f, 2);
            Assert.That(_controller.AttackSample().Phase, Is.EqualTo(2));
            _controller.Tick(Visible, _profile.LungeActiveSeconds * 0.5f, 3);
            Assert.That(_controller.AttackSample().Progress, Is.EqualTo(0.5f).Within(0.0001f));
            _controller.Tick(Visible, _profile.LungeActiveSeconds * 0.5f, 4);
            Assert.That(_controller.AttackSample().Phase, Is.EqualTo(3));
            _controller.Tick(Visible, _profile.LungeRecoverySeconds * 0.5f, 5);
            Assert.That(_controller.AttackSample().Progress, Is.EqualTo(0.5f).Within(0.0001f));
            _player.Position = Vector3.forward * 10f;
            _controller.Tick(Visible, _profile.LungeRecoverySeconds * 0.5f, 6);
            Assert.That(_controller.AttackSample().Phase, Is.Zero);
            Assert.That(_controller.AttackSample().Progress, Is.Zero);
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void AnyOneSightSampleEstablishesBelief(bool head, bool chest, bool hips)
        {
            _controller.Tick(new SightProbe(head, chest, hips), Dt, 0);
            Assert.That(_state.PlayerVisible, Is.True);
            Assert.That(_state.LastKnownPosition, Is.EqualTo(_player.Position));
            Assert.That(_state.BeliefConfidence, Is.EqualTo(1f));
        }
        [TestCase(30f, true)]
        [TestCase(30.001f, false)]
        public void SightRangeIncludesThreshold(float range, bool visible)
        { _player.Position = Vector3.forward * range; _controller.Tick(Visible, Dt, 0); Assert.That(_state.PlayerVisible, Is.EqualTo(visible)); }
        [TestCase(55f, true)]
        [TestCase(55.01f, false)]
        public void SightConeIncludesThreshold(float angle, bool visible)
        {
            _player.Position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 20f;
            _controller.Tick(Visible, Dt, 0); Assert.That(_state.PlayerVisible, Is.EqualTo(visible));
        }
        [Test] public void OcclusionSamplesUpdateEveryFourTicks()
        {
            _controller.Tick(Visible, Dt, 0);
            for (int tick = 1; tick < 4; tick++) { _controller.Tick(default, Dt, tick); Assert.That(_state.PlayerVisible, Is.True); }
            _controller.Tick(default, Dt, 4); Assert.That(_state.PlayerVisible, Is.False);
        }
        [TestCase(30, true)]
        [TestCase(31, false)]
        public void HearingUsesNoiseAgeAndNeverRefreshesAnOldNoise(long tick, bool heard)
        {
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, Vector3.forward * 5f, 1f, 0) };
            _controller.Tick(default, Dt, tick);
            Assert.That(_state.BeliefConfidence > 0f, Is.EqualTo(heard));
            _controller.Tick(default, Dt, 480);
            Assert.That(_state.BeliefConfidence, Is.Zero);
        }
        [TestCase(60, 30, true)]
        [TestCase(60, 31, false)]
        [TestCase(120, 60, true)]
        [TestCase(120, 61, false)]
        [TestCase(50, 25, true)]
        [TestCase(50, 26, false)]
        public void HearingAgeEqualitySurvivesFloatDeltaRoundingAtLargeTickOrigin(int tickRate, int elapsedTicks, bool heard)
        {
            const long noiseTick = 1000000000;
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, Vector3.forward * 5f, 1f, noiseTick) };
            _controller.Tick(default, 1f / tickRate, noiseTick + elapsedTicks);
            Assert.That(_state.BeliefConfidence > 0f, Is.EqualTo(heard));
            if (heard) Assert.That(_state.LastKnownTick, Is.EqualTo(noiseTick));
        }
        [Test] public void FutureAndOutOfRangeNoisesDoNotCreateBelief()
        {
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, Vector3.forward, 1f, 1),
                new NoiseEvent(_player.Id, Vector3.forward * 18f, 1f, 0) };
            _controller.Tick(default, Dt, 0); Assert.That(_state.BeliefConfidence, Is.Zero);
        }
        [Test] public void HiddenTargetUsesLastKnownPositionAndMemoryExpiresAtEightSeconds()
        {
            _controller.Tick(Visible, Dt, 0); Vector3 known = _player.Position;
            _player.Position = Vector3.right * 60f;
            HunterTickResult result = _controller.Tick(default, Dt, 4);
            Assert.That(result.Target, Is.EqualTo(known));
            _controller.Tick(default, Dt, 480); Assert.That(_state.BeliefConfidence, Is.Zero);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
        }
        [Test] public void DelayedHintPreservesExplicitAgeAndSamplesInsideRadius()
        {
            _controller.Tick(default, Dt, 180);
            var hint = new HintPayload(_state.Id, _player.Id, 0, 180, new Vector3(20f, 0f, 0f), 3f, 8f);
            Assert.That(_controller.ReceiveHint(hint), Is.True);
            Assert.That(Vector3.Distance(_state.LastKnownPosition, hint.Position), Is.LessThanOrEqualTo(8f));
            Assert.That(_state.LastKnownTick, Is.Zero);
            Assert.That(_state.BeliefConfidence, Is.EqualTo(0.3125f).Within(0.00001f));
            _controller.Tick(default, Dt, 181);
            Assert.That(_state.BeliefConfidence, Is.EqualTo(0.5f * (1f - (3f + Dt) / 8f)).Within(0.00001f));
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.InvestigateHint));
        }
        [Test] public void StaleWrongIdentityFutureAndNonfiniteHintsAreRejected()
        {
            _controller.Tick(default, Dt, 600);
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 0, 600, Vector3.one, 8f, 8f)), Is.False);
            Assert.That(_controller.ReceiveHint(new HintPayload(new EntityId(-2), _player.Id, 590, 600, Vector3.one, 1f, 8f)), Is.False);
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 590, 601, Vector3.one, 1f, 8f)), Is.False);
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 590, 600, Vector3.one, 1f, float.NaN)), Is.False);
        }
        [Test] public void SightOutranksHintsAndSelectsCatchPlan()
        {
            _controller.Tick(Visible, Dt, 0);
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 0, 0, Vector3.right, 0f, 8f)), Is.False);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Chase));
            _player.Position = Vector3.forward * 4f;
            _controller.Tick(Visible, Dt, 4);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Lunge));
        }
        [Test] public void FailedChaseFallsBackThenRetriesInsteadOfCachingPatrol()
        {
            _controller.Tick(Visible, Dt, 0); _controller.ReportPathFailure();
            _controller.Tick(default, Dt, 1); Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
            _controller.Tick(default, Dt, 2); Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Chase));
        }
        [Test] public void CommittedLungeHasExactPhasesAndOnlyOneTargetContact()
        {
            _player.Position = Vector3.forward * 3f;
            _controller.Tick(Visible, Dt, 0);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Windup));
            Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
            for (int tick = 1; tick <= 15; tick++) _controller.Tick(Visible, Dt, tick);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Active));
            Assert.That(_controller.TryAcceptContact(new EntityId(2), out _), Is.False);
            Assert.That(_controller.TryAcceptContact(_player.Id, out HunterHit hit), Is.True);
            Assert.That(hit.Reason, Is.EqualTo(ChaseEndReason.Lunge));
            Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
            for (int tick = 16; tick <= 33; tick++) _controller.Tick(Visible, Dt, tick);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Recovery));
            Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
            for (int tick = 34; tick <= 80; tick++) _controller.Tick(Visible, Dt, tick);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Recovery));
        }
        [Test] public void OversizedTickCannotLeaveContactsEnabledAfterActiveWindow()
        {
            _player.Position = Vector3.forward * 3f; _controller.Tick(Visible, Dt, 0);
            _controller.Tick(Visible, 0.6f, 1);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Recovery));
        }
        [Test] public void ResetClearsBeliefLungeAndPerLifeContact()
        {
            _player.Position = Vector3.forward * 3f; _controller.Tick(Visible, Dt, 0);
            for (int tick = 1; tick <= 15; tick++) _controller.Tick(Visible, Dt, tick);
            Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.True);
            _controller.Reset(new EntityId(-3), Vector3.zero, Vector3.forward);
            Assert.That(_state.BeliefConfidence, Is.Zero); Assert.That(_state.PlayerVisible, Is.False);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(_state.Id, Is.EqualTo(new EntityId(-3)));
        }
    }
}
