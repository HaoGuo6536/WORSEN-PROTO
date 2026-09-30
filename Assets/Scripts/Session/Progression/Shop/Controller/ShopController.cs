// ============================================================================
// ShopController.cs
// ============================================================================
// PURPOSE:
//   Draws catalogue pedestals and resolves the inventory half of shop transactions.
//   Progression retains the wallet and active effects, applying the returned wallet
//   and replaced identity synchronously before publishing the next revision.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Progression.Shop delegated subtree.
// KEY RESPONSIBILITIES:
//   - Cycle physical slots, spend uses and release capacity only on the final use.
//   - Multiply new Golden Cake yield before rounding; carry fractions without re-multiplying them.
//   - Draw gated offers and enforce run-long Extra Life purchase history.
//   - Quote scaled/discounted prices, scarce rerolls and economy rewards.
//   - Reserve full-inventory purchases and replace exactly one slot on confirmation.
// DEPENDENCIES:
//   - Parent Progression catalogue/utility, own state/rules and Core effect/slot values.
// USAGE NOTES:
//   The Manager owns this subtree through Progression. Each visit receives a fresh
//   random source derived from the committed round seed; menus never consume layout RNG.
//   One item per pedestal per draw. Extra Pedestal adds without restocking. Discounts
//   multiply, prices round up, refunds/interest round down; fractional yield carries.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Session.Progression.Shop
{
    public sealed class ShopController
    {
        private readonly ShopBehaviorState state;
        private readonly ShopRules rules;
        private readonly EffectCatalogueConfig catalogue;
        private readonly System.Random random;
        private readonly float nothingPriceMultiplier;
        public bool HasPending => !string.IsNullOrEmpty(state.PendingOfferId);
        public EffectCatalogueEntry Pending => EffectCatalogueUtility.Find(catalogue, state.PendingOfferId);
        public ShopRules Rules => rules;

        public ShopController(ShopBehaviorState state, ShopRules rules, EffectCatalogueConfig catalogue, System.Random random,
            float nothingPriceMultiplier = ProgressionConfig.DefaultNothingShopPriceMultiplier)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.catalogue = catalogue;
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (!float.IsFinite(nothingPriceMultiplier) || nothingPriceMultiplier < 0f || nothingPriceMultiplier > 1f)
                throw new ArgumentOutOfRangeException(nameof(nothingPriceMultiplier));
            this.nothingPriceMultiplier = nothingPriceMultiplier;
            ValidateRules();
        }

        public void Reset()
        {
            state.Inventory.Clear(); state.Offers.Clear(); state.Sold.Clear();
            state.ExtraLifePurchased = false;
            state.RemainingUses.Clear(); state.SelectedSlot = 0;
            state.PendingOfferId = null; state.BargainNextVisit = state.BargainThisVisit = false;
            state.RerollsUsed = state.FreeRerollsUsed = state.PaidRerolls = state.Round = 0; state.GoldenRemainder = 0;
        }

        public void BeginVisit(int round, IReadOnlyActiveEffects active)
        {
            state.Round = round;
            state.RerollsUsed = state.FreeRerollsUsed = state.PaidRerolls = 0;
            state.PendingOfferId = null;
            state.BargainThisVisit = state.BargainNextVisit;
            state.BargainNextVisit = false;
            Draw(active);
        }

        private List<EffectCatalogueEntry> Candidates(IReadOnlyActiveEffects active)
        {
            var result = new List<EffectCatalogueEntry>();
            if (!(catalogue is null))
                foreach (var entry in catalogue.Entries)
                    if ((entry.Kind == EffectKind.Upgrade || entry.Kind == EffectKind.Consumable) &&
                        entry.Id != "field-dressing" && Eligible(entry, active)) result.Add(entry);
            return result;
        }

        private bool Eligible(EffectCatalogueEntry entry, IReadOnlyActiveEffects active) =>
            !(entry.Id == "extra-life" && state.ExtraLifePurchased)
            && !(entry.Kind == EffectKind.Upgrade && entry.Price >= rules.ExpensivePrice && state.Round < rules.ExpensiveUnlockRound)
            && EffectCatalogueUtility.Eligible(entry, state.Round, active);

        private void Draw(IReadOnlyActiveEffects active)
        {
            state.Offers.Clear(); state.Sold.Clear();
            ExpandPedestals(active);
        }

        public void ExpandPedestals(IReadOnlyActiveEffects active)
        {
            var candidates = Candidates(active);
            candidates.RemoveAll(entry => state.Offers.Contains(entry.Id));
            int count = (int)Math.Min(candidates.Count, Math.Max(0,
                (long)rules.Pedestals + (long)Stacks(active, "extra-pedestal") * rules.ExtraPedestals - state.Offers.Count));
            for (int i = 0; i < count; i++)
            {
                int index = random.Next(candidates.Count);
                state.Offers.Add(candidates[index].Id);
                candidates.RemoveAt(index);
            }
        }

        public int Price(EffectCatalogueEntry entry, IReadOnlyActiveEffects active)
        {
            if (entry == null) return 0;
            decimal scale = 1m + (decimal)rules.RoundPriceGrowth * Math.Max(0, state.Round - 1);
            return DiscountedPrice(entry.Price * scale, active);
        }

        private int DiscountedPrice(decimal price, IReadOnlyActiveEffects active) =>
            (int)Math.Min(int.MaxValue, Math.Ceiling(price *
                (state.BargainThisVisit ? 1m - (decimal)rules.BargainDiscount : 1m) *
                (active.Has(new EffectId("nothing")) ? (decimal)nothingPriceMultiplier : 1m) *
                Math.Max(0m, 1m - (decimal)rules.LoyaltyDiscount * Stacks(active, "loyalty-card"))));

        public IReadOnlyList<ProgressionOffer> Offers(int wallet, IReadOnlyActiveEffects active)
        {
            var result = new List<ProgressionOffer>();
            foreach (string id in state.Offers)
            {
                if (id == "extra-life" && state.ExtraLifePurchased) continue;
                var entry = EffectCatalogueUtility.Find(catalogue, id);
                string reason = Unavailable(entry, wallet, active);
                bool sold = state.Sold.Contains(id);
                result.Add(new ProgressionOffer(id, entry.Title, entry.CardCopy, Price(entry, active), sold,
                    reason == null, sold ? 0 : 1, true, reason, entry.Axis, entry.Kind));
            }
            return result.AsReadOnly();
        }

        private string Unavailable(EffectCatalogueEntry entry, int wallet, IReadOnlyActiveEffects active, bool confirming = false)
        {
            if (HasPending && !confirming) return "Choose a replacement slot or cancel first.";
            if (entry == null || !state.Offers.Contains(entry.Id)) return "That pedestal is unavailable.";
            if (state.Sold.Contains(entry.Id)) return "Sold out on this pedestal.";
            if (!Eligible(entry, active)) return "Requirements, availability or stack cap not met.";
            if (wallet < Price(entry, active)) return "Not enough Golden Cakes.";
            return null;
        }

        public bool Purchase(string id, int wallet, IReadOnlyActiveEffects active, out int walletAfter,
            out string removedId, out string reason, int replacementSlot = -1)
        {
            walletAfter = wallet; removedId = null;
            var entry = EffectCatalogueUtility.Find(catalogue, id);
            bool confirming = replacementSlot >= 0;
            reason = Unavailable(entry, wallet, active, confirming);
            if (reason != null) return false;
            if (confirming && (!HasPending || state.PendingOfferId != id || replacementSlot >= state.Inventory.Count ||
                string.IsNullOrEmpty(state.Inventory[replacementSlot].Id)))
            { reason = "Choose an occupied inventory slot."; return false; }
            int slot = -1;
            if (entry.Kind == EffectKind.Consumable)
            {
                EnsureSlots(active);
                slot = confirming ? replacementSlot : state.Inventory.FindIndex(item => string.IsNullOrEmpty(item.Id));
                if (slot < 0)
                { state.PendingOfferId = id; return true; }
            }
            int price = Price(entry, active);
            int refund = 0;
            if (slot >= 0)
            {
                var previous = state.Inventory[slot];
                removedId = previous.Id;
                if (Stacks(active, "refund") > 0)
                    refund = (int)Math.Floor(previous.PaidPrice * (decimal)rules.RefundFraction);
                state.Inventory[slot] = new ProgressionInventorySlot(entry.Id, entry.Title, price);
                state.RemainingUses[slot] = UsesFor(entry.Id);
            }
            walletAfter = (int)Math.Min(int.MaxValue, (long)wallet - price + refund);
            state.Sold.Add(id); state.PendingOfferId = null;
            if (id == "extra-life") state.ExtraLifePurchased = true;
            if (id == "bargain-hunter") state.BargainNextVisit = true;
            return true;
        }

        public bool CancelReplacement()
        {
            if (!HasPending) return false;
            state.PendingOfferId = null;
            return true;
        }

        public int FreeRerolls(IReadOnlyActiveEffects active) => (int)Math.Max(0,
            Math.Min(int.MaxValue, (long)rules.FreeRerolls + (long)Stacks(active, "shop-reroll") * rules.ShopRerolls) - state.FreeRerollsUsed);

        public int RerollPrice(IReadOnlyActiveEffects active) => FreeRerolls(active) > 0 ? 0 :
            DiscountedPrice((decimal)rules.RerollPrice + (decimal)state.PaidRerolls * rules.RerollIncrease, active);

        public string RerollUnavailable(int wallet, IReadOnlyActiveEffects active)
        {
            if (HasPending) return "Choose a replacement slot or cancel first.";
            if (state.RerollsUsed == int.MaxValue) return "No rerolls remain.";
            if (Candidates(active).Count == 0) return "No eligible catalogue offers.";
            return wallet < RerollPrice(active) ? "Not enough Golden Cakes." : null;
        }

        public bool Reroll(int wallet, IReadOnlyActiveEffects active, out int walletAfter)
        {
            walletAfter = wallet;
            if (RerollUnavailable(wallet, active) != null) return false;
            int price = RerollPrice(active);
            if (FreeRerolls(active) == 0) state.PaidRerolls++;
            else state.FreeRerollsUsed++;
            state.RerollsUsed++;
            walletAfter -= price;
            Draw(active);
            return true;
        }

        private void EnsureSlots(IReadOnlyActiveEffects active)
        {
            int capacity = checked(rules.InventorySlots + Stacks(active, "bigger-pockets") * rules.BiggerPocketsSlots);
            while (state.Inventory.Count < capacity) state.Inventory.Add(default);
        }

        public IReadOnlyList<ProgressionInventorySlot> Inventory(IReadOnlyActiveEffects active)
        {
            EnsureSlots(active);
            return Array.AsReadOnly(state.Inventory.ToArray());
        }

        private int UsesFor(string id) => id == "firecracker" ? rules.FirecrackerUses :
            id == "doorstop" ? rules.DoorstopUses : rules.SingleItemUses;

        public ConsumableInventorySnapshot Consumables(IReadOnlyActiveEffects active)
        {
            var inventory = Inventory(active);
            var uses = new int[inventory.Count];
            for (int i = 0; i < uses.Length; i++) uses[i] = Remaining(i);
            return new ConsumableInventorySnapshot(inventory, Array.AsReadOnly(uses), state.SelectedSlot);
        }

        public bool Cycle(int direction, IReadOnlyActiveEffects active)
        {
            EnsureSlots(active);
            if (direction == 0 || state.Inventory.Count == 0) return false;
            state.SelectedSlot = (state.SelectedSlot + (direction > 0 ? 1 : state.Inventory.Count - 1)) % state.Inventory.Count;
            return true;
        }

        private int Remaining(int slot) => slot < 0 || slot >= state.Inventory.Count ||
            string.IsNullOrEmpty(state.Inventory[slot].Id) ? 0 :
            state.RemainingUses.TryGetValue(slot, out int count) ? count : UsesFor(state.Inventory[slot].Id);

        public bool ConsumeSelected(string expectedId, out bool exhausted)
        {
            exhausted = false;
            int slot = state.SelectedSlot;
            if (Remaining(slot) <= 0 || state.Inventory[slot].Id != expectedId) return false;
            int remaining = Remaining(slot) - 1;
            state.RemainingUses[slot] = remaining;
            exhausted = remaining == 0;
            if (exhausted) state.Inventory[slot] = default;
            return true;
        }

        public bool ConsumeWard()
        {
            int slot = state.Inventory.FindIndex(item => item.Id == "wax-ward");
            if (slot < 0) return false;
            state.Inventory[slot] = default;
            state.RemainingUses.Remove(slot);
            return true;
        }

        public int SelectionRerolls(IReadOnlyActiveEffects active) =>
            (int)Math.Min(int.MaxValue, (long)Stacks(active, "lucky-reroll") * rules.LuckyRerolls);


        public int Interest(int wallet, IReadOnlyActiveEffects active) => Stacks(active, "interest") == 0 ? 0 :
            (int)Math.Min(int.MaxValue - wallet, Math.Min(rules.InterestCap, Math.Floor(wallet * (decimal)rules.InterestFraction)));

        public bool GoldenCredit(int wallet, int baseValue, IReadOnlyActiveEffects active, out int credit, float shrineMultiplier = 1f)
        {
            if (!float.IsFinite(shrineMultiplier) || shrineMultiplier < 1f || shrineMultiplier > 2f) { credit = 0; return false; }
            decimal yield = ((decimal)baseValue + (decimal)Stacks(active, "golden-touch") * rules.GoldenTouchBonus)
                * (1m + (decimal)rules.BusinessYieldPerStack * Stacks(active, "business-license"))
                * (decimal)shrineMultiplier + state.GoldenRemainder;
            if (Math.Floor(yield) > int.MaxValue - wallet) { credit = 0; return false; }
            credit = (int)Math.Floor(yield);
            state.GoldenRemainder = yield - credit;
            return true;
        }

        private int Stacks(IReadOnlyActiveEffects active, string id)
        {
            var entry = EffectCatalogueUtility.Find(catalogue, id);
            return Math.Min(entry?.StackCap ?? 0, active.Stacks(new EffectId(id)));
        }

        private void ValidateRules()
        {
            if (rules.FirecrackerUses < 1 || rules.DoorstopUses < 1 || rules.SingleItemUses < 1 ||
                rules.Pedestals < 1 || rules.InventorySlots < 1 || rules.FreeRerolls < 0 || rules.RerollPrice < 0 ||
                rules.RerollIncrease < 0 || rules.ExpensivePrice < 0 || rules.ExpensiveUnlockRound < 1 ||
                rules.BiggerPocketsSlots < 0 || rules.LuckyRerolls < 0 || rules.ShopRerolls < 0 ||
                rules.GoldenTouchBonus < 0 || rules.InterestCap < 0 || rules.ExtraPedestals < 0 ||
                float.IsNaN(rules.RoundPriceGrowth) || float.IsInfinity(rules.RoundPriceGrowth) || rules.RoundPriceGrowth < 0)
                throw new ArgumentException("Invalid shop counts or round pricing.");
            foreach (float value in new[] { rules.BargainDiscount, rules.LoyaltyDiscount,
                rules.InterestFraction, rules.RefundFraction, rules.BusinessYieldPerStack })
                if (float.IsNaN(value) || value < 0 || value > 1) throw new ArgumentException("Invalid shop economy fraction.");
        }
    }
}
