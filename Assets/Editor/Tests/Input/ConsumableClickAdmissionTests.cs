// ============================================================================
// ConsumableClickAdmissionTests.cs
// ============================================================================
// PURPOSE:
//   Verifies click edge admission and gameplay gating through the real pure input
//   accumulator. Holding the primary action cannot repeatedly use an item, and
//   menu/focus transitions cannot leak a buffered click into gameplay.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Input.
// KEY RESPONSIBILITIES:
//   - Verify one press per click and direct selection before same-tick use.
//   - Verify closed gates and pause/focus transitions clear consumable input.
// DEPENDENCIES:
//   NUnit, Core and Input pure presentation only.
// USAGE NOTES:
//   Native binding verification is ConsumableInputTests; this fixture is headless.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Input;
namespace Worsen.Tests.Input
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ConsumableClickAdmissionTests
    {
        private static InputDriverState Ready() => new InputDriverState { InputEnabled = true, OwnerEnabled = true, HasFocus = true };
        [Test]
        public void PrimaryHoldAdmitsOneUseAndRepressAdmitsTheNext()
        {
            var p = new InputFramePresenter(); var s = Ready();
            p.SelectSlot(s, 2); p.SetButton(s, InputButtons.UseConsumable, true);
            Assert.That(p.FlushSelectedSlot(s), Is.EqualTo(2));
            Assert.That(p.Flush(s).Pressed, Is.EqualTo(InputButtons.UseConsumable));
            for (int i = 0; i < 60; i++) { p.SetButton(s, InputButtons.UseConsumable, true); Assert.That(p.Flush(s).Pressed, Is.EqualTo(InputButtons.None)); }
            p.SetButton(s, InputButtons.UseConsumable, false); p.SetButton(s, InputButtons.UseConsumable, true);
            Assert.That(p.Flush(s).Pressed, Is.EqualTo(InputButtons.UseConsumable));
        }
        [TestCase("input")] [TestCase("owner")] [TestCase("focus")] [TestCase("pause")]
        public void ClosingAnyGateDropsBufferedClickAndSelection(string gate)
        {
            var p = new InputFramePresenter(); var s = Ready(); p.SelectSlot(s, 1); p.SetButton(s, InputButtons.UseConsumable, true);
            switch (gate)
            {
                case "input": p.SetInputEnabled(s, false); break;
                case "owner": p.SetOwnerEnabled(s, false); break;
                case "focus": p.SetFocus(s, false); break;
                case "pause": p.SetPaused(s, true); break;
            }
            p.SetButton(s, InputButtons.UseConsumable, true);
            Assert.That(p.Flush(s).Pressed, Is.EqualTo(InputButtons.None)); Assert.That(p.FlushSelectedSlot(s), Is.EqualTo(-1));
            p.SetInputEnabled(s, true); p.SetOwnerEnabled(s, true); p.SetFocus(s, true); p.SetPaused(s, false);
            var frame = p.Flush(s); Assert.That(frame.Held | frame.Pressed | frame.Released, Is.EqualTo(InputButtons.None));
        }
    }
}
