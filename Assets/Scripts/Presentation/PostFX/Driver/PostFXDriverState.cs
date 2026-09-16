// ============================================================================
// PostFXDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Stores independent visual feedback inputs and their evaluated outputs.
//   This prevents one short-lived response from erasing another active effect.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Retain proximity, injury and explicit effect countdowns.
//   - Retain terminal blackout independently until an explicit reset.
//   - Carry primitive volume values without holding a live volume.
//
// DEPENDENCIES:
//   - No other project systems.
//
// USAGE NOTES:
//   - Scene-owned through PostFXDriver; passive data only.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXDriverState
    {
        public bool Consumed;
        public float ConsumptionElapsed, ConsumptionDuration, Blackout, Exposure;
        public Color SceneTint = Color.white;
        public float Proximity;
        public bool LookBack;
        public float Injury;
        public float IntrusionRemaining;
        public float BlurRemaining;
        public float Chromatic;
        public float Distortion;
        public float Vignette;
        public float Saturation;
        public float Grain;
        public float Blur;
        public float BlurRadius;
    }
}
