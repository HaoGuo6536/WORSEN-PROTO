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
//   - Cover bounded proximity, fractional injury and independent intrusion/blur expiry.
//   - Verify look-back release edges, comfort toggles and reset isolation.
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
            Assert.That(_state.Chromatic, Is.EqualTo(expectedChromatic).Within(0.00001f));
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
            Assert.That(_state.Vignette, Is.EqualTo(injury * 0.45f).Within(0.00001f));
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
            Assert.That(_state.Chromatic, Is.EqualTo(0.25f));
            Assert.That(_state.Vignette, Is.EqualTo(0.225f).Within(0.00001f));
            Assert.That(_state.Grain, Is.EqualTo(0.5f));
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
            Assert.That(_state.Grain, Is.Zero);
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
            Assert.That(_state.Chromatic + _state.Vignette + _state.Grain + _state.Blur, Is.Zero);
            Assert.That(_state.LookBack, Is.False);
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

