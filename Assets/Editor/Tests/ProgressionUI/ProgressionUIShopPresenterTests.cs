// ============================================================================
// ProgressionUIShopPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies shop copy and actions from Core snapshots without a live document.
//   Presentation must explain reservations while leaving all purchase rules to Session.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · ProgressionUI.
// KEY RESPONSIBILITIES:
//   - Preserve pedestal copy, price and authoritative rejection reasons on one detail line.
//   - Show priced rerolls and slot replacement/cancel choices with revision latches.
// DEPENDENCIES:
//   - NUnit, Core and the pure ProgressionUI presentation stack.
// USAGE NOTES:
//   Edit Mode, no scene, asset or rendering operations.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;

namespace Worsen.Tests.ProgressionUI
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProgressionUIShopPresenterTests
    {
        private static ProgressionSnapshot Snapshot(bool replacement = false) => new ProgressionSnapshot(
            10, 3, 3, 71, 5, 1, 1, ProgressionPhase.Shop, 100, 100, null,
            new[] { new ProgressionOffer("wax-ward", "Wax Ward", "Breaks the next grab automatically.", 6,
                false, false, 1, true, "Not enough Golden Cakes.", FearAxis.Agency, EffectKind.Consumable) },
            null, default, replacement ? "Inventory full. Choose a replacement slot." : "", !replacement, false,
            Array.AsReadOnly(new[] { new ProgressionInventorySlot("gauze", "Gauze", 4),
                new ProgressionInventorySlot("doorstop", "Doorstop", 4), new ProgressionInventorySlot("oil-flask", "Oil Flask", 4) }),
            replacement ? "wax-ward" : null, replacement ? "Wax Ward" : null, 6, !replacement, 2, 0);

        [Test]
        public void PedestalPreservesCopyPriceAndGreyedReasonAndRerollShowsItsCost()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot());
            var pedestal = state.Cards.Single(card => card.Kind == ProgressionUIAction.Purchase);
            Assert.That(pedestal.Title, Is.EqualTo("Wax Ward"));
            Assert.That(pedestal.Description, Is.EqualTo("Breaks the next grab automatically."));
            // One detail line: the reason first, then the kind. The price lives on the action only.
            Assert.That(pedestal.Detail, Does.StartWith("Not enough Golden Cakes.").And.Contain("Consumable").And.Not.Contain("\n"));
            Assert.That(pedestal.Detail, Does.Not.Contain("Agency"), "Fear axis is design metadata, not player copy.");
            Assert.That(pedestal.Action, Is.EqualTo("BUY  6"));
            Assert.That(pedestal.Enabled, Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Purchase, pedestal.Id, 10), Is.False);
            Assert.That(state.Cards.Single(card => card.Kind == ProgressionUIAction.Reroll).Action, Is.EqualTo("REROLL  2"));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Reroll, "reroll", 10), Is.True);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Reroll, "reroll", 10), Is.False);
        }

        [Test]
        public void FullInventoryShowsSlotChoiceAndCancellationInsteadOfPurchasesAndContinue()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(true));
            Assert.That(state.RetainedText, Does.Contain("CHOOSE A SLOT TO REPLACE").And.Contain("Gauze").And.Contain("Wax Ward"));
            Assert.That(state.Cards.Count(card => card.Kind == ProgressionUIAction.ReplaceSlot), Is.EqualTo(3));
            Assert.That(state.Cards.Any(card => card.Kind == ProgressionUIAction.Purchase || card.Kind == ProgressionUIAction.Reroll), Is.False);
            Assert.That(state.Cards[0].Detail, Does.Contain("Nothing charged yet"));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Continue, "", 10), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ReplaceSlot, "0", 9), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ReplaceSlot, "0", 10), Is.True);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ReplaceSlot, "1", 10), Is.False);
            state = new ProgressionUIDriverState(); presenter.Present(state, Snapshot(true));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.CancelReplacement, "cancel-replacement", 10), Is.True);
        }

        [Test]
        public void LuckyRerollSelectionKeepsSessionAdmittedStackableCurseSelectable()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, new ProgressionSnapshot(12, 4, 4, 71, 5, 1, 1, ProgressionPhase.ChooseCurse,
                100, 100, new[] { new ProgressionChoice("echo-shorter-delay", "Shorter Delay", "Shortens delay.", 1) },
                null, null, default, "New selection drawn.", false, false, canReroll: true, freeRerollsRemaining: 1));
            Assert.That(state.Cards.Single(card => card.Kind == ProgressionUIAction.Reroll).Enabled, Is.True);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseCurse, "echo-shorter-delay", 12), Is.True);
        }
    }
}
