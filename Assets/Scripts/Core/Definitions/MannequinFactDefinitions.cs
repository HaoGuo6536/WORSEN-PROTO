// ============================================================================
// MannequinFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Publishes the Mannequin's light subversion and environment curse evidence.
//   Session owns lamp changes; audio receives an explicit all-slots-silent fact
//   rather than falling back to the reused placeholder prefab's sound identity.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Mannequin.
// KEY RESPONSIBILITIES:
//   - Carry room overrides, floor lamp-budget multipliers and silence identity.
// DEPENDENCIES:
//   - Core entity identity only.
// USAGE NOTES:
//   Temporary overrides expire after Seconds; permanent overrides end at floor
//   teardown. Wick takes precedence. LampBudget is absolute, never per duplicate.
// ============================================================================
namespace Worsen.Core
{
    public enum MannequinFactKind { RoomLightOverride, LampBudget, SilentSoundSet }
    public readonly struct MannequinFact
    {
        public MannequinFact(EntityId hunter, MannequinFactKind kind, long tick, int roomId = 0,
            bool lit = false, float seconds = 0f, bool permanent = false, float value = 1f)
        { Hunter = hunter; Kind = kind; Tick = tick; RoomId = roomId; Lit = lit; Seconds = seconds; Permanent = permanent; Value = value; }
        public EntityId Hunter { get; }
        public MannequinFactKind Kind { get; }
        public long Tick { get; }
        public int RoomId { get; }
        public bool Lit { get; }
        public float Seconds { get; }
        public bool Permanent { get; }
        public float Value { get; }
    }
}
