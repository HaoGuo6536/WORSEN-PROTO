// ============================================================================
// DebugOverlayManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides a stable entry point for the development overlay across scene
//   loads. Scene assembly explicitly initializes this service, then routing code
//   supplies primitive samples without accessing UI Toolkit or gameplay state here.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · DebugOverlay (Service system).
//   Owns and commands DebugOverlayDriver; all text and visual work stays in its stack.
//
// KEY RESPONSIBILITIES:
//   - Return the canonical persistent instance when another scene creates a duplicate.
//   - Resolve and inject the DriverConfig and own the Driver lifecycle.
//   - Forward run and player display commands without performing calculations.
//
// DEPENDENCIES:
//   - No other project systems. Receives only primitive status values from callers.
//
// USAGE NOTES:
//   - Persistent tier (§8), using DontDestroyOnLoad. Initialize is called explicitly
//     by scene assembly; callers must use its return value to survive duplicate scenes.
//   - Serialized config first, mirrored Resources fallback second, visible warning
//     if missing. TagArenaSceneSetup deterministically rebuilds this wiring.
//   - A duplicate never replaces the existing service's config or display state.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.DebugOverlay
{
    public sealed class DebugOverlayManager : MonoBehaviour
    {
        private const string ConfigPath = "ScriptableObjects/Presentation/DebugOverlay/DebugOverlayDriverConfig";

        [SerializeField] private DebugOverlayDriverConfig _config;
        [SerializeField] private DebugOverlayDriver _driver;
        private bool _initialized;

        public static DebugOverlayManager Instance { get; private set; }

        public DebugOverlayManager Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return Instance;
            }

            Instance = this;
            if (_initialized) return this;
            DontDestroyOnLoad(gameObject);
            if (_config == null) _config = Resources.Load<DebugOverlayDriverConfig>(ConfigPath);
            if (_driver == null) _driver = GetComponent<DebugOverlayDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<DebugOverlayDriver>();
            _initialized = true;

            if (_config == null)
            {
                Debug.LogWarning("Debug overlay config is missing. Run Worsen/Scenes/1 — Build TagArena to restore it.", this);
                return this;
            }

            _driver.Initialize(_config);
            _driver.enabled = isActiveAndEnabled;
            return this;
        }

        public void SetRunStatus(long tick, string phase)
        {
            if (_driver != null) _driver.SetRunStatus(tick, phase);
        }

        public void SetPlayerStatus(float speed, string state)
        {
            if (_driver != null) _driver.SetPlayerStatus(speed, state);
        }

        public void SetPlayerUnavailable()
        {
            if (_driver != null) _driver.SetPlayerUnavailable();
        }

        private void OnEnable()
        {
            if (_initialized && _driver != null) _driver.enabled = true;
        }

        private void OnDisable()
        {
            if (_initialized && _driver != null) _driver.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (_driver != null) _driver.Teardown();
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
