// ============================================================================
// MannequinConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the silent darkness rule and its reversible owner decision.
//   Rare environmental subversion and curse scaling remain designer values,
//   not hidden constants in the shared hunter planner.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Tune observation, light-failure cadence and neutral curse hooks.
// DEPENDENCIES:
//   - HunterArchetypeConfig and Unity asset authoring only.
// USAGE NOTES:
//   All numbers are provisional. Darkness is default; Wick always prevents motion.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Mannequin Rules")]
    public sealed class MannequinConfig : HunterArchetypeConfig
    {
        [SerializeField] private bool _movesInDarkness = true;
        [SerializeField, Min(.1f)] private float _observationHeight = 1f;
        [SerializeField, Range(1f, 45f)] private float _directLookHalfAngle = 12f;
        [SerializeField, Min(1f)] private float _failureCheckSeconds = 15f;
        [SerializeField, Range(0f, 1f)] private float _failureChance = .02f;
        [SerializeField, Min(.1f)] private float _failureSeconds = 1.25f;
        [SerializeField, Min(1f)] private float _longerStridesMultiplier = 1.2f;
        [SerializeField, Range(.1f, 1f)] private float _fewerLampsMultiplier = .8f;
        public bool MovesInDarkness => _movesInDarkness;
        public float ObservationHeight => Mathf.Max(.1f, _observationHeight);
        public float DirectLookHalfAngle => Mathf.Clamp(_directLookHalfAngle, 1f, 45f);
        public float FailureCheckSeconds => Mathf.Max(1f, _failureCheckSeconds);
        public float FailureChance => Mathf.Clamp01(_failureChance);
        public float FailureSeconds => Mathf.Max(.1f, _failureSeconds);
        public float LongerStridesMultiplier => Mathf.Max(1f, _longerStridesMultiplier);
        public float FewerLampsMultiplier => Mathf.Clamp(_fewerLampsMultiplier, .1f, 1f);
    }
}
