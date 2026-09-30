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
//   - Regress sampled origins, configurable corner skipping and fallback/held refreshes.
//   - Check warning periods and complete room-boundary blocker coverage.
// DEPENDENCIES:
//   - Worsen.Domain.Floor FloorPresenter, DriverState and UnityEngine value types only.
//   - NUnit provides assertions; no live engine objects are constructed.
// USAGE NOTES:
//   Editor-mode pure tests; all timing and geometry are supplied explicitly.
//   The Driver supplies null on sample/path failure; no navigation or physics is run here.
//   Supplied DriverState retains each target's direction between explicit refresh calls.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Floor;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
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

            AssertVector(new FloorPresenter().FirstDirection(origin, corners, 1f), new Vector3(0.6f, 0f, 0.8f));
        }

        [Test]
        public void FirstDirectionReturnsZeroWithoutAUsefulHorizontalCorner()
        {
            var presenter = new FloorPresenter();
            var origin = new Vector3(3f, 2f, 7f);

            AssertVector(presenter.FirstDirection(origin, null, 1f), Vector3.zero);
            AssertVector(presenter.FirstDirection(origin, new Vector3[0], 1f), Vector3.zero);
            AssertVector(presenter.FirstDirection(origin, new[] { origin, new Vector3(3f, 12f, 7f) }, 1f), Vector3.zero);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(-0.5f, 4f, 0.5f)]
        public void GuidanceUsesSampledOriginInsteadOfAirbornePlayer(float x, float height, float z)
        {
            var state = new FloorDriverState();
            var corners = new[] { Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 6f) };
            var result = new FloorPresenter().PathCandidate(state, 101, new Vector3(x, height, z),
                Vector3.zero, corners[2], corners, 1f);

            AssertVector(result.Direction, Vector3.right);
            Assert.That(result.Length, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(state.FallbackDirections, Is.Empty);
            Assert.That(state.HeldDirections, Is.Empty);
        }

        [Test]
        public void StairGuidanceSkipsHorizontallyNearCornersRegardlessOfHeight()
        {
            var sample = new Vector3(10f, 2f, -5f);
            var corners = new[] { sample, sample + new Vector3(0f, 3f, 0.5f),
                sample + new Vector3(3f, 6f, 0f), sample + new Vector3(3f, 8f, 4f) };
            var result = new FloorPresenter().PathCandidate(new FloorDriverState(), 101,
                sample + Vector3.up, sample, corners[3], corners, 1f);

            AssertVector(result.Direction, Vector3.right);
            Assert.That(result.Direction.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [TestCase(1f, 1f, 0f)]
        [TestCase(0.25f, 0f, 1f)]
        public void CornerSkipDistanceIsConfigurableAndIncludesTheBoundary(float skip, float x, float z)
        {
            var corners = new[] { Vector3.zero, Vector3.forward, Vector3.right * 4f };

            AssertVector(new FloorPresenter().FirstDirection(Vector3.zero, corners, skip), new Vector3(x, 0f, z));
        }

        [Test]
        public void AllNearbyCornersUseTheFinalCornerNotTheFirstNonzeroCorner()
        {
            var corners = new[] { Vector3.zero, Vector3.forward * 0.5f, Vector3.left * 0.75f };

            AssertVector(new FloorPresenter().FirstDirection(Vector3.zero, corners, 1f), Vector3.left);
        }

        [Test]
        public void FailedSampleOrPathUsesFlaggedStraightLineFromRawPlayer()
        {
            var state = new FloorDriverState();
            var result = new FloorPresenter().PathCandidate(state, 101, new Vector3(10f, 4f, 5f),
                Vector3.zero, new Vector3(13f, 4f, 9f), null, 1f);

            AssertVector(result.Direction, new Vector3(0.6f, 0f, 0.8f));
            Assert.That(result.Length, Is.EqualTo(FloorPresenter.FallbackRankOffset + 5f).Within(Tolerance));
            Assert.That(state.FallbackDirections.Contains(101), Is.True);
            Assert.That(state.HeldDirections.Contains(101), Is.False);
        }

        [Test]
        public void FailedPathRanksAfterAnyCompletePathEvenWhenCloserInAStraightLine()
        {
            var presenter = new FloorPresenter();
            var state = new FloorDriverState();
            var longRoute = new[] { Vector3.zero, Vector3.right * 400f, new Vector3(400f, 0f, 400f) };
            var complete = presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero,
                longRoute[2], longRoute, 1f);
            var failed = presenter.PathCandidate(state, 102, Vector3.zero, Vector3.zero,
                Vector3.forward * 3f, null, 1f);

            Assert.That(complete.Length, Is.EqualTo(800f).Within(Tolerance));
            Assert.That(failed.Length, Is.GreaterThan(complete.Length));
            Assert.That(state.FallbackDirections.Contains(102), Is.True);
            Assert.That(state.FallbackDirections.Contains(101), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InvalidPathDataUsesFallback(int kind)
        {
            var corners = kind == 0 ? new Vector3[0] : kind == 1 ? new[] { Vector3.zero }
                : new[] { Vector3.zero, new Vector3(float.NaN, 0f, 1f) };
            var state = new FloorDriverState();
            var result = new FloorPresenter().PathCandidate(state, 101, Vector3.zero, Vector3.zero,
                Vector3.forward * 3f, corners, 1f);

            AssertVector(result.Direction, Vector3.forward);
            Assert.That(state.FallbackDirections.Contains(101), Is.True);
            Assert.That(result.Length, Is.EqualTo(FloorPresenter.FallbackRankOffset + 3f).Within(Tolerance));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedPathAndStraightLineHoldLastGoodDirectionAcrossRefreshes(bool previousFallback)
        {
            var state = new FloorDriverState();
            var presenter = new FloorPresenter();
            presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero, Vector3.right * 4f,
                previousFallback ? null : new[] { Vector3.zero, Vector3.right * 4f }, 1f);
            for (int refresh = 0; refresh < 2; refresh++)
            {
                var result = presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero,
                    Vector3.up * 3f, null, 1f);

                AssertVector(result.Direction, Vector3.right);
                Assert.That(result.Length, Is.EqualTo(FloorPresenter.FallbackRankOffset + 3f).Within(Tolerance));
                Assert.That(state.HeldDirections.Contains(101), Is.True);
                Assert.That(state.FallbackDirections.Contains(101), Is.False);
            }
            presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero, Vector3.forward * 4f,
                new[] { Vector3.zero, Vector3.forward * 4f }, 1f);
            AssertVector(state.LastGoodDirections[101], Vector3.forward);
            Assert.That(state.HeldDirections, Is.Empty);
            Assert.That(state.FallbackDirections, Is.Empty);
        }

        [Test]
        public void CandidateHistoryDoesNotBorrowAnotherCakeOrExitDirection()
        {
            var state = new FloorDriverState();
            var presenter = new FloorPresenter();
            presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero, Vector3.right * 4f, null, 1f);
            presenter.PathCandidate(state, 102, Vector3.zero, Vector3.zero, Vector3.forward * 4f, null, 1f);
            var held = presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero, Vector3.up, null, 1f);
            var exit = presenter.PathCandidate(state, 0, Vector3.zero, Vector3.zero, Vector3.up, null, 1f);

            AssertVector(held.Direction, Vector3.right);
            AssertVector(exit.Direction, Vector3.zero);
            Assert.That(state.HeldDirections.Contains(0), Is.False);
            Assert.That(exit.Length, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void ZeroHorizontalPathUsesFallbackThenHoldRatherThanPublishingZero()
        {
            var state = new FloorDriverState();
            var presenter = new FloorPresenter();
            var vertical = new[] { Vector3.zero, Vector3.up * 3f };
            var fallback = presenter.PathCandidate(state, 101, Vector3.left, Vector3.zero, Vector3.up * 3f, vertical, 1f);
            AssertVector(fallback.Direction, Vector3.right);
            Assert.That(state.FallbackDirections.Contains(101), Is.True);
            var held = presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero, Vector3.up * 3f, vertical, 1f);
            AssertVector(held.Direction, Vector3.right);
            Assert.That(state.HeldDirections.Contains(101), Is.True);
        }

        [Test]
        public void ZeroLengthWithoutHistoryIsUnavailableAndNeverNaN()
        {
            var state = new FloorDriverState();
            var presenter = new FloorPresenter();
            var result = presenter.PathCandidate(state, 101, Vector3.zero, Vector3.zero, Vector3.zero,
                new[] { Vector3.zero, Vector3.zero }, 1f);

            AssertVector(result.Direction, Vector3.zero);
            Assert.That(result.Length, Is.EqualTo(float.PositiveInfinity));
            Assert.That(state.LastGoodDirections, Is.Empty);
            Assert.That(state.FallbackDirections, Is.Empty);
            Assert.That(state.HeldDirections, Is.Empty);
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
