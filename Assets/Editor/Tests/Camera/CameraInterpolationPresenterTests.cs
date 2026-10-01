// ============================================================================
// CameraInterpolationPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies render-time sampling, identity resets and teleport snap boundaries.
//   Explicit clocks and managed config fields exercise production sample admission
//   without a Unity scene, native configuration allocation or a renderer.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Camera.
// KEY RESPONSIBILITIES:
//   - Protect one-step latency, duplicate rejection and no extrapolation.
//   - Cover lifecycle reset, discontinuities and independent traversal timestamps.
//   - Prove interpolation does not delay or reapply committed mouse-look pitch.
// DEPENDENCIES:
//   Core samples, Camera presenters/state, NUnit and managed reflection.
// USAGE NOTES:
//   Headless-safe. The uninitialized config is only a bag of explicitly supplied
//   managed fields; it is never passed to an engine API or destroyed as an asset.
// ============================================================================
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Camera;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Camera
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class CameraInterpolationPresenterTests
    {
        private const float Dt = 1f / 60f;
        private CameraDriverState state;
        private CameraDriverConfig config;
        private readonly CameraFeedbackPresenter feedback = new CameraFeedbackPresenter();

        [SetUp]
        public void Setup()
        {
            state = new CameraDriverState();
            config = (CameraDriverConfig)FormatterServices.GetUninitializedObject(typeof(CameraDriverConfig));
            Set("_positionSnapDistance", 3f);
            Set("_forwardPitchLimit", 85f);
        }

        [Test]
        public void FirstSampleSnapsThenInterpolatesPreviousToCurrentWithinOneStep()
        {
            Feed(1, Vector3.up, 1f);
            Assert.That(Position(1f), Is.EqualTo(Vector3.up));
            Feed(2, Vector3.up + Vector3.forward, 1f + Dt);
            Assert.That(Position(1f + Dt), Is.EqualTo(Vector3.up));
            Assert.That(Position(1f + Dt * 1.5f).z, Is.EqualTo(.5f).Within(.00001f));
            Assert.That(Position(1f + Dt * 2f).z, Is.EqualTo(1f).Within(.00001f));
            Assert.That(Position(100f).z, Is.EqualTo(1f), "No extrapolation while ticking is suspended.");
            Assert.That(Position(0f), Is.EqualTo(Vector3.up));
        }

        [Test]
        public void MultipleTicksWithoutRenderingRetainOnlyTheLastPair()
        {
            Feed(1, Vector3.zero, 1f);
            Feed(2, Vector3.right, 1f + Dt);
            Feed(3, Vector3.right * 2f, 1f + 2f * Dt);
            Assert.That(Position(1f + 2.5f * Dt).x, Is.EqualTo(1.5f).Within(.00002f));
        }

        [TestCase(2.999f, false)]
        [TestCase(3f, false)]
        [TestCase(3.001f, true)]
        public void ConfiguredThresholdSnapsOnlyJumpsOverItsBoundary(float distance, bool snap)
        {
            Feed(1, Vector3.zero, 1f);
            state.VaultActive = true; state.VaultHeight = state.RenderedVaultHeight = .1f;
            Feed(2, Vector3.forward * distance, 1f + Dt);
            Assert.That(Position(1f + Dt).z, Is.EqualTo(snap ? distance : 0f));
            Assert.That(state.VaultActive, Is.EqualTo(!snap));
            if (snap) Assert.That(state.VaultHeight + state.RenderedVaultHeight, Is.Zero);
        }

        [TestCase("respawn")]
        [TestCase("revival")]
        [TestCase("floor transition")]
        public void ExplicitLifecycleResetSnapsEvenASmallSameIdentityDiscontinuity(string boundary)
        {
            Feed(8, Vector3.zero, 1f);
            Feed(9, Vector3.right, 1f + Dt);
            feedback.Reset(state); // CameraManager.ResetView's production boundary.
            Feed(1, Vector3.right * 1.1f, 1f + 2f * Dt);
            Assert.That(Position(1f + 2f * Dt).x, Is.EqualTo(1.1f), boundary);
            Assert.That(state.VaultActive || state.VaultCompleting, Is.False);
        }

        [Test]
        public void IdentityReplacementSnapsAndStaleSamplesCannotOverwriteThePair()
        {
            Feed(2, Vector3.zero, 1f);
            Feed(3, Vector3.right, 1f + Dt);
            Feed(3, Vector3.right * 100f, 2f);
            Feed(2, Vector3.right * 100f, 2f);
            Assert.That(Position(1f + 1.5f * Dt).x, Is.EqualTo(.5f).Within(.00002f));
            Feed(1, Vector3.right * 1.1f, 2f, id: 2);
            Assert.That(Position(2f).x, Is.EqualTo(1.1f));
        }

        [Test]
        public void DeathLatchRejectsMovementAndResetAllowsRevivalWithoutAnOldPair()
        {
            Feed(1, Vector3.zero, 1f);
            Feed(2, Vector3.right, 1f + Dt);
            state.DeathSnapped = true;
            Feed(3, Vector3.right * 2f, 1f + 2f * Dt);
            Assert.That(state.MovementTick, Is.EqualTo(2));
            feedback.Reset(state);
            Feed(4, Vector3.right * 2f, 1f + 3f * Dt);
            Assert.That(Position(1f + 3f * Dt).x, Is.EqualTo(2f));
        }

        [Test]
        public void HeightAndMovementUseIndependentClocksEvenWhenProgressArrivesFirst()
        {
            Feed(1, Vector3.zero, 1f);
            CameraInterpolationPresenter.SetHeight(state, .1f, 1f + Dt, Dt);
            state.VaultActive = true;
            Feed(2, Vector3.right, 1f + Dt);
            Assert.That(CameraInterpolationPresenter.Height(state, 1f + 1.5f * Dt), Is.EqualTo(.05f).Within(.00001f));
            Assert.That(Position(1f + 1.5f * Dt).x, Is.EqualTo(.5f).Within(.00002f));
            CameraInterpolationPresenter.SetHeight(state, .2f, 1f + 2f * Dt, Dt);
            Assert.That(CameraInterpolationPresenter.Height(state, 1f + 2f * Dt), Is.EqualTo(.1f));
            Assert.That(CameraInterpolationPresenter.Height(state, 5f), Is.EqualTo(.2f));
        }

        [Test]
        public void PitchRemainsTickSteppedWithoutAdditionalLatencyOrRepeatedConsumption()
        {
            Feed(1, Vector3.zero, 1f, pitch: 2f);
            Feed(2, Vector3.right, 1f + Dt, pitch: 3f);
            Assert.That(state.Pitch, Is.EqualTo(-5f), "Look is applied immediately, before positional interpolation.");
            foreach (float fraction in new[] { 0f, .25f, .5f, 1f })
            {
                Position(1f + Dt * (1f + fraction));
                Assert.That(state.Pitch, Is.EqualTo(-5f));
            }
            Feed(2, Vector3.right, 1f + Dt, pitch: 3f);
            Assert.That(state.Pitch, Is.EqualTo(-5f));
        }

        [TestCase(float.NaN, 1f, Dt)]
        [TestCase(1f, float.PositiveInfinity, Dt)]
        [TestCase(1f, 1f, 0f)]
        [TestCase(1f, 1f, -1f)]
        [TestCase(1f, 1f, float.NaN)]
        public void InvalidClockOrUntimedCallUsesCurrentSample(float now, float fixedTime, float duration)
            => Assert.That(CameraInterpolationPresenter.Fraction(now, fixedTime, duration), Is.EqualTo(1f));

        [Test]
        public void ClockRewindSnapsRatherThanReplayingOldMotion()
        {
            Feed(1, Vector3.zero, 2f);
            Feed(2, Vector3.right, 1f);
            Assert.That(Position(1f), Is.EqualTo(Vector3.right));
        }

        private Vector3 Position(float now) => CameraInterpolationPresenter.Position(state, now);
        private void Feed(long tick, Vector3 eye, float time, int id = 1, float pitch = 0f)
            => feedback.SetMovement(state, config, new PlayerMovementSample(new EntityId(id), tick,
                eye, Vector3.zero, eye, 0f, new Vector2(0f, pitch), false, MovementState.Vault, 0f), time, Dt);
        private void Set(string field, float value)
            => typeof(CameraDriverConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
    }
}
