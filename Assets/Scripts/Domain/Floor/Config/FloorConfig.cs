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
        [SerializeField, Min(0.1f)] private float _tearingDuration = 2f;
        [SerializeField, Min(0.1f)] private float _encroachingDuration = 6f;
        [SerializeField, Min(0.1f)] private float _handWarningDuration = 0.7f;
        [SerializeField, Min(0.1f)] private float _handEscapeGrace = 1.4f;
        [SerializeField, Min(0.1f)] private float _handCooldown = 2f;
        [SerializeField, Range(0.15f, 0.9f)] private float _handSlowMultiplier = 0.5f;
        [SerializeField, Min(1f)] private float _handDamage = 25f;
        [SerializeField, Min(0.2f)] private float _handReach = 1.7f;
        [SerializeField, Min(0.3f)] private float _handEscapeDistance = 2.1f;
        public float TearingDuration => _tearingDuration > 0f ? _tearingDuration : 2f;
        public float EncroachingDuration => _encroachingDuration > 0f ? _encroachingDuration : 6f;
        public float HandWarningDuration => _handWarningDuration > 0f ? _handWarningDuration : 0.7f;
        public float HandEscapeGrace => _handEscapeGrace > 0f ? _handEscapeGrace : 1.4f;
        public float HandCooldown => _handCooldown > 0f ? _handCooldown : 2f;
        public float HandSlowMultiplier => _handSlowMultiplier > 0f ? Mathf.Clamp(_handSlowMultiplier, 0.15f, 0.9f) : 0.5f;
        public float HandDamage => _handDamage > 0f ? _handDamage : 25f;
        public float HandReach => _handReach > 0f ? _handReach : 1.7f;
        public float HandEscapeDistance => Mathf.Max(HandReach + 0.2f, _handEscapeDistance > 0f ? _handEscapeDistance : 2.1f);
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