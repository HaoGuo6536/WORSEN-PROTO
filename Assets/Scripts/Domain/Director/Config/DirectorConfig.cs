// ============================================================================
// DirectorConfig.cs
// ============================================================================
// PURPOSE:
//   Defines the pressure and relief policy used by the scene's Director.
//   Keeping every pacing value in one designer asset lets timing be tuned without
//   changing the fixed Session tick or giving Hunters perfect player knowledge.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Director.
// KEY RESPONSIBILITIES:
//   - Supply evaluation, delayed-hint, relief, and movement-intrusion tuning.
//   - Bound each player's historical position storage.
// DEPENDENCIES:
//   - UnityEngine serialization only; no other game system.
// USAGE NOTES:
//   Designer data only. Runtime counters belong to DirectorBehaviorState.
//   The mirrored asset is created by DirectorConfigGenerator under the Unity lease.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Director
{
    [CreateAssetMenu(menuName = "Worsen/Director/Config")]
    public sealed class DirectorConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _evaluationIntervalSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _heatThresholdSeconds = 20f;
        [SerializeField, Min(0f)] private float _reliefMinimumSeconds = 10f;
        [SerializeField, Min(0.01f)] private float _hintAgeSeconds = 3f;
        [SerializeField, Min(0f)] private float _hintRadiusMeters = 8f;
        [SerializeField, Range(0f, 1f)] private float _hintConfidence = 0.5f;
        [SerializeField, Min(0.01f)] private float _hintCadenceSeconds = 5f;
        [SerializeField, Min(0.01f)] private float _exitOpenHintCadenceSeconds = 2.5f;
        [SerializeField, Min(0f)] private float _proximityRadiusMeters = 20f;
        [SerializeField, Min(0f)] private float _slowSpeedMetersPerSecond = 1f;
        [SerializeField, Min(0f)] private float _slowThresholdSeconds = 3f;
        [SerializeField, Min(0.01f)] private float _intrusionDurationSeconds = 2f;
        [SerializeField, Min(0f)] private float _intrusionCooldownSeconds = 10f;
        [SerializeField, Min(2)] private int _historyCapacity = 512;

        public float EvaluationIntervalSeconds => _evaluationIntervalSeconds;
        public float HeatThresholdSeconds => _heatThresholdSeconds;
        public float ReliefMinimumSeconds => _reliefMinimumSeconds;
        public float HintAgeSeconds => _hintAgeSeconds;
        public float HintRadiusMeters => _hintRadiusMeters;
        public float HintConfidence => _hintConfidence;
        public float HintCadenceSeconds => _hintCadenceSeconds;
        public float ExitOpenHintCadenceSeconds => _exitOpenHintCadenceSeconds;
        public float ProximityRadiusMeters => _proximityRadiusMeters;
        public float SlowSpeedMetersPerSecond => _slowSpeedMetersPerSecond;
        public float SlowThresholdSeconds => _slowThresholdSeconds;
        public float IntrusionDurationSeconds => _intrusionDurationSeconds;
        public float IntrusionCooldownSeconds => _intrusionCooldownSeconds;
        public int HistoryCapacity => _historyCapacity;
    }
}
