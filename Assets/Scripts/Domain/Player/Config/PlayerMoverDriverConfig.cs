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
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Used only by PlayerDriver and its owned PlayerLimbStandIn. Distances are metres.
//   FootOffset is relative to the slide eye height; its default keeps the feet in the lower view.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Player
{
    [CreateAssetMenu(menuName = "Worsen/Player/Mover Driver Config")]
    public sealed class PlayerMoverDriverConfig : ScriptableObject
    {
        [SerializeField] private float _height = 1.8f;
        [SerializeField] private float _radius = 0.3f;
        [SerializeField] private float _slideHeightRatio = 0.5f;
        [SerializeField] private float _skinWidth = 0.02f;
        [SerializeField] private int _castIterations = 5;
        [SerializeField] private float _groundProbeDistance = 0.12f;
        [SerializeField] private float _groundSnapDistance = 0.2f;
        [SerializeField] private float _stepHeight = 0.3f;
        [SerializeField] private float _slopeLimitDegrees = 50f;
        [SerializeField] private float _wallProbeDistance = 0.6f;
        [SerializeField] private float _vaultProbeDistance = 1.15f;
        [SerializeField] private float _eyeHeight = 1.6f;
        [SerializeField] private float _traversalLift = 0.08f;
        [SerializeField] private float _traversalRisePortion = 0.25f;
        [SerializeField] private float _traversalTraverseEnd = 0.95f;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private bool _interpolateVisuals = true;
        [SerializeField] private Vector3 _handOffset = new Vector3(0.32f, -0.25f, 0.5f);
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
        public bool InterpolateVisuals => _interpolateVisuals;
        public Vector3 HandOffset => _handOffset;
        public Vector3 FootOffset => _footOffset;
    }
}
