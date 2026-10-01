// ============================================================================
// ProceduralOrganicConfig.cs
// ============================================================================
// PURPOSE:
//   Controls the organic refinement of connected coarse room reservations.
//   Designer choices vary room interiors without changing graph identities,
//   protected exits, gap reservations or the two-metre environment kit contract.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Store provisional room-shape weights and corridor width.
// DEPENDENCIES:
//   - Unity serialization only.
// USAGE NOTES:
//   Wired by ProceduralContentSetup. Module size is a fixed art contract, not
//   a designer scale; corridors use two modules so 3.2m doors fit at every end.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Organic Config")]
    public sealed class ProceduralOrganicConfig : ScriptableObject
    {
        public const float ModuleSize = 2f;
        [SerializeField, Range(0f, 1f)] private float _hallwayProbability = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _smallRoomProbability = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _irregularProbability = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _roundProbability = 0.3f;
        public float HallwayProbability => _hallwayProbability;
        public float SmallRoomProbability => _smallRoomProbability;
        public float IrregularProbability => _irregularProbability;
        public float RoundProbability => _roundProbability;
    }
}
