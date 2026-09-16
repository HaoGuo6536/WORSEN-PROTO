// ============================================================================
// CameraFeedbackPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies first-person presentation against explicit samples and elapsed time.
//   These tests protect the M4 view contract without relying on a live Cinemachine rig.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Camera.
//
// KEY RESPONSIBILITIES:
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

        [Test]
        public void LookBackReaches160AndReturnsAtDeclaredEndpoints()
        {
            _presenter.SetLookBack(_state, _config, true);
            _presenter.Tick(_state, _config, 0.06f, 1f);
            Assert.That(_state.LookYaw, Is.EqualTo(80f).Within(0.0001f));
            _presenter.Tick(_state, _config, 0.06f, 1f);
            Assert.That(_state.LookYaw, Is.EqualTo(160f).Within(0.0001f));
            _presenter.SetLookBack(_state, _config, false);
            _presenter.Tick(_state, _config, 0.15f, 1f);
            Assert.That(_state.LookYaw, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ReversingLookBackStartsFromTheCurrentView()
        {
            _presenter.SetLookBack(_state, _config, true);
            _presenter.Tick(_state, _config, 0.06f, 1f);
            _presenter.SetLookBack(_state, _config, false);
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.LookYaw, Is.EqualTo(80f).Within(0.0001f));
            _presenter.Tick(_state, _config, 0.15f, 1f);
            Assert.That(_state.LookYaw, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void DuplicateSamplesNeverConsumeHeadLookTwice()
        {
            var sample = Sample(8, look: new Vector2(5f, 6f), lookBack: true);
            _presenter.SetMovement(_state, _config, sample);
            _presenter.SetMovement(_state, _config, sample);
            _presenter.Tick(_state, _config, 0.12f, 1f);
            Assert.That(_state.Pitch, Is.EqualTo(-6f));
            Assert.That(_state.HeadYaw, Is.EqualTo(5f));
            Assert.That(_state.HeadingDegrees, Is.EqualTo(30f));
            Assert.That(_state.Position, Is.EqualTo(new Vector3(2f, 1.6f, 3f)));
        }

        [Test]
        public void LookBackBoundsPitchAndHeadYawWhileForwardYawComesFromBody()
        {
            _presenter.SetMovement(_state, _config, Sample(1, look: new Vector2(90f, 90f)));
            Assert.That(_state.Pitch, Is.EqualTo(-85f));
            Assert.That(_state.HeadYaw, Is.Zero);
            _presenter.SetMovement(_state, _config, Sample(2, look: new Vector2(90f, 0f), lookBack: true));
            Assert.That(_state.Pitch, Is.EqualTo(-20f));
            Assert.That(_state.HeadYaw, Is.EqualTo(20f));
        }

        [Test]
        public void ReboundIsASeparateSuccessfulFactAndTakesPriorityOverSlideRoll()
        {
            _presenter.SetMovement(_state, _config, Sample(1, movement: MovementState.Slide));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.EqualTo(6f));
            _presenter.PlayTraversal(_state, new PlayerTraversalFact(default, 1, TraversalKind.Rebound, false, Vector3.left, 0f));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.EqualTo(6f));
            _presenter.PlayTraversal(_state, new PlayerTraversalFact(default, 2, TraversalKind.Rebound, true, Vector3.left, 0f));
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.EqualTo(-10f));
            _presenter.Tick(_state, _config, 0.25f, 1f);
            _presenter.Tick(_state, _config, 0f, 1f);
            Assert.That(_state.Roll, Is.EqualTo(6f));
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
        public void DeathSnapFacesKillerAndResetClearsStaleSequences()
        {
            _presenter.SetMovement(_state, _config, Sample(1));
            _presenter.PlayDeathSnap(_state, _state.EyePosition + Vector3.left * 3f);
            _presenter.Tick(_state, _config, 0.1f, 1f);
            Assert.That(Vector3.Dot(_state.Rotation * Vector3.forward, Vector3.left), Is.GreaterThan(0.999f));
            _presenter.PlayDetectionBeat(_state);
            Assert.That(_state.DetectionElapsed, Is.LessThan(0f));
            _presenter.Reset(_state);
            Assert.That(_state.DeathSnapped, Is.False);
            Assert.That(_state.HasMovement, Is.False);
            Assert.That(_state.LookBack, Is.False);
            Assert.That(_state.TraversalTick, Is.EqualTo(-1));
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

        private PlayerMovementSample Sample(long tick, Vector3 velocity = default, Vector2 look = default,
            bool lookBack = false, MovementState movement = MovementState.Ground)
            => new PlayerMovementSample(default, tick, new Vector3(2f, 0f, 3f), velocity,
                new Vector3(2f, 1.6f, 3f), 30f, look, lookBack, movement, 0f);
    }
}

