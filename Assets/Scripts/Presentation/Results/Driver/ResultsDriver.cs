// ============================================================================
// ResultsDriver.cs
// ============================================================================
//
// PURPOSE:
//   Displays results through UI Toolkit and reports restart clicks to its owning Manager.
//   The latest summary and click latch survive document recreation, while callbacks
//   are removed from old roots so toggling the interface cannot duplicate requests.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Render no-floor failures without gameplay metrics; pair Retry/title callbacks and latch both.
//   - Apply catch completion as a hard cut and warn once on unscaled fallback expiry.
//   - Resolve the existing PanelSettings and own the procedural ResultsSurfaceDriver.
//   - Bind, render, unbind and rebind the current document; report UI interactions as facts.
//   - Bind detailed outcomes/history and publish a validated optional seed on restart.
//
// DEPENDENCIES:
//   - Worsen.Core RunSummary; UnityEngine.UIElements. No gameplay system references.
//
// USAGE NOTES:
//   - Scene-owned through ResultsManager; own ResultsDriverConfig and no global side effects.
//   - Button callbacks pair at bind/unbind; Manager event routing pairs at enable/disable.
//   - Disabling clears pending catches; already visible summaries survive document rebinding.
//   - Legacy UXML references remain serialized; the owned surface builds the named tree.
//
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Results
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ResultsDriver : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _visualTree;
        [SerializeField] private PanelSettings _panelSettings;
        private ResultsDriverConfig _config;
        private ResultsDriverState _state;
        private ResultsPresenter _presenter;
        private ResultsSurfaceDriver _surface;
        private VisualElement _boundRoot;
        private VisualElement _overlay;
        private Label _title, _runTime, _cakes, _goldenCakes, _chases, _escapes, _chaseTime, _endReason;
        private Button _restart, _returnTitle;
        private VisualElement _metrics;
        private Label _kicker, _hint;
        private TextField _seedInput;
        private Label _cause, _killer, _grabs, _exitTime, _depth, _seed, _best, _seedError;

        public event Action RestartClicked;
        public event Action ReturnToTitleClicked;
        public event Action<bool, int> RestartWithSeedClicked;

        public void Initialize(ResultsDriverConfig config)
        {
            _config = config;
            _state = _state ?? new ResultsDriverState();
            _presenter = _presenter ?? new ResultsPresenter();
            if (_document == null) _document = GetComponent<UIDocument>();
            if (_visualTree == null) _visualTree = _document.visualTreeAsset;
            if (_panelSettings == null) _panelSettings = _document.panelSettings;
            if (_visualTree == null) _visualTree = Resources.Load<VisualTreeAsset>("UI/Presentation/Results/Results");
            if (_panelSettings == null) _panelSettings = Resources.Load<PanelSettings>("UI/Presentation/Results/ResultsPanelSettings");
            if (_config == null || _panelSettings == null)
            {
                Debug.LogWarning("Results UI assets/config are missing. Run Worsen/Results/Restore UI Assets and wire the scene service.", this);
                return;
            }
            _document.panelSettings = _panelSettings;
            if (_surface == null) _surface = GetComponent<ResultsSurfaceDriver>();
            if (_surface == null) _surface = gameObject.AddComponent<ResultsSurfaceDriver>();
            BindAndApply();
        }

        public void Show(RunSummary summary)
        {
            if (_state == null) return;
            _presenter.Show(_state, summary, _config != null ? _config.CatchTimeoutSeconds : ResultsDriverConfig.DefaultCatchTimeoutSeconds);
            Apply();
        }

        public void ShowNoFloor(int seed)
        {
            if (_state == null) return;
            _presenter.ShowNoFloor(_state, seed); Apply();
        }
        public void SetGenerationSeed(int seed) { if (_state != null) _state.GenerationSeed = seed; }

        public void SetBestDepth(int depth)
        {
            if (_state == null) return;
            _presenter.SetBestDepth(_state, depth); Apply();
        }

        public void PrepareCatch(EntityId player) { if (_state != null) _presenter.PrepareCatch(_state, player); }
        public void EndCatch(EntityId player)
        {
            if (_state == null) return;
            _presenter.EndCatch(_state, player);
            Apply();
        }

        public void Hide()
        {
            if (_state == null) return;
            _presenter.Hide(_state);
            Apply();
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
            if (_state != null && !_state.Visible) _presenter.Hide(_state);
            HideAndUnbind();
        }

        private void LateUpdate()
        {
            if (_state == null || _document == null) return;
            if (_presenter.Tick(_state, Time.unscaledDeltaTime))
            {
                Debug.LogWarning("Results catch timeout: CatchHoldEnded was not received; revealing the pending summary by hard cut.", this);
                Apply();
            }
            if (!_document.isActiveAndEnabled) { HideAndUnbind(); return; }
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement)) BindAndApply();
        }

        private void BindAndApply()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _config == null || _surface == null) return;
            var root = _document.rootVisualElement;
            if (root == null) return;
            HideAndUnbind();
            _boundRoot = root;
            _surface.Build(root, _config);
            _overlay = root.Q<VisualElement>("results");
            var panel = root.Q<VisualElement>("results-panel");
            _title = root.Q<Label>("results-title");
            _runTime = root.Q<Label>("run-time");
            _cakes = root.Q<Label>("cake-total");
            _goldenCakes = root.Q<Label>("golden-cake-total");
            _chases = root.Q<Label>("chase-count");
            _escapes = root.Q<Label>("chase-escapes");
            _chaseTime = root.Q<Label>("chase-time");
            _endReason = root.Q<Label>("end-reason");
            _restart = root.Q<Button>("restart-button");
            _returnTitle = root.Q<Button>("return-title-button");
            _metrics = root.Q<VisualElement>("results-metrics");
            _kicker = root.Q<Label>("results-kicker"); _hint = root.Q<Label>("restart-hint");
            _cause = root.Q<Label>("death-cause"); _killer = root.Q<Label>("killer");
            _grabs = root.Q<Label>("grabs-escaped"); _exitTime = root.Q<Label>("exit-to-escape");
            _depth = root.Q<Label>("depth-reached"); _seed = root.Q<Label>("run-seed"); _best = root.Q<Label>("best-depth");
            _seedError = root.Q<Label>("seed-error"); _seedInput = root.Q<TextField>("next-run-seed");
            if (_overlay == null || panel == null || _title == null || _runTime == null ||
                _cakes == null || _goldenCakes == null || _chases == null || _escapes == null ||
                _chaseTime == null || _endReason == null || _restart == null)
            {
                Debug.LogWarning("Results surface is missing required named elements.", this);
                _overlay = null;
                return;
            }
            root.pickingMode = PickingMode.Ignore;
            root.style.display = DisplayStyle.Flex;
            _restart.clicked += OnRestartClicked;
            _returnTitle.clicked += OnReturnTitleClicked;
            _seedInput.RegisterValueChangedCallback(OnSeedChanged);
            _overlay.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _overlay.RegisterCallback<GeometryChangedEvent>(OnOverlayGeometryChanged);
            Apply();
        }

        private void Apply()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _state == null) return;
            if (!ReferenceEquals(_boundRoot, _document.rootVisualElement)) { BindAndApply(); return; }
            if (_overlay == null) return;
            bool opening = _state.Visible && _overlay.style.display.value == DisplayStyle.None;
            _overlay.style.display = _state.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = _state.Title;
            _metrics.style.display = _state.NoFloor ? DisplayStyle.None : DisplayStyle.Flex;
            _returnTitle.style.display = _state.NoFloor ? DisplayStyle.Flex : DisplayStyle.None;
            _returnTitle.SetEnabled(_state.Visible && !_state.RestartIssued);
            _kicker.text = _state.NoFloor ? "THE NIGHT NEVER BEGAN" : "AFTER THE NIGHT";
            _hint.text = _state.NoFloor ? "RETRY WITH A NEW SEED OR RETURN TO TITLE" : "ENTER / SPACE  ·  RUN AGAIN";
            _runTime.text = _state.RunTime;
            _cakes.text = _state.Cakes;
            _goldenCakes.text = _state.GoldenCakes;
            _chases.text = _state.Chases;
            _escapes.text = _state.Escapes;
            _chaseTime.text = _state.ChaseTime;
            _endReason.text = _state.EndReason;
            _cause.text = _state.Cause; _killer.text = _state.Killer; _grabs.text = _state.GrabsEscaped;
            _exitTime.text = _state.ExitToEscape; _depth.text = _state.Depth; _seed.text = _state.Seed; _best.text = _state.BestDepth;
            _seedError.text = _state.SeedError; _seedInput.SetValueWithoutNotify(_state.NextSeedText);
            _seedInput.SetEnabled(!_state.RestartIssued);
            _restart.SetEnabled(_state.Visible && !_state.RestartIssued && _state.SeedValid);
            _surface.SetRestartLabel(_state.RestartIssued ? "PLEASE WAIT…" : _state.NoFloor ? "RETRY (NEW SEED)" : "RUN AGAIN");
            if (opening && !_state.RestartIssued) _restart.Focus();
        }

        private void OnOverlayGeometryChanged(GeometryChangedEvent evt)
        {
            if (_state != null && _state.Visible && !_state.RestartIssued &&
                _restart != null && _restart.panel != null && evt.newRect.width > 0f) _restart.Focus();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_returnTitle != null && ReferenceEquals(evt.target, _returnTitle)) return;
            if (_seedInput != null && (ReferenceEquals(evt.target, _seedInput) || _seedInput.Contains(evt.target as VisualElement))) return;
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space) return;
            OnRestartClicked();
            evt.StopImmediatePropagation();
        }

        private void OnSeedChanged(ChangeEvent<string> evt)
        {
            if (_state == null) return;
            _presenter.SetNextSeed(_state, evt.newValue); Apply();
        }

        private void OnRestartClicked()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled ||
                !ReferenceEquals(_boundRoot, _document.rootVisualElement) || _overlay == null ||
                _state == null || !_presenter.TryRestart(_state)) return;
            Apply();
            RestartWithSeedClicked?.Invoke(!_state.NoFloor && _state.UseFixedSeed, _state.NoFloor ? 0 : _state.NextSeed);
            RestartClicked?.Invoke();
        }

        private void OnReturnTitleClicked()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled ||
                !ReferenceEquals(_boundRoot, _document.rootVisualElement) || _overlay == null ||
                _state == null || !_presenter.TryReturnToTitle(_state)) return;
            Apply(); ReturnToTitleClicked?.Invoke();
        }

        private void HideAndUnbind()
        {
            if (_restart != null) _restart.clicked -= OnRestartClicked;
            if (_returnTitle != null) _returnTitle.clicked -= OnReturnTitleClicked;
            if (_seedInput != null) _seedInput.UnregisterValueChangedCallback(OnSeedChanged);
            if (_overlay != null)
            {
                _overlay.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                _overlay.UnregisterCallback<GeometryChangedEvent>(OnOverlayGeometryChanged);
            }
            if (_surface != null) _surface.Release();
            if (_boundRoot != null) _boundRoot.style.display = DisplayStyle.None;
            _boundRoot = null;
            _overlay = null;
            _restart = null;
            _returnTitle = null; _metrics = null; _kicker = _hint = null;
            _seedInput = null;
            _cause = _killer = _grabs = _exitTime = _depth = _seed = _best = _seedError = null;
            _title = _runTime = _cakes = _goldenCakes = _chases = _escapes = _chaseTime = _endReason = null;
        }
    }
}
