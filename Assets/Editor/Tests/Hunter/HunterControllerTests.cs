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
//   - Verify walk/stalk speeds, reveal holds, sight/noise transitions and bounded path-failure replanning.
//   - Bound stalled searches by the configured travel-aware leg deadline.
//   - Reject revival-protected contacts without spending an attack's acceptance latch.
// DEPENDENCIES:
//   - Hunter pure logic and steering, Player fixture state, Level read-only contract,
//     UnityEditor for temporary profile tuning, and NUnit.
// USAGE NOTES:
//   Edit Mode tests construct a temporary profile; no Play Mode or scene mutation.
//   Disable the separately tested deliberation beat to isolate approach speeds.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
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
            SetTuning("_deliberationSeconds", 0f);
            _state = new HunterBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Position = Vector3.forward * 10f, Health = 100f, SprintSpeed = 8f };
            _controller = new HunterController(_state, _profile, new System.Random(37), _player, new LevelFixture());
            _controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_profile); }
        private void SetTuning(string name, float value)
        {
            var serialized = new SerializedObject(_profile);
            serialized.FindProperty(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private int ReplanCount => (int)typeof(HunterBehaviorState).GetField("ReplanCount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_state);

        [TestCase(HunterAction.InvestigateHint, 1f)]
        [TestCase(HunterAction.InvestigateHint, 1.5f)]
        [TestCase(HunterAction.SearchLastKnown, 1f)]
        [TestCase(HunterAction.SearchLastKnown, 1.5f)]
        [TestCase(HunterAction.InvestigateLight, 1f)]
        [TestCase(HunterAction.InvestigateLight, 1.5f)]
        public void InvestigationAndSearchUseConfiguredWalkSpeed(HunterAction action, float multiplier)
        {
            Assert.That(_profile.InvestigateSpeed, Is.EqualTo(_profile.PatrolSpeed));
            SetTuning("_investigateSpeed", 2.5f);
            _controller.ApplyRunSpeedMultiplier(multiplier);
            HunterTickResult result;
            if (action == HunterAction.InvestigateLight)
                result = _controller.Tick(default, new HunterLightObservation(true, false, Vector3.right * 5f, 0), Dt, 0);
            else if (action == HunterAction.InvestigateHint)
            {
                _controller.Tick(default, Dt, 240);
                Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 0, 240,
                    Vector3.right * 10f, 4f, 0f)), Is.True);
                result = _controller.Tick(default, Dt, 241);
            }
            else
            {
                _controller.Tick(Visible, Dt, 0);
                result = _controller.Tick(default, Dt, 240);
            }
            Assert.That(_state.CurrentAction, Is.EqualTo(action));
            Assert.That(result.Speed, Is.EqualTo(2.5f * multiplier).Within(0.0001f));
            Assert.That(result.HoldPosition, Is.False);
        }

        [Test]
        public void FreshHintStalksItsBeliefRatherThanHiddenPlayerOrLightClue()
        {
            SetTuning("_stalkSpeedMultiplier", 0.65f);
            _controller.ApplyRunSpeedMultiplier(1.5f);
            _controller.Tick(default, Dt, 0);
            Vector3 belief = Vector3.right * 8f;
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 0, 0, belief, 0f, 0f)), Is.True);
            HunterTickResult result = _controller.Tick(default,
                new HunterLightObservation(true, false, Vector3.left * 5f, 4), Dt, 4);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Stalk));
            Assert.That(_state.PlayerVisible, Is.False);
            Assert.That(result.Target, Is.EqualTo(belief));
            Assert.That(result.Speed, Is.EqualTo(_player.SprintSpeed * _profile.ChaseSpeedMultiplier * 0.65f * 1.5f).Within(0.0001f));
            Assert.That(result.HoldPosition, Is.False);
        }

        [TestCase(1f)]
        [TestCase(1.5f)]
        public void FreshNoiseStalksObservedPositionAtScaledDefaultSpeed(float multiplier)
        {
            Vector3 noisePosition = Vector3.right * 6f;
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, noisePosition, 1f, 0) };
            _controller.ApplyRunSpeedMultiplier(multiplier);
            HunterTickResult result = _controller.Tick(default, Dt, 0);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Stalk));
            Assert.That(_controller.Sighting().Visible, Is.False, "Hearing must not confirm visual pursuit.");
            Assert.That(result.Target, Is.EqualTo(noisePosition));
            Assert.That(result.HoldPosition, Is.False);
            Assert.That(result.Speed, Is.EqualTo(_player.SprintSpeed * _profile.ChaseSpeedMultiplier *
                _profile.StalkSpeedMultiplier * multiplier).Within(0.0001f));
        }

        [Test]
        public void FailedStalkFallsBackToWalkSearchWithoutReplanningEveryTick()
        {
            _controller.Tick(Visible, Dt, 0);
            _controller.Tick(default, Dt, 4);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Stalk));
            int replans = ReplanCount;
            _controller.ReportPathFailure();
            for (int tick = 5; tick < 34; tick++) _controller.Tick(default, Dt, tick);
            HunterTickResult search = _controller.Tick(default, Dt, 34);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.SearchLastKnown));
            Assert.That(search.Speed, Is.EqualTo(_profile.InvestigateSpeed));
            Assert.That(ReplanCount, Is.EqualTo(replans + 1));
            for (int tick = 35; tick < 120; tick++) _controller.Tick(default, Dt, tick);
            Assert.That(ReplanCount, Is.InRange(replans + 1, replans + 2), "Search freshness invalidation is bounded, not per-tick.");
            _controller.Tick(Visible, Dt, 120);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Chase));
        }

        [TestCase(0f, 12f, true)]
        [TestCase(55f, 12f, true)]
        [TestCase(55.01f, 12f, false)]
        [TestCase(0f, 12.01f, false)]
        [TestCase(180f, 10f, false)]
        public void StalkRevealConeIncludesAngleAndDistanceBoundaries(float angle, float distance, bool hold)
        {
            _player.Position = Vector3.forward * distance;
            _controller.Tick(Visible, Dt, 0);
            _player.HeadingDegrees = 180f + angle;
            _controller.ApplyRunSpeedMultiplier(1.5f);
            HunterTickResult result = _controller.Tick(default, Dt, 4);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Stalk));
            Assert.That(result.HoldPosition, Is.EqualTo(hold));
            Assert.That(result.Speed, Is.EqualTo(hold ? 0f :
                _player.SprintSpeed * _profile.ChaseSpeedMultiplier * _profile.StalkSpeedMultiplier * 1.5f).Within(0.0001f));
            Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.None), "A reveal hold is not attack recovery.");
        }

        [Test]
        public void StalkHoldTracksLookBackEveryTickWithoutReplanning()
        {
            _controller.Tick(Visible, Dt, 0);
            _controller.Tick(default, Dt, 4);
            int replans = ReplanCount;
            for (int tick = 5; tick < 120; tick++)
            {
                _player.LookBack = tick % 2 == 0;
                HunterTickResult result = _controller.Tick(default, Dt, tick);
                Assert.That(result.HoldPosition, Is.EqualTo(_player.LookBack));
                Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Stalk));
            }
            Assert.That(ReplanCount, Is.EqualTo(replans));
        }

        [Test]
        public void StalkHoldStopsExistingMotorMomentumWhenForwardedAndResumesUnseen()
        {
            _controller.Tick(Visible, Dt, 0);
            _player.LookBack = true;
            HunterTickResult hold = _controller.Tick(default, Dt, 4);
            var steering = new HunterSteeringPresenter();
            var motor = new HunterSteeringDriverState();
            steering.Reset(motor, _state.Position, _state.Forward);
            steering.SetPath(motor, new[] { _state.Position, hold.Target });
            motor.Velocity = Vector3.forward * 5f;
            Vector3 stopped = steering.Tick(motor, Dt, hold.Speed, _profile.Acceleration, _profile.TurnRate,
                hold.HoldPosition, hold.ActiveContact, hold.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            Assert.That(stopped, Is.EqualTo(Vector3.zero));
            Assert.That(motor.Velocity, Is.EqualTo(Vector3.zero));
            _player.LookBack = false;
            HunterTickResult advance = _controller.Tick(default, Dt, 5);
            Vector3 resumed = steering.Tick(motor, Dt, advance.Speed, _profile.Acceleration, _profile.TurnRate,
                advance.HoldPosition, advance.ActiveContact, advance.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            Assert.That(resumed.z, Is.GreaterThan(0f));
        }

        [Test]
        public void StalkRevealTunablesControlHoldAndSightImmediatelyRestoresPursuit()
        {
            SetTuning("_stalkRevealDistance", 11f);
            SetTuning("_stalkViewHalfAngleDegrees", 20f);
            _controller.Tick(Visible, Dt, 0);
            _player.HeadingDegrees = 205f;
            Assert.That(_controller.Tick(default, Dt, 4).HoldPosition, Is.False);
            _player.HeadingDegrees = 195f;
            Assert.That(_controller.Tick(default, Dt, 5).HoldPosition, Is.True);
            SetTuning("_stalkRevealDistance", 9f);
            Assert.That(_controller.Tick(default, Dt, 6).HoldPosition, Is.False);
            SetTuning("_stalkRevealDistance", 11f);
            HunterTickResult chase = _controller.Tick(Visible, Dt, 8);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Chase));
            Assert.That(chase.HoldPosition, Is.False);
            Assert.That(chase.Speed, Is.EqualTo(_player.SprintSpeed * _profile.ChaseSpeedMultiplier));
            Assert.That(_controller.Sighting().Visible, Is.True);
        }

        [Test]
        public void StaleBeliefLeavesStalkAndCompletesFixedSearchBeforePatrol()
        {
            const float memoryDt = 0.25f; // Exact binary steps exercise the inclusive freshness boundary.
            _controller.Tick(Visible, memoryDt, 0);
            _controller.Tick(default, memoryDt, 12);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Stalk));
            int replans = ReplanCount;
            HunterTickResult search = _controller.Tick(default, memoryDt, 13);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.SearchLastKnown));
            Assert.That(search.Speed, Is.EqualTo(_profile.InvestigateSpeed));
            Assert.That(ReplanCount, Is.EqualTo(replans + 1));
            _controller.Tick(default, memoryDt, 32);
            Assert.That(_state.BeliefConfidence, Is.Zero);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.SearchLastKnown));
            int deadline = 33 + Mathf.CeilToInt(6f * _profile.SearchMaximumLegSeconds / memoryDt);
            for (int tick = 33; tick <= deadline; tick++) _controller.Tick(default, memoryDt, tick);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
        }

        [Test]
        public void FreshnessWithoutBeliefDoesNotStalkAndWalkOverflowIsRejected()
        {
            _controller.Tick(default, Dt, 0);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
            SetTuning("_investigateSpeed", float.MaxValue);
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyRunSpeedMultiplier(2f));
            Assert.That(_state.RunSpeedMultiplier, Is.EqualTo(1f));
        }

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
            _controller.Tick(default, 0.5f, 1); Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
            _controller.Tick(default, 0.25f, 2); Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
            _controller.Tick(default, 0.25f, 3); Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Chase));
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
        [TestCase(0f)] [TestCase(3f)]
        public void RevivalProtectionRejectsLungeWithoutSpendingContactAndStillAllowsPursuit(float immunitySeconds)
        {
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            try
            {
                var data = new SerializedObject(profile);
                data.FindProperty("_revivalDamageImmunitySeconds").floatValue = immunitySeconds;
                data.ApplyModifiedPropertiesWithoutUndo();
                var player = new PlayerController(_player, profile, new System.Random(1));
                player.Reset(new EntityId(1), Vector3.forward * 3f, 0f, .25f);
                player.ApplyHit(1000f); Assert.That(player.ReviveInPlace(.5f), Is.True);
                _controller.Tick(Visible, Dt, 0);
                for (int tick = 1; tick <= 15; tick++) _controller.Tick(Visible, Dt, tick);
                Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Active));
                Assert.That(_state.PlayerVisible, Is.True);
                Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
                player.AdvanceRecovery(8);
                Assert.That(_player.RevivalCollisionGraceActive, Is.False);
                if (immunitySeconds > 0f)
                {
                    Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
                    player.AdvanceRecovery(12);
                }
                Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.True);
                Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test] public void OversizedTickCannotLeaveContactsEnabledAfterActiveWindow()
        {
            _player.Position = Vector3.forward * 3f; _controller.Tick(Visible, Dt, 0);
            _controller.Tick(Visible, 0.6f, 1);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Recovery));
        }
        [TestCase(HunterAttackStyle.Projectile)] [TestCase(HunterAttackStyle.GroundSpikes)]
        public void RevivalRejectsRangedContactsWithoutSpendingFiredIdentity(HunterAttackStyle style)
        {
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            try
            {
                var data = new SerializedObject(_profile);
                data.FindProperty("_attackStyle").intValue = (int)style;
                data.ApplyModifiedPropertiesWithoutUndo();
                var player = new PlayerController(_player, profile, new System.Random(1));
                player.Reset(new EntityId(1), Vector3.forward * 10f, 0f, .25f);
                player.ApplyHit(1000f); Assert.That(player.ReviveInPlace(.5f), Is.True);
                _controller.Tick(Visible, .02f, 0);
                int serial = _state.AttackSerial;
                for (int tick = 1; tick < 18; tick++) _controller.Tick(Visible, .02f, tick);
                Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out _), Is.False);
                player.AdvanceRecovery(8);
                Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out _), Is.False);
                player.AdvanceRecovery(12);
                Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out _), Is.True);
                Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
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
