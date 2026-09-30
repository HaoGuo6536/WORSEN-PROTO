// ============================================================================
// ShopBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains shop visits and held items independently of generated room objects.
//   A pending offer is only a reservation; no money or item moves until the
//   player confirms which occupied slot to replace.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Progression.Shop delegated subtree.
// KEY RESPONSIBILITIES:
//   - Store drawn identities, sold pedestals, reroll counters and inventory receipts.
//   - Retain the next-shop discount and fractional Golden Cake yield across floors.
// DEPENDENCIES:
//   - Core immutable inventory values and System collections only.
// USAGE NOTES:
//   Owned through ProgressionSessionManager. No engine references or events.
//   Progression passes this subtree state to its controller; consumers see snapshots.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Session.Progression.Shop
{
    public sealed class ShopBehaviorState : IReadOnlyShopState
    {
        public string PendingOfferId { get; internal set; }
        internal int Round, RerollsUsed, FreeRerollsUsed, PaidRerolls;
        internal bool BargainNextVisit, BargainThisVisit;
        internal decimal GoldenRemainder;
        internal List<string> Offers { get; } = new List<string>();
        internal HashSet<string> Sold { get; } = new HashSet<string>();
        internal List<ProgressionInventorySlot> Inventory { get; } = new List<ProgressionInventorySlot>();
    }
}
