// ============================================================================
// FloorController.cs
// ============================================================================
// PURPOSE:
//   Selects placed cakes and a weighted required subset, then schedules deterministic collapse.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Admit one-shot optional puzzle gold independently of required cakes and exit quotas.
//   - Prefer freeze doorway anchors and safely prioritize behind-rooms with full telegraphs.
//   - Replace optional spawns with seeded traps; apply injected cake hooks without foreign effects.
//   - Recheck live escape routes before shuffled transitions, including activated pockets.
//   - Start collapse independently of Greedy Door unlocking; keep guidance chase-independent.
//   - Build read-only guidance snapshots with the white objective before optional Golden Sense.
//   - Leave unused sockets empty; score optional cakes without advancing required progress.
//   - Return a shared hearing noise for every accepted ordinary or golden pickup.
//   - Support staged cracks, tearing, mist advance and escapable hand contacts.
//   - Exclude the exit from collapse, accelerate warning pulses and snatch only remaining rewards.
//   - Resolve uninterrupted locked-exit holds without collecting cakes or opening collapse.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Density mode ignores legacy requiredCakeCount overrides. Explicitly disabling
//   density preserves authored count fixtures; only that mode spawns every eligible socket.
//   Reads injected Player views only. Footprint cells establish room membership.
//   Rooms without an exit route and flagged pockets remain dormant until activation;
//   their delay uses the floor clock, independently of the cake-driven collapse clock.
//   No persistent singleton or competing simulation tick is created.
//   Faster/Shuffled Collapse are explicit default-off initialization hooks. Shuffle
//   permutes all ordinary rooms, deferring the occupied shortest routes until vacated.
//   Unknown occupancy defers collapse, never guesses a safe route. Wax Heart is one charge per floor.
//   ContactExit begins a locked hold; LeaveExit cancels it. TickExitHold receives
//   elapsed contact time once per simulation step and returns only bailed escapes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class FloorController
    {
        private readonly FloorBehaviorState _state;
        private readonly FloorConfig _config;
        private readonly System.Random _random;
        public FloorController(FloorBehaviorState state, FloorConfig config, System.Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public void Initialize(LevelGraph graph, IReadOnlyList<IReadOnlyPlayerState> players, int requiredCakeCount = -1,
            bool fasterCollapse = false, bool shuffledCollapse = false, int round = 1, FloorCakeHooks cakeHooks = default, bool waxHeart = false,
            IReadOnlyCollection<int> preferredAnchors = null, IReadOnlyCollection<int> earlyCollapseRooms = null)
        {
            if (graph == null || players == null) throw new ArgumentNullException();
            RequirePositive(_config.CollapseInterval, nameof(_config.CollapseInterval));
            RequirePositive(_config.TelegraphDuration, nameof(_config.TelegraphDuration));
            RequirePositive(_config.DirectionCueInterval, nameof(_config.DirectionCueInterval));
            int required = _config.UseRoomCakeDensity ? 1 : requiredCakeCount == -1 ? _config.RequiredCakeCount : requiredCakeCount;
            if (required < 1) throw new ArgumentException("At least one cake is required.");
            if (!Finite(_config.PickupNoiseLoudness) || _config.PickupNoiseLoudness < 0f || _config.PickupNoiseLoudness > 1f)
                throw new ArgumentException("Pickup loudness must be finite and normalized.");
            if (_config.UseRoomCakeDensity && (_config.MinimumCakesPerRoom < 1 ||
                _config.MaximumCakesPerRoom < _config.MinimumCakesPerRoom ||
                _config.MinimumExitRoomCakes < 0 || _config.MinimumExitRoomCakes > _config.MaximumCakesPerRoom ||
                !Finite(_config.RequiredCakeFraction) || _config.RequiredCakeFraction < 0f || _config.RequiredCakeFraction > 1f))
                throw new ArgumentException("Cake density requires ordered room bounds and a finite normalized required fraction.");
            foreach (CakeAnchorType type in Enum.GetValues(typeof(CakeAnchorType)))
                if (!Finite(Weight(type)) || Weight(type) < 0f) throw new ArgumentException("Anchor weights must be finite and nonnegative.");
            Reset();
            _state.Graph = graph;
            _state.CakeHooks = cakeHooks;
            _state.FasterCollapse = fasterCollapse;
            _state.ShuffledCollapse = shuffledCollapse;
            _state.Hands.WaxHeartAvailable = waxHeart;
            _state.Round = round;
            _state.Players = players.ToArray();
            var distances = LevelGraphUtility.DistancesTo(graph, graph.ExitRoomId, TraversalAccess.Player);
            var reachable = new HashSet<int>();
            foreach (var player in players.Where(value => value != null && value.Id.IsValid && value.IsAlive &&
                Finite(value.Position.x) && Finite(value.Position.y) && Finite(value.Position.z)))
            foreach (var room in graph.Rooms)
            {
                if (!room.ContainsXZ(player.Position) || !room.Cells.Any(cell => cell.Contains(player.Position))) continue;
                foreach (var distance in LevelGraphUtility.TopologicalDistancesFrom(graph, room.Id, TraversalAccess.Player))
                    if (distance.Value >= 0) reachable.Add(distance.Key);
            }
            if (reachable.Count == 0) throw new InvalidOperationException("Floor requires a living player inside an authored room.");
            var candidates = graph.Anchors.Where(anchor => reachable.Contains(anchor.RoomId) && distances[anchor.RoomId] >= 0 && Weight(anchor.Type) > 0f)
                .Where(anchor => graph.Rooms.Any(room => room.Id == anchor.RoomId &&
                    (room.Cells.Count == 1 || room.ContainsXZ(anchor.Position))))
                .OrderBy(anchor => anchor.Id).ToList();
            if (candidates.Count < required)
                throw new InvalidOperationException("Floor requires " + required + " weighted reachable anchors; graph has " + candidates.Count + ".");
            if (_config.UseRoomCakeDensity)
            {
                foreach (var room in candidates.GroupBy(anchor => anchor.RoomId).OrderBy(group => group.Key))
                {
                    var pool = room.ToList();
                    int maximum = Math.Min(_config.MaximumCakesPerRoom, pool.Count);
                    int minimum = Math.Min(room.Key == graph.ExitRoomId ? _config.MinimumExitRoomCakes : _config.MinimumCakesPerRoom, maximum);
                    int count = _random.Next(minimum, maximum + 1);
                    if (pool.Any(a => preferredAnchors != null && preferredAnchors.Contains(a.Id))) count = Math.Max(1, count);
                    for (int index = 0; index < count; index++) _state.SpawnedAnchors.Add(DrawAnchor(pool, preferredAnchors));
                }
                // An exit-only sweep may roll zero, but must still have one reachable goal.
                if (_state.SpawnedAnchors.Count == 0) _state.SpawnedAnchors.Add(DrawAnchor(candidates));
                required = Math.Max(1, Mathf.CeilToInt(_state.SpawnedAnchors.Count * _config.RequiredCakeFraction));
                candidates = _state.SpawnedAnchors.OrderBy(anchor => anchor.Id).ToList();
            }
            else _state.SpawnedAnchors.AddRange(candidates);
            while (_state.SelectedAnchors.Count < required) _state.SelectedAnchors.Add(DrawAnchor(candidates, preferredAnchors));
            PlaceTraps(round);
            _state.MutableActiveAnchors.AddRange(_state.SelectedAnchors);
            foreach (var anchor in _state.SpawnedAnchors) _state.RemainingRewards.Add(anchor.Id, PickupKind.Cake);
            foreach (var room in graph.Rooms)
            {
                _state.MutableRoomPhases.Add(room.Id, RoomPhase.Open);
                _state.MutableRoomHandPhases.Add(room.Id, FloorHandPhase.Idle);
            }
            foreach (var room in graph.Rooms)
                if (room.Id != graph.ExitRoomId && (room.Pocket || distances[room.Id] < 0)) _state.PocketRooms.Add(room.Id);
            var order = graph.Rooms.Where(room => room.Id != graph.ExitRoomId && !_state.PocketRooms.Contains(room.Id))
                .OrderByDescending(room => distances[room.Id])
                .ThenBy(room => room.Id).ToArray();
            if (shuffledCollapse)
            {
                for (int index = order.Length - 1; index > 0; index--)
                {
                    int other = _random.Next(index + 1);
                    var room = order[index]; order[index] = order[other]; order[other] = room;
                }
            }
            bool freezePriority = earlyCollapseRooms != null && order.Any(room => earlyCollapseRooms.Contains(room.Id));
            if (freezePriority) order = order.OrderByDescending(room => earlyCollapseRooms.Contains(room.Id)).ToArray();
            _state.RouteSafeCollapse = shuffledCollapse || freezePriority;
            if (_state.RouteSafeCollapse) _state.PendingCollapseRooms.AddRange(order.Select(room => room.Id));
            for (int index = 0; !_state.RouteSafeCollapse && index < order.Length; index++)
            {
                double duration = _config.TelegraphDuration + _config.TearingDuration + _config.EncroachingDuration;
                double start = index * Math.Max(_config.CollapseInterval, duration);
                _state.CollapseStarts.Add(order[index].Id, start);
                _state.Schedule.Add(new FloorScheduledTransition(order[index].Id, RoomPhase.Telegraph, start));
                _state.Schedule.Add(new FloorScheduledTransition(order[index].Id, RoomPhase.Tearing, start + _config.TelegraphDuration));
                _state.Schedule.Add(new FloorScheduledTransition(order[index].Id, RoomPhase.Encroaching, start + _config.TelegraphDuration + _config.TearingDuration));
                _state.Schedule.Add(new FloorScheduledTransition(order[index].Id, RoomPhase.Closed, start + duration));
            }
            _state.Schedule.Sort((left, right) =>
            {
                int orderAt = left.At.CompareTo(right.At);
                if (orderAt != 0) return orderAt;
                int phaseOrder = left.Phase.CompareTo(right.Phase);
                return phaseOrder != 0 ? phaseOrder : left.RoomId.CompareTo(right.RoomId);
            });
            _state.RequiredCakeCount = _state.SelectedAnchors.Count;
            _state.CueElapsed = _config.DirectionCueInterval;
            _state.IsReady = true;
        }

        public bool RegisterPuzzleReward(int puzzleId, int anchorId, Vector3 position)
        {
            if (!_state.IsReady || _state.Ended || puzzleId == 0 || anchorId == 0 ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                _state.PuzzleRewards.ContainsKey(puzzleId) || _state.Graph.Anchors.Any(a => a.Id == anchorId) ||
                _state.PuzzleRewards.Values.Any(a => a.Id == anchorId)) return false;
            var rooms = _state.Graph.Rooms.Where(r => r.ContainsXZ(position) && r.Cells.Any(c => c.Contains(position))).ToArray();
            if (rooms.Length != 1) return false;
            _state.PuzzleRewards.Add(puzzleId, new LevelAnchor(anchorId, rooms[0].Id, CakeAnchorType.Risk, position));
            return true;
        }

        public bool SolvePuzzle(int puzzleId, int roomId, int anchorId, out LevelAnchor reward)
        {
            reward = default;
            if (!_state.IsReady || _state.Ended || !_state.PuzzleRewards.TryGetValue(puzzleId, out var anchor) ||
                anchor.Id != anchorId || anchor.RoomId != roomId || _state.MutableRoomPhases[roomId] == RoomPhase.Closed ||
                !_state.UnlockedPuzzleRewards.Add(anchorId)) return false;
            _state.SpawnedAnchors.Add(anchor);
            _state.GoldenAnchors.Add(anchor);
            _state.RemainingRewards.Add(anchorId, PickupKind.GoldenCake);
            reward = anchor;
            return true;
        }

        public bool Collect(EntityId playerId, int anchorId, PickupKind kind, long tick, out PickupCollectedFact fact)
            => Collect(playerId, anchorId, kind, tick, out fact, out _);

        public bool Collect(EntityId playerId, int anchorId, PickupKind kind, long tick, out PickupCollectedFact fact, out NoiseEvent noise)
        {
            fact = default;
            noise = default;
            if (!_state.IsReady || _state.Ended || !LivingPlayer(playerId)) return false;
            var anchor = _state.SpawnedAnchors.FirstOrDefault(value => value.Id == anchorId);
            if (anchor.Id == 0 || _state.MutableRoomPhases[anchor.RoomId] == RoomPhase.Closed ||
                !_state.RemainingRewards.TryGetValue(anchorId, out var available) || available != kind) return false;
            if (kind == PickupKind.Cake)
            {
                if (!_state.CollectedCakes.Add(anchorId)) return false;
                _state.RemainingRewards.Remove(anchorId);
                bool required = _state.SelectedAnchors.Any(value => value.Id == anchorId);
                if (!_state.CollapseStarted && (_state.CakeHooks.BlindFaith || required))
                    _state.CakeCount = Math.Min(_state.RequiredCakeCount, _state.CakeCount + (_state.CakeHooks.BlindFaith ? 2 : 1));
                _state.MutableActiveAnchors.RemoveAll(value => value.Id == anchorId);
                if (!_state.CollapseStarted && _state.CakeCount == _state.RequiredCakeCount)
                {
                    CancelExitHolds();
                    _state.CollapseStarted = true;
                    _state.MutableActiveAnchors.Clear();
                    _state.CollapseElapsed = 0d;
                    _state.CueElapsed = _config.DirectionCueInterval;
                    // Blind Faith may finish early: never overlap gold with an uncollected cake.
                    var goldSource = _state.CakeHooks.BlindFaith ? _state.SpawnedAnchors : _state.SelectedAnchors;
                    foreach (var selected in goldSource.Where(value => _state.CollectedCakes.Contains(value.Id)))
                    { _state.GoldenAnchors.Add(selected); _state.RemainingRewards.Add(selected.Id, PickupKind.GoldenCake); }
                }
            }
            else if (kind == PickupKind.GoldenCake)
            {
                if ((!_state.CollapseStarted && !_state.UnlockedPuzzleRewards.Contains(anchorId)) ||
                    !_state.CollectedGoldenCakes.Add(anchorId)) return false;
                _state.GoldenCakeCount++;
                _state.RemainingRewards.Remove(anchorId);
            }
            else return false;
            UpdateExitLock();
            fact = new PickupCollectedFact(playerId, anchorId, kind, _state.CollectedCakes.Count, _state.GoldenCakeCount, tick);
            noise = new NoiseEvent(playerId, anchor.Position, _config.PickupNoiseLoudness, tick, NoiseSourceKind.CakePickup);
            return true;
        }

        public IReadOnlyList<RoomPhaseChangedFact> Tick(float dt, long tick)
        {
            if (!Finite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            var facts = new List<RoomPhaseChangedFact>();
            if (!_state.IsReady || _state.Ended) return facts;
            _state.Tick = tick;
            _state.Elapsed += dt;
            _state.CueElapsed += dt;
            float collapseDt = dt * (_state.FasterCollapse ? _config.FasterCollapseMultiplier : 1f);
            if (_state.CollapseStarted) _state.CollapseElapsed += collapseDt;
            var protectedRooms = _state.RouteSafeCollapse ? FloorCollapseUtility.EscapeRooms(_state.Graph, _state.RoomPhases, _state.Players) : null;
            if (_state.RouteSafeCollapse && _state.NextTransition < _state.Schedule.Count &&
                protectedRooms.Contains(_state.Schedule[_state.NextTransition].RoomId))
            {
                int room = _state.Schedule[_state.NextTransition].RoomId;
                _state.CollapseStarts[room] += collapseDt;
                _state.NextShuffledStart += collapseDt;
                for (int i = _state.NextTransition; i < _state.Schedule.Count; i++)
                {
                    var pending = _state.Schedule[i];
                    _state.Schedule[i] = new FloorScheduledTransition(pending.RoomId, pending.Phase, pending.At + collapseDt);
                }
            }
            while (_state.CollapseStarted)
            {
                if (_state.RouteSafeCollapse && _state.NextTransition == _state.Schedule.Count)
                    ScheduleShuffledRoom(protectedRooms);
                if (_state.NextTransition == _state.Schedule.Count || _state.Schedule[_state.NextTransition].At > _state.CollapseElapsed ||
                    (protectedRooms != null && protectedRooms.Contains(_state.Schedule[_state.NextTransition].RoomId))) break;
                var transition = _state.Schedule[_state.NextTransition++];
                ApplyTransition(transition.RoomId, transition.Phase, tick, facts);
            }
            foreach (var pocket in _state.PocketStarts.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key).ToArray())
            {
                if (protectedRooms != null && protectedRooms.Contains(pocket.Key))
                { _state.PocketStarts[pocket.Key] += dt; continue; }
                double age = (_state.Elapsed - pocket.Value) * (_state.FasterCollapse ? _config.FasterCollapseMultiplier : 1f);
                var phases = new[] { RoomPhase.Telegraph, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed };
                double[] thresholds = { 0d, _config.TelegraphDuration, _config.TelegraphDuration + _config.TearingDuration,
                    _config.TelegraphDuration + _config.TearingDuration + _config.EncroachingDuration };
                for (int i = 0; i < phases.Length; i++)
                    if (age >= thresholds[i] && _state.MutableRoomPhases[pocket.Key] < phases[i])
                        ApplyTransition(pocket.Key, phases[i], tick, facts);
            }
            UpdateExitLock();
            return facts;
        }

        public bool ActivatePocket(int roomId)
        {
            if (!_state.IsReady || _state.Ended || roomId == _state.Graph.ExitRoomId ||
                !_state.PocketRooms.Contains(roomId) || _state.PocketStarts.ContainsKey(roomId)) return false;
            _state.PocketStarts.Add(roomId, _state.Elapsed + _config.PocketCollapseDelay *
                (_state.FasterCollapse ? _config.FasterCollapseDurationMultiplier : 1f));
            return true;
        }

        private void ScheduleShuffledRoom(HashSet<int> protectedRooms)
        {
            int index = _state.PendingCollapseRooms.FindIndex(room => !protectedRooms.Contains(room));
            if (index < 0)
            { _state.NextShuffledStart = _state.CollapseElapsed; return; }
            int roomId = _state.PendingCollapseRooms[index];
            _state.PendingCollapseRooms.RemoveAt(index);
            double start = _state.NextShuffledStart;
            double duration = _config.TelegraphDuration + _config.TearingDuration + _config.EncroachingDuration;
            _state.CollapseStarts.Add(roomId, start);
            _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Telegraph, start));
            _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Tearing, start + _config.TelegraphDuration));
            _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Encroaching, start + _config.TelegraphDuration + _config.TearingDuration));
            _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Closed, start + duration));
            _state.NextShuffledStart = start + Math.Max(_config.CollapseInterval, duration);
        }

        private void ApplyTransition(int roomId, RoomPhase phase, long tick, List<RoomPhaseChangedFact> facts)
        {
            _state.MutableRoomPhases[roomId] = phase;
            if (phase == RoomPhase.Closed)
                foreach (var anchor in _state.SpawnedAnchors.Where(value => value.RoomId == roomId))
                    if (_state.RemainingRewards.TryGetValue(anchor.Id, out var kind))
                    {
                        _state.RemainingRewards.Remove(anchor.Id);
                        _state.CakeLosses.Add(new FloorCakeLoss(anchor.Id, anchor.RoomId, kind, tick));
                    }
            facts.Add(new RoomPhaseChangedFact(roomId, phase, tick));
        }

        public IReadOnlyList<FloorCakeLoss> DrainCakeLosses()
        {
            var facts = _state.CakeLosses.ToArray();
            _state.CakeLosses.Clear();
            return facts;
        }

        public bool ConsumeCueDue()
        {
            if (!_state.IsReady || _state.Ended || _state.CueElapsed < _config.DirectionCueInterval) return false;
            _state.CueElapsed %= _config.DirectionCueInterval;
            return true;
        }

        public FloorDisplaySnapshot SelectCue(IReadOnlyList<FloorPathCandidate> paths, float openingProgress = 0f)
        {
            bool available = false;
            var direction = Vector3.zero;
            float nearest = float.PositiveInfinity;
            int nearestId = int.MaxValue;
            if (_state.IsReady && !_state.Ended && !_state.CakeHooks.BlindFaith && paths != null)
            foreach (var path in paths)
            {
                bool wanted = _state.CollapseStarted ? path.AnchorId == 0 : _state.MutableActiveAnchors.Any(anchor => anchor.Id == path.AnchorId);
                if (!wanted || !Finite(path.Length) || path.Length < 0f || !Finite(path.Direction.x) || !Finite(path.Direction.y) || !Finite(path.Direction.z)) continue;
                if (path.Length > nearest || path.Length == nearest && path.AnchorId >= nearestId) continue;
                nearest = path.Length; nearestId = path.AnchorId; available = true; direction = path.Direction;
            }
            _state.CueAnchorId = available ? nearestId : -1;
            _state.Display = new FloorDisplaySnapshot(_state.CakeCount, _state.RequiredCakeCount,
                _state.GoldenCakeCount, _state.ExitState, available, direction, NormalizedProgress(openingProgress));
            return _state.Display;
        }

        public FloorDisplaySnapshot Snapshot(float openingProgress = 0f) => new FloorDisplaySnapshot(_state.CakeCount, _state.RequiredCakeCount,
            _state.GoldenCakeCount, _state.ExitState, _state.Display.HasCue && !_state.Ended, _state.Display.CueDirection, NormalizedProgress(openingProgress));

        public bool ContactExit(EntityId id, long tick, out ExitReachedFact fact)
        {
            fact = default;
            if (!_state.IsReady || _state.Ended || !LivingPlayer(id)) return false;
            if (_state.ExitState == ExitState.Locked)
            {
                RequirePositive(_config.EarlyBailHoldDuration, nameof(_config.EarlyBailHoldDuration));
                if (!_state.ExitHolds.ContainsKey(id)) _state.ExitHolds.Add(id, 0d);
                return false;
            }
            if (_state.ExitState != ExitState.Open) return false;
            _state.Ended = true;
            CancelExitHolds();
            fact = new ExitReachedFact(id, tick);
            return true;
        }

        public void LeaveExit(EntityId id) => _state.ExitHolds.Remove(id);
        public void CancelExitHolds() => _state.ExitHolds.Clear();

        public bool TickExitHold(float dt, long tick, out ExitReachedFact fact)
        {
            if (!Finite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            fact = default;
            if (!_state.IsReady || _state.Ended || _state.ExitState != ExitState.Locked)
            { CancelExitHolds(); return false; }
            foreach (var id in _state.ExitHolds.Keys.OrderBy(value => value.Value).ToArray())
            {
                if (!LivingPlayer(id)) { LeaveExit(id); continue; }
                _state.ExitHolds[id] += dt;
                if (_state.ExitHolds[id] < _config.EarlyBailHoldDuration) continue;
                _state.Ended = true;
                CancelExitHolds();
                fact = new ExitReachedFact(id, tick);
                return true;
            }
            return false;
        }

        // Room occupancy itself is never a kill. Only the hand controller can confirm
        // that a committed damaging grab killed its registered player.
        public bool ContactLethalRoom(EntityId id, int roomId, long tick, out FloorLethalContactFact fact)
        {
            fact = default;
            if (!_state.IsReady || _state.Ended || !_state.MutableRoomPhases.TryGetValue(roomId, out var phase) ||
                (phase != RoomPhase.Tearing && phase != RoomPhase.Encroaching && phase != RoomPhase.Closed) ||
                !_state.Players.Any(player => player != null && player.Id == id && !player.IsAlive)) return false;
            _state.Ended = true;
            CancelExitHolds();
            fact = new FloorLethalContactFact(id, roomId, tick);
            return true;
        }

        public bool TelegraphOptionalRoom(int roomId)
        {
            return _state.IsReady && !_state.Ended && roomId != _state.Graph.ExitRoomId && _state.MutableRoomPhases.TryGetValue(roomId, out var phase) &&
                phase == RoomPhase.Open && _state.OptionalCrackedRooms.Add(roomId);
        }

        public RoomDestructionSample Destruction(int roomId)
        {
            if (!_state.IsReady || !_state.MutableRoomPhases.TryGetValue(roomId, out var phase)) return default;
            bool pocket = _state.PocketStarts.TryGetValue(roomId, out double start);
            if (!pocket && !_state.CollapseStarts.TryGetValue(roomId, out start)) return new RoomDestructionSample(roomId, RoomPhase.Open, 0f);
            double age = Math.Max(0d, (pocket ? _state.Elapsed : _state.CollapseElapsed) - start);
            if (pocket && _state.FasterCollapse) age *= _config.FasterCollapseMultiplier;
            float progress = 0f;
            if (phase == RoomPhase.Telegraph) progress = (float)(age / _config.TelegraphDuration);
            else if (phase == RoomPhase.Tearing) progress = (float)((age - _config.TelegraphDuration) / _config.TearingDuration);
            else if (phase == RoomPhase.Encroaching) progress = (float)((age - _config.TelegraphDuration - _config.TearingDuration) / _config.EncroachingDuration);
            else if (phase == RoomPhase.Closed) progress = 1f;
            double duration = _config.TelegraphDuration + _config.TearingDuration + _config.EncroachingDuration;
            bool warning = phase != RoomPhase.Open && phase != RoomPhase.Closed;
            double warningAge = Math.Min(age, duration);
            double slope = (_config.WarningPulseEndRate - _config.WarningPulseStartRate) / duration;
            double cycles = _config.WarningPulseStartRate * warningAge + 0.5d * slope * warningAge * warningAge;
            float rate = warning ? (float)(_config.WarningPulseStartRate + slope * warningAge) : 0f;
            rate *= _state.FasterCollapse ? _config.FasterCollapseMultiplier : 1f;
            return new RoomDestructionSample(roomId, phase, Mathf.Clamp01(progress), rate,
                warning ? (float)(cycles - Math.Floor(cycles)) : 0f);
        }

        public IReadOnlyPlayerState CuePlayer() => _state.Players.Where(player => player != null && player.IsAlive).OrderBy(player => player.Id.Value).FirstOrDefault();

        public void Reset()
        {
            _state.MutableTraps.Clear(); _state.SprungTraps.Clear(); _state.GoldenAnchors.Clear();
            _state.PuzzleRewards.Clear(); _state.UnlockedPuzzleRewards.Clear(); _state.RouteSafeCollapse = false;
            _state.CakeHooks = default; _state.CollapseStarted = false; _state.CueAnchorId = -1;
            _state.TrapTickElapsed = 0d; _state.Elapsed = 0d;
            _state.PocketRooms.Clear(); _state.PocketStarts.Clear();
            _state.PendingCollapseRooms.Clear(); _state.ShuffledCollapse = false; _state.NextShuffledStart = 0d;
            _state.Hands.WaxHeartAvailable = false; _state.Round = 0;
            CancelExitHolds();
            _state.IsReady = false; _state.Ended = false; _state.Tick = 0;
            _state.CakeCount = 0; _state.GoldenCakeCount = 0; _state.RequiredCakeCount = 0;
            _state.ExitState = ExitState.Locked; _state.CollapseElapsed = 0d; _state.CueElapsed = 0d; _state.NextTransition = 0;
            _state.SelectedAnchors.Clear(); _state.MutableActiveAnchors.Clear(); _state.MutableRoomPhases.Clear();
            _state.SpawnedAnchors.Clear(); _state.RemainingRewards.Clear(); _state.CakeLosses.Clear();
            _state.MutableRoomHandPhases.Clear(); _state.Hands.Contacts.Clear(); _state.Hands.WaxWards.Clear(); _state.FasterCollapse = false;
            _state.CollectedCakes.Clear(); _state.CollectedGoldenCakes.Clear(); _state.Schedule.Clear(); _state.CollapseStarts.Clear(); _state.OptionalCrackedRooms.Clear();
            _state.Players = Array.Empty<IReadOnlyPlayerState>(); _state.Graph = null; _state.Display = default;
        }

        private bool LivingPlayer(EntityId id) => id.IsValid && _state.Players.Any(player => player != null && player.Id == id && player.IsAlive);
        private void PlaceTraps(int round)
        {
            if (round < _config.TrapStartRound) return;
            var optional = _state.SpawnedAnchors.Where(a => !_state.SelectedAnchors.Any(r => r.Id == a.Id)).OrderBy(a => a.Id).ToList();
            int count = Math.Min(_config.MaximumTraps, Math.Min(_state.Graph.Rooms.Count / _config.RoomsPerTrap,
                Mathf.FloorToInt(optional.Count * _config.OptionalTrapShare)));
            int extra = _state.CakeHooks.MoreTraps ? _config.ExtraBlinderTraps : 0;
            for (int index = 0; index < count + extra && optional.Count > 0; index++)
            {
                int choice = _random.Next(optional.Count);
                var anchor = optional[choice]; optional.RemoveAt(choice);
                var kind = index < count ? (FloorTrapKind)_random.Next(3) : FloorTrapKind.Blind;
                _state.MutableTraps.Add(new FloorTrapSpawn(anchor, kind));
            }
            if (_state.CakeHooks.SweetTooth && _state.MutableTraps.Count > 0) _state.MutableTraps.RemoveAt(0);
            _state.SpawnedAnchors.RemoveAll(a => _state.MutableTraps.Any(t => t.Anchor.Id == a.Id));
        }

        public bool SpringTrap(EntityId playerId, int trapId, long tick, out FloorTrapSprungFact fact, out NoiseEvent noise)
        {
            fact = default; noise = default;
            if (!_state.IsReady || _state.Ended || !LivingPlayer(playerId)) return false;
            var trap = _state.MutableTraps.FirstOrDefault(value => value.Anchor.Id == trapId);
            if (trap.Anchor.Id == 0 || _state.MutableRoomPhases[trap.Anchor.RoomId] == RoomPhase.Closed || !_state.SprungTraps.Add(trapId)) return false;
            fact = new FloorTrapSprungFact(trapId, trap.Kind, trap.Anchor.RoomId, trap.Anchor.Position, tick, playerId);
            if (trap.Kind == FloorTrapKind.Announce)
                noise = new NoiseEvent(playerId, trap.Anchor.Position, _config.TrapAnnounceLoudness, tick, NoiseSourceKind.Trap);
            return true;
        }

        public IReadOnlyList<FloorTrapSpawn> TickTraps(float dt)
        {
            if (!Finite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!_state.IsReady || _state.Ended || _state.CakeHooks.SilentTraps) return Array.Empty<FloorTrapSpawn>();
            _state.TrapTickElapsed += dt;
            if (_state.TrapTickElapsed < _config.TrapTickInterval) return Array.Empty<FloorTrapSpawn>();
            _state.TrapTickElapsed %= _config.TrapTickInterval;
            return _state.MutableTraps.Where(t => t.Kind == FloorTrapKind.Blind && !_state.SprungTraps.Contains(t.Anchor.Id) &&
                _state.MutableRoomPhases[t.Anchor.RoomId] != RoomPhase.Closed).ToArray();
        }

        public IReadOnlyList<GuidanceTarget> GuidanceTargets(bool whiteFallback, GuidanceTarget? golden = null)
        {
            var targets = new List<GuidanceTarget>();
            if (TryWhiteGuidance(whiteFallback, out var white)) targets.Add(white);
            if (_state.IsReady && !_state.Ended && !_state.CakeHooks.BlindFaith && golden.HasValue)
                targets.Add(golden.Value);
            return targets.AsReadOnly();
        }

        public bool TryWhiteGuidance(bool fallback, out GuidanceTarget target)
        {
            target = default;
            if (!_state.IsReady || _state.Ended || _state.CakeHooks.BlindFaith || _state.CueAnchorId < 0) return false;
            int id = _state.CueAnchorId;
            if (id != 0 && !_state.MutableActiveAnchors.Any(a => a.Id == id)) return false;
            var position = id == 0 ? _state.Graph.ExitPosition : _state.MutableActiveAnchors.First(a => a.Id == id).Position;
            target = new GuidanceTarget(GuidanceKind.WhiteArrow, _state.Display.CueDirection, position, id, isFallback: fallback);
            return true;
        }

        public bool TryGoldenTarget(Vector3 from, out LevelAnchor anchor)
        {
            anchor = default;
            if (!_state.IsReady || _state.Ended || !_state.CakeHooks.GoldenSense || _state.CakeHooks.BlindFaith) return false;
            anchor = _state.GoldenAnchors.Where(a => _state.RemainingRewards.TryGetValue(a.Id, out var kind) && kind == PickupKind.GoldenCake)
                .OrderBy(a => (a.Position - from).sqrMagnitude).ThenBy(a => a.Id).FirstOrDefault();
            return anchor.Id != 0;
        }

        private void UpdateExitLock()
        {
            if (!_state.CollapseStarted || _state.ExitState == ExitState.Open) return;
            int remaining = _state.RemainingRewards.Count(pair => pair.Value == PickupKind.GoldenCake && !_state.UnlockedPuzzleRewards.Contains(pair.Key));
            int collected = _state.CollectedGoldenCakes.Count(id => !_state.UnlockedPuzzleRewards.Contains(id));
            // Lost gold reduces the available pool. Zero collected waits for the full collapse.
            bool quota = collected > 0 && collected >= Mathf.CeilToInt((remaining + collected) * _config.GreedyDoorShare);
            if (!_state.CakeHooks.GreedyDoor || !_state.GoldenAnchors.Any(a => !_state.UnlockedPuzzleRewards.Contains(a.Id)) || quota ||
                (_state.PendingCollapseRooms.Count == 0 && _state.NextTransition == _state.Schedule.Count))
            { _state.ExitState = ExitState.Open; CancelExitHolds(); }
        }
        private static float NormalizedProgress(float value) => Finite(value) ? Mathf.Clamp01(value) : 0f;
        private LevelAnchor DrawAnchor(List<LevelAnchor> candidates, IReadOnlyCollection<int> preferred = null)
        {
            if (preferred != null)
            {
                int index = candidates.FindIndex(a => preferred.Contains(a.Id));
                if (index >= 0) { var first = candidates[index]; candidates.RemoveAt(index); return first; }
            }
            double sample = _random.NextDouble() * candidates.Sum(anchor => (double)Weight(anchor.Type));
            int selected = candidates.Count - 1;
            for (int index = 0; index < candidates.Count; index++)
            {
                sample -= Weight(candidates[index].Type);
                if (sample < 0d) { selected = index; break; }
            }
            var anchor = candidates[selected];
            candidates.RemoveAt(selected);
            return anchor;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void RequirePositive(float value, string name) { if (!Finite(value) || value <= 0f) throw new ArgumentException(name + " must be finite and positive."); }
        private float Weight(CakeAnchorType type)
        {
            switch (type)
            {
                case CakeAnchorType.Flow: return _config.FlowWeight;
                case CakeAnchorType.Precision: return _config.PrecisionWeight;
                case CakeAnchorType.Detour: return _config.DetourWeight;
                case CakeAnchorType.Risk: return _config.RiskWeight;
                case CakeAnchorType.Vertical: return _config.VerticalWeight;
                default: return 0f;
            }
        }
    }
}
