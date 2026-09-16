// ============================================================================
// CameraDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores the first-person view's designer settings in one asset.
//   These settings affect visual feedback only; player movement retains its own rules.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Camera.
//
// KEY RESPONSIBILITIES:
//   - Preserve the M4 field of view, look-back and detection timing defaults.
//   - Expose comfort controls without storing runtime effect state.
//
// DEPENDENCIES:
//   - No other project systems.
//
// USAGE NOTES:
//   - Asset path mirrors Presentation/Camera under Resources/ScriptableObjects.
//   - Runtime code reads this asset without changing designer values.
//   - Optional third-person detection experiment remains deferred until its chase gate.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.Camera
{
    [CreateAssetMenu(fileName = "CameraDriverConfig", menuName = "Worsen/Camera/Driver Config")]
    public sealed class CameraDriverConfig : ScriptableObject
    {
        [SerializeField, Range(40f, 140f)] private float _horizontalFieldOfView = 95f;
        [SerializeField, Range(0f, 20f)] private float _speedFieldOfView = 8f;
        [SerializeField, Min(0.1f)] private float _maxDesignSpeed = 14f;
        [SerializeField, Range(0f, 30f)] private float _detectionFieldOfView = 12f;
        [SerializeField, Min(0.001f)] private float _detectionAttackSeconds = 0.08f;
        [SerializeField, Min(0.001f)] private float _detectionDecaySeconds = 0.4f;
        [SerializeField, Range(0f, 2f)] private float _punchIntensity = 1f;
        [SerializeField, Min(0f)] private float _impulseDisplacement = 0.035f;
        [SerializeField, Min(0.001f)] private float _impulseSeconds = 0.2f;
        [SerializeField, Range(0f, 180f)] private float _lookBackYaw = 160f;
        [SerializeField, Min(0.001f)] private float _lookBackSeconds = 0.12f;
        [SerializeField, Min(0.001f)] private float _lookForwardSeconds = 0.15f;
        [SerializeField, Range(0f, 85f)] private float _lookBackPitchLimit = 20f;
        [SerializeField, Range(0f, 20f)] private float _lookBackHeadYawLimit = 20f;
        [SerializeField, Range(0f, 89f)] private float _forwardPitchLimit = 85f;
        [SerializeField] private bool _tiltEnabled = true;
        [SerializeField, Range(0f, 10f)] private float _slideRoll = 6f;
        [SerializeField, Range(0f, 15f)] private float _reboundRoll = 10f;
        [SerializeField, Min(0.001f)] private float _reboundSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float _nearClip = 0.05f;
        [SerializeField, Min(1f)] private float _farClip = 500f;

        public float HorizontalFieldOfView => _horizontalFieldOfView;
        public float SpeedFieldOfView => _speedFieldOfView;
        public float MaxDesignSpeed => _maxDesignSpeed;
        public float DetectionFieldOfView => _detectionFieldOfView;
        public float DetectionAttackSeconds => _detectionAttackSeconds;
        public float DetectionDecaySeconds => _detectionDecaySeconds;
        public float PunchIntensity => _punchIntensity;
        public float ImpulseDisplacement => _impulseDisplacement;
        public float ImpulseSeconds => _impulseSeconds;
        public float LookBackYaw => _lookBackYaw;
        public float LookBackSeconds => _lookBackSeconds;
        public float LookForwardSeconds => _lookForwardSeconds;
        public float LookBackPitchLimit => _lookBackPitchLimit;
        public float LookBackHeadYawLimit => _lookBackHeadYawLimit;
        public float ForwardPitchLimit => _forwardPitchLimit;
        public bool TiltEnabled => _tiltEnabled;
        public float SlideRoll => _slideRoll;
        public float ReboundRoll => _reboundRoll;
        public float ReboundSeconds => _reboundSeconds;
        public float NearClip => _nearClip;
        public float FarClip => _farClip;
    }
}

