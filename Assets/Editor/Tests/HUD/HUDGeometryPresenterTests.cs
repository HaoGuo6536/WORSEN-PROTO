// ============================================================================
// HUDGeometryPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Tests vector geometry on narrow and invalid layouts without opening Unity UI.
//   These cases protect gauge overflow and cut-corner clipping during document
//   creation and resizing, when usable dimensions may not yet be available.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Check bounded gauge output and non-inverted panel/slot geometry.
//
// DEPENDENCIES:
//   NUnit, own HUDGeometryPresenter and Unity value types only.
//
// USAGE NOTES:
//   Pure Edit Mode tests; no scene, engine object or global time.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.HUD;

namespace Worsen.Tests.HUD
{
    public sealed class HUDGeometryPresenterTests
    {
        [Test]
        public void NarrowPanelClampsCutAndKeepsEveryVertexInsideBounds()
        {
            var rect = new Rect(4, 7, 12, 6);
            foreach (var point in new HUDGeometryPresenter().Panel(rect, 99))
            {
                Assert.That(point.x, Is.InRange(rect.xMin, rect.xMax));
                Assert.That(point.y, Is.InRange(rect.yMin, rect.yMax));
            }
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0.25f, 50f)]
        [TestCase(9f, 200f)]
        public void GaugeNeverOverflowsOrReverses(float fraction, float width)
        {
            var result = new HUDGeometryPresenter().Gauge(new Rect(1, 2, 200, 6), fraction);
            Assert.That(result.width, Is.EqualTo(width));
            Assert.That(result.position, Is.EqualTo(new Vector2(1, 2)));
        }

        [Test]
        public void InvalidDimensionsDoNotProduceNonfinitePanelVertices()
        {
            foreach (var p in new HUDGeometryPresenter().Panel(new Rect(0, 0, float.NaN, -8), float.PositiveInfinity))
            {
                Assert.That(float.IsNaN(p.x) || float.IsInfinity(p.x), Is.False);
                Assert.That(float.IsNaN(p.y) || float.IsInfinity(p.y), Is.False);
            }
            Assert.That(new HUDGeometryPresenter().Slot(2, 32, 8).x, Is.EqualTo(80));
        }
    }
}
