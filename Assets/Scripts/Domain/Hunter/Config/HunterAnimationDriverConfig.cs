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
