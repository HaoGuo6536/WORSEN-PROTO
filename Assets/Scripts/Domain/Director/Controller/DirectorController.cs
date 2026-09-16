// ============================================================================
// DirectorController.cs
// ============================================================================
// PURPOSE:
//   Computes pressure pacing from explicit committed player and hunter samples.
//   A half-second accumulator schedules decisions while time advances solely from
//   Session ticks. Historical hints preserve uncertainty instead of revealing the
//   player's current position, and each player receives independent relief.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Director.
// KEY RESPONSIBILITIES:
//   - Maintain per-player heat, relief, target-compatible assignment, and bounded history.
//   - Return delayed hints and one intrusion per qualifying slow episode.
//   - Produce raw pressure observations without resetting gaps on hint emission.
// DEPENDENCIES:
//   - Director state/config/local snapshots and Core identities only.
// USAGE NOTES:
//   Pure rules; no engine queries, registry calls, or tick callback. Time and the
//   run Random are injected; current policy is deterministic and does not consume
//   Random. Radius describes uncertainty about the exact historical sample.
//   Batch catch-up evaluates every boundary but cadence/cooldown anchor to actual
//   delivery time, so catch-up cannot deliver stacked hints for the same player.
//   Delivery-anchored deadlines are also compared at actual delivery time, never
//   against the earlier evaluation boundary within that tick or catch-up batch.
//   Reset removes all scene history. Missing history suppresses a hint. Ring
//   capacity must retain HintAgeSeconds at the configured Session sample rate.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Director
{
    public sealed class DirectorController
    {
        private const double Epsilon = 0.0000001d;
        private readonly DirectorBehaviorState _state;
        private readonly DirectorConfig _config;

        public DirectorController(DirectorBehaviorState state, DirectorConfig config, System.Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));
            ValidateConfig();
        }

        public void Reset()
        {
            _state.Players.Clear();
            _state.ElapsedSeconds = 0d;
            _state.EvaluationAccumulatorSeconds = 0d;
            _state.EvaluationCount = 0;
            _state.LastTick = -1;
        }

        public DirectorTickResult Tick(long tick, float deltaTime, IReadOnlyList<DirectorPlayerSample> players,
            IReadOnlyList<DirectorHunterSample> hunters, bool exitOpen)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (hunters == null) throw new ArgumentNullException(nameof(hunters));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime == 0f) return EmptyResult();
            if (tick <= _state.LastTick) throw new ArgumentOutOfRangeException(nameof(tick), "Session ticks must increase.");
            ValidateConfig();
            ValidateSamples(players, hunters);
            var ordered = ReconcilePlayers(players);
            double tickEnd = _state.ElapsedSeconds + deltaTime;
            foreach (var sample in ordered)
            {
                var player = _state.Players[sample.PlayerId];
                AssignHunter(player, hunters);
                AddHistory(player, new DirectorPositionSample(tick, tickEnd, sample.Position));
                player.IsWithinProximity = IsNearHunter(sample.Position, hunters);
                if (sample.IsChasing != player.WasChasing) player.ReliefSeconds = 0d;
                player.WasChasing = sample.IsChasing;
            }

            var hints = new List<HintPayload>();
            var intrusions = new List<IntrusionSample>();
            var pressure = new List<DirectorPressureSample>();
            double remaining = deltaTime;
            while (remaining > Epsilon)
            {
                double untilEvaluation = _config.EvaluationIntervalSeconds - _state.EvaluationAccumulatorSeconds;
                double step = Math.Min(remaining, untilEvaluation);
                foreach (var sample in ordered) AdvancePlayer(_state.Players[sample.PlayerId], sample, step);
                _state.ElapsedSeconds += step;
                _state.EvaluationAccumulatorSeconds += step;
                remaining -= step;
                if (_state.EvaluationAccumulatorSeconds + Epsilon < _config.EvaluationIntervalSeconds) continue;
                _state.EvaluationAccumulatorSeconds = 0d;
                _state.EvaluationCount++;
                foreach (var sample in ordered)
                    Evaluate(_state.Players[sample.PlayerId], tick, tickEnd, exitOpen, hints, intrusions, pressure);
            }
            // Preserve sub-epsilon residue so high-frequency batches cannot lose time.
            if (remaining > 0d)
            {
                foreach (var sample in ordered) AdvancePlayer(_state.Players[sample.PlayerId], sample, remaining);
                _state.ElapsedSeconds += remaining;
                _state.EvaluationAccumulatorSeconds += remaining;
            }
            _state.LastTick = tick;
            return new DirectorTickResult(hints, intrusions, pressure);
        }

        private List<DirectorPlayerSample> ReconcilePlayers(IReadOnlyList<DirectorPlayerSample> samples)
        {
            var live = new HashSet<EntityId>();
            var ordered = new List<DirectorPlayerSample>();
            foreach (var sample in samples)
            {
                if (!sample.PlayerId.IsValid || !sample.IsAlive) continue;
                if (!live.Add(sample.PlayerId)) throw new ArgumentException("Player identities must be unique.", nameof(samples));
                ordered.Add(sample);
                if (_state.Players.ContainsKey(sample.PlayerId)) continue;
                _state.Players.Add(sample.PlayerId, new DirectorPlayerBehaviorState {
                    PlayerId = sample.PlayerId, ReliefSeconds = _config.ReliefMinimumSeconds,
                    History = new DirectorPositionSample[_config.HistoryCapacity]
                });
            }
            var removed = new List<EntityId>();
            foreach (var id in _state.Players.Keys) if (!live.Contains(id)) removed.Add(id);
            foreach (var id in removed) _state.Players.Remove(id);
            ordered.Sort((a, b) => a.PlayerId.Value.CompareTo(b.PlayerId.Value));
            return ordered;
        }

        private void AssignHunter(DirectorPlayerBehaviorState player, IReadOnlyList<DirectorHunterSample> hunters)
        {
            foreach (var hunter in hunters)
                if (hunter.IsActive && hunter.HunterId.IsValid && hunter.TargetId == player.PlayerId &&
                    hunter.HunterId == player.AssignedHunterId) return;
            player.AssignedHunterId = EntityId.None;
            int leastAssignments = int.MaxValue;
            foreach (var hunter in hunters)
            {
                if (!hunter.IsActive || !hunter.HunterId.IsValid || hunter.TargetId != player.PlayerId) continue;
                int assignments = 0;
                foreach (var other in _state.Players.Values)
                    if (other.AssignedHunterId == hunter.HunterId) assignments++;
                if (assignments > leastAssignments || (assignments == leastAssignments &&
                    player.AssignedHunterId.IsValid && hunter.HunterId.Value >= player.AssignedHunterId.Value)) continue;
                leastAssignments = assignments;
                player.AssignedHunterId = hunter.HunterId;
            }
        }

        private bool IsNearHunter(Vector3 position, IReadOnlyList<DirectorHunterSample> hunters)
        {
            float squaredRadius = _config.ProximityRadiusMeters * _config.ProximityRadiusMeters;
            foreach (var hunter in hunters)
                if (hunter.IsActive && hunter.HunterId.IsValid && (hunter.Position - position).sqrMagnitude < squaredRadius) return true;
            return false;
        }

        private void AdvancePlayer(DirectorPlayerBehaviorState player, DirectorPlayerSample sample, double dt)
        {
            if (sample.IsChasing || player.IsWithinProximity)
            { player.HeatSeconds = 0d; player.HasIssuedHint = false; player.LastHintDeliveredAtSeconds = 0d; }
            else player.HeatSeconds += dt;
            player.ReliefSeconds = sample.IsChasing ? 0d : player.ReliefSeconds + dt;
            float speedSquared = sample.Velocity.x * sample.Velocity.x + sample.Velocity.z * sample.Velocity.z;
            if (speedSquared <= _config.SlowSpeedMetersPerSecond * _config.SlowSpeedMetersPerSecond)
                player.SlowSeconds += dt;
            else { player.SlowSeconds = 0d; player.IntrusionIssuedThisEpisode = false; }
        }

        private void Evaluate(DirectorPlayerBehaviorState player, long tick, double deliverySeconds, bool exitOpen,
            List<HintPayload> hints, List<IntrusionSample> intrusions,
            List<DirectorPressureSample> pressure)
        {
            bool issued = false;
            double cadence = exitOpen ? _config.ExitOpenHintCadenceSeconds : _config.HintCadenceSeconds;
            if (!player.WasChasing && !player.IsWithinProximity && player.AssignedHunterId.IsValid &&
                player.HeatSeconds > _config.HeatThresholdSeconds + Epsilon &&
                player.ReliefSeconds + Epsilon >= _config.ReliefMinimumSeconds &&
                (!player.HasIssuedHint || deliverySeconds - player.LastHintDeliveredAtSeconds + Epsilon >= cadence) &&
                TryHistoricalPosition(player, deliverySeconds - _config.HintAgeSeconds, out var historical))
            {
                hints.Add(new HintPayload(player.AssignedHunterId, player.PlayerId, historical.Tick, tick,
                    historical.Position, (float)(deliverySeconds - historical.Seconds), _config.HintRadiusMeters, _config.HintConfidence));
                player.HasIssuedHint = true;
                player.LastHintDeliveredAtSeconds = deliverySeconds;
                issued = true;
            }
            if (!player.IntrusionIssuedThisEpisode && player.SlowSeconds > _config.SlowThresholdSeconds + Epsilon &&
                deliverySeconds + Epsilon >= player.IntrusionAvailableAtSeconds)
            {
                intrusions.Add(new IntrusionSample(player.PlayerId, tick, _config.IntrusionDurationSeconds));
                player.IntrusionIssuedThisEpisode = true;
                player.IntrusionAvailableAtSeconds = deliverySeconds + _config.IntrusionCooldownSeconds;
            }
            pressure.Add(new DirectorPressureSample(player.PlayerId, tick, (float)player.HeatSeconds, (float)player.ReliefSeconds,
                player.WasChasing, player.IsWithinProximity, issued));
        }

        private static void AddHistory(DirectorPlayerBehaviorState player, DirectorPositionSample sample)
        {
            player.History[player.HistoryNextIndex] = sample;
            player.HistoryNextIndex = (player.HistoryNextIndex + 1) % player.History.Length;
            player.HistoryCount = Math.Min(player.HistoryCount + 1, player.History.Length);
        }

        private static bool TryHistoricalPosition(DirectorPlayerBehaviorState player, double seconds, out DirectorPositionSample sample)
        {
            sample = default;
            int oldest = (player.HistoryNextIndex - player.HistoryCount + player.History.Length) % player.History.Length;
            for (int i = 0; i < player.HistoryCount; i++)
            {
                var next = player.History[(oldest + i) % player.History.Length];
                if (Math.Abs(next.Seconds - seconds) <= Epsilon) { sample = next; return true; }
                if (next.Seconds < seconds) continue;
                if (i == 0) return false;
                var previous = player.History[(oldest + i - 1) % player.History.Length];
                float fraction = (float)((seconds - previous.Seconds) / (next.Seconds - previous.Seconds));
                sample = new DirectorPositionSample(previous.Tick, seconds, previous.Position + (next.Position - previous.Position) * fraction);
                return true;
            }
            return false;
        }

        private static DirectorTickResult EmptyResult() => new DirectorTickResult(Array.Empty<HintPayload>(),
            Array.Empty<IntrusionSample>(), Array.Empty<DirectorPressureSample>());

        private static void ValidateSamples(IReadOnlyList<DirectorPlayerSample> players, IReadOnlyList<DirectorHunterSample> hunters)
        {
            foreach (var player in players)
                if (player.PlayerId.IsValid && player.IsAlive && (!Finite(player.Position) || !Finite(player.Velocity)))
                    throw new ArgumentException("Living player observations must be finite.", nameof(players));
            foreach (var hunter in hunters)
                if (hunter.HunterId.IsValid && hunter.IsActive && !Finite(hunter.Position))
                    throw new ArgumentException("Active hunter observations must be finite.", nameof(hunters));
        }

        private static bool Finite(Vector3 vector) =>
            !float.IsNaN(vector.x) && !float.IsInfinity(vector.x) &&
            !float.IsNaN(vector.y) && !float.IsInfinity(vector.y) &&
            !float.IsNaN(vector.z) && !float.IsInfinity(vector.z);

        private void ValidateConfig()
        {
            foreach (float value in new[] { _config.EvaluationIntervalSeconds, _config.HintAgeSeconds,
                _config.HintCadenceSeconds, _config.ExitOpenHintCadenceSeconds, _config.IntrusionDurationSeconds })
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new ArgumentException("Director durations must be finite and positive.", nameof(_config));
            foreach (float value in new[] { _config.HeatThresholdSeconds, _config.ReliefMinimumSeconds,
                _config.HintRadiusMeters, _config.ProximityRadiusMeters, _config.SlowSpeedMetersPerSecond,
                _config.SlowThresholdSeconds, _config.IntrusionCooldownSeconds })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new ArgumentException("Director tuning must be finite and nonnegative.", nameof(_config));
            if (_config.HistoryCapacity < 2) throw new ArgumentException("Director history needs two or more samples.", nameof(_config));
            if (float.IsNaN(_config.HintConfidence) || float.IsInfinity(_config.HintConfidence) ||
                _config.HintConfidence < 0f || _config.HintConfidence > 1f)
                throw new ArgumentException("Director hint confidence must be finite and between zero and one.", nameof(_config));
        }
    }
}
