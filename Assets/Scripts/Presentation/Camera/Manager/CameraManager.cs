// ============================================================================
// CameraManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides the scene's explicit entry point for Camera feedback.
//   The Manager forwards facts to its own Driver without reading gameplay state or manipulating visuals.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Camera (Service system).
//
// KEY RESPONSIBILITIES:
//   - Initialize the serialized Driver and mirrored config fallback.
//   - Expose unshaken aim for routed flashlight sensing; forward world event shakes.
//   - Forward confirmed consumption and expose its configured duration for routed visual synchronization.
//   - Forward commands and pair Driver enable/disable and teardown.
//
// DEPENDENCIES:
//   - Core player movement and traversal facts only.
//
// USAGE NOTES:
//   - Scene-owned Service (§8), explicitly initialized by scene assembly; no singleton.
//   - AddComponent does not initialize or create runtime effects.
//   - Serialized references are primary; missing config or Driver produces a visible warning.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Camera
{
    public sealed class CameraManager : MonoBehaviour
    {
        private const string ConfigPath = "ScriptableObjects/Presentation/Camera/CameraDriverConfig";
        [SerializeField] private CameraDriverConfig _config;
        [SerializeField] private CameraDriver _driver;
        private bool _initialized;

        public Quaternion AimRotation => _driver != null ? _driver.AimRotation : Quaternion.identity;
        public float ConsumptionSeconds => _driver != null ? _driver.ConsumptionSeconds : 0f;
        public Vector3 AimPosition => _driver != null ? _driver.AimPosition : Vector3.zero;

        public bool IsReady => _initialized && _driver != null && _driver.IsReady;

        public CameraManager Initialize()
        {
            if (_initialized) return this;
            if (_config == null) _config = Resources.Load<CameraDriverConfig>(ConfigPath);
            if (_driver == null) _driver = GetComponent<CameraDriver>();
            if (_config == null || _driver == null)
            {
                Debug.LogWarning("Camera needs its Driver and config. Rebuild its system setup wiring.", this);
                return this;
            }
            _driver.Initialize(_config);
            _initialized = _driver.IsReady;
            _driver.enabled = isActiveAndEnabled;
            return this;
        }

        public void SetMovement(PlayerMovementSample sample) { if (_initialized) _driver.SetMovement(sample); }
        public void SetLookBack(bool held) { if (_initialized) _driver.SetLookBack(held); }
        public void PlayDetectionBeat() { if (_initialized) _driver.PlayDetectionBeat(); }
        public void PlayShake(float strength, float seconds) { if (_initialized) _driver.PlayShake(strength, seconds); }
        public void SetProximity(float closeness) { if (_initialized) _driver.SetProximity(closeness); }
        public void PlayTraversal(PlayerTraversalFact fact) { if (_initialized) _driver.PlayTraversal(fact); }
        public void PlayDeathSnap(Vector3 killerPosition) { if (_initialized) _driver.PlayDeathSnap(killerPosition); }
        public void PlayConsumed(Vector3 handPosition) { if (_initialized) _driver.PlayConsumed(handPosition); }
        public void ResetView() { if (_initialized) _driver.ResetView(); }

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
