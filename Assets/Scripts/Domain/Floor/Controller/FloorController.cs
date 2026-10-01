// ============================================================================
// FloorController.cs
// ============================================================================
// PURPOSE:
//   Places every reachable cake and starts the exit race only after collection.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Select seeded traps without thinning the remaining walking lines.
//   - Accept one-shot cakes and Passage-only gold with accurate remaining counts.
//   - Start deterministic farthest-first logarithmic collapse after the last pickup.
//   - Retain reachable guidance identities with path-length hysteresis.
//   - Compute trap timing and admit only normal open-exit contact.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Legacy density/count selection protects some sockets from traps only.
//   Owner pass 2 supersedes early/shuffled/faster collapse and gold quotas: all
//   registered cakes (including activated Passage gold) precede the exit race.
//   Reads injected Player views only. Managed bounds math establishes cell membership.
//   Pocket phases follow schedule order, not legacy serialized enum ordinals.
//   Inactive pockets remain dormant. Activated pockets join the collection-gated
//   schedule; no independent timer can collapse a room during collection.
//   No persistent singleton or competing simulation tick is created.
//   Compatibility hook arguments remain accepted but cannot change exact owner timings.
//   Wax Heart is one charge per floor. No uncollected-cake loss path remains.
//   ContactExit ignores locked contact regardless of elapsed time.
//   Tick scratch retains capacity; returned facts remain independent snapshots.
//   Collapse events are ordered by scheduled time, then room id.
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
        private readonly List<RoomPhaseChangedFact> _phaseFacts = new List<RoomPhaseChangedFact>();
        private readonly List<FloorTrapSpawn> _trapTicks = new List<FloorTrapSpawn>();
        private readonly List<GuidanceTarget> _guidanceTargets = new List<GuidanceTarget>();
        public FloorController(FloorBehaviorState state, FloorConfig config, System.Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public void Initialize(LevelGraph graph, IReadOnlyList<IReadOnlyPlayerState> players, int requiredCakeCount = -1,
            bool fasterCollapse = false, bool shuffledCollapse = false, int round = 1, FloorCakeHooks cakeHooks = default, bool waxHeart = false,
            IReadOnlyCollection<int> preferredAnchors = null, IReadOnlyCollection<int> earlyCollapseRooms = null, int optionalGoldenCakeCount = 0)
        {
            if (graph == null || players == null) throw new ArgumentNullException();
            if (optionalGoldenCakeCount < 0) throw new ArgumentOutOfRangeException(nameof(optionalGoldenCakeCount));
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
                if (!room.ContainsXZ(player.Position) || !room.Cells.Any(cell => FloorBoundsUtility.Contains(cell, player.Position))) continue;
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
            var allCandidates = candidates.ToArray();
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
            // Gold exists only once the Passage altar actually registers its reward.
            // Legacy reserved puzzle/bonus counts must not create phantom HUD totals.
            var occupied = new HashSet<int>(_state.SpawnedAnchors.Select(a => a.Id));
            occupied.UnionWith(_state.MutableTraps.Select(t => t.Anchor.Id));
            foreach (var anchor in allCandidates)
                if (occupied.Add(anchor.Id)) _state.SpawnedAnchors.Add(anchor);
            _state.TotalCakes = _state.SpawnedAnchors.Count;
            _state.MutableActiveAnchors.AddRange(_state.SelectedAnchors);
            var selectedIds = new HashSet<int>(_state.SelectedAnchors.Select(a => a.Id));
            _state.MutableActiveAnchors.AddRange(_state.SpawnedAnchors.Where(a => !selectedIds.Contains(a.Id)));
            foreach (var anchor in _state.SpawnedAnchors) _state.RemainingRewards.Add(anchor.Id, PickupKind.Cake);
            foreach (var room in graph.Rooms)
            {
                _state.MutableRoomPhases.Add(room.Id, RoomPhase.Open);
                _state.MutableRoomHandPhases.Add(room.Id, FloorHandPhase.Idle);
            }
            foreach (var room in graph.Rooms)
                if (room.Id != graph.ExitRoomId && (room.Pocket || distances[room.Id] < 0)) _state.PocketRooms.Add(room.Id);
            _state.RequiredCakeCount = _state.TotalCakes;
            _state.CueElapsed = _config.DirectionCueInterval;
            _state.IsReady = true;
        }

        public bool RegisterPuzzleReward(int puzzleId, int anchorId, Vector3 position)
        {
            if (!_state.IsReady || _state.Ended || puzzleId == 0 || anchorId == 0 ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                _state.PuzzleRewards.ContainsKey(puzzleId) || _state.Graph.Anchors.Any(a => a.Id == anchorId) ||
                _state.PuzzleRewards.Values.Any(a => a.Id == anchorId) || _state.PassageRewards.Contains(anchorId)) return false;
            var rooms = _state.Graph.Rooms.Where(r => r.ContainsXZ(position) && r.Cells.Any(c => FloorBoundsUtility.Contains(c, position))).ToArray();
            if (rooms.Length != 1) return false;
            _state.PuzzleRewards.Add(puzzleId, new LevelAnchor(anchorId, rooms[0].Id, CakeAnchorType.Risk, position));
            return true;
        }

        public bool SolvePuzzle(int puzzleId, int roomId, int anchorId, out LevelAnchor reward)
        {
            reward = default;
            // Puzzle completion is still routed by its owner, but no longer mints gold.
            return false;
        }

        public bool RegisterPassageReward(LevelAnchor anchor)
        {
            if (!_state.IsReady || _state.Ended || _state.CollapseStarted || anchor.Id == 0 || !_state.PocketRooms.Contains(anchor.RoomId) ||
                _state.MutableRoomPhases[anchor.RoomId] == RoomPhase.Closed ||
                !Finite(anchor.Position.x) || !Finite(anchor.Position.y) || !Finite(anchor.Position.z) ||
                !_state.Graph.Rooms.Any(r => r.Id == anchor.RoomId && r.ContainsXZ(anchor.Position) && r.Cells.Any(c => FloorBoundsUtility.Contains(c, anchor.Position))) ||
                _state.Graph.Anchors.Any(a => a.Id == anchor.Id) || _state.PuzzleRewards.Values.Any(a => a.Id == anchor.Id) ||
                !_state.PassageRewards.Add(anchor.Id)) return false;
            _state.SpawnedAnchors.Add(anchor);
            _state.GoldenAnchors.Add(anchor);
            _state.RemainingRewards.Add(anchor.Id, PickupKind.GoldenCake);
            _state.MutableActiveAnchors.Add(anchor);
            _state.TotalGoldenCakes++;
            _state.OptionalGoldenCakeCount++;
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
                _state.CakeCount = _state.CollectedCakes.Count;
                _state.MutableActiveAnchors.RemoveAll(value => value.Id == anchorId);
            }
            else if (kind == PickupKind.GoldenCake)
            {
                if (!_state.PassageRewards.Contains(anchorId) ||
                    !_state.CollectedGoldenCakes.Add(anchorId)) return false;
                _state.GoldenCakeCount++;
                _state.RemainingRewards.Remove(anchorId);
                _state.MutableActiveAnchors.RemoveAll(value => value.Id == anchorId);
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
            var facts = _phaseFacts; facts.Clear();
            if (!_state.IsReady || _state.Ended) return Array.Empty<RoomPhaseChangedFact>();
            _state.Tick = tick;
            _state.Elapsed += dt;
            _state.CueElapsed += dt;
            if (_state.CollapseStarted) _state.CollapseElapsed += dt;
            while (_state.CollapseStarted)
            {
                if (_state.NextTransition == _state.Schedule.Count || _state.Schedule[_state.NextTransition].At > _state.CollapseElapsed) break;
                var transition = _state.Schedule[_state.NextTransition++];
                ApplyTransition(transition.RoomId, transition.Phase, tick, facts);
            }
            return Snapshot(facts);
        }

        public bool ActivatePocket(int roomId)
        {
            if (!_state.IsReady || _state.Ended || _state.CollapseStarted || roomId == _state.Graph.ExitRoomId ||
                !_state.PocketRooms.Contains(roomId) || _state.PocketStarts.ContainsKey(roomId)) return false;
            _state.PocketStarts.Add(roomId, 0d);
            return true;
        }

        private void BuildCollapseSchedule()
        {
            var distance = LevelGraphUtility.DistancesTo(_state.Graph, _state.Graph.ExitRoomId, TraversalAccess.Player);
            var order = _state.Graph.Rooms.Where(r => r.Id != _state.Graph.ExitRoomId &&
                (!_state.PocketRooms.Contains(r.Id) || _state.PocketStarts.ContainsKey(r.Id)))
                .OrderByDescending(r => distance[r.Id] < 0 ? int.MaxValue : distance[r.Id]).ThenBy(r => r.Id).ToArray();
            double total = FloorConfig.FirstLevelCollapseSeconds * (1d + _config.CollapseLogGrowth *
                Math.Log(Math.Max(1d, (order.Length + 1d) / _config.CollapseReferenceRooms)));
            // A one-room hazard has no launch interval; it simply runs its ten seconds.
            double spacing = order.Length < 2 ? 0d : (total - FloorConfig.RoomCollapseSeconds) / (order.Length - 1);
            for (int index = 0; index < order.Length; index++)
            {
                int roomId = order[index].Id;
                double start = index * spacing;
                _state.CollapseStarts.Add(roomId, start);
                _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Telegraph, start));
                _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Tearing, start + _config.CollapseTelegraphSeconds));
                _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Encroaching,
                    start + _config.CollapseTelegraphSeconds + _config.CollapseTearingSeconds));
                _state.Schedule.Add(new FloorScheduledTransition(roomId, RoomPhase.Closed, start + FloorConfig.RoomCollapseSeconds));
            }
            _state.Schedule.Sort((a, b) =>
            {
                int time = a.At.CompareTo(b.At);
                if (time != 0) return time;
                int room = a.RoomId.CompareTo(b.RoomId);
                return room != 0 ? room : a.Phase.CompareTo(b.Phase);
            });
        }

        private void ApplyTransition(int roomId, RoomPhase phase, long tick, List<RoomPhaseChangedFact> facts)
        {
            _state.MutableRoomPhases[roomId] = phase;
            facts.Add(new RoomPhaseChangedFact(roomId, phase, tick));
        }

        public IReadOnlyList<FloorCakeLoss> DrainCakeLosses()
        {
            var facts = Snapshot(_state.CakeLosses);
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
            bool available = TrySelectPath(paths, _state.CueAnchorId, false, out var selected);
            _state.CueAnchorId = available ? selected.AnchorId : -1;
            _state.Display = new FloorDisplaySnapshot(_state.CollectedCakes.Count, _state.RequiredCakeCount,
                _state.GoldenCakeCount, _state.ExitState, available, selected.Direction, NormalizedProgress(openingProgress),
                _state.TotalCakes, _state.TotalGoldenCakes, _state.CakeHooks.HiddenCount, _state.OptionalGoldenCakeCount);
            return _state.Display;
        }

        public FloorDisplaySnapshot Snapshot(float openingProgress = 0f) => new FloorDisplaySnapshot(_state.CollectedCakes.Count, _state.RequiredCakeCount,
            _state.GoldenCakeCount, _state.ExitState, _state.Display.HasCue && !_state.Ended, _state.Display.CueDirection, NormalizedProgress(openingProgress),
            _state.TotalCakes, _state.TotalGoldenCakes, _state.CakeHooks.HiddenCount, _state.OptionalGoldenCakeCount);

        public bool ContactExit(EntityId id, long tick, out ExitReachedFact fact)
        {
            fact = default;
            if (!_state.IsReady || _state.Ended || !LivingPlayer(id)) return false;
            if (_state.ExitState != ExitState.Open) return false;
            _state.Ended = true;
            fact = new ExitReachedFact(id, tick);
            return true;
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
            fact = new FloorLethalContactFact(id, roomId, tick);
            return true;
        }

        public bool TelegraphOptionalRoom(int roomId)
        {
            return _state.IsReady && !_state.Ended && _state.CollapseStarted && roomId != _state.Graph.ExitRoomId && _state.MutableRoomPhases.TryGetValue(roomId, out var phase) &&
                phase == RoomPhase.Open && _state.OptionalCrackedRooms.Add(roomId);
        }

        public RoomDestructionSample Destruction(int roomId)
        {
            if (!_state.IsReady || !_state.MutableRoomPhases.TryGetValue(roomId, out var phase)) return default;
            if (!_state.CollapseStarts.TryGetValue(roomId, out double start)) return new RoomDestructionSample(roomId, RoomPhase.Open, 0f);
            double age = Math.Max(0d, _state.CollapseElapsed - start);
            float progress = 0f;
            if (phase == RoomPhase.Telegraph) progress = (float)(age / _config.CollapseTelegraphSeconds);
            else if (phase == RoomPhase.Tearing) progress = (float)((age - _config.CollapseTelegraphSeconds) / _config.CollapseTearingSeconds);
            else if (phase == RoomPhase.Encroaching) progress = (float)((age - _config.CollapseTelegraphSeconds - _config.CollapseTearingSeconds) / _config.CollapseEncroachingSeconds);
            else if (phase == RoomPhase.Closed) progress = 1f;
            double duration = FloorConfig.RoomCollapseSeconds;
            bool warning = phase != RoomPhase.Open && phase != RoomPhase.Closed;
            double warningAge = Math.Min(age, duration);
            double slope = (_config.WarningPulseEndRate - _config.WarningPulseStartRate) / duration;
            double cycles = _config.WarningPulseStartRate * warningAge + 0.5d * slope * warningAge * warningAge;
            float rate = warning ? (float)(_config.WarningPulseStartRate + slope * warningAge) : 0f;

            return new RoomDestructionSample(roomId, phase, Mathf.Clamp01(progress), rate,
                warning ? (float)(cycles - Math.Floor(cycles)) : 0f);
        }

        public IReadOnlyPlayerState CuePlayer()
        {
            IReadOnlyPlayerState selected = null;
            for (int i = 0; i < _state.Players.Count; i++)
            {
                var player = _state.Players[i];
                if (player != null && player.IsAlive && (selected == null || player.Id.Value < selected.Id.Value)) selected = player;
            }
            return selected;
        }

        public void Reset()
        {
            _phaseFacts.Clear(); _trapTicks.Clear(); _guidanceTargets.Clear();
            _state.MutableTraps.Clear(); _state.SprungTraps.Clear(); _state.GoldenAnchors.Clear();
            _state.BonusGoldenAnchors.Clear(); _state.TotalCakes = 0; _state.TotalGoldenCakes = 0;
            _state.CollapseCakeTarget = 0; _state.CollapseCakeCredit = 0;
            _state.PuzzleRewards.Clear(); _state.UnlockedPuzzleRewards.Clear(); _state.RouteSafeCollapse = false;
            _state.PassageRewards.Clear();
            _state.OptionalGoldenCakeCount = 0; _state.ActiveEffects = null;
            _state.BlinderPolicies.Clear(); _state.AddedBlinderTraps = 0;
            _state.CakeHooks = default; _state.CollapseStarted = false; _state.CueAnchorId = -1;
            _state.GoldenCueAnchorId = -1; _state.CueRoomId = 0; _state.CuePlayerId = EntityId.None;
            _state.TrapTickElapsed = 0d; _state.Elapsed = 0d;
            _state.PocketRooms.Clear(); _state.PocketStarts.Clear();
            _state.PendingCollapseRooms.Clear(); _state.ShuffledCollapse = false; _state.NextShuffledStart = 0d;
            _state.Hands.WaxHeartAvailable = false; _state.Round = 0;
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
                noise = new NoiseEvent(playerId, trap.Anchor.Position, _config.TrapAnnounceLoudness, tick,
                    NoiseSourceKind.Trap, NoiseOrigin.PlayerTriggeredCakeTrap);
            return true;
        }

        public IReadOnlyList<FloorTrapSpawn> TickTraps(float dt)
        {
            if (!Finite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!_state.IsReady || _state.Ended || _state.CakeHooks.SilentTraps) return Array.Empty<FloorTrapSpawn>();
            foreach (var policy in _state.BlinderPolicies.Values)
                if (policy.SilentTraps) return Array.Empty<FloorTrapSpawn>();
            _state.TrapTickElapsed += dt;
            if (_state.TrapTickElapsed < _config.TrapTickInterval) return Array.Empty<FloorTrapSpawn>();
            _state.TrapTickElapsed %= _config.TrapTickInterval;
            _trapTicks.Clear();
            foreach (var trap in _state.MutableTraps)
                if (trap.Kind == FloorTrapKind.Blind && !_state.SprungTraps.Contains(trap.Anchor.Id) &&
                    _state.MutableRoomPhases[trap.Anchor.RoomId] != RoomPhase.Closed) _trapTicks.Add(trap);
            return Snapshot(_trapTicks);
        }

        public IReadOnlyList<GuidanceTarget> GuidanceTargets(bool whiteFallback, GuidanceTarget? golden = null)
        {
            var targets = _guidanceTargets; targets.Clear();
            if (TryWhiteGuidance(whiteFallback, out var white)) targets.Add(white);
            if (_state.IsReady && !_state.Ended && !_state.CakeHooks.BlindFaith && golden.HasValue)
                targets.Add(golden.Value);
            var player = CuePlayer();
            if (_state.IsReady && !_state.Ended && _state.ExitState == ExitState.Open && player != null &&
                (_state.ActiveEffects?.Has(new EffectId("exit-sense")) ?? false))
                targets.Add(new GuidanceTarget(GuidanceKind.ExitThroughWalls,
                    (_state.Graph.ExitPosition - player.Position).normalized, _state.Graph.ExitPosition));
            return targets.Count == 0 ? Array.Empty<GuidanceTarget>() : Array.AsReadOnly(targets.ToArray());
        }

        public void SetActiveEffects(IReadOnlyActiveEffects effects) => _state.ActiveEffects = effects;
        public IReadOnlyList<FloorTrapSpawn> ReceiveBlinderTrapPolicy(BlinderTrapPolicyFact fact)
        {
            var added = new List<FloorTrapSpawn>();
            if (!_state.IsReady || _state.Ended || !fact.Hunter.IsValid || fact.Tick < 0 ||
                !Finite(fact.Duration) || fact.Duration <= 0f ||
                _state.BlinderPolicies.TryGetValue(fact.Hunter, out var previous) && previous.Tick > fact.Tick) return added;
            _state.BlinderPolicies[fact.Hunter] = fact;
            int desired = _state.BlinderPolicies.Values.Max(p => Math.Max(0, Math.Min(3, p.MoreTrapsStacks))) * _config.ExtraBlinderTraps;
            if (_state.Round < _config.TrapStartRound) return added;
            // Existing traps persist until sprung/closed. Duplicate type instances never multiply placement.
            foreach (var anchor in _state.SpawnedAnchors.OrderBy(a => a.Id).ToArray())
            {
                if (_state.AddedBlinderTraps >= desired) break;
                if (_state.SelectedAnchors.Any(a => a.Id == anchor.Id) || _state.GoldenAnchors.Any(a => a.Id == anchor.Id) ||
                    _state.CollectedCakes.Contains(anchor.Id) || _state.MutableRoomPhases[anchor.RoomId] != RoomPhase.Open) continue;
                var trap = new FloorTrapSpawn(anchor, FloorTrapKind.Blind);
                _state.MutableTraps.Add(trap); added.Add(trap); _state.AddedBlinderTraps++;
                _state.SpawnedAnchors.Remove(anchor); _state.RemainingRewards.Remove(anchor.Id);
                _state.MutableActiveAnchors.RemoveAll(a => a.Id == anchor.Id); _state.RequiredCakeCount--; _state.TotalCakes--;
            }
            UpdateExitLock();
            return added.AsReadOnly();
        }
        public bool TryBlinderHit(FloorTrapSprungFact trap, out BlinderHitFact hit)
        {
            hit = default;
            if (trap.Kind != FloorTrapKind.Blind || !_state.SprungTraps.Contains(trap.TrapId) || _state.BlinderPolicies.Count == 0) return false;
            var policy = _state.BlinderPolicies.Values.OrderByDescending(p => p.Duration).ThenBy(p => p.Hunter.Value).First();
            hit = new BlinderHitFact(policy.Hunter, trap.PlayerId, trap.Tick, trap.TrapId,
                policy.Duration, _state.BlinderPolicies.Values.Any(p => p.MuffledDark), trap: true);
            return true;
        }

        public bool TryWhiteGuidance(bool fallback, out GuidanceTarget target)
        {
            target = default;
            if (!_state.IsReady || _state.Ended || _state.CakeHooks.BlindFaith || _state.CueAnchorId < 0) return false;
            int id = _state.CueAnchorId;
            var position = _state.Graph.ExitPosition;
            if (id != 0)
            {
                bool found = false;
                foreach (var anchor in _state.MutableActiveAnchors)
                    if (anchor.Id == id) { position = anchor.Position; found = true; break; }
                if (!found) return false;
            }
            target = new GuidanceTarget(GuidanceKind.WhiteArrow, _state.Display.CueDirection, position, id, isFallback: fallback);
            return true;
        }

        public bool TryGoldenTarget(IReadOnlyList<FloorPathCandidate> paths, out LevelAnchor anchor)
        {
            anchor = default;
            if (!_state.CakeHooks.GoldenSense || !TrySelectPath(paths, _state.GoldenCueAnchorId, true, out var selected))
            { _state.GoldenCueAnchorId = -1; return false; }
            _state.GoldenCueAnchorId = selected.AnchorId;
            anchor = _state.GoldenAnchors.First(a => a.Id == selected.AnchorId);
            return true;
        }

        // Compatibility query cannot claim reachability without paths. It only returns
        // a still-eligible identity already established by the path-ranked overload.
        public bool TryGoldenTarget(Vector3 from, out LevelAnchor anchor)
        {
            anchor = default;
            if (!_state.IsReady || _state.Ended || !_state.CakeHooks.GoldenSense || _state.CakeHooks.BlindFaith ||
                !_state.RemainingRewards.TryGetValue(_state.GoldenCueAnchorId, out var kind) || kind != PickupKind.GoldenCake) return false;
            anchor = _state.GoldenAnchors.First(a => a.Id == _state.GoldenCueAnchorId);
            return true;
        }

        private bool TrySelectPath(IReadOnlyList<FloorPathCandidate> paths, int retainedId, bool gold, out FloorPathCandidate selected)
        {
            selected = default;
            if (!_state.IsReady || _state.Ended || _state.CakeHooks.BlindFaith || paths == null) return false;
            bool found = false, retained = false;
            FloorPathCandidate current = default;
            foreach (var path in paths)
            {
                bool eligible = gold ? _state.RemainingRewards.TryGetValue(path.AnchorId, out var kind) && kind == PickupKind.GoldenCake :
                    _state.MutableActiveAnchors.Count == 0 ? _state.ExitState == ExitState.Open && path.AnchorId == 0 :
                    HasAnchor(_state.MutableActiveAnchors, path.AnchorId);
                if (!eligible || !Finite(path.Length) || path.Length < 0f || !Finite(path.Direction.x) ||
                    !Finite(path.Direction.y) || !Finite(path.Direction.z) || path.Direction.sqrMagnitude <= 0f) continue;
                if (path.AnchorId == retainedId) { retained = true; current = path; }
                if (found && (path.Length > selected.Length || path.Length == selected.Length && path.AnchorId >= selected.AnchorId)) continue;
                selected = path; found = true;
            }
            if (retained && current.Length - selected.Length <= Math.Max(_config.CueSwitchMargin, current.Length * _config.CueSwitchFraction))
                selected = current;
            return found;
        }

        public bool RefreshCueForRoomChange()
        {
            if (!_state.IsReady || _state.Ended) return false;
            var player = CuePlayer();
            if (player == null) return false;
            int roomId = 0;
            foreach (var room in _state.Graph.Rooms)
                if (room.Cells.Any(cell => FloorBoundsUtility.Contains(cell, player.Position)) &&
                    (roomId == 0 || room.Id < roomId)) roomId = room.Id;
            // A doorway seam need not belong to either room. Do not replace identity
            // or repeatedly refresh while traversing that small unowned interval.
            if (roomId == 0 || roomId == _state.CueRoomId && player.Id == _state.CuePlayerId) return false;
            _state.CueRoomId = roomId; _state.CuePlayerId = player.Id;
            _state.CueElapsed = _config.DirectionCueInterval;
            return true;
        }

        private void UpdateExitLock()
        {
            if (_state.MutableActiveAnchors.Count != 0 || _state.ExitState == ExitState.Open) return;
            BuildCollapseSchedule();
            _state.CollapseStarted = true;
            _state.CollapseElapsed = 0d;
            _state.CueElapsed = _config.DirectionCueInterval;
            _state.CueAnchorId = -1;
            _state.ExitState = ExitState.Open;
        }
        private static T[] Snapshot<T>(List<T> values) => values.Count == 0 ? Array.Empty<T>() : values.ToArray();
        private static bool HasAnchor(List<LevelAnchor> anchors, int id)
        {
            foreach (var anchor in anchors) if (anchor.Id == id) return true;
            return false;
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
