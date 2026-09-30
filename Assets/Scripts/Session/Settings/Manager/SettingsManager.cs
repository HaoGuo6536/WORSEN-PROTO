// ============================================================================
// SettingsManager.cs
// ============================================================================
// PURPOSE:
//   Owns player preferences and run history for the lifetime of the application.
//   It publishes sanitized Core snapshots for explicit routing to runtime consumers.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Session · Settings (Session system).
// KEY RESPONSIBILITIES:
//   - Sequence load, pure validation, save and snapshot publication through owned stacks.
//   - Report failed saves rather than claiming the active runtime overrides are persisted.
//   - Persist one completed expedition's lifetime count and best depth together.
// DEPENDENCIES:
//   Core persistence records; own Config, Controller, BehaviorState and Driver only.
// USAGE NOTES:
//   Persistent tier (§8), one root service explicitly initialized before menu display.
//   No scene references. Coordinator routes SettingsChanged, never writes config assets.
//   RecordRunStarted is one expedition start, not a generated-floor capture event.
//   RecordRunEnded is an alternative counting boundary; a route must use one, not both.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Session.Settings
{
    public sealed class SettingsManager : MonoBehaviour
    {
        [SerializeField] private SettingsConfig _config;
        [SerializeField] private SettingsDriver _driver;
        private SettingsBehaviorState _state;
        private SettingsController _controller;
        public static SettingsManager Instance { get; private set; }
        public event Action<PlayerSettingsRecord> SettingsChanged;
        public event Action<RunHistoryRecord> HistoryChanged;
        public event Action<bool, string> SaveCompleted;
        public PlayerSettingsRecord Current => _state.Settings;
        public RunHistoryRecord History => _state.History;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
        public bool Initialize(SettingsConfig config = null)
        {
            if (_controller != null) return true;
            if (Instance != null && Instance != this) return false;
            if (config != null) _config = config;
            if (_config == null) _config = Resources.Load<SettingsConfig>("ScriptableObjects/Session/Settings/SettingsConfig");
            if (_config == null) { Debug.LogWarning("SettingsConfig is missing; restore the Settings service.", this); return false; }
            if (_driver == null) _driver = GetComponent<SettingsDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<SettingsDriver>();
            _state = new SettingsBehaviorState();
            _controller = new SettingsController(_state, _config.Defaults);
            _controller.Apply(_driver.LoadSettings(_config.Defaults));
            _controller.RestoreHistory(_driver.LoadHistory());
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return true;
        }
        public void PublishCurrent()
        {
            if (_controller == null) return;
            SettingsChanged?.Invoke(Current); HistoryChanged?.Invoke(History);
        }
        public bool ApplySettings(PlayerSettingsRecord value)
        {
            if (_controller == null || !_controller.Apply(value)) return false;
            SettingsChanged?.Invoke(Current);
            bool saved = _driver.SaveSettings(Current);
            SaveCompleted?.Invoke(saved, saved ? "Settings saved." : _driver.LastError);
            return saved;
        }
        public void RecordRunStarted()
        {
            if (_controller == null) return;
            _controller.RecordRunStarted(); SaveHistory();
        }
        public void RecordBestDepth(int depth)
        {
            if (_controller != null && _controller.RecordBestDepth(depth)) SaveHistory();
        }
        public void RecordRunEnded(int depth)
        {
            if (_controller == null) return;
            _controller.RecordRunStarted();
            _controller.RecordBestDepth(depth);
            SaveHistory();
        }
        private void SaveHistory()
        {
            bool saved = _driver.SaveHistory(History);
            HistoryChanged?.Invoke(History);
            SaveCompleted?.Invoke(saved, saved ? "History saved." : _driver.LastError);
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SettingsChanged = null; HistoryChanged = null; SaveCompleted = null;
        }
    }
}
