// ============================================================================
// MimicConfig.cs
// ============================================================================
// PURPOSE:
//   Authors a stationary false cake and a short one-shot bite hold.
//   Arrow betrayal is deliberately a separate owner-controlled, default-off hook.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter Mimic.
// KEY RESPONSIBILITIES:
//   - Store bite tuning, golden chance, curse scaling and sound-set identifiers.
// DEPENDENCIES:
//   - Hunter archetype config and UnityEngine authoring only.
// USAGE NOTES:
//   Silent until touched; no presence, detection or chase cue may reveal the cake.
//   Numeric curses cap at three. Damage travels through the normal HunterHit path.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Mimic
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Mimic Rules")]
    public sealed class MimicConfig : HunterArchetypeConfig
    {
        [SerializeField, Min(.01f)] private float _biteSeconds = 1.2f;
        [SerializeField, Min(0)] private int _biteDamage = 25;
        [SerializeField, Min(.01f)] private float _touchRadius = .65f;
        [SerializeField, Min(1f)] private float _longerBiteMultiplier = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _goldenChance = .25f;
        [SerializeField] private bool _allowFaithlessArrow = false;
        [SerializeField, Min(.01f)] private float _faithlessInterval = 20f;
        [SerializeField, Min(.01f)] private float _faithlessSeconds = 2f;
        [SerializeField] private string _biteSound = "mimic-wrong-bite";
        [SerializeField] private string _winSound = "mimic-win";
        public float BiteSeconds => Mathf.Max(.01f, _biteSeconds);
        public int BiteDamage => Mathf.Max(0, _biteDamage);
        public float TouchRadius => Mathf.Max(.01f, _touchRadius);
        public float LongerBiteMultiplier => Mathf.Max(1f, _longerBiteMultiplier);
        public float GoldenChance => Mathf.Clamp01(_goldenChance);
        public bool AllowFaithlessArrow => _allowFaithlessArrow;
        public float FaithlessInterval => Mathf.Max(.01f, _faithlessInterval);
        public float FaithlessSeconds => Mathf.Max(.01f, _faithlessSeconds);
        public string BiteSound => _biteSound;
        public string WinSound => _winSound;
    }
}
