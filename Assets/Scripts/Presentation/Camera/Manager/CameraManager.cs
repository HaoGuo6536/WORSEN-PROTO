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
//   - Forward traversal, comfort, lens and shake inputs without inferring gameplay.
//   - Initialize the serialized Driver and mirrored config fallback.
//   - Expose unshaken aim for routed flashlight sensing.
//   - Forward hunter/hand catches and republish the shared sting/completion facts.
//   - Pair Driver subscriptions on initialize, disable and explicit teardown.
//
// DEPENDENCIES:
//   - Core player movement, traversal and runtime preference facts only.
//
// USAGE NOTES:
//   - Scene-owned Service (§8), explicitly initialized by scene assembly; no singleton.
//   - AddComponent does not initialize or create runtime effects.
//   - Serialized references are primary; missing config or Driver produces a visible warning.
//   - ConsumptionSeconds is a legacy duration estimate; hold events are the authoritative cut/sting boundary.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Camera
{
    public sealed class CameraManager : MonoBehaviour
    {
        private const string ConfigPath = "ScriptableObjects/Presentation/Camera/CameraDriverConfig";
        [SerializeField] private CameraDriverConfig _config;
        [SerializeField] private CameraDriver _driver;
        private bool _initialized;

        public event Action<EntityId> CatchHoldStarted;
        public event Action<EntityId> CatchHoldEnded;

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
            if (isActiveAndEnabled) OnEnable();
            return this;
        }

        public void SetMovement(PlayerMovementSample sample) { if (_initialized) _driver.SetMovement(sample); }
        public void ApplySettings(PlayerSettingsRecord settings) { if (_initialized) _driver.ApplySettings(settings); }
        public void SetLookBack(bool held) { if (_initialized) _driver.SetLookBack(held); }
        public void PlayDetectionBeat() { if (_initialized) _driver.PlayDetectionBeat(); }
        public void PlayShake(float strength, float seconds) { if (_initialized) _driver.PlayShake(strength, seconds); }
        public void SetProximity(float closeness) { if (_initialized) _driver.SetProximity(closeness); }
        public void PlayTraversal(PlayerTraversalFact fact) { if (_initialized) _driver.PlayTraversal(fact); }
        public void PlayTraversal(PlayerTraversalFact fact, float landingSeverity) { if (_initialized) _driver.PlayTraversal(fact, landingSeverity); }
        public void SetTraversalProgress(EntityId id, long tick, TraversalKind kind, float progress, bool active)
        { if (_initialized) _driver.SetTraversalProgress(id, tick, kind, progress, active); }
        public void PlayStumble(EntityId id, long tick, float seconds) { if (_initialized) _driver.PlayStumble(id, tick, seconds); }
        public void PlayDeathSnap(Vector3 killerPosition) { if (_initialized) _driver.PlayDeathSnap(killerPosition); }
        public void PlayConsumed(Vector3 handPosition) { if (_initialized) _driver.PlayConsumed(handPosition); }
        public void ResetView() { if (_initialized) _driver.ResetView(); }

        public void Teardown()
        {
            OnDisable();
            if (_driver != null) _driver.Teardown();
            _initialized = false;
        }

        private void OnEnable()
        {
            if (_driver == null) return;
            // Initialize can follow OnEnable; rebinding is idempotent for either ordering.
            _driver.CatchHoldStarted -= OnCatchHoldStarted;
            _driver.CatchHoldEnded -= OnCatchHoldEnded;
            _driver.CatchHoldStarted += OnCatchHoldStarted;
            _driver.CatchHoldEnded += OnCatchHoldEnded;
            if (_initialized && _driver != null) _driver.enabled = true;
        }

        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.CatchHoldStarted -= OnCatchHoldStarted;
            _driver.CatchHoldEnded -= OnCatchHoldEnded;
            if (_initialized && _driver != null) _driver.enabled = false;
        }

        private void OnCatchHoldStarted(EntityId player) => CatchHoldStarted?.Invoke(player);
        private void OnCatchHoldEnded(EntityId player) => CatchHoldEnded?.Invoke(player);

        private void OnDestroy() => Teardown();
    }
}
