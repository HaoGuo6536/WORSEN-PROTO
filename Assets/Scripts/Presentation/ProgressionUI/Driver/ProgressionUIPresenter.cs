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
//   - Format titles, descriptions, wallet, health and retained choices.
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
            bool changed = !state.HasSnapshot || snapshot.Revision != state.Revision;
            state.HasSnapshot = true;
            state.Hidden = false;
            if (changed) state.Pending = false;
            state.Revision = snapshot.Revision;
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

        public void Hide(ProgressionUIDriverState state) => state.Hidden = true;

        public bool TryIssue(ProgressionUIDriverState state, ProgressionUIAction action, string id, int revision)
        {
            if (!state.HasSnapshot || state.Hidden || state.Pending || !state.ModalVisible || revision != state.Revision) return false;
            bool allowed = action == ProgressionUIAction.Continue ? state.CanContinue :
                action == ProgressionUIAction.Restart ? state.CanRestart : CardEnabled(state.Cards, action, id);
            if (!allowed) return false;
            state.Pending = true;
            return true;
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
                    string detail = offer.Purchased ? "Purchased this visit" : !offer.CanAfford ? "Not enough currency" : "Available";
                    cards[i] = new ProgressionUICard(offer.Id, offer.Title, offer.Description, detail,
                        offer.Purchased ? "OWNED" : "BUY  " + Number(offer.Price), !offer.Purchased && offer.CanAfford,
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
                    "CHOOSE", !string.IsNullOrEmpty(choice.Id), kind);
            }
            return output;
        }

        private static string Retained(ProgressionSnapshot snapshot)
        {
            if (snapshot.Retained == null || snapshot.Retained.Count == 0) return "No retained choices yet.";
            var text = new StringBuilder();
            foreach (var selection in snapshot.Retained)
            {
                if (text.Length > 0) text.Append("  /  ");
                text.Append(selection.Title).Append(" x").Append(Number(selection.Count));
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
                case ProgressionPhase.ChooseCurse: return "Choose one curse. Read its effect before continuing.";
                case ProgressionPhase.Generating: return "Preparing the next room...";
                case ProgressionPhase.Shop: return "Recover, improve your equipment, then continue.";
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
