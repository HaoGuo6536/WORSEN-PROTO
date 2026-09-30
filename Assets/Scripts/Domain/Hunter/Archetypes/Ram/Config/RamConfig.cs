// ============================================================================
// RamConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Ram's stamp, fixed charge and punishable recovery rule.
//   Shared profile fields still author approach inertia, commitment, speed and loss.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter Ram.
// KEY RESPONSIBILITIES:
//   - Keep charge distances, timing, curse factors and sound identifiers immutable.
// DEPENDENCIES:
//   - Hunter archetype config and UnityEngine authoring only.
// USAGE NOTES:
//   All values are provisional. Numeric curses cap at three, binary hooks at one.
//   Sound identifiers require the Audio owner's cue mapping; no assets are loaded here.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Ram
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Ram Rules")]
    public sealed class RamConfig : HunterArchetypeConfig
    {
        [SerializeField, Min(.01f)] private float _windupSeconds = 1f;
        [SerializeField, Min(.01f)] private float _chargeSpeed = 18f;
        [SerializeField, Min(.01f)] private float _chargeDistance = 18f;
        [SerializeField, Min(.01f)] private float _staggerSeconds = 1.2f;
        [SerializeField, Min(.01f)] private float _strideMeters = 3f;
        [SerializeField, Range(0f, 1f)] private float _glancingDotThreshold = .5f;
        [SerializeField, Min(0f)] private float _knockbackSpeed = 8f;
        public float GlancingDotThreshold => Mathf.Clamp01(_glancingDotThreshold);
        public float KnockbackSpeed => Mathf.Max(0f, _knockbackSpeed);
        [SerializeField, Min(1f)] private float _longerChargeMultiplier = 1.25f;
        [SerializeField, Range(.1f, 1f)] private float _shorterWindupMultiplier = .8f;
        [SerializeField] private bool _partitionBreakerVariant = false;
        [SerializeField] private string _stampSound = "ram-stamp";
        [SerializeField] private string _bellowSound = "ram-bellow";
        [SerializeField] private string _strideSound = "ram-stride";
        [SerializeField] private string _impactSound = "ram-impact";
        [SerializeField] private string _winSound = "ram-win";
        public float WindupSeconds => Mathf.Max(.01f, _windupSeconds);
        public float ChargeSpeed => Mathf.Max(.01f, _chargeSpeed);
        public float ChargeDistance => Mathf.Max(.01f, _chargeDistance);
        public float StaggerSeconds => Mathf.Max(.01f, _staggerSeconds);
        public float StrideMeters => Mathf.Max(.01f, _strideMeters);
        public float LongerChargeMultiplier => Mathf.Max(1f, _longerChargeMultiplier);
        public float ShorterWindupMultiplier => Mathf.Clamp(_shorterWindupMultiplier, .1f, 1f);
        public bool PartitionBreakerVariant => _partitionBreakerVariant;
        public string StampSound => _stampSound;
        public string BellowSound => _bellowSound;
        public string StrideSound => _strideSound;
        public string ImpactSound => _impactSound;
        public string WinSound => _winSound;
    }
}
