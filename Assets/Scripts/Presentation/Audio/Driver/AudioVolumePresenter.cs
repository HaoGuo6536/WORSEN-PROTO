// ============================================================================
// AudioVolumePresenter.cs
// ============================================================================
// PURPOSE:
//   Converts saved linear preferences to mixer decibels without touching an engine object.
//   Failed or absent mixer parameters retain source-gain fallback without double attenuation.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Sanitize gains and select the single place each preference is applied.
// DEPENDENCIES:
//   - Pure Unity math only.
// USAGE NOTES:
//   Minus eighty decibels is the mixer's silence floor, not a designer mix gain.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioVolumePresenter
    {
        public float Linear(float gain) => float.IsNaN(gain) || float.IsInfinity(gain) ? 0f : Mathf.Clamp01(gain);
        public float Decibels(float gain) => Linear(gain) <= 0f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(Linear(gain)));
        public float SourceGain(float gain, bool appliedToMixer) => appliedToMixer ? 1f : Linear(gain);
    }
}
