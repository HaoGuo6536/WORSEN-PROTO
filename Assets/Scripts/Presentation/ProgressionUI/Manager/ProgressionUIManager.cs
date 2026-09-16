// ============================================================================
// ProgressionUIManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides the scene-owned presentation endpoint for round choices and shops.
//   It forwards Core snapshots to its own Driver and republishes user choices
//   so an Orchestrator can route them to the progression owner.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · ProgressionUI (Service system).
//
// KEY RESPONSIBILITIES:
//   - Own Driver configuration and symmetric enable/disable event routing.
//   - Republish Core audio feedback for UI navigation and intent; purchases sound only after Session commits.
//   - Forward an optional terminal reveal delay without delaying authoritative death.
//   - Expose snapshot, hide and lifecycle commands without game rules.
//
// DEPENDENCIES:
//   Core ProgressionSnapshot and own ProgressionUIDriver/DriverConfig only.
//
// USAGE NOTES:
//   Scene-owned tier (§8); no singleton, persistence or global side effects.
//   Explicit setup may pass a config; otherwise the mirrored Resources asset is used.
//   Create the scene service inactive, wire it, then enable or Initialize explicitly.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.ProgressionUI
{
    public sealed class ProgressionUIManager : MonoBehaviour
    {
        [SerializeField] private ProgressionUIDriverConfig _config;
        [SerializeField] private ProgressionUIDriver _driver;
        private bool _initialized;

        public event Action<string, int> ChooseThreatRequested;
        public event Action<string, int> ChooseCurseRequested;
        public event Action<string, int> PurchaseRequested;
        public event Action<int> ContinueRequested;
        public event Action<int> RestartRequested;
        public event Action<CueId> Feedback;

        public void Initialize(ProgressionUIDriverConfig config = null)
        {
            if (config != null) _config = config;
            if (_initialized) return;
            if (_config == null) _config = Resources.Load<ProgressionUIDriverConfig>("ScriptableObjects/Presentation/ProgressionUI/ProgressionUIDriverConfig");
            if (_driver == null) _driver = GetComponent<ProgressionUIDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<ProgressionUIDriver>();
            _driver.Initialize(_config);
            _driver.enabled = isActiveAndEnabled;
            _initialized = _config != null;
            if (isActiveAndEnabled) WireEvents();
        }

        public void SetSnapshot(ProgressionSnapshot snapshot) { if (_driver != null) _driver.SetSnapshot(snapshot); }
        public void DeferTerminal(float seconds) { if (_driver != null) _driver.DeferTerminal(seconds); }
        public void Hide() { if (_driver != null) _driver.Hide(); }
        public void Teardown()
        {
            UnwireEvents();
            if (_driver != null) _driver.Teardown();
            _initialized = false;
        }

        private void Awake() => Initialize();
        private void OnEnable()
        {
            Initialize();
            if (_driver == null) return;
            _driver.enabled = true;
            WireEvents();
        }
        private void OnDisable()
        {
            UnwireEvents();
            if (_driver != null) _driver.enabled = false;
        }
        private void OnDestroy() => Teardown();
        private void WireEvents()
        {
            if (_driver == null) return;
            UnwireEvents();
            _driver.ThreatChosen += OnThreatChosen;
            _driver.CurseChosen += OnCurseChosen;
            _driver.PurchaseClicked += OnPurchaseClicked;
            _driver.ContinueClicked += OnContinueClicked;
            _driver.RestartClicked += OnRestartClicked;
            _driver.Feedback += OnFeedback;
        }
        private void UnwireEvents()
        {
            if (_driver == null) return;
            _driver.ThreatChosen -= OnThreatChosen;
            _driver.CurseChosen -= OnCurseChosen;
            _driver.PurchaseClicked -= OnPurchaseClicked;
            _driver.ContinueClicked -= OnContinueClicked;
            _driver.RestartClicked -= OnRestartClicked;
            _driver.Feedback -= OnFeedback;
        }
        private void OnFeedback(CueId cue) => Feedback?.Invoke(cue);
        private void OnThreatChosen(string id, int revision) => ChooseThreatRequested?.Invoke(id, revision);
        private void OnCurseChosen(string id, int revision) => ChooseCurseRequested?.Invoke(id, revision);
        private void OnPurchaseClicked(string id, int revision) => PurchaseRequested?.Invoke(id, revision);
        private void OnContinueClicked(int revision) => ContinueRequested?.Invoke(revision);
        private void OnRestartClicked(int revision) => RestartRequested?.Invoke(revision);
    }
}
