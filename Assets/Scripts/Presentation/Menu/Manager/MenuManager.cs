// ============================================================================
// MenuManager.cs
// ============================================================================
// PURPOSE:
//   Exposes the scene-owned title and pause surface to explicit composition routing.
//   It republishes clicks and settings records without controlling gameplay or files.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Menu (Service system).
// KEY RESPONSIBILITIES:
//   - Own Driver initialization and paired interaction subscriptions.
//   - Forward authoritative pause acknowledgements and sanitized preference snapshots.
//   - Forward explicit application-exit commands to the owned engine boundary.
// DEPENDENCIES:
//   Core records and own Menu Driver/Config only; no sibling presentation systems.
// USAGE NOTES:
//   Scene-owned tier (§8). Create inactive, wire config/document, then activate.
//   Escape/input, cursor and quit are coordinator-owned routes, not hidden side effects.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.Menu
{
    public sealed class MenuManager : MonoBehaviour
    {
        [SerializeField] private MenuDriverConfig _config;
        [SerializeField] private MenuDriver _driver;
        private bool _initialized;
        public event Action StartClicked, QuitClicked;
        public event Action<bool> PauseSelected;
        public event Action<PlayerSettingsRecord> SettingsApplied;
        public void Initialize(MenuDriverConfig config = null)
        {
            if (_initialized) return;
            if (config != null) _config = config;
            if (_config == null) _config = Resources.Load<MenuDriverConfig>("ScriptableObjects/Presentation/Menu/MenuDriverConfig");
            if (_driver == null) _driver = GetComponent<MenuDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<MenuDriver>();
            _driver.Initialize(_config); _driver.enabled = isActiveAndEnabled;
            _initialized = _config != null;
        }
        public void ShowTitle() => _driver?.ShowTitle();
        public void QuitApplication() => _driver?.QuitApplication();
        public void SetRunState(bool canPause, bool paused) => _driver?.SetRunState(canPause, paused);
        public void SetSettings(PlayerSettingsRecord value) => _driver?.SetSettings(value);
        public void SetSaveResult(bool saved, string message) => _driver?.SetSaveResult(saved, message);
        public void TogglePause() => _driver?.TogglePause();
        private void OnEnable()
        {
            Initialize();
            OnDisable();
            _driver.enabled = true;
            _driver.StartClicked += OnStart; _driver.QuitClicked += OnQuit;
            _driver.PauseSelected += OnPause; _driver.SettingsApplied += OnSettings;
        }
        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.StartClicked -= OnStart; _driver.QuitClicked -= OnQuit;
            _driver.PauseSelected -= OnPause; _driver.SettingsApplied -= OnSettings;
            _driver.enabled = false;
        }
        private void OnDestroy() => _driver?.Teardown();
        private void OnStart() => StartClicked?.Invoke();
        private void OnQuit() => QuitClicked?.Invoke();
        private void OnPause(bool pause) => PauseSelected?.Invoke(pause);
        private void OnSettings(PlayerSettingsRecord value) => SettingsApplied?.Invoke(value);
    }
}
