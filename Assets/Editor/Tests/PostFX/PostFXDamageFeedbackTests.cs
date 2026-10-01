// ============================================================================
// PostFXDamageFeedbackTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the production damage composition with explicitly supplied health,
//   heartbeat envelopes and time. Managed configuration fields make these tests
//   runnable without a Unity process; native defaults and volume binding are separate.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify severity, retriggers, exact fade boundaries and frame partitioning.
//   - Verify low-health persistence, heartbeat modulation and regeneration clearing.
//   - Protect blindness, grace identity and constant camcorder shading from hit changes.
// DEPENDENCIES:
//   Core, PostFX, NUnit and managed reflection only.
// USAGE NOTES:
//   Headless-safe: the uninitialized config is only explicitly populated managed
//   fields. Never pass it to an engine API or destroy it as a native asset.
// ============================================================================
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.PostFX;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PostFXDamageFeedbackTests
    {
        private PostFXDriverConfig config;
        private PostFXDriverState state;
        private readonly PostFXPresenter presenter = new PostFXPresenter();

        [SetUp] public void Setup()
        {
            config = (PostFXDriverConfig)FormatterServices.GetUninitializedObject(typeof(PostFXDriverConfig));
            Set("_damageVignettePeak", .5f); Set("_damageFullStrengthHealthFraction", .5f);
            Set("_damageFadeSeconds", 1.25f); Set("_damageVignetteSmoothness", .3f);
            Set("_damageVignetteColor", new Color(1f, .015f, .01f, 1f));
            Set("_lowHealthFraction", .35f); Set("_lowHealthVignette", .25f); Set("_lowHealthPulseMultiplier", .5f);
            // Deliberately retain old serialized values: code must not revive their dimming.
            Set("_frameVignette", .12f); Set("_injuryVignette", .45f); Set("_graceSaturation", -35f);
            Set("_graceEaseInSeconds", .12f); Set("_graceEaseOutSeconds", .25f);
            Set("_blindnessDarkness", 1f); Set("_intrusionDesaturation", 70f);
            Set("_blindnessOnsetSeconds", .08f); Set("_blindnessRecoverySeconds", .65f);
            Set("_camcorderEnabled", true); Set("_camcorderCorners", .22f);
            Set("_camcorderCornerRadius", .25f); Set("_camcorderCornerSoftness", .2f);
            Set("_camcorderEdgeBlurPixels", 1.25f); Set("_tapeCriticalInjury", .75f);
            Set("_tapeRiseSeconds", .08f); Set("_tapeRelaxSeconds", .65f); Set("_tapeHitSeconds", .4f);
            state = new PostFXDriverState(); presenter.SetInjury(state, 100f, 100f);
        }
        private void Set(string name, object value) => typeof(PostFXDriverConfig)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        private void Tick(float dt) => presenter.Tick(state, config, dt);

        [TestCase(0f, 0f)] [TestCase(.5f, .005f)] [TestCase(10f, .1f)]
        [TestCase(25f, .25f)] [TestCase(50f, .5f)] [TestCase(90f, .5f)]
        public void HitStrengthScalesWithAcceptedHealthLoss(float damage, float expected)
        {
            presenter.SetInjury(state, 100f - damage, 100f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(expected).Within(.00001f));
            Assert.That(state.VignetteColor, Is.EqualTo(new Color(1f, .015f, .01f, 1f)));
        }

        [TestCase(0f, .25f)] [TestCase(.625f, .125f)] [TestCase(1.249f, .0002f)]
        [TestCase(1.25f, 0f)] [TestCase(10f, 0f)]
        public void EchoSizedHitFadesAtConfiguredBoundary(float seconds, float expected)
        {
            presenter.SetInjury(state, 75f, 100f); Tick(seconds);
            Assert.That(state.Vignette, Is.EqualTo(expected).Within(.00001f));
        }

        [Test] public void FadeIsIndependentOfFramePartition()
        {
            presenter.SetInjury(state, 75f, 100f);
            for (int i = 0; i < 50; i++) Tick(.01f);
            float split = state.Vignette;
            presenter.Reset(state); presenter.SetInjury(state, 100f, 100f);
            presenter.SetInjury(state, 75f, 100f); Tick(.5f);
            Assert.That(state.Vignette, Is.EqualTo(split).Within(.00001f));
        }

        [Test] public void WeakerRepeatHitPreservesCurrentStrengthButGetsANewFade()
        {
            presenter.SetInjury(state, 50f, 100f); Tick(.625f);
            Assert.That(state.Vignette, Is.EqualTo(.25f));
            presenter.SetInjury(state, 45f, 100f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(.25f));
            Tick(1.25f); Assert.That(state.Vignette, Is.Zero);
        }

        [Test] public void SameFrameDamageAccumulatesButDuplicateHealthDoesNotRetrigger()
        {
            presenter.SetInjury(state, 90f, 100f); presenter.SetInjury(state, 75f, 100f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(.25f));
            Tick(.5f); presenter.SetInjury(state, 75f, 100f); Tick(.75f);
            Assert.That(state.Vignette, Is.Zero);
        }

        [Test] public void InitialLowHealthAndMaximumChangesDoNotInventHits()
        {
            presenter.Reset(state); presenter.SetInjury(state, 25f, 100f); Tick(0f);
            Assert.That(state.DamageSeverity + state.PendingDamageSeverity + state.Frame.HitWeight, Is.Zero);
            Assert.That(state.Vignette, Is.GreaterThan(0f));
            presenter.SetInjury(state, 25f, 200f); Tick(0f);
            Assert.That(state.DamageSeverity + state.PendingDamageSeverity, Is.Zero);
        }

        [Test] public void LowHealthPersistsPulsesWithInjectedHeartbeatAndClearsAsItRegenerates()
        {
            presenter.SetInjury(state, 25f, 100f); Tick(10f);
            float faint = .25f * Mathf.Sqrt((.35f - .25f) / .35f);
            Assert.That(state.Vignette, Is.EqualTo(faint).Within(.00001f));
            Tick(100f); Assert.That(state.Vignette, Is.EqualTo(faint).Within(.00001f));
            presenter.SetHeartbeatEnvelope(state, 1f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(faint * 1.5f).Within(.00001f));
            presenter.SetHeartbeatEnvelope(state, .5f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(faint * 1.25f).Within(.00001f));
            presenter.SetHeartbeatEnvelope(state, 0f); presenter.SetInjury(state, 30f, 100f); Tick(0f);
            Assert.That(state.Vignette, Is.EqualTo(faint * Mathf.Sqrt(.5f)).Within(.00001f));
            presenter.SetInjury(state, 35f, 100f); Tick(0f); Assert.That(state.Vignette, Is.Zero.Within(.00001f));
            presenter.SetInjury(state, 100f, 100f); Tick(0f); Assert.That(state.Vignette, Is.Zero);
        }

        [Test] public void FullHealthImmediatelyClearsEvenAnUnfinishedHitAndHeartbeat()
        {
            presenter.SetInjury(state, 20f, 100f); presenter.SetHeartbeatEnvelope(state, 1f); Tick(.1f);
            presenter.SetInjury(state, 100f, 100f); Tick(0f);
            Assert.That(state.Vignette + state.DamageSeverity + state.PendingDamageSeverity + state.HeartbeatEnvelope, Is.Zero);
        }

        [Test] public void HitAndGraceNeverDimDesaturateOrExposeDownAndLensShadingIsUnchanged()
        {
            Tick(0f); Vector4 lens = state.Frame.Lens;
            presenter.SetInjury(state, 25f, 100f);
            var grace = new GraceWindowFact(new EntityId(1), 1, 61, HitSeverity.Heavy);
            presenter.SetGrace(state, grace, true);
            foreach (float dt in new[] { 0f, .1f, .5f, 1f, 10f })
            {
                Tick(dt);
                Assert.That(state.Blackout, Is.Zero); Assert.That(state.Exposure, Is.Zero);
                Assert.That(state.Saturation, Is.Zero); Assert.That(state.SceneTint, Is.EqualTo(Color.white));
                Assert.That(state.Frame.Lens, Is.EqualTo(lens));
            }
            Assert.That(state.GraceActive, Is.True);
            Assert.That(state.GraceWeight, Is.EqualTo(1f));
            presenter.SetGrace(state, grace, false); Tick(.25f); Assert.That(state.GraceWeight, Is.Zero);
        }

        [Test] public void BlinderAndIntrusionRemainIndependentOfDamageFadeAndHealthRecovery()
        {
            presenter.SetInjury(state, 75f, 100f); presenter.SetBlindness(state, 2f);
            presenter.PlayIntrusion(state, 2f); Tick(1.25f);
            Assert.That(state.Vignette, Is.Zero); Assert.That(state.Blackout, Is.EqualTo(PostFXDriverConfig.MaximumBlindnessDarkness));
            Assert.That(state.Saturation, Is.EqualTo(-70f));
            presenter.SetInjury(state, 100f, 100f); Tick(0f); Assert.That(state.Blackout, Is.EqualTo(PostFXDriverConfig.MaximumBlindnessDarkness));
            Tick(.75f); Assert.That(state.Blackout, Is.Zero); Assert.That(state.Saturation, Is.Zero);
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidTimeCannotAdvanceFadeAndInvalidHeartbeatIsNeutral(float value)
        {
            presenter.SetInjury(state, 75f, 100f); Tick(0f);
            Tick(value); Assert.That(state.Vignette, Is.EqualTo(.25f));
            presenter.SetHeartbeatEnvelope(state, value); Assert.That(state.HeartbeatEnvelope, Is.Zero);
        }

        [Test] public void ResetDropsHealthBaselinePendingDamageAndHeartbeat()
        {
            presenter.SetInjury(state, 25f, 100f); presenter.SetHeartbeatEnvelope(state, 1f);
            presenter.Reset(state); Tick(0f);
            Assert.That(state.HasHealthSample, Is.False);
            Assert.That(state.Vignette + state.DamageSeverity + state.PendingDamageSeverity + state.HeartbeatEnvelope, Is.Zero);
            presenter.SetInjury(state, 50f, 100f); Tick(0f); Assert.That(state.Vignette, Is.Zero);
        }
    }
}
