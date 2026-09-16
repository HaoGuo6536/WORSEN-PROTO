// ============================================================================
// FloorManager.cs
// ============================================================================
// PURPOSE:
//   Sequences Floor rules and its engine boundary, then publishes immutable gameplay facts.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Floor (Service system).
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Generated maps may supply a required-count override; configuration assets remain unchanged.
//   Scene-owned. Level and Player views are injected before ticking; Session is the sole tick owner. Floor never mutates Player health: lethal-contact facts let Session choose the end reason and health update.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(FloorDriver))]
    public sealed class FloorManager : MonoBehaviour
    {
        [SerializeField] private FloorDriver _driver;
        [SerializeField] private FloorConfig _config;
        private FloorBehaviorState _state;
        private FloorController _controller;
        public IReadOnlyFloorState ReadOnlyState => _state;
        public event Action<PickupCollectedFact> OnPickupCollected;
        public event Action<RoomPhaseChangedFact> OnRoomPhaseChanged;
        public event Action<ExitReachedFact> OnExitReached;
        public event Action<FloorLethalContactFact> OnLethalContact;
        public event Action<FloorDisplaySnapshot> OnDisplayChanged;
        public event Action<long> OnExitOpened;

        private void Awake() { if (_driver == null) _driver = GetComponent<FloorDriver>(); }
        public void Initialize(FloorConfig config, IReadOnlyLevelState level, IReadOnlyList<IReadOnlyPlayerState> players, System.Random random, int requiredCakeCount = -1)
        {
            if (level == null || !level.IsReady) throw new InvalidOperationException("Floor requires a ready authored Level.");
            Teardown();
            if (config != null) _config = config;
            if (_config == null) throw new ArgumentNullException(nameof(config));
            if (_driver == null) _driver = GetComponent<FloorDriver>();
            _state = new FloorBehaviorState();
            try
            {
                _controller = new FloorController(_state, _config, random);
                _controller.Initialize(level.Graph, players, requiredCakeCount);
                _driver.Initialize(level.Graph, _state.SelectedAnchors);
                if (isActiveAndEnabled) OnEnable();
                RefreshCue();
            }
            catch { Teardown(); throw; }
        }

        public void Tick(float dt, long tick)
        {
            if (_controller == null) return;
            var owner = _controller;
            PublishRoomTransitions(_controller.Tick(dt, tick));
            if (!ReferenceEquals(owner, _controller)) return;
            _driver.TickWarnings((float)_state.CollapseElapsed);
            if (_controller.ConsumeCueDue()) RefreshCue();
        }

        public void Collect(EntityId playerId, int anchorId, PickupKind kind)
        {
            if (_controller == null) return;
            var owner = _controller;
            var before = _state.ExitState;
            if (!_controller.Collect(playerId, anchorId, kind, _state.Tick, out var fact)) return;
            _driver.RemovePickup(anchorId, kind);
            OnPickupCollected?.Invoke(fact);
            if (!ReferenceEquals(owner, _controller)) return;
            if (before != _state.ExitState)
            {
                _driver.OpenExit(_state.SelectedAnchors);
                OnExitOpened?.Invoke(_state.Tick);
                if (!ReferenceEquals(owner, _controller)) return;
                PublishRoomTransitions(_controller.Tick(0f, _state.Tick));
                if (!ReferenceEquals(owner, _controller)) return;
                RefreshCue();
            }
            else OnDisplayChanged?.Invoke(_controller.Snapshot());
        }

        public void ContactLethalRoom(EntityId playerId, int roomId)
        {
            var owner = _controller;
            if (owner != null && owner.ContactLethalRoom(playerId, roomId, _state.Tick, out var fact))
            { var display = owner.Snapshot(); OnLethalContact?.Invoke(fact); if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display); }
        }
        public void ContactExit(EntityId playerId)
        {
            var owner = _controller;
            if (owner != null && owner.ContactExit(playerId, _state.Tick, out var fact))
            { var display = owner.Snapshot(); OnExitReached?.Invoke(fact); if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display); }
        }
        public void Teardown()
        {
            OnDisable();
            if (_driver != null) _driver.Teardown();
            _controller?.Reset(); _controller = null; _state = null;
        }

        private void OnEnable()
        {
            if (_driver == null) return;
            _driver.PickupContact -= HandlePickup; _driver.PickupContact += HandlePickup;
            _driver.LethalContact -= HandleLethal; _driver.LethalContact += HandleLethal;
            _driver.ExitContact -= HandleExit; _driver.ExitContact += HandleExit;
        }
        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.PickupContact -= HandlePickup;
            _driver.LethalContact -= HandleLethal;
            _driver.ExitContact -= HandleExit;
        }
        private void OnDestroy() => Teardown();
        private void HandlePickup(Collider other, int anchor, PickupKind kind) => Collect(Resolve(other), anchor, kind);
        private void HandleLethal(Collider other, int room) => ContactLethalRoom(Resolve(other), room);
        private void HandleExit(Collider other) => ContactExit(Resolve(other));
        private static EntityId Resolve(Collider other) => other != null ? other.GetComponentInParent<IEntityHandle>()?.Id ?? EntityId.None : EntityId.None;

        private void PublishRoomTransitions(IReadOnlyList<RoomPhaseChangedFact> facts)
        {
            var owner = _controller;
            foreach (var fact in facts)
            {
                if (!ReferenceEquals(owner, _controller)) return;
                OnRoomPhaseChanged?.Invoke(fact);
                if (!ReferenceEquals(owner, _controller)) return;
                _driver.ApplyRoomPhase(fact.RoomId, fact.Phase);
            }
        }
        private void RefreshCue()
        {
            var paths = new List<FloorPathCandidate>();
            var player = _controller.CuePlayer();
            if (player != null)
            {
                if (_state.ExitState == ExitState.Open) paths.Add(_driver.QueryPath(0, player.Position, _state.Graph.ExitPosition));
                else foreach (var anchor in _state.ActiveCakeAnchors) paths.Add(_driver.QueryPath(anchor.Id, player.Position, anchor.Position));
            }
            OnDisplayChanged?.Invoke(_controller.SelectCue(paths));
        }
    }
}
