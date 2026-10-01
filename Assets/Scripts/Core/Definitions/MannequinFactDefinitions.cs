// ============================================================================
// MannequinFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Publishes committed Mannequin movement starts and holds with world position.
//   Audio uses these edges for creaks, independently of the silent generic sound
//   identity. Legacy light payloads remain compatible but are no longer emitted.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Mannequin.
// KEY RESPONSIBILITIES:
//   - Carry room overrides, floor lamp-budget multipliers and silence identity.
//   - Carry committed movement transitions and their world position without audio dependencies.
// DEPENDENCIES:
//   - Core entity identity and UnityEngine position values only.
// USAGE NOTES:
//   Temporary overrides expire after Seconds; permanent overrides end at floor
//   teardown. Wick takes precedence. LampBudget is absolute, never per duplicate.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum MannequinFactKind { RoomLightOverride, LampBudget, SilentSoundSet, MovementStarted, MovementHeld }
    public readonly struct MannequinFact
    {
        public MannequinFact(EntityId hunter, MannequinFactKind kind, long tick, int roomId = 0,
            bool lit = false, float seconds = 0f, bool permanent = false, float value = 1f, Vector3 position = default)
        { Hunter = hunter; Kind = kind; Tick = tick; RoomId = roomId; Lit = lit; Seconds = seconds; Permanent = permanent; Value = value; Position = position; }
        public EntityId Hunter { get; }
        public MannequinFactKind Kind { get; }
        public long Tick { get; }
        public int RoomId { get; }
        public bool Lit { get; }
        public float Seconds { get; }
        public bool Permanent { get; }
        public float Value { get; }
        public Vector3 Position { get; }
    }
}
