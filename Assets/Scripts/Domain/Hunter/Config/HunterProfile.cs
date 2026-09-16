// ============================================================================
// HunterProfile.cs
// ============================================================================
// PURPOSE:
//   Defines one hunter archetype and its sensing, decision, and attack tunings. Per-life belief and attack state are kept outside this shared asset.
// ARCHITECTURAL ROLE:
//   Content SO (§4b) · Domain · Hunter.
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
        [SerializeField] private float _lungeSpeed = 18f;
        [SerializeField] private int _lungeDamage = 50;
        [SerializeField] private float _arrivalRadius = 1f;
        [SerializeField] private float _searchSeconds = 2f;
        [SerializeField] private float _cutOffPredictionSeconds = 1.5f;
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
        public float LungeSpeed => _lungeSpeed;
        public int LungeDamage => _lungeDamage;
        public float ArrivalRadius => _arrivalRadius;
        public float SearchSeconds => _searchSeconds;
        public float CutOffPredictionSeconds => _cutOffPredictionSeconds;
    }
}
