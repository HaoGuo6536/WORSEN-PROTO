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
//   - Snapshot the real round and collapse hooks; publish white guidance before Golden Sense.
//   - Publish H1 guidance, trap contacts and shared trap noise without routing foreign effects.
//   - Spawn gold at collapse start even when Greedy Door delays the physical exit.
//   - Support staged cracks, tearing, mist advance and escapable hand contacts.
//   - Publish escape facts with a bail flag; retain the legacy event for normal exits only.
//   - Resolve door identities, cancel departed holds before timing, and present bails without rewards.
//   - Publish cake loss, hand noise and rubber-band acceleration facts for upward routing.
//   - Publish every accepted pickup's noise and continuous visual exit progress.
//   - Keep rules, passive state and engine operations in their owning roles.
//   - Forward explicit pocket activation and read Low Profile protection without mutable casts.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Generated required-count overrides apply only when room density is disabled.
//   Scene-owned. Level and Player views are injected before ticking; Session is the sole tick owner. Floor never mutates Player health: hand facts let Session apply ordinary damage; death is confirmed only after a lethal hand hit.
//   No persistent singleton or competing simulation tick is created.
//   Door integration must report locked contact and LeaveExit when its last player
//   collider leaves. OnEscapeResolved carries (exit fact, bailed); consumers must
//   use it instead of OnExitReached to preserve the penalty through run resolution.
//   OnBoundaryContact carries player, room, outward acceleration (m/s squared),
//   boundary point and tick. Player's motion owner must enforce the boundary; Floor
//   never writes a foreign Transform or Rigidbody. Hit throw travels with OnCollapseHand.
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
        public IReadOnlyFloorCollapseState ReadOnlyState => _state;
        public event Action<PickupCollectedFact> OnPickupCollected;
        public event Action<RoomPhaseChangedFact> OnRoomPhaseChanged;
        public event Action<ExitReachedFact> OnExitReached;
        public event Action<ExitReachedFact, bool> OnEscapeResolved;
        public event Action<FloorLethalContactFact> OnLethalContact;
        public event Action<FloorDisplaySnapshot> OnDisplayChanged;
        public event Action<long> OnExitOpened;
        public event Action<CollapseHandFact> OnCollapseHand;
        public event Action<RoomDestructionSample> OnRoomDestruction;
        public event Action<int> OnOptionalRoomCracked;
        public event Action<NoiseEvent> OnHandNoise;
        public event Action<NoiseEvent> OnPickupNoise;
        public event Action<FloorTrapSprungFact> OnTrapSprung;
        public event Action<NoiseEvent> OnTrapNoise;
        public event Action<IReadOnlyList<GuidanceTarget>> OnGuidanceChanged;
        public event Action<int, int, PickupKind, long> OnCakeLost;
        public event Action<EntityId, int, Vector3, Vector3, long> OnBoundaryContact;

        private void Awake() { if (_driver == null) _driver = GetComponent<FloorDriver>(); }
        public void Initialize(FloorConfig config, IReadOnlyLevelState level, IReadOnlyList<IReadOnlyPlayerState> players, System.Random random,
            int requiredCakeCount = -1, bool fasterCollapse = false, bool shuffledCollapse = false, int round = 1, FloorCakeHooks cakeHooks = default, bool waxHeart = false)
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
                _controller.Initialize(level.Graph, players, requiredCakeCount, fasterCollapse, shuffledCollapse, round, cakeHooks, waxHeart);
                _hands = new FloorHandController(_state.Hands, _config);
                _driver.Initialize(level.Graph, _state.SpawnedAnchors, Resolve, _config.HandEscapeDistance, _state.Traps);
                if (isActiveAndEnabled) OnEnable();
                RefreshCue();
            }
            catch { Teardown(); throw; }
        }

        public void Tick(float dt, long tick)
        {
            if (_controller == null) return;
            var owner = _controller;
            _driver.RefreshExitContacts();
            if (!ReferenceEquals(owner, _controller)) return;
            if (owner.TickExitHold(dt, tick, out var bail))
            {
                _driver.PresentBail();
                var display = owner.Snapshot(_driver.OpeningProgress(true));
                OnEscapeResolved?.Invoke(bail, true);
                if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display);
                return;
            }
            if (_state.Ended) return;
            var before = _state.ExitState;
            PublishRoomTransitions(_controller.Tick(dt, tick));
            if (!ReferenceEquals(owner, _controller)) return;
            if (before != _state.ExitState)
            {
                _driver.OpenExit(Array.Empty<LevelAnchor>());
                OnExitOpened?.Invoke(tick);
                if (!ReferenceEquals(owner, _controller)) return;
                RefreshCue();
                if (!ReferenceEquals(owner, _controller)) return;
            }
            foreach (var trap in owner.TickTraps(dt))
            {
                if (!_driver.PickupAvailable(trap.Anchor.Id)) continue;
                _driver.PlayTrapTick(trap.Anchor.Id, _config.TrapTickLoudness);
                OnTrapNoise?.Invoke(new NoiseEvent(EntityId.None, trap.Anchor.Position, _config.TrapTickLoudness, tick, NoiseSourceKind.Trap));
                if (!ReferenceEquals(owner, _controller)) return;
            }
            _driver.TickCakeVisuals((float)_state.Elapsed);
            TickDestruction(dt);
            if (!ReferenceEquals(owner, _controller)) return;
            float previousProgress = _driver.OpeningProgress(_state.ExitState == ExitState.Open);
            _driver.TickWarnings((float)_state.CollapseElapsed);
            if (_controller.ConsumeCueDue()) RefreshCue();
            else if (_driver.OpeningProgress(_state.ExitState == ExitState.Open) != previousProgress)
                OnDisplayChanged?.Invoke(Snapshot());
        }

        public void Collect(EntityId playerId, int anchorId, PickupKind kind)
        {
            if (_controller == null || !_driver.PickupAvailable(anchorId)) return;
            var owner = _controller;
            var before = _state.ExitState;
            bool collapsing = _state.CollapseStarted;
            if (!_controller.Collect(playerId, anchorId, kind, _state.Tick, out var fact, out var noise)) return;
            _driver.RemovePickup(anchorId, kind);
            if (!collapsing && _state.CollapseStarted) _driver.SpawnGoldenCakes(_state.GoldenAnchors);
            OnPickupNoise?.Invoke(noise);
            if (!ReferenceEquals(owner, _controller)) return;
            OnPickupCollected?.Invoke(fact);
            if (!ReferenceEquals(owner, _controller)) return;
            if (before != _state.ExitState)
            {
                _driver.OpenExit(Array.Empty<LevelAnchor>());
                OnExitOpened?.Invoke(_state.Tick);
                if (!ReferenceEquals(owner, _controller)) return;
            }
            if (!collapsing && _state.CollapseStarted)
            {
                PublishRoomTransitions(owner.Tick(0f, _state.Tick));
                if (!ReferenceEquals(owner, _controller)) return;
            }
            RefreshCue();
        }

        public void SpringTrap(EntityId playerId, int trapId)
        {
            var owner = _controller;
            if (owner == null || !_driver.PickupAvailable(trapId) ||
                !owner.SpringTrap(playerId, trapId, _state.Tick, out var fact, out var noise)) return;
            _driver.RemoveTrap(trapId);
            OnTrapSprung?.Invoke(fact);
            if (!ReferenceEquals(owner, _controller)) return;
            if (fact.Kind == FloorTrapKind.Announce) OnTrapNoise?.Invoke(noise);
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
            var display = Snapshot();
            _driver.ApplyHandFact(consumed); OnCollapseHand?.Invoke(consumed);
            if (!ReferenceEquals(owner, _controller)) return;
            OnLethalContact?.Invoke(fact);
            if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display);
        }
        public bool CancelCollapseGrab(EntityId playerId)
        {
            if (_hands == null || !_hands.Cancel(playerId, _state.Tick, out var fact)) return false;
            _hands.CopyRoomPhases(_state.MutableRoomHandPhases);
            _driver.ApplyHandFact(fact); OnCollapseHand?.Invoke(fact); return true;
        }
        public bool ArmWaxWard(EntityId playerId) => _hands != null && _hands.ArmWaxWard(playerId);
        public bool ActivatePocket(int roomId) => _controller != null && _controller.ActivatePocket(roomId);
        public bool TelegraphOptionalRoom(int roomId)
        {
            if (_controller == null || !_controller.TelegraphOptionalRoom(roomId)) return false;
            _driver.PreviewCracks(roomId); OnOptionalRoomCracked?.Invoke(roomId); return true;
        }
        public void ContactExit(EntityId playerId)
        {
            var owner = _controller;
            if (owner != null && owner.ContactExit(playerId, _state.Tick, out var fact))
            {
                var display = Snapshot();
                OnEscapeResolved?.Invoke(fact, false);
                if (!ReferenceEquals(owner, _controller)) return;
                OnExitReached?.Invoke(fact);
                if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display);
            }
        }
        public void LeaveExit(EntityId playerId) => _controller?.LeaveExit(playerId);
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
            _driver.TrapContact -= HandleTrap; _driver.TrapContact += HandleTrap;

            _driver.ExitContact -= HandleExit; _driver.ExitContact += HandleExit;
            _driver.ExitDeparted -= LeaveExit; _driver.ExitDeparted += LeaveExit;
        }
        private void OnDisable()
        {
            _controller?.CancelExitHolds();
            if (_driver == null) return;
            _driver.PickupContact -= HandlePickup;
            _driver.TrapContact -= HandleTrap;

            _driver.ExitContact -= HandleExit;
            _driver.ExitDeparted -= LeaveExit;
        }
        private void OnDestroy() => Teardown();
        private void HandlePickup(Collider other, int anchor, PickupKind kind) => Collect(Resolve(other), anchor, kind);
        private void HandleTrap(Collider other, int anchor) => SpringTrap(Resolve(other), anchor);

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
            foreach (var loss in owner.DrainCakeLosses())
            {
                _driver.RemovePickup(loss.AnchorId, loss.Kind);
                OnCakeLost?.Invoke(loss.AnchorId, loss.RoomId, loss.Kind, loss.Tick);
                if (!ReferenceEquals(owner, _controller)) return;
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
            _driver.RefreshHandContacts();
            foreach (var player in _state.Players)
            {
                if (player == null) continue;
                _hands.Target(player.Id, out int roomId, out int handId);
                var effects = player as IReadOnlyPlayerEffectState;
                var probe = effects != null && effects.IsUngrabbable ? default :
                    _driver.QueryHand(player.Position, roomId, handId, player.Id);
                bool changed = _hands.Tick(player.Id, player.IsAlive, probe, dt, _state.Tick, out var fact, effects);
                _hands.CopyRoomPhases(_state.MutableRoomHandPhases);
                if (changed)
                {
                    // Emit once on entry, including grabs subsequently broken by Wax Ward.
                    if (fact.Kind == CollapseHandEventKind.Warning) OnHandNoise?.Invoke(_hands.GrabNoise(fact));
                    if (!ReferenceEquals(owner, _controller)) return;
                    _driver.ApplyHandFact(fact); OnCollapseHand?.Invoke(fact);
                }
                if (!ReferenceEquals(owner, _controller)) return;
                if (_state.Ended) return;
                var boundary = _driver.QueryHand(player.Position, playerId: player.Id, closedOnly: true);
                var acceleration = _hands.BoundaryAcceleration(boundary);
                if (player.IsAlive && acceleration.sqrMagnitude > 0f)
                    OnBoundaryContact?.Invoke(player.Id, boundary.RoomId, acceleration, boundary.Position, _state.Tick);
                if (!ReferenceEquals(owner, _controller)) return;
            }
        }

        private FloorDisplaySnapshot Snapshot() => _controller.Snapshot(_driver.OpeningProgress(_state.ExitState == ExitState.Open));

        private void RefreshCue()
        {
            var owner = _controller;
            var paths = new List<FloorPathCandidate>();
            var player = _controller.CuePlayer();
            if (player != null)
            {
                if (_state.CollapseStarted) paths.Add(_driver.QueryPath(0, player.Position, _state.Graph.ExitPosition));
                else foreach (var anchor in _state.ActiveCakeAnchors) paths.Add(_driver.QueryPath(anchor.Id, player.Position, anchor.Position));
            }
            var display = owner.SelectCue(paths, _driver.OpeningProgress(_state.ExitState == ExitState.Open));
            var targets = new List<GuidanceTarget>();
            bool fallback = _driver.IsDirectionFallback(_state.CueAnchorId) || _driver.IsDirectionHeld(_state.CueAnchorId);
            // Stable channel order is a public contract: objective first, optional gold second.
            if (owner.TryWhiteGuidance(fallback, out var white)) targets.Add(white);
            if (player != null && owner.TryGoldenTarget(player.Position, out var golden))
            {
                var path = _driver.QueryPath(golden.Id, player.Position, golden.Position);
                if (!float.IsNaN(path.Length) && !float.IsInfinity(path.Length))
                    targets.Add(new GuidanceTarget(GuidanceKind.GoldenSense, path.Direction, golden.Position, golden.Id,
                        isFallback: _driver.IsDirectionFallback(golden.Id) || _driver.IsDirectionHeld(golden.Id)));
            }
            // A read-only snapshot, including empty, replaces both channels atomically.
            OnGuidanceChanged?.Invoke(targets.AsReadOnly());
            if (ReferenceEquals(owner, _controller)) OnDisplayChanged?.Invoke(display);
        }
    }
}
