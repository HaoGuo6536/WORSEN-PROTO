// ============================================================================
// HunterSteeringPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks physical Hunter motion using supplied paths and explicit tick durations.
//   The fixtures measure acceleration, turn limits and committed travel without
//   relying on navigation or physics to supply the answer under test.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify inertial path steering, arrival, recovery and pooled-state reset.
//   - Verify direction commitment and the exact maximum travel of each lunge.
// DEPENDENCIES:
//   - Hunter pure steering types, UnityEngine value data and NUnit.
// USAGE NOTES:
//   No engine calls or scene objects; physical collision/navigation integration is
//   verified separately in the connected Unity Editor by the coordinating owner.
//   Gap cases distinguish freshly validated progress from an unverified bank sample.
//   Rejected-gap recovery distinguishes attached entry-bank detours from far-bank snaps.
//   Oblique terminal cases replay the recorded East approach with physical feedback.
//   Nearby sideways goals reject the discrete-turn spiral found during review.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;

namespace Worsen.Tests.Hunter
{
    public sealed class HunterSteeringPresenterTests
    {
        private HunterSteeringPresenter _presenter;
        private HunterSteeringDriverState _state;

        [SetUp]
        public void SetUp()
        {
            _presenter = new HunterSteeringPresenter();
            _state = new HunterSteeringDriverState();
            _presenter.Reset(_state, Vector3.zero, Vector3.forward);
            _presenter.SetPath(_state, new[] { Vector3.forward * 100f });
        }

        private Vector3 Tick(float dt = 0.1f, bool recovery = false, bool active = false, Vector3 direction = default)
            => _presenter.Tick(_state, dt, 8f * 1.12f, 20f, 240f, recovery, active, direction, 18f, 4f);

        [Test]
        public void AcceleratesAtTwentyAndCapsChaseSpeedAtPlayerSprintTimesMultiplier()
        {
            Tick();
            Assert.That(_state.Velocity.magnitude, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(_state.Position.z, Is.EqualTo(0.2f).Within(0.0001f));
            for (int i = 0; i < 10; i++) Tick();
            Assert.That(_state.Velocity.magnitude, Is.EqualTo(8.96f).Within(0.0001f));
        }

        [Test]
        public void HardCornerLimitsHeadingAndVelocityChangeIndependently()
        {
            _state.Velocity = Vector3.forward * 8.96f;
            _presenter.SetPath(_state, new[] { Vector3.right * 100f });
            Vector3 previousVelocity = _state.Velocity;
            Tick();
            Assert.That(Vector3.Angle(Vector3.forward, _state.Forward), Is.EqualTo(24f).Within(0.001f));
            Assert.That((_state.Velocity - previousVelocity).magnitude, Is.LessThanOrEqualTo(2.0001f));
            Assert.That(_state.Velocity.z, Is.GreaterThan(0f));
            Assert.That(_state.Velocity.x, Is.GreaterThan(0f));
        }

        [Test]
        public void LargeTurnBudgetStopsAtDesiredHeadingWithoutOvershoot()
        {
            _presenter.SetPath(_state, new[] { Vector3.right * 100f });
            Tick(1f);
            Assert.That(Vector3.Angle(Vector3.right, _state.Forward), Is.LessThan(0.001f));
        }

        [Test]
        public void PathCornerToleranceComesFromCallerAndAdvancesOnlyNearbyCorners()
        {
            _presenter.SetPath(_state, new[] { Vector3.forward * 0.2f, Vector3.right * 100f });
            _presenter.Tick(_state, 0.1f, 8.96f, 20f, 240f, false, false, Vector3.zero, 18f, 4f, 0.1f);
            Assert.That(_state.CornerIndex, Is.EqualTo(0));
            _presenter.Reset(_state, Vector3.zero, Vector3.forward);
            _presenter.SetPath(_state, new[] { Vector3.forward * 0.2f, Vector3.right * 100f });
            _presenter.Tick(_state, 0.1f, 8.96f, 20f, 240f, false, false, Vector3.zero, 18f, 4f, 0.25f);
            Assert.That(_state.CornerIndex, Is.EqualTo(1));
            Assert.That(_state.Forward.x, Is.GreaterThan(0f));
        }

        [Test]
        public void FinalPathPointIsReachedWithoutOvershooting()
        {
            _presenter.SetPath(_state, new[] { Vector3.zero, Vector3.forward * 4f });
            for (int i = 0; i < 120; i++)
            {
                Tick(1f / 60f);
                Assert.That(_state.Position.z, Is.LessThanOrEqualTo(4.0001f));
            }
            Assert.That(_state.Position.z, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(_state.Velocity.magnitude, Is.Zero);
            Assert.That(_state.CornerIndex, Is.EqualTo(2));
        }

        [Test]
        public void MissingPathBrakesExistingVelocityInsteadOfInventingRoute()
        {
            _state.Velocity = Vector3.forward * 4f;
            _presenter.SetPath(_state, null);
            Tick();
            Assert.That(_state.Velocity.z, Is.EqualTo(2f).Within(0.0001f));
            Tick();
            Assert.That(_state.Velocity.magnitude, Is.Zero);
        }

        [Test]
        public void RecoveryAndWindupCommandStopImmediately()
        {
            _state.Velocity = Vector3.forward * 8.96f;
            Vector3 position = _state.Position;
            Assert.That(Tick(recovery: true), Is.EqualTo(Vector3.zero));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(_state.Position, Is.EqualTo(position));
            Assert.That(Tick(recovery: true, active: true, direction: Vector3.right), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void LungeCapturesDirectionOnceAndCapsTravelAtExactlyFourMetres()
        {
            Assert.That(Tick(active: true, direction: Vector3.forward).z, Is.EqualTo(1.8f).Within(0.0001f));
            Tick(active: true, direction: Vector3.right);
            Vector3 finalStep = Tick(active: true, direction: Vector3.back);
            Assert.That(finalStep.z, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(_state.Position.z, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(_state.Position.x, Is.Zero);
            Assert.That(_state.LungeDistanceTravelled, Is.EqualTo(4f));
            Assert.That(Tick(active: true, direction: Vector3.right), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void LungeLargeTickStillConsumesOnlyAvailableDistance()
        {
            Tick(1f, active: true, direction: new Vector3(3f, 8f, 4f));
            Assert.That(_state.Position.magnitude, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(_state.Position.y, Is.Zero);
            Assert.That(_state.Position.x, Is.EqualTo(2.4f).Within(0.0001f));
            Assert.That(_state.Position.z, Is.EqualTo(3.2f).Within(0.0001f));
        }

        [Test]
        public void NewLungeAfterRecoveryGetsFreshBudgetAndDirection()
        {
            Tick(1f, active: true, direction: Vector3.forward);
            Tick(recovery: true);
            Tick(1f, active: true, direction: Vector3.right);
            Assert.That(_state.Position, Is.EqualTo(new Vector3(4f, 0f, 4f)));
            Assert.That(_state.LungeDistanceTravelled, Is.EqualTo(4f));
        }

        [Test]
        public void CollisionReconciliationCannotRefundLungeTravel()
        {
            Tick(active: true, direction: Vector3.forward);
            _state.Position = Vector3.zero;
            Tick(1f, active: true, direction: Vector3.forward);
            Assert.That(_state.Position.z, Is.EqualTo(2.2f).Within(0.0001f));
            Assert.That(_state.LungeDistanceTravelled, Is.EqualTo(4f));
        }

        [Test]
        public void ZeroLungeDirectionFallsBackToCurrentHeading()
        {
            _state.Forward = Vector3.right;
            Tick(active: true);
            Assert.That(_state.Position.x, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(_state.Position.z, Is.Zero);
        }

        [Test]
        public void InvalidTimeDoesNotMoveOrConsumeLungeBudget()
        {
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.That(Tick(dt, active: true, direction: Vector3.forward), Is.EqualTo(Vector3.zero));
            Assert.That(_state.Position, Is.EqualTo(Vector3.zero));
            Assert.That(_state.LungeDistanceTravelled, Is.Zero);
            Assert.That(_state.LungeWasActive, Is.False);
        }

        [Test]
        public void ResetClearsPooledLungePathAndVelocityState()
        {
            Tick(active: true, direction: Vector3.forward);
            _presenter.Reset(_state, Vector3.one, Vector3.right);
            Assert.That(_state.Position, Is.EqualTo(Vector3.one));
            Assert.That(_state.Forward, Is.EqualTo(Vector3.right));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(_state.Corners, Is.Empty);
            Assert.That(_state.CornerIndex, Is.Zero);
            Assert.That(_state.LungeWasActive, Is.False);
            Assert.That(_state.LungeDirection, Is.EqualTo(Vector3.zero));
            Assert.That(_state.LungeDistanceTravelled, Is.Zero);
        }

        [Test]
        public void PathIsSnapshotOfSuppliedCornerData()
        {
            Vector3[] path = { Vector3.forward * 10f };
            _presenter.SetPath(_state, path);
            path[0] = Vector3.right * 10f;
            Assert.That(_state.Corners[0], Is.EqualTo(Vector3.forward * 10f));
        }

        private Vector3[] BeginGap()
        {
            Vector3[] corners = { new Vector3(-21f, 0f, -4.8f), new Vector3(-21f, 0f, -5f),
                new Vector3(-21f, 0f, -7f), new Vector3(-21f, 0f, -8.5f) };
            _presenter.Reset(_state, new Vector3(-21f, 0f, -5.3f), Vector3.back);
            _presenter.SetPath(_state, corners);
            _state.CornerIndex = 2;
            _state.Velocity = Vector3.back * 3f;
            return corners;
        }

        [Test]
        public void BankSnapInsideActiveSegmentRequestsStableAnchorValidation()
        {
            BeginGap();
            Assert.That(_presenter.HasActiveSegmentProgress(_state, _state.Position, 0.25f), Is.True);
            _state.Position = new Vector3(-21f, 0f, -6.4f);
            Assert.That(_presenter.HasActiveSegmentProgress(_state, _state.Position, 0.25f), Is.True);
        }

        [Test]
        public void InitialOffMeshIngressDoesNotSkipItsFirstCorner()
        {
            BeginGap();
            _state.CornerIndex = 0;
            Assert.That(_presenter.HasActiveSegmentProgress(_state, _state.Position, 0.25f), Is.False);
            Vector3 before = _state.Position;
            Tick();
            Assert.That(_state.CornerIndex, Is.Zero);
            Assert.That(_state.Position, Is.Not.EqualTo(before));
        }

        [Test]
        public void MeshHeightOffsetDoesNotHideActiveGapProgress()
        {
            BeginGap();
            Assert.That(_presenter.IsOnNavigationSample(_state.Position, _state.Position + Vector3.up * 0.1f), Is.True);
            Assert.That(_presenter.HasActiveSegmentProgress(_state, _state.Position, 0.25f), Is.True);
        }

        [TestCase(-4.9f, 0f)]
        [TestCase(-7.1f, 0f)]
        [TestCase(-6f, 0.3f)]
        public void PositionsOutsideActiveSegmentDoNotInheritGapProgress(float z, float lateral)
        {
            BeginGap();
            Vector3 position = new Vector3(-21f + lateral, 0f, z);
            Assert.That(_presenter.HasActiveSegmentProgress(_state, position, 0.25f), Is.False);
        }

        [Test]
        public void ValidatedGapRefreshKeepsExitAndUsesFreshTailWithoutChangingMotion()
        {
            Vector3[] previous = BeginGap();
            Vector3 position = _state.Position, velocity = _state.Velocity, forward = _state.Forward;
            Vector3[] fresh = { previous[2], new Vector3(-19f, 0f, -10f) };
            Assert.That(_presenter.TrySetGapPath(_state, previous[1], previous[2], fresh, null, false, out bool reverse), Is.True);
            Assert.That(reverse, Is.False);
            Assert.That(_state.CornerIndex, Is.EqualTo(1));
            Assert.That(_state.Corners[3], Is.EqualTo(fresh[1]));
            Assert.That(_state.Position, Is.EqualTo(position));
            Assert.That(_state.Velocity, Is.EqualTo(velocity));
            Assert.That(_state.Forward, Is.EqualTo(forward));
            Tick(1f / 60f);
            Assert.That(_state.Position.z, Is.LessThan(position.z));
        }

        [Test]
        public void ShiftedWidePortalFromBankSampleDoesNotCountAsValidatedSegment()
        {
            Vector3[] previous = BeginGap();
            Vector3[] bankRoute = { new Vector3(-21f, 0f, -5.2f), new Vector3(-20f, 0f, -5f),
                new Vector3(-20f, 0f, -7f), previous[3] };
            Assert.That(_presenter.IsDirectSegmentPath(bankRoute, previous[1], previous[2], 0.35f), Is.False);
        }

        [Test]
        public void ReverseTargetRequiresFreshReverseSegmentAndPreservesTurnInertia()
        {
            Vector3[] previous = BeginGap();
            Vector3[] forwardTail = { previous[2], previous[1], previous[0] };
            Vector3[] reverseTail = { previous[1], previous[0] };
            Assert.That(_presenter.TrySetGapPath(_state, previous[1], previous[2], forwardTail, reverseTail,
                false, out bool reverse), Is.True);
            Assert.That(reverse, Is.True);
            Assert.That(_state.Corners[_state.CornerIndex], Is.EqualTo(previous[1]));
            Tick();
            Assert.That(Vector3.Angle(Vector3.back, _state.Forward), Is.EqualTo(24f).Within(0.001f));
            Assert.That(_state.Velocity.z, Is.LessThan(0f));
        }

        [Test]
        public void InvalidatedGapCannotBeReplacedByFarBankTailAndCanResumeAfterRevalidation()
        {
            Vector3[] previous = BeginGap();
            _state.Position = new Vector3(-21f, 0f, -6.4f);
            Assert.That(_presenter.TrySetGapPath(_state, previous[1], previous[2], null, null, false, out _), Is.False);
            Assert.That(_state.Corners, Is.Empty);
            for (int refresh = 0; refresh < 3; refresh++)
            {
                Assert.That(_presenter.TrySetGapPath(_state, previous[1], previous[2], null, null, false, out _), Is.False);
                Assert.That(_state.Corners, Is.Empty);
                Assert.That(_presenter.CanReleaseGap(_state, previous[1], previous[2], _state.Position,
                    new Vector3(-21f, 0f, -6.8f)), Is.False);
            }
            Assert.That(_presenter.TrySetGapPath(_state, previous[1], previous[2],
                new[] { previous[2], previous[3] }, null, false, out _), Is.True);
            Assert.That(_state.CornerIndex, Is.EqualTo(1));
            Assert.That(_state.Position.z, Is.EqualTo(-6.4f));
        }

        [Test]
        public void EastPortalProgressIsCapturedBeforeLeavingNearBankMesh()
        {
            Vector3 entry = new Vector3(26.2041f, 0f, -10.5f), exit = new Vector3(28f, 0f, -12.5f);
            Vector3 position = new Vector3(26.2363f, 0f, -10.5165f);
            _presenter.SetPath(_state, new[] { new Vector3(26.2f, 0.05f, -10f), entry, exit, new Vector3(27f, 0f, -13.5f) });
            _state.CornerIndex = 2;
            Assert.That(_presenter.HasActiveSegmentProgress(_state, position, 0.25f), Is.True);
            Assert.That(_presenter.TrySetGapPath(_state, entry, exit,
                new[] { exit, new Vector3(27f, 0f, -13.5f) }, null, false, out _), Is.True);
            Assert.That(_presenter.CanReleaseGap(_state, entry, exit, position, position + Vector3.up * 0.05f), Is.False);
        }

        [Test]
        public void FixedEastSegmentAllowsOnlySmallNativePortalRoundingAndHeightOffset()
        {
            Vector3 entry = new Vector3(26.2041f, 0.05f, -10.5f), exit = new Vector3(28f, 0.05f, -12.5f);
            Vector3[] forward = { entry, new Vector3(26.2041f, 0f, -10.5f), new Vector3(27.9989f, 0f, -12.5f), exit };
            Vector3[] reverse = { exit, new Vector3(28f, 0f, -12.5f), new Vector3(26.2052f, 0f, -10.5f), entry };
            Assert.That(_presenter.IsDirectSegmentPath(forward, entry, exit, 0.35f), Is.True);
            Assert.That(_presenter.IsDirectSegmentPath(reverse, exit, entry, 0.35f), Is.True);
            forward[2] += Vector3.right;
            Assert.That(_presenter.IsDirectSegmentPath(forward, entry, exit, 0.35f), Is.False);
        }

        [Test]
        public void CompleteDetourOrReversingLocalPathDoesNotValidateRemovedGap()
        {
            Vector3[] previous = BeginGap();
            Vector3 entry = previous[1], exit = previous[2];
            Assert.That(_presenter.IsDirectSegmentPath(new[] { entry, entry + Vector3.right, exit + Vector3.right, exit },
                entry, exit, 0.35f), Is.False);
            Assert.That(_presenter.IsDirectSegmentPath(new[] { entry, Vector3.Lerp(entry, exit, 0.8f),
                Vector3.Lerp(entry, exit, 0.2f), exit }, entry, exit, 0.35f), Is.False);
            Assert.That(_presenter.IsDirectSegmentPath(new[] { entry, Vector3.Lerp(entry, exit, 0.5f) + Vector3.up, exit },
                entry, exit, 0.35f), Is.False);
            Assert.That(_presenter.IsNavigationAnchor(entry, entry + Vector3.right, 0.35f), Is.False);
            Assert.That(_presenter.IsNavigationAnchor(entry, entry + Vector3.up, 0.35f), Is.False);
        }

        [Test]
        public void CompletedGapReleasesOnlyAfterPhysicalMeshAttachment()
        {
            Vector3[] previous = BeginGap();
            _presenter.TrySetGapPath(_state, previous[1], previous[2], new[] { previous[2], previous[3] }, null, false, out _);
            _state.CornerIndex = 2;
            Vector3 position = previous[2] + Vector3.forward * 0.1f;
            Assert.That(_presenter.CanReleaseGap(_state, previous[1], previous[2], position, position + Vector3.forward * 0.1f), Is.False);
            Assert.That(_presenter.CanReleaseGap(_state, previous[1], previous[2], position, position + Vector3.up * 0.05f), Is.True);
        }

        [Test]
        public void PhysicalArrivalFeedbackDoesNotRestoreAverageTravelVelocityAfterTerminalClamp()
        {
            _presenter.Reset(_state, Vector3.zero, Vector3.forward);
            _presenter.SetPath(_state, new[] { Vector3.zero, Vector3.forward * 0.01f });
            _state.Velocity = Vector3.forward;
            const float dt = 1f / 60f;
            for (int i = 0; i < 10; i++)
            {
                Vector3 start = _state.Position;
                Vector3 movement = _presenter.Tick(_state, dt, 3f, 20f, 240f, false, false, Vector3.zero, 18f, 4f);
                _presenter.ReconcileMovement(_state, start, start + movement, movement, dt);
                Assert.That(_state.Position.z, Is.EqualTo(0.01f).Within(0.000001f));
                Assert.That(_state.Velocity.z, Is.Zero);
            }
        }

        [Test]
        public void PhysicalFeedbackRetainsClippedMotionAndActualVerticalVelocity()
        {
            _state.Velocity = Vector3.zero;
            _presenter.ReconcileMovement(_state, Vector3.zero, new Vector3(0f, -0.002f, 0.005f),
                Vector3.forward * 0.01f, 0.01f);
            Assert.That(_state.Velocity.z, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(_state.Velocity.y, Is.EqualTo(-0.2f).Within(0.00001f));
            _state.Velocity = Vector3.zero;
            _presenter.ReconcileMovement(_state, Vector3.zero, new Vector3(0f, -0.002f, 0.01f),
                Vector3.forward * 0.01f, 0.01f);
            Assert.That(_state.Velocity.z, Is.Zero);
            Assert.That(_state.Velocity.y, Is.EqualTo(-0.2f).Within(0.00001f));
        }

        [TestCase(0f, 4.02f)]
        [TestCase(0f, 4.03f)]
        [TestCase(5120f, 4.04f)]
        public void RepeatedRepathsAndPhysicalFeedbackKeepArrivedHunterAtGoal(float origin, float distance)
        {
            Vector3 startPosition = new Vector3(origin, 0f, origin);
            Vector3 goal = startPosition + Vector3.forward * distance;
            _presenter.Reset(_state, startPosition, Vector3.forward);
            const float dt = 1f / 60f;
            for (int tick = 0; tick < 180; tick++)
            {
                if (tick % 10 == 0) _presenter.SetPath(_state, new[] { _state.Position, goal });
                Vector3 start = _state.Position;
                Vector3 movement = _presenter.Tick(_state, dt, 3f, 20f, 240f, false, false, Vector3.zero, 18f, 4f);
                _presenter.ReconcileMovement(_state, start, start + movement, movement, dt);
                Assert.That(_state.Position.z, Is.LessThanOrEqualTo(goal.z));
            }
            Assert.That(_state.Position, Is.EqualTo(goal));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RejectedGapCanRecoverOnConnectedEntryBankButNotFromOffMeshOrRemappedAnchors()
        {
            Vector3 entry = new Vector3(26.2041f, 0f, -10.5f);
            Vector3 position = new Vector3(26.2363f, 0f, -10.5165f);
            Vector3 sample = position + Vector3.up * 0.05f;
            Vector3 sampledEntry = entry + Vector3.up * 0.05f;
            Assert.That(_presenter.CanLeaveRejectedGap(position, sample, entry, sampledEntry, true, 0.12f), Is.True);
            Assert.That(_presenter.CanLeaveRejectedGap(position, sample, entry, sampledEntry, false, 0.12f), Is.False);
            Assert.That(_presenter.CanLeaveRejectedGap(position, sample + Vector3.back * 0.4f,
                entry, sampledEntry, true, 0.12f), Is.False);
            Assert.That(_presenter.CanLeaveRejectedGap(position, sample + Vector3.up,
                entry, sampledEntry, true, 0.12f), Is.False);
            Assert.That(_presenter.CanLeaveRejectedGap(position, sample, entry,
                sampledEntry + Vector3.right, true, 0.12f), Is.False);
        }

        [TestCase(0f)]
        [TestCase(5120f)]
        public void ObliqueEastApproachArrivesWithinRemainingRouteTicksWithoutOrbit(float origin)
        {
            Vector3 offset = new Vector3(origin, 0f, origin);
            Vector3 position = offset + new Vector3(28.0308f, 0f, -12.6395f);
            Vector3 forward = new Vector3(0.4439f, 0f, -0.8961f).normalized;
            Vector3 goal = offset + new Vector3(27f, 0f, -13.5f);
            _presenter.Reset(_state, position, forward);
            _state.Velocity = forward * 3f;
            const float dt = 1f / 60f;
            for (int tick = 0; tick < 90; tick++)
            {
                if (tick % 10 == 0) _presenter.SetPath(_state, new[] { _state.Position, goal });
                Vector3 start = _state.Position;
                Vector3 movement = _presenter.Tick(_state, dt, 3f, 20f, 240f, false, false, Vector3.zero, 18f, 4f);
                _presenter.ReconcileMovement(_state, start, start + movement, movement, dt);
            }
            Assert.That(_state.Position, Is.EqualTo(goal));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TerminalHeadingBrakeStillUsesAccelerationAndTurnLimits()
        {
            _presenter.Reset(_state, Vector3.zero, Vector3.right);
            _presenter.SetPath(_state, new[] { Vector3.forward * 4f });
            _state.Velocity = Vector3.right * 3f;
            _presenter.Tick(_state, 0.1f, 3f, 20f, 0f, false, false, Vector3.zero, 18f, 4f);
            Assert.That(_state.Forward, Is.EqualTo(Vector3.right));
            Assert.That(_state.Velocity.x, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(_state.Position.x, Is.EqualTo(0.1f).Within(0.00001f));
        }

        [TestCase(0.01f)]
        [TestCase(0.1f)]
        [TestCase(1f)]
        public void NearbySidewaysGoalStopsInsteadOfSpirallingDuringRepeatedRepaths(float distance)
        {
            Vector3 goal = Vector3.forward * distance;
            _presenter.Reset(_state, Vector3.zero, Vector3.right);
            const float dt = 1f / 60f;
            for (int tick = 0; tick < 180; tick++)
            {
                if (tick % 10 == 0) _presenter.SetPath(_state, new[] { _state.Position, goal });
                Vector3 start = _state.Position;
                Vector3 movement = _presenter.Tick(_state, dt, 3f, 20f, 240f, false, false, Vector3.zero, 18f, 4f);
                _presenter.ReconcileMovement(_state, start, start + movement, movement, dt);
            }
            Assert.That(Vector3.Distance(_state.Position, goal), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(_state.CornerIndex, Is.EqualTo(2));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
        }
    }
}
