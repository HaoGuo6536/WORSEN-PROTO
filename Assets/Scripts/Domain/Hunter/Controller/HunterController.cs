// ============================================================================
// HunterController.cs
// ============================================================================
// PURPOSE:
//   Turns sampled visibility, old noises and delayed hints into an imperfect belief.
//   The hunter plans pursuit from that belief and commits to attack timing, leaving
//   physical paths, collisions and motion to the engine boundary.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Evaluate pure sight/hearing, decay memory and choose goal-oriented actions.
//   - Own lunge windup, active and recovery phases with one accepted contact.
//   - Apply per-instance run speed scaling and expose normalized attack telegraph facts.
// DEPENDENCIES:
//   - Reads Player and Level read-only views injected by the Manager; Core facts.
// USAGE NOTES:
//   Scene-owned per-entity state. Time and shared randomness are injected.
//   Failed paths cause replanning; hidden Player position is never used as a target.
//   Noise-age equality tolerates only the injected float delta's rounding error.
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
        {
            if (!(dt > 0f) || float.IsNaN(dt) || float.IsInfinity(dt)) return default;
            _state.Tick = tick; _state.DeltaTime = dt;
            if (!_player.IsAlive) { _state.PlayerVisible = false; _state.IsActive = false; return default; }
            if (ShouldProbe(tick)) Sense(probe, dt, tick);
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
                    if (_state.LungePhase == HunterLungePhase.None)
                    { _state.PlannedFacts = ulong.MaxValue; _state.PhaseSeconds = 0f; }
                }
            }
            if (_state.LungePhase == HunterLungePhase.None)
            {
                Replan();
                UpdateTarget(dt);
                if (_state.Action == HunterAction.Lunge && _state.PlayerVisible)
                {
                    _state.LungePhase = HunterLungePhase.Windup; _state.PhaseSeconds = 0f;
                    Vector3 direction = _player.Position - _state.Position; direction.y = 0f;
                    _state.LungeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : _state.Forward;
                    _state.LungeHitAccepted = false; begin = true;
                }
            }
            float speed = _state.Action == HunterAction.Patrol ? _profile.PatrolSpeed :
                _player.SprintSpeed * _profile.ChaseSpeedMultiplier;
            return new HunterTickResult(_state.NavigationTarget, speed * _state.RunSpeedMultiplier, _state.LungePhase,
                _state.LungeDirection, begin, _state.LungePhase == HunterLungePhase.Active);
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
        { _state.Position = position; _state.Velocity = velocity; _state.Forward = forward; }
        public HunterSighting Sighting() => new HunterSighting(_state.Id, _state.TargetId, _state.Tick,
            _state.PlayerVisible, _state.Position, Vector3.Distance(_state.Position, _player.Position));
        public void ReportPathFailure() { _state.ActionFailed = true; }
        public bool TryAcceptContact(EntityId target, out HunterHit hit)
        {
            hit = default;
            if (_state.LungePhase != HunterLungePhase.Active || _state.LungeHitAccepted ||
                target != _state.TargetId || !_player.IsAlive || !_state.IsActive) return false;
            _state.LungeHitAccepted = true;
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position);
            return true;
        }
        public bool ReceiveHint(HintPayload hint)
        {
            if (hint.Hunter != _state.Id || hint.Player != _state.TargetId || hint.ObservedTick > hint.DeliveredTick ||
                hint.DeliveredTick != _state.Tick || !Finite(hint.Position) ||
                !Finite(hint.AgeSeconds) || !Finite(hint.Radius) || !Finite(hint.Confidence) ||
                hint.AgeSeconds < 0f || hint.Radius < 0f ||
                hint.AgeSeconds >= _profile.MemoryDecaySeconds || _state.PlayerVisible ||
                (_state.BeliefConfidence > 0f && hint.ObservedTick < _state.LastKnownTick)) return false;
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float radius = Mathf.Sqrt((float)_random.NextDouble()) * hint.Radius;
            _state.LastKnownPosition = hint.Position + new Vector3((float)Math.Cos(angle) * radius, 0f, (float)Math.Sin(angle) * radius);
            _state.LastKnownTick = hint.ObservedTick;
            _state.BeliefReferenceTick = hint.DeliveredTick; _state.BeliefAgeAtReference = hint.AgeSeconds;
            _state.BeliefInitialConfidence = Mathf.Clamp01(hint.Confidence);
            _state.BeliefConfidence = _state.BeliefInitialConfidence *
                Mathf.Clamp01(1f - hint.AgeSeconds / Mathf.Max(0.0001f, _profile.MemoryDecaySeconds));
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
                distance <= _profile.SightRange && dot + 0.000001f >= Mathf.Cos(_profile.SightConeDegrees * 0.5f * Mathf.Deg2Rad);
            _state.PlayerHeard = false;
            if (_state.PlayerVisible) Observe(_player.Position, tick, 1f);
            if (_player.RecentNoises != null)
            foreach (NoiseEvent noise in _player.RecentNoises)
            {
                double ageSeconds = (tick - noise.Tick) * (double)dt;
                // Mono may retain wider intermediate precision: 30 * (1f/60f) can exceed 0.5.
                // One float relative epsilon bounds delta representation error, not a gameplay grace period.
                double ageRoundingTolerance = Math.Abs(ageSeconds) * 1.1920928955078125e-7;
                if (noise.Source != _player.Id || noise.Tick <= _state.LastNoiseTick || noise.Tick > tick ||
                    ageSeconds - ageRoundingTolerance > _profile.NoiseMaxAgeSeconds) continue;
                float falloff = Mathf.Clamp01(1f - Vector3.Distance(noise.Position, _state.Position) /
                    Mathf.Max(0.0001f, _profile.HearingRange));
                if (noise.Loudness * falloff <= _profile.HearingThreshold) continue;
                _state.PlayerHeard = true; _state.LastNoiseTick = noise.Tick;
                if (!_state.PlayerVisible && noise.Tick >= _state.LastKnownTick)
                    Observe(noise.Position, noise.Tick, Mathf.Clamp01(noise.Loudness * falloff));
            }
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
                Mathf.Max(0.0001f, _profile.MemoryDecaySeconds));
            if (_state.BeliefConfidence <= 0f) _state.HasHint = false;
        }
        private float BeliefAge(float dt, long tick) => _state.BeliefAgeAtReference + (tick - _state.BeliefReferenceTick) * dt;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private float Duration(HunterLungePhase phase) => phase == HunterLungePhase.Windup ? _profile.LungeWindupSeconds :
            phase == HunterLungePhase.Active ? _profile.LungeActiveSeconds : _profile.LungeRecoverySeconds;
        private void Replan()
        {
            ulong facts = 0;
            if (_state.PlayerVisible) facts |= (ulong)HunterWorldFacts.PlayerVisible;
            if (_state.PlayerHeard) facts |= (ulong)HunterWorldFacts.PlayerHeard;
            if (_state.BeliefConfidence > 0f) facts |= (ulong)HunterWorldFacts.HasBelief;
            if (BeliefAge(_state.DeltaTime, _state.Tick) <= _profile.BeliefFreshSeconds)
                facts |= (ulong)HunterWorldFacts.BeliefFresh;
            if (_state.PlayerVisible && Vector3.Distance(_state.Position, _player.Position) <= _profile.LungeDistance)
                facts |= (ulong)HunterWorldFacts.InLungeRange;
            if (_state.LoopDetected) facts |= (ulong)HunterWorldFacts.LoopDetected;
            if (_state.HasHint) facts |= (ulong)HunterWorldFacts.HasHint;
            if (facts == _state.PlannedFacts && !_state.ActionFailed) return;
            var actions = new List<GoapActionDefinition>
            {
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
            ulong goal = _state.PlayerVisible ? (ulong)HunterWorldFacts.CaughtPlayer :
                _state.BeliefConfidence > 0f ? (ulong)HunterWorldFacts.LocatedPlayer : (ulong)HunterWorldFacts.Patrolled;
            GoapPlanResult plan = GoapPlannerUtility.Plan(facts, goal, 0, actions);
            _state.Action = plan.ActionIds.Length > 0 ? (HunterAction)plan.ActionIds[0] : HunterAction.Patrol;
            _state.PlannedFacts = plan.ActionIds.Length > 0 ? facts : ulong.MaxValue;
            _state.ActionFailed = false; _state.SearchSeconds = 0f; _state.ReplanCount++;
        }
        private static GoapActionDefinition Action(HunterAction id, HunterWorldFacts required, HunterWorldFacts absent, HunterWorldFacts effect, float cost)
            => new GoapActionDefinition((int)id, (ulong)required, (ulong)absent, (ulong)effect, 0, cost);
        private void UpdateTarget(float dt)
        {
            if (_state.Action == HunterAction.Chase || _state.Action == HunterAction.Lunge)
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
                _state.NavigationTarget = _level.IsReady && _level.Graph != null && _level.Graph.Rooms.Count > 0 ?
                    RoomTarget(_level.Graph.Rooms[_random.Next(_level.Graph.Rooms.Count)]) : _state.Position;
                _state.HasPatrolTarget = true;
            }
        }
        private Vector3 InterceptRoom()
        {
            if (!_level.IsReady || _level.Graph == null || _state.LastRoom == 0) return _state.LastKnownPosition;
            var reachable = LevelGraphUtility.TopologicalDistancesFrom(_level.Graph, _state.LastRoom, TraversalAccess.Hunter);
            Vector3 predicted = _player.Position + _player.Velocity * _profile.CutOffPredictionSeconds;
            Vector3 best = _state.LastKnownPosition; float score = float.PositiveInfinity;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (reachable[room.Id] >= 0 && Vector3.SqrMagnitude(RoomTarget(room) - predicted) < score)
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
