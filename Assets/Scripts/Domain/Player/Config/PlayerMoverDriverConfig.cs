// ============================================================================
// PlayerMoverDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Provides physical capsule, probing, interpolation and limb visibility tuning.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Tune landing contact tolerance and legal step height independently of traversal rules.
//   - Name the hunter-body layer excluded from movement queries and contacts during grace.
//   - Tune yaw-only shoulder anchors and eased, speed-scaled walking arm swing.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Used only by PlayerDriver and its owned PlayerLimbStandIn. Distances are metres.
//   HandOffset retains its serialized name but now locates the shoulder pivot.
//   Provisional relaxed defaults are migrated once by PlayerPrefabGenerator.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Player
{
    [CreateAssetMenu(menuName = "Worsen/Player/Mover Driver Config")]
    public sealed class PlayerMoverDriverConfig : ScriptableObject
    {
        public static readonly Vector3 DefaultShoulderOffset = new Vector3(0.24f, -0.22f, 0.08f);
        public const float DefaultArmSwingDegrees = 6f;
        public const float DefaultArmSwingReferenceSpeed = 4f;
        public const float DefaultArmSwingFrequency = 1.3f;
        public const float DefaultArmSwingEaseSeconds = 0.2f;
        [SerializeField] private float _height = 1.8f;
        [SerializeField] private float _radius = 0.3f;
        [SerializeField] private float _slideHeightRatio = 0.5f;
        [SerializeField] private float _skinWidth = 0.02f;
        [SerializeField] private int _castIterations = 5;
        [SerializeField] private float _groundProbeDistance = 0.16f;
        [SerializeField] private float _groundSnapDistance = 0.25f;
        [SerializeField] private float _stepHeight = 0.4f;
        [SerializeField] private float _slopeLimitDegrees = 50f;
        [SerializeField] private float _wallProbeDistance = 0.6f;
        [SerializeField] private float _vaultProbeDistance = 1.15f;
        [SerializeField] private float _eyeHeight = 1.6f;
        [SerializeField] private float _traversalLift = 0.08f;
        [SerializeField] private float _traversalRisePortion = 0.25f;
        [SerializeField] private float _traversalTraverseEnd = 0.95f;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private string _hunterBodyLayer = "HunterBody";
        [SerializeField] private bool _interpolateVisuals = true;
        [Tooltip("Shoulder pivot relative to the eye in body-yaw space; X is mirrored.")]
        [SerializeField] private Vector3 _handOffset = DefaultShoulderOffset;
        [SerializeField, Range(0f, 8f)] private float _armSwingDegrees = DefaultArmSwingDegrees;
        [SerializeField] private float _armSwingReferenceSpeed = DefaultArmSwingReferenceSpeed;
        [SerializeField] private float _armSwingFrequency = DefaultArmSwingFrequency;
        [SerializeField] private float _armSwingEaseSeconds = DefaultArmSwingEaseSeconds;
        [SerializeField, HideInInspector] private int _relaxedArmsVersion = 0;
        [SerializeField] private Vector3 _footOffset = new Vector3(0.2f, -0.25f, 0.5f);

        public float Height => _height;
        public float Radius => _radius;
        public float SlideHeightRatio => _slideHeightRatio;
        public float SkinWidth => _skinWidth;
        public int CastIterations => _castIterations;
        public float GroundProbeDistance => _groundProbeDistance;
        public float GroundSnapDistance => _groundSnapDistance;
        public float StepHeight => _stepHeight;
        public float SlopeLimitDegrees => _slopeLimitDegrees;
        public float WallProbeDistance => _wallProbeDistance;
        public float VaultProbeDistance => _vaultProbeDistance;
        public float EyeHeight => _eyeHeight;
        public float TraversalLift => _traversalLift;
        public float TraversalRisePortion => _traversalRisePortion;
        public float TraversalTraverseEnd => _traversalTraverseEnd;
        public LayerMask CollisionMask => _collisionMask;
        public string HunterBodyLayer => _hunterBodyLayer;
        public bool InterpolateVisuals => _interpolateVisuals;
        public Vector3 HandOffset => _handOffset;
        public float ArmSwingDegrees => _armSwingDegrees;
        public float ArmSwingReferenceSpeed => _armSwingReferenceSpeed;
        public float ArmSwingFrequency => _armSwingFrequency;
        public float ArmSwingEaseSeconds => _armSwingEaseSeconds;
        public int RelaxedArmsVersion => _relaxedArmsVersion;
        public Vector3 FootOffset => _footOffset;
    }
}
