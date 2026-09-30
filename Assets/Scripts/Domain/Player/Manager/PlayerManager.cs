// ============================================================================
// PlayerManager.cs
// ============================================================================
// PURPOSE:
//   Sequences probe, decision, movement, committed state and published Player facts.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Player (Entity system).
// KEY RESPONSIBILITIES:
//   - Sequence Player input, probes, resolved movement and committed facts.
//   - Route health, shield, consumables, active effects and external motion.
//   Contact/chase perk APIs admit facts only; Session owns foreign-system routing.
//   - Preserve the caught pose and revive there without restarting the floor.
//   - Push independent hunter pass-through and publish immunity through grace facts.
//   - Own initialization, read-only state access and symmetric recovery cleanup.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned; Initialize creates a fresh life, Teardown clears it. No competing FixedUpdate loop; Run Session supplies each tick.
//   Hits use the latest run tick; callers with a newer tick must advance recovery first.
//   Disable/teardown cancels recovery; death publishes an empty grace interval rather than a lingering effect.
//   SetActiveEffects follows Initialize; null clears on the next tick. Config resolves via the Driver.
//   External commands accumulate until the next Tick, including during grace; callers
//   supply acceleration time once per simulation interval, never per render frame.
//   No other Domain system or Presentation system is referenced.
//   Floor assembly captures ReadOnlyShieldState.Shield before despawn and restores
//   it after Initialize. BeginFloorHealth and health regeneration do not modify it.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    [RequireComponent(typeof(PlayerDriver))]
    public sealed class PlayerManager : MonoBehaviour, IEntityHandle
    {
        [SerializeField] private PlayerDriver _driver;
        [SerializeField] private PlayerEffectConfig _effectConfig;
        private PlayerBehaviorState _state;
        private PlayerController _controller;
        private PlayerProfile _profile;
        public EntityId Id => _state?.Id ?? EntityId.None;
        public IReadOnlyPlayerState ReadOnlyState => _state;
        public IReadOnlyPlayerShieldState ReadOnlyShieldState => _state;
        public float ShieldCapacity => _controller?.ShieldCapacity ?? 0f;
        public event Action<EntityId, float> OnShieldChanged;
        public bool IsUngrabbable => _state?.IsUngrabbable ?? false;
        public bool RevivalDamageImmune => _state?.RevivalDamageImmune ?? false;
        public bool RevivalCollisionGraceActive => _state?.RevivalCollisionGraceActive ?? false;
        public PlayerMovementSample LastMovementSample => _state?.LastMovementSample ?? default;
        public InputProbeRecord LastProbeRecord => _state?.LastProbeRecord ?? default;
        public IReadOnlyList<PlayerTraversalFact> LastTraversalFacts => _state?.LastTraversalFacts ?? Array.Empty<PlayerTraversalFact>();
        public event Action<EntityId, float, float> OnHealthChanged;
        public event Action<EntityId, Vector3> OnDied;
        public event Action<EntityId, bool> OnLookBackChanged;
        public event Action<PlayerMovementSample> OnMovementSample;
        public event Action<PlayerTraversalFact> OnTraversal;
        // id, tick, kind, normalized progress, still traversing; final/cancel ticks publish false.
        public event Action<EntityId, long, TraversalKind, float, bool> OnTraversalProgress;
        // id, tick, duration in seconds; emitted once per stumble, including airborne failures.
        public event Action<EntityId, long, float> OnStumbled;
        public event Action<InputProbeRecord> InputProbeRecorded;
        public event Action<GraceWindowFact> OnGraceStarted;
        public event Action<GraceWindowFact> OnGraceEnded;
        public event Action<EntityId, HitSeverity, HitSource> OnHitAbsorbedByGrace;
        public event Action<NoiseEvent> OnHeartbeat;
        public event Action<EntityId, int, int, long> OnDoorLatched;

        private void Awake() { if (_driver == null) _driver = GetComponent<PlayerDriver>(); }
        public void Initialize(PlayerProfile profile, EntityContext context)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!context.Id.IsValid || context.Random == null) throw new ArgumentException("Player requires an assigned id and shared random source.");
            EndRecovery();
            if (_driver == null) _driver = GetComponent<PlayerDriver>();
            _driver.Initialize();
            _effectConfig = _driver.ResolveEffectConfig(_effectConfig);
            _profile = profile;
            _state = new PlayerBehaviorState();
            _controller = new PlayerController(_state, profile, context.Random, _effectConfig);
            _controller.Reset(context.Id, _driver.Position, _driver.Heading, _driver.FixedDeltaTime);
        }

        public void Tick(InputFrame frame, float dt, long tick)
        {
            if (_controller == null) return;
            bool wasLookingBack = _state.LookBack;
            float previousHealth = _state.Health;
            float previousMaximum = _state.MaxHealth;
            bool wasAlive = _state.IsAlive;
            AdvanceRecovery(tick);
            MovementProbe probe = _driver.Probe(_profile.LedgeReach, _profile.LedgeMinimumHeight,
                _profile.LedgeMaximumHeight, _profile.LedgeChestHeight);
            PlayerTickResult result = _controller.Tick(frame, probe, dt, tick);
            if (_controller.TakeHeartbeat(out NoiseEvent heartbeat)) OnHeartbeat?.Invoke(heartbeat);
            PlayerMoveResult movement = !_state.IsAlive
                ? new PlayerMoveResult(_state.Position, Vector3.zero, _state.Grounded, false)
                : result.Traversing
                ? _driver.MoveTraversal(result.TraversalStart, result.TraversalTarget, result.TraversalProgress,
                    result.TraversalHeight, _state.Velocity, _state.HeadingDegrees, dt, _controller.MaximumMovementSpeed, result.TraversalOffset)
                : _driver.Move(result.Displacement, _state.Velocity, result.Crouched, _state.HeadingDegrees, dt,
                    _state.MovementState == MovementState.Slide, _controller.SlideWallSpeedRetention);
            _controller.CommitPose(movement);
            var resolution = new MovementResolution(movement.Position, movement.Velocity, movement.Grounded, movement.Ceiling, _driver.EyePosition);
            var record = new InputProbeRecord(InputProbeRecord.CurrentSchemaVersion, tick, frame, probe, dt, resolution);
            _controller.CommitFrame(_driver.EyePosition, record, result.Facts);
            _driver.ShowMovement(_state.MovementState);
            if (previousHealth != _state.Health || previousMaximum != _state.MaxHealth)
                OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            if (wasAlive && !_state.IsAlive) { EndRecovery(); OnDied?.Invoke(Id, _state.Position); }
            if (wasLookingBack != _state.LookBack) OnLookBackChanged?.Invoke(Id, _state.LookBack);
            OnMovementSample?.Invoke(LastMovementSample);
            if (_state.TraversalSampleActive)
                OnTraversalProgress?.Invoke(Id, tick, _state.VaultKind, _state.VaultProgress, _state.MovementState == MovementState.Vault);
            if (_state.StumbleStartedSeconds > 0f) OnStumbled?.Invoke(Id, tick, _state.StumbleStartedSeconds);
            InputProbeRecorded?.Invoke(record);
            foreach (PlayerTraversalFact fact in LastTraversalFacts) OnTraversal?.Invoke(fact);
        }

        public bool ApplyHit(float damage, Vector3 killerPosition, HitSeverity severity = HitSeverity.Heavy, HitSource source = HitSource.Lunge)
        {
            if (_controller == null) return false;
            if (source == HitSource.Lunge && _state.RevivalCollisionGraceActive) return false;
            float previousShield = _state.Shield;
            PlayerHitResult result = _controller.ApplyHit(damage, severity);
            if (previousShield != _state.Shield) OnShieldChanged?.Invoke(Id, _state.Shield);
            _driver.SetGraceActive(_state.GraceActive || _state.RevivalCollisionGraceActive);
            if (result.GraceStarted.HasValue)
            {
                OnGraceStarted?.Invoke(result.GraceStarted.Value);
                if (!_state.GraceActive) OnGraceEnded?.Invoke(result.GraceStarted.Value);
            }
            if (result.AbsorbedByGrace) OnHitAbsorbedByGrace?.Invoke(Id, severity, source);
            if (result.Changed) OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            if (result.Died) OnDied?.Invoke(Id, killerPosition);
            return result.Changed;
        }

        public void HealOverTime(float perSecond, float movingSeconds)
        {
            if (_controller != null && _controller.HealOverTime(perSecond, movingSeconds))
                OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
        }
        public void SetConsumableSpeedMultiplier(float multiplier) => _controller?.SetConsumableSpeedMultiplier(multiplier);

        public void ClearSlows() => _controller?.ClearSlows();
        public void ReceiveChase(ChaseFact fact) => _controller?.ReceiveChase(fact);
        public void ReceiveMimic(MimicFact fact) => _controller?.ReceiveMimic(fact);
        public void ReceiveHeraldDeafen(HeraldDeafenFact fact) => _controller?.ReceiveHeraldDeafen(fact);
        public bool TryReboundFromHunter(EntityId hunter, Vector3 contactNormal)
        {
            if (_controller == null || !_controller.TryReboundFromHunter(hunter, contactNormal, out var fact)) return false;
            OnTraversal?.Invoke(fact);
            return true;
        }
        public bool TryLatchDoor(int roomId, int doorId)
        {
            if (_controller == null || !_controller.TryLatchDoor(roomId, doorId)) return false;
            OnDoorLatched?.Invoke(Id, roomId, doorId, _state.Tick);
            return true;
        }
        public bool ApplyRamHit(float damage, Vector3 killerPosition, Vector3 knockback, bool glancing)
        {
            if (_controller == null) return false;
            if (_controller.TryDeflectGlancingRam(glancing, knockback)) return false;
            bool changed = ApplyHit(damage, killerPosition);
            if (changed) _controller.ApplyExternalVelocity(knockback, ExternalMotionKind.Impulse);
            return changed;
        }
        public bool ReviveInPlace(float healthFraction)
        {
            if (_controller == null || !_controller.ReviveInPlace(healthFraction)) return false;
            _driver.Teleport(_state.Position, _state.HeadingDegrees, _state.Crouched);
            _driver.SetGraceActive(_state.RevivalCollisionGraceActive);
            if (_state.RevivalDamageImmune) OnGraceStarted?.Invoke(_state.RevivalImmunityWindow);
            OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            return true;
        }

        public bool RespawnAtFloorStart(float healthFraction)
        {
            if (_controller == null || !_controller.RespawnAtFloorStart(healthFraction)) return false;
            _driver.Teleport(_state.Position, _state.HeadingDegrees);
            _driver.SetGraceActive(false);
            OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            return true;
        }

        public bool GrantShield(float hitPoints)
        {
            if (_controller == null || !_controller.GrantShield(hitPoints)) return false;
            OnShieldChanged?.Invoke(Id, _state.Shield);
            return true;
        }
        public bool RestoreShield(float hitPoints)
        {
            if (_controller == null || !_controller.RestoreShield(hitPoints)) return false;
            OnShieldChanged?.Invoke(Id, _state.Shield);
            return true;
        }

        public void AdvanceRecovery(long tick)
        {
            if (_controller == null) return;
            GraceWindowFact? ended = _controller.AdvanceRecovery(tick);
            _driver.SetGraceActive(_state.GraceActive || _state.RevivalCollisionGraceActive);
            if (ended.HasValue) OnGraceEnded?.Invoke(ended.Value);
        }

        private void EndRecovery()
        {
            GraceWindowFact? ended = _controller?.EndRecovery();
            if (_driver != null) _driver.SetGraceActive(false);
            if (ended.HasValue) OnGraceEnded?.Invoke(ended.Value);
        }

        public void SetLookBackEnabled(bool enabled) { _controller?.SetLookBackEnabled(enabled); }
        public void ApplyExternalVelocity(Vector3 velocity, ExternalMotionKind kind)
        { _controller?.ApplyExternalVelocity(velocity, kind); }
        public void ApplyExternalAcceleration(Vector3 acceleration, float deltaSeconds)
        { _controller?.ApplyExternalAcceleration(acceleration, deltaSeconds); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { _controller?.SetActiveEffects(effects); }
        public void SetHealthRecoveryEffects(float regenerationMultiplier = 1f, float floorStartHealthFraction = 1f)
        { _controller?.SetHealthRecoveryEffects(regenerationMultiplier, floorStartHealthFraction); }
        public void BeginFloorHealth(float maximumHealth, float movementMultiplier)
        {
            if (_controller == null) return;
            EndRecovery();
            _controller.BeginFloorHealth(maximumHealth, movementMultiplier);
            OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
        }
        public void ApplyRunModifiers(float health, float maximumHealth, float movementMultiplier)
        {
            if (_controller == null) return;
            PlayerHitResult result = _controller.ApplyRunModifiers(health, maximumHealth, movementMultiplier);
            if (result.Died) EndRecovery();
            if (result.Changed) OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            if (result.Died) OnDied?.Invoke(Id, _state.Position);
        }
        public void SetMovementEffects(float footstepNoiseMultiplier, float reboundCooldownMultiplier, float grabSpeedMultiplier)
        { _controller?.SetMovementEffects(footstepNoiseMultiplier, reboundCooldownMultiplier, grabSpeedMultiplier); }
        public void SetGrabSpeedMultiplier(float multiplier) { _controller?.SetGrabSpeedMultiplier(multiplier); }
        public void SetTrapSpeedMultiplier(float multiplier) { _controller?.SetTrapSpeedMultiplier(multiplier); }
        public void ApplyWebSlow(WebHitFact fact) { _controller?.ApplyWebSlow(fact); }
        public void ApplyLungeHit(Vector3 killerPosition) { if (_profile != null) ApplyHit(_profile.LungeDamage, killerPosition); }
        public void Teardown()
        {
            EndRecovery();
            if (_driver != null) _driver.Teardown();
            _controller = null;
            _state = null;
            _profile = null;
        }
        private void OnDisable() { EndRecovery(); }
        private void OnDestroy() { Teardown(); }
    }
}
