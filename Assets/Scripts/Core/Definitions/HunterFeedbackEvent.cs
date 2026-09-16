// ============================================================================
// HunterFeedbackEvent.cs
// ============================================================================
// PURPOSE:
//   Describes enemy anticipation, attack outcome, and distinctive presence for presentation.
//   Values cross system boundaries without transferring ownership of live state.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Hunter feedback shared contracts.
// KEY RESPONSIBILITIES:
//   - Identify one committed attack for bounded projectile travel loop lifetimes.
//   - Carry immutable observations and committed facts between layers.
//   - Keep identity and timing explicit without engine operations.
// DEPENDENCIES:
//   - Core EntityId and UnityEngine value types only.
// USAGE NOTES:
//   Positions/ranges are metres, angles are degrees, and lifetime is seconds.
//   Constructors assign data only; owning systems validate and apply rules.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{

    public enum HunterFeedbackKind { Detected, LostTarget, AttackWindup, AttackSwing, AttackMiss, AttackHit, AttackRecovery, LightReaction, Scream, Footstep, ProjectileLaunched, ProjectileTravel, ProjectileImpact, ProjectileExpired, SpikeWarning, SpikeErupt, ProjectileMoved }
    public readonly struct HunterFeedbackEvent
    {
        public HunterFeedbackEvent(EntityId hunter, string archetypeKey, HunterFeedbackKind kind, Vector3 position, long tick, int attackSerial = 0, int emitterId = 0)
        { Hunter = hunter; ArchetypeKey = archetypeKey; Kind = kind; Position = position; Tick = tick; AttackSerial = attackSerial; EmitterId = emitterId; }
        public EntityId Hunter { get; }
        public string ArchetypeKey { get; }
        public HunterFeedbackKind Kind { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public int AttackSerial { get; }
        public int EmitterId { get; }
    }
}
