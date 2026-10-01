// ============================================================================
// HUDInventoryPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Proves physical slot layout, selection emphasis and flashlight display math.
//   Tests use snapshots and injected time without a document or engine objects.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HUD.
// KEY RESPONSIBILITIES:
//   - Cover holes, overflow, empty/invalid selection and independent flashlight state.
//   - Protect selected geometry, ready pulse, charge and aim sanitization.
// DEPENDENCIES:
//   NUnit, Core and the HUD pure presentation stack.
// USAGE NOTES:
//   Pure Edit Mode/headless tests. Native rendering is a separate fixture.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.HUD;

namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDInventoryPresenterTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void PhysicalSelectionNeverCompactsHolesOrSelectsFlashlight(int index)
        {
            var p = new HUDInventoryPresenter(); var s = new HUDDriverState();
            p.SetFlashlight(s, true, .5f, 0f);
            p.SetSlots(s, new ConsumableInventorySnapshot(new[] { default(ProgressionInventorySlot),
                new ProgressionInventorySlot("gauze", "Gauze", 0), default }, new[] { 0, 1, 0 }, index));
            Assert.That(s.DisplayedSlots, Is.EqualTo(3)); Assert.That(s.SelectedDisplaySlot, Is.EqualTo(index));
            Assert.That(s.SlotLabels, Is.EqualTo(new[] { "Empty", "Gauze", "Empty" }));
            Assert.That(s.FlashlightCharge, Is.EqualTo(.5f)); Assert.That(s.FlashlightOn, Is.True);
            Assert.That(s.SelectedSlotText, Is.EqualTo(index == 1 ? "Gauze" : "Empty"));
        }

        [Test]
        public void OverflowCannotMasqueradeAsTheReservedFlashlightSlot()
        {
            var p = new HUDInventoryPresenter(); var s = new HUDDriverState();
            p.SetSlots(s, new ConsumableInventorySnapshot(new[] { default(ProgressionInventorySlot), default,
                default, new ProgressionInventorySlot("oil-flask", "Oil", 0) }, null, 3));
            Assert.That(s.DisplayedSlots, Is.EqualTo(3)); Assert.That(s.SelectedDisplaySlot, Is.EqualTo(-1));
            Assert.That(s.SelectedSlotText, Is.EqualTo("Oil")); Assert.That(s.SlotOverflowText, Is.EqualTo("+1"));
            Assert.That(s.FlashlightKnown, Is.False);
            p.SetSlots(s, default);
            Assert.That(s.DisplayedSlots, Is.EqualTo(3)); Assert.That(s.SlotLabels, Is.All.EqualTo("Empty"));
            Assert.That(s.SelectedSlotText, Is.Empty); Assert.That(s.SlotOverflowText, Is.Empty);
        }

        [Test]
        public void SelectedFrameIsLargerBrighterAndSeparatedFromFlashlight()
        {
            var p = new HUDInventoryPresenter();
            var selected = p.SlotRect(1, true, 112f, 72f, 14f, 1.08f);
            var ordinary = p.SlotRect(1, false, 112f, 72f, 14f, 1.08f);
            Assert.That(selected.width, Is.GreaterThan(ordinary.width));
            Assert.That(selected.height, Is.GreaterThan(ordinary.height));
            Assert.That(selected.center, Is.EqualTo(ordinary.center));
            Assert.That(p.SlotOpacity(true, .55f), Is.EqualTo(1f));
            Assert.That(p.SlotOpacity(false, .55f), Is.EqualTo(.55f));
            Assert.That(p.SlotRect(0, false, 112f, 72f, 14f, 1.08f).xMax, Is.LessThan(selected.xMin));
            Assert.That(selected.xMax, Is.LessThan(p.SlotRect(2, false, 112f, 72f, 14f, 1.08f).xMin));
            Assert.That(p.FlashlightLeft(112f, 14f, 32f), Is.GreaterThan(p.SlotRect(2, true, 112f, 72f, 14f, 1.08f).xMax));
        }

        [Test]
        public void ReadyPulseUsesInjectedTimeButNeverInventsRechargeOrAim()
        {
            var p = new HUDInventoryPresenter(); var s = new HUDDriverState();
            Assert.That(p.FlashlightReady(s), Is.False); Assert.That(p.ReadyPulse(s), Is.Zero);
            p.SetFlashlight(s, false, 1f, .5f);
            Assert.That(s.FlashlightText, Is.EqualTo("Flashlight OFF")); Assert.That(s.FlashlightAim, Is.Zero);
            Assert.That(s.FlashlightStatusText, Is.EqualTo("Stun ready"));
            float first = p.ReadyPulse(s); p.Tick(s, .6f, 1.2f);
            Assert.That(p.ReadyPulse(s), Is.LessThan(first));
            p.SetFlashlight(s, true, .25f, .5f); p.Tick(s, 100f, 1.2f);
            Assert.That(s.FlashlightCharge, Is.EqualTo(.25f)); Assert.That(s.FlashlightAim, Is.Zero);
            Assert.That(s.FlashlightStatusText, Is.EqualTo("Recharge 25%"));
            p.SetFlashlight(s, true, 1f, .5f);
            Assert.That(s.FlashlightStatusText, Is.EqualTo("Aim 50%"));
            new HUDPresenter().SetChaseMode(s, true);
            Assert.That(s.ChromeVisible, Is.False); Assert.That(s.FlashlightAim, Is.EqualTo(.5f));
            s.HiddenCount = true;
            Assert.That(s.FlashlightKnown, Is.True, "Hidden Count affects cakes, not inventory.");
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidProgressNeverShowsReadyOrAiming(float invalid)
        {
            var p = new HUDInventoryPresenter(); var s = new HUDDriverState();
            p.SetFlashlight(s, true, invalid, invalid);
            Assert.That(s.FlashlightCharge, Is.Zero); Assert.That(s.FlashlightAim, Is.Zero);
            Assert.That(p.FlashlightReady(s), Is.False);
            p.Tick(s, invalid, 1.2f); Assert.That(s.FlashlightPulsePhase, Is.Zero);
        }
    }
}
