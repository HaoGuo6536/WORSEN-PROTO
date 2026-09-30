// ============================================================================
// PlayerProfile.cs
// ============================================================================
// PURPOSE:
//   Defines a player archetype, its prefab, movement tuning and hidden health.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Content SO (§4b) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Bound total commanded speed on ticks that consume external impulses or acceleration.
//   - Tune passive health regeneration and the quiet interval after accepted damage.
//   - Tune ledge reach, late traversal steering, timed boosts and fail-forward recovery.
//   - Tune grace duration and independent light/heavy hit recovery speed and duration.
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Slide steering is limited by lateral acceleration and angular rate; legacy LookBackSteerAuthority is unused.
//   Designer data only. The factory resolves ArchetypeKey; runtime code never edits this asset.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Player
{
    [CreateAssetMenu(menuName = "Worsen/Player/Player Profile")]
    public sealed class PlayerProfile : ScriptableObject
    {
        [SerializeField] private string _archetypeKey = "player";
        [SerializeField] private GameObject _prefab;
        [SerializeField] private float _sprintSpeed = 8f;
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _maxDesignSpeed = 14f;
        [SerializeField, Min(0f)] private float _maximumExternalMotionSpeed = 14f;
        [SerializeField] private float _groundAcceleration = 60f;
        [SerializeField] private float _groundFriction = 70f;
        [SerializeField] private float _jumpSpeed = 5.5f;
        [SerializeField] private float _jumpBuffer = 0.1f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _airAcceleration = 25f;
        [SerializeField, Min(0f)] private float _airControlSpeedFloor = 2f;
        [SerializeField] private float _gravity = 18f;
        [SerializeField] private float _slideMinimumSpeed = 6f;
        [SerializeField] private float _slideBoost = 2f;
        [SerializeField] private float _slideDuration = 1.2f;
        [SerializeField, Min(0f)] private float _slideLateralAcceleration = 18f;
        [SerializeField, Range(0f, 180f)] private float _slideMaximumTurnRate = 100f;
        [SerializeField, Range(0f, 1f)] private float _slideWallSpeedRetention = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _walkingLoudness = 0.12f;
        [SerializeField] private float _vaultMinimumHeight = 0.35f;
        [SerializeField] private float _vaultMaximumHeight = 1.2f;
        [SerializeField] private float _mantleMaximumHeight = 2f;
        [SerializeField] private float _vaultDuration = 0.25f;
        [SerializeField] private float _mantleDuration = 0.35f;
        [SerializeField] private float _vaultCompletionTolerance = 0.05f;
        [SerializeField, Min(0f)] private float _traversalSteeringSpeed = 2f;
        [SerializeField, Min(0f)] private float _traversalBoostWindow = 0.12f;
        [SerializeField, Min(0f)] private float _traversalBoostSpeed = 3f;
        [SerializeField, Min(0f)] private float _ledgeReach = 1.2f;
        [SerializeField, Min(0f)] private float _ledgeMinimumHeight = 0.5f;
        [SerializeField, Min(0f)] private float _ledgeMaximumHeight = 1.8f;
        [SerializeField, Min(0f)] private float _ledgeChestHeight = 0.8f;
        [SerializeField, Min(0f)] private float _ledgeRegrabDelay = 0.2f;
        [SerializeField, Min(0f)] private float _failedVaultStumbleDuration = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _stumbleSpeedMultiplier = 0.6f;
        [SerializeField] private float _reboundDistance = 0.6f;
        [SerializeField] private float _reboundAngle = 45f;
        [SerializeField] private float _reboundJumpWindow = 0.15f;
        [SerializeField] private float _reboundUpwardBoost = 3f;
        [SerializeField] private float _reboundCooldown = 0.4f;
        [SerializeField] private float _softLandingThreshold = 12f;
        [SerializeField] private float _hardLandingThreshold = 18f;
        [SerializeField] private float _softLandingRetention = 0.6f;
        [SerializeField] private float _hardLandingRetention = 0.3f;
        [SerializeField] private float _softStumbleDuration = 0.2f;
        [SerializeField] private float _hardStumbleDuration = 0.5f;
        [SerializeField] private float _lookBackSteerAuthority = 0.35f;
        [SerializeField] private float _maximumHealth = 100f;
        [SerializeField, Min(0f)] private float _healthRegenerationPerSecond = 1.5f;
        [SerializeField, Min(0f)] private float _healthRegenerationDelay = 4f;
        [SerializeField] private float _lungeDamage = 50f;
        [SerializeField, Min(0f)] private float _hitGraceSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _lightHitSpeedBoost = 0.12f;
        [SerializeField, Min(0f)] private float _lightHitBoostSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _heavyHitSpeedBoost = 0.25f;
        [SerializeField, Min(0f)] private float _heavyHitBoostSeconds = 1.2f;
        [SerializeField] private float _injuredThreshold = 50f;
        [SerializeField] private float _criticalThreshold = 25f;
        [SerializeField] private float _injuredSpeedMultiplier = 0.95f;
        [SerializeField] private int _noiseCapacity = 16;
        [SerializeField] private float _footstepInterval = 0.35f;
        [SerializeField] private float _sprintLoudness = 0.25f;
        [SerializeField] private float _slideLoudness = 0.55f;
        [SerializeField] private float _traversalLoudness = 1f;

        public string ArchetypeKey => _archetypeKey;
        public GameObject Prefab => _prefab;
        public float SprintSpeed => _sprintSpeed;
        public float WalkSpeed => _walkSpeed;
        public float MaxDesignSpeed => _maxDesignSpeed;
        public float MaximumExternalMotionSpeed => _maximumExternalMotionSpeed;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundFriction => _groundFriction;
        public float JumpSpeed => _jumpSpeed;
        public float JumpBuffer => _jumpBuffer;
        public float CoyoteTime => _coyoteTime;
        public float AirAcceleration => _airAcceleration;
        public float AirControlSpeedFloor => _airControlSpeedFloor;
        public float Gravity => _gravity;
        public float SlideMinimumSpeed => _slideMinimumSpeed;
        public float SlideBoost => _slideBoost;
        public float SlideDuration => _slideDuration;
        public float SlideLateralAcceleration => _slideLateralAcceleration;
        public float SlideMaximumTurnRate => _slideMaximumTurnRate;
        public float SlideWallSpeedRetention => _slideWallSpeedRetention;
        public float WalkingLoudness => _walkingLoudness;
        public float VaultMinimumHeight => _vaultMinimumHeight;
        public float VaultMaximumHeight => _vaultMaximumHeight;
        public float MantleMaximumHeight => _mantleMaximumHeight;
        public float VaultDuration => _vaultDuration;
        public float MantleDuration => _mantleDuration;
        public float VaultCompletionTolerance => _vaultCompletionTolerance;
        public float TraversalSteeringSpeed => _traversalSteeringSpeed;
        public float TraversalBoostWindow => _traversalBoostWindow;
        public float TraversalBoostSpeed => _traversalBoostSpeed;
        public float LedgeReach => _ledgeReach;
        public float LedgeMinimumHeight => _ledgeMinimumHeight;
        public float LedgeMaximumHeight => _ledgeMaximumHeight;
        public float LedgeChestHeight => _ledgeChestHeight;
        public float LedgeRegrabDelay => _ledgeRegrabDelay;
        public float FailedVaultStumbleDuration => _failedVaultStumbleDuration;
        public float StumbleSpeedMultiplier => _stumbleSpeedMultiplier;
        public float ReboundDistance => _reboundDistance;
        public float ReboundAngle => _reboundAngle;
        public float ReboundJumpWindow => _reboundJumpWindow;
        public float ReboundUpwardBoost => _reboundUpwardBoost;
        public float ReboundCooldown => _reboundCooldown;
        public float SoftLandingThreshold => _softLandingThreshold;
        public float HardLandingThreshold => _hardLandingThreshold;
        public float SoftLandingRetention => _softLandingRetention;
        public float HardLandingRetention => _hardLandingRetention;
        public float SoftStumbleDuration => _softStumbleDuration;
        public float HardStumbleDuration => _hardStumbleDuration;
        public float LookBackSteerAuthority => _lookBackSteerAuthority;
        public float MaximumHealth => _maximumHealth;
        public float HealthRegenerationPerSecond => _healthRegenerationPerSecond;
        public float HealthRegenerationDelay => _healthRegenerationDelay;
        public float LungeDamage => _lungeDamage;
        public float HitGraceSeconds => _hitGraceSeconds;
        public float LightHitSpeedBoost => _lightHitSpeedBoost;
        public float LightHitBoostSeconds => _lightHitBoostSeconds;
        public float HeavyHitSpeedBoost => _heavyHitSpeedBoost;
        public float HeavyHitBoostSeconds => _heavyHitBoostSeconds;
        public float InjuredThreshold => _injuredThreshold;
        public float CriticalThreshold => _criticalThreshold;
        public float InjuredSpeedMultiplier => _injuredSpeedMultiplier;
        public int NoiseCapacity => _noiseCapacity;
        public float FootstepInterval => _footstepInterval;
        public float SprintLoudness => _sprintLoudness;
        public float SlideLoudness => _slideLoudness;
        public float TraversalLoudness => _traversalLoudness;
    }
}
