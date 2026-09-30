// ============================================================================
// MenuDriver.cs
// ============================================================================
// PURPOSE:
//   Builds a title/headphones overlay and a pause/settings menu in the current scene.
//   It reports user intent as Core values and waits for the simulation owner's response.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Menu.
// KEY RESPONSIBILITIES:
//   - Freeze scaled engine work only after Session acknowledges pause; restore on teardown.
//   - Own the UI Toolkit tree, callbacks and rebind-safe presentation state.
//   - Expose all player preferences and an explicit save/apply interaction.
//   - Execute an explicitly routed quit, including clean editor Play Mode exit.
// DEPENDENCIES:
//   Core records, own Menu stack and Unity UI Toolkit only.
// USAGE NOTES:
//   Scene-owned through MenuManager with its own DriverConfig. QuitApplication owns
//   application exit only when commanded; clicks alone never quit or pause gameplay.
//   Owns Time.timeScale during acknowledged pause only, restoring the prior value.
//   Ownership is exclusive across scenes; invalid or paused baselines restore to 1.
//   Disable/destroy/teardown release ownership, including scene unload and editor exit.
//   Requires a wired UIDocument PanelSettings; missing wiring is logged, not fabricated.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
namespace Worsen.Presentation.Menu
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuDriver : MonoBehaviour
    {
        private UIDocument _document;
        private MenuDriverConfig _config;
        private readonly MenuDriverState _state = new MenuDriverState();
        private readonly MenuPresenter _presenter = new MenuPresenter();
        private VisualElement _root, _settings;
        private Label _title, _headphones, _message;
        private Button _start, _resume, _apply, _quit;
        private FloatField _sensitivity;
        private Slider _fov, _master, _music, _effects;
        private Toggle _invert, _tilt, _punch, _blur;
        public event Action StartClicked, QuitClicked;
        public event Action<bool> PauseSelected;
        public event Action<PlayerSettingsRecord> SettingsApplied;
        public void Initialize(MenuDriverConfig config)
        {
            _config = config; _document = GetComponent<UIDocument>();
            if (_config == null || _document.panelSettings == null)
            { Debug.LogWarning("Menu requires MenuDriverConfig and UIDocument PanelSettings.", this); return; }
            Bind();
        }
        public void ShowTitle() { SetEnginePaused(false); _presenter.ShowTitle(_state); Apply(); }
        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void SetRunState(bool canPause, bool paused)
        { _presenter.SetRunState(_state, canPause, paused); SetEnginePaused(_state.Paused); Apply(); }
        private void SetEnginePaused(bool paused)
        {
            if (paused && !_state.OwnsTimeScale)
            {
                ReleasePauseTimeScale();
                _state.PreviousTimeScale = ValidTimeScale(Time.timeScale);
                MenuDriverState.TimeScaleOwner = _state;
                _state.OwnsTimeScale = true;
                Time.timeScale = 0f;
            }
            else if (!paused && _state.OwnsTimeScale)
            {
                if (ReferenceEquals(MenuDriverState.TimeScaleOwner, _state)) ReleasePauseTimeScale();
                _state.OwnsTimeScale = false;
            }
        }
        public static bool ReleasePauseTimeScale()
        {
            var owner = MenuDriverState.TimeScaleOwner;
            if (owner == null) return false;
            MenuDriverState.TimeScaleOwner = null;
            owner.OwnsTimeScale = false;
            Time.timeScale = ValidTimeScale(owner.PreviousTimeScale);
            return true;
        }
        private static float ValidTimeScale(float value) => value > 0f && !float.IsInfinity(value) ? value : 1f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPauseOwnership() => ReleasePauseTimeScale();
        public void SetSettings(PlayerSettingsRecord value) { _presenter.SetSettings(_state, value); Apply(); }
        public void SetSaveResult(bool saved, string message) { _presenter.SetSaveResult(_state, saved, message); Apply(); }
        public void TogglePause()
        {
            if (!_presenter.TryTogglePause(_state, out bool pause)) return;
            Apply(); PauseSelected?.Invoke(pause);
        }
        private void OnStart()
        {
            if (!_presenter.TryStart(_state)) return;
            Apply(); StartClicked?.Invoke();
        }
        private void OnApply()
        {
            if (!_presenter.TryApply(_state, out var value)) return;
            Apply(); SettingsApplied?.Invoke(value);
        }
        private void OnQuit() { if (_state.TitleVisible || _state.Paused) QuitClicked?.Invoke(); }
        private void OnFloat(ChangeEvent<float> evt) => ReadDraft();
        private void OnBool(ChangeEvent<bool> evt) => ReadDraft();
        private void ReadDraft()
        {
            var value = new PlayerSettingsRecord(1, _sensitivity.value, _invert.value, _fov.value,
                _tilt.value, _punch.value, _blur.value, _master.value, _music.value, _effects.value);
            if (_presenter.EditSettings(_state, value)) Apply();
        }
        private void OnEnable() { if (_config != null) Bind(); }
        private void OnDisable() { SetEnginePaused(false); Unbind(); }
        private void OnDestroy() => Teardown();
        private void LateUpdate()
        {
            if (_document == null || _config == null) return;
            if (!_document.isActiveAndEnabled) { Unbind(); return; }
            if (!ReferenceEquals(_root, _document.rootVisualElement)) Bind();
        }
        private void Bind()
        {
            if (!isActiveAndEnabled || _document == null || !_document.isActiveAndEnabled || _document.panelSettings == null) return;
            Unbind(); _root = _document.rootVisualElement;
            if (_root == null) return;
            _root.Clear(); _root.pickingMode = PickingMode.Position;
            _root.style.position = Position.Absolute;
            _root.style.left = _root.style.right = _root.style.top = _root.style.bottom = 0;
            _root.style.alignItems = Align.Center; _root.style.justifyContent = Justify.Center;
            _root.style.backgroundColor = _config.Backdrop; _root.style.color = _config.Text;
            _root.style.fontSize = _config.FontSize;
            var body = new ScrollView { name = "menu-body" };
            body.style.width = _config.Width; body.style.maxWidth = Length.Percent(100);
            body.style.maxHeight = Length.Percent(100); body.style.flexShrink = 1;
            body.style.paddingLeft = body.style.paddingRight = _config.Padding;
            body.style.paddingTop = body.style.paddingBottom = _config.Padding; _root.Add(body);
            _title = new Label { name = "menu-title", enableRichText = false }; body.Add(_title);
            _headphones = new Label("Wear headphones.") { name = "headphones", enableRichText = false }; body.Add(_headphones);
            _start = Button(body, "start-run", "START RUN"); _start.clicked += OnStart;
            _resume = Button(body, "resume-run", "RESUME"); _resume.clicked += TogglePause;
            _settings = new VisualElement { name = "player-settings" }; body.Add(_settings);
            _sensitivity = new FloatField("Mouse sensitivity (degrees / pixel)") { name = "sensitivity" };
            _settings.Add(_sensitivity); _sensitivity.RegisterValueChangedCallback(OnFloat);
            _invert = Toggle("Invert Y", "invert-y");
            _fov = Slider("Horizontal field of view", "field-of-view", 1, 179);
            _tilt = Toggle("Camera tilt", "camera-tilt");
            _punch = Toggle("Camera punch", "camera-punch");
            _blur = Toggle("Reacquire blur", "reacquire-blur");
            _master = Slider("Master volume", "master-volume", 0, 1);
            _music = Slider("Music volume", "music-volume", 0, 1);
            _effects = Slider("Effects volume", "effects-volume", 0, 1);
            _apply = Button(_settings, "apply-settings", "APPLY AND SAVE"); _apply.clicked += OnApply;
            _message = new Label { name = "settings-feedback", enableRichText = false }; _message.style.whiteSpace = WhiteSpace.Normal;
            _settings.Add(_message);
            _quit = Button(body, "quit-game", "QUIT"); _quit.clicked += OnQuit;
            Apply();
        }
        private Button Button(VisualElement parent, string name, string text)
        {
            var button = new Button { name = name, text = text };
            button.style.minHeight = _config.ButtonHeight; parent.Add(button); return button;
        }
        private Toggle Toggle(string label, string name)
        {
            var control = new Toggle(label) { name = name }; _settings.Add(control);
            control.RegisterValueChangedCallback(OnBool); return control;
        }
        private Slider Slider(string label, string name, float min, float max)
        {
            var control = new Slider(label, min, max) { name = name, showInputField = true }; _settings.Add(control);
            control.RegisterValueChangedCallback(OnFloat); return control;
        }
        private void Apply()
        {
            if (_root == null) return;
            bool opening = _root.style.display.value == DisplayStyle.None;
            _root.style.display = _state.TitleVisible || _state.Paused ? DisplayStyle.Flex : DisplayStyle.None;
            _title.text = _state.TitleVisible ? "WORSEN" : "PAUSED";
            _headphones.style.display = _start.style.display = _state.TitleVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _resume.style.display = _settings.style.display = _state.Paused ? DisplayStyle.Flex : DisplayStyle.None;
            _start.SetEnabled(!_state.Pending); _resume.SetEnabled(!_state.Pending);
            _settings.SetEnabled(_state.SettingsReady && !_state.Pending);
            var d = _state.Draft;
            _sensitivity.SetValueWithoutNotify(d.MouseSensitivity); _invert.SetValueWithoutNotify(d.InvertY);
            _fov.SetValueWithoutNotify(d.FieldOfView); _tilt.SetValueWithoutNotify(d.CameraTilt);
            _punch.SetValueWithoutNotify(d.CameraPunch); _blur.SetValueWithoutNotify(d.ReacquireBlur);
            _master.SetValueWithoutNotify(d.MasterVolume); _music.SetValueWithoutNotify(d.MusicVolume);
            _effects.SetValueWithoutNotify(d.EffectsVolume); _message.text = _state.Message;
            if (opening) { if (_state.TitleVisible) _start.Focus(); else if (_state.Paused) _resume.Focus(); }
        }
        public void Teardown() { SetEnginePaused(false); Unbind(); }
        private void Unbind()
        {
            if (_start != null) _start.clicked -= OnStart;
            if (_resume != null) _resume.clicked -= TogglePause;
            if (_apply != null) _apply.clicked -= OnApply;
            if (_quit != null) _quit.clicked -= OnQuit;
            if (_sensitivity != null) _sensitivity.UnregisterValueChangedCallback(OnFloat);
            foreach (var control in new[] { _fov, _master, _music, _effects }) control?.UnregisterValueChangedCallback(OnFloat);
            foreach (var control in new[] { _invert, _tilt, _punch, _blur }) control?.UnregisterValueChangedCallback(OnBool);
            if (_root != null) { _root.style.display = DisplayStyle.None; _root.Clear(); }
            _root = _settings = null; _start = _resume = _apply = _quit = null;
            _title = _headphones = _message = null; _sensitivity = null;
            _fov = _master = _music = _effects = null; _invert = _tilt = _punch = _blur = null;
        }
    }
}
