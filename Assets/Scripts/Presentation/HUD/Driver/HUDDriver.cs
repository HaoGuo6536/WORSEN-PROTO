// ============================================================================
// HUDDriver.cs
// ============================================================================
//
// PURPOSE:
//   Presents supplied run facts as a quiet, vector-drawn survival interface.
//   The existing commands and chase restoration remain unchanged; an owned
//   drawing sub-driver builds the UI Toolkit tree and applies pure display state.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Own document binding and the HUDVisualDriver lifetime.
//   - Preserve supplied facts across document recreation and disable/enable.
//   - Receive camera aim orientation for a three-dimensional objective compass.
//
// DEPENDENCIES:
//   Core primitives and own HUD presentation stack; Unity UI Toolkit only at Driver boundaries.
//
// USAGE NOTES:
//   Scene-owned through HUDManager; own HUDDriverConfig. No global side effects.
//   Serialized UXML is retained for scene compatibility, but the vector tree is built in code.
//   Only this Driver samples unscaled time and passes it to the pure Presenter.
//
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;

namespace Worsen.Presentation.HUD
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HUDDriver : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private PanelSettings _panelSettings;
        private HUDDriverConfig _config;
        private HUDDriverState _state;
        private HUDPresenter _presenter;
        private HUDVisualDriver _visual;
        private VisualElement _boundRoot;
        private bool _ownsVisual;

        public void Initialize(HUDDriverConfig config)
        {
            _config = config;
            _state = _state ?? new HUDDriverState();
            _presenter = _presenter ?? new HUDPresenter();
            if (_document == null) _document = GetComponent<UIDocument>();
            if (_panelSettings == null) _panelSettings = _document.panelSettings;
            if (_panelSettings == null) _panelSettings = Resources.Load<PanelSettings>("UI/Presentation/HUD/HUDPanelSettings");
            if (_config == null || _panelSettings == null)
            {
                Debug.LogWarning("HUD configuration or PanelSettings is missing. Restore the HUD scene service.", this);
                return;
            }
            if (_visual == null) _visual = GetComponent<HUDVisualDriver>();
            if (_visual == null) { _visual = gameObject.AddComponent<HUDVisualDriver>(); _ownsVisual = true; }
            _document.panelSettings = _panelSettings;
            BindAndApply();
        }

        public void SetCount(int collected, int total)
        {
            if (_state == null) return;
            _presenter.SetCount(_state, collected, total);
            Apply();
        }

        public void SetExitState(ExitState exitState)
        {
            if (_state == null) return;
            _presenter.SetExitState(_state, exitState);
            Apply();
        }

        public void SetDirection(Vector3 worldDirection, bool visible)
        {
            if (_state == null) return;
            _presenter.SetDirection(_state, worldDirection, visible);
            Apply();
        }

        public void SetHeading(float headingDegrees)
        {
            if (_state == null) return;
            _presenter.SetHeading(_state, headingDegrees);
            Apply();
        }

        public void SetViewRotation(Quaternion rotation)
        {
            if (_state == null) return;
            _presenter.SetViewRotation(_state, rotation);
            Apply();
        }

        public void SetItemSlots(int emptySlotCount)
        {
            if (_state == null || _config == null) return;
            _presenter.SetItemSlots(_state, emptySlotCount, _config.MaximumDisplayedSlots);
            Apply();
        }

        public void SetChaseMode(bool chasing)
        {
            if (_state == null) return;
            _presenter.SetChaseMode(_state, chasing);
            Apply();
        }

        public void ResetRunView()
        {
            if (_state == null) return;
            _presenter.ResetRunView(_state);
            Apply();
        }

        public void Teardown()
        {
            HideAndUnbind();
            _state = null;
            _presenter = null;
            _config = null;
        }

        private void OnEnable() { if (_state != null) BindAndApply(); }
        private void OnDisable() => HideAndUnbind();
        private void OnDestroy()
        {
            Teardown();
            if (_ownsVisual && _visual != null) Destroy(_visual);
        }

        private void LateUpdate()
        {
            if (_state == null || _config == null || _document == null) return;
            _presenter.Tick(_state, Time.unscaledDeltaTime, _config.RestoreSeconds);
            if (!_document.isActiveAndEnabled) { HideAndUnbind(); return; }
            Apply();
        }

        private void BindAndApply()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _config == null || _visual == null) return;
            var root = _document.rootVisualElement;
            if (root == null) return;
            HideAndUnbind();
            _boundRoot = root;
            _visual.Bind(root, _config);
            _visual.Apply(_state);
        }

        private void Apply()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _state == null) return;
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement)) { BindAndApply(); return; }
            if (_visual != null) _visual.Apply(_state);
        }

        private void HideAndUnbind()
        {
            if (_visual != null) _visual.Unbind();
            _boundRoot = null;
        }
    }
}
