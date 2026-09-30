// ============================================================================
// ShrineProgressionBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains shrine history and the current floor's temporary effects and hearing queue.
//   A marked bargain survives escape until the shelter accepts or dismisses it.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Separate run history and pending deals from floor-scoped stacks and replay guards.
// DEPENDENCIES:
//   - Core shrine/effect values and System collections only.
// USAGE NOTES:
//   Owned only by Progression; snapshots copy collections before publication.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Session.Progression
{
    public sealed class ShrineProgressionBehaviorState
    {
        internal readonly HashSet<int> Seen = new HashSet<int>();
        internal readonly Dictionary<string, ActiveEffect> FloorEffects = new Dictionary<string, ActiveEffect>();
        internal readonly List<ShrineResolvedFact> History = new List<ShrineResolvedFact>();
        internal readonly List<(ShrineActivatedFact Fact, double Due, float Loudness)> Noises =
            new List<(ShrineActivatedFact, double, float)>();
        internal readonly List<ShrineDealOffer> Offers = new List<ShrineDealOffer>();
        internal int BargainFloor, BargainMultiplier = 1;
        internal bool BargainMarked;
        internal double Clock;
        internal long LastTick = -1;
        internal float YieldMultiplier = 1f;
    }
}
