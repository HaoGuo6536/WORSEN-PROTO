// ============================================================================
// FloorCollapseHazardConfig.cs
// ============================================================================
// PURPOSE:
//   Holds provisional front and contact settings separately from the collapse schedule.
//   The schedule still owns phase duration; this asset tunes how consumption uses it.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Tune front travel, closed-wall thickness, bounce and repeat damage spacing.
// DEPENDENCIES:
//   - Unity serialized values only; no foreign system references.
// USAGE NOTES:
//   Mirror asset: Resources/ScriptableObjects/Domain/Floor/FloorCollapseHazardConfig.
//   FloorManager must inject the same asset into the hand controller and FloorDriver.
//   No runtime mutation. Defaults also support legacy callers without the asset.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Floor
{
    [CreateAssetMenu(menuName = "Worsen/Floor/Collapse Hazard Config")]
    public sealed class FloorCollapseHazardConfig : ScriptableObject
    {
        public const float DefaultFrontExponent = 1f;
        public const float DefaultWallThickness = .2f;
        public const float DefaultBounceSpeed = 6f;
        public const float DefaultContactDistance = .65f;
        public const float DefaultDamageInterval = 4.5f;
        [SerializeField, Range(.25f, 4f)] private float _frontExponent = DefaultFrontExponent;
        [SerializeField, Min(.01f)] private float _wallThickness = DefaultWallThickness;
        [SerializeField, Min(.1f)] private float _bounceSpeed = DefaultBounceSpeed;
        [SerializeField, Min(.01f)] private float _contactDistance = DefaultContactDistance;
        [SerializeField, Min(.1f)] private float _damageInterval = DefaultDamageInterval;
        public float FrontExponent => Mathf.Clamp(_frontExponent, .25f, 4f);
        public float WallThickness => Mathf.Max(.01f, _wallThickness);
        public float BounceSpeed => Mathf.Max(.1f, _bounceSpeed);
        public float ContactDistance => Mathf.Max(.01f, _contactDistance);
        public float DamageInterval => Mathf.Max(.1f, _damageInterval);
    }
}
