// ============================================================================
// AudioMixPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Exercises audible mixer decisions without depending on a running scene.
//   Explicit samples and time steps make interruption and reload behavior repeatable.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
//
// KEY RESPONSIBILITIES:
//   - Verify priority contention, interrupted fades, expiry and complete reset.
//   - Verify fractional critical-health thresholds, proximity and cadence remain finite and bounded.
//
// DEPENDENCIES:
//   - AudioMixPresenter and its owned state; Core MovementState; NUnit.
//
// USAGE NOTES:
//   - Edit Mode pure tests; these assert decisions, not speaker audibility.
//   - The coordinator performs listening and routed scene acceptance separately.
//
// ============================================================================

using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Audio;

namespace Worsen.Tests.Audio
{
    public sealed class AudioMixPresenterTests
    {
        private AudioMixPresenter _presenter;
        private AudioDriverState _state;
        private AudioMixSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _presenter = new AudioMixPresenter();
            _state = new AudioDriverState();
            _settings = new AudioMixSettings
            {
                LayerFadeSeconds = 0.3f, BreathMaximumGain = 0.45f, HunterMaximumGain = 0.8f,
                CriticalBreathGain = 0.55f, CriticalHealthFraction = 0.25f,
                SlowStepSeconds = 0.6f, FastStepSeconds = 0.28f, MinimumStepSpeed = 0.04f
            };
        }

        [Test]
        public void LowerPriorityAndRepeatedCueCannotInterruptActiveCue()
        {
            Assert.That(_presenter.TryCue(_state, 1, 80, 1f, 0.8f, 0.1f), Is.True);
            Assert.That(_presenter.TryCue(_state, 2, 20, 3f, 1f, 0.1f), Is.False);
            Assert.That(_presenter.TryCue(_state, 1, 100, 3f, 1f, 0.1f), Is.False);
            Assert.That(_state.ActiveCueKey, Is.EqualTo(1));
            Assert.That(_state.CueRemaining, Is.EqualTo(1f));
        }

        [Test]
        public void HigherPriorityCuePreservesInterruptedFadeThenReplacesIt()
        {
            _presenter.TryCue(_state, 1, 20, 1f, 0.8f, 0.1f);
            _presenter.Tick(_state, _settings, 0.02f);
            float priorGain = _state.CueGain;
            Assert.That(priorGain, Is.EqualTo(0.8f));
            Assert.That(_presenter.TryCue(_state, 2, 100, 1f, 0.9f, 0.1f), Is.True);
            Assert.That(_state.OutgoingCueGain, Is.EqualTo(priorGain));
            Assert.That(_state.CueGain, Is.EqualTo(0.9f));
            _presenter.Tick(_state, _settings, 0.02f);
            Assert.That(_state.CueGain, Is.GreaterThan(0f));
            Assert.That(_state.OutgoingCueGain, Is.LessThan(priorGain));
        }

        [Test]
        public void PositiveCueShorterThanOneFrameHasAudibleOnsetBeforeTick()
        {
            Assert.That(_presenter.TryCue(_state, 1, 80, 0.005f, 0.75f, 0.08f), Is.True);
            Assert.That(_state.CueGain, Is.EqualTo(0.75f));
            _presenter.Tick(_state, _settings, 0.016f);
            Assert.That(_state.CueRemaining, Is.Zero);
            Assert.That(_state.CueGain, Is.InRange(0f, 0.75f));
        }

        [Test]
        public void CueExpiryReleasesPriorityForLaterPresence()
        {
            _presenter.TryCue(_state, 1, 100, 0.2f, 1f, 0.1f);
            _presenter.Tick(_state, _settings, 0.1f);
            _presenter.Tick(_state, _settings, 0.2f);
            Assert.That(_state.ActiveCueKey, Is.EqualTo(-1));
            Assert.That(_state.CueGain, Is.Zero);
            Assert.That(_presenter.TryCue(_state, 2, 1, 1f, 1f, 0.1f), Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void UnavailableDurationCannotStartCue(float seconds)
        {
            Assert.That(_presenter.TryCue(_state, 1, 10, seconds, 1f, 0.1f), Is.False);
            Assert.That(_state.ActiveCueKey, Is.EqualTo(-1));
        }

        [Test]
        public void ProximityRaisesBothLayersWithoutDistanceSensing()
        {
            _presenter.SetProximity(_state, 0.5f);
            _presenter.Tick(_state, _settings, 1f);
            Assert.That(_state.BreathGain, Is.EqualTo(0.225f).Within(0.0001f));
            Assert.That(_state.HunterGain, Is.EqualTo(0.2f).Within(0.0001f));
            _presenter.SetProximity(_state, 1f);
            _presenter.Tick(_state, _settings, 1f);
            Assert.That(_state.BreathGain, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(_state.HunterGain, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [TestCase(100, 0f)]
        [TestCase(50, 0f)]
        [TestCase(25, 0.55f)]
        [TestCase(0, 0f)]
        public void CriticalHealthAddsBreathingWhileDeathSilencesIt(int health, float expected)
        {
            _presenter.SetInjury(_state, health, 100);
            _presenter.Tick(_state, _settings, 1f);
            Assert.That(_state.BreathGain, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(25.1f, 100f, 0f)]
        [TestCase(24.9f, 100f, 0.55f)]
        [TestCase(0.125f, 100f, 0.55f)]
        [TestCase(25.125f, 100.5f, 0.55f)]
        [TestCase(25.126f, 100.5f, 0f)]
        public void FractionalHealthIsPreservedAtTheCriticalBoundary(float health, float maximum, float expected)
        {
            _presenter.SetInjury(_state, health, maximum);
            Assert.That(_state.CurrentHealth, Is.EqualTo(health));
            Assert.That(_state.MaxHealth, Is.EqualTo(maximum));
            _presenter.Tick(_state, _settings, 1f);
            Assert.That(_state.BreathGain, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(float.NaN, 100f)]
        [TestCase(float.PositiveInfinity, 100f)]
        [TestCase(25f, float.NaN)]
        [TestCase(25f, float.PositiveInfinity)]
        public void NonfiniteHealthDoesNotPoisonLayerGains(float health, float maximum)
        {
            _presenter.SetInjury(_state, health, maximum);
            _presenter.SetProximity(_state, 1f);
            _presenter.Tick(_state, _settings, 1f);
            Assert.That(_state.CurrentHealth, Is.Zero);
            Assert.That(_state.BreathGain, Is.Zero);
            Assert.That(_state.HunterGain, Is.Zero);
        }

        [Test]
        public void ChangedLayerTargetReversesAnInterruptedFade()
        {
            _presenter.SetProximity(_state, 1f);
            _presenter.Tick(_state, _settings, 0.05f);
            float rising = _state.HunterGain;
            _presenter.SetProximity(_state, 0f);
            _presenter.Tick(_state, _settings, 0.025f);
            Assert.That(_state.HunterGain, Is.GreaterThan(0f).And.LessThan(rising));
        }

        [Test]
        public void InvalidInputsAndInvalidTuningNeverProduceNonfiniteGains()
        {
            _presenter.SetProximity(_state, float.NaN);
            _presenter.SetSpeedNormalized(_state, float.PositiveInfinity);
            _settings.LayerFadeSeconds = float.NaN;
            _settings.BreathMaximumGain = float.PositiveInfinity;
            _settings.HunterMaximumGain = -100f;
            _presenter.Tick(_state, _settings, float.PositiveInfinity);
            _presenter.Tick(_state, _settings, 1f);
            Assert.That(_state.Proximity, Is.Zero);
            Assert.That(_state.SpeedNormalized, Is.Zero);
            Assert.That(_state.BreathGain, Is.InRange(0f, 1f));
            Assert.That(_state.HunterGain, Is.InRange(0f, 1f));
        }

        [Test]
        public void GroundSpeedProducesCadencedStepsAndAirSuppressesThem()
        {
            _presenter.SetSpeedNormalized(_state, 1f);
            Assert.That(_presenter.Tick(_state, _settings, 0.01f), Is.True);
            Assert.That(_presenter.Tick(_state, _settings, 0.1f), Is.False);
            Assert.That(_presenter.Tick(_state, _settings, 0.2f), Is.True);
            _presenter.SetMovementState(_state, MovementState.Air);
            Assert.That(_presenter.Tick(_state, _settings, 1f), Is.False);
            _presenter.SetMovementState(_state, MovementState.Ground);
            _presenter.SetSpeedNormalized(_state, 0f);
            Assert.That(_presenter.Tick(_state, _settings, 1f), Is.False);
        }

        [Test]
        public void ZeroAndNegativeTimeDoNotAdvanceOrProduceSteps()
        {
            _presenter.TryCue(_state, 1, 20, 1f, 0.8f, 0.1f);
            _presenter.SetSpeedNormalized(_state, 1f);
            Assert.That(_presenter.Tick(_state, _settings, -1f), Is.False);
            Assert.That(_presenter.Tick(_state, _settings, 0f), Is.False);
            Assert.That(_state.CueRemaining, Is.EqualTo(1f));
        }

        [Test]
        public void ResetClearsActiveCuesProximityInjuryAndCadence()
        {
            _presenter.TryCue(_state, 1, 20, 1f, 1f, 0.1f);
            _presenter.SetInjury(_state, 25, 100);
            _presenter.SetProximity(_state, 1f);
            _presenter.SetSpeedNormalized(_state, 1f);
            _presenter.Tick(_state, _settings, 0.1f);
            _presenter.Reset(_state);
            Assert.That(_state.ActiveCueKey, Is.EqualTo(-1));
            Assert.That(_state.BreathGain, Is.Zero);
            Assert.That(_state.HunterGain, Is.Zero);
            Assert.That(_state.SpeedNormalized, Is.Zero);
            Assert.That(_state.FootstepRemaining, Is.Zero);
            Assert.That(_state.CurrentHealth, Is.EqualTo(100));
            Assert.That(_presenter.Tick(_state, _settings, 1f), Is.False);
        }
    }
}
