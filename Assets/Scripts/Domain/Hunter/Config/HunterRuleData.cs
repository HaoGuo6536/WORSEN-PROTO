// ============================================================================
// HunterRuleData.cs
// ============================================================================
// PURPOSE:
//   Provides serializable entries for an archetype's habits and mutation pool.
//   These immutable-at-runtime records belong to HunterProfile; individual hunters
//   keep timers and overrides separately so one entity never edits shared content.
// ARCHITECTURAL ROLE:
//   Content SO data (section 4b) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Author habit enable flags, threshold duration and cake reaction radius.
//   - Define selectable rule/value/tell entries without choosing progression events.
// DEPENDENCIES:
//   - Hunter-local rule identifiers and UnityEngine serialization only.
// USAGE NOTES:
//   First entry of each habit kind wins. Missing entries disable that habit.
//   Pool entries are validated by HunterController when applied, never at selection here.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    [Serializable]
    public sealed class HunterHabitData
    {
        [SerializeField] private HunterHabitKind _kind;
        [SerializeField] private bool _enabled = true;
        [SerializeField, Min(0f)] private float _pauseSeconds = 0.4f;
        [SerializeField, Min(0f)] private float _radius = 15f;
        public HunterHabitData(HunterHabitKind kind, bool enabled = true)
        { _kind = kind; _enabled = enabled; }
        public HunterHabitKind Kind => _kind;
        public bool Enabled => _enabled;
        public float PauseSeconds => Mathf.Max(0f, _pauseSeconds);
        public float Radius => Mathf.Max(0f, _radius);
    }
    [Serializable]
    public sealed class HunterMutationData
    {
        [SerializeField] private HunterTunable _tunable = HunterTunable.ChaseSpeedMultiplier;
        [SerializeField] private float _value = 1.12f;
        [SerializeField] private string _tellId = string.Empty;
        public HunterMutation Mutation => new HunterMutation(_tunable, _value, _tellId);
    }
}
