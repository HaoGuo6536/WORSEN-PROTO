// ============================================================================
// CameraFeedbackPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies first-person presentation against explicit samples and elapsed time.
//   These tests protect rear-view snaps, held catches and unshaken aim without a live Cinemachine rig.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Camera.
//
// KEY RESPONSIBILITIES:
//   - Verify catch approach, stable hold timing, hand targeting, precedence and reset.
//   - Cover once-per-tick look input, bounded view angles and exact timing endpoints.
//   - Exercise effect composition, comfort settings, death and reset isolation.
//
// DEPENDENCIES:
//   - Core contracts; Camera presentation math; NUnit and editor config serialization.
//
// USAGE NOTES:
//   - Edit Mode; creates temporary config assets only and destroys them after every test.
//   - No physics, live camera, scene, input device or human comfort acceptance is simulated.
//
// ============================================================================

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Camera;

namespace Worsen.Tests.Camera
{
    public sealed class CameraFeedbackPresenterTests
    {
        private CameraDriverConfig _config;
        private CameraDriverState _state;
        private CameraFeedbackPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            _state = new CameraDriverState();
            _presenter = new CameraFeedbackPresenter();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void HorizontalLensIsStableAcrossAspectRatios()
        {
            Assert.That(_presenter.HorizontalToVerticalFieldOfView(95f, 1f), Is.EqualTo(95f).Within(0.0001f));
            var widescreen = _presenter.HorizontalToVerticalFieldOfView(95f, 16f / 9f);
            Assert.That(widescreen, Is.InRange(63f, 64f));
            Assert.That(_presenter.HorizontalToVerticalFieldOfView(95f, 32f / 9f), Is.LessThan(widescreen));
            Assert.That(_presenter.HorizontalToVerticalFieldOfView(95f, float.NaN), Is.EqualTo(widescreen));
        }

        [Test]
        public void SpeedFieldOfViewUsesHorizontalSpeedAndCapsAtDesignMaximum()
        {
            _presenter.SetMovement(_state, _config, Sample(1, velocity: new Vector3(0f, 100f, 14f)));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(103f).Within(0.0001f));
            _presenter.SetMovement(_state, _config, Sample(2, velocity: new Vector3(0f, 0f, 100f)));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(103f).Within(0.0001f));
        }

        [Test]
        public void DetectionHasExactAttackDecayAndComposesWithSpeed()
        {
            _presenter.SetMovement(_state, _config, Sample(1, velocity: Vector3.forward * 14f));
            _presenter.PlayDetectionBeat(_state);
            _presenter.Tick(_state, _config, 0.04f, 1f);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(109f).Within(0.0001f));
            _presenter.Tick(_state, _config, 0.04f, 1f);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(115f).Within(0.0001f));
            _presenter.Tick(_state, _config, 0.4f, 1f);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(103f).Within(0.0001f));
            Assert.That(_state.DetectionElapsed, Is.LessThan(0f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LookBackSnapsInOneStepWithoutIntermediateYawEvenWithLegacyTuning(bool legacy)
        {
            if (legacy)
            {
                var serialized = new SerializedObject(_config);
                serialized.FindProperty("_lookBackYaw").floatValue = 160f;
                serialized.FindProperty("_lookBackSeconds").floatValue = 0.12f;
                serialized.FindProperty("_lookForwardSeconds").floatValue = 0.15f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            _presenter.SetMovement(_state, _config, Sample(1, lookBack: true));
            Assert.That(_state.LookYaw, Is.EqualTo(180f));
            foreach (float dt in new[] { 0f, 1f / 60f, 0.12f })
            {
                _presenter.Tick(_state, _config, dt, 1f);
                Assert.That(Quaternion.Angle(_state.Rotation, Quaternion.Euler(0f, 210f, 0f)), Is.LessThan(0.01f));
                Assert.That(_state.LookYaw, Is.EqualTo(180f));
            }
            _presenter.SetMovement(_state, _config, Sample(2));
            Assert.That(_state.LookYaw, Is.Zero);
            _presenter.Tick(_state, _config, 1f / 60f, 1f);
            Assert.That(Quaternion.Angle(_state.Rotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void RapidLookBackEdgesRemainBinary()
        {
            _presenter.SetLookBack(_state, _config, true);
            _presenter.SetMovement(_state, _config, Sample(1, look: new Vector2(80f, 0f), lookBack: true));
            _presenter.SetLookBack(_state, _config, false);
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.LookYaw, Is.Zero);
            _presenter.SetLookBack(_state, _config, true);
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.LookYaw, Is.EqualTo(180f));
        }

        [Test]
        public void DuplicateSamplesNeverConsumeHeadLookTwice()
        {
            var sample = Sample(8, look: new Vector2(5f, 6f));
            _presenter.SetMovement(_state, _config, sample);
            _presenter.SetMovement(_state, _config, sample);
            _presenter.Tick(_state, _config, 0.12f, 1f);
            Assert.That(_state.Pitch, Is.EqualTo(-6f));
            Assert.That(_state.HeadYaw, Is.Zero);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(30f));
            Assert.That(_state.Position, Is.EqualTo(new Vector3(2f, 1.6f, 3f)));
        }

        [Test]
        public void LookBackIgnoresLookDeltasAndPreservesForwardPitch()
        {
            _presenter.SetMovement(_state, _config, Sample(1, look: new Vector2(90f, 90f)));
            Assert.That(_state.Pitch, Is.EqualTo(-85f));
            Assert.That(_state.HeadYaw, Is.Zero);
            _presenter.SetMovement(_state, _config, Sample(2, look: new Vector2(900f, -900f), lookBack: true));
            Assert.That(_state.Pitch, Is.EqualTo(-85f));
            Assert.That(_state.HeadYaw, Is.Zero);
            _presenter.SetMovement(_state, _config, Sample(3, look: new Vector2(-900f, 900f), lookBack: true));
            Assert.That(_state.Pitch, Is.EqualTo(-85f));
            Assert.That(_state.LookYaw, Is.EqualTo(180f));
            _presenter.SetMovement(_state, _config, Sample(4));
            Assert.That(_state.Pitch, Is.EqualTo(-85f));
            Assert.That(_state.HeadYaw + _state.LookYaw, Is.Zero);
        }

        [Test]
        public void ReboundIsASeparateSuccessfulFactAndTakesPriorityOverSlideRoll()
        {
            _presenter.SetMovement(_state, _config, Sample(1, movement: MovementState.Slide));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.Zero);
            _presenter.PlayTraversal(_state, new PlayerTraversalFact(default, 1, TraversalKind.Rebound, false, Vector3.left, 0f));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.Zero);
            _presenter.PlayTraversal(_state, new PlayerTraversalFact(default, 2, TraversalKind.Rebound, true, Vector3.left, 0f));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.EqualTo(-10f));
            _presenter.Tick(_state, _config, 0.25f, 1f);
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.Zero);
        }

        [Test]
        public void ComfortSettingsDisableRollAndDetectionFieldOfView()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_tiltEnabled").boolValue = false;
            serialized.FindProperty("_punchIntensity").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _presenter.SetMovement(_state, _config, Sample(1, movement: MovementState.Slide));
            _presenter.PlayDetectionBeat(_state);
            _presenter.Tick(_state, _config, 0.08f, 1f);
            Assert.That(_state.Roll, Is.Zero);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(95f));
        }

        [Test]
        public void CatchFacesHunterUpperBodyAndResetClearsStaleSequences()
        {
            _presenter.SetMovement(_state, _config, Sample(1));
            var killer = _state.EyePosition + Vector3.left * 3f - Vector3.up * _config.CatchHunterFocusHeight;
            _presenter.PlayDeathSnap(_state, _config, killer);
            _presenter.Tick(_state, _config, _config.CatchApproachSeconds, 1f);
            Assert.That(Vector3.Dot(_state.Rotation * Vector3.forward, Vector3.left), Is.GreaterThan(0.999f));
            _presenter.PlayDetectionBeat(_state);
            Assert.That(_state.DetectionElapsed, Is.LessThan(0f));
            _presenter.Reset(_state);
            Assert.That(_state.DeathSnapped, Is.False);
            Assert.That(_state.HasMovement, Is.False);
            Assert.That(_state.LookBack, Is.False);
            Assert.That(_state.TraversalTick, Is.EqualTo(-1));
            Assert.That(_state.CatchHoldStarted || _state.CatchHoldEnded, Is.False);
        }

        [Test]
        public void InvalidTimeDoesNotAdvanceOrPoisonView()
        {
            _presenter.PlayDetectionBeat(_state);
            _presenter.Tick(_state, _config, float.NaN, 1f);
            Assert.That(_state.DetectionElapsed, Is.Zero);
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(95f));
            _presenter.Tick(_state, _config, -1f, 1f);
            Assert.That(_state.DetectionElapsed, Is.Zero);
        }

        [Test]
        public void BankingUsesAchievedTurnAndReturnsSmoothlyToNeutral()
        {
            _presenter.SetMovement(_state, _config, Sample(1, movement: MovementState.Slide, turnRate: 40f));
            _presenter.Tick(_state, _config, 0.1f, 1f);
            Assert.That(_state.Roll, Is.InRange(-6f, -0.1f));
            float bank = _state.Roll;
            _presenter.SetMovement(_state, _config, Sample(2));
            _presenter.Tick(_state, _config, 0.05f, 1f);
            Assert.That(_state.Roll, Is.GreaterThan(bank).And.LessThan(0f));
            _presenter.Tick(_state, _config, 1f, 1f);
            Assert.That(_state.Roll, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ShakeIsClampedDoesNotChangeAimAndCanBeDisabled()
        {
            _presenter.SetMovement(_state, _config, Sample(1, look: new Vector2(150f, 10f), lookBack: true));
            _presenter.Tick(_state, _config, 0f, 1f);
            Quaternion aim = _state.AimRotation;
            _presenter.PlayShake(_state, 100f, 10f);
            _presenter.Tick(_state, _config, 0.02f, 1f);
            Assert.That(_state.AimRotation, Is.EqualTo(aim));
            Assert.That(Vector3.Distance(_state.Position, _state.EyePosition), Is.LessThanOrEqualTo(_config.MaximumShakeDisplacement));
            Assert.That(Quaternion.Angle(_state.Rotation, aim), Is.LessThanOrEqualTo(_config.MaximumShakeDegrees * 3f));
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_shakeIntensity").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _presenter.Tick(_state, _config, 0.02f, 1f);
            Assert.That(_state.Position, Is.EqualTo(_state.EyePosition));
            Assert.That(Quaternion.Angle(_state.Rotation, aim), Is.LessThan(0.001f));
        }

        [Test]
        public void HandCatchUsesExactGrabPointAndWinsOverFollowingDeathAndMovement()
        {
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.Tick(_state, _config, 0f, 1f);
            var start = _state.Position;
            var grab = start + Vector3.left * 3f + Vector3.down;
            _presenter.PlayConsumed(_state, _config, grab);
            _presenter.PlayDeathSnap(_state, _config, start + Vector3.right * 10f);
            _presenter.SetMovement(_state, _config, Sample(2, look: new Vector2(90f, 90f)));
            _presenter.Tick(_state, _config, _config.CatchApproachSeconds, 1f);
            Assert.That(_state.Consumed, Is.True);
            Assert.That(Vector3.Distance(_state.Position, grab), Is.EqualTo(_config.CatchDistance).Within(0.0001f));
            Assert.That(Vector3.Angle(_state.Rotation * Vector3.forward, grab - _state.Position), Is.LessThan(0.01f));
            var end = _state.Position;
            _presenter.PlayConsumed(_state, _config, start + Vector3.right * 10f);
            _presenter.Tick(_state, _config, 10f, 1f);
            Assert.That(_state.Position, Is.EqualTo(end));
            Assert.That(_state.CatchHoldEnded, Is.True);
        }

        [Test]
        public void CatchNeedsValidTriggerAndResetAllowsANewHunterCatch()
        {
            _presenter.PlayConsumed(_state, _config, Vector3.zero);
            Assert.That(_state.Consumed, Is.False);
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.PlayShake(_state, 1f, 1f);
            _presenter.PlayDetectionBeat(_state);
            _presenter.PlayConsumed(_state, _config, new Vector3(float.NaN, 0f, 0f));
            _presenter.PlayConsumed(_state, _config, new Vector3(float.MaxValue, 0f, 0f));
            Assert.That(_state.Consumed, Is.False);
            _presenter.PlayConsumed(_state, _config, _state.EyePosition + Vector3.back * 10000f);
            _presenter.Tick(_state, _config, float.NaN, 1f);
            Assert.That(_state.CatchElapsed, Is.Zero);
            _presenter.Tick(_state, _config, 100f, 1f);
            Assert.That(float.IsNaN(_state.Position.sqrMagnitude), Is.False);
            Assert.That(_state.CatchHoldStarted, Is.True);
            Assert.That(_state.CatchHoldEnded, Is.False, "A long approach frame must not swallow the visible hold.");
            _presenter.Reset(_state);
            Assert.That(_state.Consumed, Is.False);
            Assert.That(_state.CatchApproachDuration, Is.Zero);
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.PlayDeathSnap(_state, _config, _state.EyePosition + Vector3.right - Vector3.up * _config.CatchHunterFocusHeight);
            _presenter.Tick(_state, _config, _config.CatchApproachSeconds, 1f);
            Assert.That(Vector3.Dot(_state.Rotation * Vector3.forward, Vector3.right), Is.GreaterThan(0.999f));
        }

        [Test]
        public void CatchApproachesThenHoldsForConfiguredBeatWithoutDrift()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_catchDistance").floatValue = 2f;
            serialized.FindProperty("_catchApproachSeconds").floatValue = 0.25f;
            serialized.FindProperty("_catchHoldSeconds").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.Tick(_state, _config, 0f, 1f);
            var start = _state.Position;
            var killer = start + Vector3.back * 4f;
            var focus = killer + Vector3.up * _config.CatchHunterFocusHeight;
            _presenter.PlayDeathSnap(_state, _config, killer);
            Assert.That(_state.CatchHoldStarted || _state.CatchHoldEnded, Is.False);
            _presenter.Tick(_state, _config, 0.125f, 1f);
            Assert.That(_state.Position, Is.Not.EqualTo(start).And.Not.EqualTo(_state.CatchTargetPosition));
            Assert.That(_state.CatchHoldStarted, Is.False);
            _presenter.Tick(_state, _config, 0.125f, 1f);
            Assert.That(_state.CatchHoldStarted, Is.True);
            Assert.That(_state.CatchHoldEnded, Is.False);
            Assert.That(Vector3.Distance(_state.Position, focus), Is.EqualTo(2f).Within(0.0001f));
            var held = _state.Position; var rotation = _state.Rotation;
            _presenter.PlayDeathSnap(_state, _config, Vector3.zero);
            _presenter.PlayShake(_state, 1f, 1f);
            _presenter.SetMovement(_state, _config, Sample(2, velocity: Vector3.one * 100f, look: Vector2.one * 90f));
            _presenter.Tick(_state, _config, 0.25f, 1f);
            Assert.That(_state.CatchHoldEnded, Is.False);
            _presenter.Tick(_state, _config, 0.25f, 1f);
            Assert.That(_state.CatchHoldEnded, Is.True);
            _presenter.Tick(_state, _config, 10f, 1f);
            Assert.That(_state.Position, Is.EqualTo(held));
            Assert.That(_state.Rotation, Is.EqualTo(rotation));
            Assert.That(_state.CatchHoldElapsed, Is.EqualTo(0.5f));
            Assert.That(_state.HorizontalFieldOfView, Is.EqualTo(_config.HorizontalFieldOfView));
            Assert.That(_state.Roll, Is.Zero);
        }

        [Test]
        public void DefaultCatchBeatAndTimingEdgesOccurOnceUntilReset()
        {
            Assert.That(_config.CatchDistance, Is.EqualTo(1.2f));
            Assert.That(_config.CatchApproachSeconds, Is.EqualTo(0.15f));
            Assert.That(_config.CatchHoldSeconds, Is.EqualTo(1.4f));
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.PlayDeathSnap(_state, _config, Vector3.zero);
            int starts = 0, ends = 0;
            foreach (float dt in new[] { 0.15f, 0.7f, 0.7f, 10f })
            {
                bool started = _state.CatchHoldStarted, ended = _state.CatchHoldEnded;
                _presenter.Tick(_state, _config, dt, 1f);
                if (!started && _state.CatchHoldStarted) starts++;
                if (!ended && _state.CatchHoldEnded) ends++;
                if (_state.CatchHoldElapsed < 1.4f) Assert.That(ends, Is.Zero);
            }
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(ends, Is.EqualTo(1));
            _presenter.Reset(_state);
            _presenter.Tick(_state, _config, 10f, 1f);
            Assert.That(_state.CatchHoldStarted || _state.CatchHoldEnded, Is.False);
        }

        [Test]
        public void SnapDefaultsAndDurationValidationAllowZeroButNotNegative()
        {
            Assert.That(_config.LookBackYaw, Is.EqualTo(180f));
            Assert.That(_config.LookBackSeconds, Is.Zero);
            Assert.That(_config.LookForwardSeconds, Is.Zero);
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_lookBackSeconds").floatValue = -1f;
            serialized.FindProperty("_lookForwardSeconds").floatValue = -1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(_config.LookBackSeconds, Is.Zero);
            Assert.That(_config.LookForwardSeconds, Is.Zero);
        }

        [Test]
        public void ZeroCatchDurationsStillPublishStartBeforeEnd()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_catchApproachSeconds").floatValue = 0f;
            serialized.FindProperty("_catchHoldSeconds").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.PlayConsumed(_state, _config, _state.EyePosition);
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.CatchHoldStarted, Is.True);
            Assert.That(_state.CatchHoldEnded, Is.False);
            Assert.That(Vector3.Distance(_state.Position, _state.EyePosition), Is.EqualTo(_config.CatchDistance).Within(0.0001f));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.CatchHoldEnded, Is.True);
        }

        private PlayerMovementSample Sample(long tick, Vector3 velocity = default, Vector2 look = default,
            bool lookBack = false, MovementState movement = MovementState.Ground, float turnRate = 0f)
            => new PlayerMovementSample(default, tick, new Vector3(2f, 0f, 3f), velocity,
                new Vector3(2f, 1.6f, 3f), 30f, look, lookBack, movement, 0f, turnRate);
    }
}

