// ============================================================================
// HunterMotorDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Holds the physical probe dimensions used by the hunter engine boundary. The separate profile supplies gameplay speeds and phases so changes remain explicit.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Keep authored data and system-local value contracts separate from execution.
// DEPENDENCIES:
//   - The owning Hunter system and pure UnityEngine values only.
// USAGE NOTES:
//   Scene-owned instances receive immutable shared configuration. Runtime code never changes assets.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Motor Driver Config")]
    public sealed class HunterMotorDriverConfig : ScriptableObject
    {
        [SerializeField] private float _radius = 0.4f;
        [SerializeField] private float _height = 1.8f;
        [SerializeField] private float _skinWidth = 0.02f;
        [SerializeField] private float _eyeHeight = 1.5f;
        [SerializeField] private Vector3 _targetSampleHeights = new Vector3(1.55f, 1f, 0.5f);
        [SerializeField] private float _pathSampleRadius = 2f;
        [SerializeField] private float _pathRepathSeconds = 0.15f;
        [SerializeField] private float _cornerTolerance = 0.25f;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private LayerMask _sightMask = ~0;
        [SerializeField] private float _gravity = 24f;
        [SerializeField] private float _stepHeight = 0.35f;
        [SerializeField] private float _groundProbeDistance = 0.1f;
        [SerializeField] private float _slopeLimitDegrees = 45f;
        public float Radius => _radius;
        public float Height => _height;
        public float SkinWidth => _skinWidth;
        public float EyeHeight => _eyeHeight;
        public Vector3 TargetSampleHeights => _targetSampleHeights;
        public float PathSampleRadius => _pathSampleRadius;
        public float PathRepathSeconds => _pathRepathSeconds;
        public float CornerTolerance => _cornerTolerance;
        public int CollisionMask => _collisionMask.value;
        public int SightMask => _sightMask.value;
        public float Gravity => _gravity;
        public float StepHeight => _stepHeight;
        public float GroundProbeDistance => _groundProbeDistance;
        public float SlopeLimitDegrees => _slopeLimitDegrees;
    }
}
