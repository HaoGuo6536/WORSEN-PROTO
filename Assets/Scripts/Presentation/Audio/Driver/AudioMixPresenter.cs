// ============================================================================
// AudioMixPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Calculates priority cue arbitration and smooth audio layer gains from facts.
//   This makes interruption, critical breathing and step cadence deterministic
//   without reading a clock, scene object or audio source in the calculator.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Count landing and slide-exit contact as the current step, so cadence waits for the next footfall.
//   - Admit higher-priority cues and retain an outgoing fade on interruption.
//   - Preserve fractional injury samples and map them with proximity to bounded gains.
//   - Advance footsteps and all fades using caller-supplied elapsed time.
//
// DEPENDENCIES:
//   - Core MovementState only; AudioDriverState and AudioMixSettings are owned data.
//
// USAGE NOTES:
//   - Stateless; AudioDriver owns every state instance and performs playback.
//   - Cue keys are opaque integers. Only the Driver interprets Core CueId values.
//   - The Driver checks clip availability before asking to arbitrate a cue.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioMixPresenter
    {
        public bool TryCue(AudioDriverState state, int cueKey, int priority, float seconds, float gain, float fadeSeconds)
        {
            seconds = Nonnegative(seconds);
            if (seconds <= 0f || cueKey < 0) return false;
            if (state.CueRemaining > 0f && (priority < state.ActivePriority || cueKey == state.ActiveCueKey)) return false;
            state.OutgoingCueGain = state.CueGain;
            state.OutgoingFadeSeconds = Mathf.Max(0.001f, Nonnegative(fadeSeconds));
            state.ActiveCueKey = cueKey;
            state.ActivePriority = priority;
            state.CueRemaining = seconds;
            state.CueTargetGain = Unit(gain);
            state.CueFadeSeconds = Mathf.Max(0.001f, Nonnegative(fadeSeconds));
            // Admission establishes gain before playback; a short cue may finish before the next render frame.
            state.CueGain = state.CueTargetGain;
            return true;
        }

        public void SetProximity(AudioDriverState state, float closeness) => state.Proximity = Unit(closeness);
        public void SetSpeedNormalized(AudioDriverState state, float speed) => state.SpeedNormalized = Unit(speed);
        public float ClampGain(float gain) => Unit(gain);
        public void SetMovementState(AudioDriverState state, MovementState movement) => state.MovementState = movement;
        public void MarkFootContact(AudioDriverState state, AudioMixSettings settings) =>
            state.FootstepRemaining = Mathf.Max(state.FootstepRemaining, StepInterval(state, settings));
        public void SetInjury(AudioDriverState state, float currentHealth, float maxHealth)
        {
            state.MaxHealth = Nonnegative(maxHealth);
            state.CurrentHealth = Mathf.Clamp(Nonnegative(currentHealth), 0f, state.MaxHealth);
        }

        public bool Tick(AudioDriverState state, AudioMixSettings settings, float dt)
        {
            dt = Nonnegative(dt);
            if (dt <= 0f) return false;
            state.CueRemaining = Mathf.Max(0f, state.CueRemaining - dt);
            float cueTarget = state.CueRemaining > 0f ? state.CueTargetGain : 0f;
            state.CueGain = Approach(state.CueGain, cueTarget, dt, state.CueFadeSeconds);
            state.OutgoingCueGain = Approach(state.OutgoingCueGain, 0f, dt, state.OutgoingFadeSeconds);
            if (state.CueRemaining <= 0f && state.CueGain <= 0f)
            {
                state.ActiveCueKey = -1;
                state.ActivePriority = -1;
            }

            bool alive = state.CurrentHealth > 0f && state.MaxHealth > 0f;
            bool critical = alive && state.CurrentHealth / state.MaxHealth <= Unit(settings.CriticalHealthFraction);
            float breath = alive ? state.Proximity * Unit(settings.BreathMaximumGain) : 0f;
            if (critical) breath = Mathf.Max(breath, Unit(settings.CriticalBreathGain));
            float hunter = alive ? state.Proximity * state.Proximity * Unit(settings.HunterMaximumGain) : 0f;
            state.BreathGain = Approach(state.BreathGain, breath, dt, settings.LayerFadeSeconds);
            state.HunterGain = Approach(state.HunterGain, hunter, dt, settings.LayerFadeSeconds);

            if (!alive || state.MovementState != MovementState.Ground || state.SpeedNormalized <= Unit(settings.MinimumStepSpeed))
            {
                state.FootstepRemaining = 0f;
                return false;
            }
            state.FootstepRemaining = Mathf.Max(0f, state.FootstepRemaining - dt);
            if (state.FootstepRemaining > 0f) return false;
            state.FootstepRemaining = StepInterval(state, settings);
            return true;
        }

        public void Reset(AudioDriverState state)
        {
            state.ActiveCueKey = -1;
            state.ActivePriority = -1;
            state.CueRemaining = state.CueGain = state.CueTargetGain = state.CueFadeSeconds = 0f;
            state.OutgoingCueGain = state.OutgoingFadeSeconds = 0f;
            state.Proximity = state.SpeedNormalized = state.BreathGain = state.HunterGain = state.FootstepRemaining = 0f;
            state.MovementState = MovementState.Ground;
            state.CurrentHealth = state.MaxHealth = 100f;
            state.VoiceIndex = 0;
            state.MissingCueWarnings.Clear();
        }

        private float Approach(float current, float target, float dt, float seconds) =>
            Mathf.MoveTowards(Unit(current), Unit(target), dt / Mathf.Max(0.001f, Nonnegative(seconds)));
        private float StepInterval(AudioDriverState state, AudioMixSettings settings) =>
            Mathf.Max(0.01f, Mathf.Lerp(Nonnegative(settings.SlowStepSeconds), Nonnegative(settings.FastStepSeconds), Unit(state.SpeedNormalized)));
        private float Nonnegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        private float Unit(float value) => Mathf.Clamp01(Nonnegative(value));
    }
}
