// ============================================================================
// HeraldConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Herald's radius attack, warning and varied chase-call cadence.
//   The imported four-file mapping is fixed in its controller; only non-attack
//   pitch varies. Runtime timers and alternating-call state never live here.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Supply low damage and neutral, bounded curse multipliers.
// DEPENDENCIES:
//   - Hunter config base and UnityEngine serialization only.
// USAGE NOTES:
//   Provisional values require arena tuning. Ear Plugs belongs to the receiver.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Herald
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Herald Config")]
    public sealed class HeraldConfig : HunterArchetypeConfig
    {
        [SerializeField, Min(.01f)] private float _radius = 7f;
        [SerializeField, Min(0)] private int _damage = 10;
        [SerializeField, Min(.01f)] private float _deafenSeconds = 4f;
        [SerializeField, Min(.01f)] private float _warningSeconds = .8f;
        [SerializeField, Min(.01f)] private float _attackCooldown = 5f;
        [SerializeField, Min(.01f)] private float _chaseIntervalMin = 2.5f;
        [SerializeField, Min(.01f)] private float _chaseIntervalMax = 4f;
        [SerializeField, Range(0f, .25f)] private float _pitchVariation = .06f;
        [SerializeField, Range(0f, 1f)] private float _loudness = 1f;
        [SerializeField, Min(1f)] private float _longerDeafnessMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _widerScreamMultiplier = 1.2f;
        [SerializeField, Range(.1f, 1f)] private float _restlessThroatMultiplier = .75f;
        [SerializeField, Range(1, 8)] private int _curseStackCap = 3;
        public float Radius => Mathf.Max(.01f, _radius);
        public int Damage => Mathf.Max(0, _damage);
        public float DeafenSeconds => Mathf.Max(.01f, _deafenSeconds);
        public float WarningSeconds => Mathf.Max(.01f, _warningSeconds);
        public float AttackCooldown => Mathf.Max(.01f, _attackCooldown);
        public float ChaseIntervalMin => Mathf.Max(.01f, _chaseIntervalMin);
        public float ChaseIntervalMax => Mathf.Max(ChaseIntervalMin, _chaseIntervalMax);
        public float PitchVariation => Mathf.Clamp(_pitchVariation, 0f, .25f);
        public float Loudness => Mathf.Clamp01(_loudness);
        public float LongerDeafnessMultiplier => Mathf.Max(1f, _longerDeafnessMultiplier);
        public float WiderScreamMultiplier => Mathf.Max(1f, _widerScreamMultiplier);
        public float RestlessThroatMultiplier => Mathf.Clamp(_restlessThroatMultiplier, .1f, 1f);
        public int CurseStackCap => Mathf.Clamp(_curseStackCap, 1, 8);
    }
}
