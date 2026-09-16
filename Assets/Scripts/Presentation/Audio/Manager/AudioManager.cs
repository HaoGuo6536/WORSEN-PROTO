// ============================================================================
// AudioManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides one persistent command surface for audible run feedback.
//   Explicit initialization returns the canonical service across repeated scene
//   creation, keeping event routing separate from source playback and mixer math.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Audio (Service system).
//   Owns AudioDriver and forwards Core facts into its presentation stack.
//
// KEY RESPONSIBILITIES:
//   - Establish exactly one persistent Audio service and retire duplicate roots.
//   - Forward cue, movement, fractional injury and proximity commands without gameplay rules.
//   - Pair owner enable/disable and teardown with the owned Driver lifetime.
//
// DEPENDENCIES:
//   - Core CueId and MovementState; Audio system's own Driver and DriverConfig.
//
// USAGE NOTES:
//   - Persistent (DontDestroyOnLoad); requires a dedicated root GameObject.
//   - Scene assembly must retain the canonical instance returned by Initialize.
//   - ResetRun releases all previous run playback; no scene-owned references are cached.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioDriver))]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioDriverConfig _config;
        [SerializeField] private AudioDriver _driver;
        private bool _initialized;
        public static AudioManager Instance { get; private set; }
        public float BreathGain => _driver != null ? _driver.BreathGain : 0f;
        public float HunterGain => _driver != null ? _driver.HunterGain : 0f;

        public AudioManager Initialize()
        {
            if (Instance != null && Instance != this)
            {
                AudioManager canonical = Instance;
                enabled = false;
                var duplicate = GetComponent<AudioDriver>();
                if (duplicate != null) { duplicate.Teardown(); duplicate.enabled = false; }
                Destroy(gameObject);
                return canonical;
            }
            Instance = this;
            if (_initialized) return this;
            DontDestroyOnLoad(gameObject);
            if (_driver == null) _driver = GetComponent<AudioDriver>();
            _driver.Initialize(_config);
            _initialized = _driver.IsInitialized;
            if (_initialized) _driver.SetOwnerEnabled(isActiveAndEnabled);
            return this;
        }

        public void PlayCue(CueId cue) { if (_initialized && isActiveAndEnabled) _driver.PlayCue(cue); }
        public void SetProximity(float closeness) { if (_initialized) _driver.SetProximity(closeness); }
        public void SetMovementState(MovementState movement) { if (_initialized) _driver.SetMovementState(movement); }
        public void SetSpeedNormalized(float speed) { if (_initialized) _driver.SetSpeedNormalized(speed); }
        public void SetInjury(float currentHealth, float maxHealth) { if (_initialized) _driver.SetInjury(currentHealth, maxHealth); }
        public void ResetRun() { if (_initialized) _driver.ResetRun(); }

        private void OnEnable() { if (_initialized) _driver.SetOwnerEnabled(true); }
        private void OnDisable() { if (_initialized && _driver != null) _driver.SetOwnerEnabled(false); }
        private void OnDestroy()
        {
            if (_initialized && _driver != null) _driver.Teardown();
            if (Instance == this) Instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
