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
//   - Request seeded withdrawals after continuous pursuit; emit room regions and audible noises.
// DEPENDENCIES:
//   - Director state/config, Core hearing/topology and optional injected Level read-only view.
// USAGE NOTES:
//   Pure rules; no engine queries, registry calls, or tick callback. Time and the
//   run Random are injected; only retreat trials consume Random. With no Level
//   view, legacy historical hints remain available but no region/noise inference is made.
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
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Director
{
    public sealed class DirectorController
    {
        private const double Epsilon = 0.0000001d;
        private readonly DirectorBehaviorState _state;
        private readonly DirectorConfig _config;
        private readonly System.Random _random;
        private IReadOnlyLevelState _level;
        private IReadOnlyDictionary<int, bool> _closedDoors;

        public DirectorController(DirectorBehaviorState state, DirectorConfig config, System.Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            ValidateConfig();
        }

        public void Reset()
        {
            _state.Players.Clear();
            _state.Pursuits.Clear(); _state.Noises.Clear(); _state.DeliveredNoises.Clear();
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
            var retreats = Retreats(hunters, deltaTime, tickEnd);
            var regions = Regions(hints, hunters);
            var noises = NoiseHints(hunters, tick, deltaTime);
            _state.LastTick = tick;
            return new DirectorTickResult(hints, intrusions, pressure, retreats, regions, noises);
        }
        public void SetLevelView(IReadOnlyLevelState level) { _level = level; }
        public void SetClosedDoors(IReadOnlyDictionary<int, bool> doors) { _closedDoors = doors; }
        public void HearNoise(NoiseEvent noise)
        {
            if (!Finite(noise.Position) || float.IsNaN(noise.Loudness) || float.IsInfinity(noise.Loudness) ||
                _state.Noises.Contains(noise) || _state.DeliveredNoises.Contains(noise)) return;
            _state.Noises.Add(noise);
        }
        private List<EntityId> Retreats(IReadOnlyList<DirectorHunterSample> hunters, float dt, double now)
        {
            var result = new List<EntityId>();
            var ordered = new List<DirectorHunterSample>(hunters);
            ordered.Sort((a, b) => a.HunterId.Value.CompareTo(b.HunterId.Value));
            var present = new HashSet<EntityId>();
            foreach (var hunter in ordered)
            {
                if (!hunter.IsActive || !hunter.HunterId.IsValid || !_state.Players.ContainsKey(hunter.TargetId) || !present.Add(hunter.HunterId)) continue;
                if (!_state.Pursuits.TryGetValue(hunter.HunterId, out var pursuit))
                    _state.Pursuits.Add(hunter.HunterId, pursuit = new DirectorPursuitBehaviorState());
                if (pursuit.Target != hunter.TargetId) { pursuit.Seconds = 0d; pursuit.Target = hunter.TargetId; }
                pursuit.Seconds = hunter.IsPursuing && _state.Players[hunter.TargetId].WasChasing ? pursuit.Seconds + dt : 0d;
                if (pursuit.Seconds <= _config.RetreatPursuitSeconds + Epsilon || now + Epsilon < pursuit.AvailableAt) continue;
                // A rejected trial also cools down: do not reroll every simulation tick.
                pursuit.AvailableAt = now + _config.RetreatCooldownSeconds;
                if (_random.NextDouble() < _config.RetreatProbability) result.Add(hunter.HunterId);
            }
            foreach (var id in new List<EntityId>(_state.Pursuits.Keys))
                if (!present.Contains(id)) _state.Pursuits.Remove(id);
            return result;
        }
        private int RoomAt(Vector3 point)
        {
            if (_level != null && _level.IsReady && _level.Graph != null)
                foreach (var room in _level.Graph.Rooms) if (room.Bounds.Contains(point)) return room.Id;
            return 0;
        }
        private List<DirectorRegionHint> Regions(List<HintPayload> hints, IReadOnlyList<DirectorHunterSample> hunters)
        {
            var result = new List<DirectorRegionHint>();
            foreach (var hint in hints)
            {
                int roomId = RoomAt(hint.Position);
                if (roomId == 0) continue;
                foreach (var hunter in hunters)
                {
                    if (hunter.HunterId != hint.Hunter || hunter.Hearing.ReferenceDistance <= 0f) continue;
                    var hearing = AcousticOcclusionUtility.Sample(_level.Graph, roomId, hint.Position,
                        RoomAt(hunter.Position), hunter.Position, 1f, hunter.Hearing, _closedDoors);
                    if (hearing.PortalCount < 0) continue;
                    double falloff = Math.Pow(Math.Max(1d, Vector3.Distance(hint.Position, hunter.Position) /
                        hunter.Hearing.ReferenceDistance), -hunter.Hearing.Rolloff);
                    float occlusion = 1f - Mathf.Clamp01((float)(hearing.PerceivedLoudness / Math.Max(double.Epsilon, falloff)));
                    foreach (var room in _level.Graph.Rooms)
                    {
                        if (room.Id != roomId) continue;
                        var region = new HintPayload(hint.Hunter, hint.Player, hint.ObservedTick, hint.DeliveredTick,
                            new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z), hint.AgeSeconds,
                            hint.Radius + _config.OcclusionHintRadiusMeters * occlusion, hint.Confidence);
                        result.Add(new DirectorRegionHint(region, roomId)); break;
                    }
                    break;
                }
            }
            return result;
        }
        private List<DirectorNoiseHint> NoiseHints(IReadOnlyList<DirectorHunterSample> hunters, long tick, float dt)
        {
            var result = new List<DirectorNoiseHint>();
            _state.DeliveredNoises.RemoveAll(noise => (tick - noise.Tick) * dt > _config.NoiseMaxAgeSeconds);
            foreach (var noise in _state.Noises)
            {
                if (noise.Tick > tick || (tick - noise.Tick) * dt > _config.NoiseMaxAgeSeconds || RoomAt(noise.Position) == 0) continue;
                foreach (var hunter in hunters)
                {
                    if (!hunter.IsActive || hunter.HunterId == noise.Source || hunter.Hearing.ReferenceDistance <= 0f) continue;
                    var heard = AcousticOcclusionUtility.Sample(_level.Graph, RoomAt(noise.Position), noise.Position,
                        RoomAt(hunter.Position), hunter.Position, noise.Loudness, hunter.Hearing, _closedDoors);
                    if (heard.Audible) result.Add(new DirectorNoiseHint(hunter.HunterId, noise));
                }
                _state.DeliveredNoises.Add(noise);
            }
            _state.Noises.Clear(); return result;
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
                _config.HintCadenceSeconds, _config.ExitOpenHintCadenceSeconds, _config.IntrusionDurationSeconds, _config.RetreatCooldownSeconds })
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new ArgumentException("Director durations must be finite and positive.", nameof(_config));
            foreach (float value in new[] { _config.HeatThresholdSeconds, _config.ReliefMinimumSeconds,
                _config.HintRadiusMeters, _config.ProximityRadiusMeters, _config.SlowSpeedMetersPerSecond,
                _config.SlowThresholdSeconds, _config.IntrusionCooldownSeconds, _config.RetreatPursuitSeconds,
                _config.OcclusionHintRadiusMeters, _config.NoiseMaxAgeSeconds })
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                    throw new ArgumentException("Director tuning must be finite and nonnegative.", nameof(_config));
            if (_config.HistoryCapacity < 2) throw new ArgumentException("Director history needs two or more samples.", nameof(_config));
            if (!(_config.RetreatProbability >= 0f && _config.RetreatProbability <= 1f))
                throw new ArgumentException("Retreat probability must be between zero and one.", nameof(_config));
            if (float.IsNaN(_config.HintConfidence) || float.IsInfinity(_config.HintConfidence) ||
                _config.HintConfidence < 0f || _config.HintConfidence > 1f)
                throw new ArgumentException("Director hint confidence must be finite and between zero and one.", nameof(_config));
        }
    }
}
