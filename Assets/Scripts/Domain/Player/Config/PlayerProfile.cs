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
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
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
        [SerializeField] private float _groundAcceleration = 60f;
        [SerializeField] private float _groundFriction = 70f;
        [SerializeField] private float _jumpSpeed = 5.5f;
        [SerializeField] private float _jumpBuffer = 0.1f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _airAcceleration = 25f;
        [SerializeField] private float _gravity = 18f;
        [SerializeField] private float _slideMinimumSpeed = 6f;
        [SerializeField] private float _slideBoost = 2f;
        [SerializeField] private float _slideDuration = 1.2f;
        [SerializeField] private float _vaultMinimumHeight = 0.35f;
        [SerializeField] private float _vaultMaximumHeight = 1.2f;
        [SerializeField] private float _mantleMaximumHeight = 2f;
        [SerializeField] private float _vaultDuration = 0.25f;
        [SerializeField] private float _mantleDuration = 0.35f;
        [SerializeField] private float _vaultCompletionTolerance = 0.05f;
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
        [SerializeField] private float _lungeDamage = 50f;
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
        public float GroundAcceleration => _groundAcceleration;
        public float GroundFriction => _groundFriction;
        public float JumpSpeed => _jumpSpeed;
        public float JumpBuffer => _jumpBuffer;
        public float CoyoteTime => _coyoteTime;
        public float AirAcceleration => _airAcceleration;
        public float Gravity => _gravity;
        public float SlideMinimumSpeed => _slideMinimumSpeed;
        public float SlideBoost => _slideBoost;
        public float SlideDuration => _slideDuration;
        public float VaultMinimumHeight => _vaultMinimumHeight;
        public float VaultMaximumHeight => _vaultMaximumHeight;
        public float MantleMaximumHeight => _mantleMaximumHeight;
        public float VaultDuration => _vaultDuration;
        public float MantleDuration => _mantleDuration;
        public float VaultCompletionTolerance => _vaultCompletionTolerance;
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
        public float LungeDamage => _lungeDamage;
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
