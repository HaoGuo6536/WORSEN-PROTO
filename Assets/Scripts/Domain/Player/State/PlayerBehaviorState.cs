// ============================================================================
// PlayerBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one player life, including motion timers, health and a bounded noise history.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Store per-life movement, health and aggregate run modifiers without changing shared assets.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Passive per-entity data. PlayerController.Reset replaces every value when a pooled life begins.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public sealed class PlayerBehaviorState : IReadOnlyPlayerState
    {
        public EntityId Id { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 Forward { get; set; } = Vector3.forward;
        public float HeadingDegrees { get; set; }
        public float MovementSpeedMultiplier { get; set; } = 1f;
        // Nominal archetype speed is the Hunter reference; player-only upgrades remain separate.
        public float SprintSpeed { get; set; }
        public float MaxDesignSpeed { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public PlayerHealthState HealthState { get; set; }
        public bool IsAlive => Health > 0f;
        public bool LookBack { get; set; }
        public MovementState MovementState { get; set; }
        public long Tick { get; set; }
        public IReadOnlyList<NoiseEvent> RecentNoises { get; set; } = Array.Empty<NoiseEvent>();
        public InventorySnapshot Inventory { get; set; }
        public Vector2 HeadLookDelta { get; set; }
        public bool Grounded { get; set; }
        public bool Crouched { get; set; }
        public float JumpBufferRemaining { get; set; }
        public bool VaultAttemptResolvedForPress { get; set; }
        public float ReboundJumpRemaining { get; set; }
        public float CoyoteRemaining { get; set; }
        public float ReboundCooldownRemaining { get; set; }
        public int LastReboundWall { get; set; }
        public float SlideRemaining { get; set; }
        public float SlideEntrySpeed { get; set; }
        public float StumbleRemaining { get; set; }
        public float VaultRemaining { get; set; }
        public Vector3 VaultTarget { get; set; }
        public Vector3 VaultStart { get; set; }
        public float VaultDuration { get; set; }
        public float VaultHeight { get; set; }
        public TraversalKind VaultKind { get; set; }
        public bool PreserveVelocityOnCommit { get; set; }
        public bool VaultCompletionPending { get; set; }
        public PlayerTraversalFact? CompletedTraversal { get; set; }
        public Vector3 VaultExitVelocity { get; set; }
        public float InputLockSeconds { get; set; }
        public float LandingImpactSpeed { get; set; }
        public float FootstepRemaining { get; set; }
        public NoiseEvent[] NoiseRing { get; set; } = Array.Empty<NoiseEvent>();
        public int NoiseCount { get; set; }
        public int NextNoiseIndex { get; set; }
        public PlayerMovementSample LastMovementSample { get; set; }
        public InputProbeRecord LastProbeRecord { get; set; }
        public IReadOnlyList<PlayerTraversalFact> LastTraversalFacts { get; set; } = Array.Empty<PlayerTraversalFact>();
    }
}
