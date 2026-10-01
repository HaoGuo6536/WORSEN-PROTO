// ============================================================================
// WeaverDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Configures floor-plane firing probes and the ceiling body offset. The same
//   probe geometry is used for warning and launch so a ray cannot approve a web
//   that is wider than the clearance actually tested.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Hunter shared swept-shot presentation stack.
// KEY RESPONSIBILITIES:
//   - Bound deterministic candidate sweeps and author physical offsets.
// DEPENDENCIES:
//   - UnityEngine asset authoring only.
// USAGE NOTES:
//   Shared by Weaver and Blinder. Existing Resources paths and script identity
//   are retained for asset compatibility; no tunings or runtime writes change.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Weaver Driver Config")]
    public sealed class WeaverDriverConfig : ScriptableObject
    {
        [SerializeField, Min(.01f)] private float _shotHeight = 1f;
        [SerializeField, Min(.01f)] private float _candidateDistance = 2.5f;
        [SerializeField, Range(4, 32)] private int _candidateCount = 8;
        [SerializeField, Min(.01f)] private float _sampleRadius = .6f;
        [SerializeField, Min(.001f)] private float _arrivalTolerance = .05f;
        [SerializeField, Min(0f)] private float _ceilingClearance = .15f;
        public float ShotHeight => Mathf.Max(.01f, _shotHeight);
        public float CandidateDistance => Mathf.Max(.01f, _candidateDistance);
        public int CandidateCount => Mathf.Clamp(_candidateCount, 4, 32);
        public float SampleRadius => Mathf.Max(.01f, _sampleRadius);
        public float ArrivalTolerance => Mathf.Max(.001f, _arrivalTolerance);
        public float CeilingClearance => Mathf.Max(0f, _ceilingClearance);
    }
}
