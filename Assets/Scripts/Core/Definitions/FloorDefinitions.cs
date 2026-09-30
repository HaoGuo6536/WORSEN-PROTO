// ============================================================================
// FloorDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Describes floor collection, exit readiness, collapse and directional display.
//   Values cross system boundaries without exposing mutable runtime state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Floor shared contracts.
//
// KEY RESPONSIBILITIES:
//   - Carry tick-stamped identity and immutable values between owning systems.
//   - Keep event payloads independent of Domain and Presentation implementations.
//   - Publish normalized visual opening progress independently of logical exit readiness.
//   - Carry generation-fixed cake totals and floor-scoped counter suppression.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Closed retains its serialized value and denotes fully consumed, not a new blocker wall.
//   Distances are metres and durations are seconds; ticks identify committed steps.
//   Constructors carry supplied values and perform no engine or gameplay operations.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum PickupKind { Cake, GoldenCake }
    public enum ExitState { Locked, Open }
    public enum RoomPhase { Open = 0, Telegraph = 1, Closed = 2, Tearing = 3, Encroaching = 4 }
    public readonly struct PickupCollectedFact
    {
        public PickupCollectedFact(EntityId playerId, int anchorId, PickupKind kind, int cakeCount, int goldenCount, long tick)
        {
            PlayerId = playerId;
            AnchorId = anchorId;
            Kind = kind;
            CakeCount = cakeCount;
            GoldenCount = goldenCount;
            Tick = tick;
        }
        public EntityId PlayerId { get; }
        public int AnchorId { get; }
        public PickupKind Kind { get; }
        public int CakeCount { get; }
        public int GoldenCount { get; }
        public long Tick { get; }
    }
    public readonly struct RoomPhaseChangedFact
    {
        public RoomPhaseChangedFact(int roomId, RoomPhase phase, long tick)
        {
            RoomId = roomId;
            Phase = phase;
            Tick = tick;
        }
        public int RoomId { get; }
        public RoomPhase Phase { get; }
        public long Tick { get; }
    }
    public readonly struct ExitReachedFact
    {
        public ExitReachedFact(EntityId playerId, long tick)
        {
            PlayerId = playerId;
            Tick = tick;
        }
        public EntityId PlayerId { get; }
        public long Tick { get; }
    }
    public readonly struct FloorLethalContactFact
    {
        public FloorLethalContactFact(EntityId playerId, int roomId, long tick)
        {
            PlayerId = playerId;
            RoomId = roomId;
            Tick = tick;
        }
        public EntityId PlayerId { get; }
        public int RoomId { get; }
        public long Tick { get; }
    }
    public readonly struct FloorDisplaySnapshot
    {
        public FloorDisplaySnapshot(int collected, int required, int golden, ExitState exit, bool hasCue, Vector3 cueDirection,
            float openingProgress = 0f, int totalCakes = 0, int totalGoldenCakes = 0, bool hiddenCount = false,
            int optionalGoldenCakeCount = 0)
        {
            Collected = collected;
            Required = required;
            Golden = golden;
            Exit = exit;
            HasCue = hasCue;
            CueDirection = cueDirection;
            OpeningProgress = openingProgress;
            TotalCakes = totalCakes;
            TotalGoldenCakes = totalGoldenCakes;
            HiddenCount = hiddenCount;
            OptionalGoldenCakeCount = optionalGoldenCakeCount;
        }
        public int Collected { get; }
        public int Required { get; }
        public int Golden { get; }
        public ExitState Exit { get; }
        public bool HasCue { get; }
        public Vector3 CueDirection { get; }
        public float OpeningProgress { get; }
        public int TotalCakes { get; }
        public int TotalGoldenCakes { get; }
        public bool HiddenCount { get; }
        public int OptionalGoldenCakeCount { get; }
    }
}
