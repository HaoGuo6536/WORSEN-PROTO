// ============================================================================
// PlayerLimbPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks gravity-aligned shoulder placement and injected walking swing without
//   a camera or scene. Also preserves legacy offset helpers and conservative
//   bounds clearance; real imported geometry is covered by PlayerArmsGeometryTests.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify mirrored offsets, projected bounds and whole-hand near-plane clearance.
//   - Verify yaw isolation, bounded opposite-phase swing and finite eased settling.
// DEPENDENCIES:
//   - PlayerLimbPresenter, NUnit and UnityEngine value types only.
// USAGE NOTES:
//   Pure Edit Mode tests. PlayerDriverTests covers the actual render callback;
//   neither fixture replaces owner review of the placeholder art in play.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Player;
using Worsen.Core;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerLimbPresenterTests
    {
        [TestCase(-85f, 0f)] [TestCase(0f, 180f)] [TestCase(85f, 180f)]
        public void OffCentreArmBoundsClearThePlaneAtEveryCorner(float pitch, float yaw)
        {
            var presenter = new PlayerLimbPresenter();
            // Roll about forward does not change this projection axis. Use managed
            // value math so these pure assertions also execute outside Unity.
            double pitchRadians = pitch * Math.PI / 180, yawRadians = yaw * Math.PI / 180;
            Vector3 forward = new Vector3((float)(Math.Cos(pitchRadians) * Math.Sin(yawRadians)),
                (float)-Math.Sin(pitchRadians), (float)(Math.Cos(pitchRadians) * Math.Cos(yawRadians)));
            var bounds = new Bounds(new Vector3(0.12f, -0.3f, -0.2f), new Vector3(0.2f, 0.6f, 0.7f));
            float rear = presenter.RearExtent(bounds.center, bounds.extents, forward);
            Vector3 offset = presenter.HandOffset(new Vector3(0.32f, -0.25f, 0f), true, 0.3f, rear);
            foreach (float x in new[] { -1f, 1f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Assert.That(offset.z + Vector3.Dot(corner, forward), Is.GreaterThan(0.3f));
            }
        }

        [Test]
        public void RearExtentIncludesTheArmsOffsetFromItsHandRoot()
        {
            Assert.That(new PlayerLimbPresenter().RearExtent(new Vector3(0f, -0.2f, -0.4f),
                new Vector3(0.1f, 0.2f, 0.3f), Vector3.forward), Is.EqualTo(0.7f).Within(0.00001f));
        }

        [TestCase(-85f, 0f)] [TestCase(45f, 70f)] [TestCase(85f, 180f)]
        public void ShoulderYawIgnoresHeadPitchAndRoll(float pitch, float yawDegrees)
        {
            var presenter = new PlayerLimbPresenter();
            double p = pitch * Math.PI / 180, y = yawDegrees * Math.PI / 180;
            Vector3 forward = new Vector3((float)(Math.Sin(y) * Math.Cos(p)),
                (float)-Math.Sin(p), (float)(Math.Cos(y) * Math.Cos(p)));
            Quaternion yaw = presenter.YawRotation(forward, Vector3.forward);
            Assert.That((yaw * Vector3.up - Vector3.up).sqrMagnitude, Is.LessThan(0.000001f));
            Assert.That(yaw.x, Is.Zero); Assert.That(yaw.z, Is.Zero);
            Vector3 left = presenter.ShoulderOffset(PlayerMoverDriverConfig.DefaultShoulderOffset, true, yaw);
            Vector3 right = presenter.ShoulderOffset(PlayerMoverDriverConfig.DefaultShoulderOffset, false, yaw);
            Assert.That(left.y, Is.EqualTo(-0.22f).Within(0.00001f));
            Assert.That(right.y, Is.EqualTo(left.y));
            Assert.That(Vector3.Distance(left, right), Is.EqualTo(0.48f).Within(0.00001f));
        }

        [Test]
        public void VerticalViewFallsBackToBodyHeading()
        {
            Quaternion yaw = new PlayerLimbPresenter().YawRotation(Vector3.down, Vector3.right);
            Assert.That((yaw * Vector3.forward - Vector3.right).sqrMagnitude, Is.LessThan(0.000001f));
        }

        [TestCase(MovementState.Ground, false, 0f)]
        [TestCase(MovementState.Ground, true, 4f)]
        [TestCase(MovementState.Slide, false, 12f)]
        [TestCase(MovementState.Vault, false, 4f)]
        [TestCase(MovementState.Air, false, 4f)]
        [TestCase(MovementState.Stumble, false, 4f)]
        public void NonWalkingStatesHaveZeroSwingAndSettleWithoutSnapping(MovementState movement, bool crouched, float speed)
        {
            var presenter = new PlayerLimbPresenter();
            float target = presenter.SwingTarget(movement, crouched, speed, 6f, 4f);
            Assert.That(target, Is.Zero);
            presenter.StepSwing(0f, 0f, target, 6f, 1.3f, 0.2f, 0.02f, out float phase, out float envelope);
            Assert.That(presenter.SwingAngle(phase, envelope, true), Is.Zero);
            // Entering a blocked state removes walking drive immediately, with a
            // finite 0.2s settle of the existing presentation rather than a pose pop.
            presenter.StepSwing(1f, 6f, target, 6f, 1.3f, 0.2f, 0.02f, out phase, out envelope);
            Assert.That(envelope, Is.EqualTo(5.4f).Within(0.00001f));
            presenter.StepSwing(phase, envelope, target, 6f, 1.3f, 0.2f, 0.2f, out phase, out envelope);
            Assert.That(envelope, Is.Zero); Assert.That(phase, Is.Zero);
            Assert.That(presenter.SwingAngle(phase, envelope, false), Is.Zero);
        }

        [Test]
        public void WalkingAmplitudeIsSpeedProportionalBoundedAndOppositePhase()
        {
            var presenter = new PlayerLimbPresenter();
            Assert.That(presenter.SwingTarget(MovementState.Ground, false, 2f, 6f, 4f), Is.EqualTo(3f));
            Assert.That(presenter.SwingTarget(MovementState.Ground, false, 20f, 90f, 4f), Is.EqualTo(8f));
            Assert.That(presenter.SwingTarget(MovementState.Ground, false, -2f, 6f, 4f), Is.Zero);
            float phase = 0f, envelope = 0f;
            for (int i = 0; i < 300; i++)
            {
                presenter.StepSwing(phase, envelope, 100f, 100f, 1.3f, 0.2f, 0.01f, out phase, out envelope);
                float left = presenter.SwingAngle(phase, envelope, true), right = presenter.SwingAngle(phase, envelope, false);
                Assert.That(Math.Abs(left), Is.LessThanOrEqualTo(8f));
                Assert.That(left, Is.EqualTo(-right));
            }
        }

        [Test]
        public void SwingEasesOnStartAndSpeedChangeAndDoesNotAdvanceWithoutTime()
        {
            var presenter = new PlayerLimbPresenter();
            presenter.StepSwing(0f, 0f, 6f, 6f, 1.3f, 0.2f, 0.02f, out float phase, out float envelope);
            Assert.That(envelope, Is.EqualTo(0.6f).Within(0.00001f));
            presenter.StepSwing(phase, envelope, 6f, 6f, 1.3f, 0.2f, 0f, out float pausedPhase, out float pausedEnvelope);
            Assert.That(pausedPhase, Is.EqualTo(phase)); Assert.That(pausedEnvelope, Is.EqualTo(envelope));
            presenter.StepSwing(phase, 6f, 3f, 6f, 1.3f, 0.2f, 0.02f, out _, out envelope);
            Assert.That(envelope, Is.EqualTo(5.4f).Within(0.00001f));
        }

        [TestCase(true)] [TestCase(false)]
        public void OrdinaryHandsKeepTheAuthoredLowerViewOffset(bool left)
        {
            Vector3 offset = new PlayerLimbPresenter().HandOffset(new Vector3(0.32f, -0.25f, 0.5f), left, 0.05f, 0.24f);
            Assert.That(offset, Is.EqualTo(new Vector3(left ? -0.32f : 0.32f, -0.25f, 0.5f)));
        }

        [TestCase(0.05f, 0.24f)] [TestCase(0.3f, 0.24f)] [TestCase(1f, 0.5f)]
        public void UnsafeDepthMovesTheWholeHandBeyondTheNearPlane(float near, float radius)
        {
            Vector3 offset = new PlayerLimbPresenter().HandOffset(new Vector3(0.32f, -0.25f, 0f), false, near, radius);
            Assert.That(offset.z - radius, Is.GreaterThan(near));
            Assert.That(offset.x, Is.EqualTo(0.32f));
            Assert.That(offset.y, Is.EqualTo(-0.25f));
        }
    }
}
