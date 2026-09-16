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
            Assert.That(state.Cards[1].Detail, Is.EqualTo("Not enough currency"));
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

        private static ProgressionSnapshot Snapshot(int revision, ProgressionPhase phase, string message = "", float health = 75, float maximum = 100)
        {
            return new ProgressionSnapshot(revision, 1, 3, 123, 3, 2, 1, phase, health, maximum,
                Array.AsReadOnly(Choices), Array.AsReadOnly(Offers),
                Array.AsReadOnly(new[] { new ProgressionSelection("watcher", "Watcher", ProgressionChoiceKind.Threat, 2) }),
                default, message, phase == ProgressionPhase.Shop,
                phase == ProgressionPhase.Ended || phase == ProgressionPhase.GenerationFailed);
        }
    }
}
