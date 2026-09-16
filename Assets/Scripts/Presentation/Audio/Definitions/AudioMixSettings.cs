// ============================================================================
// AudioMixSettings.cs
// ============================================================================
//
// PURPOSE:
//   Carries designer mixer settings into a pure calculation as plain values.
//   The Driver reads its config and supplies this snapshot without exposing sources.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Describe gain limits, fade timing, critical breathing and step cadence.
//   - Keep the mixer calculator independent of asset and engine operations.
//
// DEPENDENCIES:
//   - No other project systems.
//
// USAGE NOTES:
//   - Values are copied from AudioDriverConfig by the owning Driver.
//   - This is plain data; validation and calculations belong to the Presenter.
//
// ============================================================================

namespace Worsen.Presentation.Audio
{
    public struct AudioMixSettings
    {
        public float LayerFadeSeconds;
        public float BreathMaximumGain;
        public float HunterMaximumGain;
        public float CriticalBreathGain;
        public float CriticalHealthFraction;
        public float SlowStepSeconds;
        public float FastStepSeconds;
        public float MinimumStepSpeed;
    }
}
