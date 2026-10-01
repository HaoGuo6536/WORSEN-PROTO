// ============================================================================
// StareConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Stare's attention window, edge placement and return cadence.
//   The four shared chase tunables remain on HunterProfile; this config supplies
//   the harder loss multiplier and the archetype-specific curse hooks.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Supply provisional rule timing, placement, sound ids and capped scaling.
// DEPENDENCIES:
//   - HunterArchetypeConfig and Unity asset authoring only.
// USAGE NOTES:
//   No clips required. Half-window repeats the current encounter's voice line.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Stare Rules")]
    public sealed class StareConfig : HunterArchetypeConfig
    {
        [SerializeField, Min(.1f)] private float _windowSeconds = 8f;
        [SerializeField, Min(.1f)] private float _holdSeconds = 1f;
        [SerializeField, Min(.1f)] private float _returnSeconds = 25f;
        [SerializeField, Min(1f)] private float _distance = 6f;
        [SerializeField, Range(.1f, .95f)] private float _edgeFraction = .82f;
        [SerializeField, Range(1f, 30f)] private float _lookHalfAngle = 8f;
        [SerializeField, Min(.1f)] private float _observationHeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _subversionChance = .03f;
        [SerializeField, Range(91f, 170f)] private float _hiddenAngle = 120f;
        [SerializeField, Min(1f)] private float _lossMultiplier = 2f;
        [SerializeField, Range(.1f, 1f)] private float _shorterWindowMultiplier = .8f;
        [SerializeField, Range(.1f, 1f)] private float _quieterCallMultiplier = .7f;
        [SerializeField, Range(.1f, 1f)] private float _soonerReturnMultiplier = .8f;
        [SerializeField, Range(0f, .3f)] private float _wanderFractionPerStack = .04f;
        [SerializeField, Min(.1f)] private float _wanderSeconds = 2f;
        [SerializeField] private string _callId = "stare.i-see-you";
        [SerializeField] private string _hiddenCallId = "stare.find-me";
        [SerializeField] private string _chaseId = "stare.chase";
        [SerializeField] private string _attackId = "stare.attack";
        [SerializeField] private string _deathId = "stare.death";
        public float WindowSeconds => Mathf.Max(.1f, _windowSeconds);
        public float HoldSeconds => Mathf.Max(.1f, _holdSeconds);
        public float ReturnSeconds => Mathf.Max(.1f, _returnSeconds);
        public float Distance => Mathf.Max(1f, _distance);
        public float EdgeFraction => Mathf.Clamp(_edgeFraction, .1f, .95f);
        public float LookHalfAngle => Mathf.Clamp(_lookHalfAngle, 1f, 30f);
        public float ObservationHeight => Mathf.Max(.1f, _observationHeight);
        public float SubversionChance => Mathf.Clamp01(_subversionChance);
        public float HiddenAngle => Mathf.Clamp(_hiddenAngle, 91f, 170f);
        public float LossMultiplier => Mathf.Max(1f, _lossMultiplier);
        public float ShorterWindowMultiplier => Mathf.Clamp(_shorterWindowMultiplier, .1f, 1f);
        public float QuieterCallMultiplier => Mathf.Clamp(_quieterCallMultiplier, .1f, 1f);
        public float SoonerReturnMultiplier => Mathf.Clamp(_soonerReturnMultiplier, .1f, 1f);
        public float WanderFractionPerStack => Mathf.Clamp(_wanderFractionPerStack, 0f, .3f);
        public float WanderSeconds => Mathf.Max(.1f, _wanderSeconds);
        public string CallId => _callId;
        public string HiddenCallId => _hiddenCallId;
        public string ChaseId => _chaseId;
        public string AttackId => _attackId;
        public string DeathId => _deathId;
    }
}
