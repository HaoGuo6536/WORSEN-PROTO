// ============================================================================
// HunterAnimationDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Supplies authored assets and tuning to the named Hunter engine concern.
//   Separate configurations let imported creature rigs and attack visuals change
//   without changing authoritative collision or accepted damage rules.
// ARCHITECTURAL ROLE:
//   DriverConfig (section 7d) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
//   - Tune per-archetype pose sampling, humanoid gaze and bounded foot grounding.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Shared immutable config; deterministic editor setup assigns asset references.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Animation Driver Config")]
    public sealed class HunterAnimationDriverConfig : ScriptableObject
    {
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _walk;
        [SerializeField] private AnimationClip _run;
        [SerializeField] private AnimationClip _windup;
        [SerializeField] private AnimationClip _attack;
        [SerializeField] private AnimationClip _recovery;
        [SerializeField] private float _runThreshold = 4f;
        [SerializeField] private float _blendSeconds = 0.1f;
        [SerializeField, Min(0f)] private float _sampleRate = 0f;
        [SerializeField, Range(1, 32)] private int _maximumPoseSteps = 8;
        [SerializeField, Min(0f)] private float _lookBlendInSeconds = 0.25f;
        [SerializeField, Min(0f)] private float _lookBlendOutSeconds = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _lookBodyWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _lookHeadWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _lookClampWeight = 0.5f;
        [SerializeField, Min(0f)] private float _lookTargetHeight = 1.4f;
        [SerializeField] private bool _footIK = true;
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField, Min(0f)] private float _footBlendSeconds = 0.12f;
        [SerializeField, Min(0f)] private float _footProbeUp = 0.6f;
        [SerializeField, Min(0f)] private float _footProbeDown = 1.2f;
        [SerializeField, Min(0f)] private float _footOffset = 0.03f;
        [SerializeField, Min(0f)] private float _maximumFootOffset = 0.5f;
        [SerializeField, Range(0f, 89f)] private float _footSlopeLimit = 60f;
        public float SampleRate => _sampleRate;
        public int MaximumPoseSteps => Mathf.Clamp(_maximumPoseSteps, 1, 32);
        public float LookBlendInSeconds => Mathf.Max(0f, _lookBlendInSeconds);
        public float LookBlendOutSeconds => Mathf.Max(0f, _lookBlendOutSeconds);
        public float LookBodyWeight => Mathf.Clamp01(_lookBodyWeight);
        public float LookHeadWeight => Mathf.Clamp01(_lookHeadWeight);
        public float LookClampWeight => Mathf.Clamp01(_lookClampWeight);
        public float LookTargetHeight => Mathf.Max(0f, _lookTargetHeight);
        public bool FootIK => _footIK;
        public int GroundMask => _groundMask;
        public float FootBlendSeconds => Mathf.Max(0f, _footBlendSeconds);
        public float FootProbeUp => Mathf.Max(0f, _footProbeUp);
        public float FootProbeDown => Mathf.Max(0f, _footProbeDown);
        public float FootOffset => Mathf.Max(0f, _footOffset);
        public float MaximumFootOffset => Mathf.Max(0f, _maximumFootOffset);
        public float FootSlopeLimit => Mathf.Clamp(_footSlopeLimit, 0f, 89f);
        public AnimationClip Idle => _idle;
        public AnimationClip Walk => _walk;
        public AnimationClip Run => _run;
        public AnimationClip Windup => _windup;
        public AnimationClip Attack => _attack;
        public AnimationClip Recovery => _recovery;
        public float RunThreshold => _runThreshold;
        public float BlendSeconds => _blendSeconds;
    }
}
