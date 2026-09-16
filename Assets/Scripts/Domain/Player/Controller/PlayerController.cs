// ============================================================================
// PlayerController.cs
// ============================================================================
// PURPOSE:
//   Decides movement verbs and damage from recorded input and physics facts.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
//   - Admit traversal endpoints only within the chosen lock's effective speed budget.
//   - Retain committed walkable contact after uphill landings and use hold-to-sprint input.
//   - Preserve held free-look during vault/mantle locks without changing their captured trajectories.
//   - Keep held free-look independent of movement; bound slide turns without restoring collision-lost speed.
//   - Commit supported held crouch and achieved grounded sprint facts from input and resolved motion.
//   - Cancel slide propulsion on a fresh jump press, retaining a low capsule when blocked.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Pure rules with injected time/randomness. Reset clears a pooled life; hard stumble does not lock input.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public sealed class PlayerController
    {
        private readonly PlayerBehaviorState _state;
        private readonly PlayerProfile _profile;
        public float MaximumMovementSpeed => EffectiveMaximumSpeed();
        public PlayerController(PlayerBehaviorState state, PlayerProfile profile, System.Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (random == null) throw new ArgumentNullException(nameof(random));
        }

        public void Reset(EntityId id, Vector3 position, float headingDegrees)
        {
            _state.Id = id;
            _state.Position = position;
            _state.Velocity = Vector3.zero;
            _state.HeadingDegrees = headingDegrees;
            _state.Forward = Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
            _state.MovementSpeedMultiplier = 1f;
            _state.FootstepNoiseMultiplier = _state.ReboundCooldownMultiplier = _state.GrabSpeedMultiplier = 1f;
            _state.SlideTurnRateDegrees = _state.MovementDeltaTime = 0f;
            _state.PreviousHorizontalVelocity = Vector3.zero;
            _state.SprintSpeed = _profile.SprintSpeed;
            _state.MaxDesignSpeed = _profile.MaxDesignSpeed;
            _state.Health = _profile.MaximumHealth;
            _state.MaxHealth = _profile.MaximumHealth;
            _state.HealthState = PlayerHealthState.Healthy;
            _state.MovementState = MovementState.Ground;
            _state.LookBack = _state.Grounded = _state.Crouched = _state.IsSprinting = false;
            _state.Tick = 0;
            _state.HeadLookDelta = Vector2.zero;
            _state.JumpBufferRemaining = _state.ReboundJumpRemaining = _state.CoyoteRemaining = 0f;
            _state.ReboundCooldownRemaining = _state.SlideRemaining = _state.StumbleRemaining = 0f;
            _state.VaultRemaining = _state.InputLockSeconds = _state.LandingImpactSpeed = 0f;
            _state.SlideEntrySpeed = _state.FootstepRemaining = 0f;
            _state.LastReboundWall = _state.NoiseCount = _state.NextNoiseIndex = 0;
            _state.VaultTarget = _state.VaultExitVelocity = Vector3.zero;
            _state.VaultStart = Vector3.zero;
            _state.VaultDuration = 0f;
            _state.VaultHeight = 0f;
            _state.VaultKind = TraversalKind.None;
            _state.VaultCompletionPending = _state.PreserveVelocityOnCommit = false;
            _state.CompletedTraversal = null;
            _state.VaultAttemptResolvedForPress = false;
            _state.NoiseRing = new NoiseEvent[Math.Max(1, _profile.NoiseCapacity)];
            _state.RecentNoises = Array.Empty<NoiseEvent>();
            _state.Inventory = new InventorySnapshot(string.Empty, string.Empty);
            _state.LastMovementSample = default;
            _state.LastProbeRecord = default;
            _state.LastTraversalFacts = Array.Empty<PlayerTraversalFact>();
        }

        public PlayerTickResult Tick(InputFrame frame, MovementProbe probe, float deltaTime, long tick)
        {
            if (!Finite(deltaTime) || deltaTime <= 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            var facts = new List<PlayerTraversalFact>(2);
            _state.Tick = tick;
            _state.MovementDeltaTime = deltaTime;
            _state.PreviousHorizontalVelocity = Horizontal(_state.Velocity);
            _state.SlideTurnRateDegrees = 0f;
            _state.LastTraversalFacts = Array.Empty<PlayerTraversalFact>();
            _state.InputLockSeconds = 0f;
            _state.IsSprinting = false;
            _state.PreserveVelocityOnCommit = false;
            _state.CompletedTraversal = null;
            AdvanceTimers(deltaTime);
            if (!_state.IsAlive)
            {
                _state.Velocity = Vector3.zero;
                _state.HeadLookDelta = Vector2.zero;
                _state.LookBack = false;
                return new PlayerTickResult(Vector3.zero, _state.Crouched, facts.ToArray());
            }
            ApplyLook(frame);
            if ((frame.Pressed & InputButtons.Jump) != 0)
            {
                _state.VaultAttemptResolvedForPress = false;
                _state.JumpBufferRemaining = _profile.JumpBuffer;
                _state.ReboundJumpRemaining = _profile.ReboundJumpWindow;
            }
            if (_state.MovementState == MovementState.Vault)
                return ContinueVault(deltaTime, facts);

            // A slope can project a landing velocity upward without starting a jump.
            // The last resolved contact distinguishes that support from a rising jump.
            bool grounded = probe.Grounded && (_state.MovementState != MovementState.Air
                || _state.Grounded || _state.Velocity.y <= 0f);
            _state.Grounded = grounded;
            if (grounded)
            {
                if (_state.MovementState == MovementState.Air) Land(facts);
                _state.CoyoteRemaining = _profile.CoyoteTime;
                _state.LastReboundWall = 0;
            }
            else if (_state.MovementState != MovementState.Air)
                _state.MovementState = MovementState.Air;

            bool cancelSlide = _state.MovementState == MovementState.Slide
                && (frame.Pressed & InputButtons.Jump) != 0;
            if (cancelSlide)
            {
                _state.SlideRemaining = 0f;
                _state.MovementState = grounded ? MovementState.Ground : MovementState.Air;
                // A blocked cancellation ends the forced slide, but cannot grow the
                // capsule or leave a delayed jump waiting for the ceiling to clear.
                if (probe.StandingBlocked) ConsumeJump();
            }

            bool jump = _state.JumpBufferRemaining > 0f;
            if (jump && probe.VaultCandidate && !_state.VaultAttemptResolvedForPress)
            {
                if (CanVault(probe))
                {
                    BeginVault(probe, facts);
                    return ContinueVault(deltaTime, facts);
                }
                bool mantle = probe.VaultHeight > _profile.VaultMaximumHeight;
                _state.VaultAttemptResolvedForPress = true;
                facts.Add(Fact(mantle ? TraversalKind.Mantle : TraversalKind.Vault, false,
                    _state.Forward, mantle ? _profile.MantleDuration : _profile.VaultDuration));
            }
            if (_state.MovementState == MovementState.Air && CanRebound(probe))
            {
                _state.Velocity = Vector3.Reflect(_state.Velocity, probe.WallNormal.normalized)
                    + Vector3.up * _profile.ReboundUpwardBoost;
                _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
                _state.LastReboundWall = probe.WallId;
                _state.ReboundCooldownRemaining = _profile.ReboundCooldown * _state.ReboundCooldownMultiplier;
                ConsumeJump();
                facts.Add(Fact(TraversalKind.Rebound, true, _state.Velocity.normalized, _profile.ReboundCooldown));
                AddNoise(_profile.TraversalLoudness);
            }
            else if (jump && _state.CoyoteRemaining > 0f && !probe.StandingBlocked)
            {
                _state.Velocity = new Vector3(_state.Velocity.x, _profile.JumpSpeed, _state.Velocity.z);
                _state.MovementState = MovementState.Air;
                _state.Grounded = grounded = false;
                _state.CoyoteRemaining = 0f;
                ConsumeJump();
                facts.Add(Fact(TraversalKind.Jump, true, _state.Forward, 0f));
            }
            else if (!cancelSlide && grounded && (frame.Pressed & InputButtons.Crouch) != 0
                && Horizontal(_state.Velocity).magnitude >= _profile.SlideMinimumSpeed
                && _state.MovementState != MovementState.Slide)
            {
                Vector3 horizontal = Horizontal(_state.Velocity);
                _state.SlideEntrySpeed = Mathf.Min(EffectiveMaximumSpeed(), horizontal.magnitude + _profile.SlideBoost);
                _state.Velocity = horizontal.normalized * _state.SlideEntrySpeed;
                _state.SlideRemaining = _profile.SlideDuration;
                _state.MovementState = MovementState.Slide;
                facts.Add(Fact(TraversalKind.Slide, true, horizontal.normalized, _profile.SlideDuration));
                AddNoise(_profile.SlideLoudness);
            }

            MoveHorizontal(frame, probe, deltaTime);
            if (_state.MovementState == MovementState.Air)
                _state.Velocity += Vector3.down * _profile.Gravity * deltaTime;
            else _state.Velocity = Horizontal(_state.Velocity);
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            _state.Crouched = _state.MovementState == MovementState.Slide || probe.StandingBlocked
                || (_state.Grounded && (frame.Held & InputButtons.Crouch) != 0);
            if (_state.Grounded && Horizontal(_state.Velocity).magnitude >= 0.5f
                && _state.FootstepRemaining <= 0f && _state.MovementState != MovementState.Slide)
            {
                bool sprinting = (frame.Held & InputButtons.Sprint) != 0;
                AddNoise(sprinting ? _profile.SprintLoudness : _profile.WalkingLoudness * _state.FootstepNoiseMultiplier);
                _state.FootstepRemaining = _profile.FootstepInterval;
            }
            return new PlayerTickResult(_state.Velocity * deltaTime, _state.Crouched, facts.ToArray());
        }

        public void CommitPose(PlayerMoveResult result)
        {
            if (!Finite(result.Position) || !Finite(result.Velocity)) throw new ArgumentException("A resolved pose must be finite.");
            if (result.Grounded && _state.MovementState == MovementState.Air)
                _state.LandingImpactSpeed = Mathf.Max(_state.LandingImpactSpeed, -_state.Velocity.y);
            _state.Position = result.Position;
            Vector3 resolvedHorizontal = Horizontal(result.Velocity);
            if (_state.MovementState == MovementState.Slide && _state.MovementDeltaTime > 0f
                && resolvedHorizontal.sqrMagnitude > 0.01f && _state.PreviousHorizontalVelocity.sqrMagnitude > 0.01f)
                _state.SlideTurnRateDegrees = Vector3.SignedAngle(_state.PreviousHorizontalVelocity, resolvedHorizontal, Vector3.up) / _state.MovementDeltaTime;
            _state.Velocity = _state.PreserveVelocityOnCommit ? _state.Velocity : result.Velocity;
            _state.Grounded = result.Grounded;
            if (result.Ceiling && _state.Velocity.y > 0f)
                _state.Velocity = Horizontal(_state.Velocity);
            if (_state.VaultCompletionPending)
            {
                _state.CompletedTraversal = Fact(_state.VaultKind,
                    Vector3.Distance(_state.Position, _state.VaultTarget) <= _profile.VaultCompletionTolerance,
                    (_state.VaultTarget - _state.VaultStart).normalized, _state.VaultDuration);
                _state.VaultCompletionPending = false;
            }
        }

        public void CommitFrame(Vector3 eyePosition, InputProbeRecord record, PlayerTraversalFact[] facts)
        {
            _state.IsSprinting = AchievedSprinting(record);
            _state.LastMovementSample = new PlayerMovementSample(_state.Id, _state.Tick,
                _state.Position, record.Resolution.Present ? record.Resolution.Velocity : _state.Velocity,
                eyePosition, _state.HeadingDegrees,
                _state.HeadLookDelta, _state.LookBack, _state.MovementState, _state.InputLockSeconds, _state.SlideTurnRateDegrees, _state.Crouched, _state.IsSprinting);
            _state.LastProbeRecord = record;
            var resolved = new List<PlayerTraversalFact>(facts ?? Array.Empty<PlayerTraversalFact>());
            if (_state.CompletedTraversal.HasValue) resolved.Add(_state.CompletedTraversal.Value);
            _state.LastTraversalFacts = resolved.AsReadOnly();
        }

        private bool AchievedSprinting(InputProbeRecord record)
        {
            if (!_state.IsAlive || _state.Crouched || _state.MovementState != MovementState.Ground
                || !record.Resolution.Present || !record.Resolution.Grounded
                || (record.Input.Held & InputButtons.Sprint) == 0) return false;
            Vector2 move = record.Input.Move;
            Vector3 horizontal = Horizontal(record.Resolution.Velocity);
            if (!Finite(move.x) || !Finite(move.y) || !Finite(horizontal)) return false;
            Vector3 desired = _state.Forward * move.y + Vector3.Cross(Vector3.up, _state.Forward) * move.x;
            float walkingSpeed = _profile.WalkSpeed * _state.MovementSpeedMultiplier * InjuryMultiplier() * _state.GrabSpeedMultiplier;
            // Acceleration below walking pace, idle intent and wall-arrested motion
            // are not an achieved sprint. Modifier-scaled walking pace keeps grabs meaningful.
            return Vector3.Dot(horizontal, desired) > 0f && horizontal.magnitude > walkingSpeed + 0.0001f;
        }

        public PlayerHitResult ApplyHit(float damage)
        {
            if (!_state.IsAlive || !Finite(damage) || damage <= 0f) return default;
            _state.Health = Mathf.Max(0f, _state.Health - damage);
            _state.HealthState = HealthTier(_state.Health);
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            if (!_state.IsAlive) { _state.IsSprinting = false; _state.Velocity = Vector3.zero; _state.LookBack = false; _state.HeadLookDelta = Vector2.zero; }
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
            return new PlayerHitResult(true, !_state.IsAlive);
        }

        public PlayerHitResult ApplyRunModifiers(float health, float maximumHealth, float movementMultiplier)
        {
            if (!Finite(health) || !Finite(maximumHealth) || maximumHealth <= 0f
                || !Finite(movementMultiplier) || movementMultiplier <= 0f
                || !Finite(_profile.SprintSpeed * movementMultiplier)
                || !Finite(_profile.MaxDesignSpeed * movementMultiplier))
                throw new ArgumentOutOfRangeException(nameof(movementMultiplier), "Run health and speed values must be finite and positive where required.");
            bool wasAlive = _state.IsAlive;
            float previousHealth = _state.Health, previousMaximum = _state.MaxHealth;
            _state.MaxHealth = maximumHealth;
            _state.Health = Mathf.Clamp(health, 0f, maximumHealth);
            _state.HealthState = HealthTier(_state.Health);
            _state.MovementSpeedMultiplier = movementMultiplier;
            _state.SprintSpeed = _profile.SprintSpeed;
            _state.MaxDesignSpeed = _profile.MaxDesignSpeed * movementMultiplier;
            if (!_state.IsAlive) _state.IsSprinting = false;
            _state.Velocity = _state.IsAlive ? ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed()) : Vector3.zero;
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
            return new PlayerHitResult(previousHealth != _state.Health || previousMaximum != _state.MaxHealth,
                wasAlive && !_state.IsAlive);
        }

        public void Replay(InputProbeRecord record)
        {
            if (record.SchemaVersion != InputProbeRecord.CurrentSchemaVersion || !record.Resolution.Present)
                throw new ArgumentException("Exact replay requires the supported schema and recorded movement resolution.");
            PlayerTickResult result = Tick(record.Input, record.Probe, record.DeltaTime, record.Tick);
            MovementResolution resolution = record.Resolution;
            CommitPose(new PlayerMoveResult(resolution.Position, resolution.Velocity, resolution.Grounded, resolution.Ceiling));
            CommitFrame(resolution.EyePosition, record, result.Facts);
        }

        private void AdvanceTimers(float dt)
        {
            _state.JumpBufferRemaining = Mathf.Max(0f, _state.JumpBufferRemaining - dt);
            _state.ReboundJumpRemaining = Mathf.Max(0f, _state.ReboundJumpRemaining - dt);
            _state.CoyoteRemaining = Mathf.Max(0f, _state.CoyoteRemaining - dt);
            _state.ReboundCooldownRemaining = Mathf.Max(0f, _state.ReboundCooldownRemaining - dt);
            _state.StumbleRemaining = Mathf.Max(0f, _state.StumbleRemaining - dt);
            _state.FootstepRemaining = Mathf.Max(0f, _state.FootstepRemaining - dt);
        }

        public void SetMovementEffects(float footstepNoiseMultiplier, float reboundCooldownMultiplier, float grabSpeedMultiplier)
        {
            _state.FootstepNoiseMultiplier = Finite(footstepNoiseMultiplier) ? Mathf.Clamp01(footstepNoiseMultiplier) : 1f;
            _state.ReboundCooldownMultiplier = Finite(reboundCooldownMultiplier) ? Mathf.Clamp(reboundCooldownMultiplier, 0.65f, 1f) : 1f;
            SetGrabSpeedMultiplier(grabSpeedMultiplier);
        }

        public void SetGrabSpeedMultiplier(float multiplier)
        {
            _state.GrabSpeedMultiplier = Finite(multiplier) ? Mathf.Clamp(multiplier, 0.25f, 1f) : 1f;
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
        }

        private void ApplyLook(InputFrame frame)
        {
            _state.LookBack = (frame.Held & InputButtons.LookBack) != 0;
            Vector2 look = Finite(frame.LookDelta.x) && Finite(frame.LookDelta.y) ? frame.LookDelta : Vector2.zero;
            if (!_state.LookBack) _state.HeadingDegrees = Mathf.Repeat(_state.HeadingDegrees + look.x, 360f);
            _state.Forward = Quaternion.Euler(0f, _state.HeadingDegrees, 0f) * Vector3.forward;
            _state.HeadLookDelta = _state.LookBack ? look : new Vector2(0f, look.y);
        }

        private void MoveHorizontal(InputFrame frame, MovementProbe probe, float dt)
        {
            Vector3 horizontal = Horizontal(_state.Velocity);
            if (_state.MovementState == MovementState.Slide)
            {
                _state.SlideRemaining = Mathf.Max(0f, _state.SlideRemaining - dt);
                float duration = Mathf.Max(0.0001f, _profile.SlideDuration);
                float speed = Mathf.Lerp(EffectiveSprintSpeed(), _state.SlideEntrySpeed, _state.SlideRemaining / duration);
                // Never recover speed lost to collision, damage or a grab. Steering rotates only.
                speed = Mathf.Min(speed, horizontal.magnitude);
                float steering = Finite(frame.Move.x) ? Mathf.Clamp(frame.Move.x, -1f, 1f) : 0f;
                float rate = Mathf.Min(Mathf.Max(0f, _profile.SlideMaximumTurnRate),
                    Mathf.Max(0f, _profile.SlideLateralAcceleration) / Mathf.Max(0.1f, speed) * Mathf.Rad2Deg);
                horizontal = Quaternion.AngleAxis(steering * rate * dt, Vector3.up) * horizontal.normalized * speed;
                _state.SlideTurnRateDegrees = steering * rate;
                if (_state.SlideRemaining <= 0f && !probe.StandingBlocked)
                    _state.MovementState = MovementState.Ground;
            }
            else
            {
                Vector2 input = Finite(frame.Move.x) && Finite(frame.Move.y) ? Vector2.ClampMagnitude(frame.Move, 1f) : Vector2.zero;
                Vector3 right = Vector3.Cross(Vector3.up, _state.Forward);
                Vector3 direction = _state.Forward * input.y + right * input.x;
                if (_state.MovementState == MovementState.Air)
                {
                    float cap = horizontal.magnitude;
                    horizontal = Vector3.ClampMagnitude(horizontal + direction * _profile.AirAcceleration * dt, cap);
                }
                else
                {
                    float speed = (frame.Held & InputButtons.Sprint) != 0
                        ? EffectiveSprintSpeed() : _profile.WalkSpeed * _state.MovementSpeedMultiplier * InjuryMultiplier() * _state.GrabSpeedMultiplier;
                    // Preserve a landing's retained momentum on its transition tick.
                    bool landed = _state.LandingImpactSpeed < 0f;
                    if (!landed) horizontal = Vector3.MoveTowards(horizontal, direction * speed,
                        (direction.sqrMagnitude > 0f ? _profile.GroundAcceleration : _profile.GroundFriction) * dt);
                    if (_state.MovementState == MovementState.Stumble && _state.StumbleRemaining <= 0f)
                        _state.MovementState = MovementState.Ground;
                }
            }
            if (_state.LandingImpactSpeed < 0f) _state.LandingImpactSpeed = 0f;
            _state.Velocity = new Vector3(horizontal.x, _state.Velocity.y, horizontal.z);
        }

        private void Land(List<PlayerTraversalFact> facts)
        {
            float impact = Mathf.Max(_state.LandingImpactSpeed, -_state.Velocity.y);
            float retention = impact > _profile.HardLandingThreshold ? _profile.HardLandingRetention
                : impact >= _profile.SoftLandingThreshold ? _profile.SoftLandingRetention : 1f;
            _state.StumbleRemaining = impact > _profile.HardLandingThreshold ? _profile.HardStumbleDuration
                : impact >= _profile.SoftLandingThreshold ? _profile.SoftStumbleDuration : 0f;
            _state.Velocity = Horizontal(_state.Velocity) * retention;
            _state.MovementState = _state.StumbleRemaining > 0f ? MovementState.Stumble : MovementState.Ground;
            _state.LandingImpactSpeed = -1f;
            facts.Add(Fact(TraversalKind.Land, true, Vector3.down, _state.StumbleRemaining));
            AddNoise(_profile.TraversalLoudness);
        }

        private bool CanVault(MovementProbe probe)
        {
            if (!Finite(probe.VaultHeight) || !Finite(probe.VaultClearance) || !Finite(probe.VaultTarget)
                || !(probe.VaultHeight >= _profile.VaultMinimumHeight && probe.VaultHeight <= _profile.MantleMaximumHeight)
                || probe.VaultClearance <= 0f || probe.StandingBlocked) return false;
            float duration = probe.VaultHeight > _profile.VaultMaximumHeight ? _profile.MantleDuration : _profile.VaultDuration;
            float maximumSpeed = EffectiveMaximumSpeed();
            Vector3 horizontal = Horizontal(probe.VaultTarget - _state.Position);
            float distance = horizontal.magnitude;
            float budget = maximumSpeed * duration;
            // This necessary travel bound does not replace swept collision or resolved completion.
            return Finite(duration) && duration > 0f && Finite(maximumSpeed) && maximumSpeed > 0f
                && Finite(horizontal) && Finite(distance) && Finite(budget) && budget > 0f && distance <= budget + 0.00001f;
        }

        private void BeginVault(MovementProbe probe, List<PlayerTraversalFact> facts)
        {
            bool mantle = probe.VaultHeight > _profile.VaultMaximumHeight;
            _state.VaultRemaining = mantle ? _profile.MantleDuration : _profile.VaultDuration;
            _state.VaultDuration = _state.VaultRemaining;
            _state.VaultHeight = probe.VaultHeight;
            _state.VaultStart = _state.Position;
            _state.VaultKind = mantle ? TraversalKind.Mantle : TraversalKind.Vault;
            _state.VaultTarget = probe.VaultTarget;
            _state.VaultExitVelocity = Horizontal(_state.Velocity);
            _state.MovementState = MovementState.Vault;
            _state.Crouched = false;
            // ApplyLook already captured the held head input. Traversal locks only
            // locomotion; its stored start/target and exit velocity remain unchanged.
            _state.Grounded = false;
            ConsumeJump();
            AddNoise(_profile.TraversalLoudness);
        }

        private PlayerTickResult ContinueVault(float dt, List<PlayerTraversalFact> facts)
        {
            float remaining = _state.VaultRemaining;
            _state.VaultRemaining = Mathf.Max(0f, remaining - dt);
            if (_state.VaultRemaining < 0.000001f) _state.VaultRemaining = 0f;
            _state.InputLockSeconds = remaining;
            _state.PreserveVelocityOnCommit = false;
            if (_state.VaultRemaining <= 0f)
            {
                _state.MovementState = MovementState.Air;
                _state.Velocity = _state.VaultExitVelocity;
                _state.VaultCompletionPending = true;
                _state.PreserveVelocityOnCommit = true;
            }
            float progress = 1f - _state.VaultRemaining / Mathf.Max(0.0001f, _state.VaultDuration);
            return new PlayerTickResult(Vector3.zero, false, facts.ToArray(), true,
                _state.VaultStart, _state.VaultTarget, progress, _state.VaultHeight);
        }

        private bool CanRebound(MovementProbe probe)
        {
            return _state.ReboundJumpRemaining > 0f && _state.ReboundCooldownRemaining <= 0f
                && probe.WallDetected && probe.WallId != 0 && probe.WallId != _state.LastReboundWall
                && Finite(probe.WallDistance) && probe.WallDistance >= 0f && probe.WallDistance <= _profile.ReboundDistance
                && Finite(probe.WallAngleDegrees) && probe.WallAngleDegrees >= 0f && probe.WallAngleDegrees <= _profile.ReboundAngle
                && Finite(probe.WallNormal) && probe.WallNormal.sqrMagnitude > 0.5f
                && Vector3.Dot(_state.Velocity, probe.WallNormal) < 0f;
        }

        private void ConsumeJump() { _state.JumpBufferRemaining = _state.ReboundJumpRemaining = 0f; }
        private PlayerHealthState HealthTier(float health)
            => health <= 0f ? PlayerHealthState.Dead
                : health <= _state.MaxHealth * (_profile.CriticalThreshold / _profile.MaximumHealth) ? PlayerHealthState.Critical
                : health <= _state.MaxHealth * (_profile.InjuredThreshold / _profile.MaximumHealth) ? PlayerHealthState.Injured : PlayerHealthState.Healthy;
        private float InjuryMultiplier() => _state.HealthState == PlayerHealthState.Injured || _state.HealthState == PlayerHealthState.Critical
            ? _profile.InjuredSpeedMultiplier : 1f;
        private float EffectiveMaximumSpeed() => _state.MaxDesignSpeed * InjuryMultiplier() * _state.GrabSpeedMultiplier;
        private float EffectiveSprintSpeed() => _state.SprintSpeed * _state.MovementSpeedMultiplier * InjuryMultiplier() * _state.GrabSpeedMultiplier;
        private PlayerTraversalFact Fact(TraversalKind kind, bool succeeded, Vector3 direction, float duration)
            => new PlayerTraversalFact(_state.Id, _state.Tick, kind, succeeded, direction, duration);
        private void AddNoise(float loudness)
        {
            var noise = new NoiseEvent(_state.Id, _state.Position, loudness, _state.Tick);
            _state.NoiseRing[_state.NextNoiseIndex] = noise;
            _state.NextNoiseIndex = (_state.NextNoiseIndex + 1) % _state.NoiseRing.Length;
            _state.NoiseCount = Math.Min(_state.NoiseCount + 1, _state.NoiseRing.Length);
            var ordered = new NoiseEvent[_state.NoiseCount];
            int first = (_state.NextNoiseIndex - _state.NoiseCount + _state.NoiseRing.Length) % _state.NoiseRing.Length;
            for (int i = 0; i < ordered.Length; i++) ordered[i] = _state.NoiseRing[(first + i) % _state.NoiseRing.Length];
            _state.RecentNoises = Array.AsReadOnly(ordered);
        }
        private static Vector3 Horizontal(Vector3 value) => new Vector3(value.x, 0f, value.z);
        private static Vector3 ClampHorizontal(Vector3 value, float maximum)
        {
            Vector3 horizontal = Vector3.ClampMagnitude(Horizontal(value), Mathf.Max(0f, maximum));
            return new Vector3(horizontal.x, value.y, horizontal.z);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
