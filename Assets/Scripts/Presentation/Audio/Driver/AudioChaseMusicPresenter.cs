// ============================================================================
// AudioChaseMusicPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes the Deep Impacts escalation and Claustrophobia danger mix from
//   supplied threat facts. One aggregate pursuit edge schedules the run intro,
//   loop and ending, independently of individual hunter joins or departures.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Crossfade normal/stress impacts and gradually change their playback speed.
//   - Increase Claustrophobia during confirmed pursuit and end the run layer once.
//   - Cancel all queued run audio on death without playing a celebratory outro.
// DEPENDENCIES:
//   - Own Audio value types, DriverState and shared read-only DriverConfig.
// USAGE NOTES:
//   Delta time, DSP clock and intro duration are supplied by the Driver.
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
            bool alive, float dt, double dspTime, double introSeconds, AudioSoundscapeDriverConfig config)
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
                state.TensionGain = state.StressGain = state.DangerGain = 0f;
                state.ImpactPitch = config.ImpactMinimumPitch;
                return;
            }
            if (chase && !state.Chasing)
            {
                state.StartRun = true;
                state.IntroStart = dspTime + .05;
                state.LoopStart = state.IntroStart + (double.IsNaN(introSeconds) || double.IsInfinity(introSeconds) ? 0 : Math.Max(0, introSeconds));
            }
            else if (!chase && state.Chasing)
            {
                state.EndRun = true;
                state.EndingStart = dspTime + .02;
            }
            state.Chasing = chase;
            float stress = chase ? 1f : Mathf.InverseLerp(.55f, .95f, proximity);
            float intensity = chase ? 1f : proximity;
            state.TensionGain = Fade(state.TensionGain, proximity * (1f - stress) * config.DeepImpactGain, dt, config);
            state.StressGain = Fade(state.StressGain, stress * config.StressImpactGain, dt, config);
            state.DangerGain = Fade(state.DangerGain, chase ? config.ChaseDangerGain :
                Mathf.InverseLerp(.35f, 1f, proximity) * config.DangerAmbienceGain, dt, config);
            state.ImpactPitch = Mathf.MoveTowards(state.ImpactPitch,
                Mathf.Lerp(config.ImpactMinimumPitch, config.ImpactMaximumPitch, intensity),
                dt * (config.ImpactMaximumPitch - config.ImpactMinimumPitch) / Mathf.Max(.1f, config.ImpactRampSeconds));
        }
        private float Fade(float value, float target, float dt, AudioSoundscapeDriverConfig config) =>
            Mathf.MoveTowards(value, target, dt / Mathf.Max(.05f, target > value ? config.AttackSeconds : config.ReleaseSeconds));
    }
}
