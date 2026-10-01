// ============================================================================
// ProgressionUIOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects expedition menus to the persistent progression flow. It forwards
//   committed snapshots and revision-stamped choices without duplicating the
//   wallet, selection, restart or floor-generation rules.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · ProgressionUI target.
// KEY RESPONSIBILITIES:
//   - Suppress the legacy terminal shelter when HorrorRun delegates outcomes to Results.
//   - Arm every death's terminal gate before RunEnded can publish its terminal snapshot.
//   - Route display snapshots, actual modal visibility and UI decisions to their owners.
//   - Route Bargains, rerolls and replacements to Progression; Continue walks away for free.
//   - Supply a fresh externally generated seed for normal UI restarts, retaining fixed-seed replay.
// DEPENDENCIES:
//   - Session Progression/Run, Presentation ProgressionUI/HUD and Core payloads.
//   - CameraManager supplies the authoritative catch-completed event.
// USAGE NOTES:
//   Scene-owned. Configure reconnects canonical persistent services after scene
//   assembly; no runtime state or engine rendering operations live here.
//   Call Configure after Camera.Initialize. Optional Run/Camera arguments preserve
//   legacy scene callers. PlayerDied is synchronous before RunEnded;
//   UI owns the event gate and unscaled fallback, cleared on restart/disable.
//   Inject the UI owner's actual visibility getter; never infer it from progression
//   phase because catch gates and explicit Hide can differ from that phase.
// ============================================================================
using UnityEngine;
using System;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Progression;
using Worsen.Presentation.ProgressionUI;
using Worsen.Session.Run;
using Worsen.Presentation.Camera;
using Worsen.Presentation.HUD;
namespace Worsen.Orchestrator
{
    public sealed class ProgressionUIOrchestrator : MonoBehaviour
    {
        private ProgressionSessionManager _progression;
        private ProgressionUIManager _ui;
        private RunSessionManager _run;
        private CameraManager _camera;
        private Func<int> _nextRunSeed;
        private bool _terminalResults;
        private HUDManager _hud;
        private Func<bool> _modalVisible;
        public void Configure(ProgressionSessionManager progression, ProgressionUIManager ui,
            RunSessionManager run = null, CameraManager camera = null, Func<int> nextRunSeed = null, bool terminalResults = false,
            HUDManager hud = null, Func<bool> modalVisible = null)
        {
            OnDisable(); _progression = progression; _ui = ui; _run = run;
            _nextRunSeed = nextRunSeed;
            _terminalResults = terminalResults;
            _camera = camera;
            _hud = hud; _modalVisible = modalVisible;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_progression == null || _ui == null) return;
            OnDisable();
            _progression.SnapshotChanged += OnSnapshot;

            if (_run != null) { _run.PlayerDied += OnDeath; _run.CaptureStarted += OnCapture; }
            if (_camera != null) _camera.CatchHoldEnded += OnCatchEnded;
            _ui.ChooseThreatRequested += OnThreat;
            _ui.ChooseCurseRequested += OnCurse;
            _ui.BargainRequested += OnBargain;
            _ui.PurchaseRequested += OnPurchase;
            _ui.RerollRequested += OnReroll;
            _ui.ReplacementRequested += OnReplacement;
            _ui.CancelReplacementRequested += OnCancelReplacement;
            _ui.ContinueRequested += OnContinue;
            _ui.RestartRequested += OnRestart;
            RefreshModalVisibility();
        }
        private void OnDisable()
        {
            if (_hud != null) _hud.SetModalOpen(false);
            if (_progression != null)
            {
                _progression.SnapshotChanged -= OnSnapshot;

            }
            if (_run != null) { _run.PlayerDied -= OnDeath; _run.CaptureStarted -= OnCapture; }
            if (_camera != null) _camera.CatchHoldEnded -= OnCatchEnded;
            if (_ui == null) return;
            _ui.ChooseThreatRequested -= OnThreat;
            _ui.ChooseCurseRequested -= OnCurse;
            _ui.BargainRequested -= OnBargain;
            _ui.PurchaseRequested -= OnPurchase;
            _ui.RerollRequested -= OnReroll;
            _ui.ReplacementRequested -= OnReplacement;
            _ui.CancelReplacementRequested -= OnCancelReplacement;
            _ui.ContinueRequested -= OnContinue;
            _ui.RestartRequested -= OnRestart;
            _ui.ResetCatch();
        }
        private void OnSnapshot(ProgressionSnapshot value)
        {

            if (_terminalResults && value.Phase == ProgressionPhase.Ended) _ui.Hide();
            else _ui.SetSnapshot(value);
            RefreshModalVisibility();
        }

        private void LateUpdate() => RefreshModalVisibility();
        private void RefreshModalVisibility()
        { if (_hud != null) _hud.SetModalOpen(_ui != null && _ui.isActiveAndEnabled && (_modalVisible?.Invoke() ?? false)); }

        private void OnDeath(EntityId player, Vector3 position) => _ui.PrepareCatch(player);
        private void OnCatchEnded(EntityId player) => _ui.EndCatch(player);
        private void OnCapture(RunCaptureMetadata metadata) => _ui.ResetCatch();
        private void OnThreat(string id, int revision) => _progression.ChooseThreat(id, revision);
        private void OnCurse(string id, int revision) => _progression.ChooseCurse(id, revision);
        private void OnBargain(string id, int revision) => _progression.TakeBargain(id, revision);
        private void OnPurchase(string id, int revision) => _progression.Purchase(id, revision);
        private void OnCancelReplacement(int revision) => _progression.CancelReplacement(revision);
        private void OnReplacement(int slot, int revision) => _progression.ReplaceInventorySlot(slot, revision);
        private void OnReroll(int revision)
        {
            if (_progression.Snapshot.Phase == ProgressionPhase.Shop) _progression.RerollShop(revision);
            else _progression.RerollSelection(revision);
        }
        private void OnContinue(int revision) => _progression.ContinueShop(revision);
        private void OnRestart(int revision)
        {
            ProgressionSnapshot snapshot = _progression.Snapshot;
            if (snapshot.Revision != revision || !snapshot.CanRestart) return;
            _ui.ResetCatch();
            _progression.RestartRun(revision, _nextRunSeed != null ? _nextRunSeed() : snapshot.Seed);
        }
    }
}
