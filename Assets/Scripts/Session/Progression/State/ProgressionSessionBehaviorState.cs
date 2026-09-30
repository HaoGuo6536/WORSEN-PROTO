// ============================================================================
// ProgressionSessionBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains the expedition independently of any generated floor or scene.
//   The owning controller changes this plain data when choices, collection,
//   purchases, floor completion or death are accepted.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Store round identity, wallet, baseline health and legacy loadout effects.
//   - Retain committed hunter/curse offers and stage-specific selection reroll counts.
//   - Retain catalogue stacks separately from lifetime purchase/selection counts.
//   - Own delegated shop state without moving the wallet out of Progression.
// DEPENDENCIES:
//   - Core progression types, System collections and the owned Shop state.
// USAGE NOTES:
//   Persistent only through ProgressionSessionManager. No scene references,
//   subscriptions or engine calls are stored here. Foreign systems receive
//   immutable Core snapshots and never read this mutable state directly.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
using Worsen.Session.Progression.Shop;

namespace Worsen.Session.Progression
{
    public sealed class ProgressionSessionBehaviorState
    {
        public int Revision { get; internal set; }
        public int GenerationId { get; internal set; }
        public int Seed { get; internal set; }
        public int RoundSeed { get; internal set; }
        public int Round { get; internal set; }
        public int CompletedCombatFloors { get; internal set; }
        public int LastShopAtCombatCount { get; internal set; }
        public ProgressionTraits Traits { get; internal set; }
        public int WaxWardCharges { get; internal set; }
        public int Wallet { get; internal set; }
        public int ThreatCount { get; internal set; }
        public int CurseCount { get; internal set; }
        public bool IsShop { get; internal set; }
        public ProgressionPhase Phase { get; internal set; }
        public float Health { get; internal set; }
        public float MaximumHealth { get; internal set; }
        public float MovementSpeedMultiplier { get; internal set; } = 1f;
        public float HunterSpeedMultiplier { get; internal set; } = 1f;
        public float FogDensityMultiplier { get; internal set; } = 1f;
        public float FlashlightRangeMultiplier { get; internal set; } = 1f;
        public string Message { get; internal set; } = string.Empty;
        internal Dictionary<string, int> SelectionCounts { get; } = new Dictionary<string, int>();
        internal Dictionary<string, ActiveEffect> ActiveEffectEntries { get; } = new Dictionary<string, ActiveEffect>();
        internal ShopBehaviorState Shop { get; } = new ShopBehaviorState();
        internal int ThreatRerollsUsed, CurseRerollsUsed;
        internal List<string> ActiveThreatIds { get; } = new List<string>();
        internal List<string> OfferedThreatIds { get; } = new List<string>();
        internal List<string> OfferedCurseIds { get; } = new List<string>();
        internal HashSet<int> CollectedGoldenAnchors { get; } = new HashSet<int>();
    }
}
