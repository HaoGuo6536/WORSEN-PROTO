// ============================================================================
// MenuOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects the title and preference surface to explicit session owners. Starting
//   a run is a user decision, and saving preferences reports the actual store result.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Menu target.
// KEY RESPONSIBILITIES:
//   - Route a debounced title start to the scene's configured start callback.
//   - Round-trip settings and save acknowledgements through the Settings service.
//   - Route quit to the Menu Manager's owned application-exit Driver.
//   - Persist history at the committed expedition end, never at each floor escape.
//   - Forward the independent input pause action to menu intent admission.
// DEPENDENCIES:
//   Presentation Menu/Input, Session Settings/Progression, Core records and a scene callback.
// USAGE NOTES:
//   Scene-owned. Configure pairs old subscriptions before binding canonical services.
//   Pause and runtime preference consumers require the separately owned state wiring.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Menu;
using Worsen.Presentation.Input;
using Worsen.Session.Settings;
using Worsen.Session.Progression;
namespace Worsen.Orchestrator
{
    public sealed class MenuOrchestrator : MonoBehaviour
    {
        private MenuManager _menu;
        private SettingsManager _settings;
        private ProgressionSessionManager _progression;
        private InputManager _input;
        private Action _startRun;

        public void Configure(MenuManager menu, SettingsManager settings, Action startRun,
            ProgressionSessionManager progression = null, InputManager input = null)
        {
            OnDisable(); _menu = menu; _settings = settings; _startRun = startRun;
            _progression = progression;
            _input = input;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_menu == null || _settings == null) return;
            OnDisable();
            _menu.StartClicked += OnStart;
            _menu.QuitClicked += OnQuit;
            _menu.SettingsApplied += OnSettingsApplied;
            _settings.SettingsChanged += OnSettingsChanged;
            _settings.SaveCompleted += OnSaveCompleted;
            if (_input != null) _input.PausePressed += OnPausePressed;
            if (_progression != null) _progression.TransactionCommitted += OnProgressionTransaction;
        }
        private void OnDisable()
        {
            if (_menu != null)
            {
                _menu.StartClicked -= OnStart;
                _menu.QuitClicked -= OnQuit;
                _menu.SettingsApplied -= OnSettingsApplied;
            }
            if (_progression != null) _progression.TransactionCommitted -= OnProgressionTransaction;
            if (_input != null) _input.PausePressed -= OnPausePressed;
            if (_settings == null) return;
            _settings.SettingsChanged -= OnSettingsChanged;
            _settings.SaveCompleted -= OnSaveCompleted;
        }
        private void OnStart()
        { _startRun?.Invoke(); _menu.SetRunState(false, false); }
        private void OnQuit() => _menu.QuitApplication();
        private void OnPausePressed() => _menu.TogglePause();
        private void OnSettingsApplied(PlayerSettingsRecord settings) => _settings.ApplySettings(settings);
        private void OnSettingsChanged(PlayerSettingsRecord settings) => _menu.SetSettings(settings);
        private void OnSaveCompleted(bool saved, string message) => _menu.SetSaveResult(saved, message);
        private void OnProgressionTransaction(ProgressionSnapshot before, ProgressionSnapshot after, string operation, string choice)
        {
            if (before.Phase != ProgressionPhase.Ended && after.Phase == ProgressionPhase.Ended)
                _settings.RecordRunEnded(after.Round);
        }
    }
}
