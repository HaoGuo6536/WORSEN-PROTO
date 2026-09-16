// ============================================================================
// PlayerMovementDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Describes committed player motion, traversal and passive inventory display.
//   It keeps cooperating systems on one contract without sharing mutable state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared boilerplate contracts.
//
// KEY RESPONSIBILITIES:
//   - Carry achieved slide turning for cosmetic banking without steering authority.
//   - Carry committed crouch and sprint facts so feedback never guesses from camera height or input intent.
//   - Carry explicit values across system and layer boundaries.
//   - Preserve replay and measurement identity without engine object references.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Positions are metres; look deltas are degrees; Tick identifies the committed step.
//   These values contain no engine operations or gameplay decision logic.
//   IsSprinting describes achieved grounded sprint movement; IsCrouched describes the committed low posture.
//   Optional constructor fields preserve older consumers that do not provide these facts.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum MovementState { Ground, Air, Slide, Vault, Stumble }
    public enum TraversalKind { None, Jump, Slide, Vault, Mantle, Rebound, Land }
    public enum TraversalSurfaceKind { None, Vault, Rebound, SlideGate, OneWayDrop }
    public interface ITraversalSurface
    {
        int SurfaceId { get; }
        TraversalSurfaceKind Kind { get; }
        Vector3 Target { get; }
    }
    public readonly struct PlayerMovementSample
    {
        public PlayerMovementSample(EntityId id, long tick, Vector3 position, Vector3 velocity, Vector3 eyePosition, float headingDegrees, Vector2 headLookDelta, bool lookBack, MovementState movementState, float inputLockSeconds, float slideTurnRateDegrees = 0f, bool isCrouched = false, bool isSprinting = false)
        {
            Id = id;
            Tick = tick;
            Position = position;
            Velocity = velocity;
            EyePosition = eyePosition;
            HeadingDegrees = headingDegrees;
            HeadLookDelta = headLookDelta;
            LookBack = lookBack;
            MovementState = movementState;
            InputLockSeconds = inputLockSeconds;
            SlideTurnRateDegrees = slideTurnRateDegrees;
            IsCrouched = isCrouched;
            IsSprinting = isSprinting;
        }
        public EntityId Id { get; }
        public long Tick { get; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public Vector3 EyePosition { get; }
        public float HeadingDegrees { get; }
        public Vector2 HeadLookDelta { get; }
        public bool LookBack { get; }
        public MovementState MovementState { get; }
        public float InputLockSeconds { get; }
        public float SlideTurnRateDegrees { get; }
        public bool IsCrouched { get; }
        public bool IsSprinting { get; }
    }
    public readonly struct PlayerTraversalFact
    {
        public PlayerTraversalFact(EntityId id, long tick, TraversalKind kind, bool succeeded, Vector3 direction, float duration)
        {
            Id = id;
            Tick = tick;
            Kind = kind;
            Succeeded = succeeded;
            Direction = direction;
            Duration = duration;
        }
        public EntityId Id { get; }
        public long Tick { get; }
        public TraversalKind Kind { get; }
        public bool Succeeded { get; }
        public Vector3 Direction { get; }
        public float Duration { get; }
    }
    public readonly struct InventorySnapshot
    {
        public InventorySnapshot(string slotOne, string slotTwo)
        {
            SlotOne = slotOne;
            SlotTwo = slotTwo;
        }
        public string SlotOne { get; }
        public string SlotTwo { get; }
    }
}
