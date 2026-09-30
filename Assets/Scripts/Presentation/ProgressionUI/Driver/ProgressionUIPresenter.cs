// ============================================================================
// ProgressionUIPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Formats immutable progression snapshots into readable menu cards and status.
//   It also prevents a single displayed revision from issuing duplicate UI
//   actions while awaiting the next authoritative response. No run rules live here.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Preserve unique ownership, consumable stock and authoritative rejection reasons.
//   - Display catalogue axes, priced rerolls, inventory and deferred replacement choices.
//   - Trust Session-admitted selection cards, including stackable curses after rerolls.
//   - Group retained choices without truncation; distinguish UI intent from committed purchase audio.
//   - Redact retained hunters and their count when the explicit Hidden Count hook is enabled.
//   - Gate terminal and shelter presentation on catch completion with a bounded, flagged timeout.
//   - Reject hidden, stale or repeated UI clicks using the displayed snapshot.
//   - Format health only for selection, shelter and terminal screens, never a live floor or generation.
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
            state.BurdenText = (state.HideActiveHunters ? "HUNTERS HIDDEN" : Number(snapshot.ThreatCount) + " THREATS")
                + "  /  " + Number(snapshot.CurseCount) + " CURSES";
            state.Message = snapshot.Message ?? "";
            state.Title = Title(snapshot.Phase);
            state.Subtitle = Subtitle(snapshot.Phase);
            state.RetainedText = Retained(snapshot, state.HideActiveHunters) + Inventory(snapshot);
            state.Cards = Cards(snapshot);
            return true;
        }

        public void SetHiddenCount(ProgressionUIDriverState state, bool hidden)
        {
            state.HideActiveHunters = hidden;
            bool wasHidden = state.Hidden;
            if (state.HasSnapshot) Present(state, state.LatestSnapshot);
            state.Hidden = wasHidden;
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
            if (snapshot.Phase == ProgressionPhase.Shop && !string.IsNullOrEmpty(snapshot.PendingOfferId))
            {
                var replacements = new List<ProgressionUICard>();
                for (int slot = 0; slot < (snapshot.Inventory?.Count ?? 0); slot++)
                {
                    var item = snapshot.Inventory[slot];
                    replacements.Add(new ProgressionUICard(Number(slot), "SLOT " + Number(slot + 1) + " · " + item.Title,
                        "Discard this item for " + snapshot.PendingOfferTitle + ". The discarded item is gone.",
                        "Pending purchase · " + Number(snapshot.PendingPrice) + " coins. Nothing charged yet.",
                        "REPLACE  " + Number(snapshot.PendingPrice), !string.IsNullOrEmpty(item.Id), ProgressionUIAction.ReplaceSlot));
                }
                replacements.Add(new ProgressionUICard("cancel-replacement", "KEEP YOUR INVENTORY", "Cancel this purchase.",
                    "No debit and no refund. Keep every held item.", "CANCEL", true, ProgressionUIAction.CancelReplacement));
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
                    string status = owned ? "Owned for this run" : soldOut ? "Sold out this visit"
                        : !string.IsNullOrEmpty(offer.UnavailableReason) ? offer.UnavailableReason
                        : !offer.CanAfford ? "Not enough currency" : "Available";
                    string detail = status + "\nPrice · " + Number(offer.Price) + " coins\n" +
                        (offer.Kind == EffectKind.Consumable ? "Consumable · inventory slot" : "Upgrade · kept for this run") +
                        (offer.Repeatable ? " · stock " + Number(Math.Max(0, offer.StockRemaining)) : "") +
                        (offer.Axis == FearAxis.None ? "" : "\nFear axis · " + offer.Axis);
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
                    choice.SelectedCount > 0 ? "Already retained: " + Number(choice.SelectedCount) : "New to this expedition",
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
                ? "Draw a new set of eligible pedestals." : "Draw new eligible choices.",
                snapshot.RerollUnavailableReason ?? (Number(snapshot.FreeRerollsRemaining) + " free rerolls remaining"),
                "REROLL  " + Number(snapshot.RerollPrice), snapshot.CanReroll, ProgressionUIAction.Reroll));
            return result.ToArray();
        }

        private static string Inventory(ProgressionSnapshot snapshot)
        {
            if (snapshot.Inventory == null) return "";
            var text = new StringBuilder("\n\nINVENTORY");
            for (int slot = 0; slot < snapshot.Inventory.Count; slot++)
                text.Append("\n").Append(Number(slot + 1)).Append(" · ").Append(snapshot.Inventory[slot].Title ?? "Empty");
            if (!string.IsNullOrEmpty(snapshot.PendingOfferId)) text.Append("\n\nCHOOSE A SLOT TO REPLACE · ").Append(snapshot.PendingOfferTitle);
            return text.ToString();
        }

        private static string Retained(ProgressionSnapshot snapshot, bool hideHunters)
        {
            if (snapshot.Retained == null || snapshot.Retained.Count == 0) return "No retained choices yet.";
            var text = new StringBuilder();
            foreach (ProgressionChoiceKind kind in new[] { ProgressionChoiceKind.Threat, ProgressionChoiceKind.Curse, ProgressionChoiceKind.Upgrade })
            {
                if (hideHunters && kind == ProgressionChoiceKind.Threat) continue;
                bool heading = false;
                foreach (var selection in snapshot.Retained)
                {
                    if (selection.Kind != kind) continue;
                    if (!heading)
                    {
                        if (text.Length > 0) text.Append("\n\n");
                        text.Append(kind == ProgressionChoiceKind.Threat ? "FOLLOWING YOU" : kind == ProgressionChoiceKind.Curse ? "YOUR CURSES" : "YOUR EQUIPMENT");
                        heading = true;
                    }
                    text.Append("\n• ").Append(selection.Title);
                    if (selection.Count > 1) text.Append(" x").Append(Number(selection.Count));
                }
            }
            return hideHunters ? "Active hunters are hidden.\n" + text : text.ToString();
        }

        private static string Title(ProgressionPhase phase)
        {
            switch (phase)
            {
                case ProgressionPhase.ChooseThreat: return "CHOOSE WHO FOLLOWS";
                case ProgressionPhase.ChooseCurse: return "CHOOSE YOUR CURSE";
                case ProgressionPhase.Generating: return "THE NEXT ROOM STIRS";
                case ProgressionPhase.Shop: return "A MOMENT OF SHELTER";
                case ProgressionPhase.Ended: return "THE EXPEDITION ENDS";
                case ProgressionPhase.GenerationFailed: return "THE WAY IS CLOSED";
                default: return "";
            }
        }

        private static string Subtitle(ProgressionPhase phase)
        {
            switch (phase)
            {
                case ProgressionPhase.ChooseThreat: return "Choose one threat. It remains with this expedition.";
                case ProgressionPhase.ChooseCurse: return "Each curse changes a specific rule. Only eligible choices below their stack cap are offered.";
                case ProgressionPhase.Generating: return "Preparing the next room...";
                case ProgressionPhase.Shop: return "Upgrades stay with you. Consumables occupy slots; full inventory requires a replacement choice.";
                case ProgressionPhase.Ended: return "Your retained choices are shown below.";
                case ProgressionPhase.GenerationFailed: return "The room could not be prepared. Start again to begin a fresh expedition.";
                default: return "";
            }
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Health(float value) => Finite(value) ? value.ToString("0", CultureInfo.InvariantCulture) : "—";
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Fraction(float value, float maximum) => Finite(value) && Finite(maximum) && maximum > 0f
            ? Math.Max(0f, Math.Min(1f, value / maximum)) : 0f;
    }
}
