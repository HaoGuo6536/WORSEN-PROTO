// ============================================================================
// HunterChaseDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Describes hunter observations, accepted hits and committed chase transitions.
//   Values cross system boundaries without exposing mutable runtime state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Hunter and Chase shared contracts.
//
// KEY RESPONSIBILITIES:
//   - Carry tick-stamped identity and immutable values between owning systems.
//   - Keep event payloads independent of Domain and Presentation implementations.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Distances are metres and durations are seconds; ticks identify committed steps.
//   Constructors carry supplied values and perform no engine or gameplay operations.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum ChasePhase { None, Confirmed, Lost }
    public readonly struct SightProbe
    {
        public SightProbe(bool headVisible, bool chestVisible, bool hipsVisible)
        {
            HeadVisible = headVisible;
            ChestVisible = chestVisible;
            HipsVisible = hipsVisible;
        }
        public bool HeadVisible { get; }
        public bool ChestVisible { get; }
        public bool HipsVisible { get; }
    }
    public readonly struct HunterHit
    {
        public HunterHit(EntityId hunter, EntityId target, int damage, long tick, Vector3 hunterPosition, ChaseEndReason reason = ChaseEndReason.Lunge)
        {
            Hunter = hunter;
            Target = target;
            Damage = damage;
            Tick = tick;
            HunterPosition = hunterPosition;
            Reason = reason;
        }
        public EntityId Hunter { get; }
        public EntityId Target { get; }
        public int Damage { get; }
        public long Tick { get; }
        public Vector3 HunterPosition { get; }
        public ChaseEndReason Reason { get; }
    }
    public readonly struct HunterSighting
    {
        public HunterSighting(EntityId hunter, EntityId target, long tick, bool visible, Vector3 position, float distance)
        {
            Hunter = hunter;
            Target = target;
            Tick = tick;
            Visible = visible;
            Position = position;
            Distance = distance;
        }
        public EntityId Hunter { get; }
        public EntityId Target { get; }
        public long Tick { get; }
        public bool Visible { get; }
        public Vector3 Position { get; }
        public float Distance { get; }
    }
    public readonly struct ChaseFact
    {
        public ChaseFact(int chaseId, EntityId player, EntityId hunter, long tick, ChasePhase phase, ChaseEndReason endReason = ChaseEndReason.Unknown)
        {
            ChaseId = chaseId;
            Player = player;
            Hunter = hunter;
            Tick = tick;
            Phase = phase;
            EndReason = endReason;
        }
        public int ChaseId { get; }
        public EntityId Player { get; }
        public EntityId Hunter { get; }
        public long Tick { get; }
        public ChasePhase Phase { get; }
        public ChaseEndReason EndReason { get; }
    }
    public readonly struct ProximitySample
    {
        public ProximitySample(EntityId player, EntityId hunter, long tick, int chaseId, float distance, float closeness, bool inChase)
        {
            Player = player;
            Hunter = hunter;
            Tick = tick;
            ChaseId = chaseId;
            Distance = distance;
            Closeness = closeness;
            InChase = inChase;
        }
        public EntityId Player { get; }
        public EntityId Hunter { get; }
        public long Tick { get; }
        public int ChaseId { get; }
        public float Distance { get; }
        public float Closeness { get; }
        public bool InChase { get; }
    }
}
