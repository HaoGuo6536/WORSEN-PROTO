// ============================================================================
// PostFXManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides the scene's explicit entry point for PostFX feedback.
//   The Manager forwards facts to its own Driver without reading gameplay state or manipulating visuals.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · PostFX (Service system).
//
// KEY RESPONSIBILITIES:
//   - Initialize the serialized Driver and mirrored config fallback.
//   - Forward commands and pair Driver enable/disable and teardown.
//
// DEPENDENCIES:
//   - No other project systems; receives primitive effect facts.
//
// USAGE NOTES:
//   - Scene-owned Service (§8), explicitly initialized by scene assembly; no singleton.
//   - AddComponent does not initialize or create runtime effects.
//   - Serialized references are primary; missing config or Driver produces a visible warning.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXManager : MonoBehaviour
    {
        private const string ConfigPath = "ScriptableObjects/Presentation/PostFX/PostFXDriverConfig";
        [SerializeField] private PostFXDriverConfig _config;
        [SerializeField] private PostFXDriver _driver;
        private bool _initialized;

        public bool IsReady => _initialized && _driver != null && _driver.IsReady;

        public PostFXManager Initialize()
        {
            if (_initialized) return this;
            if (_config == null) _config = Resources.Load<PostFXDriverConfig>(ConfigPath);
            if (_driver == null) _driver = GetComponent<PostFXDriver>();
            if (_config == null || _driver == null)
            {
                Debug.LogWarning("PostFX needs its Driver and config. Rebuild its system setup wiring.", this);
                return this;
            }
            _driver.Initialize(_config);
            _initialized = _driver.IsReady;
            _driver.enabled = isActiveAndEnabled;
            return this;
        }

        public void SetProximity(float closeness) { if (_initialized) _driver.SetProximity(closeness); }
        public void SetLookBack(bool held) { if (_initialized) _driver.SetLookBack(held); }
        public void SetInjury(float currentHealth, float maxHealth) { if (_initialized) _driver.SetInjury(currentHealth, maxHealth); }
        public void PlayReacquireBlur() { if (_initialized) _driver.PlayReacquireBlur(); }
        public void PlayIntrusion(float seconds) { if (_initialized) _driver.PlayIntrusion(seconds); }
        public void ResetEffects() { if (_initialized) _driver.ResetEffects(); }

        public void Teardown()
        {
            if (_driver != null) _driver.Teardown();
            _initialized = false;
        }

        private void OnEnable()
        {
            if (_initialized && _driver != null) _driver.enabled = true;
        }

        private void OnDisable()
        {
            if (_initialized && _driver != null) _driver.enabled = false;
        }

        private void OnDestroy() => Teardown();
    }
}
