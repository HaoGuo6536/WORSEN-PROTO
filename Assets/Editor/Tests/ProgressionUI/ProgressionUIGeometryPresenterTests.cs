// ============================================================================
// ProgressionUIGeometryPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies vector paths remain within supplied local dimensions on menu resize.
//   It also checks incomplete layout and invalid health fill inputs before the
//   live drawing callbacks receive these plain geometry values.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Check clipped corners, bounded emblems and safe zero-size gauge paths.
//
// DEPENDENCIES:
//   Own pure geometry Presenter, NUnit and Unity value types only.
//
// USAGE NOTES:
//   Pure Edit Mode tests; no UI objects or engine calls.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.ProgressionUI;

namespace Worsen.Tests.ProgressionUI
{
    public sealed class ProgressionUIGeometryPresenterTests
    {
        [TestCase(ProgressionUIAction.ChooseThreat)]
        [TestCase(ProgressionUIAction.ChooseCurse)]
        [TestCase(ProgressionUIAction.Purchase)]
        public void EmblemsStayInsideTheirResizedSlot(ProgressionUIAction action)
        {
            foreach (var p in new ProgressionUIGeometryPresenter().Emblem(new Rect(0, 0, 64, 32), action))
            {
                Assert.That(p.x, Is.InRange(0, 64));
                Assert.That(p.y, Is.InRange(0, 32));
            }
        }

        [Test]
        public void LargeCornerCutCannotInvertSmallPanel()
        {
            foreach (var p in new ProgressionUIGeometryPresenter().Panel(new Rect(0, 0, 8, 4), 100))
            {
                Assert.That(p.x, Is.InRange(0, 8));
                Assert.That(p.y, Is.InRange(0, 4));
            }
        }

        [Test]
        public void UnknownHealthAndUninitializedLayoutProduceEmptyFiniteFill()
        {
            var p = new ProgressionUIGeometryPresenter();
            Assert.That(p.Gauge(new Rect(0, 0, 100, 5), float.NaN).width, Is.Zero);
            var path = p.Panel(new Rect(0, 0, float.NaN, -10), float.PositiveInfinity);
            foreach (var point in path) Assert.That(point, Is.EqualTo(Vector2.zero));
        }
    }
}
