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
//   - Support staged cracks, tearing, mist advance and escapable hand contacts.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Generated maps may supply a required-count override; configuration assets remain unchanged.
//   Scene-owned. Level and Player views are injected before ticking; Session is the sole tick owner. Floor never mutates Player health: hand facts let Session apply ordinary damage; death is confirmed only after a lethal hand hit.
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
        private FloorHandController _hands;
        public IReadOnlyFloorState ReadOnlyState => _state;
        public event Action<PickupCollectedFact> OnPickupCollected;
        public event Action<RoomPhaseChangedFact> OnRoomPhaseChanged;
        public event Action<ExitReachedFact> OnExitReached;
        public event Action<FloorLethalContactFact> OnLethalContact;
        public event Action<FloorDisplaySnapshot> OnDisplayChanged;
        public event Action<long> OnExitOpened;
        public event Action<CollapseHandFact> OnCollapseHand;
        public event Action<RoomDestructionSample> OnRoomDestruction;
        public event Action<int> OnOptionalRoomCracked;

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
                _hands = new FloorHandController(new FloorHandBehaviorState(), _config);
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
            TickDestruction(dt);
            if (!ReferenceEquals(owner, _controller)) return;
            _driver.TickWarnings((float)_state.CollapseElapsed);
            if (_controller.ConsumeCueDue()) RefreshCue();
        }

        public void Collect(EntityId playerId, int anchorId, PickupKind kind)
        {
            if (_controller == null || !_driver.PickupAvailable(anchorId)) return;
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

        // Compatibility entry: contact without a confirmed lethal hand hit is rejected.
        public void ContactLethalRoom(EntityId playerId, int roomId) => ConfirmCollapseDeath(playerId, roomId);
        public void ConfirmCollapseDeath(EntityId playerId, int roomId)
        {
            var owner = _controller;
            if (owner == null || _hands == null) return;
            IReadOnlyPlayerState player = null;
            foreach (var candidate in _state.Players) if (candidate != null && candidate.Id == playerId) { player = candidate; break; }
            if (player == null || !_hands.ConfirmDeath(playerId, roomId, player.IsAlive, _state.Tick, out var consumed)) return;
            if (!owner.ContactLethalRoom(playerId, roomId, _state.Tick, out var fact)) return;
            var display = owner.Snapshot();
            _driver.ApplyHandFact(consumed); OnCollapseHand?.Invoke(consumed);
            if (!ReferenceEquals(owner, _controller)) return;
            OnLethalContact?.Invoke(fact);
            if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display);
        }
        public bool CancelCollapseGrab(EntityId playerId)
        {
            if (_hands == null || !_hands.Cancel(playerId, _state.Tick, out var fact)) return false;
            _driver.ApplyHandFact(fact); OnCollapseHand?.Invoke(fact); return true;
        }
        public bool TelegraphOptionalRoom(int roomId)
        {
            if (_controller == null || !_controller.TelegraphOptionalRoom(roomId)) return false;
            _driver.PreviewCracks(roomId); OnOptionalRoomCracked?.Invoke(roomId); return true;
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
            _hands?.Reset(); _hands = null;
            _controller?.Reset(); _controller = null; _state = null;
        }

        private void OnEnable()
        {
            if (_driver == null) return;
            _driver.PickupContact -= HandlePickup; _driver.PickupContact += HandlePickup;

            _driver.ExitContact -= HandleExit; _driver.ExitContact += HandleExit;
        }
        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.PickupContact -= HandlePickup;

            _driver.ExitContact -= HandleExit;
        }
        private void OnDestroy() => Teardown();
        private void HandlePickup(Collider other, int anchor, PickupKind kind) => Collect(Resolve(other), anchor, kind);

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
        private void TickDestruction(float dt)
        {
            var owner = _controller;
            foreach (var room in _state.Graph.Rooms)
            {
                var sample = owner.Destruction(room.Id);
                _driver.ApplyDestruction(sample, (float)_state.CollapseElapsed);
                OnRoomDestruction?.Invoke(sample);
                if (!ReferenceEquals(owner, _controller)) return;
            }
            foreach (var player in _state.Players)
            {
                if (player == null) continue;
                _hands.Target(player.Id, out int roomId, out int handId);
                var probe = _driver.QueryHand(player.Position, roomId, handId);
                if (_hands.Tick(player.Id, player.IsAlive, probe, dt, _state.Tick, out var fact)) { _driver.ApplyHandFact(fact); OnCollapseHand?.Invoke(fact); }
                if (!ReferenceEquals(owner, _controller)) return;
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
