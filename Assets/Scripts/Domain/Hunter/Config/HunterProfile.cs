// ============================================================================
// HunterProfile.cs
// ============================================================================
// PURPOSE:
//   Defines immutable Hunter archetype sensing, light response and attack tuning.
//   Profiles distinguish melee reach/elevation, traveling spells and warned ground eruptions.
//   Runtime memories, curse effects and cooldowns live in per-instance state.
// ARCHITECTURAL ROLE:
//   Content SO (section 4b) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Shared immutable asset; never modified by runtime code.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Hunter Profile")]
    public sealed class HunterProfile : ScriptableObject
    {
        [SerializeField] private string _archetypeKey = "Hunter";
        [SerializeField] private GameObject _prefab;
        [SerializeField] private float _acceleration = 20f;
        [SerializeField] private float _turnRate = 240f;
        [SerializeField] private float _chaseSpeedMultiplier = 1.12f;
        [SerializeField] private float _patrolSpeed = 3f;
        [SerializeField] private float _sightConeDegrees = 110f;
        [SerializeField] private float _sightRange = 30f;
        [SerializeField] private int _sensorIntervalTicks = 4;
        [SerializeField] private float _hearingRange = 18f;
        [SerializeField] private float _hearingThreshold = 0.08f;
        [SerializeField] private float _noiseMaxAgeSeconds = 0.5f;
        [SerializeField] private float _memoryDecaySeconds = 8f;
        [SerializeField] private float _beliefFreshSeconds = 3f;
        [SerializeField] private float _lungeWindupSeconds = 0.25f;
        [SerializeField] private float _lungeActiveSeconds = 0.3f;
        [SerializeField] private float _lungeRecoverySeconds = 0.8f;
        [SerializeField] private float _lungeDistance = 4f;
        [SerializeField] private float _maximumMeleeElevation = 0.45f;
        [SerializeField] private float _lungeSpeed = 18f;
        [SerializeField] private int _lungeDamage = 50;
        [SerializeField] private float _arrivalRadius = 1f;
        [SerializeField] private float _searchSeconds = 2f;
        [SerializeField] private float _cutOffPredictionSeconds = 1.5f;
        [SerializeField] private HunterLightResponse _lightResponse = HunterLightResponse.Investigate;
        [SerializeField] private float _lightMemorySeconds = 3f;
        [SerializeField] private float _lightReactionSeconds = 0.8f;
        [SerializeField] private float _lightReactionCooldownSeconds = 3f;
        [SerializeField] private float _lightExposureSeconds = 0.12f;
        [SerializeField] private float _lightReactionDistance = 3f;
        // Retain the serialized key for existing roster assets and setup tools.
        [SerializeField, InspectorName("Attack Screams Enabled")] private bool _screamOnDetection;
        [SerializeField, Range(0f, 1f)] private float _attackScreamChance = 0.25f;
        [SerializeField] private float _screamCooldownSeconds = 12f;
        [SerializeField] private HunterAttackStyle _attackStyle;
        [SerializeField] private float _rangedAttackDistance = 15f;
        [SerializeField] private float _projectileSpeed = 11f;
        [SerializeField] private float _projectileRadius = 0.22f;
        [SerializeField] private float _spikeRadius = 1.5f;
        public HunterAttackStyle AttackStyle => _attackStyle;
        public float RangedAttackDistance => Mathf.Max(2f, _rangedAttackDistance);
        public float ProjectileSpeed => Mathf.Max(2f, _projectileSpeed);
        public float ProjectileRadius => Mathf.Max(0.1f, _projectileRadius);
        public float SpikeRadius => Mathf.Max(0.5f, _spikeRadius);
        public HunterLightResponse LightResponse => _lightResponse;
        public float LightMemorySeconds => Mathf.Max(0.1f, _lightMemorySeconds);
        public float LightReactionSeconds => Mathf.Max(0.1f, _lightReactionSeconds);
        public float LightReactionCooldownSeconds => Mathf.Max(1f, _lightReactionCooldownSeconds);
        public float LightExposureSeconds => Mathf.Max(0.05f, _lightExposureSeconds);
        public float LightReactionDistance => Mathf.Max(0.5f, _lightReactionDistance);
        public bool ScreamOnDetection => _screamOnDetection;
        public bool AttackScreamsEnabled => _screamOnDetection;
        public float AttackScreamChance => Mathf.Clamp01(_attackScreamChance);
        public float ScreamCooldownSeconds => Mathf.Max(5f, _screamCooldownSeconds);
        public string ArchetypeKey => _archetypeKey;
        public GameObject Prefab => _prefab;
        public float Acceleration => _acceleration;
        public float TurnRate => _turnRate;
        public float ChaseSpeedMultiplier => _chaseSpeedMultiplier;
        public float PatrolSpeed => _patrolSpeed;
        public float SightConeDegrees => _sightConeDegrees;
        public float SightRange => _sightRange;
        public int SensorIntervalTicks => _sensorIntervalTicks;
        public float HearingRange => _hearingRange;
        public float HearingThreshold => _hearingThreshold;
        public float NoiseMaxAgeSeconds => _noiseMaxAgeSeconds;
        public float MemoryDecaySeconds => _memoryDecaySeconds;
        public float BeliefFreshSeconds => _beliefFreshSeconds;
        public float LungeWindupSeconds => _lungeWindupSeconds;
        public float LungeActiveSeconds => _lungeActiveSeconds;
        public float LungeRecoverySeconds => _lungeRecoverySeconds;
        public float LungeDistance => _lungeDistance;
        public float MaximumMeleeElevation => Mathf.Max(0.2f, _maximumMeleeElevation);
        public float LungeSpeed => _lungeSpeed;
        public int LungeDamage => _lungeDamage;
        public float ArrivalRadius => _arrivalRadius;
        public float SearchSeconds => _searchSeconds;
        public float CutOffPredictionSeconds => _cutOffPredictionSeconds;
    }
}
