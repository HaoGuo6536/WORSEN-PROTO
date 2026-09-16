// ============================================================================
// AudioChaseMusicPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies adaptive music transitions using explicit clocks and threat samples.
//   The tests cover sequence ownership and escalation without depending on speakers.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Verify one intro/outro per aggregate pursuit, death and rapid reacquisition.
//   - Verify danger gain, bounded gradual impact speed and invalid time handling.
// DEPENDENCIES:
//   - AudioChaseMusicPresenter and its state/config, NUnit and Unity test fixtures.
// USAGE NOTES:
//   Temporary configuration is destroyed without modifying shared assets.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
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
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            presenter = new AudioChaseMusicPresenter(); state = new AudioChaseMusicDriverState { ImpactPitch = config.ImpactMinimumPitch };
            threats = new Dictionary<int, AudioThreatSample>();
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);
        private void Tick(float dt = 1f, double now = 10, bool alive = true, double duration = 7.125) =>
            presenter.Tick(state, threats.Values, alive, dt, now, duration, config);
        [Test] public void ConfirmedChaseSchedulesExactIntroDurationAndOnlyOneAggregateStart()
        {
            threats[1] = new AudioThreatSample { Chasing = true };
            Tick(); Assert.That(state.StartRun, Is.True); Assert.That(state.IntroStart, Is.EqualTo(10.05).Within(.000001));
            Assert.That(state.LoopStart - state.IntroStart, Is.EqualTo(7.125).Within(.000001));
            threats[2] = new AudioThreatSample { Chasing = true }; Tick(now: 11);
            Assert.That(state.StartRun, Is.False); Assert.That(state.IntroStart, Is.EqualTo(10.05).Within(.000001));
            threats.Remove(1); Tick(); Assert.That(state.EndRun, Is.False);
            threats.Remove(2); Tick(now: 12); Assert.That(state.EndRun, Is.True);
            Tick(now: 13); Assert.That(state.EndRun, Is.False);
        }
        [Test] public void DangerIsAudibleBeforePursuitAndLouderDuringDistantConfirmedChase()
        {
            threats[1] = new AudioThreatSample { Closeness = 1f }; Tick(5);
            float prior = state.DangerGain;
            Assert.That(prior, Is.EqualTo(config.DangerAmbienceGain)); Assert.That(state.StartRun, Is.False);
            threats[1] = new AudioThreatSample { Chasing = true, Closeness = 0f }; Tick(5);
            Assert.That(state.DangerGain, Is.EqualTo(config.ChaseDangerGain).And.GreaterThan(prior));
            Assert.That(state.StressGain, Is.EqualTo(config.StressImpactGain)); Assert.That(state.TensionGain, Is.Zero);
        }
        [Test] public void ImpactSpeedRampsUpAndDownInsteadOfJumpingAndUsesStressBeforeChase()
        {
            threats[1] = new AudioThreatSample { Closeness = .9f }; Tick(.1f);
            Assert.That(state.ImpactPitch, Is.GreaterThan(config.ImpactMinimumPitch).And.LessThan(config.ImpactMaximumPitch));
            Assert.That(state.StressGain, Is.GreaterThan(0f)); Assert.That(state.StartRun, Is.False);
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(10);
            Assert.That(state.ImpactPitch, Is.EqualTo(config.ImpactMaximumPitch));
            threats.Clear(); Tick(.1f); Assert.That(state.ImpactPitch, Is.LessThan(config.ImpactMaximumPitch).And.GreaterThan(config.ImpactMinimumPitch));
            Tick(10); Assert.That(state.ImpactPitch, Is.EqualTo(config.ImpactMinimumPitch));
            Assert.That(state.TensionGain + state.StressGain + state.DangerGain, Is.Zero);
        }
        [Test] public void ReacquisitionStartsFreshIntroAndCancelsThePreviousEnding()
        {
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(); threats.Clear(); Tick(now: 11);
            Assert.That(state.EndRun, Is.True);
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(now: 11.1);
            Assert.That(state.StartRun, Is.True); Assert.That(state.EndRun, Is.False);
            Assert.That(state.IntroStart, Is.EqualTo(11.15).Within(.000001));
        }
        [Test] public void DeathCancelsPlaybackWithoutEndingAndStaleThreatsCannotRestartIt()
        {
            threats[1] = new AudioThreatSample { Chasing = true }; Tick(); Tick(alive: false);
            Assert.That(state.StopRun, Is.True); Assert.That(state.EndRun, Is.False); Assert.That(state.StartRun, Is.False);
            Assert.That(state.Chasing, Is.False); Assert.That(state.TensionGain + state.StressGain + state.DangerGain, Is.Zero);
            Tick(alive: false); Assert.That(state.StartRun, Is.False);
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
    }
}
