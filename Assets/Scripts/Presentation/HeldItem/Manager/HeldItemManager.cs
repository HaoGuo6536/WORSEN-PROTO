// ============================================================================
// HeldItemManager.cs
// ============================================================================
// PURPOSE:
//   Provides a scene-owned entry point for the selected consumable view model.
//   The scene assembler injects the output camera and config; routing supplies
//   inventory and modal/death visibility without exposing Session to Presentation.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · HeldItem (Service system).
// KEY RESPONSIBILITIES:
//   - Own and initialize its Driver against an explicitly supplied output camera.
//   - Forward immutable inventory and visibility commands without gameplay queries.
//   - Pair enable/disable and explicit teardown with the Driver lifetime.
// DEPENDENCIES:
//   Core inventory snapshots and own Driver/DriverConfig only.
// USAGE NOTES:
//   Scene-owned tier (§8), no singleton. Initialize after the output camera exists.
//   A SceneRoot creates this Manager; an owning routing component supplies snapshots.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.HeldItem
{
    public sealed class HeldItemManager : MonoBehaviour
    {
        [SerializeField] private HeldItemDriverConfig _config;
        private HeldItemDriver _driver;
        public void Initialize(UnityEngine.Camera view, HeldItemDriverConfig config = null)
        {
            if (config != null) _config = config;

            if (_driver == null) _driver = gameObject.AddComponent<HeldItemDriver>();
            _driver.Initialize(view, _config);
            _driver.enabled = isActiveAndEnabled;
        }
        public void SetConsumables(ConsumableInventorySnapshot snapshot) { if (_driver != null) _driver.SetConsumables(snapshot); }
        public void SetSuppressed(bool suppressed) { if (_driver != null) _driver.SetSuppressed(suppressed); }
        public void Teardown() { if (_driver != null) _driver.Teardown(); }
        private void OnEnable() { if (_driver != null) _driver.enabled = true; }
        private void OnDisable() { if (_driver != null) _driver.enabled = false; }
        private void OnDestroy() => Teardown();
    }
}
