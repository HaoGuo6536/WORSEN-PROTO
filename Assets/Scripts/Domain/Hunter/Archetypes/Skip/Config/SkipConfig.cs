// ============================================================================
// SkipConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the Skip's route-learning and silent interception cadence.
//   It is an annoyance, never a fast pursuer; its shared profile sets slow foot speed.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Hunter Skip.
// KEY RESPONSIBILITIES:
//   - Store provisional threshold, cooldown and capped curse modifiers.
// DEPENDENCIES:
//   - Hunter archetype config and UnityEngine authoring only.
// USAGE NOTES:
//   Silence is the sound set: neither teleport nor its mark produces an audio cue.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Skip
{
    [CreateAssetMenu(menuName = "Worsen/Hunter/Skip Rules")]
    public sealed class SkipConfig : HunterArchetypeConfig
    {
        [SerializeField, Min(1)] private int _usesRequired = 3;
        [SerializeField, Min(.01f)] private float _cooldownSeconds = 12f;
        [SerializeField, Range(.1f, 1f)] private float _shorterCooldownMultiplier = .8f;
        public int UsesRequired => Mathf.Max(1, _usesRequired);
        public float CooldownSeconds => Mathf.Max(.01f, _cooldownSeconds);
        public float ShorterCooldownMultiplier => Mathf.Clamp(_shorterCooldownMultiplier, .1f, 1f);
    }
}
