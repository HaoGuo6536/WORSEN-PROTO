// ============================================================================
// AudioChaseMusicPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies adaptive music transitions using explicit clocks and threat samples.
//   Seeded episodes cover the persistent floor, ambiguous losses and belief-gated
//   escalation without depending on speakers or changing shared assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Verify one intro per aggregate pursuit, silent outro, death and reacquisition.
//   - Verify danger gain, bounded gradual impact speed and invalid time handling.
//   - Verify bounded reproducible delays and statistically bounded early-fade frequency.
// DEPENDENCIES:
//   - AudioChaseMusicPresenter and its state/config, NUnit and Unity test fixtures.
// USAGE NOTES:
//   Temporary configuration is destroyed without modifying shared assets.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Audio;

namespace Worsen.Tests.Audio
{
    public sealed class AudioChaseMusicPresenterTests
    {
        private AudioSoundscapeDriverConfig config;
        private AudioChaseMusicPresenter presenter;
        private AudioChaseMusicDriverState state;
        private Dictionary<int, AudioThreatSample> threats;
        private System.Random random;
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            presenter = new AudioChaseMusicPresenter(); state = new AudioChaseMusicDriverState { ImpactPitch = config.ImpactMinimumPitch };
            threats = new Dictionary<int, AudioThreatSample>();
            random = new System.Random(76103);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);
        private void Tick(float dt = 1f, double now = 10, bool alive = true, double duration = 7.125) =>
            presenter.Tick(state, threats.Values, alive, dt, now, duration, config, random);
        [Test] public void ConfirmedChaseSchedulesExactIntroDurationAndOnlyOneAggregateStart()
        {
            threats[1] = new AudioThreatSample { Chasing = true };
            Tick(); Assert.That(state.StartRun, Is.True); Assert.That(state.IntroStart, Is.EqualTo(10.05).Within(.000001));
            Assert.That(state.LoopStart - state.IntroStart, Is.EqualTo(7.125).Within(.000001));
            threats[2] = new AudioThreatSample { Chasing = true }; Tick(now: 11);
            Assert.That(state.StartRun, Is.False); Assert.That(state.IntroStart, Is.EqualTo(10.05).Within(.000001));
            threats.Remove(1); Tick(); Assert.That(state.EndRun, Is.False);
            threats.Remove(2); Tick(now: 12); Assert.That(state.StopRun, Is.True); Assert.That(state.EndRun, Is.False);
            Tick(now: 13); Assert.That(state.StopRun, Is.False); Assert.That(state.EndRun, Is.False);
        }
        [Test] public void ProximityAloneIsSilentButDistantBeliefAdmitsMusic()
        {
            threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(5);
            float prior = state.DangerGain;
            Assert.That(prior, Is.Zero); Assert.That(state.StartRun, Is.False);
            Assert.That(state.TensionGain + state.StressGain, Is.Zero);
            threats[1] = new AudioThreatSample { Chasing = true, Closeness = 0f }; Tick(5);
            Assert.That(state.DangerGain, Is.EqualTo(config.ChaseDangerGain).And.GreaterThan(prior));
            Assert.That(state.StressGain, Is.EqualTo(config.StressImpactGain)); Assert.That(state.TensionGain, Is.EqualTo(Floor));
        }
        [Test] public void ImpactSpeedRampsWithBeliefAndLossLeavesOnlyTheFloor()
        {
            threats[1] = new AudioThreatSample { Chasing = true, Closeness = .9f }; Tick(.1f);
            Assert.That(state.ImpactPitch, Is.GreaterThan(config.ImpactMinimumPitch).And.LessThan(config.ImpactMaximumPitch));
            Assert.That(state.StressGain, Is.GreaterThan(0f)); Assert.That(state.StartRun, Is.True);
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(10);
            Assert.That(state.ImpactPitch, Is.EqualTo(config.ImpactMaximumPitch));
            threats.Clear(); Tick(.1f); Assert.That(state.ImpactPitch, Is.LessThan(config.ImpactMaximumPitch).And.GreaterThan(config.ImpactMinimumPitch));
            Tick(10); Assert.That(state.ImpactPitch, Is.EqualTo(config.ImpactMinimumPitch));
            Assert.That(state.TensionGain, Is.EqualTo(Floor));
            Assert.That(state.StressGain + state.DangerGain, Is.Zero);
        }
        [Test] public void ReacquisitionStartsFreshIntroAndCancelsTheLossEpisode()
        {
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(); threats.Clear(); Tick(now: 11);
            Assert.That(state.StopRun, Is.True); Assert.That(state.LossActive, Is.True);
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(now: 11.1);
            Assert.That(state.StartRun, Is.True); Assert.That(state.EndRun, Is.False);
            Assert.That(state.IntroStart, Is.EqualTo(11.15).Within(.000001));
            Assert.That(state.LossActive, Is.False); Assert.That(state.EarlyDangerFade, Is.False);
            Assert.That(state.ReleaseRemaining, Is.Zero);
        }
        [Test] public void DeathCancelsPlaybackWithoutEndingAndStaleThreatsCannotRestartIt()
        {
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(); Tick(alive: false);
            Assert.That(state.StopRun, Is.True); Assert.That(state.EndRun, Is.False); Assert.That(state.StartRun, Is.False);
            Assert.That(state.Chasing, Is.False); Assert.That(state.TensionGain + state.StressGain + state.DangerGain, Is.Zero);
            Tick(alive: false); Assert.That(state.StartRun, Is.False);
            Assert.That(state.HasContact, Is.False); Assert.That(state.ReleaseRemaining, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(0f)]
        public void InvalidDeltaCannotAdmitSequence(float dt)
        {
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(dt);
            Assert.That(state.StartRun, Is.False); Assert.That(state.Chasing, Is.False);
        }
        [Test] public void MissingIntroSchedulesLoopAtTheSameStartTime()
        {
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(duration: 0);
            Assert.That(state.LoopStart, Is.EqualTo(state.IntroStart));
        }
        private float Floor => config.DeepImpactGain * config.TensionFloorFraction;
        private void Set(string field, float value)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [Test] public void FloorSurvivesLongSilenceAndProximityCannotReenterEscalation()
        {
            Tick(); Assert.That(state.TensionGain + state.StressGain + state.DangerGain, Is.Zero);
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(.001f);
            Assert.That(state.TensionGain, Is.GreaterThanOrEqualTo(Floor));
            threats.Clear();
            for (int i = 0; i < 10000; i++)
            { Tick(.1f); Assert.That(state.TensionGain, Is.GreaterThanOrEqualTo(Floor)); }
            threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(10);
            Assert.That(state.TensionGain, Is.EqualTo(Floor));
            Assert.That(state.StressGain + state.DangerGain, Is.Zero);
            state = new AudioChaseMusicDriverState(); threats.Clear(); Tick();
            Assert.That(state.TensionGain + state.StressGain + state.DangerGain, Is.Zero);
        }
        [Test] public void FloorFractionIsConfigurable()
        {
            Set("_tensionFloorFraction", .3f);
            threats[1] = new AudioThreatSample { Chasing = true }; Tick();
            threats.Clear(); Tick(20);
            Assert.That(state.TensionGain, Is.EqualTo(config.DeepImpactGain * .3f));
        }
        [Test] public void ReleaseDelayIsUniformBoundedSeededAndDrawnOnlyOncePerLoss()
        {
            Set("_earlyDangerFadeProbability", 0f);
            var expected = new System.Random(76103);
            double total = 0;
            for (int episode = 0; episode < 1000; episode++)
            {
                threats[1] = new AudioThreatSample { Chasing = true }; Tick(10);
                threats.Clear(); Tick(.01f);
                float delay = state.ReleaseDelaySeconds;
                Assert.That(delay, Is.InRange(config.DangerReleaseMinimumSeconds, config.DangerReleaseMaximumSeconds));
                float replay = Mathf.Lerp(config.DangerReleaseMinimumSeconds, config.DangerReleaseMaximumSeconds, (float)expected.NextDouble());
                expected.NextDouble();
                Assert.That(delay, Is.EqualTo(replay)); total += delay;
                float gain = state.DangerGain;
                Tick(state.ReleaseRemaining * .5f);
                Assert.That(state.DangerGain, Is.EqualTo(gain));
                Assert.That(state.ReleaseDelaySeconds, Is.EqualTo(delay));
                Tick(state.ReleaseRemaining + .1f);
                Assert.That(state.DangerGain, Is.LessThan(gain));
            }
            Assert.That(total / 1000, Is.EqualTo((config.DangerReleaseMinimumSeconds + config.DangerReleaseMaximumSeconds) * .5f).Within(.15));
        }
        [TestCase(0f)] [TestCase(.15f)] [TestCase(1f)]
        public void EarlyFadeFrequencyMatchesProbabilityWithoutTickRerolls(float probability)
        {
            Set("_earlyDangerFadeProbability", probability);
            int lies = 0;
            for (int episode = 0; episode < 5000; episode++)
            {
                threats[1] = new AudioThreatSample { Chasing = true, Closeness = 1f }; Tick(10);
                threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(.01f);
                bool lie = state.EarlyDangerFade;
                if (lie) lies++;
                Assert.That(state.DangerGain < config.ChaseDangerGain, Is.EqualTo(lie));
                float delay = state.ReleaseDelaySeconds;
                Tick(.01f);
                Assert.That(state.EarlyDangerFade, Is.EqualTo(lie));
                Assert.That(state.ReleaseDelaySeconds, Is.EqualTo(delay));
            }
            if (probability == 0f || probability == 1f) Assert.That(lies, Is.EqualTo(5000 * probability));
            else Assert.That(lies / 5000f, Is.EqualTo(probability).Within(.025f));
        }
        [Test] public void OrdinaryLossWaitsItsDelayEvenWhileCloseThenFadesWithoutReentry()
        {
            Set("_earlyDangerFadeProbability", 0f);
            threats[1] = new AudioThreatSample { Chasing = true, Closeness = 1f }; Tick(10);
            threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(.01f);
            Assert.That(state.DangerGain, Is.EqualTo(config.ChaseDangerGain));
            Assert.That(state.ReleaseRemaining, Is.EqualTo(state.ReleaseDelaySeconds - .01f).Within(.000001f));
            Tick(state.ReleaseRemaining);
            Assert.That(state.DangerGain, Is.EqualTo(config.ChaseDangerGain));
            Tick(.1f); float fading = state.DangerGain;
            Assert.That(fading, Is.LessThan(config.ChaseDangerGain));
            threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(.1f);
            Assert.That(state.DangerGain, Is.LessThanOrEqualTo(fading));
            threats.Clear(); Tick(10); Assert.That(state.DangerGain, Is.Zero);
        }
        [Test] public void EarlyFadePersistsWhileCloseAndReacquisitionRestoresDanger()
        {
            Set("_earlyDangerFadeProbability", 1f);
            threats[1] = new AudioThreatSample { Chasing = true, Closeness = 1f }; Tick(10);
            threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(.1f);
            Assert.That(state.EarlyDangerFade, Is.True);
            Tick(10); Assert.That(state.DangerGain, Is.Zero);
            threats[1] = new AudioThreatSample { Chasing = true, Closeness = 1f }; Tick(10);
            Assert.That(state.EarlyDangerFade, Is.False);
            Assert.That(state.DangerGain, Is.EqualTo(config.ChaseDangerGain));
        }
        [Test] public void AnotherHunterPreventsLossAndFarLossAlwaysUsesTheDelay()
        {
            Set("_earlyDangerFadeProbability", 1f);
            threats[1] = threats[2] = new AudioThreatSample { Chasing = true }; Tick(10);
            threats.Remove(1); Tick(); Assert.That(state.LossActive, Is.False);
            threats.Clear(); Tick(.01f);
            Assert.That(state.LossActive, Is.True); Assert.That(state.EarlyDangerFade, Is.False);
            Assert.That(state.DangerGain, Is.EqualTo(config.ChaseDangerGain));
        }
        [Test] public void AmbienceDefaultsAreNearSilent()
        {
            Assert.That(config.AmbienceGain, Is.EqualTo(.01f));
            var horror = ScriptableObject.CreateInstance<Worsen.Presentation.Horror.HorrorDriverConfig>();
            try { Assert.That(horror.AmbienceGain, Is.EqualTo(.01f)); }
            finally { Object.DestroyImmediate(horror); }
        }
    }
}
