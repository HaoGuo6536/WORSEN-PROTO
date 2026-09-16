// ============================================================================
// ProgressionUIDriver.cs
// ============================================================================
//
// PURPOSE:
//   Owns the progression document, prepared state and drawing sub-drivers.
//   It receives immutable snapshots from its Manager and emits revision-tagged
//   choices, keeping menu input and rendering separate from progression rules.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Bind and rebuild the current document without losing presentation state.
//   - Route one admitted UI action and maintain symmetric callback ownership.
//   - Own panel/card sub-drivers and scheduled keyboard focus.
//
// DEPENDENCIES:
//   Core ProgressionSnapshot/Phase; own Presenter, DriverState and drawing stack.
//
// USAGE NOTES:
//   Scene-owned through ProgressionUIManager; owns ProgressionUIDriverConfig.
//   No global side effects. Orchestrator handles cursor/input and game suspension.
//   Uses serialized PanelSettings, then its resource, then existing Results settings
//   without mutating the shared asset. UXML is not required.
//
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;

namespace Worsen.Presentation.ProgressionUI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProgressionUIDriver : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        private ProgressionUIDriverConfig _config;
        private ProgressionUIDriverState _state;
        private ProgressionUIPresenter _presenter;
        private ProgressionUIVisualDriver _visual;
        private readonly List<ProgressionUICardDriver> _cards = new List<ProgressionUICardDriver>();
        private VisualElement _boundRoot;
        private IVisualElementScheduledItem _focusTask;
        private bool _ownsVisual;

        public event Action<string, int> ThreatChosen;
        public event Action<string, int> CurseChosen;
        public event Action<string, int> PurchaseClicked;
        public event Action<int> ContinueClicked;
        public event Action<int> RestartClicked;

        public void Initialize(ProgressionUIDriverConfig config)
        {
            _config = config;
            _state = _state ?? new ProgressionUIDriverState();
            _presenter = _presenter ?? new ProgressionUIPresenter();
            if (_document == null) _document = GetComponent<UIDocument>();
            if (_panelSettings == null) _panelSettings = _document.panelSettings;
            if (_panelSettings == null) _panelSettings = Resources.Load<PanelSettings>("UI/Presentation/ProgressionUI/ProgressionUIPanelSettings");
            if (_panelSettings == null) _panelSettings = Resources.Load<PanelSettings>("UI/Presentation/Results/ResultsPanelSettings");
            if (_config == null || _panelSettings == null)
            {
                Debug.LogWarning("Progression UI needs its DriverConfig and PanelSettings. Restore the scene service.", this);
                return;
            }
            if (_visual == null) _visual = GetComponent<ProgressionUIVisualDriver>();
            if (_visual == null) { _visual = gameObject.AddComponent<ProgressionUIVisualDriver>(); _ownsVisual = true; }
            _document.panelSettings = _panelSettings;
            if (isActiveAndEnabled) OnEnable();
        }

        public void SetSnapshot(ProgressionSnapshot snapshot)
        {
            if (_state == null) return;
            bool focus = !_state.HasSnapshot || _state.Phase != snapshot.Phase || _state.Pending;
            if (_presenter.Present(_state, snapshot)) Apply(focus);
        }

        public void Hide()
        {
            if (_state == null) return;
            _presenter.Hide(_state);
            Apply(false);
        }

        public void Teardown()
        {
            Unhook();
            HideAndUnbind();
            foreach (var card in _cards) if (card != null) Destroy(card);
            _cards.Clear();
            _state = null; _presenter = null; _config = null;
        }

        private void OnEnable()
        {
            if (_visual == null || _state == null) return;
            Unhook();
            _visual.ContinueClicked += OnContinue;
            _visual.RestartClicked += OnRestart;
            foreach (var card in _cards) card.Activated += OnCardActivated;
            BindAndApply();
        }

        private void OnDisable() { Unhook(); HideAndUnbind(); }
        private void OnDestroy()
        {
            Teardown();
            if (_ownsVisual && _visual != null) Destroy(_visual);
        }

        private void LateUpdate()
        {
            if (_state == null || _document == null) return;
            if (!_document.isActiveAndEnabled) { HideAndUnbind(); return; }
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement)) BindAndApply();
        }

        private void BindAndApply()
        {
            if (!isActiveAndEnabled || _config == null || _visual == null || _document == null || !_document.isActiveAndEnabled) return;
            var root = _document.rootVisualElement;
            if (root == null) return;
            HideAndUnbind();
            _boundRoot = root;
            _visual.Bind(root, _config);
            EnsureCards();
            foreach (var card in _cards) card.Bind(_visual.CardContainer, _config);
            Apply(true);
        }

        private void Apply(bool focus)
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _state == null || _visual == null) return;
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement)) { BindAndApply(); return; }
            _visual.Apply(_state);
            EnsureCards();
            for (int i = 0; i < _cards.Count; i++) _cards[i].Apply(_state.Cards[i], _state.Revision, _state.Pending);
            if (focus && _state.ModalVisible && !_state.Hidden)
            {
                _focusTask?.Pause();
                _focusTask = _boundRoot.schedule.Execute(FocusFirst).Every(16);
            }
        }

        private void EnsureCards()
        {
            while (_cards.Count > _state.Cards.Length)
            {
                var card = _cards[_cards.Count - 1];
                card.Activated -= OnCardActivated; card.Unbind();
                _cards.RemoveAt(_cards.Count - 1); Destroy(card);
            }
            while (_cards.Count < _state.Cards.Length)
            {
                var card = gameObject.AddComponent<ProgressionUICardDriver>();
                _cards.Add(card);
                if (isActiveAndEnabled) card.Activated += OnCardActivated;
                card.Bind(_visual.CardContainer, _config);
            }
        }

        private void FocusFirst()
        {
            if (_state == null || _state.Hidden || !_state.ModalVisible || _state.Pending ||
                !isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled ||
                !ReferenceEquals(_boundRoot, _document.rootVisualElement)) { _focusTask?.Pause(); return; }
            foreach (var card in _cards)
                if (card.FocusIfEnabled()) { _focusTask?.Pause(); return; }
            bool hasEnabledCard = false;
            foreach (var model in _state.Cards) hasEnabledCard |= model.Enabled;
            if (!hasEnabledCard && _visual.FocusPrimary()) _focusTask?.Pause();
        }

        private void OnCardActivated(string id, int revision)
        {
            if (_state == null) return;
            var action = _state.Phase == ProgressionPhase.ChooseThreat ? ProgressionUIAction.ChooseThreat :
                _state.Phase == ProgressionPhase.ChooseCurse ? ProgressionUIAction.ChooseCurse : ProgressionUIAction.Purchase;
            if (!Admit(action, id, revision)) return;
            if (action == ProgressionUIAction.ChooseThreat) ThreatChosen?.Invoke(id, revision);
            else if (action == ProgressionUIAction.ChooseCurse) CurseChosen?.Invoke(id, revision);
            else PurchaseClicked?.Invoke(id, revision);
        }
        private void OnContinue(int revision) { if (Admit(ProgressionUIAction.Continue, "", revision)) ContinueClicked?.Invoke(revision); }
        private void OnRestart(int revision) { if (Admit(ProgressionUIAction.Restart, "", revision)) RestartClicked?.Invoke(revision); }

        private bool Admit(ProgressionUIAction action, string id, int revision)
        {
            if (_state == null || !isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled ||
                !ReferenceEquals(_boundRoot, _document.rootVisualElement) || !_presenter.TryIssue(_state, action, id, revision)) return false;
            Apply(false);
            return true;
        }

        private void Unhook()
        {
            if (_visual != null) { _visual.ContinueClicked -= OnContinue; _visual.RestartClicked -= OnRestart; }
            foreach (var card in _cards) if (card != null) card.Activated -= OnCardActivated;
        }

        private void HideAndUnbind()
        {
            _focusTask?.Pause(); _focusTask = null;
            foreach (var card in _cards) if (card != null) card.Unbind();
            if (_visual != null) _visual.Unbind();
            _boundRoot = null;
        }
    }
}
