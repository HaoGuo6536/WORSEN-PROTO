// ============================================================================
// PostFXPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Maps supplied pursuit and health facts to visual effect strengths.
//   The pure calculator composes independent effects and advances only with supplied time.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Preserve independent cleanse, revival, grace and runtime blur commands.
//   - Bound invalid inputs and preserve arbitrary fractional health values.
//   - Expire optional blur and intrusion without clearing injury or proximity.
//   - Keep catches visible: legacy consumption never fades to black.
//   - Compose existing degradation with the independently timed camcorder frame.
//
// DEPENDENCIES:
//   - Core grace/effects contracts; pure UnityEngine math only.
//
// USAGE NOTES:
//   - Stateless; PostFXDriver owns the supplied state and all volume APIs.
//   - The caller supplies intrusion duration, including the initial two-second response.
//   - Look-back release blur affects focus only, never camera pose or view blending.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXPresenter
    {
        public void SetReacquireBlurEnabled(PostFXDriverState state, bool enabled)
        {
            state.ReacquireBlurEnabled = enabled;
            if (enabled) return;
            state.BlurRemaining = state.Blur = 0f;
            state.BlurRadius = 0.5f;
        }

        public void Reset(PostFXDriverState state)
        {
            CamcorderFramePresenter.Reset(state.Frame);
            state.ActiveEffects = null;
            state.CleansedBlindness.Clear();
            state.Grace = default;
            state.GraceActive = false;
            state.GraceWeight = 0f;
            state.Consumed = false;
            state.ConsumptionElapsed = state.ConsumptionDuration = state.Blackout = state.Exposure = 0f;
            state.SceneTint = Color.white;
            state.LookBack = false;
            state.SubtleIntrusionRemaining = state.BlindnessRemaining = 0f;
            state.Proximity = state.Injury = state.IntrusionRemaining = state.BlurRemaining = 0f;
            state.Chromatic = state.Distortion = state.Vignette = state.Saturation = state.Grain = state.Blur = 0f;
            state.BlurRadius = 0.5f;
        }

        public void SetLookBack(PostFXDriverState state, PostFXDriverConfig config, bool held)
        {
            if (state.LookBack && !held) PlayReacquireBlur(state, config);
            state.LookBack = held;
        }

        public void SetProximity(PostFXDriverState state, float closeness)
            => state.Proximity = Mathf.Clamp01(Finite(closeness));

        public void SetInjury(PostFXDriverState state, float currentHealth, float maxHealth)
        {
            maxHealth = Finite(maxHealth);
            float injury = maxHealth > 0f ? 1f - Mathf.Clamp01(Finite(currentHealth) / maxHealth) : 0f;
            if (injury > state.Injury) state.Frame.HitWeight = 1f;
            state.Injury = injury;
        }

        public void PlayReacquireBlur(PostFXDriverState state, PostFXDriverConfig config)
            => state.BlurRemaining = (state.ReacquireBlurEnabled ?? config.ReacquireBlurEnabled) ? Mathf.Max(0f, config.ReacquireBlurSeconds) : 0f;

        public void PlayIntrusion(PostFXDriverState state, float seconds)
            => state.IntrusionRemaining = Mathf.Max(state.IntrusionRemaining, Mathf.Max(0f, Finite(seconds)));

        public void PlayIntrusion(PostFXDriverState state, float seconds, bool startle)
        {
            if (startle) PlayIntrusion(state, seconds);
            else state.SubtleIntrusionRemaining = Mathf.Max(state.SubtleIntrusionRemaining, Mathf.Max(0f, Finite(seconds)));
        }

        public void SetBlindness(PostFXDriverState state, float seconds, PostFXDriverConfig config = null)
        {
            state.BlindnessRemaining = Mathf.Max(0f, Finite(seconds));
            if (seconds != 0f || config == null || config.BlindnessEffectIds == null) return;
            foreach (string id in config.BlindnessEffectIds)
                if (!string.IsNullOrWhiteSpace(id) && state.ActiveEffects?.Has(new EffectId(id)) == true)
                    state.CleansedBlindness.Add(new EffectId(id));
        }

        public void SetActiveEffects(PostFXDriverState state, IReadOnlyActiveEffects effects)
        {
            state.ActiveEffects = effects;
            state.CleansedBlindness.RemoveWhere(id => effects == null || !effects.Has(id));
        }

        public void ClearConsumed(PostFXDriverState state)
        {
            state.Consumed = false;
            state.ConsumptionElapsed = state.ConsumptionDuration = 0f;
        }

        public void SetGrace(PostFXDriverState state, GraceWindowFact fact, bool active)
        {
            if (active)
            {
                if (!fact.PlayerId.IsValid || fact.EndTick <= fact.StartTick
                    || (state.Grace.PlayerId == fact.PlayerId && fact.StartTick < state.Grace.StartTick)) return;
                state.Grace = fact;
                state.GraceActive = true;
            }
            else if (state.Grace.PlayerId == fact.PlayerId && state.Grace.StartTick == fact.StartTick)
                state.GraceActive = false;
        }

        public bool HasBlindness(PostFXDriverState state, PostFXDriverConfig config)
        {
            state.CleansedBlindness.RemoveWhere(id => state.ActiveEffects == null || !state.ActiveEffects.Has(id));
            if (state.ActiveEffects == null || config.BlindnessEffectIds == null) return false;
            foreach (string id in config.BlindnessEffectIds)
                if (!string.IsNullOrWhiteSpace(id) && state.ActiveEffects.Has(new EffectId(id)) &&
                    !state.CleansedBlindness.Contains(new EffectId(id))) return true;
            return false;
        }

        public void PlayConsumed(PostFXDriverState state, float seconds)
        {
            seconds = Finite(seconds);
            if (state.Consumed || seconds <= 0f) return;
            state.Consumed = true;
            state.ConsumptionElapsed = 0f;
            state.ConsumptionDuration = Mathf.Clamp(seconds, 0.1f, 2f);
        }

        public void Tick(PostFXDriverState state, PostFXDriverConfig config, float dt)
        {
            dt = Mathf.Max(0f, Finite(dt));
            state.IntrusionRemaining = Mathf.Max(0f, state.IntrusionRemaining - dt);
            state.SubtleIntrusionRemaining = Mathf.Max(0f, state.SubtleIntrusionRemaining - dt);
            state.BlindnessRemaining = Mathf.Max(0f, state.BlindnessRemaining - dt);
            state.BlurRemaining = (state.ReacquireBlurEnabled ?? config.ReacquireBlurEnabled) ? Mathf.Max(0f, state.BlurRemaining - dt) : 0f;
            state.Chromatic = Mathf.Clamp01(config.BaselineChromatic + state.Proximity * config.PeripheralChromatic);
            state.Distortion = -Mathf.Clamp01(state.Proximity * config.PeripheralDistortion);
            state.Vignette = Mathf.Clamp01(config.FrameVignette + state.Injury * config.InjuryVignette);
            float intrusion = state.IntrusionRemaining > 0f ? 1f :
                state.SubtleIntrusionRemaining > 0f ? Mathf.Clamp01(config.SubtleIntrusionMultiplier) : 0f;
            CamcorderFramePresenter.Tick(state.Frame, config, state.Injury, state.Proximity, intrusion, dt);
            state.Saturation = -Mathf.Clamp(config.IntrusionDesaturation, 0f, 100f) * intrusion;
            float ease = state.GraceActive ? config.GraceEaseInSeconds : config.GraceEaseOutSeconds;
            state.GraceWeight = Mathf.MoveTowards(state.GraceWeight, state.GraceActive ? 1f : 0f,
                dt / Mathf.Max(0.001f, Finite(ease)));
            float grace = state.GraceWeight * state.GraceWeight * (3f - 2f * state.GraceWeight);
            state.Saturation = Mathf.Min(state.Saturation, Mathf.Clamp(Finite(config.GraceSaturation), -100f, 0f) * grace);
            state.Grain = Mathf.Clamp01(config.BaselineGrain + config.IntrusionGrain * intrusion);
            state.Blur = Mathf.Clamp01(state.BlurRemaining / Mathf.Max(0.001f, config.ReacquireBlurSeconds));
            state.BlurRadius = Mathf.Lerp(0.5f, config.BlurRadius, state.Blur);
            state.Blackout = state.Exposure = 0f;
            // A held catch must remain visible even if a blindness hook was still active.
            state.Blackout = !state.Consumed && state.Injury < 1f && (state.BlindnessRemaining > 0f || HasBlindness(state, config))
                ? Mathf.Clamp01(config.BlindnessDarkness) : 0f;
            state.SceneTint = Color.Lerp(Color.white, Color.black, state.Blackout);
            if (!state.Consumed) return;
            state.ConsumptionElapsed = Mathf.Min(state.ConsumptionDuration, state.ConsumptionElapsed + dt);
        }

        private float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
