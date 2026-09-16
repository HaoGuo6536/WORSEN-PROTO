// ============================================================================
// DebugOverlayDriver.cs
// ============================================================================
//
// PURPOSE:
//   Displays the development overlay using UI Toolkit and primitive samples
//   pushed by its Manager. It owns document binding and replacement, keeping
//   live visual objects out of presentation formatting and gameplay systems.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · DebugOverlay.
//   DebugOverlayManager commands this engine boundary; DebugOverlayPresenter
//   prepares text in a DriverState before the Driver applies it to labels.
//
// KEY RESPONSIBILITIES:
//   - Own the procedural DebugOverlaySurfaceDriver and bind its diagnostic ribbon.
//   - Rebind when UIDocument replaces its root, including disable/enable cycles.
//   - Resolve missing document assets by mirrored Resources paths and report failure.
//
// DEPENDENCIES:
//   - No other project systems. UnityEngine.UIElements owns the display surface.
//
// USAGE NOTES:
//   - Persistent: owned by the persistent DebugOverlayManager and its GameObject.
//   - Own DriverConfig: DebugOverlayDriverConfig; no global engine side effects.
//   - Commands only come from its Manager. LateUpdate checks document identity,
//     never game state; UI Toolkit can recreate a root after this Driver enables.
//   - Reuses setup's document and PanelSettings; legacy UXML remains serialized but optional.
//
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace Worsen.Presentation.DebugOverlay
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugOverlayDriver : MonoBehaviour
    {
        private const string VisualTreePath = "UI/Presentation/DebugOverlay/DebugOverlay";
        private const string PanelSettingsPath = "UI/Presentation/DebugOverlay/DebugOverlayPanelSettings";

        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private PanelSettings _panelSettings;

        private DebugOverlayDriverConfig _config;
        private DebugOverlayDriverState _state;
        private DebugOverlayPresenter _presenter;
        private DebugOverlaySurfaceDriver _surface;
        private VisualElement _boundRoot;
        private Label _tickLabel;
        private Label _phaseLabel;
        private Label _speedLabel;
        private Label _movementLabel;

        public void Initialize(DebugOverlayDriverConfig config)
        {
            _config = config;
            _state = _state ?? new DebugOverlayDriverState();
            _presenter = _presenter ?? new DebugOverlayPresenter();
            if (_document == null) _document = GetComponent<UIDocument>();
            if (_visualTree == null) _visualTree = _document.visualTreeAsset;
            if (_panelSettings == null) _panelSettings = _document.panelSettings;
            if (_visualTree == null) _visualTree = Resources.Load<VisualTreeAsset>(VisualTreePath);
            if (_panelSettings == null) _panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);

            if (_config == null || _panelSettings == null)
            {
                Debug.LogWarning("Debug overlay needs config and PanelSettings. Run Worsen/Scenes/1 — Build TagArena to restore its wiring.", this);
                return;
            }

            _document.panelSettings = _panelSettings;
            if (_surface == null) _surface = GetComponent<DebugOverlaySurfaceDriver>();
            if (_surface == null) _surface = gameObject.AddComponent<DebugOverlaySurfaceDriver>();
            BindAndApply();
        }

        public void SetRunStatus(long tick, string phase)
        {
            if (_state == null) return;
            _presenter.SetRunStatus(_state, tick, phase);
            ApplyText();
        }

        public void SetPlayerStatus(float speed, string movement)
        {
            if (_state == null || _config == null) return;
            _presenter.SetPlayerStatus(_state, speed, movement, _config.SpeedDecimalPlaces);
            ApplyText();
        }

        public void SetPlayerUnavailable()
        {
            if (_state == null) return;
            _presenter.SetPlayerUnavailable(_state);
            ApplyText();
        }

        public void Teardown()
        {
            HideAndUnbind();
            _state = null;
            _presenter = null;
            _config = null;
        }

        private void OnEnable()
        {
            if (_state != null) BindAndApply();
        }

        private void OnDisable()
        {
            HideAndUnbind();
        }

        private void LateUpdate()
        {
            if (_state == null || _document == null) return;
            if (!_document.isActiveAndEnabled) { HideAndUnbind(); return; }
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement)) BindAndApply();
        }

        private void BindAndApply()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _config == null || _surface == null) return;
            var root = _document.rootVisualElement;
            if (root == null) return;
            HideAndUnbind();
            _surface.Build(root, _config);
            var panel = root.Q<VisualElement>("debug-overlay");
            _tickLabel = root.Q<Label>("tick-value");
            _phaseLabel = root.Q<Label>("phase-value");
            _speedLabel = root.Q<Label>("speed-value");
            _movementLabel = root.Q<Label>("movement-value");
            _boundRoot = root;
            if (panel == null || _tickLabel == null || _phaseLabel == null || _speedLabel == null || _movementLabel == null)
            {
                Debug.LogWarning("Debug overlay surface is missing its required named elements.", this);
                return;
            }

            root.pickingMode = PickingMode.Ignore;
            root.style.display = DisplayStyle.Flex;
            ApplyText();
        }

        private void ApplyText()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled) return;
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement))
            {
                BindAndApply();
                return;
            }
            if (_tickLabel == null || _phaseLabel == null || _speedLabel == null || _movementLabel == null) return;
            _tickLabel.text = _state.TickText;
            _phaseLabel.text = _state.PhaseText;
            _speedLabel.text = _state.SpeedText;
            _movementLabel.text = _state.MovementText;
        }

        private void HideAndUnbind()
        {
            if (_surface != null) _surface.Release();
            if (_boundRoot != null) _boundRoot.style.display = DisplayStyle.None;
            _boundRoot = null;
            _tickLabel = null;
            _phaseLabel = null;
            _speedLabel = null;
            _movementLabel = null;
        }
    }
}
