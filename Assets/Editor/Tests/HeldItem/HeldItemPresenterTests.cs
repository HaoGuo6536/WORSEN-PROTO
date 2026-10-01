// ============================================================================
// HeldItemPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies physical selection and animated handoffs without opening a scene.
//   Explicit time protects empty-slot hiding, same-kind slot changes and rapid
//   selection reversals from stale items or automatic use side effects.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HeldItem.
// KEY RESPONSIBILITIES:
//   - Verify supported selection, remaining uses and interrupted raise/lower motion.
//   - Verify suppression and bounded cosmetic sway independently of camera aim.
// DEPENDENCIES:
//   NUnit, Core and HeldItem pure presentation only.
// USAGE NOTES:
//   Pure Edit Mode/headless fixture; no native objects or clock reads.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.HeldItem;
namespace Worsen.Tests.HeldItem
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HeldItemPresenterTests
    {
        private static ConsumableInventorySnapshot Slots(int selected, int uses = 1) => new ConsumableInventorySnapshot(
            new[] { new ProgressionInventorySlot("gauze", "Gauze", 0), default,
                new ProgressionInventorySlot("glass-vial", "Vial", 0) }, new[] { uses, 0, 1 }, selected);
        [TestCase(0, "gauze")] [TestCase(1, "")] [TestCase(2, "glass-vial")] [TestCase(-1, "")] [TestCase(3, "")]
        public void SelectedPhysicalSlotDoesNotCompactHoles(int slot, string id)
        {
            var p = new HeldItemPresenter(); var s = new HeldItemDriverState(); var inventory = Slots(slot);
            p.SetSelection(s, inventory); p.Tick(s, .16f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.EqualTo(id));
            Assert.That(s.Raise, Is.EqualTo(id.Length > 0 ? 1f : 0f));
            Assert.That(inventory.RemainingUses[0], Is.EqualTo(1), "Selection never consumes inventory.");
        }
        [Test]
        public void SelectionChangeLowersOldThenRaisesNewAndConsumptionClearsIt()
        {
            var p = new HeldItemPresenter(); var s = new HeldItemDriverState();
            p.SetSelection(s, Slots(0)); p.Tick(s, .16f, .16f, 2.4f);
            p.SetSelection(s, Slots(2)); p.Tick(s, .08f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.EqualTo("gauze")); Assert.That(s.Raise, Is.EqualTo(.5f).Within(.0001f));
            p.Tick(s, .16f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.EqualTo("glass-vial")); Assert.That(s.Raise, Is.EqualTo(.5f).Within(.0001f));
            p.SetSelection(s, default); p.Tick(s, .2f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.Empty); Assert.That(s.Raise, Is.Zero);
        }
        [Test]
        public void ReversalAndSameKindSlotChangesRemainInterruptible()
        {
            var p = new HeldItemPresenter(); var s = new HeldItemDriverState();
            p.SetSelection(s, Slots(0)); p.Tick(s, .16f, .16f, 2.4f);
            p.SetSelection(s, Slots(2)); p.Tick(s, .08f, .16f, 2.4f);
            p.SetSelection(s, Slots(0)); p.Tick(s, .04f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.EqualTo("gauze")); Assert.That(s.Raise, Is.EqualTo(.75f).Within(.0001f));
            p.SetSelection(s, new ConsumableInventorySnapshot(new[] { new ProgressionInventorySlot("gauze", "Gauze", 0),
                new ProgressionInventorySlot("gauze", "Gauze", 0) }, new[] { 1, 1 }, 1));
            p.Tick(s, .04f, .16f, 2.4f);
            Assert.That(s.DisplayedSlot, Is.Zero); Assert.That(s.Raise, Is.EqualTo(.5f).Within(.0001f));
        }
        [Test]
        public void ExhaustedUnknownAndSuppressedSelectionsNeverRemainVisible()
        {
            var p = new HeldItemPresenter(); var s = new HeldItemDriverState();
            p.SetSelection(s, Slots(0, 0)); Assert.That(s.SelectedId, Is.Empty);
            p.SetSelection(s, new ConsumableInventorySnapshot(new[] { new ProgressionInventorySlot("unknown", "Unknown", 0) }, new[] { 1 }, 0));
            Assert.That(s.SelectedId, Is.Empty);
            p.SetSelection(s, Slots(2)); p.Tick(s, 1f, .16f, 2.4f); p.SetSuppressed(s, true);
            p.Tick(s, 1f, .16f, 2.4f); Assert.That(s.Raise, Is.Zero); Assert.That(s.DisplayedId, Is.Empty);
            p.SetSuppressed(s, false); p.Tick(s, .08f, .16f, 2.4f);
            Assert.That(s.Raise, Is.EqualTo(.5f).Within(.0001f)); Assert.That(s.DisplayedId, Is.EqualTo("glass-vial"));
        }
        [Test]
        public void ExhaustedSlotLowersForTheConfiguredDurationBeforeClearing()
        {
            var p = new HeldItemPresenter(); var s = new HeldItemDriverState();
            p.SetSelection(s, Slots(0)); p.Tick(s, .16f, .16f, 2.4f);
            p.SetSelection(s, Slots(0, 0));
            p.Tick(s, .08f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.EqualTo("gauze")); Assert.That(s.Raise, Is.EqualTo(.5f).Within(.0001f));
            p.Tick(s, .08f, .16f, 2.4f);
            Assert.That(s.DisplayedId, Is.Empty); Assert.That(s.DisplayedSlot, Is.EqualTo(-1)); Assert.That(s.Raise, Is.Zero);
        }
        [Test]
        public void TransitionIsFramePartitionIndependentAndSwayStaysSmall()
        {
            var p = new HeldItemPresenter(); var a = new HeldItemDriverState(); var b = new HeldItemDriverState();
            foreach (var s in new[] { a, b }) { p.SetSelection(s, Slots(0)); p.Tick(s, .16f, .16f, 2.4f); p.SetSelection(s, Slots(2)); }
            p.Tick(a, .24f, .16f, 2.4f);
            for (int i = 0; i < 24; i++) p.Tick(b, .01f, .16f, 2.4f);
            Assert.That(a.Raise, Is.EqualTo(b.Raise).Within(.0001f)); Assert.That(a.DisplayedId, Is.EqualTo(b.DisplayedId));
            p.Tick(a, 1f, .16f, 2.4f);
            for (int i = 0; i < 120; i++)
            {
                p.Tick(a, .02f, .16f, 2.4f); var position = p.Position(a, new Vector3(.22f, -.19f, .45f), .35f, .004f);
                Assert.That(position.x, Is.InRange(.216f, .2241f)); Assert.That(position.y, Is.InRange(-.1941f, -.186f));
                Assert.That(position.z, Is.EqualTo(.45f));
            }
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(0f)]
        public void InvalidTimeDoesNotAdvancePose(float dt)
        {
            var p = new HeldItemPresenter(); var s = new HeldItemDriverState(); p.SetSelection(s, Slots(0)); p.Tick(s, dt, .16f, 2.4f);
            Assert.That(s.Raise, Is.Zero); Assert.That(s.IdlePhase, Is.Zero);
        }
        [TestCase(40f, 1.7777778f)] [TestCase(60f, 1.7777778f)] [TestCase(80f, 2.3333333f)]
        public void ViewModelPlacementAndSizeStayStableThroughFovAndAspectChanges(float fov, float aspect)
        {
            var p = new HeldItemPresenter(); var position = p.ViewPosition(new Vector3(.22f, -.12f, .45f), fov, aspect);
            float halfHeight = .45f * (float)System.Math.Tan(fov * System.Math.PI / 360d);
            Assert.That(position.x / (halfHeight * aspect), Is.EqualTo(.22f / (.45f * (float)System.Math.Tan(System.Math.PI / 6d) * (16f/9f))).Within(.001f));
            Assert.That(position.y / halfHeight, Is.EqualTo(-.12f / (.45f * (float)System.Math.Tan(System.Math.PI / 6d))).Within(.001f));
            Assert.That((position.y - .10f * p.ProjectionScale(fov)) / halfHeight, Is.GreaterThan(-1f), "Tallest silhouette stays inside the view.");
        }
    }
}
