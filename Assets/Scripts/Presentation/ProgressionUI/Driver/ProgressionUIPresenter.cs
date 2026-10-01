// ============================================================================
// ProgressionUIPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Formats immutable progression snapshots into readable menu cards and status.
//   Labels are plain and functional: a short title per phase, one detail line per
//   card, and no lore or flavour copy. It also prevents a single displayed revision
//   from issuing duplicate UI actions while awaiting the next authoritative response.
//   No run rules live here.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Present Session-admitted choices, purchases, inventory and shelter Bargains tersely.
//   - Group all retained hunters and effects; Hidden Count belongs to the in-level HUD.
//   - Gate terminal presentation on catch completion with a bounded, flagged timeout.
//   - Reject hidden, stale or repeated clicks and distinguish intent from purchase feedback.
//   - Format health only for selection, shelter and terminal screens.
//
// DEPENDENCIES:
//   Core progression snapshots and own ProgressionUI stack only.
//
// USAGE NOTES:
//   Stateless calculator over caller-owned DriverState; no engine calls.
//   Session supplies phase, eligibility, cost and messages. A new revision releases the latch.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Worsen.Core;

namespace Worsen.Presentation.ProgressionUI
{
    public sealed class ProgressionUIPresenter
    {
        public bool Present(ProgressionUIDriverState state, ProgressionSnapshot snapshot)
        {
            if (state.HasSnapshot && snapshot.Revision < state.Revision) return false;
            if (state.HasDeferredTerminal && snapshot.Revision < state.DeferredTerminal.Revision) return false;
            if ((state.TerminalDeferred || state.CatchCompleted) && (snapshot.GenerationId != state.DeferredGenerationId
                || snapshot.Phase == ProgressionPhase.Dormant || snapshot.Phase == ProgressionPhase.Generating
                || snapshot.Phase == ProgressionPhase.ChooseThreat || snapshot.Phase == ProgressionPhase.ChooseCurse))
                ClearTerminalDeferral(state);
            if (state.TerminalDeferred && (snapshot.Phase == ProgressionPhase.Ended || snapshot.Phase == ProgressionPhase.Shop))
            {
                state.DeferredTerminal = snapshot;
                state.HasDeferredTerminal = true;
                state.ModalVisible = false;
                return true;
            }
            bool changed = !state.HasSnapshot || snapshot.Revision != state.Revision;
            state.HasSnapshot = true;
            state.LatestSnapshot = snapshot;
            state.Hidden = false;
            if (changed) state.Pending = false;
            state.Revision = snapshot.Revision;
            state.GenerationId = snapshot.GenerationId;
            state.Phase = snapshot.Phase;
            state.ModalVisible = snapshot.Phase != ProgressionPhase.Dormant && snapshot.Phase != ProgressionPhase.Exploring;
            state.CanContinue = snapshot.CanContinue;
            state.CanRestart = snapshot.CanRestart;
            state.RoundText = "ROUND " + Number(snapshot.Round);
            state.WalletText = "WALLET  " + Number(snapshot.Wallet);
            state.HealthVisible = snapshot.Phase == ProgressionPhase.ChooseThreat || snapshot.Phase == ProgressionPhase.ChooseCurse
                || snapshot.Phase == ProgressionPhase.Shop || snapshot.Phase == ProgressionPhase.Ended
                || snapshot.Phase == ProgressionPhase.GenerationFailed;
            state.HealthText = state.HealthVisible ? "HEALTH  " + Health(snapshot.Health) + " / " + Health(snapshot.MaxHealth) : "";
            state.HealthFraction = state.HealthVisible ? Fraction(snapshot.Health, snapshot.MaxHealth) : 0f;
            state.BurdenText = Count(snapshot.ThreatCount, "THREAT") + "  ·  " + Count(snapshot.CurseCount, "CURSE");
            state.Message = snapshot.Message ?? "";
            state.Title = Title(snapshot.Phase);
            state.Subtitle = "";
            if (snapshot.Phase == ProgressionPhase.Shop && snapshot.Bargain.Pending)
            {
                state.Title = "BARGAIN";
                state.Subtitle = "Take one curse for Golden Cakes, or continue for free.";
                if ((snapshot.Bargain.Offers?.Count ?? 0) == 0)
                    state.Message = "No eligible curses. Continue for free.";
            }
            state.RetainedText = Join(Retained(snapshot), Inventory(snapshot));
            state.Cards = Cards(snapshot);
            return true;
        }


        public void Hide(ProgressionUIDriverState state)
        {
            ClearTerminalDeferral(state);
            state.Hidden = true;
        }

        public void DeferTerminal(ProgressionUIDriverState state, float seconds, EntityId player = default)
        {
            if (state.TerminalDeferred || state.CatchCompleted || state.Phase == ProgressionPhase.Ended) return;
            state.TerminalDeferred = true;
            state.TerminalRemaining = Finite(seconds) && seconds > 0f ? seconds : ProgressionUIDriverConfig.DefaultCatchTimeoutSeconds;
            state.DeferredGenerationId = state.GenerationId;
            state.CatchPlayer = player;
            state.CatchFallbackFired = false;
        }

        public bool EndCatch(ProgressionUIDriverState state, EntityId player)
        {
            if (!state.TerminalDeferred || !player.IsValid || (state.CatchPlayer.IsValid && state.CatchPlayer != player)) return false;
            return ReleaseTerminal(state);
        }

        public bool Tick(ProgressionUIDriverState state, float dt)
        {
            if (!state.TerminalDeferred || !Finite(dt) || dt <= 0f) return false;
            state.TerminalRemaining = Math.Max(0f, state.TerminalRemaining - dt);
            if (state.TerminalRemaining > 0f) return false;
            state.CatchFallbackFired = true;
            ReleaseTerminal(state);
            return true;
        }

        private bool ReleaseTerminal(ProgressionUIDriverState state)
        {
            bool pending = state.HasDeferredTerminal;
            var snapshot = state.DeferredTerminal;
            state.TerminalDeferred = state.HasDeferredTerminal = false;
            state.TerminalRemaining = 0f;
            state.DeferredTerminal = default;
            state.CatchCompleted = true;
            return pending && Present(state, snapshot);
        }

        public void ClearTerminalDeferral(ProgressionUIDriverState state)
        {
            state.TerminalDeferred = state.HasDeferredTerminal = false;
            state.CatchCompleted = state.CatchFallbackFired = false;
            state.CatchPlayer = default;
            state.TerminalRemaining = 0f;
            state.DeferredGenerationId = 0;
            state.DeferredTerminal = default;
        }

        public bool TryIssue(ProgressionUIDriverState state, ProgressionUIAction action, string id, int revision)
        {
            if (!state.HasSnapshot || state.Hidden || state.Pending || !state.ModalVisible || revision != state.Revision) return false;
            bool allowed = action == ProgressionUIAction.Continue ? state.CanContinue :
                action == ProgressionUIAction.Restart ? state.CanRestart : CardEnabled(state.Cards, action, id);
            if (!allowed) return false;
            state.Pending = true;
            return true;
        }

        public CueId? ActionFeedback(ProgressionUIAction action)
            => action == ProgressionUIAction.Purchase || action == ProgressionUIAction.ReplaceSlot ? (CueId?)null
                : action == ProgressionUIAction.Continue ? CueId.UiBack : CueId.UiConfirm;

        public bool DescribeRejectedCard(ProgressionUIDriverState state, string id, int revision)
        {
            if (!state.HasSnapshot || state.Hidden || state.Pending || !state.ModalVisible || revision != state.Revision) return false;
            foreach (var card in state.Cards)
                if (!card.Enabled && string.Equals(card.Id, id, StringComparison.Ordinal))
                { state.Message = card.Title + ": " + card.Detail; return true; }
            return false;
        }

        private static bool CardEnabled(ProgressionUICard[] cards, ProgressionUIAction kind, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var card in cards)
                if (card.Kind == kind && card.Enabled && string.Equals(card.Id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static ProgressionUICard[] Cards(ProgressionSnapshot snapshot)
        {
            if (snapshot.Phase == ProgressionPhase.Shop && snapshot.Bargain.Pending)
            {
                var offers = snapshot.Bargain.Offers;
                var cards = new ProgressionUICard[offers?.Count ?? 0];
                for (int i = 0; i < cards.Length; i++)
                    cards[i] = new ProgressionUICard(offers[i].Id, offers[i].Title, offers[i].Copy, "Curse · kept for this run",
                        "TAKE  +" + Number(offers[i].Payout), true, ProgressionUIAction.TakeBargain);
                return cards;
            }
            if (snapshot.Phase == ProgressionPhase.Shop && !string.IsNullOrEmpty(snapshot.PendingOfferId))
            {
                var replacements = new List<ProgressionUICard>();
                for (int slot = 0; slot < (snapshot.Inventory?.Count ?? 0); slot++)
                {
                    var item = snapshot.Inventory[slot];
                    replacements.Add(new ProgressionUICard(Number(slot), "SLOT " + Number(slot + 1) + " · " + item.Title,
                        "Replace with " + snapshot.PendingOfferTitle + ".", "Nothing charged yet.",
                        "REPLACE  " + Number(snapshot.PendingPrice), !string.IsNullOrEmpty(item.Id), ProgressionUIAction.ReplaceSlot));
                }
                replacements.Add(new ProgressionUICard("cancel-replacement", "KEEP ITEMS", "Cancel this purchase.", "",
                    "CANCEL", true, ProgressionUIAction.CancelReplacement));
                return replacements.ToArray();
            }
            if (snapshot.Phase == ProgressionPhase.Shop)
            {
                var offers = snapshot.Offers;
                var cards = new ProgressionUICard[offers?.Count ?? 0];
                for (int i = 0; i < cards.Length; i++)
                {
                    var offer = offers[i];
                    bool owned = offer.Purchased && !offer.Repeatable;
                    bool soldOut = offer.Repeatable && offer.StockRemaining <= 0;
                    // One line: why it is unavailable (if it is), then its kind and stock. The price is on the action.
                    string status = owned ? "Owned for this run" : soldOut ? "Sold out this visit"
                        : !string.IsNullOrEmpty(offer.UnavailableReason) ? offer.UnavailableReason
                        : !offer.CanAfford ? "Not enough currency" : "";
                    string detail = (status.Length > 0 ? status + " · " : "") +
                        (offer.Kind == EffectKind.Consumable ? "Consumable" : "Upgrade") +
                        (offer.Repeatable ? " · stock " + Number(Math.Max(0, offer.StockRemaining)) : "");
                    bool available = !owned && !soldOut && offer.CanAfford && string.IsNullOrEmpty(offer.UnavailableReason);
                    cards[i] = new ProgressionUICard(offer.Id, offer.Title, offer.Description, detail,
                        owned ? "OWNED" : soldOut ? "SOLD OUT" : "BUY  " + Number(offer.Price), available,
                        ProgressionUIAction.Purchase);
                }
                return WithReroll(cards, snapshot);
            }
            if (snapshot.Phase != ProgressionPhase.ChooseThreat && snapshot.Phase != ProgressionPhase.ChooseCurse)
                return Array.Empty<ProgressionUICard>();
            var choices = snapshot.Choices;
            var output = new ProgressionUICard[choices?.Count ?? 0];
            var kind = snapshot.Phase == ProgressionPhase.ChooseThreat ? ProgressionUIAction.ChooseThreat : ProgressionUIAction.ChooseCurse;
            for (int i = 0; i < output.Length; i++)
            {
                var choice = choices[i];
                output[i] = new ProgressionUICard(choice.Id, choice.Title, choice.Description,
                    choice.SelectedCount > 0 ? "Already retained: " + Number(choice.SelectedCount) : "",
                    "CHOOSE", !string.IsNullOrEmpty(choice.Id), kind);
            }
            return WithReroll(output, snapshot);
        }

        private static ProgressionUICard[] WithReroll(ProgressionUICard[] cards, ProgressionSnapshot snapshot)
        {
            // Optional snapshot additions preserve legacy producers until they migrate.
            if (!snapshot.CanReroll && snapshot.Inventory == null) return cards;
            var result = new List<ProgressionUICard>(cards);
            result.Add(new ProgressionUICard("reroll", "REROLL", snapshot.Phase == ProgressionPhase.Shop
                ? "New offers." : "New choices.",
                snapshot.RerollUnavailableReason ?? (Number(snapshot.FreeRerollsRemaining) + " free left"),
                "REROLL  " + Number(snapshot.RerollPrice), snapshot.CanReroll, ProgressionUIAction.Reroll));
            return result.ToArray();
        }

        private static string Join(string first, string second)
            => first.Length == 0 ? second : second.Length == 0 ? first : first + "\n\n" + second;

        private static string Inventory(ProgressionSnapshot snapshot)
        {
            if (snapshot.Inventory == null) return "";
            var text = new StringBuilder("INVENTORY");
            for (int slot = 0; slot < snapshot.Inventory.Count; slot++)
                text.Append("\n").Append(Number(slot + 1)).Append(" · ").Append(snapshot.Inventory[slot].Title ?? "Empty");
            if (!string.IsNullOrEmpty(snapshot.PendingOfferId)) text.Append("\n\nCHOOSE A SLOT TO REPLACE · ").Append(snapshot.PendingOfferTitle);
            return text.ToString();
        }

        private static string Retained(ProgressionSnapshot snapshot)
        {
            if (snapshot.Retained == null || snapshot.Retained.Count == 0) return "";
            var text = new StringBuilder();
            foreach (ProgressionChoiceKind kind in new[] { ProgressionChoiceKind.Threat, ProgressionChoiceKind.Curse, ProgressionChoiceKind.Upgrade })
            {
                bool heading = false;
                foreach (var selection in snapshot.Retained)
                {
                    if (selection.Kind != kind) continue;
                    if (!heading)
                    {
                        if (text.Length > 0) text.Append("\n\n");
                        text.Append(kind == ProgressionChoiceKind.Threat ? "THREATS" : kind == ProgressionChoiceKind.Curse ? "CURSES" : "UPGRADES");
                        heading = true;
                    }
                    text.Append("\n• ").Append(selection.Title);
                    if (selection.Count > 1) text.Append(" x").Append(Number(selection.Count));
                }
            }
            return text.ToString();
        }

        private static string Title(ProgressionPhase phase)
        {
            switch (phase)
            {
                case ProgressionPhase.ChooseThreat: return "CHOOSE A THREAT";
                case ProgressionPhase.ChooseCurse: return "CHOOSE A CURSE";
                case ProgressionPhase.Generating: return "LOADING";
                case ProgressionPhase.Shop: return "SHOP";
                case ProgressionPhase.Ended: return "RUN OVER";
                case ProgressionPhase.GenerationFailed: return "GENERATION FAILED";
                default: return "";
            }
        }

        private static string Count(int value, string noun) => Number(value) + " " + noun + (value == 1 ? "" : "S");
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Health(float value) => Finite(value) ? value.ToString("0", CultureInfo.InvariantCulture) : "—";
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Fraction(float value, float maximum) => Finite(value) && Finite(maximum) && maximum > 0f
            ? Math.Max(0f, Math.Min(1f, value / maximum)) : 0f;
    }
}
