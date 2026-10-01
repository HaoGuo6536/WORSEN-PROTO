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
//   - Retain committed pose, movement/traversal timers and bounded noise history.
//   - Store health, shield, regeneration and independent movement effects.
//   - Retain hit recovery and independent end-exclusive revival protection intervals.
//   - Expose read-only grab and revival protection without foreign state writes.
//   - Store pending external motion and committed replay/publication facts.
//   Chase/contact perk bookkeeping is isolated in PlayerPerkBehaviorState.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Passive per-entity data. PlayerController.Reset replaces every value when a pooled life begins.
//   Crouched is a legacy read-only name for Slide's reduced capsule, never a held posture.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public sealed class PlayerBehaviorState : IReadOnlyPlayerShieldState, IReadOnlyPlayerEffectState, IReadOnlyPlayerRevivalState
    {
        internal PlayerPerkBehaviorState Perks { get; } = new PlayerPerkBehaviorState();
        internal readonly Dictionary<EntityId, long> MimicHolds = new Dictionary<EntityId, long>();
        internal readonly Dictionary<EntityId, long> MimicTicks = new Dictionary<EntityId, long>();
        internal readonly Dictionary<EntityId, long> HeraldTicks = new Dictionary<EntityId, long>();
        internal long PreventRunningEndTick;
        public IReadOnlyActiveEffects ActiveEffects { get; set; }
        public ActiveEffects AppliedEffects { get; set; }
        public float BaseMaximumHealth { get; set; }
        public bool FloorHealthPending { get; set; }
        public float FloorStartHealth { get; set; }
        public float StoredMomentumSpeed { get; set; }
        public float StoredMomentumRemaining { get; set; }
        public bool LowProfileEnabled { get; set; }
        public bool IsUngrabbable => RevivalDamageImmune || (IsAlive && LowProfileEnabled && MovementState == MovementState.Slide);
        public bool RevivalCollisionGraceActive => IsAlive && Tick < RevivalCollisionEndTick;
        public bool RevivalDamageImmune => IsAlive && Tick < RevivalImmunityWindow.EndTick;
        internal long RevivalCollisionEndTick;
        internal GraceWindowFact RevivalImmunityWindow;
        public EntityId Id { get; set; }
        public Vector3 Position { get; set; }
        internal Vector3 FloorStartPosition;
        internal float FloorStartHeading;
        internal float ConsumableSpeedMultiplier = 1f;
        public Vector3 Velocity { get; set; }
        public Vector3 PendingExternalVelocity { get; set; }
        public Vector3 Forward { get; set; } = Vector3.forward;
        public float HeadingDegrees { get; set; }
        public float MovementSpeedMultiplier { get; set; } = 1f;
        // Nominal archetype speed is the Hunter reference; player-only upgrades remain separate.
        public float SprintSpeed { get; set; }
        public float MaxDesignSpeed { get; set; }
        public float Health { get; set; }
        public float Shield { get; set; }
        public float MaxHealth { get; set; }
        public double RegenerationDelayRemaining { get; set; }
        public float RegenerationMultiplier { get; set; } = 1f;
        public float FloorStartHealthFraction { get; set; } = 1f;
        public PlayerHealthState HealthState { get; set; }
        public bool IsAlive => Health > 0f;
        public bool LookBack { get; set; }
        public bool LookBackEnabled { get; set; } = true;
        public float RecoveryTickSeconds { get; set; }
        public GraceWindowFact GraceWindow { get; set; }
        public bool GraceActive { get; set; }
        public long HitBoostEndTick { get; set; }
        public float HitBoostMultiplier { get; set; } = 1f;
        public MovementState MovementState { get; set; }
        public long Tick { get; set; }
        public IReadOnlyList<NoiseEvent> RecentNoises { get; set; } = Array.Empty<NoiseEvent>();
        public InventorySnapshot Inventory { get; set; }
        public Vector2 HeadLookDelta { get; set; }
        public bool Grounded { get; set; }
        public bool Crouched => MovementState == MovementState.Slide;
        public bool IsSprinting { get; set; }
        public float JumpBufferRemaining { get; set; }
        public bool VaultAttemptResolvedForPress { get; set; }
        public float ReboundJumpRemaining { get; set; }
        public float CoyoteRemaining { get; set; }
        public float ReboundCooldownRemaining { get; set; }
        public int LastReboundWall { get; set; }
        public float SlideRemaining { get; set; }
        public float SlideEntrySpeed { get; set; }
        public float SlideTurnRateDegrees { get; set; }
        public float MovementDeltaTime { get; set; }
        public Vector3 PreviousHorizontalVelocity { get; set; }
        public float FootstepNoiseMultiplier { get; set; } = 1f;
        public float ReboundCooldownMultiplier { get; set; } = 1f;
        public float GrabSpeedMultiplier { get; set; } = 1f;
        public float TrapSpeedMultiplier { get; set; } = 1f;
        public float WebSpeedMultiplier { get; set; } = 1f;
        public float WebSlowRemaining { get; set; }
        public float StumbleRemaining { get; set; }
        public float StumbleSpeedLimit { get; set; }
        public float StumbleStartedSeconds { get; set; }
        public float LedgeRegrabRemaining { get; set; }
        public float VaultProgress { get; set; }
        public bool TraversalSampleActive { get; set; }
        public Vector3 VaultSteeringOffset { get; set; }
        public float VaultRemaining { get; set; }
        public Vector3 VaultTarget { get; set; }
        public Vector3 VaultStart { get; set; }
        public float VaultDuration { get; set; }
        public float VaultHeight { get; set; }
        public TraversalKind VaultKind { get; set; }
        public int VaultSurfaceId { get; set; }
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
