// ============================================================================
// MannequinConfig.cs
// ============================================================================
// PURPOSE:
//   Authors observation height, the peripheral-creep cone and stride scaling.
//   The owner rule is light-independent pursuit while unseen. Light failures and
//   lamp budgets are no longer part of this archetype's configuration.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Tune observation and the two retained curse hooks.
// DEPENDENCIES:
//   - HunterArchetypeConfig and Unity asset authoring only.
// USAGE NOTES:
//   All serialized numbers are provisional. Wick remains a shrine freeze, not light.
//   AfterglowSeconds is a zero-valued compatibility getter for pending Session cleanup.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Mannequin Rules")]
    public sealed class MannequinConfig : HunterArchetypeConfig
    {

        [SerializeField, Min(.1f)] private float _observationHeight = 1f;
        [SerializeField, Range(1f, 45f)] private float _directLookHalfAngle = 12f;
        public float AfterglowSeconds => 0f;
        [SerializeField, Min(1f)] private float _longerStridesMultiplier = 1.2f;

        public float ObservationHeight => Mathf.Max(.1f, _observationHeight);
        public float DirectLookHalfAngle => Mathf.Clamp(_directLookHalfAngle, 1f, 45f);
        public float LongerStridesMultiplier => Mathf.Max(1f, _longerStridesMultiplier);
    }
}
