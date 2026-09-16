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
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
//   - Verify paired traversal landings across approach directions and malformed geometry.
//   - Check horizontal progress throughout the lock while preserving the clearance envelope.
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
    public sealed class PlayerMoverPresenterTests
    {
        private readonly PlayerMoverPresenter _presenter = new PlayerMoverPresenter();
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
            Assert.That(early.y, Is.EqualTo(0.54f).Within(0.0001f));
            Vector3 raised = _presenter.TraversalPosition(from, target, 0.7f, 1f, 0.08f, 0.4f, 0.9f);
            Assert.That(raised.y, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(raised.x, Is.GreaterThan(early.x).And.LessThan(target.x));
            Assert.That(raised.z, Is.GreaterThan(early.z).And.LessThan(target.z));
            Vector3 descending = _presenter.TraversalPosition(from, target, 0.95f, 1f, 0.08f, 0.4f, 0.9f);
            Assert.That(descending.y, Is.EqualTo(1.04f).Within(0.0001f));
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
