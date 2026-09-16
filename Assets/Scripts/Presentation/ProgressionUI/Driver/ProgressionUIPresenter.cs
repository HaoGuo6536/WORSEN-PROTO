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
//   - Group retained choices without truncation; distinguish UI intent from committed purchase audio.
//   - Delay only terminal presentation with supplied time; leave progression authority unchanged.
//   - Reject hidden, stale or repeated UI clicks using the displayed snapshot.
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
            if (state.TerminalDeferred && (snapshot.GenerationId != state.DeferredGenerationId
                || snapshot.Phase == ProgressionPhase.Dormant || snapshot.Phase == ProgressionPhase.Generating
                || snapshot.Phase == ProgressionPhase.ChooseThreat || snapshot.Phase == ProgressionPhase.ChooseCurse))
                ClearTerminalDeferral(state);
            if (state.TerminalDeferred && state.TerminalRemaining > 0f && snapshot.Phase == ProgressionPhase.Ended)
            {
                state.DeferredTerminal = snapshot;
                state.HasDeferredTerminal = true;
                state.ModalVisible = false;
                return true;
            }
            bool changed = !state.HasSnapshot || snapshot.Revision != state.Revision;
            state.HasSnapshot = true;
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
            state.HealthText = "HEALTH  " + Health(snapshot.Health) + " / " + Health(snapshot.MaxHealth);
            state.HealthFraction = Fraction(snapshot.Health, snapshot.MaxHealth);
            state.BurdenText = Number(snapshot.ThreatCount) + " THREATS  /  " + Number(snapshot.CurseCount) + " CURSES";
            state.Message = snapshot.Message ?? "";
            state.Title = Title(snapshot.Phase);
            state.Subtitle = Subtitle(snapshot.Phase);
            state.RetainedText = Retained(snapshot);
            state.Cards = Cards(snapshot);
            return true;
        }

        public void Hide(ProgressionUIDriverState state)
        {
            ClearTerminalDeferral(state);
            state.Hidden = true;
        }

        public void DeferTerminal(ProgressionUIDriverState state, float seconds)
        {
            if (state.TerminalDeferred || state.Phase == ProgressionPhase.Ended || !Finite(seconds) || seconds <= 0f) return;
            state.TerminalDeferred = true;
            state.TerminalRemaining = Math.Min(2f, seconds);
            state.DeferredGenerationId = state.GenerationId;
        }

        public bool Tick(ProgressionUIDriverState state, float dt)
        {
            if (!state.TerminalDeferred || !Finite(dt) || dt <= 0f) return false;
            state.TerminalRemaining = Math.Max(0f, state.TerminalRemaining - dt);
            if (state.TerminalRemaining > 0f) return false;
            bool pending = state.HasDeferredTerminal;
            var snapshot = state.DeferredTerminal;
            ClearTerminalDeferral(state);
            return pending && Present(state, snapshot);
        }

        public void ClearTerminalDeferral(ProgressionUIDriverState state)
        {
            state.TerminalDeferred = state.HasDeferredTerminal = false;
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
            => action == ProgressionUIAction.Purchase ? (CueId?)null
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
                    string detail = status + "\n" + (offer.Repeatable
                        ? "Consumable · stock " + Number(Math.Max(0, offer.StockRemaining))
                        : "Unique equipment · kept for this run");
                    bool available = !owned && !soldOut && offer.CanAfford && string.IsNullOrEmpty(offer.UnavailableReason);
                    cards[i] = new ProgressionUICard(offer.Id, offer.Title, offer.Description, detail,
                        owned ? "OWNED" : soldOut ? "SOLD OUT" : "BUY  " + Number(offer.Price), available,
                        ProgressionUIAction.Purchase);
                }
                return cards;
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
                    choice.SelectedCount > 0 && kind == ProgressionUIAction.ChooseCurse ? "RETAINED" : "CHOOSE",
                    !string.IsNullOrEmpty(choice.Id) && (kind != ProgressionUIAction.ChooseCurse || choice.SelectedCount == 0), kind);
            }
            return output;
        }

        private static string Retained(ProgressionSnapshot snapshot)
        {
            if (snapshot.Retained == null || snapshot.Retained.Count == 0) return "No retained choices yet.";
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
                        text.Append(kind == ProgressionChoiceKind.Threat ? "FOLLOWING YOU" : kind == ProgressionChoiceKind.Curse ? "YOUR CURSES" : "YOUR EQUIPMENT");
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
                case ProgressionPhase.ChooseCurse: return "Each curse changes a specific rule and can be chosen only once per run.";
                case ProgressionPhase.Generating: return "Preparing the next room...";
                case ProgressionPhase.Shop: return "Unique equipment stays with you. Consumables have limited stock. Continue whenever you are ready.";
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
