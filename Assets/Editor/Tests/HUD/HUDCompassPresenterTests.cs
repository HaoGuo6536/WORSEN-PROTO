// ============================================================================
// HUDCompassPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies three-dimensional compass projection across cardinal and vertical directions.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10) - test suite (section 11) - Presentation - HUD.
// KEY RESPONSIBILITIES:
//   - Bound white facet geometry and retain front/back and height cues.
// DEPENDENCIES:
//   NUnit, HUDCompassPresenter and Unity value types only.
// USAGE NOTES:
//   Pure tests; no scene, rendering or engine objects required.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.HUD;
namespace Worsen.Tests.HUD
{
    public sealed class HUDCompassPresenterTests
    {
        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(1f, 1f, -1f)]
        public void NeedleIsFiniteWhiteBoundedAndDepthSorted(float x, float y, float z)
        {
            var rect = new Rect(7f, 11f, 84f, 84f);
            var faces = new HUDCompassPresenter().Needle(rect, new Vector3(x, y, z));
            Assert.That(faces.Length, Is.EqualTo(18));
            float depth = float.NegativeInfinity;
            foreach (var face in faces)
            {
                foreach (Vector2 point in new[] { face.A, face.B, face.C })
                {
                    Assert.That(point.x, Is.InRange(rect.xMin, rect.xMax));
                    Assert.That(point.y, Is.InRange(rect.yMin, rect.yMax));
                }
                Assert.That(face.Depth, Is.GreaterThanOrEqualTo(depth)); depth = face.Depth;
                Assert.That(face.Color.r, Is.EqualTo(face.Color.g));
                Assert.That(face.Color.g, Is.EqualTo(face.Color.b));
                Assert.That(face.Color.r, Is.InRange(.66f, 1f));
                Assert.That(face.Color.a, Is.EqualTo(1f));
            }
        }

        [Test]
        public void CompassPlaneShowsOppositeDirectionsAndIndependentElevation()
        {
            var presenter = new HUDCompassPresenter(); var rect = new Rect(0, 0, 84, 84);
            Vector2 center = presenter.Project(rect, Vector3.zero);
            Assert.That(presenter.Project(rect, Vector3.forward).y, Is.LessThan(center.y));
            Assert.That(presenter.Project(rect, Vector3.back).y, Is.GreaterThan(center.y));
            Assert.That(presenter.Project(rect, Vector3.right).x, Is.GreaterThan(center.x));
            Assert.That(presenter.Project(rect, Vector3.left).x, Is.LessThan(center.x));
            Assert.That(presenter.Project(rect, Vector3.up).y, Is.LessThan(center.y));
            Assert.That(presenter.Project(rect, Vector3.down).y, Is.GreaterThan(center.y));
            Assert.That(presenter.Project(rect, Vector3.up).y,
                Is.Not.EqualTo(presenter.Project(rect, Vector3.forward).y).Within(.001f));
        }

        [Test]
        public void UnavailableDirectionAndUninitializedLayoutAreSafe()
        {
            var presenter = new HUDCompassPresenter();
            Assert.That(presenter.Needle(new Rect(), Vector3.zero), Is.Empty);
            Assert.That(presenter.Needle(new Rect(), new Vector3(float.NaN, 0, 1)), Is.Empty);
            var bounds = new Rect(float.NaN, float.PositiveInfinity, -1f, float.NaN);
            foreach (var face in presenter.Needle(bounds, Vector3.up))
            {
                Assert.That(face.A, Is.EqualTo(Vector2.zero));
                Assert.That(face.B, Is.EqualTo(Vector2.zero));
                Assert.That(face.C, Is.EqualTo(Vector2.zero));
            }
            foreach (var point in presenter.BaseRing(bounds)) Assert.That(point, Is.EqualTo(Vector2.zero));
        }
    }
}
