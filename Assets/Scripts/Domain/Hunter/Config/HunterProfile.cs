// ============================================================================
// HunterProfile.cs
// ============================================================================
// PURPOSE:
//   Defines immutable Hunter archetype sensing, light response and attack tuning.
//   Profiles distinguish melee reach/elevation, traveling spells and warned ground eruptions.
//   Runtime memories, curse effects and cooldowns live in per-instance state.
//   Investigation and unseen stalking have separate approach and reveal tuning.
//   The four-part brief maps inertia to acceleration/turn, commitment to action
//   and lunge timing, speed ratio to chase multiplier, and loss to time/distance.
// ARCHITECTURAL ROLE:
//   Content SO (section 4b) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Author one hunter's shared tuning, separate from per-life state and foreign systems, preserving observable sensing and committed attacks.
//   - Configure movement: walk speed, chase-relative stalk speed, the reveal cone, and door-break and Wick sight approach tuning.
//   - Supply decision tuning: deliberation, utility, prediction, missed lunge, chase lead versus loop intercepts, Stalk clues and search-leg budgets.
//   - Author habits, a tell-only mutation pool, emergence preferences, and the optional archetype rules config and per-profile motor override.
//   - Expose provisional roster and selection depth gates for admission only; Session enforces them and never despawns by depth.
// DEPENDENCIES:
//   - Hunter-local enums, Core hearing settings and UnityEngine asset authoring types.
//   - No foreign system state or runtime engine operations.
// USAGE NOTES:
//   Shared immutable asset; never modified by runtime code.
//   Approach defaults are provisional. InvestigateSpeed starts at the patrol default,
//   not a live link to PatrolSpeed; StalkSpeedMultiplier is relative to chase speed.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Hunter Profile")]
    public sealed class HunterProfile : ScriptableObject
    {
        [SerializeField] private string _archetypeKey = "Hunter";
        [SerializeField, Min(1)] private int _minimumDepth = 1;
        public int MinimumDepth => Mathf.Max(1, _minimumDepth);
        [SerializeField] private GameObject _prefab;
        [SerializeField] private HunterArchetypeConfig _archetypeRules = null;
        [SerializeField] private HunterMotorDriverConfig _motorOverride = null;
        public HunterArchetypeConfig ArchetypeRules => _archetypeRules;
        public HunterMotorDriverConfig MotorOverride => _motorOverride;
        [SerializeField, Min(1f)] private float _wickSightMultiplier = 1.3f;
        [SerializeField, Min(0.1f)] private float _doorBreakReach = 1.5f;
        public float WickSightMultiplier => Mathf.Max(1f, _wickSightMultiplier);
        public float DoorBreakReach => Mathf.Max(0.1f, _doorBreakReach);
        [Header("Habits and hidden mutations")]
        [SerializeField] private HunterHabitData[] _habits = {
            new HunterHabitData(HunterHabitKind.ThresholdPause),
            new HunterHabitData(HunterHabitKind.TurnToFace),
            new HunterHabitData(HunterHabitKind.CakeReaction) };
        [SerializeField] private HunterMutationData[] _mutationPool = System.Array.Empty<HunterMutationData>();
        [SerializeField] private bool _emergenceBias = true;
        [SerializeField, Range(2, 32)] private int _emergenceWaypointBudget = 16;
        public System.Collections.Generic.IReadOnlyList<HunterHabitData> Habits => _habits;
        public System.Collections.Generic.IReadOnlyList<HunterMutationData> MutationPool => _mutationPool;
        public bool EmergenceBias => _emergenceBias;
        public int EmergenceWaypointBudget => Mathf.Clamp(_emergenceWaypointBudget, 2, 32);
        [SerializeField] private float _acceleration = 20f;
        [SerializeField] private float _turnRate = 240f;
        [SerializeField] private float _chaseSpeedMultiplier = 1.12f;
        [SerializeField] private float _patrolSpeed = 3f;
        [SerializeField, Min(0f)] private float _investigateSpeed = 3f;
        [SerializeField, Range(0f, 1f)] private float _stalkSpeedMultiplier = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _stalkMinimumConfidence = 0.25f;
        [SerializeField, Min(0f)] private float _stalkRevealDistance = 12f;
        [SerializeField, Range(0f, 180f)] private float _stalkViewHalfAngleDegrees = 55f;
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
        [Header("Commitment and loss rule")]
        [SerializeField, Min(0.1f)] private float _actionCommitmentSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _lossSeconds = 2.5f;
        [SerializeField, Min(0f)] private float _lossDistance = 14f;
        [SerializeField, Min(0.01f)] private float _missStaggerSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _missStumbleMeters = 0.8f;
        [Header("Felt intelligence")]
        [SerializeField, Min(0f)] private float _deliberationSeconds = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _predictionChance = 0.75f;
        [SerializeField, Min(0f)] private float _chasePredictionSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _parallelCorridorChance = 0.25f;
        [SerializeField, Min(0f)] private float _searchExpansionMeters = 2f;
        [SerializeField, Min(0.1f)] private float _searchLegTimeoutSeconds = 4f;
        [SerializeField, Min(1f)] private float _searchTravelAllowance = 2f;
        [SerializeField, Min(0.1f)] private float _searchMaximumLegSeconds = 60f;
        [SerializeField, Min(0.1f)] private float _retreatTimeoutSeconds = 12f;
        [SerializeField, Min(0f)] private float _cakeGoalUtility = 40f;
        [SerializeField, Min(0f)] private float _exitGoalUtility = 65f;
        [SerializeField, Min(0f)] private float _loopGoalUtility = 110f;
        [Header("Shared hearing")]
        [SerializeField, Min(0.01f)] private float _hearingReferenceMeters = 2f;
        [SerializeField, Min(0f)] private float _hearingRolloff = 1f;
        [SerializeField, Range(0f, 1f)] private float _hearingPortalRetention = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _hearingClosedDoorRetention = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _investigateNoiseThreshold = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _exitNoiseThreshold = 0.4f;
        public float ActionCommitmentSeconds => Mathf.Max(0.1f, _actionCommitmentSeconds);
        public bool NeverLoses => _archetypeRules != null && _archetypeRules.NeverLoses;
        public float LossSeconds => NeverLoses ? float.PositiveInfinity : Mathf.Max(0f, _lossSeconds);
        public float LossDistance => NeverLoses ? float.PositiveInfinity : Mathf.Max(0f, _lossDistance);
        // Windup and active duration already define the uninterruptible lunge commitment.
        public float LungeCommitmentSeconds => LungeWindupSeconds + LungeActiveSeconds;
        public float MissStaggerSeconds => Mathf.Max(0.01f, _missStaggerSeconds);
        public float MissStumbleMeters => Mathf.Max(0f, _missStumbleMeters);
        public float DeliberationSeconds => Mathf.Max(0f, _deliberationSeconds);
        public float PredictionChance => Mathf.Clamp01(_predictionChance);
        public float ChasePredictionSeconds => Mathf.Max(0f, _chasePredictionSeconds);
        public float ParallelCorridorChance => Mathf.Clamp01(_parallelCorridorChance);
        public float SearchExpansionMeters => Mathf.Max(0f, _searchExpansionMeters);
        public float SearchLegTimeoutSeconds => Mathf.Max(0.1f, _searchLegTimeoutSeconds);
        public float SearchTravelAllowance => Mathf.Max(1f, _searchTravelAllowance);
        public float SearchMaximumLegSeconds => Mathf.Max(SearchLegTimeoutSeconds, _searchMaximumLegSeconds);
        public float RetreatTimeoutSeconds => Mathf.Max(0.1f, _retreatTimeoutSeconds);
        public float CakeGoalUtility => Mathf.Max(0f, _cakeGoalUtility);
        public float ExitGoalUtility => Mathf.Max(0f, _exitGoalUtility);
        public float LoopGoalUtility => Mathf.Max(0f, _loopGoalUtility);
        public float InvestigateNoiseThreshold => Mathf.Clamp01(_investigateNoiseThreshold);
        public float ExitNoiseThreshold => Mathf.Clamp01(_exitNoiseThreshold);
        public HearingModelSettings HearingModel => new HearingModelSettings(_hearingReferenceMeters,
            _hearingRolloff, _hearingPortalRetention, _hearingClosedDoorRetention, _hearingThreshold);
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
        public float InvestigateSpeed => Mathf.Max(0f, _investigateSpeed);
        public float StalkSpeedMultiplier => Mathf.Clamp01(_stalkSpeedMultiplier);
        public float StalkMinimumConfidence => Mathf.Clamp01(_stalkMinimumConfidence);
        public float StalkRevealDistance => Mathf.Max(0f, _stalkRevealDistance);
        public float StalkViewHalfAngleDegrees => Mathf.Clamp(_stalkViewHalfAngleDegrees, 0f, 180f);
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
