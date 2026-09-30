// ============================================================================
// HiddenCountPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hidden Count no longer redacts the shelter roster or threat count.
//   The in-level cake counter is owned by the HUD, not this presentation stack.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · ProgressionUI.
// KEY RESPONSIBILITIES:
//   - Preserve hunter names/counts, retained curses and catch gates.
// DEPENDENCIES:
//   NUnit, Core snapshots and ProgressionUI pure presentation.
// USAGE NOTES:
//   Pure tests; live HUD hiding is covered by its owner's fixtures.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;
namespace Worsen.Tests.ProgressionUI
{
    public sealed class HiddenCountPresenterTests
    {
        [Test]
        public void HiddenCountLeavesShelterHuntersAndCountsReadable()
        {
            var snapshot = new ProgressionSnapshot(1, 1, 3, 42, 2, 2, 1, ProgressionPhase.Shop, 100, 100,
                null, null, new[] { new ProgressionSelection("echo", "The Echo", ProgressionChoiceKind.Threat, 2),
                    new ProgressionSelection("hidden-count", "Hidden Count", ProgressionChoiceKind.Curse, 1) }, default, "", true, false);
            var state = new ProgressionUIDriverState(); var p = new ProgressionUIPresenter(); p.Present(state, snapshot);
            Assert.That(state.RetainedText, Does.Contain("The Echo x2"));
            Assert.That(state.BurdenText, Does.Contain("2 THREATS"));
            Assert.That(state.RetainedText, Does.Contain("Hidden Count"));
            Assert.That(snapshot.ThreatCount, Is.EqualTo(2));
            p.Hide(state); Assert.That(state.Hidden, Is.True);
        }
        [Test]
        public void HiddenCountDoesNotReleasePendingCatch()
        {
            var state = new ProgressionUIDriverState(); var p = new ProgressionUIPresenter();
            p.Present(state, new ProgressionSnapshot(1, 1, 1, 1, 0, 1, 0, ProgressionPhase.Exploring,
                100, 100, null, null, null, default, "", false, false));
            p.DeferTerminal(state, 3, new EntityId(1));
            p.Present(state, new ProgressionSnapshot(2, 1, 1, 1, 0, 1, 0, ProgressionPhase.Ended,
                0, 100, null, null, null, default, "", false, true));
            Assert.That(state.ModalVisible, Is.False);
            Assert.That(state.TerminalDeferred, Is.True);
        }
    }
}
