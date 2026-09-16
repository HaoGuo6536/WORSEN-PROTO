// ============================================================================
// FloorPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the pure geometry and warning calculations used by FloorDriver.
//   Fixed corner paths and offset room bounds expose direction, distance and
//   placement mistakes without requiring a scene or a navigation bake.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Check path distance and first useful horizontal direction.
//   - Check warning periods and complete room-boundary blocker coverage.
// DEPENDENCIES:
//   - Worsen.Domain.Floor FloorPresenter and UnityEngine value types only.
//   - NUnit provides assertions; no live engine objects are constructed.
// USAGE NOTES:
//   Editor-mode pure tests; all timing and geometry are supplied explicitly.
//   Navigation path completion status is validated by the owning Driver before
//   these corner calculations; this suite tests insufficient corner data only.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Floor;

namespace Worsen.Tests.Floor
{
    public sealed class FloorPresenterTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void PathLengthFollowsEveryBendInsteadOfStraightLineDistance()
        {
            var corners = new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) };
            var presenter = new FloorPresenter();

            Assert.That(presenter.PathLength(corners), Is.EqualTo(7f).Within(Tolerance));
            Assert.That(presenter.PathLength(corners), Is.GreaterThan(Vector3.Distance(corners[0], corners[2])));
        }

        [Test]
        public void PathLengthIncludesVerticalTravelAndIgnoresRepeatedCorners()
        {
            var corners = new[] { Vector3.zero, Vector3.zero, new Vector3(0f, 3f, 4f), new Vector3(0f, 6f, 8f) };

            Assert.That(new FloorPresenter().PathLength(corners), Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void PathLengthRejectsMissingOrInsufficientCorners()
        {
            var presenter = new FloorPresenter();

            Assert.That(presenter.PathLength(null), Is.EqualTo(float.PositiveInfinity));
            Assert.That(presenter.PathLength(new Vector3[0]), Is.EqualTo(float.PositiveInfinity));
            Assert.That(presenter.PathLength(new[] { Vector3.one }), Is.EqualTo(float.PositiveInfinity));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void PathLengthRejectsNonfiniteCorners(float invalid)
        {
            var presenter = new FloorPresenter();
            var badCorner = new Vector3(invalid, 0f, 0f);

            Assert.That(presenter.PathLength(new[] { badCorner, Vector3.one }), Is.EqualTo(float.PositiveInfinity));
            Assert.That(presenter.PathLength(new[] { Vector3.zero, Vector3.one, badCorner }), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void FirstDirectionUsesFirstUsefulCornerAndFlattensHeight()
        {
            var origin = new Vector3(10f, 2f, -5f);
            var corners = new[]
            {
                origin,
                new Vector3(10f, 8f, -5f),
                new Vector3(13f, 11f, -1f),
                new Vector3(-10f, 2f, -5f)
            };

            AssertVector(new FloorPresenter().FirstDirection(origin, corners), new Vector3(0.6f, 0f, 0.8f));
        }

        [Test]
        public void FirstDirectionReturnsZeroWithoutAUsefulHorizontalCorner()
        {
            var presenter = new FloorPresenter();
            var origin = new Vector3(3f, 2f, 7f);

            AssertVector(presenter.FirstDirection(origin, null), Vector3.zero);
            AssertVector(presenter.FirstDirection(origin, new Vector3[0]), Vector3.zero);
            AssertVector(presenter.FirstDirection(origin, new[] { origin, new Vector3(3f, 12f, 7f) }), Vector3.zero);
        }

        [TestCase(0f, 2f)]
        [TestCase(0.5f, 4f)]
        [TestCase(1f, 2f)]
        [TestCase(1.5f, 0f)]
        [TestCase(2f, 2f)]
        public void WarningIntensityPulsesAcrossTheConfiguredPeriod(float elapsed, float expected)
        {
            var presenter = new FloorPresenter();

            Assert.That(presenter.WarningIntensity(elapsed, 2f, 4f), Is.EqualTo(expected).Within(Tolerance));
            Assert.That(presenter.WarningIntensity(elapsed + 2f, 2f, 4f), Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(0f, 4f)]
        [TestCase(-2f, 4f)]
        [TestCase(2f, 0f)]
        [TestCase(2f, -4f)]
        public void WarningIntensityIsOffForDisabledPeriodOrMaximum(float period, float maximum)
        {
            Assert.That(new FloorPresenter().WarningIntensity(0.5f, period, maximum), Is.Zero);
        }

        [Test]
        public void BoundaryBlockersCoverAllFourWorldSpaceSidesAtFullRoomHeight()
        {
            var room = new Bounds(new Vector3(10f, 3f, -6f), new Vector3(8f, 6f, 12f));
            var blockers = new FloorPresenter().BoundaryBlockers(room, 0.25f);
            var expectedCenters = new[]
            {
                new Vector3(6f, 3f, -6f), new Vector3(14f, 3f, -6f),
                new Vector3(10f, 3f, -12f), new Vector3(10f, 3f, 0f)
            };
            var expectedSizes = new[]
            {
                new Vector3(0.25f, 6f, 12f), new Vector3(0.25f, 6f, 12f),
                new Vector3(8f, 6f, 0.25f), new Vector3(8f, 6f, 0.25f)
            };

            Assert.That(blockers, Has.Length.EqualTo(4));
            for (var index = 0; index < blockers.Length; index++)
            {
                AssertVector(blockers[index].center, expectedCenters[index]);
                AssertVector(blockers[index].size, expectedSizes[index]);
                Assert.That(blockers[index].min.y, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(blockers[index].max.y, Is.EqualTo(6f).Within(Tolerance));
                bool centerOutsideWall = room.center.x < blockers[index].min.x
                    || room.center.x > blockers[index].max.x
                    || room.center.z < blockers[index].min.z
                    || room.center.z > blockers[index].max.z;
                Assert.That(centerOutsideWall, Is.True);
            }
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
