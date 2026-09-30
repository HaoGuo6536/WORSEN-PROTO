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
//   - Arm every death's terminal gate before RunEnded can publish its terminal snapshot.
//   - Route display snapshots and UI decisions through paired subscriptions.
//   - Supply a fresh externally generated seed for normal UI restarts, retaining fixed-seed replay.
//   - Default Hidden Count to false until Progression supplies its future curse flag.
// DEPENDENCIES:
//   - Session Progression/Run, Presentation ProgressionUI and Core payloads.
//   - CameraManager supplies the authoritative catch-completed event.
// USAGE NOTES:
//   Scene-owned. Configure reconnects canonical persistent services after scene
//   assembly; no runtime state or engine rendering operations live here.
//   Call Configure after Camera.Initialize. Optional Run/Camera arguments preserve
//   legacy scene callers. PlayerDied is synchronous before RunEnded;
//   UI owns the event gate and unscaled fallback, cleared on restart/disable.
// ============================================================================
using UnityEngine;
using System;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Progression;
using Worsen.Presentation.ProgressionUI;
using Worsen.Session.Run;
using Worsen.Presentation.Camera;
namespace Worsen.Orchestrator
{
    public sealed class ProgressionUIOrchestrator : MonoBehaviour
    {
        private ProgressionSessionManager _progression;
        private ProgressionUIManager _ui;
        private RunSessionManager _run;
        private CameraManager _camera;
        private Func<int> _nextRunSeed;
        public void Configure(ProgressionSessionManager progression, ProgressionUIManager ui,
            RunSessionManager run = null, CameraManager camera = null, Func<int> nextRunSeed = null)
        {
            OnDisable(); _progression = progression; _ui = ui; _run = run;
            _nextRunSeed = nextRunSeed;
            _camera = camera;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_progression == null || _ui == null) return;
            OnDisable();
            _progression.SnapshotChanged += OnSnapshot;
            _progression.TransactionCommitted += OnTransaction;
            if (_run != null) { _run.PlayerDied += OnDeath; _run.CaptureStarted += OnCapture; }
            if (_camera != null) _camera.CatchHoldEnded += OnCatchEnded;
            _ui.ChooseThreatRequested += OnThreat;
            _ui.ChooseCurseRequested += OnCurse;
            _ui.PurchaseRequested += OnPurchase;
            _ui.ContinueRequested += OnContinue;
            _ui.RestartRequested += OnRestart;
        }
        private void OnDisable()
        {
            if (_progression != null)
            {
                _progression.SnapshotChanged -= OnSnapshot;
                _progression.TransactionCommitted -= OnTransaction;
            }
            if (_run != null) { _run.PlayerDied -= OnDeath; _run.CaptureStarted -= OnCapture; }
            if (_camera != null) _camera.CatchHoldEnded -= OnCatchEnded;
            if (_ui == null) return;
            _ui.ChooseThreatRequested -= OnThreat;
            _ui.ChooseCurseRequested -= OnCurse;
            _ui.PurchaseRequested -= OnPurchase;
            _ui.ContinueRequested -= OnContinue;
            _ui.RestartRequested -= OnRestart;
            _ui.ResetCatch();
        }
        private void OnSnapshot(ProgressionSnapshot value)
        { _ui.SetHiddenCount(false); _ui.SetSnapshot(value); }
        private void OnTransaction(ProgressionSnapshot before, ProgressionSnapshot after, string operation, string choice)
        { if (operation == nameof(ProgressionSessionManager.StartRun)) _ui.SetHiddenCount(false); }
        private void OnDeath(EntityId player, Vector3 position) => _ui.PrepareCatch(player);
        private void OnCatchEnded(EntityId player) => _ui.EndCatch(player);
        private void OnCapture(RunCaptureMetadata metadata) => _ui.ResetCatch();
        private void OnThreat(string id, int revision) => _progression.ChooseThreat(id, revision);
        private void OnCurse(string id, int revision) => _progression.ChooseCurse(id, revision);
        private void OnPurchase(string id, int revision) => _progression.Purchase(id, revision);
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
