// ============================================================================
// CollapseDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries staged room destruction and escapable hand hazards without imposing damage in presentation.
//   Values cross system boundaries without transferring ownership of live state.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Collapse shared contracts.
// KEY RESPONSIBILITIES:
//   - Carry immutable observations and committed facts between layers.
//   - Keep identity and timing explicit without engine operations.
//   - Carry optional Light/Hand recovery metadata for the hand hit path.
//   - Carry an outward hit velocity and continuous room warning pulse for routed consumers.
// DEPENDENCIES:
//   - Core EntityId and UnityEngine value types only.
// USAGE NOTES:
//   Positions/ranges are metres, angles are degrees, and lifetime is seconds.
//   Constructors assign data only; owning systems validate and apply rules.
//   Severity and Source describe damage only when Kind is Hit; they do not cause damage.
//   ThrowVelocity is metres/second, applied by Player only on an accepted Hit.
//   PulseRate is cycles/second; PulsePhase is a wrapped cycle, zero when inactive.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{

    public enum CollapseHandEventKind { Warning, Grabbed, Escaped, Hit, Released, Consumed }
    /// <summary>A collapse-hand transition, including recovery metadata for accepted hits.</summary>
    public readonly struct CollapseHandFact
    {
        public CollapseHandFact(EntityId playerId, int roomId, CollapseHandEventKind kind, Vector3 position,
            float slowMultiplier, float damage, long tick, HitSeverity severity = HitSeverity.Light,
            HitSource source = HitSource.Hand, Vector3 throwVelocity = default)
        { PlayerId = playerId; RoomId = roomId; Kind = kind; Position = position; SlowMultiplier = slowMultiplier;
          Damage = damage; Tick = tick; Severity = severity; Source = source; ThrowVelocity = throwVelocity; }
        public EntityId PlayerId { get; }
        public int RoomId { get; }
        public CollapseHandEventKind Kind { get; }
        public Vector3 Position { get; }
        public float SlowMultiplier { get; }
        public float Damage { get; }
        public long Tick { get; }
        public HitSeverity Severity { get; }
        public HitSource Source { get; }
        public Vector3 ThrowVelocity { get; }
    }
    public readonly struct RoomDestructionSample
    {
        public RoomDestructionSample(int roomId, RoomPhase phase, float progress, float pulseRate = 0f, float pulsePhase = 0f)
        { RoomId = roomId; Phase = phase; Progress = progress; PulseRate = pulseRate; PulsePhase = pulsePhase; }
        public int RoomId { get; }
        public RoomPhase Phase { get; }
        public float Progress { get; }
        public float PulseRate { get; }
        public float PulsePhase { get; }
    }
}
