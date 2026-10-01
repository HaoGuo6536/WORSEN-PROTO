// ============================================================================
// PlayerMoverPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks collision and interpolation math using independent geometric expectations.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Reproduce separating drop-edge snap admission, jump rejection and uphill support.
//   - Reproduce the 26.6-degree overlap sentinel, uphill support, ledge gates and slide redirects.
//   - Verify grace mask exclusion, invalid-layer safety and the session warning latch.
//   - Verify paired traversal landings across approach directions and malformed geometry.
//   - Check geometry-derived phases, clearance and eased rise/fall joins to the apex hold.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Pure NUnit tests without scene objects or physics queries; Driver integration remains a separate live gate.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Player;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerMoverPresenterTests
    {
        private readonly PlayerMoverPresenter _presenter = new PlayerMoverPresenter();

        [TestCase(.25f, .95f)]
        [TestCase(.4f, .9f)]
        [TestCase(.01f, .99f)]
        public void TraversalHasMatchingValuesAndFirstDerivativesAtBothPhaseBoundaries(float rise, float end)
        {
            var from = new Vector3(0f, .2f, 0f);
            var to = new Vector3(2f, .5f, 3f);
            const float top = 1.28f;
            foreach (float boundary in new[] { rise, end })
            {
                float epsilon = Mathf.Min(rise, 1f - end) * .002f;
                Vector3 left = _presenter.TraversalPosition(from, to, boundary - epsilon, 1f, .08f, rise, end);
                Vector3 center = _presenter.TraversalPosition(from, to, boundary, 1f, .08f, rise, end);
                Vector3 right = _presenter.TraversalPosition(from, to, boundary + epsilon, 1f, .08f, rise, end);
                Assert.That(center.y, Is.EqualTo(top).Within(.00001f));
                Assert.That(left.y, Is.EqualTo(center.y).Within(.00002f));
                Assert.That(right.y, Is.EqualTo(center.y).Within(.00002f));
                // Normalize the one-sided derivatives by each phase's height/time scale.
                // C0 linear ramps have a normalized jump of 1; these quadratic joins tend to 0.
                float scale = boundary == rise ? rise / (top - from.y) : (1f - end) / (top - to.y);
                Assert.That(Mathf.Abs((center.y - left.y) / epsilon * scale), Is.LessThan(.01f));
                Assert.That(Mathf.Abs((right.y - center.y) / epsilon * scale), Is.LessThan(.01f));
                Assert.That((right.x - left.x) / (2f * epsilon), Is.EqualTo(2f).Within(.02f));
            }
            Assert.That(_presenter.TraversalPosition(from, to, 0f, 1f, .08f, rise, end), Is.EqualTo(from));
            Assert.That(_presenter.TraversalPosition(from, to, 1f, 1f, .08f, rise, end), Is.EqualTo(to));
        }

        [TestCase(.25f, .95f)]
        [TestCase(.4f, .9f)]
        public void EveryEasingPhaseIsMonotoneAndItsVelocityIsBounded(float rise, float end)
        {
            const float epsilon = .0001f, height = 1.08f;
            float previous = 0f;
            for (int i = 1; i <= 10000; i++)
            {
                float p = i * epsilon;
                float y = _presenter.TraversalPosition(Vector3.zero, Vector3.forward * 2f,
                    p, 1f, .08f, rise, end).y;
                Assert.That(y, Is.InRange(0f, height));
                float velocity = (y - previous) / epsilon;
                float bound = p <= rise ? 2f * height / rise : p > end ? 2f * height / (1f - end) : .02f;
                Assert.That(Mathf.Abs(velocity), Is.LessThanOrEqualTo(bound + .02f));
                if (p <= rise) Assert.That(y, Is.GreaterThanOrEqualTo(previous));
                if (p > end) Assert.That(y, Is.LessThanOrEqualTo(previous));
                previous = y;
            }
        }
        [TestCase(.25f, .95f)]
        [TestCase(.4f, .7f)]
        public void RiseEasesOutAtLeastAsHighAsLinearAndFallEasesIn(float rise, float end)
        {
            const float top = 1.08f;
            float previousRiseStep = float.PositiveInfinity, previousFallStep = 0f;
            float previousRise = 0f, previousFall = top;
            for (int i = 1; i <= 100; i++)
            {
                float t = i / 100f;
                float up = _presenter.TraversalPosition(Vector3.zero, Vector3.forward * 2f,
                    t * rise, 1f, .08f, rise, end).y;
                float down = _presenter.TraversalPosition(Vector3.zero, Vector3.forward * 2f,
                    end + t * (1f - end), 1f, .08f, rise, end).y;
                Assert.That(up, Is.EqualTo(top * (1f - (1f - t) * (1f - t))).Within(.00001f));
                Assert.That(down, Is.EqualTo(top * (1f - t * t)).Within(.00001f));
                Assert.That(up, Is.GreaterThanOrEqualTo(top * t - .00001f), "Never lose linear-rise near-face clearance.");
                Assert.That(up - previousRise, Is.InRange(0f, previousRiseStep + .00001f));
                Assert.That(previousFall - down, Is.GreaterThanOrEqualTo(previousFallStep - .00001f));
                previousRiseStep = up - previousRise; previousFallStep = previousFall - down;
                previousRise = up; previousFall = down;
            }
            // One-sided endpoint slopes are intentionally nonzero: fastest at takeoff/landing.
            const float epsilon = .0001f;
            float takeoff = _presenter.TraversalPosition(Vector3.zero, Vector3.forward,
                epsilon * rise, 1f, .08f, rise, end).y / (epsilon * top);
            float landing = _presenter.TraversalPosition(Vector3.zero, Vector3.forward,
                1f - epsilon * (1f - end), 1f, .08f, rise, end).y / (epsilon * top);
            Assert.That(takeoff, Is.EqualTo(2f).Within(.02f));
            Assert.That(landing, Is.EqualTo(2f).Within(.02f));
        }

        [TestCase(0f, 4f, 1f, 1.2f)] // Thin obstacle: long real descent span.
        [TestCase(0f, 4f, 1f, 3.4f)] // Deep obstacle: retain apex until the back clears.
        [TestCase(.68f, 4f, 1f, 2f)] // Closest clear start: full radius + skin from face.
        [TestCase(-2f, 4f, 1f, 2f)] // Far start: derive rise, do not force fallback .25.
        [TestCase(0f, 4f, 1f, 3.66f)] // Descent starts beyond the old .99 clamp.
        [TestCase(.70f, 4f, 1f, 2f)] // Rise ends before the old .01 clamp.
        public void GeometryPhasesUseBothFacesCapsuleAndSkinInEitherDirection(float start, float finish, float near, float far)
        {
            const float radius = .3f, skin = .02f;
            foreach (float direction in new[] { -1f, 1f })
            {
                Vector3 from = Vector3.right * (direction * start);
                Vector3 to = Vector3.right * (direction * finish);
                var bounds = new Bounds(new Vector3(direction * (near + far) * .5f, .5f, 0f), new Vector3(far - near, 1f, 2f));
                _presenter.TraversalPhases(from, to, bounds, radius, skin, .25f, .95f, out float rise, out float end);
                Assert.That(start + (finish - start) * rise + radius, Is.EqualTo(near + skin).Within(.00001f));
                Assert.That(start + (finish - start) * end - radius, Is.EqualTo(far + skin).Within(.00001f));
                Assert.That(rise, Is.GreaterThan(0f).And.LessThan(end));
                Assert.That(end, Is.LessThan(1f));
                Assert.That(_presenter.TraversalPosition(from, to, rise, 1f, .08f, rise, end).y,
                    Is.EqualTo(1.08f).Within(.00001f), "The evaluator must not stretch a short geometric rise.");
                Assert.That(_presenter.TraversalPosition(from, to, end, 1f, .08f, rise, end).y,
                    Is.EqualTo(1.08f).Within(.00001f), "The evaluator must not descend early on a deep obstacle.");
                Assert.That(_presenter.TraversalPosition(from, to, 0f, 1f, .08f, rise, end), Is.EqualTo(from));
                Assert.That(_presenter.TraversalPosition(from, to, 1f, 1f, .08f, rise, end), Is.EqualTo(to));
            }
        }

        [Test]
        public void GeometryPhasesProjectWorldBoundsAlongAnObliqueHorizontalPath()
        {
            var from = new Vector3(10f, 2f, -4f);
            var direction = new Vector3(.6f, 0f, .8f);
            var to = from + direction * 5f + Vector3.up;
            var bounds = new Bounds(from + direction * 2.5f, new Vector3(.8f, 2f, 1.2f));
            _presenter.TraversalPhases(from, to, bounds, .3f, .02f, .25f, .95f, out float rise, out float end);
            float near = float.PositiveInfinity, far = float.NegativeInfinity;
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float z in new[] { bounds.min.z, bounds.max.z })
            {
                float along = Vector3.Dot(new Vector3(x, from.y, z) - from, direction);
                near = Mathf.Min(near, along); far = Mathf.Max(far, along);
            }
            Assert.That(rise * 5f + .3f, Is.EqualTo(near + .02f).Within(.00001f));
            Assert.That(end * 5f - .3f, Is.EqualTo(far + .02f).Within(.00001f));
        }

        [TestCase(.25f, .95f, .25f, .95f)]
        [TestCase(.4f, .8f, .4f, .8f)]
        [TestCase(-1f, 2f, .01f, .99f)]
        public void MissingGeometryUsesConfiguredFallbacks(float fallbackRise, float fallbackEnd, float expectedRise, float expectedEnd)
        {
            _presenter.TraversalPhases(Vector3.zero, Vector3.forward * 2f, null, .3f, .02f,
                fallbackRise, fallbackEnd, out float rise, out float end);
            Assert.That(rise, Is.EqualTo(expectedRise));
            Assert.That(end, Is.EqualTo(expectedEnd));
        }

        [Test]
        public void UnusableGeometryKeepsFallbacksRatherThanInventingAThroughSpan()
        {
            var box = new Bounds(Vector3.right * 2f, Vector3.one);
            foreach (Vector3 endPoint in new[] { Vector3.zero, Vector3.up, Vector3.right * 2f,
                Vector3.left * 4f, Vector3.right * float.PositiveInfinity })
            {
                _presenter.TraversalPhases(Vector3.zero, endPoint, box, .3f, .02f, .4f, .8f, out float rise, out float end);
                Assert.That(rise, Is.EqualTo(.4f)); Assert.That(end, Is.EqualTo(.8f));
            }
            foreach (Bounds invalid in new[] { new Bounds(), new Bounds(Vector3.right * float.NaN, Vector3.one),
                new Bounds(Vector3.right * 2f, -Vector3.one) })
            {
                _presenter.TraversalPhases(Vector3.zero, Vector3.right * 4f, invalid, .3f, .02f, .4f, .8f, out float rise, out float end);
                Assert.That(rise, Is.EqualTo(.4f)); Assert.That(end, Is.EqualTo(.8f));
            }
        }

        [TestCase(0.25f, 0.16f)]
        [TestCase(0.2f, 0.12f)]
        public void SnapSizedDropRetainsSupportAcrossRoundedEdge(float snapDistance, float probeDistance)
        {
            const float skin = 0.02f;
            Vector3 velocity = Vector3.right * 4f;
            Vector3 edgeNormal = new Vector3(0.4f, Mathf.Sqrt(0.84f), 0f);
            Assert.That(_presenter.CanGround(velocity, edgeNormal, 50f), Is.False,
                "Regression: a capsule-down edge normal separates from horizontal travel.");
            Assert.That(_presenter.CanSnapToGround(velocity, edgeNormal, 50f), Is.True);
            Vector3 feet = new Vector3(11.1f, 1.2f, 0f);
            float edgeDistance = 0.3f - 0.28f * edgeNormal.y;
            feet = _presenter.GroundSnap(feet, edgeDistance, skin, snapDistance);
            float lowerDistance = feet.y - 1.05f + skin;
            Assert.That(lowerDistance, Is.LessThan(snapDistance));
            Assert.That(0.15f + skin, Is.GreaterThan(probeDistance),
                "Losing committed support would shorten the next search below the full drop.");
            Assert.That(_presenter.CanSnapToGround(velocity, Vector3.up, 50f), Is.True);
            feet = _presenter.GroundSnap(feet, lowerDistance, skin, snapDistance);
            Assert.That(feet.y, Is.EqualTo(1.05f).Within(0.00001f));
        }

        [Test]
        public void SnapRejectsRisingJumpButRetainsWalkableUphillMotion()
        {
            Vector3 normal = new Vector3(-1f, 2f, 0f).normalized;
            Vector3 uphill = _presenter.ContactVelocity(Vector3.right * 4f, normal, false);
            Assert.That(uphill.y, Is.GreaterThan(0f));
            Assert.That(_presenter.CanSnapToGround(uphill, normal, 50f), Is.True);
            Assert.That(_presenter.CanSnapToGround(uphill + Vector3.up * 5.5f, normal, 50f), Is.False);
            Assert.That(_presenter.CanSnapToGround(new Vector3(4f, 5.5f, 0f), Vector3.up, 50f), Is.False);
            Assert.That(_presenter.CanSnapToGround(Vector3.right * 4f, Vector3.right, 50f), Is.False);
        }

        [TestCase(26.56505f)]
        [TestCase(40f)]
        public void SlopeOverlapDepenetratesWithoutZeroingAlongSlopeVelocity(float degrees)
        {
            const float radius = 0.3f, skin = 0.02f;
            Vector3 normal = Quaternion.Euler(-degrees, 0f, 0f) * Vector3.up;
            _presenter.Capsule(Vector3.zero, 1.8f, radius, out Vector3 bottom, out _);
            float clearance = Vector3.Dot(bottom, normal);
            Assert.That(clearance, Is.LessThan(radius - skin), "The shrunken sweep really begins inside the plane.");
            Vector3 velocity = Vector3.ProjectOnPlane(Vector3.forward * 4f, normal);
            Vector3 syntheticNormal = -velocity.normalized;
            Assert.That(_presenter.ProjectAfterHit(velocity, syntheticNormal).magnitude, Is.LessThan(0.00001f), "Old freeze mechanism.");
            bool overlap = _presenter.IsInitialOverlap(0f, Vector3.zero);
            Assert.That(overlap, Is.True);
            Assert.That(_presenter.ContactVelocity(velocity, syntheticNormal, overlap), Is.EqualTo(velocity));
            Vector3 corrected = bottom + _presenter.PenetrationOffset(normal, radius - clearance, skin);
            Assert.That(Vector3.Dot(corrected, normal), Is.GreaterThan(radius - skin));
            Assert.That(_presenter.CanGround(velocity, normal, 50f), Is.True);
            Assert.That(_presenter.CanGround(velocity + Vector3.up * 5.5f, normal, 50f), Is.False);
            Assert.That(_presenter.IsInitialOverlap(0.1f, Vector3.zero), Is.False, "A real world-origin contact is not an overlap.");
        }

        [Test]
        public void SlideWallRedirectKeepsMostSpeedButNeverPushesIntoTheWall()
        {
            Vector3 incoming = new Vector3(8f, 0f, 6f);
            Vector3 redirected = _presenter.RedirectSlide(incoming, Vector3.left, 0.9f);
            Assert.That(redirected.x, Is.Zero.Within(0.00001f));
            Assert.That(redirected.magnitude, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(_presenter.RedirectSlide(Vector3.right * 10f, Vector3.left, 0.9f), Is.EqualTo(Vector3.zero));
            Assert.That(_presenter.RedirectSlide(Vector3.left, Vector3.left, 0.9f), Is.EqualTo(Vector3.left));
        }

        [TestCase(true, false, true, false, 1f, 0f, true)]
        [TestCase(false, false, true, false, 1f, 0f, false)]
        [TestCase(true, true, true, false, 1f, 0f, false)]
        [TestCase(true, false, false, false, 1f, 0f, false)]
        [TestCase(true, false, true, true, 1f, 0f, false)]
        [TestCase(true, false, true, false, 1.801f, 0f, false)]
        [TestCase(true, false, true, false, 0.499f, 0f, false)]
        [TestCase(true, false, true, false, 1f, 60f, false)]
        public void LedgeRequiresChestEdgeClearHeadroomReachAndWalkableTop(bool chest, bool above,
            bool top, bool blocked, float height, float slope, bool accepted)
        {
            Assert.That(_presenter.CanClimbLedge(chest, above, top, blocked, Vector3.zero,
                new Vector3(0f, height, 1f), Quaternion.Euler(slope, 0f, 0f) * Vector3.up,
                1.2f, 0.5f, 1.8f, 50f), Is.EqualTo(accepted));
            Assert.That(_presenter.CanClimbLedge(true, false, true, false, Vector3.zero,
                new Vector3(0f, 1f, 1.201f), Vector3.up, 1.2f, 0.5f, 1.8f, 50f), Is.False);
        }

        [TestCase(0)]
        [TestCase(12)]
        [TestCase(31)]
        public void GraceMaskRemovesOnlyHunterLayerAndRestoresTheOriginal(int layer)
        {
            int original = ~0;
            Assert.That(_presenter.MovementMask(original, layer, true), Is.EqualTo(original & ~(1 << layer)));
            Assert.That(_presenter.MovementMask(original, layer, false), Is.EqualTo(original));
            Assert.That(_presenter.MovementMask(0, layer, true), Is.Zero);
        }

        [Test]
        public void MissingLayerWarnsOncePerSessionAndNeverExcludesAnything()
        {
            var session = new PlayerDriverState();
            Assert.That(_presenter.ShouldWarnMissingHunterLayer(session, 12), Is.False);
            Assert.That(_presenter.ShouldWarnMissingHunterLayer(session, -1), Is.True);
            Assert.That(new PlayerMoverPresenter().ShouldWarnMissingHunterLayer(session, -1), Is.False);
            Assert.That(_presenter.MovementMask(~0, -1, true), Is.EqualTo(~0));
            Assert.That(_presenter.MovementMask(~0, 32, true), Is.EqualTo(~0));
            Assert.That(_presenter.ShouldWarnMissingHunterLayer(new PlayerDriverState(), -1), Is.True);
        }

        [Test]
        public void CapsuleRetainsFeetWhenHeightShrinks()
        {
            _presenter.Capsule(new Vector3(1f, 2f, 3f), 0.9f, 0.3f, out Vector3 bottom, out Vector3 top);
            Assert.That(bottom.y - 0.3f, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(top.y + 0.3f, Is.EqualTo(2.9f).Within(0.0001f));
        }
        [Test]
        public void CollisionProjectionRemovesOnlyInwardMotion()
        {
            Vector3 projected = _presenter.ProjectAfterHit(new Vector3(3f, -2f, 4f), Vector3.up);
            Assert.That(projected, Is.EqualTo(new Vector3(3f, 0f, 4f)));
            Assert.That(_presenter.ProjectAfterHit(Vector3.up, Vector3.up), Is.EqualTo(Vector3.up));
            Assert.That(_presenter.ProjectAfterHit(Vector3.right, Vector3.zero), Is.EqualTo(Vector3.zero));
        }
        [Test]
        public void TravelStopsAtSkinAndNeverMovesBackward()
        {
            Assert.That(_presenter.TravelBeforeHit(Vector3.forward * 10f, 2f, 0.02f).z, Is.EqualTo(1.98f).Within(0.0001f));
            Assert.That(_presenter.TravelBeforeHit(Vector3.forward, 0.01f, 0.02f), Is.EqualTo(Vector3.zero));
        }
        [Test]
        public void WallIsNotWalkableAndGroundSnapIsBounded()
        {
            Assert.That(_presenter.IsWalkable(Vector3.up, 50f), Is.True);
            Assert.That(_presenter.IsWalkable(Vector3.right, 50f), Is.False);
            Assert.That(_presenter.GroundSnap(Vector3.up, 100f, 0.02f, 0.2f).y, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(_presenter.IsValidStep(Vector3.zero, Vector3.up * 0.31f, 0.3f), Is.False);
        }
        [Test]
        public void InterpolationClampsAndUsesShortestYawAcrossWrap()
        {
            var state = new PlayerDriverState
            {
                PreviousPosition = Vector3.zero, Position = Vector3.forward * 10f,
                PreviousHeading = 350f, Heading = 10f, LastStepTime = 1f, LastStepDuration = 1f
            };
            Assert.That(_presenter.InterpolatePosition(state, 1.5f).z, Is.EqualTo(5f));
            Assert.That(_presenter.InterpolatePosition(state, 3f).z, Is.EqualTo(10f));
            Assert.That(Mathf.DeltaAngle(0f, _presenter.InterpolateHeading(state, 1.5f)), Is.EqualTo(0f).Within(0.0001f));
        }
        [Test]
        public void VaultRisesBeforeCrossingAndFinishesAtTarget()
        {
            Vector3 from = new Vector3(-1f, 0f, -2f);
            Vector3 target = new Vector3(1f, 1f, 2f);
            Vector3 early = _presenter.TraversalPosition(from, target, 0.2f, 1f, 0.08f, 0.4f, 0.9f);
            Assert.That(early.x - from.x, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(early.z - from.z, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(early.y, Is.EqualTo(0.81f).Within(0.0001f));
            Vector3 raised = _presenter.TraversalPosition(from, target, 0.7f, 1f, 0.08f, 0.4f, 0.9f);
            Assert.That(raised.y, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(raised.x, Is.GreaterThan(early.x).And.LessThan(target.x));
            Assert.That(raised.z, Is.GreaterThan(early.z).And.LessThan(target.z));
            Vector3 descending = _presenter.TraversalPosition(from, target, 0.95f, 1f, 0.08f, 0.4f, 0.9f);
            Assert.That(descending.y, Is.EqualTo(1.06f).Within(0.0001f));
            Assert.That(descending.x, Is.LessThan(target.x));
            Assert.That(descending.z, Is.LessThan(target.z));
            Assert.That(_presenter.TraversalPosition(from, target, 0f, 1f, 0.08f, 0.4f, 0.9f), Is.EqualTo(from));
            Assert.That(_presenter.TraversalPosition(from, target, 1f, 1f, 0.08f, 0.4f, 0.9f), Is.EqualTo(target));
        }

        [Test]
        public void VaultClearsObstacleEvenWhenLandingReturnsToFloorHeight()
        {
            Vector3 target = new Vector3(0f, 0f, 2f);
            Vector3 above = _presenter.TraversalPosition(Vector3.zero, target, 0.7f, 1f, 0.08f, 0.25f, 0.95f);
            Assert.That(above.y, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(_presenter.TraversalPosition(Vector3.zero, target, 1f, 1f, 0.08f, 0.25f, 0.95f), Is.EqualTo(target));
            Vector3 bounded = _presenter.LimitHorizontalDisplacement(new Vector3(3f, 2f, 4f), 14f, 0.1f);
            Assert.That(new Vector2(bounded.x, bounded.z).magnitude, Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(bounded.y, Is.EqualTo(2f));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void PairedLandingChoosesFarSideAndPreservesAuthoredHeight(float approachSide)
        {
            Vector3 west = new Vector3(-1.6f, 0.4f, 15f);
            Vector3 east = new Vector3(1.6f, 0.8f, 15f);
            Vector3 feet = new Vector3(approachSide * 1.4f, 20f, 15f);
            Vector3 approach = Vector3.left * approachSide;
            Vector3 expected = approachSide > 0f ? west : east;
            Assert.That(_presenter.TrySelectTraversalEndpoint(feet, approach, west, east, out Vector3 target), Is.True);
            Assert.That(target, Is.EqualTo(expected));
            Assert.That(_presenter.TrySelectTraversalEndpoint(feet, approach, east, west, out target), Is.True);
            Assert.That(target, Is.EqualTo(expected), "Endpoint order must not choose the approach-side landing.");
        }

        [Test]
        public void PairedLandingUsesTheRotatedHorizontalAxis()
        {
            Vector3 near = new Vector3(3f, 0f, 7f);
            Vector3 far = new Vector3(7f, 2f, 11f);
            Assert.That(_presenter.TrySelectTraversalEndpoint(new Vector3(4f, 50f, 8f),
                new Vector3(1f, 10f, 1f), near, far, out Vector3 target), Is.True);
            Assert.That(target, Is.EqualTo(far));
        }

        [TestCase(-1f)]
        [TestCase(1f)]
        public void PairedLandingOnMidpointPlaneUsesApproach(float direction)
        {
            Assert.That(_presenter.TrySelectTraversalEndpoint(Vector3.up * 10f, Vector3.right * direction,
                Vector3.left, Vector3.right, out Vector3 target), Is.True);
            Assert.That(target, Is.EqualTo(Vector3.right * direction));
        }

        [TestCase(-0.5f, -1f, 0f)]
        [TestCase(0.5f, 1f, 0f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(-0.5f, 0f, 0f)]
        public void PairedLandingRejectsAwayPerpendicularOrVerticalApproach(float feetX, float approachX, float approachZ)
        {
            Assert.That(_presenter.TrySelectTraversalEndpoint(new Vector3(feetX, 0f, 0f),
                new Vector3(approachX, 1f, approachZ), Vector3.left, Vector3.right, out Vector3 target), Is.False);
            Assert.That(target, Is.EqualTo(Vector3.zero));
        }

        [TestCase(-0.1f, false)]
        [TestCase(0f, false)]
        [TestCase(0.1f, true)]
        public void OffAxisApproachMustFaceTheOppositeSideEvenWhenItsEndpointIsAhead(float approachX, bool accepted)
        {
            Vector3 feet = new Vector3(-0.5f, 0f, 12.6f);
            Vector3 approach = new Vector3(approachX, 0f, 1f);
            Vector3 west = new Vector3(-1.6f, 0f, 15f);
            Vector3 east = new Vector3(1.6f, 0f, 15f);
            Assert.That(Vector3.Dot(east - feet, approach), Is.GreaterThan(0f), "The endpoint lies ahead in all three arrangements.");
            Assert.That(_presenter.TrySelectTraversalEndpoint(feet, approach, west, east, out Vector3 target), Is.EqualTo(accepted));
            Assert.That(target, Is.EqualTo(accepted ? east : Vector3.zero));
        }

        [Test]
        public void PairedLandingRejectsInvalidInputsWithoutInventingATarget()
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                foreach (Vector3 value in new[] { new Vector3(invalid, 0f, 0f), new Vector3(0f, invalid, 0f), new Vector3(0f, 0f, invalid) })
                {
                    Assert.That(_presenter.TrySelectTraversalEndpoint(value, Vector3.right, Vector3.left, Vector3.right, out Vector3 target), Is.False);
                    Assert.That(target, Is.EqualTo(Vector3.zero));
                    Assert.That(_presenter.TrySelectTraversalEndpoint(Vector3.zero, value, Vector3.left, Vector3.right, out target), Is.False);
                    Assert.That(target, Is.EqualTo(Vector3.zero));
                    Assert.That(_presenter.TrySelectTraversalEndpoint(Vector3.zero, Vector3.right, value, Vector3.right, out target), Is.False);
                    Assert.That(target, Is.EqualTo(Vector3.zero));
                    Assert.That(_presenter.TrySelectTraversalEndpoint(Vector3.zero, Vector3.right, Vector3.left, value, out target), Is.False);
                    Assert.That(target, Is.EqualTo(Vector3.zero));
                }
            }
        }

        [Test]
        public void PairedLandingRejectsHorizontalDegeneracyAndFiniteOverflow()
        {
            Assert.That(_presenter.TrySelectTraversalEndpoint(Vector3.left, Vector3.right,
                Vector3.zero, Vector3.up, out Vector3 target), Is.False);
            Assert.That(target, Is.EqualTo(Vector3.zero));
            Assert.That(_presenter.TrySelectTraversalEndpoint(Vector3.zero, Vector3.right,
                Vector3.left * float.MaxValue, Vector3.right * float.MaxValue, out target), Is.False);
            Assert.That(target, Is.EqualTo(Vector3.zero));
        }
    }
}
