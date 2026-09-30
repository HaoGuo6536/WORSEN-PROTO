// ============================================================================
// ShrineProgressionConfig.cs
// ============================================================================
// PURPOSE:
//   Authors provisional shrine rewards and costs; Echo repeats their normal rules.
//   The data remains separate from active deals, effect stacks and delayed noises.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Tune Protection, Bargain, Pacification, Wick and Purgatory rules.
//   - Preserve source compatibility while preventing amplified Echo outcomes.
// DEPENDENCIES:
//   - Core shrine values and Unity serialization only.
// USAGE NOTES:
//   Default rules are also usable by pure tests. Magnitude means Chance draw count,
//   Bargain payout, noise loudness, Wick duration, Passage yield, shield HP or mutation chance.
//   Echo itself is not a replay target; history retains the last resolved non-Echo kind.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Session.Progression
{
    [Serializable]
    public sealed class ShrineEchoRule
    {
        [SerializeField] private ShrineKind _kind;
        public ShrineEchoRule(ShrineKind kind, int magnitude = 1, int cost = 1, float delay = 1f)
        { _kind = kind; }
        public ShrineKind Kind => _kind;
        public int Magnitude => 1;
        public int CostMultiplier => 1;
        public float DelayMultiplier => 1f;
    }
    [Serializable]
    public sealed class ShrineProgressionRules
    {
        [SerializeField, Min(0)] private int _protectionCost = 4;
        [SerializeField, Min(1)] private int _costFloorInterval = 3;
        [SerializeField, Min(0f)] private float _shieldHitPoints = 40f;
        [SerializeField, Min(1)] private int _bargainOffers = 3;
        [SerializeField, Min(0)] private int _bargainBase = 2;
        [SerializeField, Min(1)] private int _bargainFloorInterval = 2;
        [SerializeField, Min(0f)] private float _pacificationDelay = 1.5f;
        [SerializeField, Min(0f)] private float _pacificationLoudness = 20f;
        [SerializeField, Min(0f)] private float _wickSeconds = 10f;
        [SerializeField, Range(0f, 1f)] private float _mutationChance = 0.25f;
        [SerializeField] private ShrineEchoRule[] _echo =
        {
            new ShrineEchoRule(ShrineKind.Chance), new ShrineEchoRule(ShrineKind.Bargain),
            new ShrineEchoRule(ShrineKind.Pacification), new ShrineEchoRule(ShrineKind.Wick),
            new ShrineEchoRule(ShrineKind.Passage), new ShrineEchoRule(ShrineKind.Protection),
            new ShrineEchoRule(ShrineKind.Purgatory)
        };
        public int ProtectionCost => _protectionCost;
        public int CostFloorInterval => _costFloorInterval;
        public float ShieldHitPoints => _shieldHitPoints;
        public int BargainOffers => _bargainOffers;
        public int BargainBase => _bargainBase;
        public int BargainFloorInterval => _bargainFloorInterval;
        public float PacificationDelay => _pacificationDelay;
        public float PacificationLoudness => _pacificationLoudness;
        public float WickSeconds => _wickSeconds;
        public float MutationChance => _mutationChance;
        public IReadOnlyList<ShrineEchoRule> Echo => Array.AsReadOnly(_echo);
    }
    [CreateAssetMenu(menuName = "Worsen/Progression/Shrine Config")]
    public sealed class ShrineProgressionConfig : ScriptableObject
    {
        [SerializeField] private ShrineProgressionRules _rules = new ShrineProgressionRules();
        public ShrineProgressionRules Rules => _rules;
    }
}
