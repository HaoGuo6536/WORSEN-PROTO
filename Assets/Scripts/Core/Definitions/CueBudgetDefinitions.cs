// ============================================================================
// CueBudgetDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines the silence-first cue budget alongside the unchanged legacy CueId list.
//   Variation metadata preserves exact timing for cues that teach an attack tell.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Audio contracts.
// KEY RESPONSIBILITIES:
//   - Name owner-specific cue slots and reject invalid variation specifications.
// DEPENDENCIES:
//   - System only; no audio engine or other project layers.
// USAGE NOTES:
//   Pitch is a positive playback-rate multiplier. Volume jitter is a symmetric
//   amplitude offset in [0,1]; timing jitter is a symmetric offset in seconds.
//   AlternateCount counts additional takes (zero means the primary take only).
//   Config/Profile owners supply all tuning; random selection happens elsewhere.
// ============================================================================
using System;

namespace Worsen.Core
{
    /// <summary>The owner category used to budget concurrent cues.</summary>
    public enum CueCategory { Player, Hunter, World, Interface, Music }
    /// <summary>The three player-owned sound slots.</summary>
    public enum PlayerCueSlot { Footstep, Breathing, TraversalContact }
    /// <summary>The sound slots owned separately by each hunter archetype.</summary>
    public enum HunterCueSlot { Presence, Detection, ChaseLayer, AttackTiming, DeathSting }
    /// <summary>The meaningful environmental sound slots.</summary>
    public enum WorldCueSlot { CakePickup, ExitDoor, RoomTelegraph, TorchGutter, HunterClosedDoor, TrapTrigger, ShrineActivate }

    /// <summary>Validated immutable variation bounds, with exact timing for tell cues.</summary>
    public readonly struct CueVariationSpec
    {
        public CueVariationSpec(float minimumPitch, float maximumPitch, float volumeJitter,
            float timingJitter, int alternateCount, bool timingIsTell)
        {
            if (!(minimumPitch > 0f) || float.IsInfinity(minimumPitch)) throw new ArgumentOutOfRangeException(nameof(minimumPitch));
            if (!(maximumPitch >= minimumPitch) || float.IsInfinity(maximumPitch)) throw new ArgumentOutOfRangeException(nameof(maximumPitch));
            if (!(volumeJitter >= 0f && volumeJitter <= 1f)) throw new ArgumentOutOfRangeException(nameof(volumeJitter));
            if (!(timingJitter >= 0f) || float.IsInfinity(timingJitter)) throw new ArgumentOutOfRangeException(nameof(timingJitter));
            if (alternateCount < 0) throw new ArgumentOutOfRangeException(nameof(alternateCount));
            if (timingIsTell && timingJitter != 0f) throw new ArgumentException("A timing tell cannot have timing jitter.", nameof(timingJitter));
            MinimumPitch = minimumPitch; MaximumPitch = maximumPitch; VolumeJitter = volumeJitter;
            TimingJitter = timingJitter; AlternateCount = alternateCount; TimingIsTell = timingIsTell;
        }
        public float MinimumPitch { get; }
        public float MaximumPitch { get; }
        public float VolumeJitter { get; }
        public float TimingJitter { get; }
        public int AlternateCount { get; }
        public bool TimingIsTell { get; }
    }
}
