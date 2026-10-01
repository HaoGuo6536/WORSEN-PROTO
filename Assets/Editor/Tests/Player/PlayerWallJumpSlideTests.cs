// ============================================================================
// PlayerWallJumpSlideTests.cs
// ============================================================================
// PURPOSE:
//   Locks the owner's 2026-09-30 Windows playtest decisions into executable pure
//   movement tests. Runs the actual Controller tick without constructing native
//   ScriptableObjects, scenes or physics, including clearance and replay facts.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Verify press-only slides, full-height exit and safe blocked-slide escape.
//   - Verify buffered airborne/grounded wall jumps, momentum and typed facts.
//   - Verify per-wall admission, cooldown and vault/mantle priority.
//   - Verify managed heading/slide rotation and committed posture publication.
// DEPENDENCIES:
//   - Player Controller/state/profile, Core input/probe values and NUnit.
// USAGE NOTES:
//   Explicit provisional fixture tuning mirrors PlayerProfile, but does not replace
//   Unity asset/default tests. Uninitialized SOs are managed data only and are never
//   passed to engine APIs or destroyed. PlayerPerkControllerTests covers Second Bounce.
// ============================================================================
using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerWallJumpSlideTests
    {
        private const float Dt = 1f / 60f;
        private PlayerBehaviorState state;
        private PlayerProfile profile;
        private PlayerController controller;
        private long tick;
        private static readonly MovementProbe Ground = new MovementProbe(true, Vector3.up);

        [SetUp]
        public void SetUp()
        {
            profile = (PlayerProfile)FormatterServices.GetUninitializedObject(typeof(PlayerProfile));
            Tune("_maximumHealth", 100f); Tune("_sprintSpeed", 8f); Tune("_walkSpeed", 4f);
            Tune("_maxDesignSpeed", 14f); Tune("_maximumExternalMotionSpeed", 14f);
            Tune("_groundAcceleration", 60f); Tune("_groundFriction", 70f);
            Tune("_jumpSpeed", 5.5f); Tune("_jumpBuffer", .1f); Tune("_coyoteTime", .1f);
            Tune("_airAcceleration", 25f); Tune("_airControlSpeedFloor", 2f); Tune("_gravity", 18f);
            Tune("_slideMinimumSpeed", 6f); Tune("_slideBoost", 2f); Tune("_slideDuration", 1.2f);
            Tune("_slideLateralAcceleration", 18f); Tune("_slideMaximumTurnRate", 100f);
            Tune("_reboundDistance", .6f); Tune("_reboundAngle", 45f); Tune("_reboundJumpWindow", .15f);
            Tune("_reboundUpwardBoost", 3f); Tune("_reboundCooldown", .4f); Tune("_wallJumpOutwardRatio", .5f);
            Tune("_vaultMinimumHeight", .35f); Tune("_vaultMaximumHeight", 1.2f); Tune("_mantleMaximumHeight", 2f);
            Tune("_vaultDuration", .25f); Tune("_mantleDuration", .35f); Tune("_vaultCompletionTolerance", .05f);
            Tune("_ledgeReach", 1.2f); Tune("_ledgeMinimumHeight", .5f); Tune("_ledgeMaximumHeight", 1.8f);
            Tune("_softLandingThreshold", 12f); Tune("_hardLandingThreshold", 18f);
            Tune("_softLandingRetention", .6f); Tune("_hardLandingRetention", .3f);
            Tune("_injuredThreshold", 50f); Tune("_criticalThreshold", 25f); Tune("_injuredSpeedMultiplier", .95f);
            Tune("_footstepInterval", .35f); Tune("_walkingLoudness", .12f);
            Tune("_sprintLoudness", .25f); Tune("_slideLoudness", .55f); Tune("_traversalLoudness", 1f);
            typeof(PlayerProfile).GetField("_noiseCapacity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, 16);
            state = new PlayerBehaviorState();
            controller = new PlayerController(state, profile, new System.Random(13));
            controller.Reset(new EntityId(1), Vector3.zero, 0f);
            tick = 0;
        }

        private void Tune(string field, float value)
            => typeof(PlayerProfile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, value);
        private static InputFrame Frame(InputButtons pressed = InputButtons.None, InputButtons held = InputButtons.None,
            Vector2 move = default, Vector2 look = default) => new InputFrame(move, look, held, pressed, InputButtons.None);
        private PlayerTickResult Step(InputFrame frame, MovementProbe probe, float dt = Dt)
            => controller.Tick(frame, probe, dt, ++tick);
        private static MovementProbe Wall(int id = 10, bool grounded = false, float distance = .5f)
            => new MovementProbe(grounded, Vector3.up, true, distance, Vector3.left, 90f, id);
        private float Speed => new Vector2(state.Velocity.x, state.Velocity.z).magnitude;
        private void StartSlide()
        {
            state.Velocity = Vector3.forward * 8f;
            Assert.That(Step(Frame(InputButtons.Crouch), Ground).Crouched, Is.True);
        }
        private static bool Rebounded(PlayerTickResult result)
            => Array.Exists(result.Facts, f => f.Kind == TraversalKind.Rebound && f.Succeeded);

        [TestCase(false)] [TestCase(true)]
        public void HoldingSlideNeverLowersPostureOrChangesSpeedAndNoise(bool sprint)
        {
            var other = new PlayerBehaviorState();
            var baseline = new PlayerController(other, profile, new System.Random(13));
            baseline.Reset(new EntityId(1), Vector3.zero, 0f);
            var held = sprint ? InputButtons.Sprint : InputButtons.None;
            for (int i = 1; i <= 120; i++)
            {
                baseline.Tick(Frame(held: held, move: Vector2.up), Ground, Dt, i);
                var result = Step(Frame(held: held | InputButtons.Crouch, move: Vector2.up), Ground);
                Assert.That(result.Crouched || state.Crouched, Is.False);
                Assert.That(state.Velocity, Is.EqualTo(other.Velocity));
                Assert.That(state.RecentNoises, Is.EqualTo(other.RecentNoises));
            }
            Assert.That(Speed, Is.EqualTo(sprint ? 8f : 4f));
        }

        [Test]
        public void PressBelowSlideMinimumNeverCreatesCrouch()
        {
            var result = Step(Frame(InputButtons.Crouch, InputButtons.Crouch), Ground);
            Assert.That(result.Crouched, Is.False);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(result.Facts, Is.Empty);
        }

        [Test]
        public void SlideExpiryPublishesStandingHeightEvenWithButtonHeld()
        {
            StartSlide();
            var frame = Frame(held: InputButtons.Crouch);
            var result = Step(frame, Ground, profile.SlideDuration);
            Assert.That(result.Crouched || state.Crouched, Is.False);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Ground));
            var driverState = new PlayerDriverState { Height = result.Crouched ? .9f : 1.8f };
            Vector3 eye = new PlayerMoverPresenter().EyePosition(driverState, 1.6f, 1.8f);
            controller.CommitFrame(eye, new InputProbeRecord(1, tick, frame, Ground, profile.SlideDuration), result.Facts);
            Assert.That(eye.y, Is.EqualTo(1.6f));
            Assert.That(state.LastMovementSample.IsCrouched, Is.False);
            Assert.That(state.LastMovementSample.EyePosition.y, Is.EqualTo(1.6f));
        }

        [TestCase(false)] [TestCase(true)]
        public void BlockedSlideContinuesUntilClearAndDoesNotQueueAnExitJump(bool cancel)
        {
            StartSlide();
            var blocked = new MovementProbe(true, Vector3.up, standingBlocked: true);
            var result = Step(Frame(cancel ? InputButtons.Jump : InputButtons.None), blocked, 1.3f);
            Assert.That(result.Crouched, Is.True);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Slide));
            Assert.That(Speed, Is.GreaterThan(0f));
            Assert.That(state.JumpBufferRemaining, Is.Zero);
            result = Step(Frame(held: InputButtons.Crouch), Ground);
            Assert.That(result.Crouched, Is.False);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(state.Velocity.y, Is.Zero);
            Assert.That(result.Facts, Is.Empty);
        }

        [Test]
        public void ExpiredSlideArrestedUnderCeilingCanBackOutInsteadOfBecomingTrapped()
        {
            StartSlide();
            controller.CommitPose(new PlayerMoveResult(Vector3.zero, Vector3.zero, true, false));
            var blocked = new MovementProbe(true, Vector3.up, standingBlocked: true);
            Step(Frame(move: Vector2.down), blocked, 1.3f);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Slide));
            Assert.That(state.Velocity.z, Is.LessThan(0f));
            Assert.That(Speed, Is.LessThanOrEqualTo(profile.WalkSpeed));
            Assert.That(Step(Frame(), Ground).Crouched, Is.False);
        }

        [Test]
        public void LosingSupportUnderCeilingKeepsSlideCapsuleButStillFalls()
        {
            StartSlide();
            var result = Step(Frame(), new MovementProbe(false, Vector3.up, standingBlocked: true));
            Assert.That(result.Crouched, Is.True);
            Assert.That(state.Grounded, Is.False);
            Assert.That(state.Velocity.y, Is.LessThan(0f));
            Assert.That(Step(Frame(), default).Crouched, Is.False);
        }

        [TestCase(false, -4f)] [TestCase(false, 0f)] [TestCase(false, 4f)]
        [TestCase(true, -4f)] [TestCase(true, 0f)] [TestCase(true, 4f)]
        public void WallJumpKeepsSpeedAndAlongWallMotionWhileKickingOutward(bool grounded, float across)
        {
            state.MovementState = grounded ? MovementState.Ground : MovementState.Air;
            state.Velocity = new Vector3(across, grounded ? 0f : -10f, 8f);
            float before = Speed;
            // Input into the wall must not undo the kick on its admission tick.
            var result = Step(Frame(InputButtons.Jump, move: Vector2.right), Wall(grounded: grounded));
            Assert.That(Rebounded(result), Is.True);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(state.Grounded, Is.False);
            Assert.That(state.CoyoteRemaining, Is.Zero);
            Assert.That(state.Velocity.x, Is.LessThan(0f));
            Assert.That(state.Velocity.z, Is.GreaterThan(0f));
            Assert.That(Speed, Is.EqualTo(before).Within(.00001f));
            Assert.That(state.Velocity.y, Is.EqualTo(3f - 18f * Dt).Within(.00001f));
            Assert.That(result.Facts.Length, Is.EqualTo(1));
            Assert.That(state.RecentNoises.Count, Is.EqualTo(1));
            Assert.That(state.RecentNoises[0].SourceKind, Is.EqualTo(NoiseSourceKind.Rebound));
            Assert.That(state.RecentNoises[0].Loudness, Is.EqualTo(profile.TraversalLoudness));
        }

        [TestCase(1f, 1f)] [TestCase(8f, 8f)] [TestCase(30f, 14f)]
        public void WallKickPreservesLowSpeedsAndRespectsExistingCap(float speed, float expected)
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * speed;
            Step(Frame(InputButtons.Jump), Wall());
            Assert.That(Speed, Is.EqualTo(expected).Within(.00001f));
        }

        [Test]
        public void StandingStillUsesOrdinaryGroundJumpNotWallKick()
        {
            var result = Step(Frame(InputButtons.Jump), Wall(grounded: true));
            Assert.That(Rebounded(result), Is.False);
            Assert.That(result.Facts[0].Kind, Is.EqualTo(TraversalKind.Jump));
        }

        [Test]
        public void TouchingWallWithoutFreshOrBufferedPressNeverBounces()
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.right * 8f;
            Assert.That(Rebounded(Step(Frame(held: InputButtons.Jump), Wall())), Is.False);
            Assert.That(state.Velocity.x, Is.EqualTo(8f));
        }

        [TestCase(.149f, true)] [TestCase(.151f, false)]
        public void WallJumpUsesExistingReboundBufferExpiry(float delay, bool expected)
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * 8f;
            Step(Frame(InputButtons.Jump), default);
            Assert.That(Rebounded(Step(Frame(), Wall(), delay)), Is.EqualTo(expected));
        }

        [Test]
        public void SameWallStaysSpentAcrossSeparationUntilLanding()
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * 8f;
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall())), Is.True);
            Step(Frame(), default, .5f);
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall())), Is.False);
            state.Velocity = Vector3.forward * 8f + Vector3.down;
            Step(Frame(), Ground);
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall(grounded: true))), Is.True);
        }

        [Test]
        public void DifferentWallContactRearmsButNeverBypassesCooldown()
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * 8f;
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall())), Is.True);
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall(11), .1f)), Is.False);
            Assert.That(state.LastReboundWall, Is.Zero, "A different valid wall contact releases the old wall latch.");
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall(), .3f)), Is.True);
        }

        [TestCase(.6f, true)] [TestCase(.601f, false)] [TestCase(-1f, false)]
        [TestCase(float.NaN, false)] [TestCase(float.PositiveInfinity, false)]
        public void WallReachIsFiniteInclusiveAndDoesNotDependOnFacing(float distance, bool expected)
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * 8f;
            Assert.That(Rebounded(Step(Frame(InputButtons.Jump), Wall(distance: distance))), Is.EqualTo(expected));
        }

        [TestCase(false, 1f)] [TestCase(false, 1.5f)] [TestCase(true, 1f)]
        public void VaultMantleAndAutomaticLedgeKeepPriorityOverWallJump(bool ledge, float height)
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * 8f;
            var probe = new MovementProbe(false, Vector3.up, true, .5f, Vector3.left, 90f, 10,
                !ledge, height, 1.8f, new Vector3(0f, height, 1f));
            var result = Step(Frame(InputButtons.Jump), probe);
            Assert.That(result.Traversing, Is.True);
            Assert.That(state.VaultKind, Is.EqualTo(ledge || height > 1.2f ? TraversalKind.Mantle : TraversalKind.Vault));
            Assert.That(Rebounded(result), Is.False);
            Assert.That(state.LastReboundWall, Is.Zero);
        }

        [Test]
        public void RejectedVaultConsumesPressInsteadOfFallingThroughToWallKick()
        {
            state.MovementState = MovementState.Air; state.Velocity = Vector3.forward * 8f;
            var probe = new MovementProbe(false, Vector3.up, true, .5f, Vector3.left, 90f, 10, true, 1f);
            var result = Step(Frame(InputButtons.Jump), probe);
            Assert.That(Rebounded(result), Is.False);
            Assert.That(result.Facts.Length, Is.EqualTo(1));
            Assert.That(result.Facts[0].Kind, Is.EqualTo(TraversalKind.Vault));
            Assert.That(result.Facts[0].Succeeded, Is.False);
        }

        [Test]
        public void ManagedHeadingAndSlideRotationPreserveDirectionAndTurnLimit()
        {
            controller.Reset(new EntityId(1), Vector3.zero, 90f);
            Assert.That((state.Forward - Vector3.right).sqrMagnitude, Is.LessThan(.000001f));
            Step(Frame(look: new Vector2(90f, 0f)), Ground);
            Assert.That((state.Forward - Vector3.back).sqrMagnitude, Is.LessThan(.000001f));
            StartSlide();
            Step(Frame(look: new Vector2(30f, 0f)), Ground, .1f);
            Assert.That(Vector3.Angle(Vector3.forward, state.Velocity), Is.EqualTo(10f).Within(.001f));
        }
    }
}
