// ============================================================================
// PlayerPerkController.cs
// ============================================================================
// PURPOSE:
//   Computes Player-owned chase/contact perks without querying hunters or doors.
//   Session supplies committed chase and body-contact facts through PlayerManager.
//   Footstep history and heartbeat cadence use injected fixed-step ticks for replay.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Admit chase transitions and one jump-requested hunter-body rebound per chase.
//   - Replace only footstep origins with bounded historical player positions.
//   - Compute chase-only sprint tuning and deliver player-origin heartbeat facts.
//   - Admit glancing Ram protection and one sprint-door latch per room.
// DEPENDENCIES:
//   - Own Player state/config and immutable Core effect/chase/noise values only.
// USAGE NOTES:
//   No engine operations or randomness; all observations are supplied by the owner.
//   Lost sight is still a chase. Only None ends it; duplicate starts never rearm.
//   Heartbeats have a separate delivery channel, not a forged movement source kind.
//   Until Core adds Heartbeat, their NoiseEvent category is Other; Session must
//   route this explicit Player channel, never enable generic Other/world hearing.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public sealed class PlayerPerkController
    {
        private readonly PlayerBehaviorState _player;
        private readonly PlayerPerkBehaviorState _state;
        private readonly PlayerEffectConfig _config;
        public PlayerPerkController(PlayerBehaviorState player, PlayerEffectConfig config)
        { _player = player ?? throw new ArgumentNullException(nameof(player)); _state = player.Perks; _config = config; }
        private bool Has(string id) => !string.IsNullOrWhiteSpace(id) && _player.AppliedEffects.Has(new EffectId(id));
        public bool LoudHeartActive => _player.IsAlive && _state.ChaseId > 0 && _config is not null && Has(_config.LoudHeartId);
        public float SprintMultiplier => LoudHeartActive ? Checked(_config.LoudHeartSprintMultiplier, 1f) : 1f;
        public bool ProtectsGlancingRam => _player.IsAlive && _config is not null && Has(_config.SureFootingId);

        public void Reset()
        {
            _state.ChaseId = _state.LastChaseId = 0; _state.ChaseTick = -1;
            _state.BodyBounceSpent = false; _state.NextHeartbeatTick = 0;
            _state.Heartbeat = null; _state.Positions.Clear(); _state.LatchedRooms.Clear();
        }

        public void ReceiveChase(ChaseFact fact)
        {
            if (fact.Player != _player.Id || !_player.IsAlive || fact.ChaseId <= 0 || fact.Tick < _state.ChaseTick
                || fact.ChaseId < _state.LastChaseId) return;
            if (fact.Phase == ChasePhase.None)
            {
                if (fact.ChaseId != _state.ChaseId) return;
                _state.ChaseTick = fact.Tick; _state.ChaseId = 0; _state.Heartbeat = null;
            }
            else if (fact.Phase == ChasePhase.Confirmed || fact.Phase == ChasePhase.Lost)
            {
                if (_state.ChaseId == 0 && fact.ChaseId <= _state.LastChaseId) return;
                if (fact.ChaseId != _state.ChaseId)
                { _state.BodyBounceSpent = false; _state.NextHeartbeatTick = fact.Tick; }
                _state.ChaseId = _state.LastChaseId = fact.ChaseId; _state.ChaseTick = fact.Tick;
            }
        }

        public void Tick()
        {
            _state.Heartbeat = null;
            if (_config is null || !_player.IsAlive) return;
            long delay = Ticks(Checked(_config.EchoBootsDelaySeconds, 0f));
            var positions = _state.Positions;
            if (positions.Count > 0 && positions[positions.Count - 1].Tick > _player.Tick) positions.Clear();
            if (positions.Count == 0 || positions[positions.Count - 1].Tick != _player.Tick)
                positions.Add((_player.Tick, _player.Position));
            int expired = 0;
            while (expired + 1 < positions.Count && positions[expired + 1].Tick <= _player.Tick - delay) expired++;
            if (expired > 0) positions.RemoveRange(0, expired);
            if (!LoudHeartActive || _player.Tick < _state.NextHeartbeatTick) return;
            _state.NextHeartbeatTick = checked(_player.Tick + Math.Max(1, Ticks(Checked(_config.HeartbeatIntervalSeconds, 0.01f))));
            float loudness = Checked(_config.HeartbeatLoudness, 0f);
            if (loudness > 1f) throw new ArgumentOutOfRangeException(nameof(_config));
            _state.Heartbeat = new NoiseEvent(_player.Id, _player.Position, loudness, _player.Tick);
        }

        public bool TakeHeartbeat(out NoiseEvent noise)
        {
            noise = _state.Heartbeat ?? default;
            bool present = _state.Heartbeat.HasValue; _state.Heartbeat = null; return present;
        }

        public Vector3 FootstepOrigin()
        {
            if (_config is null || !Has(_config.EchoBootsId) || _state.Positions.Count == 0) return _player.Position;
            // The oldest retained entry is the nearest recorded sample at or before
            // the delay boundary. During startup it is the oldest available pose.
            return _state.Positions[0].Position;
        }

        public bool TryBodyRebound(EntityId hunter, Vector3 normal, float upwardBoost, float maximumSpeed, out Vector3 velocity)
        {
            velocity = default;
            if (_config is null || !Has(_config.SecondBounceId) || !_player.IsAlive || _state.ChaseId <= 0
                || _state.BodyBounceSpent || !hunter.IsValid || hunter == _player.Id
                || _player.MovementState != MovementState.Air || _player.ReboundJumpRemaining <= 0f
                || _player.GraceActive || _player.RevivalCollisionGraceActive || _player.RevivalDamageImmune
                || !Finite(_player.Velocity.x) || !Finite(_player.Velocity.y) || !Finite(_player.Velocity.z)
                || !Finite(normal.x) || !Finite(normal.y) || !Finite(normal.z) || !Finite(normal.sqrMagnitude)
                || normal.sqrMagnitude < 0.5f || Vector3.Dot(_player.Velocity, normal) >= 0f) return false;
            velocity = Vector3.Reflect(_player.Velocity, normal.normalized) + Vector3.up * Checked(upwardBoost, 0f);
            Vector3 horizontal = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z), Checked(maximumSpeed, 0f));
            velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            _state.BodyBounceSpent = true;
            return true;
        }

        public bool TryLatchDoor(int roomId, int doorId)
        {
            return _config is not null && Has(_config.LatchId) && _player.IsAlive && _player.IsSprinting
                && roomId >= 0 && doorId > 0 && _state.LatchedRooms.Add(roomId);
        }

        private long Ticks(float seconds)
        {
            Checked(_player.RecoveryTickSeconds, float.Epsilon);
            double ticks = seconds / (double)_player.RecoveryTickSeconds;
            double nearest = Math.Round(ticks);
            if (nearest >= 1d && Math.Abs(ticks - nearest) <= ticks * 2d * 1.1920928955078125e-7d) ticks = nearest;
            return checked((long)Math.Ceiling(ticks));
        }
        private static float Checked(float value, float minimum)
        { if (!Finite(value) || value < minimum) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
