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
//   - Route display snapshots and UI decisions through paired subscriptions.
// DEPENDENCIES:
//   - Session Progression, Presentation ProgressionUI and Core payloads.
// USAGE NOTES:
//   Scene-owned. Configure reconnects canonical persistent services after scene
//   assembly; no runtime state or engine rendering operations live here.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
using Worsen.Presentation.ProgressionUI;
namespace Worsen.Orchestrator
{
    public sealed class ProgressionUIOrchestrator : MonoBehaviour
    {
        private ProgressionSessionManager _progression;
        private ProgressionUIManager _ui;
        public void Configure(ProgressionSessionManager progression, ProgressionUIManager ui)
        { OnDisable(); _progression = progression; _ui = ui; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            if (_progression == null || _ui == null) return;
            _progression.SnapshotChanged += OnSnapshot;
            _ui.ChooseThreatRequested += OnThreat;
            _ui.ChooseCurseRequested += OnCurse;
            _ui.PurchaseRequested += OnPurchase;
            _ui.ContinueRequested += OnContinue;
            _ui.RestartRequested += OnRestart;
        }
        private void OnDisable()
        {
            if (_progression != null) _progression.SnapshotChanged -= OnSnapshot;
            if (_ui == null) return;
            _ui.ChooseThreatRequested -= OnThreat;
            _ui.ChooseCurseRequested -= OnCurse;
            _ui.PurchaseRequested -= OnPurchase;
            _ui.ContinueRequested -= OnContinue;
            _ui.RestartRequested -= OnRestart;
        }
        private void OnSnapshot(ProgressionSnapshot value) => _ui.SetSnapshot(value);
        private void OnThreat(string id, int revision) => _progression.ChooseThreat(id, revision);
        private void OnCurse(string id, int revision) => _progression.ChooseCurse(id, revision);
        private void OnPurchase(string id, int revision) => _progression.Purchase(id, revision);
        private void OnContinue(int revision) => _progression.ContinueShop(revision);
        private void OnRestart(int revision) => _progression.RestartRun(revision);
    }
}
