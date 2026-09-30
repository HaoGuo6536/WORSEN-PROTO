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
//   - Apply Session-timed healing/speed, clear slows and revive at the retained floor spawn.
//   - Emit soft/hard landing severity independently of the stumble duration.
//   - Compose trap and grab speed factors multiplicatively without sharing their lifetimes.
//   - Consume bounded external velocity once after locomotion, regardless of hit grace.
//   - Spend shield before health; never regenerate it or clear it at BeginFloorHealth.
//   - Snapshot active effects each tick; combine Player config rules through PlayerEffectUtility.
//   - Release vault momentum once, suppress slide noise and expose sliding grab protection.
//   - Reset floor health to its effective maximum and regenerate living players after accepted hits.
//   - Classify every movement noise; crouch changes posture, not speed or loudness.
//   - Keep traversal look/cancel live, steer its last third and reward fresh end-window jumps.
//   - Auto-grab checked untagged ledges, bend slides and enforce brief stumble speed cuts.
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
//   - Admit traversal endpoints only within the chosen lock's effective speed budget.
//   - Retain committed walkable contact after uphill landings and use hold-to-sprint input.
//   - Steer body heading during held look-back snaps without scanning; retain the captured base path.
//   - Absorb hits during grace and apply a non-stacking, severity-scaled recovery speed multiplier.
//   - Commit supported held crouch and achieved grounded sprint facts from input and resolved motion.
//   - Cancel slide propulsion on a fresh jump press, retaining a low capsule when blocked.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Pure rules with injected time/randomness. Reset clears a pooled life; hard stumble does not lock input.
//   Recovery uses end-exclusive run ticks, rounded up from seconds at Reset's injected fixed step.
//   The optional 60 Hz step preserves existing pure callers; the Manager supplies the actual engine step.
//   Regeneration advances only with Tick's delta time, not AdvanceRecovery or wall time.
//   Health hooks are neutral after Reset; configure them before BeginFloorHealth, after spawning.
//   Effects do not retime published grace or admitted traversal intervals. Heavy Legs cancels a live boost.
//   Floor reset reconciles next-tick effects once, preserving intervening damage and never reviving deaths.
//   External motion interrupts scripted traversal and uses normal swept movement.
//   Its next ticks use ordinary friction, gravity and locomotion caps; replay callers
//   must supply external commands at matching ticks, as they already do for hits.
//   No other Domain system or Presentation system is referenced.
//   Reset clears shield for a new life; assembly must restore the previous floor's
//   captured Shield after replacing a living Player, never after starting a new run.
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
        private readonly PlayerEffectConfig _effectConfig;
        public float MaximumMovementSpeed => EffectiveMaximumSpeed();
        public float ShieldCapacity => _state.IsAlive ? float.MaxValue - _state.Shield : 0f;
        public float SlideWallSpeedRetention => Effect(PlayerEffectStat.SlideRetention, _profile.SlideWallSpeedRetention);
        public PlayerController(PlayerBehaviorState state, PlayerProfile profile, System.Random random, PlayerEffectConfig effectConfig = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _effectConfig = effectConfig;
            if (random == null) throw new ArgumentNullException(nameof(random));
        }

        public void Reset(EntityId id, Vector3 position, float headingDegrees, float fixedDeltaTime = 1f / 60f)
        {
            if (!id.IsValid) throw new ArgumentException("Player requires a valid identity.", nameof(id));
            if (!Finite(fixedDeltaTime) || fixedDeltaTime <= 0f) throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            _state.Id = id;
            _state.ActiveEffects = null;
            _state.AppliedEffects = default;
            _state.BaseMaximumHealth = _profile.MaximumHealth;
            _state.FloorHealthPending = _state.LowProfileEnabled = false;
            _state.FloorStartHealth = _state.StoredMomentumSpeed = _state.StoredMomentumRemaining = 0f;
            _state.RecoveryTickSeconds = fixedDeltaTime;
            _state.GraceWindow = default;
            _state.GraceActive = false;
            _state.HitBoostEndTick = 0;
            _state.HitBoostMultiplier = 1f;
            _state.LookBackEnabled = true;
            _state.Position = position;
            _state.Velocity = Vector3.zero;
            _state.FloorStartPosition = position;
            _state.FloorStartHeading = headingDegrees;
            _state.WebSpeedMultiplier = _state.ConsumableSpeedMultiplier = 1f;
            _state.PendingExternalVelocity = Vector3.zero;
            _state.HeadingDegrees = headingDegrees;
            _state.Forward = Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;
            _state.MovementSpeedMultiplier = 1f;
            _state.FootstepNoiseMultiplier = _state.ReboundCooldownMultiplier = _state.GrabSpeedMultiplier = 1f;
            _state.TrapSpeedMultiplier = 1f;
            _state.SlideTurnRateDegrees = _state.MovementDeltaTime = 0f;
            _state.PreviousHorizontalVelocity = Vector3.zero;
            _state.SprintSpeed = _profile.SprintSpeed;
            _state.MaxDesignSpeed = _profile.MaxDesignSpeed;
            _state.Health = _profile.MaximumHealth;
            _state.Shield = 0f;
            _state.MaxHealth = _profile.MaximumHealth;
            _state.RegenerationDelayRemaining = 0d;
            _state.RegenerationMultiplier = _state.FloorStartHealthFraction = 1f;
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
            _state.VaultProgress = _state.LedgeRegrabRemaining = 0f;
            _state.StumbleSpeedLimit = _state.StumbleStartedSeconds = 0f;
            _state.TraversalSampleActive = false;
            _state.VaultSteeringOffset = Vector3.zero;
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
            AdvanceRecovery(tick);
            ApplyActiveEffects();
            _state.MovementDeltaTime = deltaTime;
            _state.PreviousHorizontalVelocity = Horizontal(_state.Velocity);
            _state.SlideTurnRateDegrees = 0f;
            _state.LastTraversalFacts = Array.Empty<PlayerTraversalFact>();
            _state.InputLockSeconds = 0f;
            _state.IsSprinting = false;
            _state.PreserveVelocityOnCommit = false;
            _state.CompletedTraversal = null;
            AdvanceTimers(deltaTime);
            _state.StumbleStartedSeconds = 0f;
            _state.TraversalSampleActive = false;
            if (!_state.IsAlive)
            {
                _state.PendingExternalVelocity = Vector3.zero;
                _state.Velocity = Vector3.zero;
                _state.HeadLookDelta = Vector2.zero;
                _state.LookBack = false;
                return new PlayerTickResult(Vector3.zero, _state.Crouched, facts.ToArray());
            }
            RegenerateHealth(deltaTime);
            ApplyLook(frame);
            bool externalMotion = _state.PendingExternalVelocity.sqrMagnitude > 0f;
            if (externalMotion && _state.MovementState == MovementState.Vault)
            {
                _state.TraversalSampleActive = true;
                _state.MovementState = MovementState.Air;
                _state.VaultRemaining = _state.CoyoteRemaining = 0f;
                _state.VaultCompletionPending = false;
                _state.LedgeRegrabRemaining = _profile.LedgeRegrabDelay;
            }
            if ((frame.Pressed & InputButtons.Jump) != 0)
            {
                _state.VaultAttemptResolvedForPress = false;
                _state.JumpBufferRemaining = _profile.JumpBuffer;
                _state.ReboundJumpRemaining = _profile.ReboundJumpWindow;
            }
            if (_state.MovementState == MovementState.Vault)
                return ContinueVault(frame, probe, deltaTime, facts, true);
            _state.VaultProgress = 0f;

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
            // Untagged edges use checked clearance/target without claiming an authored
            // VaultCandidate. All decision inputs still fit the recorded MovementProbe.
            bool ledge = !probe.VaultCandidate && probe.VaultClearance > 0f
                && _state.MovementState == MovementState.Air && _state.LedgeRegrabRemaining <= 0f;
            if (!externalMotion && ledge && CanVault(probe, true))
            {
                BeginVault(probe, true);
                return ContinueVault(frame, probe, deltaTime, facts, false);
            }
            if (!externalMotion && jump && probe.VaultCandidate && !_state.VaultAttemptResolvedForPress)
            {
                if (CanVault(probe))
                {
                    BeginVault(probe, false);
                    return ContinueVault(frame, probe, deltaTime, facts, false);
                }
                bool mantle = probe.VaultHeight > _profile.VaultMaximumHeight;
                _state.VaultAttemptResolvedForPress = true;
                facts.Add(Fact(mantle ? TraversalKind.Mantle : TraversalKind.Vault, false,
                    _state.Forward, TraversalDuration(mantle)));
                StartStumble(_profile.FailedVaultStumbleDuration, _profile.StumbleSpeedMultiplier);
                ConsumeJump();
                jump = false;
            }
            if (_state.MovementState == MovementState.Air && CanRebound(probe))
            {
                _state.Velocity = Vector3.Reflect(_state.Velocity, probe.WallNormal.normalized)
                    + Vector3.up * _profile.ReboundUpwardBoost;
                _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
                ReleaseStoredMomentum();
                _state.LastReboundWall = probe.WallId;
                _state.ReboundCooldownRemaining = _profile.ReboundCooldown * _state.ReboundCooldownMultiplier;
                ConsumeJump();
                facts.Add(Fact(TraversalKind.Rebound, true, _state.Velocity.normalized, _profile.ReboundCooldown));
                AddNoise(_profile.TraversalLoudness, NoiseSourceKind.Rebound);
            }
            else if (jump && _state.CoyoteRemaining > 0f && !probe.StandingBlocked)
            {
                _state.Velocity = new Vector3(_state.Velocity.x, JumpSpeed(), _state.Velocity.z);
                ReleaseStoredMomentum();
                _state.MovementState = MovementState.Air;
                _state.Grounded = grounded = false;
                _state.CoyoteRemaining = 0f;
                ConsumeJump();
                facts.Add(Fact(TraversalKind.Jump, true, _state.Forward, 0f));
            }
            else if (!cancelSlide && grounded && _state.StumbleRemaining <= 0f && (frame.Pressed & InputButtons.Crouch) != 0
                && Horizontal(_state.Velocity).magnitude >= _profile.SlideMinimumSpeed
                && _state.MovementState != MovementState.Slide)
            {
                Vector3 horizontal = Horizontal(_state.Velocity);
                _state.SlideEntrySpeed = Mathf.Min(EffectiveMaximumSpeed(), horizontal.magnitude + _profile.SlideBoost);
                _state.Velocity = horizontal.normalized * _state.SlideEntrySpeed;
                _state.SlideRemaining = Effect(PlayerEffectStat.SlideDuration, _profile.SlideDuration);
                _state.MovementState = MovementState.Slide;
                facts.Add(Fact(TraversalKind.Slide, true, horizontal.normalized, _state.SlideRemaining));
                if (!HasEffect(PlayerEffectStat.QuietSlide)) AddNoise(_profile.SlideLoudness, NoiseSourceKind.Slide);
            }

            MoveHorizontal(frame, probe, deltaTime);
            if (_state.MovementState == MovementState.Air)
                _state.Velocity += Vector3.down * _profile.Gravity * deltaTime;
            else _state.Velocity = Horizontal(_state.Velocity);
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            if (externalMotion)
            {
                _state.Velocity = Vector3.ClampMagnitude(_state.Velocity + _state.PendingExternalVelocity,
                    _profile.MaximumExternalMotionSpeed);
                _state.PendingExternalVelocity = Vector3.zero;
                if (_state.Velocity.y > 0f)
                {
                    _state.MovementState = MovementState.Air;
                    _state.Grounded = false;
                    _state.CoyoteRemaining = 0f;
                }
            }
            _state.Crouched = _state.MovementState == MovementState.Slide || probe.StandingBlocked
                || (_state.Grounded && (frame.Held & InputButtons.Crouch) != 0);
            if (_state.Grounded && Horizontal(_state.Velocity).magnitude >= 0.5f
                && _state.FootstepRemaining <= 0f && _state.MovementState != MovementState.Slide)
            {
                bool sprinting = (frame.Held & InputButtons.Sprint) != 0;
                AddNoise(sprinting ? _profile.SprintLoudness : _profile.WalkingLoudness * _state.FootstepNoiseMultiplier,
                    NoiseSourceKind.Footstep);
                _state.FootstepRemaining = _profile.FootstepInterval;
            }
            return new PlayerTickResult(_state.Velocity * deltaTime, _state.Crouched, facts.ToArray());
        }

        public void ApplyExternalVelocity(Vector3 velocity, ExternalMotionKind kind)
        {
            if (kind != ExternalMotionKind.Impulse && kind != ExternalMotionKind.CollapseHandThrow)
                throw new ArgumentOutOfRangeException(nameof(kind));
            QueueExternalVelocity(velocity);
        }

        public void ApplyExternalAcceleration(Vector3 acceleration, float deltaSeconds)
        {
            if (!Finite(acceleration) || !Finite(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            QueueExternalVelocity(acceleration * deltaSeconds);
        }

        private void QueueExternalVelocity(Vector3 velocity)
        {
            Vector3 pending = _state.PendingExternalVelocity + velocity;
            if (!Finite(velocity) || !Finite(pending.sqrMagnitude)
                || !Finite(_profile.MaximumExternalMotionSpeed) || _profile.MaximumExternalMotionSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(velocity));
            if (_state.IsAlive) _state.PendingExternalVelocity = pending;
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
                bool succeeded = Vector3.Distance(_state.Position, _state.VaultTarget + _state.VaultSteeringOffset) <= _profile.VaultCompletionTolerance;
                _state.CompletedTraversal = Fact(_state.VaultKind, succeeded,
                    (_state.VaultTarget - _state.VaultStart).normalized, _state.VaultDuration);
                if (!succeeded) StartStumble(_profile.FailedVaultStumbleDuration, _profile.StumbleSpeedMultiplier);
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
            float walkingSpeed = _profile.WalkSpeed * _state.MovementSpeedMultiplier * InjuryMultiplier() * _state.GrabSpeedMultiplier * _state.TrapSpeedMultiplier * _state.WebSpeedMultiplier * _state.ConsumableSpeedMultiplier * _state.HitBoostMultiplier;
            // Acceleration below walking pace, idle intent and wall-arrested motion
            // are not an achieved sprint. Modifier-scaled walking pace keeps grabs meaningful.
            return Vector3.Dot(horizontal, desired) > 0f && horizontal.magnitude > walkingSpeed + 0.0001f;
        }

        public PlayerHitResult ApplyHit(float damage, HitSeverity severity = HitSeverity.Heavy)
        {
            if (!_state.IsAlive || !Finite(damage) || damage <= 0f) return default;
            if (_state.GraceActive) return new PlayerHitResult(false, false, absorbedByGrace: true);
            if (severity != HitSeverity.Light && severity != HitSeverity.Heavy) throw new ArgumentOutOfRangeException(nameof(severity));
            long graceEnd = RecoveryEndTick(Effect(PlayerEffectStat.GraceSeconds, _profile.HitGraceSeconds));
            long boostEnd = RecoveryEndTick(Effect(PlayerEffectStat.BoostDuration,
                severity == HitSeverity.Light ? _profile.LightHitBoostSeconds : _profile.HeavyHitBoostSeconds));
            float boost = severity == HitSeverity.Light ? _profile.LightHitSpeedBoost : _profile.HeavyHitSpeedBoost;
            if (!Finite(boost) || boost < 0f || !Finite(1f + boost)) throw new ArgumentOutOfRangeException(nameof(boost));
            float absorbed = Math.Min(_state.Shield, damage);
            _state.Shield -= absorbed;
            _state.Health = Mathf.Max(0f, _state.Health - (damage - absorbed));
            _state.RegenerationDelayRemaining = Math.Max(0d, _profile.HealthRegenerationDelay);
            _state.HealthState = HealthTier(_state.Health);
            _state.GraceWindow = new GraceWindowFact(_state.Id, _state.Tick, _state.IsAlive ? graceEnd : _state.Tick, severity);
            _state.GraceActive = _state.Tick < _state.GraceWindow.EndTick;
            _state.HitBoostEndTick = _state.IsAlive ? boostEnd : _state.Tick;
            _state.HitBoostMultiplier = _state.Tick < _state.HitBoostEndTick ? 1f + boost : 1f;
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            if (!_state.IsAlive) { _state.IsSprinting = false; _state.Velocity = Vector3.zero; _state.LookBack = false; _state.HeadLookDelta = Vector2.zero; }
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
            return new PlayerHitResult(true, !_state.IsAlive, graceStarted: _state.GraceWindow);
        }

        public bool HealOverTime(float perSecond, float movingSeconds)
        {
            if (!_state.IsAlive || !Finite(perSecond) || perSecond <= 0f || !Finite(movingSeconds) || movingSeconds <= 0f) return false;
            _state.Health = (float)Math.Min(_state.MaxHealth, _state.Health + (double)perSecond * movingSeconds);
            _state.HealthState = HealthTier(_state.Health);
            return true;
        }

        public void SetConsumableSpeedMultiplier(float multiplier)
        { _state.ConsumableSpeedMultiplier = Finite(multiplier) ? Mathf.Max(1f, multiplier) : 1f; }

        public void SetWebSpeedMultiplier(float multiplier)
        { _state.WebSpeedMultiplier = Finite(multiplier) ? Mathf.Clamp01(multiplier) : 1f; }

        public void ClearSlows()
        { _state.WebSpeedMultiplier = _state.TrapSpeedMultiplier = 1f; }

        public bool RespawnAtFloorStart(float healthFraction)
        {
            if (_state.IsAlive || !Finite(healthFraction) || healthFraction <= 0f || healthFraction > 1f) return false;
            var effects = _state.ActiveEffects;
            float maximum = _state.BaseMaximumHealth, movement = _state.MovementSpeedMultiplier;
            float footsteps = _state.FootstepNoiseMultiplier, rebound = _state.ReboundCooldownMultiplier;
            float regeneration = _state.RegenerationMultiplier, startFraction = _state.FloorStartHealthFraction;
            bool lookBack = _state.LookBackEnabled;
            long tick = _state.Tick;
            Reset(_state.Id, _state.FloorStartPosition, _state.FloorStartHeading, _state.RecoveryTickSeconds);
            SetActiveEffects(effects);
            ApplyActiveEffects();
            ApplyRunModifiers(Effect(PlayerEffectStat.MaximumHealth, maximum) * healthFraction, maximum, movement);
            SetMovementEffects(footsteps, rebound, 1f);
            SetHealthRecoveryEffects(regeneration, startFraction);
            SetLookBackEnabled(lookBack);
            _state.Tick = tick;
            _state.RegenerationDelayRemaining = _profile.HealthRegenerationDelay;
            return true;
        }

        public bool GrantShield(float hitPoints)
        {
            if (!Finite(hitPoints) || hitPoints <= 0f || hitPoints > ShieldCapacity) return false;
            _state.Shield += hitPoints;
            return true;
        }

        public bool RestoreShield(float hitPoints)
        {
            if (!_state.IsAlive || !Finite(hitPoints) || hitPoints < 0f) return false;
            _state.Shield = hitPoints;
            return true;
        }

        public GraceWindowFact? AdvanceRecovery(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            _state.Tick = tick;
            if (tick >= _state.HitBoostEndTick && _state.HitBoostMultiplier != 1f)
            {
                _state.HitBoostMultiplier = 1f;
                _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
                _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
            }
            if (!_state.GraceActive || tick < _state.GraceWindow.EndTick) return null;
            _state.GraceActive = false;
            return _state.GraceWindow;
        }

        public GraceWindowFact? EndRecovery()
        {
            _state.PendingExternalVelocity = Vector3.zero;
            GraceWindowFact? ended = _state.GraceActive
                ? new GraceWindowFact(_state.Id, _state.GraceWindow.StartTick,
                    Math.Max(_state.GraceWindow.StartTick, Math.Min(_state.Tick, _state.GraceWindow.EndTick)), _state.GraceWindow.Severity)
                : (GraceWindowFact?)null;
            _state.GraceActive = false;
            _state.HitBoostEndTick = _state.Tick;
            _state.HitBoostMultiplier = 1f;
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
            return ended;
        }

        private long RecoveryEndTick(float seconds)
        {
            if (!Finite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            double ticks = seconds / (double)_state.RecoveryTickSeconds;
            double nearest = Math.Round(ticks);
            // Two single-precision operands can straddle an integral tick boundary.
            // Snap only within their relative rounding error; real fractions still ceil.
            if (nearest >= 1d && Math.Abs(ticks - nearest) <= ticks * 2d * 1.1920928955078125e-7d)
                ticks = nearest;
            return checked(_state.Tick + (long)Math.Ceiling(ticks));
        }

        public void SetActiveEffects(IReadOnlyActiveEffects effects)
        {
            if (effects != null && effects.Count > 0 && _effectConfig is null)
                throw new InvalidOperationException("Player active effects require PlayerEffectConfig; run Worsen/Player/Ensure Effect Config.");
            _state.ActiveEffects = effects;
        }

        private void ApplyActiveEffects()
        {
            // A previously empty live view may acquire entries without another setter call.
            SetActiveEffects(_state.ActiveEffects);
            bool alive = _state.IsAlive;
            float floorDamage = _state.FloorStartHealth - _state.Health;
            _state.AppliedEffects = _state.ActiveEffects == null ? default : new ActiveEffects(_state.ActiveEffects);
            _state.MaxHealth = Effect(PlayerEffectStat.MaximumHealth, _state.BaseMaximumHealth);
            if (_state.FloorHealthPending && alive)
                _state.Health = Mathf.Max(0f, _state.MaxHealth * Effect(PlayerEffectStat.FloorStartHealth,
                    _state.FloorStartHealthFraction) - floorDamage);
            _state.FloorHealthPending = false;
            _state.Health = Mathf.Min(_state.Health, _state.MaxHealth);
            _state.HealthState = HealthTier(_state.Health);
            _state.LowProfileEnabled = HasEffect(PlayerEffectStat.LowProfile);
            if (!HasEffect(PlayerEffectStat.StoredMomentum) || !_state.IsAlive)
                _state.StoredMomentumSpeed = _state.StoredMomentumRemaining = 0f;
            if (Effect(PlayerEffectStat.BoostDuration, 1f) == 0f)
            { _state.HitBoostMultiplier = 1f; _state.HitBoostEndTick = _state.Tick; }
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
        }

        private float Effect(PlayerEffectStat stat, float baseline)
            => PlayerEffectUtility.Value(_effectConfig, _state.AppliedEffects, stat, baseline);
        private bool HasEffect(PlayerEffectStat stat) => Effect(stat, 0f) > 0f;
        private float TraversalDuration(bool mantle)
            => Effect(PlayerEffectStat.TraversalDuration, mantle ? _profile.MantleDuration : _profile.VaultDuration);
        private float JumpSpeed() => _profile.JumpSpeed * Mathf.Sqrt(Effect(PlayerEffectStat.JumpHeight, 1f));

        private void ReleaseStoredMomentum()
        {
            if (_state.StoredMomentumRemaining <= 0f) return;
            Vector3 horizontal = Horizontal(_state.Velocity);
            Vector3 direction = horizontal.sqrMagnitude > 0f ? horizontal.normalized : _state.Forward;
            horizontal = direction * Mathf.Max(horizontal.magnitude, _state.StoredMomentumSpeed);
            _state.Velocity = ClampHorizontal(new Vector3(horizontal.x, _state.Velocity.y, horizontal.z), EffectiveMaximumSpeed());
            _state.StoredMomentumSpeed = _state.StoredMomentumRemaining = 0f;
        }

        public void SetLookBackEnabled(bool enabled) { _state.LookBackEnabled = enabled; }

        public void SetHealthRecoveryEffects(float regenerationMultiplier = 1f, float floorStartHealthFraction = 1f)
        {
            if (!Finite(regenerationMultiplier) || regenerationMultiplier < 0f
                || !Finite(floorStartHealthFraction) || floorStartHealthFraction <= 0f || floorStartHealthFraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(regenerationMultiplier));
            // Zero stops regeneration; 0.5 halves it. No curse identifiers are wired here.
            _state.RegenerationMultiplier = regenerationMultiplier;
            _state.FloorStartHealthFraction = floorStartHealthFraction;
        }

        public PlayerHitResult BeginFloorHealth(float maximumHealth, float movementMultiplier)
        {
            PlayerHitResult result = ApplyRunModifiers(Effect(PlayerEffectStat.MaximumHealth, maximumHealth)
                * Effect(PlayerEffectStat.FloorStartHealth, _state.FloorStartHealthFraction),
                maximumHealth, movementMultiplier);
            _state.FloorHealthPending = true;
            _state.FloorStartHealth = _state.Health;
            _state.StoredMomentumSpeed = _state.StoredMomentumRemaining = 0f;
            _state.RegenerationDelayRemaining = 0d;
            return result;
        }

        private void RegenerateHealth(float dt)
        {
            double healingSeconds = Math.Max(0d, dt - _state.RegenerationDelayRemaining);
            _state.RegenerationDelayRemaining = Math.Max(0d, _state.RegenerationDelayRemaining - dt);
            float regeneration = Effect(PlayerEffectStat.Regeneration, _state.RegenerationMultiplier);
            if (healingSeconds <= 0d || _state.Health >= _state.MaxHealth || regeneration <= 0f
                || !Finite(_profile.HealthRegenerationPerSecond) || _profile.HealthRegenerationPerSecond <= 0f) return;
            _state.Health = (float)Math.Min(_state.MaxHealth, _state.Health
                + healingSeconds * _profile.HealthRegenerationPerSecond * regeneration);
            _state.HealthState = HealthTier(_state.Health);
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
            _state.FloorHealthPending = false;
            _state.BaseMaximumHealth = maximumHealth;
            _state.MaxHealth = Effect(PlayerEffectStat.MaximumHealth, maximumHealth);
            _state.Health = Mathf.Clamp(health, 0f, _state.MaxHealth);
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
            if (_state.MovementState != MovementState.Vault)
                _state.StoredMomentumRemaining = Mathf.Max(0f, _state.StoredMomentumRemaining - dt);
            if (_state.StoredMomentumRemaining <= 0f) _state.StoredMomentumSpeed = 0f;
            _state.JumpBufferRemaining = Mathf.Max(0f, _state.JumpBufferRemaining - dt);
            _state.ReboundJumpRemaining = Mathf.Max(0f, _state.ReboundJumpRemaining - dt);
            _state.CoyoteRemaining = Mathf.Max(0f, _state.CoyoteRemaining - dt);
            _state.ReboundCooldownRemaining = Mathf.Max(0f, _state.ReboundCooldownRemaining - dt);
            _state.StumbleRemaining = Mathf.Max(0f, _state.StumbleRemaining - dt);
            _state.LedgeRegrabRemaining = Mathf.Max(0f, _state.LedgeRegrabRemaining - dt);
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

        public void SetTrapSpeedMultiplier(float multiplier)
        {
            _state.TrapSpeedMultiplier = Finite(multiplier) ? Mathf.Clamp01(multiplier) : 1f;
            _state.Velocity = ClampHorizontal(_state.Velocity, EffectiveMaximumSpeed());
            _state.VaultExitVelocity = ClampHorizontal(_state.VaultExitVelocity, EffectiveMaximumSpeed());
        }

        private void ApplyLook(InputFrame frame)
        {
            _state.LookBack = _state.LookBackEnabled && !HasEffect(PlayerEffectStat.NoLookBack)
                && (frame.Held & InputButtons.LookBack) != 0;
            Vector2 look = Finite(frame.LookDelta.x) && Finite(frame.LookDelta.y) ? frame.LookDelta : Vector2.zero;
            _state.HeadingDegrees = Mathf.Repeat(_state.HeadingDegrees + look.x, 360f);
            _state.Forward = Quaternion.Euler(0f, _state.HeadingDegrees, 0f) * Vector3.forward;
            _state.HeadLookDelta = _state.LookBack ? Vector2.zero : new Vector2(0f, look.y);
        }

        private void MoveHorizontal(InputFrame frame, MovementProbe probe, float dt)
        {
            Vector3 horizontal = Horizontal(_state.Velocity);
            if (_state.MovementState == MovementState.Slide)
            {
                _state.SlideRemaining = Mathf.Max(0f, _state.SlideRemaining - dt);
                float duration = Mathf.Max(0.0001f, Effect(PlayerEffectStat.SlideDuration, _profile.SlideDuration));
                float speed = Mathf.Lerp(EffectiveSprintSpeed(), _state.SlideEntrySpeed, _state.SlideRemaining / duration);
                // The Driver retains speed at glancing walls; never manufacture recovery here.
                speed = Mathf.Min(speed, horizontal.magnitude);
                float rate = Mathf.Min(Mathf.Max(0f, _profile.SlideMaximumTurnRate),
                    Mathf.Max(0f, _profile.SlideLateralAcceleration) / Mathf.Max(0.1f, speed) * Mathf.Rad2Deg);
                float strafe = Finite(frame.Move.x) ? Mathf.Clamp(frame.Move.x, -1f, 1f) : 0f;
                float mouseTurn = Finite(frame.LookDelta.x) ? frame.LookDelta.x : 0f;
                float turn = Mathf.Clamp(mouseTurn + strafe * rate * dt, -rate * dt, rate * dt);
                horizontal = Quaternion.AngleAxis(turn, Vector3.up) * horizontal.normalized * speed;
                _state.SlideTurnRateDegrees = turn / dt;
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
                    float cap = Mathf.Max(horizontal.magnitude, _profile.AirControlSpeedFloor);
                    horizontal = Vector3.ClampMagnitude(horizontal + direction * Effect(PlayerEffectStat.AirAcceleration,
                        _profile.AirAcceleration) * dt, cap);
                }
                else
                {
                    float speed = (frame.Held & InputButtons.Sprint) != 0
                        ? EffectiveSprintSpeed() : _profile.WalkSpeed * _state.MovementSpeedMultiplier * InjuryMultiplier() * _state.GrabSpeedMultiplier * _state.TrapSpeedMultiplier * _state.WebSpeedMultiplier * _state.ConsumableSpeedMultiplier * _state.HitBoostMultiplier;
                    if (_state.StumbleRemaining > 0f) speed *= _profile.StumbleSpeedMultiplier;
                    // Preserve a landing's retained momentum on its transition tick.
                    bool landed = _state.LandingImpactSpeed < 0f;
                    float acceleration = _effectConfig is not null && horizontal.magnitude <= _effectConfig.StandstillSpeed
                        ? Effect(PlayerEffectStat.GroundAcceleration, _profile.GroundAcceleration) : _profile.GroundAcceleration;
                    if (!landed) horizontal = Vector3.MoveTowards(horizontal, direction * speed,
                        (direction.sqrMagnitude > 0f ? acceleration : _profile.GroundFriction) * dt);
                    if (_state.MovementState == MovementState.Stumble && _state.StumbleRemaining <= 0f)
                        _state.MovementState = MovementState.Ground;
                }
            }
            if (_state.LandingImpactSpeed < 0f) _state.LandingImpactSpeed = 0f;
            if (_state.StumbleRemaining > 0f) horizontal = Vector3.ClampMagnitude(horizontal, _state.StumbleSpeedLimit);
            _state.Velocity = new Vector3(horizontal.x, _state.Velocity.y, horizontal.z);
        }

        private void Land(List<PlayerTraversalFact> facts)
        {
            float impact = Mathf.Max(_state.LandingImpactSpeed, -_state.Velocity.y);
            float retention = impact > _profile.HardLandingThreshold ? _profile.HardLandingRetention
                : impact >= _profile.SoftLandingThreshold ? _profile.SoftLandingRetention : 1f;
            float stumble = impact > _profile.HardLandingThreshold ? _profile.HardStumbleDuration
                : impact >= _profile.SoftLandingThreshold ? _profile.SoftStumbleDuration : 0f;
            if (HasEffect(PlayerEffectStat.SoftLanding) && impact > _profile.HardLandingThreshold) stumble = 0f;
            _state.Velocity = Horizontal(_state.Velocity) * retention;
            if (stumble > 0f) StartStumble(Mathf.Max(stumble, _state.StumbleRemaining), 1f);
            _state.MovementState = _state.StumbleRemaining > 0f ? MovementState.Stumble : MovementState.Ground;
            _state.LandingImpactSpeed = -1f;
            facts.Add(new PlayerTraversalFact(_state.Id, _state.Tick, TraversalKind.Land, true,
                Vector3.down, _state.StumbleRemaining, impact > _profile.HardLandingThreshold ? 1f : 0f));
            AddNoise(_profile.TraversalLoudness, NoiseSourceKind.Landing);
        }

        private void StartStumble(float duration, float retention)
        {
            _state.StumbleRemaining = Mathf.Max(0f, duration);
            _state.StumbleStartedSeconds = _state.StumbleRemaining;
            Vector3 horizontal = Horizontal(_state.Velocity) * Mathf.Clamp01(retention);
            _state.Velocity = new Vector3(horizontal.x, _state.Velocity.y, horizontal.z);
            _state.StumbleSpeedLimit = Mathf.Max(horizontal.magnitude, _profile.WalkSpeed * _profile.StumbleSpeedMultiplier);
            if (_state.Grounded) _state.MovementState = MovementState.Stumble;
        }

        private bool CanVault(MovementProbe probe, bool ledge = false)
        {
            if (!Finite(probe.VaultHeight) || !Finite(probe.VaultClearance) || !Finite(probe.VaultTarget)
                || !(probe.VaultHeight >= _profile.VaultMinimumHeight && probe.VaultHeight <= _profile.MantleMaximumHeight)
                || probe.VaultClearance <= 0f || probe.StandingBlocked) return false;
            if (ledge && (!(probe.VaultHeight >= _profile.LedgeMinimumHeight && probe.VaultHeight <= _profile.LedgeMaximumHeight)
                || Horizontal(probe.VaultTarget - _state.Position).magnitude > _profile.LedgeReach)) return false;
            float duration = TraversalDuration(ledge || probe.VaultHeight > _profile.VaultMaximumHeight);
            float maximumSpeed = EffectiveMaximumSpeed();
            Vector3 horizontal = Horizontal(probe.VaultTarget - _state.Position);
            float distance = horizontal.magnitude;
            float budget = maximumSpeed * duration;
            // This necessary travel bound does not replace swept collision or resolved completion.
            return Finite(duration) && duration > 0f && duration <= 1f && Finite(maximumSpeed) && maximumSpeed > 0f
                && Finite(horizontal) && Finite(distance) && Finite(budget) && budget > 0f && distance <= budget + 0.00001f;
        }

        private void BeginVault(MovementProbe probe, bool ledge)
        {
            bool mantle = ledge || probe.VaultHeight > _profile.VaultMaximumHeight;
            _state.VaultRemaining = TraversalDuration(mantle);
            _state.VaultDuration = _state.VaultRemaining;
            _state.VaultHeight = probe.VaultHeight;
            _state.VaultStart = _state.Position;
            _state.VaultKind = mantle ? TraversalKind.Mantle : TraversalKind.Vault;
            _state.VaultTarget = probe.VaultTarget;
            _state.VaultExitVelocity = Horizontal(_state.Velocity);
            if (!mantle && HasEffect(PlayerEffectStat.StoredMomentum))
            {
                _state.StoredMomentumSpeed = _state.VaultExitVelocity.magnitude;
                _state.StoredMomentumRemaining = _effectConfig.StoredMomentumWindow;
            }
            _state.VaultSteeringOffset = Vector3.zero;
            _state.VaultProgress = 0f;
            _state.MovementState = MovementState.Vault;
            _state.Crouched = false;
            // Look stays live; the base path is captured once and steering is a separate swept offset.
            _state.Grounded = false;
            ConsumeJump();
            AddNoise(_profile.TraversalLoudness, NoiseSourceKind.Vault);
        }

        private PlayerTickResult ContinueVault(InputFrame frame, MovementProbe probe, float dt, List<PlayerTraversalFact> facts, bool canCancel)
        {
            float remaining = _state.VaultRemaining;
            _state.TraversalSampleActive = true;
            if (canCancel && (frame.Pressed & InputButtons.Jump) != 0 && !probe.StandingBlocked)
            {
                bool boost = remaining > 0f && remaining <= _profile.TraversalBoostWindow + 0.000001f;
                _state.Velocity = ClampHorizontal(_state.VaultExitVelocity
                    + (boost ? _state.Forward * _profile.TraversalBoostSpeed : Vector3.zero), EffectiveMaximumSpeed());
                _state.Velocity += Vector3.up * (JumpSpeed() - _profile.Gravity * dt);
                ReleaseStoredMomentum();
                _state.MovementState = MovementState.Air;
                _state.Grounded = false;
                _state.VaultRemaining = _state.CoyoteRemaining = 0f;
                _state.LedgeRegrabRemaining = _profile.LedgeRegrabDelay;
                ConsumeJump();
                facts.Add(Fact(TraversalKind.Jump, true, _state.Velocity.normalized, 0f));
                return new PlayerTickResult(_state.Velocity * dt, false, facts.ToArray());
            }
            if (canCancel && (frame.Pressed & InputButtons.Jump) != 0) ConsumeJump();
            _state.VaultRemaining = Mathf.Max(0f, remaining - dt);
            if (_state.VaultRemaining < 0.000001f) _state.VaultRemaining = 0f;
            float elapsed = _state.VaultDuration - remaining;
            float steeringStart = _state.VaultDuration * (2f / 3f);
            _state.InputLockSeconds = Mathf.Max(0f, steeringStart - elapsed);
            if (_state.InputLockSeconds < 0.000001f) _state.InputLockSeconds = 0f;
            float steeringDt = Mathf.Max(0f, Mathf.Min(dt, _state.VaultDuration - elapsed) - Mathf.Max(0f, steeringStart - elapsed));
            Vector2 input = Finite(frame.Move.x) && Finite(frame.Move.y) ? Vector2.ClampMagnitude(frame.Move, 1f) : Vector2.zero;
            Vector3 wish = _state.Forward * input.y + Vector3.Cross(Vector3.up, _state.Forward) * input.x;
            Vector3 axis = Horizontal(_state.VaultTarget - _state.VaultStart).normalized;
            // Lateral steering preserves forward route progress; all offset travel is swept.
            Vector3 steering = Vector3.ProjectOnPlane(wish, axis) * _profile.TraversalSteeringSpeed;
            _state.VaultSteeringOffset += steering * steeringDt;
            if (steeringDt > 0f && steering.sqrMagnitude > 0f)
                _state.VaultExitVelocity = Vector3.ClampMagnitude(_state.VaultExitVelocity + steering * steeringDt,
                    _state.VaultExitVelocity.magnitude);
            _state.PreserveVelocityOnCommit = false;
            if (_state.VaultRemaining <= 0f)
            {
                _state.MovementState = MovementState.Air;
                _state.Velocity = _state.VaultExitVelocity;
                _state.VaultCompletionPending = true;
                _state.PreserveVelocityOnCommit = true;
                _state.LedgeRegrabRemaining = _profile.LedgeRegrabDelay;
            }
            float progress = 1f - _state.VaultRemaining / Mathf.Max(0.0001f, _state.VaultDuration);
            _state.VaultProgress = progress;
            return new PlayerTickResult(Vector3.zero, false, facts.ToArray(), true,
                _state.VaultStart, _state.VaultTarget, progress, _state.VaultHeight, _state.VaultSteeringOffset);
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
        private float EffectiveMaximumSpeed() => CapEffectSpeed(_state.MaxDesignSpeed * InjuryMultiplier() * _state.GrabSpeedMultiplier * _state.TrapSpeedMultiplier * _state.WebSpeedMultiplier * _state.HitBoostMultiplier) * _state.ConsumableSpeedMultiplier;
        private float EffectiveSprintSpeed() => CapEffectSpeed(Effect(PlayerEffectStat.SprintSpeed,
            _state.SprintSpeed * _state.MovementSpeedMultiplier) * InjuryMultiplier() * _state.GrabSpeedMultiplier * _state.TrapSpeedMultiplier * _state.WebSpeedMultiplier * _state.HitBoostMultiplier) * _state.ConsumableSpeedMultiplier;
        private float CapEffectSpeed(float speed) => PlayerEffectUtility.HasModifier(_effectConfig, _state.AppliedEffects, PlayerEffectStat.SprintSpeed)
            ? Mathf.Min(speed, PlayerEffectUtility.SprintCeiling(_effectConfig)) : speed;
        private PlayerTraversalFact Fact(TraversalKind kind, bool succeeded, Vector3 direction, float duration)
            => new PlayerTraversalFact(_state.Id, _state.Tick, kind, succeeded, direction, duration);
        private void AddNoise(float loudness, NoiseSourceKind sourceKind)
        {
            var noise = new NoiseEvent(_state.Id, _state.Position, loudness, _state.Tick, sourceKind);
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
