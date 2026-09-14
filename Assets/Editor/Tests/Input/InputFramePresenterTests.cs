// ============================================================================
// InputFramePresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies that render-time input facts survive until a fixed tick consumes them.
//   These tests exercise short taps, repeated ticks, look accumulation, and input
//   gates with plain data so failures do not depend on a scene or physical device.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Input.
//
// KEY RESPONSIBILITIES:
//   - Assert that both edges of an inter-tick tap survive publication.
//   - Assert that motion and button edges are consumed once while held data persists.
//   - Assert that disabled and unfocused input cannot leak stale commands.
//
// DEPENDENCIES:
//   - NUnit, Worsen.Core, and Worsen.Presentation.Input pure types.
//
// USAGE NOTES:
//   - Editor-only tests for InputFramePresenter; no engine objects are constructed.
//   - Elapsed time and device motion are explicit test inputs.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Input;

namespace Worsen.Tests.Input
{
    [TestFixture]
    public sealed class InputFramePresenterTests
    {
        private InputDriverState _state;
        private InputFramePresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _state = new InputDriverState { OwnerEnabled = true, InputEnabled = true };
            _presenter = new InputFramePresenter();
        }

        [Test]
        public void PressAndReleaseBeforeTickPreservesBothEdges()
        {
            _presenter.SetButton(_state, InputButtons.Jump, true);
            _presenter.SetButton(_state, InputButtons.Jump, false);

            InputFrame first = _presenter.Flush(_state);
            InputFrame second = _presenter.Flush(_state);

            Assert.That(first.Pressed, Is.EqualTo(InputButtons.Jump));
            Assert.That(first.Released, Is.EqualTo(InputButtons.Jump));
            Assert.That(first.Held, Is.EqualTo(InputButtons.None));
            Assert.That(second.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(second.Released, Is.EqualTo(InputButtons.None));
        }

        [Test]
        public void HeldButtonsPersistWithoutRepeatingPress()
        {
            _presenter.SetButton(_state, InputButtons.Sprint | InputButtons.LookBack, true);
            InputFrame first = _presenter.Flush(_state);
            _presenter.SetButton(_state, InputButtons.Sprint | InputButtons.LookBack, true);
            InputFrame second = _presenter.Flush(_state);

            Assert.That(first.Pressed, Is.EqualTo(InputButtons.Sprint | InputButtons.LookBack));
            Assert.That(second.Held, Is.EqualTo(first.Held));
            Assert.That(second.Pressed, Is.EqualTo(InputButtons.None));
        }

        [Test]
        public void ReleaseOfOneButtonPreservesOtherHeldButtons()
        {
            _presenter.SetButton(_state, InputButtons.Sprint | InputButtons.Crouch, true);
            _presenter.Flush(_state);
            _presenter.SetButton(_state, InputButtons.Crouch, false);
            InputFrame frame = _presenter.Flush(_state);

            Assert.That(frame.Held, Is.EqualTo(InputButtons.Sprint));
            Assert.That(frame.Released, Is.EqualTo(InputButtons.Crouch));
            Assert.That(frame.Pressed, Is.EqualTo(InputButtons.None));
        }

        [Test]
        public void ReleaseThenRepressBeforeTickPreservesEdgesAndFinalHeld()
        {
            _presenter.SetButton(_state, InputButtons.Interact, true);
            _presenter.Flush(_state);
            _presenter.SetButton(_state, InputButtons.Interact, false);
            _presenter.SetButton(_state, InputButtons.Interact, true);
            InputFrame frame = _presenter.Flush(_state);

            Assert.That(frame.Held, Is.EqualTo(InputButtons.Interact));
            Assert.That(frame.Pressed, Is.EqualTo(InputButtons.Interact));
            Assert.That(frame.Released, Is.EqualTo(InputButtons.Interact));
        }

        [Test]
        public void LookFromSeveralRenderUpdatesIsConsumedExactlyOnce()
        {
            _presenter.AccumulateMouseLook(_state, new Vector2(10f, -20f), 0.1f, false);
            _presenter.AccumulateMouseLook(_state, new Vector2(30f, 10f), 0.1f, false);
            InputFrame first = _presenter.Flush(_state);
            InputFrame second = _presenter.Flush(_state);
            _presenter.AccumulateMouseLook(_state, new Vector2(-10f, 10f), 0.1f, false);
            InputFrame third = _presenter.Flush(_state);

            Assert.That(first.LookDelta, Is.EqualTo(new Vector2(4f, -1f)));
            Assert.That(second.LookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(third.LookDelta, Is.EqualTo(new Vector2(-1f, 1f)));
        }

        [Test]
        public void MovementIsNormalizedAndPersistsAcrossTicks()
        {
            _presenter.SetMove(_state, Vector2.one);
            InputFrame first = _presenter.Flush(_state);
            InputFrame second = _presenter.Flush(_state);

            Assert.That(first.Move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(second.Move, Is.EqualTo(first.Move));
            _presenter.SetMove(_state, Vector2.zero);
            Assert.That(_presenter.Flush(_state).Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void GamepadTurnUsesElapsedTimeAndFlushDoesNotIntegrateAgain()
        {
            _presenter.SetGamepadLook(_state, new Vector2(0.5f, -0.25f));
            _presenter.AccumulateGamepadLook(_state, 0.1f, 100f, false);
            _presenter.AccumulateGamepadLook(_state, 0.2f, 100f, false);
            InputFrame frame = _presenter.Flush(_state);

            Assert.That(frame.LookDelta, Is.EqualTo(new Vector2(15f, -7.5f)));
            Assert.That(_presenter.Flush(_state).LookDelta, Is.EqualTo(Vector2.zero));
            _presenter.AccumulateGamepadLook(_state, 0.1f, 100f, false);
            Assert.That(_presenter.Flush(_state).LookDelta, Is.EqualTo(new Vector2(5f, -2.5f)));
        }

        [Test]
        public void InvertYAppliesToBothMouseAndGamepad()
        {
            _presenter.AccumulateMouseLook(_state, new Vector2(10f, 10f), 0.1f, true);
            _presenter.SetGamepadLook(_state, Vector2.up);
            _presenter.AccumulateGamepadLook(_state, 0.1f, 100f, true);

            Assert.That(_presenter.Flush(_state).LookDelta, Is.EqualTo(new Vector2(1f, -11f)));
        }

        [Test]
        public void FocusLossDiscardsPendingAndHeldInput()
        {
            SeedPendingInput();
            _presenter.SetFocus(_state, false);
            _presenter.SetButton(_state, InputButtons.Jump, true);
            _presenter.AccumulateMouseLook(_state, Vector2.one, 1f, false);
            AssertNeutral(_presenter.Flush(_state));
            _presenter.SetFocus(_state, true);
            AssertNeutral(_presenter.Flush(_state));
        }

        [Test]
        public void DisabledInputDiscardsAndIgnoresDeviceFactsUntilReenabled()
        {
            SeedPendingInput();
            _presenter.SetInputEnabled(_state, false);
            _presenter.SetMove(_state, Vector2.one);
            _presenter.SetGamepadLook(_state, Vector2.one);
            _presenter.SetButton(_state, InputButtons.UseItem, true);
            _presenter.AccumulateGamepadLook(_state, 1f, 100f, false);
            AssertNeutral(_presenter.Flush(_state));
            _presenter.SetInputEnabled(_state, true);
            AssertNeutral(_presenter.Flush(_state));

            _presenter.SetButton(_state, InputButtons.UseItem, true);
            Assert.That(_presenter.Flush(_state).Pressed, Is.EqualTo(InputButtons.UseItem));
        }

        [Test]
        public void OwnerReenableDoesNotOverrideExplicitInputGate()
        {
            SeedPendingInput();
            _presenter.SetInputEnabled(_state, false);
            _presenter.SetOwnerEnabled(_state, false);
            _presenter.SetOwnerEnabled(_state, true);
            _presenter.SetButton(_state, InputButtons.Jump, true);

            Assert.That(_presenter.IsAcceptingInput(_state), Is.False);
            AssertNeutral(_presenter.Flush(_state));
        }

        [Test]
        public void DriverDisableResetDiscardsEverythingIncludingLookRate()
        {
            SeedPendingInput();
            _presenter.Reset(_state);
            _presenter.AccumulateGamepadLook(_state, 1f, 100f, false);

            AssertNeutral(_presenter.Flush(_state));
        }

        private void SeedPendingInput()
        {
            _presenter.SetMove(_state, Vector2.up);
            _presenter.SetGamepadLook(_state, Vector2.right);
            _presenter.AccumulateMouseLook(_state, Vector2.one, 1f, false);
            _presenter.SetButton(_state, InputButtons.Sprint, true);
            _presenter.SetButton(_state, InputButtons.Jump, true);
            _presenter.SetButton(_state, InputButtons.Jump, false);
        }

        private static void AssertNeutral(InputFrame frame)
        {
            Assert.That(frame.Move, Is.EqualTo(Vector2.zero));
            Assert.That(frame.LookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(frame.Held, Is.EqualTo(InputButtons.None));
            Assert.That(frame.Pressed, Is.EqualTo(InputButtons.None));
            Assert.That(frame.Released, Is.EqualTo(InputButtons.None));
        }
    }
}
