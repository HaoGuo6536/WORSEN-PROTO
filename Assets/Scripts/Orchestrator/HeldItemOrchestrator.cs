// ============================================================================
// HeldItemOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects the selected progression consumable to the scene's arm-free view.
//   It seeds late connections and hides the view outside live exploration without
//   retaining inventory, death or floor state in a second owner.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · HeldItem target.
// KEY RESPONSIBILITIES:
//   - Pair inventory and floor-release facts with the held-item Manager.
//   - Seed the current inventory after binding and after a new exploration snapshot.
//   - Suppress the view during pause, modal phases, terminal outcomes and camera catches.
//   - Clear floor-local presentation on release and disconnect.
// DEPENDENCIES:
//   Session Progression/Run/Expedition; Presentation HeldItem/Camera; Core facts.
// USAGE NOTES:
//   Scene-owned. Configure after HeldItem.Initialize and Camera.Initialize.
//   Update forwards current owner state before HeldItemDriver.LateUpdate; no
//   presentation clock or lifecycle latch is duplicated here. Camera retains its
//   death latch through the terminal hold, clearing it only on reset/revival.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Camera;
using Worsen.Presentation.HeldItem;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Orchestrator
{
    public sealed class HeldItemOrchestrator : MonoBehaviour
    {
        private ProgressionSessionManager _progression;
        private RunSessionManager _run;
        private ExpeditionSessionManager _expedition;
        private HeldItemManager _heldItem;
        private CameraManager _camera;

        public void Configure(ProgressionSessionManager progression, RunSessionManager run,
            ExpeditionSessionManager expedition, HeldItemManager heldItem, CameraManager camera)
        {
            OnDisable();
            _progression = progression; _run = run; _expedition = expedition;
            _heldItem = heldItem; _camera = camera;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            OnDisable();
            if (_progression == null || _run == null || _heldItem == null) return;
            _progression.ConsumablesChanged += OnConsumables;
            _progression.SnapshotChanged += OnSnapshot;
            _run.PlayerDeathPending += OnDeath;
            _run.PlayerDied += OnDeath;
            _run.PauseChanged += OnPause;
            if (_expedition != null) _expedition.FloorReleased += Clear;
            OnConsumables(_progression.Consumables);
            RefreshVisibility();
        }
        private void OnDisable()
        {
            if (_progression != null)
            {
                _progression.ConsumablesChanged -= OnConsumables;
                _progression.SnapshotChanged -= OnSnapshot;
            }
            if (_run != null)
            {
                _run.PlayerDeathPending -= OnDeath;
                _run.PlayerDied -= OnDeath;
                _run.PauseChanged -= OnPause;
            }
            if (_expedition != null) _expedition.FloorReleased -= Clear;
            Clear();
        }
        private void OnDestroy() => OnDisable();
        private void Update() => RefreshVisibility();
        private void RefreshVisibility()
        {
            if (_heldItem == null) return;
            _heldItem.SetSuppressed(_progression == null || _run == null ||
                _progression.Snapshot.Phase != ProgressionPhase.Exploring || _progression.Snapshot.Health <= 0f ||
                _run.IsPaused || _run.Phase == RunPhase.Boot || _run.Phase == RunPhase.Ended ||
                (_camera != null && _camera.IsDeathPresentationActive));
        }
        private void OnConsumables(ConsumableInventorySnapshot snapshot) => _heldItem.SetConsumables(snapshot);
        private void OnSnapshot(ProgressionSnapshot snapshot)
        { OnConsumables(_progression.Consumables); RefreshVisibility(); }
        private void OnDeath(EntityId player, Vector3 position) => _heldItem.SetSuppressed(true);
        private void OnPause(bool paused) => RefreshVisibility();
        private void Clear()
        {
            if (_heldItem == null) return;
            _heldItem.SetSuppressed(true);
            _heldItem.SetConsumables(default);
        }
    }
}
