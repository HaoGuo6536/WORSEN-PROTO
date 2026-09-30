// ============================================================================
// AudioDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Stores transient cue arbitration, fading and movement playback data.
//   Separating these values from live sources lets mixer behavior run in pure tests.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Preserve runtime volume multipliers and listener-pause ownership across run resets.
//   - Retain current cue priority, timing and overlapping fade gains.
//   - Retain pushed proximity, movement and fractional injury values for layer mixing.
//
// DEPENDENCIES:
//   - Core MovementState describes movement without a Domain dependency.
//
// USAGE NOTES:
//   - Owned by AudioDriver; never retained by another system.
//   - No engine operations or events; ResetRun preserves runtime preferences.
//
// ============================================================================

using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioDriverState
    {
        public int ActiveCueKey = -1;
        public float RuntimeMaster = 1f, RuntimeMusic = 1f, RuntimeEffects = 1f;
        public bool Paused, OwnsListenerPause, PreviousListenerPause;
        public int ActivePriority = -1;
        public float CueRemaining;
        public float CueGain;
        public float CueTargetGain;
        public float CueFadeSeconds;
        public float OutgoingCueGain;
        public float OutgoingFadeSeconds;
        public float Proximity;
        public float SpeedNormalized;
        public float BreathGain;
        public float HunterGain;
        public float FootstepRemaining;
        public MovementState MovementState;
        public float CurrentHealth = 100f;
        public float MaxHealth = 100f;
        public int VoiceIndex;
        public readonly HashSet<int> MissingCueWarnings = new HashSet<int>();
    }
}
