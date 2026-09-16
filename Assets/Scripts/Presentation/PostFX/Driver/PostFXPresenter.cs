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
//   - Bound invalid inputs and preserve arbitrary fractional health values.
//   - Expire optional blur and intrusion without clearing injury or proximity.
//   - Reset all transient effects on a scene or run reset.
//
// DEPENDENCIES:
//   - No other project systems; pure UnityEngine math only.
//
// USAGE NOTES:
//   - Stateless; PostFXDriver owns the supplied state and all volume APIs.
//   - The caller supplies intrusion duration, including the initial two-second response.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXPresenter
    {
        public void Reset(PostFXDriverState state)
        {
            state.LookBack = false;
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
            state.Injury = maxHealth > 0f ? 1f - Mathf.Clamp01(Finite(currentHealth) / maxHealth) : 0f;
        }

        public void PlayReacquireBlur(PostFXDriverState state, PostFXDriverConfig config)
            => state.BlurRemaining = config.ReacquireBlurEnabled ? Mathf.Max(0f, config.ReacquireBlurSeconds) : 0f;

        public void PlayIntrusion(PostFXDriverState state, float seconds)
            => state.IntrusionRemaining = Mathf.Max(state.IntrusionRemaining, Mathf.Max(0f, Finite(seconds)));

        public void Tick(PostFXDriverState state, PostFXDriverConfig config, float dt)
        {
            dt = Mathf.Max(0f, Finite(dt));
            state.IntrusionRemaining = Mathf.Max(0f, state.IntrusionRemaining - dt);
            state.BlurRemaining = config.ReacquireBlurEnabled ? Mathf.Max(0f, state.BlurRemaining - dt) : 0f;
            state.Chromatic = Mathf.Clamp01(state.Proximity * config.PeripheralChromatic);
            state.Distortion = -Mathf.Clamp01(state.Proximity * config.PeripheralDistortion);
            state.Vignette = Mathf.Clamp01(state.Injury * config.InjuryVignette);
            state.Saturation = state.IntrusionRemaining > 0f ? -Mathf.Clamp(config.IntrusionDesaturation, 0f, 100f) : 0f;
            state.Grain = state.IntrusionRemaining > 0f ? Mathf.Clamp01(config.IntrusionGrain) : 0f;
            state.Blur = Mathf.Clamp01(state.BlurRemaining / Mathf.Max(0.001f, config.ReacquireBlurSeconds));
            state.BlurRadius = Mathf.Lerp(0.5f, config.BlurRadius, state.Blur);
        }

        private float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
