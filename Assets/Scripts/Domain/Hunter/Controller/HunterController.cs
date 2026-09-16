// ============================================================================
// HunterController.cs
// ============================================================================
// PURPOSE:
//   Decides Hunter behavior from observable sight, light, noises and memory.
//   Pure rules select goals and committed attack phases while preserving distinct
//   archetype curses, reachable melee elevation, occasional attack vocals and bounded light reactions.
// ARCHITECTURAL ROLE:
//   Controller (section 2) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Admit only this archetype's curse bits while retaining general run traits.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Time and randomness are injected. Hidden player position is never used as a clue.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterController
    {
        private readonly HunterBehaviorState _state;
        private readonly HunterProfile _profile;
        private readonly System.Random _random;
        private readonly IReadOnlyPlayerState _player;
        private readonly IReadOnlyLevelState _level;
        public HunterController(HunterBehaviorState state, HunterProfile profile, System.Random random,
            IReadOnlyPlayerState player, IReadOnlyLevelState level)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _level = level ?? throw new ArgumentNullException(nameof(level));
        }
        public void Reset(EntityId id, Vector3 position, Vector3 forward)
        {
            _state.UnavailableRoomIds.Clear(); _state.UnavailableRooms.Clear();
            _state.Afterimage = default; _state.AfterimageRemaining = 0f; _state.Traits = ProgressionTraits.None;
            _state.FiredRangedAttacks.Clear(); _state.AcceptedRangedAttacks.Clear(); _state.AttackSerial = 0; _state.AttackBecameActive = false;
            _state.StepDistance = 0f; _state.FootstepCooldown = 0f;
            _state.Flashlight = default; _state.LightObserved = false; _state.DirectlyIlluminated = false;
            _state.LastLightPosition = position; _state.LightMemoryRemaining = 0f;
            _state.LightExposure = 0f; _state.LightReactionRemaining = 0f;
            _state.LightReactionCooldown = 0f; _state.ScreamCooldown = 0f; _state.Feedback.Clear();
            _state.Id = id; _state.TargetId = _player.Id; _state.Position = position;
            _state.Forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            _state.Velocity = Vector3.zero; _state.Tick = 0; _state.IsActive = true;
            _state.RunSpeedMultiplier = 1f;
            _state.PlayerVisible = false; _state.PlayerHeard = false; _state.HasHint = false;
            _state.LastKnownPosition = position; _state.LastKnownTick = 0;
            _state.BeliefConfidence = 0f; _state.BeliefInitialConfidence = 0f;
            _state.BeliefReferenceTick = 0; _state.BeliefAgeAtReference = 0f;
            _state.LastNoiseTick = -1; _state.SensorInitialized = false;
            _state.LungePhase = HunterLungePhase.None; _state.PhaseSeconds = 0f;
            _state.LungeHitAccepted = false; _state.LungeDirection = Vector3.zero;
            _state.HasPatrolTarget = false; _state.NavigationTarget = position; _state.SearchSeconds = 0f;
            _state.PlannedFacts = ulong.MaxValue; _state.Action = HunterAction.Patrol;
            _state.ActionFailed = false; _state.ReplanCount = 0; _state.LastRoom = 0;
            _state.LoopDetected = false; _state.RecentRooms.Clear(); _state.DeltaTime = 0f;
        }
        public bool ShouldProbe(long tick) => !_state.SensorInitialized || tick % Math.Max(1, _profile.SensorIntervalTicks) == 0;
        public HunterTickResult Tick(SightProbe probe, float dt, long tick)
            => Tick(probe, default, dt, tick);
        public HunterTickResult Tick(SightProbe probe, HunterLightObservation light, float dt, long tick)
        {
            if (!(dt > 0f) || float.IsNaN(dt) || float.IsInfinity(dt)) return default;
            _state.AttackBecameActive = false;
            _state.Tick = tick; _state.DeltaTime = dt;
            if (!_player.IsAlive)
            {
                _state.PlayerVisible = false; _state.IsActive = false;
                _state.LungePhase = HunterLungePhase.None; _state.PhaseSeconds = 0f;
                _state.Feedback.Clear(); return default;
            }
            _state.FootstepCooldown = Mathf.Max(0f, _state.FootstepCooldown - dt);
            _state.ScreamCooldown = Mathf.Max(0f, _state.ScreamCooldown - dt);
            UpdateLightTimers(dt);
            if (ShouldProbe(tick))
            {
                bool wasVisible = _state.PlayerVisible;
                Sense(probe, dt, tick);
                SenseLight(light, tick);
                if (_state.PlayerVisible != wasVisible)
                {
                    _state.Feedback.Enqueue(_state.PlayerVisible ? HunterFeedbackKind.Detected : HunterFeedbackKind.LostTarget);
                }
            }
            if (_state.DirectlyIlluminated) _state.LightExposure += dt;
            else _state.LightExposure = 0f;
            DecayBelief(dt, tick);
            TrackRooms();
            bool begin = false;
            if (_state.LungePhase != HunterLungePhase.None)
            {
                _state.PhaseSeconds += dt;
                while (_state.LungePhase != HunterLungePhase.None &&
                    _state.PhaseSeconds + 0.000001f >= Duration(_state.LungePhase))
                {
                    _state.PhaseSeconds = Mathf.Max(0f, _state.PhaseSeconds - Duration(_state.LungePhase));
                    _state.LungePhase = _state.LungePhase == HunterLungePhase.Windup ? HunterLungePhase.Active :
                        _state.LungePhase == HunterLungePhase.Active ? HunterLungePhase.Recovery : HunterLungePhase.None;
                    if (_state.LungePhase == HunterLungePhase.Active)
                    {
                        _state.AttackBecameActive = true;
                        if (_profile.AttackStyle != HunterAttackStyle.Lunge) _state.FiredRangedAttacks.Add(_state.AttackSerial);
                        _state.Feedback.Enqueue(HunterFeedbackKind.AttackSwing);
                    }
                    if (_state.LungePhase == HunterLungePhase.Recovery)
                    {
                        if (_profile.AttackStyle == HunterAttackStyle.Lunge && !_state.LungeHitAccepted) _state.Feedback.Enqueue(HunterFeedbackKind.AttackMiss);
                        _state.Feedback.Enqueue(HunterFeedbackKind.AttackRecovery);
                    }
                    if (_state.LungePhase == HunterLungePhase.None)
                    { _state.PlannedFacts = ulong.MaxValue; _state.PhaseSeconds = 0f; }
                }
            }
            if (_state.LungePhase == HunterLungePhase.None && !_state.AttackBecameActive)
            {
                Replan();
                UpdateTarget(dt);
                if (_state.Action == HunterAction.Lunge && _state.PlayerVisible)
                {
                    _state.LungePhase = HunterLungePhase.Windup; _state.PhaseSeconds = 0f;
                    _state.AttackSerial++; _state.AttackTarget = _player.Position;
                    _state.AcceptedRangedAttacks.RemoveWhere(serial => serial < _state.AttackSerial - 8);
                    _state.FiredRangedAttacks.RemoveWhere(serial => serial < _state.AttackSerial - 8);
                    Vector3 direction = _player.Position - _state.Position; direction.y = 0f;
                    _state.LungeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : _state.Forward;
                    _state.LungeHitAccepted = false; begin = true;
                    if ((_profile.AttackScreamsEnabled || Cursed(ProgressionTraits.WatcherUnquietGaze)) &&
                        _state.ScreamCooldown <= 0f && _random.NextDouble() < _profile.AttackScreamChance)
                    { _state.Feedback.Enqueue(HunterFeedbackKind.Scream); _state.ScreamCooldown = _profile.ScreamCooldownSeconds; }
                    else _state.Feedback.Enqueue(HunterFeedbackKind.AttackWindup);
                }
            }
            float speed = _state.Action == HunterAction.Patrol ? _profile.PatrolSpeed :
                _player.SprintSpeed * _profile.ChaseSpeedMultiplier;
            return new HunterTickResult(_state.NavigationTarget, speed * _state.RunSpeedMultiplier, _state.LungePhase,
                _state.LungeDirection, begin, _state.LungePhase == HunterLungePhase.Active);
        }
        public float EffectiveAttackDistance => _profile.AttackStyle == HunterAttackStyle.Lunge ?
            _profile.LungeDistance * (Cursed(ProgressionTraits.RusherLongStride) ? 1.35f : 1f) : _profile.RangedAttackDistance;
        public float EffectiveSightRange => _profile.SightRange * (Cursed(ProgressionTraits.WatcherUnquietGaze) ? 1.25f : 1f);
        public float EffectiveSightCone => _profile.SightConeDegrees * (Cursed(ProgressionTraits.LurkerDarkAdaptation) ? 1.3f : 1f);
        public float WindupDuration => Duration(HunterLungePhase.Windup);
        public float ProjectileSpeed => _profile.ProjectileSpeed * (Cursed(ProgressionTraits.HexerLingeringHex) ? 0.65f : 1f);
        public float ProjectileRadius => _profile.ProjectileRadius * (Cursed(ProgressionTraits.HexerLingeringHex) ? 1.7f : 1f);
        public float SpikeRadius => _profile.SpikeRadius * (Cursed(ProgressionTraits.ThorncallerReachingRoots) ? 1.4f : 1f);
        public bool SplitBolt => Cursed(ProgressionTraits.HexerSplitBolt);
        public bool ThornRing => Cursed(ProgressionTraits.ThorncallerThornRing);
        public IReadOnlyList<Bounds> UnavailableRooms => _state.UnavailableRooms;
        public void SetRoomPhase(RoomPhaseChangedFact fact)
        {
            if (fact.Phase != RoomPhase.Closed || !_state.UnavailableRoomIds.Add(fact.RoomId) || _level.Graph == null) return;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (room.Id == fact.RoomId) { _state.UnavailableRooms.Add(room.Bounds); break; }
            _state.PlannedFacts = ulong.MaxValue; _state.HasPatrolTarget = false;
        }
        private bool Unavailable(Vector3 point)
        { foreach (Bounds room in _state.UnavailableRooms) if (room.Contains(point)) return true; return false; }
        public void SetTraits(ProgressionTraits traits)
        {
            const ProgressionTraits rusher = ProgressionTraits.RusherLongStride | ProgressionTraits.RusherSecondWind | ProgressionTraits.RusherBloodScent;
            const ProgressionTraits lurker = ProgressionTraits.LurkerDarkAdaptation | ProgressionTraits.LurkerCrookedStep | ProgressionTraits.LurkerStolenSilence;
            const ProgressionTraits watcher = ProgressionTraits.WatcherLongMemory | ProgressionTraits.WatcherCuttingCorners | ProgressionTraits.WatcherUnquietGaze;
            const ProgressionTraits hexer = ProgressionTraits.HexerSplitBolt | ProgressionTraits.HexerHastyScript | ProgressionTraits.HexerLingeringHex;
            const ProgressionTraits thorncaller = ProgressionTraits.ThorncallerThornRing | ProgressionTraits.ThorncallerQuickRoots | ProgressionTraits.ThorncallerReachingRoots;
            const ProgressionTraits specific = rusher | lurker | watcher | hexer | thorncaller;
            ProgressionTraits own = ProgressionTraits.None;
            switch (_profile.ArchetypeKey)
            {
                case "rusher": own = rusher; break;
                case "lurker": own = lurker; break;
                case "watcher": own = watcher; break;
                case "hexer": own = hexer; break;
                case "thorncaller": own = thorncaller; break;
            }
            _state.Traits = traits & (~specific | own);
            _state.PlannedFacts = ulong.MaxValue;
        }
        private bool Cursed(ProgressionTraits trait) => (_state.Traits & trait) != 0;
        public void SetAfterimage(FlashlightSample sample, float lifetime)
        {
            if (sample.Source != _state.TargetId) return;
            _state.Afterimage = sample; _state.AfterimageRemaining = Finite(lifetime) ? Mathf.Clamp(lifetime, 0f, 4f) : 0f;
        }
        public bool HearNoise(NoiseEvent noise, float transmission)
        {
            if (noise.Source != _player.Id || noise.Tick > _state.Tick || noise.Tick <= _state.LastNoiseTick ||
                !Finite(noise.Position) || !Finite(noise.Loudness) || !Finite(transmission)) return false;
            float age = (_state.Tick - noise.Tick) * _state.DeltaTime;
            if (age - Mathf.Abs(age) * 1.1920929e-7f > _profile.NoiseMaxAgeSeconds * (Cursed(ProgressionTraits.RusherBloodScent) ? 2f : 1f)) return false;
            float range = _profile.HearingRange * (Cursed(ProgressionTraits.RusherBloodScent) ? 1.35f : 1f);
            float loudness = noise.Loudness * Mathf.Clamp01(transmission) * Mathf.Clamp01(1f - Vector3.Distance(noise.Position, _state.Position) / range);
            if (loudness <= _profile.HearingThreshold) return false;
            _state.LastNoiseTick = noise.Tick; _state.PlayerHeard = true;
            if (!_state.PlayerVisible && noise.Tick >= _state.LastKnownTick) Observe(noise.Position, noise.Tick, Mathf.Clamp01(loudness));
            return true;
        }
        public bool TryAcceptRangedContact(EntityId target, int attackSerial, out HunterHit hit)
        {
            hit = default;
            if (_profile.AttackStyle == HunterAttackStyle.Lunge || target != _state.TargetId || !_player.IsAlive || !_state.IsActive ||
                attackSerial <= 0 || !_state.FiredRangedAttacks.Contains(attackSerial) || attackSerial > _state.AttackSerial || attackSerial < _state.AttackSerial - 8 ||
                !_state.AcceptedRangedAttacks.Add(attackSerial)) return false;
            _state.Feedback.Enqueue(HunterFeedbackKind.AttackHit);
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position,
                _profile.AttackStyle == HunterAttackStyle.Projectile ? ChaseEndReason.Projectile : ChaseEndReason.GroundSpike);
            return true;
        }
        public void ReportAttackMiss(int attackSerial)
        {
            if (!_state.AcceptedRangedAttacks.Contains(attackSerial)) _state.Feedback.Enqueue(HunterFeedbackKind.AttackMiss);
        }
        public void SetFlashlight(FlashlightSample sample)
        { if (sample.Source == _state.TargetId) _state.Flashlight = sample; }
        public bool TryDequeueFeedback(out HunterFeedbackEvent feedback)
        {
            feedback = default;
            if (!_player.IsAlive) { _state.Feedback.Clear(); return false; }
            if (_state.Feedback.Count == 0) return false;
            feedback = new HunterFeedbackEvent(_state.Id, _profile.ArchetypeKey, _state.Feedback.Dequeue(), _state.Position, _state.Tick);
            return true;
        }
        private void UpdateLightTimers(float dt)
        {
            _state.AfterimageRemaining = Mathf.Max(0f, _state.AfterimageRemaining - dt);
            _state.LightMemoryRemaining = Mathf.Max(0f, _state.LightMemoryRemaining - dt);
            _state.LightReactionCooldown = Mathf.Max(0f, _state.LightReactionCooldown - dt);
            if (_state.LightReactionRemaining > 0f)
            {
                _state.LightReactionRemaining = Mathf.Max(0f, _state.LightReactionRemaining - dt);
                if (_state.LightReactionRemaining <= 0f) _state.PlannedFacts = ulong.MaxValue;
            }
        }
        private void SenseLight(HunterLightObservation observation, long tick)
        {
            bool valid = observation.Tick == tick && Finite(observation.Position);
            _state.LightObserved = valid && observation.Observed;
            _state.DirectlyIlluminated = valid && observation.Illuminated;
            if (!_state.LightObserved && !_state.DirectlyIlluminated) return;
            _state.LastLightPosition = observation.Position;
            _state.LightMemoryRemaining = _profile.LightMemorySeconds * (Cursed(ProgressionTraits.WatcherLongMemory) ? 1.75f : 1f);
        }
        public float LungeSpeed => _profile.LungeSpeed * _state.RunSpeedMultiplier;
        public void ApplyRunSpeedMultiplier(float multiplier)
        {
            if (!Finite(multiplier) || multiplier <= 0f || !Finite(_profile.PatrolSpeed * multiplier)
                || !Finite(_player.SprintSpeed * _profile.ChaseSpeedMultiplier * multiplier)
                || !Finite(_profile.LungeSpeed * multiplier))
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Hunter run speed must be finite and positive.");
            _state.RunSpeedMultiplier = multiplier;
        }
        public HunterAttackSample AttackSample()
        {
            bool attacking = _state.IsActive && _state.LungePhase != HunterLungePhase.None;
            return new HunterAttackSample(_state.Id, _state.Position,
                attacking ? _state.LungeDirection : _state.Forward,
                attacking ? (int)_state.LungePhase : 0,
                attacking ? Mathf.Clamp01(_state.PhaseSeconds / Mathf.Max(0.0001f, Duration(_state.LungePhase))) : 0f);
        }
        public void CommitPose(Vector3 position, Vector3 velocity, Vector3 forward)
        {
            Vector3 displacement = position - _state.Position; displacement.y = 0f;
            if (_state.LungePhase == HunterLungePhase.None) _state.StepDistance += displacement.magnitude;
            _state.Position = position; _state.Velocity = velocity; _state.Forward = forward;
            if (_state.StepDistance >= 2.2f && _state.FootstepCooldown <= 0f)
            { _state.StepDistance = 0f; _state.FootstepCooldown = 0.18f; _state.Feedback.Enqueue(HunterFeedbackKind.Footstep); }
        }
        public HunterSighting Sighting() => new HunterSighting(_state.Id, _state.TargetId, _state.Tick,
            _state.PlayerVisible, _state.Position, Vector3.Distance(_state.Position, _player.Position));
        public void ReportPathFailure()
        {
            _state.ActionFailed = true;
            if (_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight)
            { _state.LightReactionRemaining = 0f; _state.LightExposure = 0f; }
        }
        public bool TryAcceptContact(EntityId target, out HunterHit hit)
        {
            hit = default;
            if (_state.LungePhase != HunterLungePhase.Active || _state.LungeHitAccepted ||
                target != _state.TargetId || !_player.IsAlive || !_state.IsActive) return false;
            _state.LungeHitAccepted = true;
            _state.Feedback.Enqueue(HunterFeedbackKind.AttackHit);
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position);
            return true;
        }
        public bool ReceiveHint(HintPayload hint)
        {
            if (hint.Hunter != _state.Id || hint.Player != _state.TargetId || hint.ObservedTick > hint.DeliveredTick ||
                hint.DeliveredTick != _state.Tick || !Finite(hint.Position) ||
                !Finite(hint.AgeSeconds) || !Finite(hint.Radius) || !Finite(hint.Confidence) ||
                hint.AgeSeconds < 0f || hint.Radius < 0f ||
                hint.AgeSeconds >= MemoryDuration || _state.PlayerVisible ||
                (_state.BeliefConfidence > 0f && hint.ObservedTick < _state.LastKnownTick)) return false;
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float radius = Mathf.Sqrt((float)_random.NextDouble()) * hint.Radius;
            _state.LastKnownPosition = hint.Position + new Vector3((float)Math.Cos(angle) * radius, 0f, (float)Math.Sin(angle) * radius);
            _state.LastKnownTick = hint.ObservedTick;
            _state.BeliefReferenceTick = hint.DeliveredTick; _state.BeliefAgeAtReference = hint.AgeSeconds;
            _state.BeliefInitialConfidence = Mathf.Clamp01(hint.Confidence);
            _state.BeliefConfidence = _state.BeliefInitialConfidence *
                Mathf.Clamp01(1f - hint.AgeSeconds / Mathf.Max(0.0001f, MemoryDuration));
            _state.HasHint = _state.BeliefConfidence > 0f; _state.PlannedFacts = ulong.MaxValue;
            return _state.HasHint;
        }
        private void Sense(SightProbe probe, float dt, long tick)
        {
            _state.SensorInitialized = true;
            Vector3 offset = _player.Position - _state.Position;
            float distance = offset.magnitude;
            Vector3 planar = offset; planar.y = 0f;
            float dot = planar.sqrMagnitude <= 0.0001f ? 1f : Vector3.Dot(_state.Forward.normalized, planar.normalized);
            _state.PlayerVisible = (probe.HeadVisible || probe.ChestVisible || probe.HipsVisible) &&
                distance <= EffectiveSightRange && dot + 0.000001f >= Mathf.Cos(EffectiveSightCone * 0.5f * Mathf.Deg2Rad);
            _state.PlayerHeard = false;
            if (_state.PlayerVisible) Observe(_player.Position, tick, 1f);
            if (_player.RecentNoises != null)
                foreach (NoiseEvent noise in _player.RecentNoises) HearNoise(noise, 1f);
        }

        private void Observe(Vector3 position, long tick, float confidence)
        {
            _state.LastKnownPosition = position; _state.LastKnownTick = tick;
            _state.BeliefInitialConfidence = confidence; _state.BeliefConfidence = confidence;
            _state.BeliefReferenceTick = tick; _state.BeliefAgeAtReference = 0f;
            _state.HasHint = false;
        }
        private void DecayBelief(float dt, long tick)
        {
            _state.BeliefConfidence = _state.BeliefInitialConfidence *
                Mathf.Clamp01(1f - Mathf.Max(0f, BeliefAge(dt, tick)) /
                Mathf.Max(0.0001f, MemoryDuration));
            if (_state.BeliefConfidence <= 0f) _state.HasHint = false;
        }
        private float BeliefAge(float dt, long tick) => _state.BeliefAgeAtReference + (tick - _state.BeliefReferenceTick) * dt;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private float MemoryDuration => _profile.MemoryDecaySeconds * (Cursed(ProgressionTraits.WatcherLongMemory) ? 1.75f : 1f);
        private float Duration(HunterLungePhase phase)
        {
            if (phase == HunterLungePhase.Windup)
                return Mathf.Max(0.15f, _profile.LungeWindupSeconds *
                    (Cursed(ProgressionTraits.LurkerStolenSilence) || Cursed(ProgressionTraits.HexerHastyScript) || Cursed(ProgressionTraits.ThorncallerQuickRoots) ? 0.7f : 1f));
            return phase == HunterLungePhase.Active ? Mathf.Max(0.05f, _profile.LungeActiveSeconds) :
                Mathf.Max(0.15f, _profile.LungeRecoverySeconds * (Cursed(ProgressionTraits.RusherSecondWind) ? 0.65f : 1f));
        }
        private void Replan()
        {
            ulong facts = 0;
            if (_state.PlayerVisible) facts |= (ulong)HunterWorldFacts.PlayerVisible;
            if (_state.PlayerHeard) facts |= (ulong)HunterWorldFacts.PlayerHeard;
            if (_state.BeliefConfidence > 0f) facts |= (ulong)HunterWorldFacts.HasBelief;
            if (BeliefAge(_state.DeltaTime, _state.Tick) <= _profile.BeliefFreshSeconds)
                facts |= (ulong)HunterWorldFacts.BeliefFresh;
            bool reachableElevation = _profile.AttackStyle != HunterAttackStyle.Lunge ||
                Mathf.Abs(_state.Position.y - _player.Position.y) <= _profile.MaximumMeleeElevation;
            if (_state.PlayerVisible && reachableElevation && !Unavailable(_state.Position) && !Unavailable(_player.Position) && Vector3.Distance(_state.Position, _player.Position) <= EffectiveAttackDistance)
                facts |= (ulong)HunterWorldFacts.InLungeRange;
            if (_state.LoopDetected || (_profile.LightResponse == HunterLightResponse.Flank && _state.PlayerVisible)) facts |= (ulong)HunterWorldFacts.LoopDetected;
            if (_state.HasHint) facts |= (ulong)HunterWorldFacts.HasHint;
            if (_state.LightObserved) facts |= (ulong)HunterWorldFacts.LightObserved;
            if (_state.DirectlyIlluminated) facts |= (ulong)HunterWorldFacts.DirectlyIlluminated;
            if (_state.LightMemoryRemaining > 0f) facts |= (ulong)HunterWorldFacts.LightMemoryFresh;
            bool react = _profile.LightResponse != HunterLightResponse.Investigate &&
                ((_state.LightExposure >= _profile.LightExposureSeconds && _state.LightReactionCooldown <= 0f) || _state.LightReactionRemaining > 0f);
            if (react) facts |= (ulong)HunterWorldFacts.LightReactionReady;
            if (facts == _state.PlannedFacts && !_state.ActionFailed) return;
            var actions = new List<GoapActionDefinition>
            {
                Action(HunterAction.InvestigateLight, HunterWorldFacts.LightMemoryFresh, HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 0.75f),
                Action(_profile.LightResponse == HunterLightResponse.Avoid ? HunterAction.AvoidLight : HunterAction.FlankLight,
                    HunterWorldFacts.LightReactionReady, 0, HunterWorldFacts.EscapedBeam, 0.5f),
                Action(HunterAction.Patrol, 0, 0, HunterWorldFacts.Patrolled, 4f),
                Action(HunterAction.InvestigateHint, HunterWorldFacts.HasHint, HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 1f),
                Action(HunterAction.SearchLastKnown, HunterWorldFacts.HasBelief, HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 2f),
                Action(HunterAction.Chase, HunterWorldFacts.PlayerVisible, HunterWorldFacts.InLungeRange, HunterWorldFacts.InLungeRange, 2f),
                Action(HunterAction.CutOff, HunterWorldFacts.PlayerVisible | HunterWorldFacts.LoopDetected,
                    HunterWorldFacts.InLungeRange, HunterWorldFacts.InLungeRange, 1f),
                Action(HunterAction.Lunge, HunterWorldFacts.PlayerVisible | HunterWorldFacts.InLungeRange, 0, HunterWorldFacts.CaughtPlayer, 1f)
            };
            if (_state.ActionFailed)
            {
                actions.RemoveAll(action => action.Id == (int)_state.Action);
                _state.HasPatrolTarget = false;
            }
            ulong goal = react ? (ulong)HunterWorldFacts.EscapedBeam : _state.PlayerVisible ? (ulong)HunterWorldFacts.CaughtPlayer :
                _state.BeliefConfidence > 0f || _state.LightMemoryRemaining > 0f ? (ulong)HunterWorldFacts.LocatedPlayer : (ulong)HunterWorldFacts.Patrolled;
            GoapPlanResult plan = GoapPlannerUtility.Plan(facts, goal, 0, actions);
            _state.Action = plan.ActionIds.Length > 0 ? (HunterAction)plan.ActionIds[0] : HunterAction.Patrol;
            if ((_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight) && _state.LightReactionRemaining <= 0f)
            {
                Vector3 away = _state.Position - _state.LastLightPosition; away.y = 0f;
                away = away.sqrMagnitude > 0.001f ? away.normalized : -_state.Forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, away) * ((_state.Id.Value & 1) == 0 ? 1f : -1f);
                _state.LightReactionTarget = _state.Position +
                    (_state.Action == HunterAction.AvoidLight ? (away + lateral).normalized : lateral) * _profile.LightReactionDistance * (Cursed(ProgressionTraits.LurkerCrookedStep) ? 1.5f : 1f);
                _state.LightReactionRemaining = _profile.LightReactionSeconds * (Cursed(ProgressionTraits.LurkerCrookedStep) ? 1.3f : 1f);
                _state.LightReactionCooldown = _profile.LightReactionSeconds + _profile.LightReactionCooldownSeconds;
                _state.Feedback.Enqueue(HunterFeedbackKind.LightReaction);
            }
            _state.PlannedFacts = plan.ActionIds.Length > 0 ? facts : ulong.MaxValue;
            _state.ActionFailed = false; _state.SearchSeconds = 0f; _state.ReplanCount++;
        }
        private static GoapActionDefinition Action(HunterAction id, HunterWorldFacts required, HunterWorldFacts absent, HunterWorldFacts effect, float cost)
            => new GoapActionDefinition((int)id, (ulong)required, (ulong)absent, (ulong)effect, 0, cost);
        private void UpdateTarget(float dt)
        {
            if (_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight)
                _state.NavigationTarget = _state.LightReactionTarget;
            else if (_state.Action == HunterAction.InvestigateLight)
            {
                _state.NavigationTarget = _state.LastLightPosition;
                if (Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius)
                { _state.LightMemoryRemaining = 0f; _state.PlannedFacts = ulong.MaxValue; }
            }
            else if (_state.Action == HunterAction.Chase || _state.Action == HunterAction.Lunge)
                _state.NavigationTarget = _player.Position;
            else if (_state.Action == HunterAction.CutOff) _state.NavigationTarget = InterceptRoom();
            else if (_state.Action == HunterAction.InvestigateHint || _state.Action == HunterAction.SearchLastKnown)
            {
                _state.NavigationTarget = _state.LastKnownPosition;
                if (Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius)
                {
                    _state.SearchSeconds += dt;
                    if (_state.SearchSeconds >= _profile.SearchSeconds)
                    { _state.BeliefInitialConfidence = 0f; _state.BeliefConfidence = 0f; _state.HasHint = false; _state.PlannedFacts = ulong.MaxValue; }
                }
            }
            else if (!_state.HasPatrolTarget || Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius)
            {
                var available = new List<LevelRoom>();
                if (_level.IsReady && _level.Graph != null)
                    foreach (LevelRoom room in _level.Graph.Rooms) if (!_state.UnavailableRoomIds.Contains(room.Id)) available.Add(room);
                _state.NavigationTarget = available.Count > 0 ? RoomTarget(available[_random.Next(available.Count)]) : _state.Position;
                _state.HasPatrolTarget = true;
            }
        }
        private Vector3 InterceptRoom()
        {
            if (!_level.IsReady || _level.Graph == null || _state.LastRoom == 0) return _state.LastKnownPosition;
            var reachable = LevelGraphUtility.TopologicalDistancesFrom(_level.Graph, _state.LastRoom, TraversalAccess.Hunter);
            Vector3 predicted = _player.Position + _player.Velocity * _profile.CutOffPredictionSeconds * (Cursed(ProgressionTraits.WatcherCuttingCorners) ? 1.7f : 1f);
            Vector3 best = _state.LastKnownPosition; float score = float.PositiveInfinity;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (!_state.UnavailableRoomIds.Contains(room.Id) && reachable[room.Id] >= 0 && Vector3.SqrMagnitude(RoomTarget(room) - predicted) < score)
                { best = RoomTarget(room); score = Vector3.SqrMagnitude(RoomTarget(room) - predicted); }
            return best;
        }
        private static Vector3 RoomTarget(LevelRoom room) => new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
        private void TrackRooms()
        {
            if (!_level.IsReady || _level.Graph == null) return;
            foreach (LevelRoom room in _level.Graph.Rooms)
            {
                if (!room.Bounds.Contains(_state.Position) || room.Id == _state.LastRoom) continue;
                _state.LastRoom = room.Id; _state.RecentRooms.Add(room.Id);
                if (_state.RecentRooms.Count > 8) _state.RecentRooms.RemoveAt(0);
                int visits = 0; foreach (int id in _state.RecentRooms) if (id == room.Id) visits++;
                _state.LoopDetected = visits >= 3; break;
            }
        }
    }
}
