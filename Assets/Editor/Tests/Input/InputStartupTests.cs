// ============================================================================
// InputStartupTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the scene-readiness input boundary with a newly created buffer.
//   Keys pressed during service initialization must not enter the first tick
//   when scene assembly later opens the input gate.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Prove that pre-readiness device facts are discarded rather than deferred.
// DEPENDENCIES:
//   - NUnit, Core InputButtons, and InputFramePresenter with InputDriverState.
// USAGE NOTES:
//   - Editor-only pure-data regression; the original open gate delivered Jump.
// ============================================================================

using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Input;

namespace Worsen.Tests.Input
{
    public sealed class InputStartupTests
    {
        [Test]
        public void PreReadinessTapDoesNotLeakIntoFirstSceneTick()
        {
            var state = new InputDriverState { OwnerEnabled = true };
            var presenter = new InputFramePresenter();
            presenter.SetButton(state, InputButtons.Jump, true);
            presenter.SetButton(state, InputButtons.Jump, false);
            presenter.SetInputEnabled(state, true);
            var first = presenter.Flush(state);
            Assert.That(first.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(first.Released, Is.EqualTo(InputButtons.None));
            presenter.SetButton(state, InputButtons.Jump, true);
            Assert.That(presenter.Flush(state).Pressed, Is.EqualTo(InputButtons.Jump));
        }
    }
}
