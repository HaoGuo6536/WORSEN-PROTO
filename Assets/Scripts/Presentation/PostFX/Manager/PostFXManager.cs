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
//   - Forward targeted sensory cleansing and revival without resetting unrelated feedback.
//   - Expose the configured Blind trap duration to the routing boundary.
//   - Forward Run grace boundaries and the injected active-effects view.
//   - Forward runtime blur enablement without changing the designer config.
//   - Initialize the serialized Driver and mirrored config fallback.
//   - Forward confirmed consumption with a duration supplied by the coordinator.
//   - Forward budgeted intrusion and blindness hooks; pair Driver lifetime.
//
// DEPENDENCIES:
//   - Core read-only effects and grace facts; no gameplay implementation references.
//
// USAGE NOTES:
//   - Scene-owned Service (§8), explicitly initialized by scene assembly; no singleton.
//   - AddComponent does not initialize or create runtime effects.
//   - Serialized references are primary; missing config or Driver produces a visible warning.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXManager : MonoBehaviour
    {
        private const string ConfigPath = "ScriptableObjects/Presentation/PostFX/PostFXDriverConfig";
        [SerializeField] private PostFXDriverConfig _config;
        [SerializeField] private PostFXDriver _driver;
        private bool _initialized;

        public bool IsReady => _initialized && _driver != null && _driver.IsReady;
        public float BlindTrapSeconds => _config != null ? _config.BlindTrapSeconds : 0f;

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
        public void SetReacquireBlurEnabled(bool enabled) { if (_initialized) _driver.SetReacquireBlurEnabled(enabled); }
        public void PlayIntrusion(float seconds) { if (_initialized) _driver.PlayIntrusion(seconds); }
        public void PlayIntrusion(float seconds, bool startle) { if (_initialized) _driver.PlayIntrusion(seconds, startle); }
        public void SetBlindness(float seconds) { if (_initialized) _driver.SetBlindness(seconds); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { if (_initialized) _driver.SetActiveEffects(effects); }
        public void SetGrace(GraceWindowFact fact, bool active) { if (_initialized) _driver.SetGrace(fact, active); }
        public void PlayConsumed(float seconds) { if (_initialized) _driver.PlayConsumed(seconds); }
        public void ClearConsumed() { if (_initialized) _driver.ClearConsumed(); }
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
