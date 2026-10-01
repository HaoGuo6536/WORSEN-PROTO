// ============================================================================
// HUDConsumableTests.cs
// ============================================================================
// PURPOSE:
//   Verifies three physical slots without compacting holes after consumption.
//   Empty selections remain highlighted and legacy display limits cannot hide them.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HUD.
// KEY RESPONSIBILITIES:
//   - Cover item names, remaining uses, selection highlights and empty/overflow slots.
// DEPENDENCIES:
//   - Core inventory values, HUDPresenter/DriverState and NUnit.
// USAGE NOTES:
//   Pure Edit Mode tests; no document, camera or engine clock.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.HUD;

namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDConsumableTests
    {
        [Test]
        public void SelectedPhysicalSlotShowsItsNameUsesAndStableHighlight()
        {
            var state = new HUDDriverState(); var hud = new HUDPresenter();
            var slots = new[] { default(ProgressionInventorySlot), new ProgressionInventorySlot("gauze", "Gauze", 4), new ProgressionInventorySlot("firecracker", "Firecracker", 4) };
            hud.SetConsumables(state, new ConsumableInventorySnapshot(slots, new[] { 0, 1, 2 }, 2), 3);
            Assert.That(state.DisplayedSlots, Is.EqualTo(3)); Assert.That(state.SelectedDisplaySlot, Is.EqualTo(2));
            Assert.That(state.SelectedSlotText, Is.EqualTo("3: Firecracker ×2"));
            hud.SetChaseMode(state, true); Assert.That(state.SelectedSlotText, Is.EqualTo("3: Firecracker ×2"));
            hud.SetConsumables(state, new ConsumableInventorySnapshot(slots, new[] { 0, 1, 2 }, 0), 3);
            Assert.That(state.SelectedSlotText, Is.EqualTo("1: Empty")); Assert.That(state.SelectedDisplaySlot, Is.Zero);
            hud.SetConsumables(state, new ConsumableInventorySnapshot(slots, new[] { 0, 1, 2 }, 2), 1);
            Assert.That(state.SelectedDisplaySlot, Is.EqualTo(2)); Assert.That(state.SelectedSlotText, Does.Contain("Firecracker"));
            hud.SetConsumables(state, default, 3); Assert.That(state.DisplayedSlots, Is.EqualTo(3)); Assert.That(state.SelectedSlotText, Is.Empty);
        }

        [Test]
        public void ExhaustedInventoryClearsTheItemButKeepsItsPhysicalSelection()
        {
            var state = new HUDDriverState(); var hud = new HUDPresenter();
            hud.SetConsumables(state, new ConsumableInventorySnapshot(
                new[] { new ProgressionInventorySlot("gauze", "Gauze", 4) }, new[] { 1 }, 0), 3);
            Assert.That(state.SelectedSlotText, Is.EqualTo("1: Gauze ×1"));
            hud.SetConsumables(state, new ConsumableInventorySnapshot(
                new[] { default(ProgressionInventorySlot) }, new[] { 0 }, 0), 3);
            Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            Assert.That(state.SelectedDisplaySlot, Is.Zero);
            Assert.That(state.SelectedSlotText, Is.EqualTo("1: Empty"));
            Assert.That(state.SlotOverflowText, Is.Empty);
        }
    }
}
