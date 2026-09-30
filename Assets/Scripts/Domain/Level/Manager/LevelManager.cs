// ============================================================================
// LevelManager.cs
// ============================================================================
// PURPOSE:
//   Initializes the scene-owned Level service before scene readiness is published.
//   It connects marker lifecycle events, the Registry and pure graph assembly,
//   providing downstream consumers a typed read-only state rather than scene searches.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Level (Service system).
// KEY RESPONSIBILITIES:
//   - Forward effect-owned jam admission so rejected opens never publish transient state.
//   - Publish door-open provenance only after a real closed-to-open change; legacy opens are anonymous.
//   - Reconcile enabled markers, sequence rebuilds and publish readiness facts.
//   - Revalidate incomplete removal snapshots after lifecycle callbacks settle.
//   - Accept a generated graph through explicit initialization, without markers.
//   - Own floor interactables and acoustic portal state; publish committed Core facts.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Scene-owned Service system. SceneRoot or Session calls Initialize explicitly;
//   this service never subscribes to an upper-layer SceneReady event. Initialization
//   failures propagate so the boot owner cannot announce a usable partial graph.
//   Marker removal invalidates readiness immediately. An incomplete removal is
//   revalidated on the next active LateUpdate before reporting an authoring error,
//   because scene unload may disable children before this Manager. This callback
//   flushes a lifecycle diagnostic only; it does not tick gameplay or read time.
//   Session must route InteractableChanged to Procedural geometry and Environment light
//   presentation. Neither producer nor presentation owns this mutable registry.
//   No current runtime caller opens these doors for player interaction: LevelDriver
//   only registers markers; Procedural applies state. Future interaction routing must
//   call OpenDoor(id, openedByPlayer: true), not infer provenance from room crossings.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelDriver), typeof(LevelMarkerRegistry))]
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelDriver _driver;
        [SerializeField] private LevelMarkerRegistry _registry;
        private readonly LevelBehaviorState _state = new LevelBehaviorState();
        private LevelController _controller;
        private readonly LevelInteractableBehaviorState _interactables = new LevelInteractableBehaviorState();
        private LevelInteractableController _interactableController;
        private bool _initialized;
        private bool _subscribed;
        private bool _removalValidationPending;

        public IReadOnlyLevelState ReadOnlyState => _state;
        public event Action<bool> ReadinessChanged;
        public IReadOnlyInteractableSet Interactables => _interactables;
        public IReadOnlyDictionary<int, bool> ClosedDoors => _interactables.ClosedDoors;
        public event Action<InteractableState, InteractableState> InteractableChanged;
        public event Action<InteractableState, bool> DoorOpened;

        public IReadOnlyLevelState Initialize()
        {
            _interactableController?.Clear();
            if (_driver == null) _driver = GetComponent<LevelDriver>();
            if (_registry == null) _registry = GetComponent<LevelMarkerRegistry>();
            if (_controller == null) _controller = new LevelController(_state);
            _driver.Initialize();
            _initialized = true;
            if (isActiveAndEnabled && !_subscribed) OnEnable();
            else Reconcile();
            return _state;
        }

        public IReadOnlyLevelState InitializeGenerated(LevelGraph graph, IReadOnlyList<InteractableState> interactables = null)
        {
            Teardown();
            if (_controller == null) _controller = new LevelController(_state);
            if (_interactableController == null) _interactableController = new LevelInteractableController(_interactables);
            try
            {
                _controller.LoadGenerated(graph);
                _interactableController.Load(_state.Graph, interactables ?? Array.Empty<InteractableState>());
            }
            catch { Teardown(); throw; }
            ReadinessChanged?.Invoke(true);
            return _state;
        }

        public bool OpenDoor(int id, bool openedByPlayer = false)
        {
            if (!Change(id, InteractableKind.Door, InteractableStateValue.Open)) return false;
            if (_interactables.TryGet(id, out var door)) DoorOpened?.Invoke(door, openedByPlayer);
            return true;
        }
        public bool CloseDoor(int id) => Change(id, InteractableKind.Door, InteractableStateValue.Inactive);
        public void SetDoorJammed(int id, bool active) => _interactableController?.SetDoorJammed(id, active);
        public void ClearDoorJams() => _interactableController?.ClearDoorJams();
        public bool SetLit(int id, bool lit) => Change(id, InteractableKind.Light,
            lit ? InteractableStateValue.Lit : InteractableStateValue.Inactive);
        public bool Knock(int id) => Change(id, InteractableKind.KnockableProp, InteractableStateValue.Marked);
        public bool Mark(int id) => Change(id, InteractableKind.ThresholdMark, InteractableStateValue.Marked);
        public bool Break(int id)
        {
            if (!_interactables.TryGet(id, out var item)) return false;
            if (item.Kind != InteractableKind.Door && item.Kind != InteractableKind.Partition &&
                item.Kind != InteractableKind.KnockableProp) return false;
            return Change(id, item.Kind, InteractableStateValue.Broken);
        }

        private bool Change(int id, InteractableKind kind, InteractableStateValue value)
        {
            if (!_state.IsReady || !isActiveAndEnabled || _interactableController == null ||
                !_interactableController.Change(id, kind, value, out var before, out var after)) return false;
            InteractableChanged?.Invoke(before, after);
            return true;
        }

        public void Teardown()
        {
            OnDisable();
            if (_driver != null) _driver.Teardown();
            if (_registry != null) _registry.Clear();
            _controller?.Invalidate();
            _initialized = false;
        }

        private void OnEnable()
        {
            if (!_initialized || _subscribed) return;
            _driver.MarkerEnabled += HandleMarkerEnabled;
            _driver.MarkerDisabled += HandleMarkerDisabled;
            _subscribed = true;
            _driver.enabled = true;
            Reconcile();
        }

        private void OnDisable()
        {
            _interactableController?.Clear();
            _removalValidationPending = false;
            if (_subscribed)
            {
                _driver.MarkerEnabled -= HandleMarkerEnabled;
                _driver.MarkerDisabled -= HandleMarkerDisabled;
                _subscribed = false;
            }
            if (_driver != null) _driver.enabled = false;
            _controller?.Invalidate();
            if (_initialized) ReadinessChanged?.Invoke(false);
        }

        private void OnDestroy() => Teardown();

        private void LateUpdate()
        {
            if (!_initialized || !_removalValidationPending) return;
            _removalValidationPending = false;
            try
            {
                _controller.Rebuild(_registry.Records);
                ReadinessChanged?.Invoke(true);
            }
            catch (ArgumentException exception)
            {
                _controller.Invalidate();
                ReadinessChanged?.Invoke(false);
                Debug.LogError("Level marker registration is incomplete: " + exception.Message, this);
            }
        }

        private void Reconcile()
        {
            _registry.Clear();
            foreach (var record in _driver.CaptureEnabledMarkers()) _registry.Register(record);
            _controller.Rebuild(_registry.Records);
            _removalValidationPending = false;
            ReadinessChanged?.Invoke(true);
        }

        private void HandleMarkerEnabled(LevelMarkerRecord record)
        {
            try
            {
                _registry.Register(record);
                _controller.Rebuild(_registry.Records);
                _removalValidationPending = false;
                ReadinessChanged?.Invoke(true);
            }
            catch (ArgumentException exception)
            {
                _controller.Invalidate();
                ReadinessChanged?.Invoke(false);
                Debug.LogError("Level marker registration is incomplete: " + exception.Message, this);
            }
        }

        private void HandleMarkerDisabled(LevelMarkerRecord record)
        {
            _registry.Unregister(record.Id);
            try
            {
                _controller.Rebuild(_registry.Records);
                _removalValidationPending = false;
                ReadinessChanged?.Invoke(true);
            }
            catch (ArgumentException)
            {
                _controller.Invalidate();
                _removalValidationPending = true;
                ReadinessChanged?.Invoke(false);
            }
        }
    }
}
