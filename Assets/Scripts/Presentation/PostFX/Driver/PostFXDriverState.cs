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
//   - Remember cleansed blindness identities until they disappear from the active view.
//   - Retain an injected active-effects view and identity-matched grace envelope.
//   - Preserve runtime blur preferences, independent effect/Glimpse countdowns and blindness easing.
//   - Retain health, red hit/heartbeat envelopes and the independent camcorder tape.
//   - Carry primitive volume values without holding a live volume.
//
// DEPENDENCIES:
//   - Core read-only effects and grace facts; no gameplay implementation dependencies.
//
// USAGE NOTES:
//   - Scene-owned through PostFXDriver; passive data only.
//
// ============================================================================

using UnityEngine;
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXDriverState
    {
        public readonly CamcorderFrameDriverState Frame = new CamcorderFrameDriverState();
        public IReadOnlyActiveEffects ActiveEffects;
        public readonly HashSet<EffectId> CleansedBlindness = new HashSet<EffectId>();
        public GraceWindowFact Grace;
        public bool GraceActive;
        public float GraceWeight;
        public bool Consumed;
        public bool? ReacquireBlurEnabled;
        public float ConsumptionElapsed, ConsumptionDuration, Blackout, Exposure;
        public Color SceneTint = Color.white;
        public float Proximity;
        public bool LookBack;
        public float GlimpseRemaining;
        public float HunterRim;
        public float Injury;
        public bool HasHealthSample;
        public float Health;
        public float DamageSeverity, DamageElapsed, PendingDamageSeverity, HeartbeatEnvelope;
        public Color VignetteColor = Color.red;
        public float VignetteSmoothness;
        public float IntrusionRemaining;
        public float SubtleIntrusionRemaining;
        public float BlindnessRemaining;
        public float BlindnessPhase, BlindnessWeight;
        public bool BlindnessSourceActive;
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
