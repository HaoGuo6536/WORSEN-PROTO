// ============================================================================
// ProgressionBargainPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the shelter deal screen's copy, reward quotes and revision-safe actions.
//   Presentation receives only Core values and does not derive curse costs or eligibility.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · ProgressionUI.
// KEY RESPONSIBILITIES:
//   - Keep Bargain choices distinct from normal selection and expose free dismissal.
// DEPENDENCIES:
//   - Core, ProgressionUI Presenter/state and NUnit only.
// USAGE NOTES:
//   Pure tests; rendering, cursor gating and screen transitions need live coordinator checks.
// ============================================================================
using System;
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;
namespace Worsen.Tests.ProgressionUI
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProgressionBargainPresenterTests
    {
        private static ProgressionSnapshot Snapshot(int revision, bool pending, ProgressionPhase phase = ProgressionPhase.Shop)
            => new ProgressionSnapshot(revision, 1, 9, 123, 4, 2, 1, phase, 100f, 100f,
                null, null, null, default, "", true, false, bargain: new ShrineDealSnapshot(pending, 8,
                    Array.AsReadOnly(new[] { new ShrineDealOffer("a", "A", "Removes safety.", 18),
                        new ShrineDealOffer("b", "B", "Removes time.", 6), new ShrineDealOffer("c", "C", "Removes light.", 6) })));
        [Test]
        public void DealShowsThreeRewardCardsAndAdmitsOneTakeOrFreeContinue()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(4, true));
            Assert.That(state.Title, Is.EqualTo("BARGAIN")); Assert.That(state.Subtitle, Does.Contain("free"));
            Assert.That(state.Cards.Length, Is.EqualTo(3)); Assert.That(state.Cards[0].Action, Is.EqualTo("TAKE  +18"));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.ChooseCurse, "a", 4), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.TakeBargain, "a", 3), Is.False);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.TakeBargain, "a", 4), Is.True);
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.TakeBargain, "a", 4), Is.False);
            presenter.Present(state, Snapshot(5, true));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.Continue, "", 5), Is.True);
        }
        [Test]
        public void AcceptedAndLiveFloorSnapshotsRemoveTheDeal()
        {
            var state = new ProgressionUIDriverState(); var presenter = new ProgressionUIPresenter();
            presenter.Present(state, Snapshot(4, true)); presenter.Present(state, Snapshot(5, false));
            Assert.That(state.Title, Is.EqualTo("SHOP"));
            Assert.That(presenter.TryIssue(state, ProgressionUIAction.TakeBargain, "a", 5), Is.False);
            presenter.Present(state, Snapshot(6, true, ProgressionPhase.Exploring));
            Assert.That(state.Cards, Is.Empty); Assert.That(state.ModalVisible, Is.False);
        }
    }
}
