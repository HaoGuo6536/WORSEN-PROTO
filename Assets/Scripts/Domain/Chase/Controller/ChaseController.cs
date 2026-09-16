// ============================================================================
// ChaseController.cs
// ============================================================================
// PURPOSE:
//   Tracks pursuit from independently observed hunter sources and explicit time.
//   One chase identity survives temporary loss, avoiding duplicate starts and
//   inflated telemetry counts when sight returns during the grace interval.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Confirm continuous sight, require both loss conditions, and preserve grace identity.
//   - Return accepted catch outcomes and bounded directional proximity facts.
// DEPENDENCIES:
//   - Reads injected Player and Hunter read-only views; Core event facts.
// USAGE NOTES:
//   Scene-owned state; Run Session supplies each tick after committed hunter poses.
//   No engine calls or random decisions. Missing sources cannot retain sight.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Chase
{
    public sealed class ChaseController
    {
        private readonly ChaseBehaviorState _state;
        private readonly ChaseConfig _config;
        private readonly IReadOnlyPlayerState _player;
        public ChaseController(ChaseBehaviorState state, ChaseConfig config, IReadOnlyPlayerState player)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            Reset();
        }
        public void Reset()
        {
            _state.Phase = ChasePhase.None; _state.ChaseId = 0; _state.NextChaseId = 0;
            _state.PlayerId = _player.Id; _state.HunterId = EntityId.None;
            _state.StartTick = 0; _state.GraceSeconds = 0f; _state.Closeness = 0f;
            _state.LastTick = -1; _state.LastCatchTick = -1; _state.Hunters.Clear();
        }
        public ChaseTickResult Tick(IReadOnlyList<IReadOnlyHunterState> hunters, float dt, long tick)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt) || tick <= _state.LastTick)
                return default;
            _state.LastTick = tick;
            ChasePhase previous = _state.Phase;
            EntityId confirmation = EntityId.None;
            bool retainsPursuit = false;
            float nearest = float.PositiveInfinity;
            float closeness = 0f;
            EntityId proximityHunter = EntityId.None;
            var present = new HashSet<EntityId>();
            if (hunters != null)
            foreach (IReadOnlyHunterState hunter in hunters)
            {
                if (hunter == null || !hunter.IsActive || !hunter.Id.IsValid || hunter.TargetId != _player.Id ||
                    !present.Add(hunter.Id)) continue;
                if (!_state.Hunters.TryGetValue(hunter.Id, out ChaseHunterBehaviorState source))
                    _state.Hunters.Add(hunter.Id, source = new ChaseHunterBehaviorState());
                bool visible = _player.IsAlive && hunter.PlayerVisible;
                source.SightSeconds = visible ? source.SightSeconds + dt : 0f;
                source.NoSightSeconds = visible ? 0f : source.NoSightSeconds + dt;
                source.SeenTick = tick;
                float distance = Vector3.Distance(hunter.Position, _player.Position);
                if (source.SightSeconds + 0.000001f >= _config.ConfirmationSeconds)
                {
                    source.Participating = true;
                    if (!confirmation.IsValid || hunter.Id.Value < confirmation.Value) confirmation = hunter.Id;
                }
                if (source.Participating && !(source.NoSightSeconds + 0.000001f >= _config.LossSeconds &&
                    distance > _config.LossDistance)) retainsPursuit = true;
                float value = Closeness(distance, Vector3.Dot(hunter.Position - _player.Position, _player.Forward) < 0f);
                if (value > closeness || (!proximityHunter.IsValid && distance < nearest))
                { closeness = value; proximityHunter = hunter.Id; nearest = distance; }
            }
            foreach (KeyValuePair<EntityId, ChaseHunterBehaviorState> pair in _state.Hunters)
            {
                if (present.Contains(pair.Key)) continue;
                pair.Value.SightSeconds = 0f; pair.Value.NoSightSeconds += dt;
                if (pair.Value.Participating && pair.Value.NoSightSeconds + 0.000001f < _config.LossSeconds)
                    retainsPursuit = true;
            }
            bool started = false, lost = false, ended = false;
            ChaseEndReason reason = ChaseEndReason.Unknown;
            if (!_player.IsAlive)
            {
                if (_state.HasActiveChase) { ended = true; _state.Phase = ChasePhase.None; }
            }
            else if (_state.Phase == ChasePhase.None && confirmation.IsValid && tick > _state.LastCatchTick)
            {
                _state.Phase = ChasePhase.Confirmed; _state.ChaseId = ++_state.NextChaseId;
                _state.StartTick = tick; _state.HunterId = confirmation; started = true;
            }
            else if (_state.Phase == ChasePhase.Confirmed && !retainsPursuit)
            {
                _state.Phase = ChasePhase.Lost; _state.GraceSeconds = 0f; lost = true;
            }
            else if (_state.Phase == ChasePhase.Lost)
            {
                if (confirmation.IsValid)
                { _state.Phase = ChasePhase.Confirmed; _state.HunterId = confirmation; _state.GraceSeconds = 0f; }
                else
                {
                    _state.GraceSeconds += dt;
                    if (_state.GraceSeconds + 0.000001f >= _config.LostGraceSeconds)
                    { _state.Phase = ChasePhase.None; reason = ChaseEndReason.Lost; ended = true; }
                }
            }
            _state.Closeness = _state.HasActiveChase ? closeness : 0f;
            var fact = new ChaseFact(_state.ChaseId, _state.PlayerId, _state.HunterId, tick, _state.Phase, reason);
            var proximity = new ProximitySample(_state.PlayerId, proximityHunter, tick, _state.ChaseId,
                float.IsPositiveInfinity(nearest) ? _config.FarDistance : nearest, _state.Closeness, _state.HasActiveChase);
            if (ended) _state.Hunters.Clear();
            return new ChaseTickResult(started, lost, ended, previous != _state.Phase, fact, proximity);
        }
        public ChaseTickResult RecordCatch(HunterHit hit)
        {
            if (!_state.HasActiveChase || hit.Target != _state.PlayerId || hit.Tick <= _state.LastCatchTick ||
                hit.Tick < _state.StartTick) return default;
            _state.LastCatchTick = hit.Tick; _state.Phase = ChasePhase.None; _state.Closeness = 0f;
            _state.HunterId = hit.Hunter; _state.Hunters.Clear();
            return new ChaseTickResult(false, false, true, true,
                new ChaseFact(_state.ChaseId, _state.PlayerId, hit.Hunter, hit.Tick, ChasePhase.None, hit.Reason),
                new ProximitySample(_state.PlayerId, hit.Hunter, hit.Tick, _state.ChaseId, 0f, 0f, false));
        }
        private float Closeness(float distance, bool behind)
        {
            float span = Mathf.Max(0.0001f, _config.FarDistance - _config.NearDistance);
            return Mathf.Clamp01((_config.FarDistance - distance) / span) *
                Mathf.Clamp01(behind ? _config.RearWeight : _config.FrontWeight);
        }
    }
}
