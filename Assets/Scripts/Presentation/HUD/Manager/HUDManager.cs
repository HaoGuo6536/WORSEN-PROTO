// ============================================================================
// HUDManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides the scene-owned entry point for run progress and chase HUD feedback.
//   It owns the HUD Driver lifecycle and forwards supplied facts without querying
//   gameplay systems or manipulating visual elements.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · HUD (Service system).
//
// KEY RESPONSIBILITIES:
//   - Resolve owned references, initialize once, and pair enable/disable lifecycle.
//   - Forward counts, exit state, world direction, heading, slots and chase facts.
//   - Reset transient chase presentation at an explicitly routed new-run boundary.
//   - Forward explicitly routed camera orientation to the objective compass.
//
// DEPENDENCIES:
//   - Worsen.Core ExitState; no Domain, Session or sibling Presentation systems.
//
// USAGE NOTES:
//   - Scene-owned tier (§8); no singleton and no persistence across scene loads.
//   - Awake performs only internal initialization; scene assembly may call Initialize explicitly.
//   - Serialized config first, mirrored Resources fallback second; HUDSetup restores wiring.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDManager : MonoBehaviour
    {
        [SerializeField] private HUDDriverConfig _config;
        [SerializeField] private HUDDriver _driver;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            if (_config == null) _config = Resources.Load<HUDDriverConfig>("ScriptableObjects/Presentation/HUD/HUDDriverConfig");
            if (_driver == null) _driver = GetComponent<HUDDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<HUDDriver>();
            _driver.Initialize(_config);
            _driver.enabled = isActiveAndEnabled;
            _initialized = _config != null;
        }

        public void SetCount(int collected, int total) { if (_driver != null) _driver.SetCount(collected, total); }
        public void SetExitState(ExitState exitState) { if (_driver != null) _driver.SetExitState(exitState); }
        public void SetDirection(Vector3 worldDirection, bool visible) { if (_driver != null) _driver.SetDirection(worldDirection, visible); }
        public void SetHeading(float headingDegrees) { if (_driver != null) _driver.SetHeading(headingDegrees); }
        public void SetViewRotation(Quaternion rotation) { if (_driver != null) _driver.SetViewRotation(rotation); }
        public void SetItemSlots(int emptySlotCount) { if (_driver != null) _driver.SetItemSlots(emptySlotCount); }
        public void SetChaseMode(bool chasing) { if (_driver != null) _driver.SetChaseMode(chasing); }

        public void ResetRunView() { if (_driver != null) _driver.ResetRunView(); }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            Initialize();
            if (_driver == null) return;
            _driver.enabled = true;
        }

        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.enabled = false;
        }

        private void OnDestroy()
        {
            if (_driver != null) _driver.Teardown();
        }
    }
}

