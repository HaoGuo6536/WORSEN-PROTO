// ============================================================================
// ExpeditionConfig.cs
// ============================================================================
// PURPOSE:
//   Supplies floor-assembly tuning not yet exposed by Progression's public view.
//   The provisional golden reward multiplier is injected into the Floor mapping.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Keep the Faster Collapse golden reward multiplier designer-owned.
// DEPENDENCIES:
//   Unity serialization only.
// USAGE NOTES:
//   Provisional bridge until Progression exposes its authoritative config value.
//   Runtime reads never modify this asset; the Manager owns any transient default.
// ============================================================================
using UnityEngine;
namespace Worsen.Session.Expedition
{
    [CreateAssetMenu(menuName = "Worsen/Expedition/Config")]
    public sealed class ExpeditionConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _fasterCollapseGoldenCakeMultiplier = 1.15f;
        public float FasterCollapseGoldenCakeMultiplier => _fasterCollapseGoldenCakeMultiplier;
    }
}
