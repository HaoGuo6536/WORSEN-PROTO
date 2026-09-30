// ============================================================================
// HUDConsumableTests.cs
// ============================================================================
// PURPOSE:
//   Verifies physical inventory selection without drawing empty item outlines.
//   The selected caption stays meaningful for empty slots and overflow capacity.
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
    public sealed class HUDConsumableTests
    {
        [Test]
        public void SelectedPhysicalSlotShowsItsNameUsesAndCompactHighlight()
        {
            var state = new HUDDriverState(); var hud = new HUDPresenter();
            var slots = new[] { default(ProgressionInventorySlot), new ProgressionInventorySlot("gauze", "Gauze", 4), new ProgressionInventorySlot("firecracker", "Firecracker", 4) };
            hud.SetConsumables(state, new ConsumableInventorySnapshot(slots, new[] { 0, 1, 2 }, 2), 3);
            Assert.That(state.DisplayedSlots, Is.EqualTo(2)); Assert.That(state.SelectedDisplaySlot, Is.EqualTo(1));
            Assert.That(state.SelectedSlotText, Is.EqualTo("3: Firecracker ×2"));
            hud.SetChaseMode(state, true); Assert.That(state.SelectedSlotText, Is.EqualTo("3: Firecracker ×2"));
            hud.SetConsumables(state, new ConsumableInventorySnapshot(slots, new[] { 0, 1, 2 }, 0), 3);
            Assert.That(state.SelectedSlotText, Is.EqualTo("1: Empty")); Assert.That(state.SelectedDisplaySlot, Is.EqualTo(-1));
            hud.SetConsumables(state, new ConsumableInventorySnapshot(slots, new[] { 0, 1, 2 }, 2), 1);
            Assert.That(state.SelectedDisplaySlot, Is.EqualTo(-1)); Assert.That(state.SelectedSlotText, Does.Contain("Firecracker"));
            hud.SetConsumables(state, default, 3); Assert.That(state.DisplayedSlots, Is.Zero); Assert.That(state.SelectedSlotText, Is.Empty);
        }
    }
}
