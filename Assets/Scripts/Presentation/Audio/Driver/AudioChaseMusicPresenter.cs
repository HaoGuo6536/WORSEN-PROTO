// ============================================================================
// AudioChaseMusicPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes the Deep Impacts escalation and Claustrophobia danger mix from
//   supplied threat facts. After first belief contact a tension floor survives loss;
//   injected randomness delays danger release or occasionally permits an early fade.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Keep the normal impact floor beneath stress and gradually change playback speed.
//   - Gate escalation on supplied aggregate belief, not mere proximity.
//   - Draw once per aggregate loss and cancel run audio without a resolving outro.
// DEPENDENCIES:
//   - Own Audio value types, DriverState and shared read-only DriverConfig.
// USAGE NOTES:
//   Delta time, DSP clock, intro duration and cosmetic randomness are supplied by the Driver.
//   AudioThreatSample.Chasing is the legacy field name for music belief admission.
//   Live snapshots include Confirmed/Lost or fresh hunter belief and uncensored proximity.
//   Ordinary loss waits the sampled delay even if danger remains close. A lie
//   bypasses that wait for a nearby-loss episode; reacquisition cancels release.
//   No engine calls or clip properties are read here. Reset replaces the state.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioChaseMusicPresenter
    {
        public void Tick(AudioChaseMusicDriverState state, IEnumerable<AudioThreatSample> threats,
            bool alive, float dt, double dspTime, double introSeconds, AudioSoundscapeDriverConfig config,
            System.Random random)
        {
            state.StartRun = state.EndRun = state.StopRun = false;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f || double.IsNaN(dspTime) || double.IsInfinity(dspTime)) return;
            bool chase = false; float proximity = 0f;
            foreach (AudioThreatSample threat in threats)
            {
                chase |= threat.Chasing;
                if (!float.IsNaN(threat.Closeness) && !float.IsInfinity(threat.Closeness))
                    proximity = Mathf.Max(proximity, Mathf.Clamp01(threat.Closeness));
            }
            chase &= alive;
            if (!alive)
            {
                state.StopRun = true; state.Chasing = false;
                state.HasContact = state.LossActive = state.EarlyDangerFade = false;
                state.ReleaseDelaySeconds = state.ReleaseRemaining = 0f;
                state.TensionGain = state.StressGain = state.DangerGain = 0f;
                state.ImpactPitch = config.ImpactMinimumPitch;
                return;
            }
            if (chase && !state.Chasing)
            {
                state.HasContact = true;
                state.LossActive = state.EarlyDangerFade = false;
                state.ReleaseDelaySeconds = state.ReleaseRemaining = 0f;
                state.StartRun = true;
                state.IntroStart = dspTime + .05;
                state.LoopStart = state.IntroStart + (double.IsNaN(introSeconds) || double.IsInfinity(introSeconds) ? 0 : Math.Max(0, introSeconds));
            }
            else if (!chase && state.Chasing)
            {
                state.StopRun = true;
                state.LossActive = true;
                float minimum = Mathf.Max(0f, config.DangerReleaseMinimumSeconds);
                float maximum = Mathf.Max(minimum, config.DangerReleaseMaximumSeconds);
                state.ReleaseDelaySeconds = Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
                state.ReleaseRemaining = state.ReleaseDelaySeconds;
                state.EarlyDangerFade = random.NextDouble() < Mathf.Clamp01(config.EarlyDangerFadeProbability) &&
                    proximity > config.DangerProximityThreshold;
            }
            state.Chasing = chase;
            float floor = state.HasContact ? Mathf.Clamp01(config.TensionFloorFraction) * config.DeepImpactGain : 0f;
            state.TensionGain = Mathf.Max(floor, Fade(state.TensionGain, floor, dt, config));
            state.StressGain = Fade(state.StressGain, chase ? config.StressImpactGain : 0f, dt, config);
            float dangerDt = dt;
            if (!chase && state.LossActive && !state.EarlyDangerFade)
            {
                // Spend only the part of this tick beyond the hold on the fade.
                dangerDt = Mathf.Max(0f, dt - state.ReleaseRemaining);
                state.ReleaseRemaining = Mathf.Max(0f, state.ReleaseRemaining - dt);
            }
            state.DangerGain = Fade(state.DangerGain, chase ? config.ChaseDangerGain : 0f, dangerDt, config);
            state.ImpactPitch = Mathf.MoveTowards(state.ImpactPitch,
                chase ? config.ImpactMaximumPitch : config.ImpactMinimumPitch,
                dt * (config.ImpactMaximumPitch - config.ImpactMinimumPitch) / Mathf.Max(.1f, config.ImpactRampSeconds));
        }
        private float Fade(float value, float target, float dt, AudioSoundscapeDriverConfig config) =>
            Mathf.MoveTowards(value, target, dt / Mathf.Max(.05f, target > value ? config.AttackSeconds : config.ReleaseSeconds));
    }
}
