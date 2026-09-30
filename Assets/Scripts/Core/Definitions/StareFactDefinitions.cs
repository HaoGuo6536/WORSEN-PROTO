// ============================================================================
// StareFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Publishes the Stare's presence, escalation and spatial voice identity.
//   Voice lines are stable ids, not audio assets or references to Presentation.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Stare.
// KEY RESPONSIBILITIES:
//   - Carry per-entity presence and five-slot sound facts for Session relays.
// DEPENDENCIES:
//   - Core entity/cue values and Unity Vector3 data only.
// USAGE NOTES:
//   stare.i-see-you and stare.find-me are spoken lines. Presence is logical
//   despawn/respawn of the retained instance, not removal from the chosen roster.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum StareFactKind { Appeared, Disappeared, ChaseStarted, Sound }
    public readonly struct StareFact
    {
        public StareFact(EntityId hunter, StareFactKind kind, Vector3 position, long tick,
            string soundId = null, float volume = 1f, HunterCueSlot slot = HunterCueSlot.Presence)
        { Hunter = hunter; Kind = kind; Position = position; Tick = tick; SoundId = soundId; Volume = volume; Slot = slot; }
        public EntityId Hunter { get; }
        public StareFactKind Kind { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public string SoundId { get; }
        public float Volume { get; }
        public HunterCueSlot Slot { get; }
    }
}
