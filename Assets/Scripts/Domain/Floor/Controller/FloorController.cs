// ============================================================================
// FloorController.cs
// ============================================================================
// PURPOSE:
//   Selects weighted anchors, counts contacts once and schedules deterministic room collapse.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   A positive requiredCakeCount override lets generated maps use every anchor;
//   the default preserves the authored-floor configuration without editing assets.
//   Reads injected Player views only. Shared graph utility supplies directed distances to the exit; unreachable rooms close first and cannot contain required cakes.
//   No persistent singleton or competing simulation tick is created.
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

        public void Initialize(LevelGraph graph, IReadOnlyList<IReadOnlyPlayerState> players, int requiredCakeCount = -1)
        {
            if (graph == null || players == null) throw new ArgumentNullException();
            RequirePositive(_config.CollapseInterval, nameof(_config.CollapseInterval));
            RequirePositive(_config.TelegraphDuration, nameof(_config.TelegraphDuration));
            RequirePositive(_config.DirectionCueInterval, nameof(_config.DirectionCueInterval));
            int required = requiredCakeCount == -1 ? _config.RequiredCakeCount : requiredCakeCount;
            if (required < 1) throw new ArgumentException("At least one cake is required.");
            foreach (CakeAnchorType type in Enum.GetValues(typeof(CakeAnchorType)))
                if (!Finite(Weight(type)) || Weight(type) < 0f) throw new ArgumentException("Anchor weights must be finite and nonnegative.");
            Reset();
            _state.Graph = graph;
            _state.Players = players.ToArray();
            var distances = LevelGraphUtility.DistancesTo(graph, graph.ExitRoomId, TraversalAccess.Player);
            var reachable = new HashSet<int>();
            foreach (var player in players.Where(value => value != null && value.Id.IsValid && value.IsAlive &&
                Finite(value.Position.x) && Finite(value.Position.y) && Finite(value.Position.z)))
            foreach (var room in graph.Rooms)
            {
                var delta = player.Position - room.Center;
                var half = room.Size * 0.5f;
                if (Math.Abs(delta.x) > half.x || Math.Abs(delta.y) > half.y || Math.Abs(delta.z) > half.z) continue;
                foreach (var distance in LevelGraphUtility.TopologicalDistancesFrom(graph, room.Id, TraversalAccess.Player))
                    if (distance.Value >= 0) reachable.Add(distance.Key);
            }
            if (reachable.Count == 0) throw new InvalidOperationException("Floor requires a living player inside an authored room.");
            var candidates = graph.Anchors.Where(anchor => reachable.Contains(anchor.RoomId) && distances[anchor.RoomId] >= 0 && Weight(anchor.Type) > 0f)
                .OrderBy(anchor => anchor.Id).ToList();
            if (candidates.Count < required)
                throw new InvalidOperationException("Floor requires " + required + " weighted reachable anchors; graph has " + candidates.Count + ".");
            while (_state.SelectedAnchors.Count < required)
            {
                double total = candidates.Sum(anchor => (double)Weight(anchor.Type));
                double sample = _random.NextDouble() * total;
                int selected = candidates.Count - 1;
                for (int index = 0; index < candidates.Count; index++)
                {
                    sample -= Weight(candidates[index].Type);
                    if (sample < 0d) { selected = index; break; }
                }
                _state.SelectedAnchors.Add(candidates[selected]);
                candidates.RemoveAt(selected);
            }
            _state.MutableActiveAnchors.AddRange(_state.SelectedAnchors);
            var order = graph.Rooms.OrderByDescending(room => distances[room.Id] < 0 ? int.MaxValue : distances[room.Id])
                .ThenBy(room => room.Id).ToArray();
            for (int index = 0; index < order.Length; index++)
            {
                _state.MutableRoomPhases.Add(order[index].Id, RoomPhase.Open);
                double start = index * (double)_config.CollapseInterval;
                _state.Schedule.Add(new FloorScheduledTransition(order[index].Id, RoomPhase.Telegraph, start));
                _state.Schedule.Add(new FloorScheduledTransition(order[index].Id, RoomPhase.Closed, start + _config.TelegraphDuration));
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

        public bool Collect(EntityId playerId, int anchorId, PickupKind kind, long tick, out PickupCollectedFact fact)
        {
            fact = default;
            if (!_state.IsReady || _state.Ended || !LivingPlayer(playerId)) return false;
            var anchor = _state.SelectedAnchors.FirstOrDefault(value => value.Id == anchorId);
            if (anchor.Id == 0 || _state.MutableRoomPhases[anchor.RoomId] == RoomPhase.Closed) return false;
            if (kind == PickupKind.Cake)
            {
                if (_state.ExitState != ExitState.Locked || !_state.CollectedCakes.Add(anchorId)) return false;
                _state.CakeCount++;
                _state.MutableActiveAnchors.RemoveAll(value => value.Id == anchorId);
                if (_state.CakeCount == _state.RequiredCakeCount)
                {
                    _state.ExitState = ExitState.Open;
                    _state.CollapseElapsed = 0d;
                    _state.CueElapsed = _config.DirectionCueInterval;
                }
            }
            else if (kind == PickupKind.GoldenCake)
            {
                if (_state.ExitState != ExitState.Open || !_state.CollectedGoldenCakes.Add(anchorId)) return false;
                _state.GoldenCakeCount++;
            }
            else return false;
            fact = new PickupCollectedFact(playerId, anchorId, kind, _state.CakeCount, _state.GoldenCakeCount, tick);
            return true;
        }

        public IReadOnlyList<RoomPhaseChangedFact> Tick(float dt, long tick)
        {
            if (!Finite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            var facts = new List<RoomPhaseChangedFact>();
            if (!_state.IsReady || _state.Ended) return facts;
            _state.Tick = tick;
            _state.CueElapsed += dt;
            if (_state.ExitState != ExitState.Open) return facts;
            _state.CollapseElapsed += dt;
            while (_state.NextTransition < _state.Schedule.Count && _state.Schedule[_state.NextTransition].At <= _state.CollapseElapsed)
            {
                var transition = _state.Schedule[_state.NextTransition++];
                _state.MutableRoomPhases[transition.RoomId] = transition.Phase;
                facts.Add(new RoomPhaseChangedFact(transition.RoomId, transition.Phase, tick));
            }
            return facts;
        }

        public bool ConsumeCueDue()
        {
            if (!_state.IsReady || _state.Ended || _state.CueElapsed < _config.DirectionCueInterval) return false;
            _state.CueElapsed %= _config.DirectionCueInterval;
            return true;
        }

        public FloorDisplaySnapshot SelectCue(IReadOnlyList<FloorPathCandidate> paths)
        {
            bool available = false;
            var direction = Vector3.zero;
            float nearest = float.PositiveInfinity;
            int nearestId = int.MaxValue;
            if (_state.IsReady && !_state.Ended && paths != null)
            foreach (var path in paths)
            {
                bool wanted = _state.ExitState == ExitState.Open ? path.AnchorId == 0 : _state.MutableActiveAnchors.Any(anchor => anchor.Id == path.AnchorId);
                if (!wanted || !Finite(path.Length) || path.Length < 0f || !Finite(path.Direction.x) || !Finite(path.Direction.y) || !Finite(path.Direction.z)) continue;
                if (path.Length > nearest || path.Length == nearest && path.AnchorId >= nearestId) continue;
                nearest = path.Length; nearestId = path.AnchorId; available = true; direction = path.Direction;
            }
            _state.Display = new FloorDisplaySnapshot(_state.CakeCount, _state.RequiredCakeCount,
                _state.GoldenCakeCount, _state.ExitState, available, direction);
            return _state.Display;
        }

        public FloorDisplaySnapshot Snapshot() => new FloorDisplaySnapshot(_state.CakeCount, _state.RequiredCakeCount,
            _state.GoldenCakeCount, _state.ExitState, _state.Display.HasCue && !_state.Ended, _state.Display.CueDirection);

        public bool ContactExit(EntityId id, long tick, out ExitReachedFact fact)
        {
            fact = default;
            if (!_state.IsReady || _state.Ended || _state.ExitState != ExitState.Open || !LivingPlayer(id) ||
                _state.MutableRoomPhases[_state.Graph.ExitRoomId] == RoomPhase.Closed) return false;
            _state.Ended = true;
            fact = new ExitReachedFact(id, tick);
            return true;
        }

        public bool ContactLethalRoom(EntityId id, int roomId, long tick, out FloorLethalContactFact fact)
        {
            fact = default;
            if (!_state.IsReady || _state.Ended || !LivingPlayer(id) || !_state.MutableRoomPhases.TryGetValue(roomId, out var phase) || phase != RoomPhase.Closed) return false;
            _state.Ended = true;
            fact = new FloorLethalContactFact(id, roomId, tick);
            return true;
        }

        public IReadOnlyPlayerState CuePlayer() => _state.Players.Where(player => player != null && player.IsAlive).OrderBy(player => player.Id.Value).FirstOrDefault();

        public void Reset()
        {
            _state.IsReady = false; _state.Ended = false; _state.Tick = 0;
            _state.CakeCount = 0; _state.GoldenCakeCount = 0; _state.RequiredCakeCount = 0;
            _state.ExitState = ExitState.Locked; _state.CollapseElapsed = 0d; _state.CueElapsed = 0d; _state.NextTransition = 0;
            _state.SelectedAnchors.Clear(); _state.MutableActiveAnchors.Clear(); _state.MutableRoomPhases.Clear();
            _state.CollectedCakes.Clear(); _state.CollectedGoldenCakes.Clear(); _state.Schedule.Clear();
            _state.Players = Array.Empty<IReadOnlyPlayerState>(); _state.Graph = null; _state.Display = default;
        }

        private bool LivingPlayer(EntityId id) => id.IsValid && _state.Players.Any(player => player != null && player.Id == id && player.IsAlive);
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
