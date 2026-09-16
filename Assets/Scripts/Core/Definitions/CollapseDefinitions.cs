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
// DEPENDENCIES:
//   - Core EntityId and UnityEngine value types only.
// USAGE NOTES:
//   Positions/ranges are metres, angles are degrees, and lifetime is seconds.
//   Constructors assign data only; owning systems validate and apply rules.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{

    public enum CollapseHandEventKind { Warning, Grabbed, Escaped, Hit, Released, Consumed }
    public readonly struct CollapseHandFact
    {
        public CollapseHandFact(EntityId playerId, int roomId, CollapseHandEventKind kind, Vector3 position, float slowMultiplier, float damage, long tick)
        { PlayerId = playerId; RoomId = roomId; Kind = kind; Position = position; SlowMultiplier = slowMultiplier; Damage = damage; Tick = tick; }
        public EntityId PlayerId { get; }
        public int RoomId { get; }
        public CollapseHandEventKind Kind { get; }
        public Vector3 Position { get; }
        public float SlowMultiplier { get; }
        public float Damage { get; }
        public long Tick { get; }
    }
    public readonly struct RoomDestructionSample
    {
        public RoomDestructionSample(int roomId, RoomPhase phase, float progress)
        { RoomId = roomId; Phase = phase; Progress = progress; }
        public int RoomId { get; }
        public RoomPhase Phase { get; }
        public float Progress { get; }
    }
}
