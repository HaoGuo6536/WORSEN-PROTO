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
//   - Forward independent health/heartbeat, cleanse, revival, grace and catch commands.
//   - Expose Blind trap duration and forward injected effects and hunter-rim strength.
//   - Forward runtime blur preferences without editing designer configuration.
//   - Initialize and pair the owned Driver lifetime with mirrored config fallback.
//   - Forward budgeted intrusion and timed blindness hooks.
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
        public void SetHunterRim(float strength) { if (_initialized) _driver.SetHunterRim(strength); }
        public void SetInjury(float currentHealth, float maxHealth) { if (_initialized) _driver.SetInjury(currentHealth, maxHealth); }
        public void SetHeartbeatEnvelope(float strength) { if (_initialized) _driver.SetHeartbeatEnvelope(strength); }
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
