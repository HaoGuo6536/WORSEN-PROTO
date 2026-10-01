// ============================================================================
// PostFXPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Tests effect composition and release timing without creating a live rendering volume.
//   The suite distinguishes pure timing evidence from visual or human acceptance.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Verify hunter and hand death paths remain visible with no terminal fade.
//   - Cover bounded proximity, fractional injury and independent intrusion/blur expiry.
//   - Verify look-back release edges, comfort toggles and reset isolation.
//   - Assert constant degradation, budget-denied subtle intrusion and timed blindness.
//   - Verify transient hits and critical health feed tape without replacing existing effects.
//
// DEPENDENCIES:
//   - PostFX presentation math, NUnit and editor config serialization only.
//
// USAGE NOTES:
//   - Edit Mode; temporary config is destroyed after each test.
//   - No renderer or scene is needed; real volume integration remains a separate gate.
//
// ============================================================================

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.PostFX;

namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXPresenterTests
    {
        private PostFXDriverConfig _config;
        private PostFXDriverState _state;
        private PostFXPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            _state = new PostFXDriverState();
            _presenter = new PostFXPresenter();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [TestCase(-1f, 0f)]
        [TestCase(0.5f, 0.125f)]
        [TestCase(10f, 0.25f)]
        [TestCase(float.NaN, 0f)]
        public void ProximityOnlyMapsTheSuppliedCloseness(float closeness, float expectedChromatic)
        {
            _presenter.SetProximity(_state, closeness);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Chromatic, Is.EqualTo(_config.BaselineChromatic + expectedChromatic).Within(0.00001f));
            Assert.That(_state.Distortion, Is.InRange(-0.12f, 0f));
        }

        [TestCase(100f, 100f, 0f)]
        [TestCase(50f, 100f, 0.5f)]
        [TestCase(25f, 100f, 0.75f)]
        [TestCase(0f, 100f, 1f)]
        [TestCase(99.5f, 100f, 0.005f)]
        [TestCase(100f, 0f, 0f)]
        public void InjuryPreservesArbitraryDamageAndDoesNotAddHealthRules(float health, float maximum, float injury)
        {
            _presenter.SetInjury(_state, health, maximum);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Injury, Is.EqualTo(injury).Within(0.00001f));
            Assert.That(_state.Vignette, Is.EqualTo(_config.FrameVignette + injury * 0.45f).Within(0.00001f));
        }

        [Test]
        public void BlurExpiresAtPointOneSecondsWithoutClearingOtherEffects()
        {
            _presenter.SetProximity(_state, 1f);
            _presenter.SetInjury(_state, 50f, 100f);
            _presenter.PlayIntrusion(_state, 2f);
            _presenter.PlayReacquireBlur(_state, _config);
            _presenter.Tick(_state, _config, 0.05f);
            Assert.That(_state.Blur, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(_state.Saturation, Is.EqualTo(-70f));
            _presenter.Tick(_state, _config, 0.05f);
            Assert.That(_state.Blur, Is.Zero);
            Assert.That(_state.Chromatic, Is.EqualTo(_config.BaselineChromatic + 0.25f));
            Assert.That(_state.Vignette, Is.EqualTo(_config.FrameVignette + 0.225f).Within(0.00001f));
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain + 0.5f));
        }

        [Test]
        public void LookBackReleaseTriggersExactlyOneBlurEnvelope()
        {
            _presenter.SetLookBack(_state, _config, false);
            Assert.That(_state.BlurRemaining, Is.Zero);
            _presenter.SetLookBack(_state, _config, true);
            _presenter.SetLookBack(_state, _config, false);
            _presenter.Tick(_state, _config, 0.05f);
            _presenter.SetLookBack(_state, _config, false);
            _presenter.Tick(_state, _config, 0.05f);
            Assert.That(_state.Blur, Is.Zero);
        }

        [Test]
        public void DisablingBlurCancelsAnActiveEnvelope()
        {
            _presenter.PlayReacquireBlur(_state, _config);
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_reacquireBlurEnabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.BlurRemaining, Is.Zero);
            Assert.That(_state.Blur, Is.Zero);
        }

        [Test]
        public void IntrusionExpiresAtSuppliedDurationAndShortRetriggerDoesNotTruncateIt()
        {
            _presenter.PlayIntrusion(_state, 2f);
            _presenter.Tick(_state, _config, 1f);
            _presenter.PlayIntrusion(_state, 0.2f);
            _presenter.Tick(_state, _config, 0.5f);
            Assert.That(_state.Saturation, Is.EqualTo(-70f));
            _presenter.Tick(_state, _config, 0.5f);
            Assert.That(_state.Saturation, Is.Zero);
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain));
        }

        [Test]
        public void ResetClearsAllEffectsAndTheRememberedReleaseEdge()
        {
            _presenter.SetProximity(_state, 1f);
            _presenter.SetInjury(_state, 25f, 100f);
            _presenter.SetLookBack(_state, _config, true);
            _presenter.PlayIntrusion(_state, 2f);
            _presenter.PlayReacquireBlur(_state, _config);
            _presenter.Reset(_state);
            _presenter.SetLookBack(_state, _config, false);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Chromatic, Is.EqualTo(_config.BaselineChromatic));
            Assert.That(_state.Vignette, Is.EqualTo(_config.FrameVignette));
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain));
            Assert.That(_state.Blur, Is.Zero);
            Assert.That(_state.LookBack, Is.False);
        }

        [Test]
        public void ConsumptionNeverFadesAndPreservesOtherFeedback()
        {
            _presenter.PlayConsumed(_state, 0.9f);
            _presenter.Tick(_state, _config, 0.09f);
            Assert.That(_state.Blackout, Is.Zero);
            _presenter.Tick(_state, _config, 0.36f);
            Assert.That(_state.Blackout, Is.Zero);
            _presenter.PlayConsumed(_state, 2f);
            Assert.That(_state.ConsumptionElapsed, Is.EqualTo(0.45f).Within(0.0001f));
            _presenter.Tick(_state, _config, 0.5f);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
            Assert.That(_state.Blackout, Is.Zero);
            _presenter.SetInjury(_state, 100f, 100f);
            _presenter.PlayIntrusion(_state, 2f);
            _presenter.PlayReacquireBlur(_state, _config);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain + _config.IntrusionGrain));
            Assert.That(_state.Blur, Is.EqualTo(1f));
            Assert.That(_state.Exposure, Is.Zero);
            _presenter.Reset(_state);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Consumed, Is.False);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
            Assert.That(_state.Exposure + _state.Blackout, Is.Zero);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void OrdinaryInjuryAndInvalidConsumptionDoNotBlackOut(float duration)
        {
            _presenter.SetInjury(_state, 0f, 100f);
            _presenter.PlayIntrusion(_state, 1f);
            _presenter.PlayConsumed(_state, duration);
            _presenter.Tick(_state, _config, 1f);
            Assert.That(_state.Consumed, Is.False);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
            Assert.That(_state.Blackout, Is.Zero);
        }

        [Test]
        public void HunterDeathInjuryNeverProducesFadeThroughoutCatch()
        {
            _presenter.SetInjury(_state, 0f, 100f);
            for (int step = 0; step < 120; step++)
            {
                _presenter.Tick(_state, _config, 1f / 60f);
                Assert.That(_state.Blackout, Is.Zero);
                Assert.That(_state.Exposure, Is.Zero);
                Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
                Assert.That(_state.Vignette, Is.EqualTo(_config.FrameVignette + _config.InjuryVignette));
            }
        }

        [Test]
        public void NoEventsApplyConstantDegradation()
        {
            _presenter.Tick(_state, _config, 10f);
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain).And.GreaterThan(0f));
            Assert.That(_state.Chromatic, Is.EqualTo(_config.BaselineChromatic).And.GreaterThan(0f));
            Assert.That(_state.Vignette, Is.EqualTo(_config.FrameVignette).And.GreaterThan(0f));
            Assert.That(_state.Blackout, Is.Zero);
        }

        [Test]
        public void SubtleIntrusionCannotExtendLoudEnvelopeAndReturnsToBaseline()
        {
            _presenter.PlayIntrusion(_state, 1f, true);
            _presenter.PlayIntrusion(_state, 2f, false);
            _presenter.Tick(_state, _config, 1f);
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain + _config.IntrusionGrain * _config.SubtleIntrusionMultiplier));
            Assert.That(_state.Saturation, Is.EqualTo(-_config.IntrusionDesaturation * _config.SubtleIntrusionMultiplier));
            _presenter.Tick(_state, _config, 1f);
            Assert.That(_state.Grain, Is.EqualTo(_config.BaselineGrain));
            Assert.That(_state.Saturation, Is.Zero);
        }

        [Test]
        public void BlindnessIsDefaultOffTimedCancellableAndNeverHidesHandCatch()
        {
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
            _presenter.SetBlindness(_state, 2f);
            _presenter.Tick(_state, _config, 1f);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.black));
            _presenter.Tick(_state, _config, 1f);
            Assert.That(_state.SceneTint, Is.EqualTo(Color.white));
            _presenter.SetBlindness(_state, 2f);
            _presenter.SetBlindness(_state, 0f);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Blackout, Is.Zero);
            _presenter.SetBlindness(_state, 2f);
            _presenter.PlayConsumed(_state, 0.9f);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Blackout, Is.Zero);
            _presenter.Reset(_state);
            Assert.That(_state.BlindnessRemaining, Is.Zero);
            _presenter.SetBlindness(_state, 2f);
            _presenter.SetInjury(_state, 0f, 100f);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Blackout, Is.Zero, "Hunter catches remain visible too.");
        }

        [Test]
        public void HitTapeRelaxesWhileCriticalHealthPersistsAndResetRearmsIt()
        {
            _presenter.SetInjury(_state, 50f, 100f);
            _presenter.Tick(_state, _config, _config.TapeRiseSeconds);
            Assert.That(_state.Frame.Tape.x, Is.GreaterThan(0f));
            _presenter.Tick(_state, _config, _config.TapeHitSeconds);
            _presenter.Tick(_state, _config, _config.TapeRelaxSeconds);
            Assert.That(_state.Frame.Tape.x, Is.Zero);
            Assert.That(_state.Vignette, Is.EqualTo(_config.FrameVignette + .5f * _config.InjuryVignette));
            _presenter.SetInjury(_state, 25f, 100f);
            _presenter.Tick(_state, _config, 5f);
            _presenter.Tick(_state, _config, 5f);
            Assert.That(_state.Frame.Degradation, Is.EqualTo(.75f));
            _presenter.Reset(_state);
            _presenter.Tick(_state, _config, 0f);
            Assert.That(_state.Frame.HitWeight + _state.Frame.Degradation, Is.Zero);
            Assert.That(_state.Frame.Lens.x, Is.EqualTo(_config.CamcorderCorners));
        }

        [Test]
        public void InvalidTimeAndDurationNeverCreateNonFiniteOutputs()
        {
            _presenter.PlayIntrusion(_state, float.PositiveInfinity);
            _presenter.PlayReacquireBlur(_state, _config);
            _presenter.Tick(_state, _config, float.NaN);
            Assert.That(_state.IntrusionRemaining, Is.Zero);
            Assert.That(_state.Blur, Is.EqualTo(1f));
        }
    }
}

