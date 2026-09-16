// ============================================================================
// FloorConfig.cs
// ============================================================================
// PURPOSE:
//   Stores collection weights and collapse timing as designer-owned tuning.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Mirrored asset: ScriptableObjects/Domain/Floor/FloorConfig. Runtime getters only.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    [CreateAssetMenu(menuName = "Worsen/Floor/Floor Config")]
    public sealed class FloorConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _requiredCakeCount = 10;
        [SerializeField, Min(0f)] private float _flowWeight = 5f;
        [SerializeField, Min(0f)] private float _precisionWeight = 3f;
        [SerializeField, Min(0f)] private float _detourWeight = 2f;
        [SerializeField, Min(0f)] private float _riskWeight = 1f;
        [SerializeField, Min(0f)] private float _verticalWeight = 2f;
        [SerializeField, Min(0.01f)] private float _collapseInterval = 12f;
        [SerializeField, Min(0.01f)] private float _telegraphDuration = 6f;
        [SerializeField, Min(0.01f)] private float _directionCueInterval = 0.5f;
        public int RequiredCakeCount => _requiredCakeCount;
        public float FlowWeight => _flowWeight;
        public float PrecisionWeight => _precisionWeight;
        public float DetourWeight => _detourWeight;
        public float RiskWeight => _riskWeight;
        public float VerticalWeight => _verticalWeight;
        public float CollapseInterval => _collapseInterval;
        public float TelegraphDuration => _telegraphDuration;
        public float DirectionCueInterval => _directionCueInterval;
    }
}