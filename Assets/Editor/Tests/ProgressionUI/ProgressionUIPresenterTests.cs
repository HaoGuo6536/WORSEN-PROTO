// ============================================================================
// ProgressionUIPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Exercises menu formatting and revision-safe UI input with plain snapshots.
//   These cases protect stale-click rejection and clear shop feedback without
//   using a scene, a UI document or the Session's game-rule implementation.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Verify optional terminal deferral, latest snapshot, timing and reset boundaries.
//   - Verify stock, permanent ownership, reason preservation, retained list size and audio intent.
//   - Verify displayed descriptions and authoritative eligibility survive formatting.
//   - Verify one pending action per revision and recovery on a newer response.
//
// DEPENDENCIES:
//   Core progression definitions, ProgressionUI pure Presenter/state, NUnit.
//
// USAGE NOTES:
//   Pure Edit Mode tests; no engine objects, clocks, scenes or external state.
//
// ============================================================================

using System;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;

namespace Worsen.Tests.ProgressionUI
{
    public sealed class ProgressionUIPresenterTests
    {
        private static readonly ProgressionChoice[] Choices =
        { new ProgressionChoice("watcher", "Watcher", "A patient hunter follows you.", 2) };
        private static readonly ProgressionOffer[] Offers =
        { new ProgressionOffer("heal", "Medkit", "Restore 35 health.", 3, false, true),
          new ProgressionOffer("boots", "Running shoes", "Move faster.", 5, false, false),
          new ProgressionOffer("lens", "Focus lens", "Longer beam.", 4, true, true) };

        [Test]
        public void ChoiceKeepsDescriptionAndRetainedCount()
        {
            var state = new ProgressionUIDriverState();
            new ProgressionUIPresenter().Present(state, Snapshot(4, ProgressionPhase.ChooseThreat));
            Assert.That(state.Cards[0].Description, Is.EqualTo(Choices[0].Description));
            Assert.That(state.Cards[0].Detail, Is.EqualTo("Already retained: 2"));
            Assert.That(state.WalletText, Is.EqualTo("WALLET  3"));
            Assert.That(state.RetainedText, Does.Contain("Watcher x2"));
            Assert.That(state.ModalVisible, Is.True);
        }

        [Test]
        public void DuplicateAndStaleActionsCannotConsumeTheNextChoice()
        {
            var state = new ProgressionUIDriverState();
            var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(4, ProgressionPhase.ChooseThreat));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseThreat, "watcher", 3), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseCurse, "watcher", 4), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseThreat, "watcher", 4), Is.True);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseThreat, "watcher", 4), Is.False);
            presenter.Present(state, Snapshot(5, ProgressionPhase.ChooseCurse));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseThreat, "watcher", 4), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseCurse, "watcher", 5), Is.True);
        }

        [Test]
        public void SameRevisionKeepsPendingAndNewFeedbackReleasesIt()
        {
            var state = new ProgressionUIDriverState();
            var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(10, ProgressionPhase.Shop));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Purchase, "heal", 10), Is.True);
            presenter.Present(state, Snapshot(10, ProgressionPhase.Shop));
            Assert.That(state.Pending, Is.True);
            presenter.Present(state, Snapshot(11, ProgressionPhase.Shop, "Purchase declined; wallet is unchanged."));
            Assert.That(state.Pending, Is.False);
            Assert.That(state.Message, Is.EqualTo("Purchase declined; wallet is unchanged."));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Continue, "", 11), Is.True);
        }

        [Test]
        public void OlderSnapshotCannotOverwriteCurrentWalletOrChoices()
        {
            var state = new ProgressionUIDriverState();
            var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(8, ProgressionPhase.Shop, "Current"));
            Assert.That(presenter.Present(state, Snapshot(7, ProgressionPhase.ChooseCurse, "Old")), Is.False);
            Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.Shop));
            Assert.That(state.Message, Is.EqualTo("Current"));
        }

        [Test]
        public void ShopUsesSuppliedEligibilityAndDescriptionsWithoutDerivingCalendarRules()
        {
            var state = new ProgressionUIDriverState();
            var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(3, ProgressionPhase.Shop));
            Assert.That(state.RoundText, Is.EqualTo("ROUND 3"));
            Assert.That(state.Cards.Length, Is.EqualTo(3));
            Assert.That(state.Cards[0].Description, Is.EqualTo("Restore 35 health."));
            Assert.That(state.Cards[0].Action, Is.EqualTo("BUY  3"));
            Assert.That(state.Cards[1].Detail, Does.StartWith("Not enough currency"));
            Assert.That(state.Cards[2].Action, Is.EqualTo("OWNED"));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Purchase, "boots", 3), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Purchase, "lens", 3), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Purchase, "missing", 3), Is.False);
        }

        [Test]
        public void HiddenAndExploringViewsCannotIssueModalActions()
        {
            var state = new ProgressionUIDriverState();
            var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(1, ProgressionPhase.Shop));
            presenter.Hide(state);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Continue, "", 1), Is.False);
            presenter.Present(state, Snapshot(2, ProgressionPhase.Exploring));
            Assert.That(state.Hidden, Is.False);
            Assert.That(state.ModalVisible, Is.False);
            Assert.That(state.Cards, Is.Empty);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Continue, "", 2), Is.False);
        }

        [TestCase(float.NaN, 100f, 0f)]
        [TestCase(50f, 0f, 0f)]
        [TestCase(150f, 100f, 1f)]
        [TestCase(25f, 100f, .25f)]
        public void HealthGaugeIsFiniteAndBounded(float health, float maximum, float expected)
        {
            var state = new ProgressionUIDriverState();
            new ProgressionUIPresenter().Present(state, Snapshot(1, ProgressionPhase.Exploring, "", health, maximum));
            Assert.That(state.HealthFraction, Is.EqualTo(expected));
            Assert.That(state.HealthText, Does.Not.Contain("NaN"));
        }

        [Test]
        public void DefaultSnapshotIsSafeAndRestartRequiresSuppliedEligibility()
        {
            var state = new ProgressionUIDriverState();
            var presenter = new ProgressionUIPresenter();
            presenter.Present(state, default);
            Assert.That(state.Cards, Is.Empty);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Restart, "", 0), Is.False);
            presenter.Present(state, Snapshot(1, ProgressionPhase.GenerationFailed, "Room generation failed."));
            Assert.That(state.Message, Is.EqualTo("Room generation failed."));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Restart, "", 1), Is.True);
        }

        [Test]
        public void ShopShowsAllSixOffersAndPreservesHealthWardAndStockReasons()
        {
            var offers = new[] {
                new ProgressionOffer("lens", "Shuttered Lens", "Narrows the beam.", 4, true, true),
                new ProgressionOffer("felt", "Felt Soles", "Quiets ordinary footsteps.", 4, false, true),
                new ProgressionOffer("wraps", "Climber Wraps", "Shortens rebound recovery.", 5, false, true),
                new ProgressionOffer("chalk", "Pilgrim Chalk", "Marks crossed doorways.", 3, false, false),
                new ProgressionOffer("heal", "Field Dressing", "Restores health.", 3, false, true, 2, true, "Already at full health."),
                new ProgressionOffer("ward", "Wax Ward", "Breaks the next grab.", 4, false, true, 1, true, "A ward is already carried.") };
            var snapshot = new ProgressionSnapshot(3, 1, 3, 1, 5, 1, 1, ProgressionPhase.Shop, 100, 100,
                null, offers, null, default, "", true, false);
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, snapshot);
            Assert.That(state.Cards.Length, Is.EqualTo(6));
            Assert.That(state.Cards[0].Detail, Does.Contain("Owned for this run"));
            Assert.That(state.Cards[1].Enabled, Is.True);
            Assert.That(state.Cards[4].Detail, Does.Contain("Already at full health.").And.Contain("stock 2"));
            Assert.That(state.Cards[5].Detail, Does.Contain("A ward is already carried."));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Purchase, "heal", 3), Is.False);
            Assert.That(presenter.DescribeRejectedCard(state, "heal", 3), Is.True);
            Assert.That(state.Message, Does.Contain("Already at full health."));
            Assert.That(state.Pending, Is.False);
            Assert.That(presenter.DescribeRejectedCard(state, "heal", 2), Is.False);
        }

        [Test]
        public void RepeatableOfferUsesRemainingStockInsteadOfPermanentOwnedFlag()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, new ProgressionSnapshot(1, 1, 3, 1, 10, 1, 0, ProgressionPhase.Shop, 50, 100,
                null, new[] { new ProgressionOffer("heal", "Field Dressing", "Heals.", 3, true, true, 1, true),
                    new ProgressionOffer("ward", "Wax Ward", "Escapes.", 4, false, true, 0, true) }, null, default, "", true, false));
            Assert.That(state.Cards[0].Enabled, Is.True);
            Assert.That(state.Cards[0].Action, Is.EqualTo("BUY  3"));
            Assert.That(state.Cards[1].Enabled, Is.False);
            Assert.That(state.Cards[1].Action, Is.EqualTo("SOLD OUT"));
        }

        [Test]
        public void RetainedListKeepsAllTwentyTwoDistinctCursesWithoutRepeatLabels()
        {
            var retained = new ProgressionSelection[22];
            for (int i = 0; i < retained.Length; i++) retained[i] = new ProgressionSelection("curse-" + i, "Curse " + i, ProgressionChoiceKind.Curse, 1);
            var state = new ProgressionUIDriverState();
            new ProgressionUIPresenter().Present(state, new ProgressionSnapshot(1, 1, 30, 1, 0, 1, 22,
                ProgressionPhase.Ended, 0, 100, null, null, retained, default, "", false, true));
            for (int i = 0; i < retained.Length; i++) Assert.That(state.RetainedText, Does.Contain("• Curse " + i));
            Assert.That(state.RetainedText.Split('\n').Length, Is.EqualTo(23));
            Assert.That(state.RetainedText, Does.Not.Contain(" x1"));
        }

        [Test]
        public void PurchaseIntentIsSilentUntilSessionCommitsAndContinueUsesBackCue()
        {
            var presenter = new ProgressionUIPresenter();
            Assert.That(presenter.ActionFeedback(ProgressionUIAction.Purchase), Is.Null);
            Assert.That(presenter.ActionFeedback(ProgressionUIAction.Continue), Is.EqualTo(CueId.UiBack));
            Assert.That(presenter.ActionFeedback(ProgressionUIAction.ChooseCurse), Is.EqualTo(CueId.UiConfirm));
        }

        [Test]
        public void DeferredTerminalKeepsWorldVisibleThenPresentsLatestResultExactlyOnce()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(1, ProgressionPhase.Exploring));
            presenter.DeferTerminal(state, 0.9f);
            presenter.Present(state, Snapshot(2, ProgressionPhase.Ended, "Earlier result"));
            presenter.Present(state, Snapshot(3, ProgressionPhase.Ended, "Latest result"));
            Assert.That(presenter.Present(state, Snapshot(2, ProgressionPhase.Ended)), Is.False);
            Assert.That(state.ModalVisible, Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Restart, "", 3), Is.False);
            Assert.That(presenter.Tick(state, float.NaN), Is.False);
            Assert.That(presenter.Tick(state, -1f), Is.False);
            Assert.That(presenter.Tick(state, 0.45f), Is.False);
            presenter.DeferTerminal(state, 2f);
            Assert.That(presenter.Tick(state, 0.5f), Is.True);
            Assert.That(state.ModalVisible, Is.True);
            Assert.That(state.Message, Is.EqualTo("Latest result"));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Restart, "", 3), Is.True);
            Assert.That(presenter.Tick(state, 1f), Is.False);
        }

        [Test]
        public void NormalTerminalIsImmediateAndHideClearsPendingConsumption()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(1, ProgressionPhase.Ended));
            Assert.That(state.ModalVisible, Is.True);
            presenter.Present(state, Snapshot(2, ProgressionPhase.Exploring));
            presenter.DeferTerminal(state, 0.9f);
            presenter.Present(state, Snapshot(3, ProgressionPhase.Ended));
            presenter.Hide(state);
            Assert.That(state.TerminalDeferred, Is.False);
            Assert.That(state.HasDeferredTerminal, Is.False);
            Assert.That(presenter.Tick(state, 1f), Is.False);
            Assert.That(state.Hidden, Is.True);
        }

        [Test]
        public void GenerationAndNewRunChoicesClearTerminalDelay()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(1, ProgressionPhase.Exploring));
            presenter.DeferTerminal(state, 0.9f);
            presenter.Present(state, Snapshot(2, ProgressionPhase.Ended));
            presenter.Present(state, new ProgressionSnapshot(3, 2, 1, 123, 0, 0, 0,
                ProgressionPhase.Generating, 100, 100, null, null, null, default, "", false, false));
            Assert.That(state.TerminalDeferred, Is.False);
            Assert.That(presenter.Tick(state, 1f), Is.False);
            presenter.Present(state, Snapshot(4, ProgressionPhase.Exploring));
            presenter.DeferTerminal(state, 0.9f);
            presenter.Present(state, Snapshot(5, ProgressionPhase.ChooseThreat));
            Assert.That(state.TerminalDeferred, Is.False);
            Assert.That(state.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
        }

        private static ProgressionSnapshot Snapshot(int revision, ProgressionPhase phase, string message = "", float health = 75, float maximum = 100)
        {
            return new ProgressionSnapshot(revision, 1, 3, 123, 3, 2, 1, phase, health, maximum,
                phase == ProgressionPhase.ChooseCurse ? Array.AsReadOnly(new[] { new ProgressionChoice("watcher", "Unquiet Gaze", "The Watcher remembers light longer.", 0) }) : Array.AsReadOnly(Choices), Array.AsReadOnly(Offers),
                Array.AsReadOnly(new[] { new ProgressionSelection("watcher", "Watcher", ProgressionChoiceKind.Threat, 2) }),
                default, message, phase == ProgressionPhase.Shop,
                phase == ProgressionPhase.Ended || phase == ProgressionPhase.GenerationFailed);
        }
    }
}
