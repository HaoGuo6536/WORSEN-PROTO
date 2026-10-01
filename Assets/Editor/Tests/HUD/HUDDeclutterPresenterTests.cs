// ============================================================================
// HUDDeclutterPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies flat-arrow orientation and remaining-count facts through chase suppression.
//   The owner's three-slot contract preserves empty capacity without disturbing counts.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · HUD.
// KEY RESPONSIBILITIES:
//   - Cover cardinal and vertical arrow bearings plus occupied-slot distinction.
// DEPENDENCIES:
//   NUnit, Unity value types and HUD pure presentation.
// USAGE NOTES:
//   Pure tests; the separate visual fixture verifies absence of title/chrome/hints.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.HUD;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDDeclutterPresenterTests
    {
        [TestCase(0, 0, 1, 0)] [TestCase(1, 0, 0, 90)]
        [TestCase(-1, 0, 0, -90)] [TestCase(0, 0, -1, 180)]
        [TestCase(0, 1, 0, 0)] [TestCase(0, -1, 0, 180)]
        public void FlatArrowKeepsItsBearingAndVisibilityInAChase(float x, float y, float z, float angle)
        {
            var state = new HUDDriverState(); var p = new HUDPresenter();
            p.SetDirection(state, new Vector3(x, y, z), true); p.SetChaseMode(state, true);
            Assert.That(state.DirectionVisible, Is.True); Assert.That(state.ArrowDegrees, Is.EqualTo(angle).Within(.001));
        }
        [Test]
        public void GoldenFigureIsIndependentAndThreeSlotsAlwaysRemain()
        {
            var state = new HUDDriverState(); var p = new HUDPresenter();
            p.SetCount(state, 3, 8); p.SetGoldenCount(state, 4, 4); p.SetItemSlots(state, 2, 8);
            Assert.That(state.CountText, Is.EqualTo("5")); Assert.That(state.GoldenText, Is.EqualTo("0"));
            Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            p.SetHeldItemCount(state, 1, 8); Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            p.SetHeldItemCount(state, 0, 8); Assert.That(state.DisplayedSlots, Is.EqualTo(3));
            p.SetGoldenCount(state, -1); Assert.That(state.GoldenText, Is.EqualTo("—"));
        }
    }
}
