// ============================================================================
// PostFXBlindnessFeedbackTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the production blindness composition with supplied durations and time.
//   Managed configuration fields allow headless verification of non-black output,
//   easing and coexistence; native defaults and rendered visibility are separate gates.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Protect the darkness safety ceiling, onset and within-duration eased recovery.
//   - Verify independent red damage, low-health heartbeat, lens and blur composition.
//   - Verify retriggers, catalogue removal, cleanse, Mirror Skin and catch/reset boundaries.
// DEPENDENCIES:
//   Core, PostFX, NUnit and managed reflection only.
// USAGE NOTES:
//   Headless-safe: the uninitialized config holds explicitly populated managed fields.
//   Never pass this config to an engine API or destroy it as a native asset.
// ============================================================================
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.PostFX;

namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXBlindnessFeedbackTests
    {
        private PostFXDriverConfig config;
        private PostFXDriverState state;
        private readonly PostFXPresenter presenter = new PostFXPresenter();

        [SetUp] public void Setup()
        {
            config = (PostFXDriverConfig)FormatterServices.GetUninitializedObject(typeof(PostFXDriverConfig));
            Set("_blindnessDarkness", .85f); Set("_blindnessOnsetSeconds", .08f);
            Set("_blindnessRecoverySeconds", .65f); Set("_blindnessVignette", .75f);
            Set("_blindnessVignetteRadius", 1f); Set("_blindnessVignetteSoftness", .6f);
            Set("_blindnessBlurRadius", 1.5f); Set("_blindnessEdgeBlurPixels", 4f);
            Set("_blindnessEffectIds", new[] { "blinded" }); Set("_mirrorSkinDurationMultiplier", .5f);
            Set("_blindTrapSeconds", 2.5f);
            Set("_damageVignettePeak", .5f); Set("_damageFullStrengthHealthFraction", .5f);
            Set("_damageFadeSeconds", 1.25f); Set("_damageVignetteSmoothness", .3f);
            Set("_damageVignetteColor", new Color(1f, .015f, .01f, 1f));
            Set("_lowHealthFraction", .35f); Set("_lowHealthVignette", .25f); Set("_lowHealthPulseMultiplier", .5f);
            Set("_camcorderEnabled", true); Set("_camcorderCorners", .22f);
            Set("_camcorderCornerRadius", .25f); Set("_camcorderCornerSoftness", .2f);
            Set("_camcorderEdgeBlurPixels", 1.25f); Set("_camcorderEdgeStart", .65f);
            Set("_reacquireBlurEnabled", true); Set("_reacquireBlurSeconds", .1f); Set("_blurRadius", 1f);
            state = new PostFXDriverState(); presenter.SetInjury(state, 100f, 100f);
        }
        private void Set(string name, object value) => typeof(PostFXDriverConfig)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        private void Tick(float dt) => presenter.Tick(state, config, dt);
        private void Effects(params string[] ids) => presenter.SetActiveEffects(state,
            new ActiveEffects(System.Array.ConvertAll(ids, id => new ActiveEffect(new EffectId(id), EffectKind.Curse, 1))));

        [TestCase(.85f, .85f)] [TestCase(1f, .88f)] [TestCase(100f, .88f)]
        [TestCase(-1f, 0f)] [TestCase(float.NaN, 0f)] [TestCase(float.PositiveInfinity, 0f)]
        public void LegacyOrMalformedDarknessNeverProducesBlack(float requested, float expected)
        {
            Set("_blindnessDarkness", requested); presenter.SetBlindness(state, 3f, config); Tick(.08f);
            Assert.That(PostFXDriverConfig.MaximumBlindnessDarkness, Is.LessThan(1f));
            Assert.That(state.Blackout, Is.EqualTo(expected).Within(.00001f).And.LessThan(1f));
            Assert.That(state.SceneTint.r, Is.GreaterThan(0f));
            // Independently decode the sRGB filter just as URP's grading pass does.
            double transmission = System.Math.Pow((state.SceneTint.r + .055d) / 1.055d, 2.4d);
            Assert.That(transmission, Is.EqualTo(1f - expected).Within(.00001d));
            Assert.That(state.SceneTint.g, Is.EqualTo(state.SceneTint.r));
            Assert.That(state.SceneTint.b, Is.EqualTo(state.SceneTint.r));
            Assert.That(state.Exposure, Is.Zero);
        }

        [TestCase(0f, 0f)] [TestCase(.02f, .15625f)] [TestCase(.04f, .5f)]
        [TestCase(.06f, .84375f)] [TestCase(.08f, 1f)]
        public void FastOnsetUsesSuppliedTimeAndSmoothStep(float seconds, float weight)
        {
            presenter.SetBlindness(state, 3f, config); Tick(seconds);
            Assert.That(state.BlindnessWeight, Is.EqualTo(weight).Within(.00001f));
            Assert.That(state.Blackout, Is.EqualTo(.85f * weight).Within(.00001f));
            Assert.That(state.BlindnessRemaining, Is.EqualTo(3f - seconds).Within(.00001f));
        }

        [TestCase(0f, 1f)] [TestCase(.1625f, .84375f)] [TestCase(.325f, .5f)]
        [TestCase(.4875f, .15625f)] [TestCase(.65f, 0f)]
        public void RecoveryIsEasedInsideOriginalDuration(float recoveryElapsed, float weight)
        {
            presenter.SetBlindness(state, 3f, config); Tick(2.35f); Tick(recoveryElapsed);
            Assert.That(state.BlindnessWeight, Is.EqualTo(weight).Within(.00001f));
            Assert.That(state.Blackout, Is.EqualTo(.85f * weight).Within(.00001f));
            if (weight == 0f) Assert.That(state.SceneTint, Is.EqualTo(Color.white));
        }

        [TestCase(3f)] [TestCase(2.5f)] [TestCase(.04f)]
        public void TimedBlindnessHasNoTailOrExtensionAndIsFramePartitionIndependent(float duration)
        {
            presenter.SetBlindness(state, duration, config); Tick(duration * .9f);
            float single = state.Blackout;
            presenter.Reset(state); presenter.SetBlindness(state, duration, config);
            for (int i = 0; i < 90; i++) Tick(duration / 100f);
            Assert.That(state.Blackout, Is.EqualTo(single).Within(.00001f));
            Tick(duration); Assert.That(state.Blackout + state.BlindnessRemaining + state.BlindnessWeight, Is.Zero);
            Assert.That(state.Frame.Lens.x, Is.EqualTo(.22f)); Assert.That(state.Blur, Is.Zero);
        }

        [Test] public void HeavyLensAndBlurAreIndependentOfTheRedDamageBorder()
        {
            presenter.SetBlindness(state, 3f, config); Tick(.08f);
            Assert.That(state.Frame.Lens, Is.EqualTo(new Vector4(.75f, 1f, .6f, 4f)));
            Assert.That(state.Blur, Is.EqualTo(1f)); Assert.That(state.BlurRadius, Is.EqualTo(1.5f));
            Assert.That(state.Vignette, Is.Zero);
            presenter.SetInjury(state, 75f, 100f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(.25f)); Assert.That(state.VignetteColor, Is.EqualTo(config.DamageVignetteColor));
            Assert.That(state.Blackout, Is.EqualTo(.85f)); Assert.That(state.Frame.Lens.x, Is.EqualTo(.75f));
            Tick(1.25f); Assert.That(state.Vignette, Is.Zero); Assert.That(state.Blackout, Is.EqualTo(.85f));
            presenter.SetInjury(state, 100f, 100f); Tick(0f); Assert.That(state.Blackout, Is.EqualTo(.85f));
        }

        [Test] public void BlindnessExpiryDoesNotCancelAFreshHitOrHeartbeat()
        {
            presenter.SetBlindness(state, 3f, config); Tick(2.675f);
            presenter.SetInjury(state, 25f, 100f); presenter.SetHeartbeatEnvelope(state, 1f); Tick(.325f);
            Assert.That(state.Blackout, Is.Zero); Assert.That(state.SceneTint, Is.EqualTo(Color.white));
            Assert.That(state.Vignette, Is.EqualTo(.5f * (1f - .325f / 1.25f)).Within(.00001f));
            Tick(1.25f); float pulsing = state.Vignette;
            Assert.That(pulsing, Is.GreaterThan(0f)); presenter.SetHeartbeatEnvelope(state, 0f); Tick(0f);
            Assert.That(pulsing, Is.EqualTo(state.Vignette * 1.5f).Within(.00001f));
        }

        [Test] public void BlurComfortToggleAndCamcorderDisableCannotEraseBlindnessPenalty()
        {
            Set("_camcorderEnabled", false); presenter.SetBlindness(state, 3f, config); Tick(.08f);
            presenter.PlayReacquireBlur(state, config); presenter.SetReacquireBlurEnabled(state, false);
            Assert.That(state.BlurRemaining, Is.Zero); Assert.That(state.Blur, Is.EqualTo(1f)); Tick(0f);
            Assert.That(state.Frame.Lens, Is.EqualTo(new Vector4(.75f, 1f, .6f, 4f)));
            Assert.That(state.Blackout, Is.EqualTo(.85f)); Assert.That(state.BlurRadius, Is.EqualTo(1.5f));
            Tick(3f); Assert.That(state.Frame.Lens, Is.EqualTo(Vector4.zero)); Assert.That(state.Blur, Is.Zero);
        }

        [Test] public void ShortRepeatDoesNotFlashToClearAndMirrorSkinScalesOnlyDuration()
        {
            Effects("mirror-skin"); presenter.SetBlindness(state, 3f, config);
            Assert.That(state.BlindnessRemaining, Is.EqualTo(1.5f)); Tick(.08f);
            float before = state.Blackout; presenter.SetBlindness(state, 2.5f, config); Tick(0f);
            Assert.That(state.BlindnessRemaining, Is.EqualTo(1.25f)); Assert.That(state.Blackout, Is.EqualTo(before));
            Tick(1.25f); Assert.That(state.Blackout, Is.Zero);
        }

        [Test] public void CatalogueRemovalEasesOutAndCleanseClearsImmediatelyWithoutErasingDamage()
        {
            Effects("Blinded"); Tick(.08f); Assert.That(state.Blackout, Is.Zero);
            Effects("blinded"); Tick(.08f); Assert.That(state.Blackout, Is.EqualTo(.85f));
            Effects(); Tick(.325f); Assert.That(state.Blackout, Is.EqualTo(.425f).Within(.00001f));
            Tick(.325f); Assert.That(state.Blackout, Is.Zero);
            Effects("blinded"); presenter.SetBlindness(state, 3f, config); Tick(.08f);
            presenter.SetInjury(state, 75f, 100f); Tick(0f);
            presenter.SetBlindness(state, 0f, config); Tick(0f);
            Assert.That(state.Blackout + state.BlindnessWeight + state.Blur, Is.Zero);
            Assert.That(state.Vignette, Is.EqualTo(.25f)); Assert.That(presenter.HasBlindness(state, config), Is.False);
            Effects(); Effects("blinded"); Tick(.08f); Assert.That(state.Blackout, Is.EqualTo(.85f));
        }

        [TestCase("hand")] [TestCase("hunter")] [TestCase("reset")]
        public void CatchAndResetCannotRetainBlindnessShading(string boundary)
        {
            presenter.SetBlindness(state, 3f, config); Tick(.08f);
            if (boundary == "hand") presenter.PlayConsumed(state, .9f);
            if (boundary == "hunter") presenter.SetInjury(state, 0f, 100f);
            if (boundary == "reset") presenter.Reset(state);
            Tick(0f); Assert.That(state.Blackout + state.BlindnessWeight + state.Blur, Is.Zero);
            Assert.That(state.SceneTint, Is.EqualTo(Color.white)); Assert.That(state.Frame.Lens.x, Is.EqualTo(.22f));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidTimeCannotAdvanceBlindness(float value)
        {
            presenter.SetBlindness(state, 3f, config); Tick(.04f); Tick(value);
            Assert.That(state.Blackout, Is.EqualTo(.425f).Within(.00001f));
            Assert.That(state.BlindnessRemaining, Is.EqualTo(2.96f).Within(.00001f));
        }

        [Test] public void GlimpseIsSuppressedEvenAtTheZeroTimeOnsetEdge()
        {
            Effects("glimpse"); presenter.SetLookBack(state, config, true); presenter.SetHunterRim(state, .08f);
            presenter.SetBlindness(state, 3f, config); Tick(0f);
            Assert.That(state.Blackout, Is.Zero); Assert.That(presenter.OutlineStrength(state), Is.Zero);
        }

        [Test] public void PreviewSamplesExportProductionParametersWithNonzeroSceneTransmission()
        {
            Tick(0f); Preview("baseline");
            presenter.SetBlindness(state, 3f, config); Tick(.04f); Preview("onset");
            Tick(.04f); Preview("blinded");
            presenter.SetInjury(state, 75f, 100f); Tick(0f); Preview("blinded_damage");
            Tick(2.595f); Preview("mid_recovery");
            Tick(.325f); Preview("recovered");
        }

        private void Preview(string label)
        {
            Assert.That(state.SceneTint.r, Is.GreaterThan(0f));
            Vector4 lens = state.Frame.Lens;
            TestContext.WriteLine(System.FormattableString.Invariant(
                $"BLIND_PREVIEW {label} tint={state.SceneTint.r:R} lens={lens.x:R},{lens.y:R},{lens.z:R},{lens.w:R} edge={state.Frame.EdgeStart:R} blur={state.BlurRadius:R} damage={state.Vignette:R} smoothness={state.VignetteSmoothness:R}"));
        }
    }
}
