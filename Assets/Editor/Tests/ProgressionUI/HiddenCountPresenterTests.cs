// ============================================================================
// HiddenCountPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies optional shelter redaction without changing the authoritative roster.
//   Default presentation remains readable and the flag can change on the same revision.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · ProgressionUI.
// KEY RESPONSIBILITIES:
//   - Hide hunter names/counts while preserving other retained choices and catch gates.
// DEPENDENCIES:
//   NUnit, Core snapshots and ProgressionUI pure presentation.
// USAGE NOTES:
//   Pure tests; the curse owner supplies the flag through routing.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.ProgressionUI;
namespace Worsen.Tests.ProgressionUI
{
    public sealed class HiddenCountPresenterTests
    {
        [Test]
        public void FlagRedactsHuntersImmediatelyAndRestoresThemWithoutErasingChoices()
        {
            var snapshot = new ProgressionSnapshot(1, 1, 3, 42, 2, 2, 1, ProgressionPhase.Shop, 100, 100,
                null, null, new[] { new ProgressionSelection("echo", "The Echo", ProgressionChoiceKind.Threat, 2),
                    new ProgressionSelection("curse", "Short Grace", ProgressionChoiceKind.Curse, 1) }, default, "", true, false);
            var state = new ProgressionUIDriverState(); var p = new ProgressionUIPresenter(); p.Present(state, snapshot);
            Assert.That(state.RetainedText, Does.Contain("The Echo x2"));
            p.SetHiddenCount(state, true);
            Assert.That(state.RetainedText, Does.Not.Contain("Echo").And.Not.Contain("FOLLOWING YOU"));
            Assert.That(state.BurdenText, Does.Not.Contain("2 THREATS"));
            Assert.That(state.RetainedText, Does.Contain("Short Grace"));
            p.SetHiddenCount(state, false); Assert.That(state.RetainedText, Does.Contain("The Echo x2"));
            Assert.That(snapshot.ThreatCount, Is.EqualTo(2));
            p.Hide(state); p.SetHiddenCount(state, true); Assert.That(state.Hidden, Is.True);
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
            p.SetHiddenCount(state, true); Assert.That(state.ModalVisible, Is.False);
            Assert.That(state.TerminalDeferred, Is.True);
        }
    }
}
