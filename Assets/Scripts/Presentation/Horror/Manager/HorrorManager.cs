// ============================================================================
// HorrorManager.cs
// ============================================================================
//
// PURPOSE:
//   Exposes the dark-room flashlight and attack warning command surface.
//   It forwards already-decided presentation facts and leaves world rendering and sound to its Driver.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Horror (Service system).
//
// KEY RESPONSIBILITIES:
//   - Forward authoritative aim and afterimage facts from gameplay without taking ownership.
//   - Initialize the scene-owned Driver and forward commands.
//   - Pair owner enable, disable and destruction with rendering restoration.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and EntityId; its own Horror presentation stack.
//
// USAGE NOTES:
//   Scene-owned; exactly one service per assembled gameplay scene, with no singleton.
//   The scene setup explicitly wires the Driver's output camera, fog Volume and optional daylights.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Horror
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HorrorDriver))]
    public sealed class HorrorManager : MonoBehaviour
    {
        [SerializeField] private HorrorDriverConfig _config;
        [SerializeField] private HorrorDriver _driver;
        public bool IsReady => _driver != null && _driver.IsReady;
        public bool FlashlightEnabled => _driver != null && _driver.FlashlightEnabled;
        public float FogCurveStart => _driver != null ? _driver.FogCurveStart : 0f;
        public float FogCurveEnd => _driver != null ? _driver.FogCurveEnd : 0f;
        public float FlashlightRange => _driver != null ? _driver.FlashlightRange : 0f;

        private void Awake() { if (_driver == null) _driver = GetComponent<HorrorDriver>(); }

        public HorrorManager Initialize(HorrorDriverConfig config)
        {
            if (_driver == null) _driver = GetComponent<HorrorDriver>();
            if (config != null) _config = config;
            _driver.Initialize(_config);
            _driver.SetOwnerEnabled(isActiveAndEnabled);
            return this;
        }

        public void SetFlashlight(FlashlightSample sample) { if (_driver != null) _driver.SetFlashlight(sample); }
        public void SetAfterimage(FlashlightSample sample, float lifetime) { if (_driver != null) _driver.SetAfterimage(sample, lifetime); }
        public void ToggleFlashlight() { if (_driver != null && isActiveAndEnabled) _driver.ToggleFlashlight(); }
        public void SetEffects(float fogMultiplier, float flashlightMultiplier)
        { if (_driver != null) _driver.SetEffects(fogMultiplier, flashlightMultiplier); }
        public void ResetRound() { if (_driver != null) _driver.ResetRound(); }
        public void SetAttack(HunterAttackSample sample)
        { if (_driver != null && isActiveAndEnabled) _driver.SetAttack(sample); }
        public void RemoveAttack(EntityId hunter) { if (_driver != null) _driver.RemoveAttack(hunter); }

        private void OnEnable() { if (_driver != null) _driver.SetOwnerEnabled(true); }
        private void OnDisable() { if (_driver != null) _driver.SetOwnerEnabled(false); }
        private void OnDestroy() { if (_driver != null) _driver.Teardown(); }
    }
}
