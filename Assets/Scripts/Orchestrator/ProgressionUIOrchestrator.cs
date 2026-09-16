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
//   - Arm terminal reveal deferral on confirmed consumption before the Ended snapshot.
//   - Route display snapshots and UI decisions through paired subscriptions.
//   - Supply a fresh externally generated seed for normal UI restarts, retaining fixed-seed replay.
// DEPENDENCIES:
//   - Session Progression/Run, Presentation ProgressionUI and Core payloads.
//   - CameraManager supplies its configured duration only during Configure.
// USAGE NOTES:
//   Scene-owned. Configure reconnects canonical persistent services after scene
//   assembly; no runtime state or engine rendering operations live here.
//   Call Configure after Camera.Initialize. Optional Run/Camera arguments preserve
//   legacy scene callers. Consumed is synchronous before Run finishes the tick;
//   UI owns the unscaled reveal gate and clears it on a new generation/disable.
// ============================================================================
using UnityEngine;
using System;
using Worsen.Core;
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
        private Func<int> _nextRunSeed;
        [SerializeField, Range(0.1f, 2f)] private float _consumptionSeconds = 0.9f;
        public void Configure(ProgressionSessionManager progression, ProgressionUIManager ui,
            RunSessionManager run = null, CameraManager camera = null, Func<int> nextRunSeed = null)
        {
            OnDisable(); _progression = progression; _ui = ui; _run = run;
            _nextRunSeed = nextRunSeed;
            if (camera != null) _consumptionSeconds = camera.ConsumptionSeconds;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_progression == null || _ui == null) return;
            _progression.SnapshotChanged += OnSnapshot;
            if (_run != null) _run.CollapseHandPublished += OnCollapseHand;
            _ui.ChooseThreatRequested += OnThreat;
            _ui.ChooseCurseRequested += OnCurse;
            _ui.PurchaseRequested += OnPurchase;
            _ui.ContinueRequested += OnContinue;
            _ui.RestartRequested += OnRestart;
        }
        private void OnDisable()
        {
            if (_progression != null) _progression.SnapshotChanged -= OnSnapshot;
            if (_run != null) _run.CollapseHandPublished -= OnCollapseHand;
            if (_ui == null) return;
            _ui.ChooseThreatRequested -= OnThreat;
            _ui.ChooseCurseRequested -= OnCurse;
            _ui.PurchaseRequested -= OnPurchase;
            _ui.ContinueRequested -= OnContinue;
            _ui.RestartRequested -= OnRestart;
        }
        private void OnSnapshot(ProgressionSnapshot value) => _ui.SetSnapshot(value);
        private void OnCollapseHand(CollapseHandFact fact)
        {
            if (fact.Kind == CollapseHandEventKind.Consumed) _ui.DeferTerminal(_consumptionSeconds);
        }
        private void OnThreat(string id, int revision) => _progression.ChooseThreat(id, revision);
        private void OnCurse(string id, int revision) => _progression.ChooseCurse(id, revision);
        private void OnPurchase(string id, int revision) => _progression.Purchase(id, revision);
        private void OnContinue(int revision) => _progression.ContinueShop(revision);
        private void OnRestart(int revision)
        {
            ProgressionSnapshot snapshot = _progression.Snapshot;
            if (snapshot.Revision != revision || !snapshot.CanRestart) return;
            _progression.RestartRun(revision, _nextRunSeed != null ? _nextRunSeed() : snapshot.Seed);
        }
    }
}
