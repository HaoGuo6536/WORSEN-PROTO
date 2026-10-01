// ============================================================================
// PlayerControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises movement thresholds, life reset, resolved traversal and recorded pure replay.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify locomotion, clearance-safe slides and jump-requested wall kicks.
//   - Verify vault/ledge priority, steering, cancellation and resolved outcomes.
//   - Verify external motion, landing recovery, health and hit protection boundaries.
//   - Verify committed sprint/posture facts, typed noise and life resets.
//   - Verify deterministic input/probe/resolution replay.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Edit Mode NUnit tests. A default profile supplies actual prototype tuning; frames, probes and delta time are explicit.
//   Pure traversal resolution has no obstacles; live collision coverage belongs to PlayerDriverTests.
//   Damage is supplied separately at matching replay ticks because input records do not encode hits.
//   Reset after changing profile speed caps so arithmetic tests use the next life's copied values.
//   No other Domain system or Presentation system is referenced.
//   Crouch expectations follow the owner's 2026-09-30 Windows playtest decision:
//   holding C never lowers posture; an obstructed slide continues until it can stand.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerControllerTests
    {
        private const float Dt = 1f / 60f;
        private PlayerProfile _profile;
        private PlayerBehaviorState _state;
        private PlayerController _controller;
        private static readonly MovementProbe Ground = new MovementProbe(true, Vector3.up);
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<PlayerProfile>();
            _state = new PlayerBehaviorState();
            _controller = new PlayerController(_state, _profile, new System.Random(77));
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_profile); }
        private static InputFrame Frame(Vector2 move = default, InputButtons pressed = InputButtons.None,
            InputButtons held = InputButtons.None, Vector2 look = default)
            => new InputFrame(move, look, held, pressed, InputButtons.None);
        private float Speed => new Vector2(_state.Velocity.x, _state.Velocity.z).magnitude;

        [Test]
        public void ExternalImpulseAddsOnceOnNextTickAndCommitsCollisionVelocity()
        {
            _state.Velocity = Vector3.forward * 2f;
            _controller.ApplyExternalVelocity(Vector3.right * 3f, ExternalMotionKind.CollapseHandThrow);
            _controller.ApplyExternalVelocity(Vector3.right, ExternalMotionKind.Impulse);
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.forward * 2f));
            PlayerTickResult result = _controller.Tick(default, default, Dt, 1);
            Assert.That(_state.Velocity.x, Is.EqualTo(4f).Within(0.00001f));
            Assert.That(_state.Velocity.z, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(result.Displacement, Is.EqualTo(_state.Velocity * Dt));
            Assert.That(_state.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
            var presenter = new PlayerMoverPresenter();
            Vector3 resolved = presenter.ContactVelocity(_state.Velocity, Vector3.left, false);
            _controller.CommitPose(new PlayerMoveResult(Vector3.zero, resolved, false, false));
            _controller.Tick(default, default, Dt, 2);
            Assert.That(_state.Velocity.x, Is.Zero, "A blocked impulse must not be reapplied.");
            Assert.That(_state.Velocity.z, Is.EqualTo(2f).Within(0.00001f));
        }

        [Test]
        public void ExternalImpulseClampsCombinedThreeDimensionalSpeedAndLeavesGround()
        {
            var data = new UnityEditor.SerializedObject(_profile);
            data.FindProperty("_maximumExternalMotionSpeed").floatValue = 7f;
            data.ApplyModifiedPropertiesWithoutUndo();
            _controller.ApplyExternalVelocity(new Vector3(30f, 40f, 0f), ExternalMotionKind.Impulse);
            _controller.ApplyExternalAcceleration(Vector3.forward * 50f, 1f);
            _controller.Tick(default, Ground, Dt, 1);
            Assert.That(_state.Velocity.magnitude, Is.EqualTo(7f).Within(0.00001f));
            Assert.That(_state.Velocity.x / _state.Velocity.y, Is.EqualTo(0.75f).Within(0.00001f));
            Assert.That(_state.Grounded, Is.False);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(_state.CoyoteRemaining, Is.Zero);
        }

        [Test]
        public void ExternalAccelerationIntegratesExplicitIntervalsDuringGrace()
        {
            _controller.ApplyHit(1f, HitSeverity.Light);
            for (int tick = 1; tick <= 3; tick++)
            {
                _controller.ApplyExternalAcceleration(Vector3.right * 6f, 0.1f);
                _controller.ApplyExternalAcceleration(Vector3.right * 6f, 0.15f);
                _controller.Tick(default, default, Dt, tick);
                Assert.That(_state.Velocity.x, Is.EqualTo(tick * 1.5f).Within(0.00001f));
                Assert.That(_state.GraceActive, Is.True);
            }
            _controller.Tick(default, default, Dt, 4);
            Assert.That(_state.Velocity.x, Is.EqualTo(4.5f).Within(0.00001f));
            Assert.That(_state.Health, Is.EqualTo(99f));
        }

        [Test]
        public void ExternalMotionInterruptsVaultAndCannotEnterAnotherTraversalThatTick()
        {
            var ledge = new MovementProbe(false, Vector3.up, vaultHeight: 1f,
                vaultClearance: 2f, vaultTarget: new Vector3(0f, 1f, 1f));
            Assert.That(_controller.Tick(default, ledge, Dt, 1).Traversing, Is.True);
            _controller.ApplyExternalVelocity(Vector3.back * 4f + Vector3.up * 2f, ExternalMotionKind.CollapseHandThrow);
            PlayerTickResult result = _controller.Tick(default, ledge, Dt, 2);
            Assert.That(result.Traversing, Is.False);
            Assert.That(result.Displacement.z, Is.LessThan(0f));
            Assert.That(_state.VaultRemaining, Is.Zero);
            Assert.That(_state.PreserveVelocityOnCommit || _state.VaultCompletionPending, Is.False);
            Assert.That(_state.TraversalSampleActive, Is.True, "Existing progress publication ends the interrupted traversal.");
        }

        [Test]
        public void ExternalMotionRejectsInvalidInputsAndClearsAtLifeBoundaries()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyExternalVelocity(Vector3.zero, (ExternalMotionKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyExternalVelocity(Vector3.up * float.NaN, ExternalMotionKind.Impulse));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyExternalAcceleration(Vector3.one, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyExternalAcceleration(Vector3.one, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyExternalVelocity(Vector3.one * float.MaxValue, ExternalMotionKind.Impulse));
            _controller.ApplyExternalAcceleration(Vector3.one, 0f);
            Assert.That(_state.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
            _controller.ApplyExternalVelocity(Vector3.right, ExternalMotionKind.Impulse);
            _controller.EndRecovery();
            Assert.That(_state.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
            _controller.ApplyExternalVelocity(Vector3.right, ExternalMotionKind.Impulse);
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(_state.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
            _controller.ApplyExternalVelocity(Vector3.right, ExternalMotionKind.Impulse);
            _controller.ApplyHit(100f);
            _controller.Tick(default, Ground, Dt, 1);
            _controller.ApplyExternalVelocity(Vector3.right, ExternalMotionKind.Impulse);
            Assert.That(_state.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DefaultGroundMovementWalksAndSprintHoldRuns()
        {
            for (int i = 0; i < 60; i++) _controller.Tick(Frame(Vector2.up), Ground, Dt, i);
            Assert.That(Speed, Is.EqualTo(_profile.WalkSpeed).Within(0.0001f));
            for (int i = 0; i < 60; i++) _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, Dt, i + 60);
            Assert.That(Speed, Is.EqualTo(_profile.SprintSpeed).Within(0.0001f));
            for (int i = 0; i < 60; i++) _controller.Tick(Frame(Vector2.up), Ground, Dt, i + 120);
            Assert.That(Speed, Is.EqualTo(_profile.WalkSpeed).Within(0.0001f));
        }

        [Test]
        public void HeldSlideButtonNeverCommitsCrouchOrCreatesALowPosture()
        {
            var held = Frame(held: InputButtons.Crouch);
            CommitAction(held, Ground, Vector3.zero, true, 1);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(_state.LastMovementSample.IsCrouched, Is.False);
            Assert.That(_state.LastMovementSample.IsSprinting, Is.False);
            CommitAction(Frame(), new MovementProbe(true, Vector3.up, standingBlocked: true), Vector3.zero, true, 2);
            Assert.That(_state.LastMovementSample.IsCrouched, Is.False, "A clearance fact cannot create a crouch state.");
            CommitAction(Frame(), Ground, Vector3.zero, true, 3);
            Assert.That(_state.LastMovementSample.IsCrouched, Is.False);
        }

        [Test]
        public void HeldSlideButtonDoesNotPreventJumpOrStandingAfterSlideExpiry()
        {
            _controller.Tick(Frame(held: InputButtons.Crouch), Ground, Dt, 1);
            var jump = _controller.Tick(Frame(pressed: InputButtons.Jump, held: InputButtons.Crouch), Ground, Dt, 2);
            Assert.That(jump.Crouched, Is.False);
            Assert.That(Array.Exists(jump.Facts, fact => fact.Kind == TraversalKind.Jump && fact.Succeeded), Is.True);
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch, InputButtons.Crouch), Ground, Dt, 1);
            var expired = _controller.Tick(Frame(held: InputButtons.Crouch), Ground, _profile.SlideDuration + Dt, 2);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(expired.Crouched, Is.False);
            Assert.That(_controller.Tick(Frame(), Ground, Dt, 3).Crouched, Is.False);
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch, InputButtons.Crouch), Ground, Dt, 4);
            var cancelled = _controller.Tick(Frame(pressed: InputButtons.Jump, held: InputButtons.Crouch), Ground, Dt, 5);
            Assert.That(cancelled.Crouched, Is.False, "Successful slide-jump cancellation has standing clearance and overrides held C while airborne.");
        }

        [TestCase(0f, true, true, true, false, false)]
        [TestCase(2f, true, true, true, false, false)]
        [TestCase(8f, true, true, true, false, true)]
        [TestCase(8f, true, false, true, false, false)]
        [TestCase(8f, true, true, false, false, false)]
        [TestCase(8f, false, true, true, false, false)]
        [TestCase(8f, true, true, true, true, true)]
        public void SprintFactRequiresAchievedGroundMotionAndUprightSprintInput(float speed, bool grounded,
            bool movingInput, bool sprintHeld, bool crouched, bool expected)
        {
            InputButtons held = (sprintHeld ? InputButtons.Sprint : InputButtons.None)
                | (crouched ? InputButtons.Crouch : InputButtons.None);
            _state.Velocity = Vector3.forward * 8f;
            CommitAction(Frame(movingInput ? Vector2.up : Vector2.zero, held: held), Ground,
                Vector3.forward * speed, grounded, 1);
            Assert.That(_state.LastMovementSample.IsSprinting, Is.EqualTo(expected));
            Assert.That(_state.IsSprinting, Is.EqualTo(expected));
            Assert.That(_state.LastMovementSample.IsCrouched, Is.False);
        }

        [Test]
        public void SlideAndModifiedSprintFactsRemainDistinctAndResetWithTheLife()
        {
            _state.Velocity = Vector3.forward * 8f;
            CommitAction(Frame(Vector2.up, InputButtons.Crouch, InputButtons.Sprint | InputButtons.Crouch), Ground,
                Vector3.forward * 10f, true, 1);
            Assert.That(_state.LastMovementSample.IsCrouched, Is.True);
            Assert.That(_state.LastMovementSample.IsSprinting, Is.False);
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            _controller.SetGrabSpeedMultiplier(0.25f);
            CommitAction(Frame(Vector2.up, held: InputButtons.Sprint), Ground, Vector3.forward * 2f, true, 2);
            Assert.That(_state.LastMovementSample.IsSprinting, Is.True, "Achieved pace compares against the same modifier-scaled walking speed.");
            _controller.ApplyHit(_profile.MaximumHealth);
            Assert.That(_state.IsSprinting, Is.False);
            _controller.Reset(new EntityId(3), Vector3.zero, 0f);
            Assert.That(_state.IsSprinting || _state.Crouched, Is.False);
            Assert.That(_state.LastMovementSample.IsSprinting || _state.LastMovementSample.IsCrouched, Is.False);
        }

        [Test]
        public void RecordedActionFactsReplayExactlyWithoutNewCaptureFields()
        {
            var inputs = new[] { Frame(held: InputButtons.Crouch), Frame(), Frame(Vector2.up, held: InputButtons.Sprint), Frame(Vector2.up, held: InputButtons.Sprint) };
            var velocities = new[] { Vector3.zero, Vector3.zero, Vector3.forward * 8f, Vector3.zero };
            var records = new InputProbeRecord[inputs.Length];
            var samples = new PlayerMovementSample[inputs.Length];
            for (int index = 0; index < inputs.Length; index++)
            {
                records[index] = CommitAction(inputs[index], Ground, velocities[index], true, index + 1);
                samples[index] = _state.LastMovementSample;
            }
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            for (int index = 0; index < records.Length; index++)
            {
                _controller.Replay(records[index]);
                Assert.That(_state.LastMovementSample.IsCrouched, Is.EqualTo(samples[index].IsCrouched));
                Assert.That(_state.LastMovementSample.IsSprinting, Is.EqualTo(samples[index].IsSprinting));
                Assert.That(_state.LastMovementSample.Position, Is.EqualTo(samples[index].Position));
            }
        }

        private InputProbeRecord CommitAction(InputFrame input, MovementProbe probe, Vector3 resolvedVelocity, bool grounded, long tick)
        {
            var decision = _controller.Tick(input, probe, Dt, tick);
            var position = _state.Position + resolvedVelocity * Dt;
            var resolution = new MovementResolution(position, resolvedVelocity, grounded, false,
                position + Vector3.up * (decision.Crouched ? 0.8f : 1.6f));
            var record = new InputProbeRecord(InputProbeRecord.CurrentSchemaVersion, tick, input, probe, Dt, resolution);
            _controller.CommitPose(new PlayerMoveResult(position, resolvedVelocity, grounded, false));
            _controller.CommitFrame(resolution.EyePosition, record, decision.Facts);
            return record;
        }

        [TestCase(5.999f, false)]
        [TestCase(6f, true)]
        public void SlideEntryHonorsInclusiveMinimum(float speed, bool enters)
        {
            _state.Velocity = Vector3.forward * speed;
            PlayerTickResult result = _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            Assert.That(_state.MovementState == MovementState.Slide, Is.EqualTo(enters));
            if (enters)
            {
                Assert.That(Speed, Is.GreaterThan(speed));
                Assert.That(result.Crouched, Is.True);
                Assert.That(result.Facts[0].Kind, Is.EqualTo(TraversalKind.Slide));
            }
        }

        [Test]
        public void SlideJumpPreservesHorizontalMomentum()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            float entry = Speed;
            _controller.Tick(Frame(Vector2.up, InputButtons.Jump), Ground, Dt, 2);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(Speed, Is.EqualTo(entry).Within(0.0001f));
            Assert.That(_state.Velocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void BlockedSlideCancellationContinuesSlideWithoutStandingOrDelayedJump()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            float slidingSpeed = Speed;
            var blocked = new MovementProbe(true, Vector3.up, standingBlocked: true);
            PlayerTickResult cancelled = _controller.Tick(Frame(pressed: InputButtons.Jump), blocked, Dt, 2);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Slide));
            Assert.That(_state.SlideRemaining, Is.Zero);
            Assert.That(cancelled.Crouched, Is.True);
            Assert.That(Speed, Is.LessThan(slidingSpeed));
            Assert.That(_state.Velocity.y, Is.Zero);
            Assert.That(cancelled.Facts, Is.Empty);
            Assert.That(_state.JumpBufferRemaining, Is.Zero);
            PlayerTickResult clear = _controller.Tick(Frame(), Ground, Dt, 3);
            Assert.That(clear.Crouched, Is.False);
            Assert.That(_state.Velocity.y, Is.Zero);
            Assert.That(clear.Facts, Is.Empty);
        }

        [Test]
        public void JumpCancellationWinsOverSimultaneousSlidePress()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            var blocked = new MovementProbe(true, Vector3.up, standingBlocked: true);
            _controller.Tick(Frame(Vector2.up, InputButtons.Jump | InputButtons.Crouch), blocked, Dt, 2);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Slide));
            Assert.That(_state.SlideRemaining, Is.Zero);
        }

        [Test]
        public void CommittedUphillLandingRegainsGroundControlAndCanJumpAgain()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = new Vector3(0f, -2f, 8f);
            _controller.CommitPose(new PlayerMoveResult(Vector3.up, new Vector3(0f, 2f, 6f), true, false));
            var slope = new MovementProbe(true, new Vector3(0f, 0.9f, -0.4f).normalized);
            PlayerTickResult landed = _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), slope, Dt, 1);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(_state.Grounded, Is.True);
            Assert.That(Array.Exists(landed.Facts, fact => fact.Kind == TraversalKind.Land), Is.True);
            _controller.Tick(Frame(Vector2.up, InputButtons.Jump, InputButtons.Sprint), slope, Dt, 2);
            Assert.That(_state.Velocity.y, Is.GreaterThan(0f));
            Assert.That(_state.Grounded, Is.False);
            PlayerTickResult rising = _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), slope, Dt, 3);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(_state.Grounded, Is.False, "A nearby floor does not cancel a real rising jump.");
            Assert.That(rising.Facts, Is.Empty);
        }

        [Test]
        public void SlideCannotStandInsideGateAndRecoversWhenClear()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            var blocked = new MovementProbe(true, Vector3.up, standingBlocked: true);
            PlayerTickResult result = _controller.Tick(Frame(), blocked, 1.3f, 2);
            Assert.That(result.Crouched, Is.True);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Slide));
            result = _controller.Tick(Frame(), Ground, Dt, 3);
            Assert.That(result.Crouched, Is.False);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
        }

        [TestCase(0.099f, true)]
        [TestCase(0.101f, false)]
        public void CoyoteWindowHasAnExplicitExpiry(float delay, bool jumps)
        {
            _controller.Tick(Frame(), Ground, Dt, 1);
            _controller.Tick(Frame(pressed: InputButtons.Jump), default, delay, 2);
            Assert.That(_state.Velocity.y > 0f, Is.EqualTo(jumps));
        }

        [Test]
        public void InterTickJumpTapIsBufferedUntilGroundReturns()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.down;
            var tap = new InputFrame(Vector2.zero, Vector2.zero, InputButtons.None, InputButtons.Jump, InputButtons.Jump);
            _controller.Tick(tap, default, 0.05f, 1);
            _controller.Tick(Frame(), Ground, 0.05f, 2);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(_state.Velocity.y, Is.GreaterThan(0f));
            Assert.That(_state.JumpBufferRemaining, Is.Zero);
        }

        [Test]
        public void AirSteeringChangesDirectionWithoutFreeHorizontalSpeed()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 10f;
            _controller.Tick(Frame(Vector2.right), default, 0.1f, 1);
            Assert.That(Speed, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(_state.Velocity.x, Is.GreaterThan(0f));
        }

        [Test]
        public void UpwardSlopeProjectionDoesNotBecomeAnAirJump()
        {
            _state.MovementState = MovementState.Ground;
            _state.Velocity = new Vector3(0f, 3f, 7f);
            var slope = new MovementProbe(true, new Vector3(0f, 0.9f, -0.4f).normalized);
            _controller.Tick(Frame(Vector2.up), slope, Dt, 1);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(_state.Grounded, Is.True);
        }

        [Test]
        public void LookBackFreezesBodyAndLeavesJumpAvailable()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Jump, InputButtons.LookBack, new Vector2(30f, 5f)), Ground, Dt, 1);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(30f));
            Assert.That(_state.HeadLookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(_state.LookBack, Is.True);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            _controller.Tick(Frame(look: new Vector2(10f, 4f)), default, Dt, 2);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(40f));
            Assert.That(_state.HeadLookDelta, Is.EqualTo(new Vector2(0f, 4f)));
            Assert.That(_state.LookBack, Is.False);
        }

        [TestCase(1f, 0.25f)]
        [TestCase(1.5f, 0.35f)]
        public void HeldFreeLookSurvivesTraversalWhileTrajectoryAndLockMatchForwardView(float height, float duration)
        {
            var baselineState = new PlayerBehaviorState();
            var baseline = new PlayerController(baselineState, _profile, new System.Random(77));
            baseline.Reset(new EntityId(1), Vector3.zero, 90f);
            _controller.Reset(new EntityId(1), Vector3.zero, 90f);
            _state.Velocity = baselineState.Velocity = Vector3.right * 8f;
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: height, vaultClearance: 1.8f, vaultTarget: new Vector3(2f, height, 0f));
            int steps = Mathf.RoundToInt(duration / Dt);
            for (int index = 0; index < steps; index++)
            {
                InputButtons pressed = index == 0 ? InputButtons.Jump : InputButtons.None;
                var look = new Vector2(index == 0 ? 165f : 0.5f, 2f);
                var actual = _controller.Tick(Frame(Vector2.up, pressed, InputButtons.LookBack, look), probe, Dt, index + 1);
                var expected = baseline.Tick(Frame(Vector2.up, pressed), probe, Dt, index + 1);
                Assert.That(_state.LookBack, Is.True, "Q must survive admission and every locked traversal tick.");
                Assert.That(_state.HeadingDegrees, Is.EqualTo(Mathf.Repeat(90f + 165f + index * 0.5f, 360f)));
                Assert.That(_state.HeadLookDelta, Is.EqualTo(Vector2.zero));
                Assert.That(actual.Traversing, Is.True);
                Assert.That(actual.TraversalStart, Is.EqualTo(expected.TraversalStart));
                Assert.That(actual.TraversalTarget, Is.EqualTo(expected.TraversalTarget));
                Assert.That(actual.TraversalHeight, Is.EqualTo(expected.TraversalHeight));
                Assert.That(actual.TraversalProgress, Is.EqualTo(expected.TraversalProgress));
                Assert.That(_state.InputLockSeconds, Is.EqualTo(baselineState.InputLockSeconds));
                Assert.That(_state.InputLockSeconds, Is.EqualTo(Mathf.Max(0f, duration * (2f / 3f) - index * Dt)).Within(0.000001f));
                Assert.That(_state.VaultExitVelocity.magnitude, Is.EqualTo(baselineState.VaultExitVelocity.magnitude).Within(0.0001f));
                if (actual.TraversalProgress <= 2f / 3f) Assert.That(actual.TraversalOffset, Is.EqualTo(Vector3.zero));
            }
            Assert.That(_state.VaultRemaining, Is.Zero);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            _controller.Tick(Frame(look: new Vector2(7f, 0f)), default, Dt, steps + 1);
            Assert.That(_state.LookBack, Is.False);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(Mathf.Repeat(90f + 165f + (steps - 1) * 0.5f + 7f, 360f)));
            Assert.That(_state.InputLockSeconds, Is.Zero);
        }

        [Test]
        public void LookBackPreservesLateralAirAuthorityAndForwardInput()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.right, held: InputButtons.LookBack), default, 0.1f, 1);
            float reduced = _state.Velocity.x;
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.right), default, 0.1f, 1);
            Assert.That(reduced, Is.EqualTo(_state.Velocity.x).Within(0.0001f));
        }

        [Test]
        public void SlideSteeringIsHeavyAndCannotGainSpeedOrRenewDuration()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            float speed = _state.Velocity.magnitude, remaining = _state.SlideRemaining;
            Vector3 before = _state.Velocity;
            _controller.Tick(Frame(Vector2.right, InputButtons.Crouch, InputButtons.LookBack, new Vector2(170f, 0f)), Ground, 0.1f, 2);
            Assert.That(Vector3.SignedAngle(before, _state.Velocity, Vector3.up), Is.GreaterThan(4f)
                .And.LessThanOrEqualTo(_profile.SlideMaximumTurnRate * 0.1f + 0.001f));
            Assert.That(_state.Velocity.magnitude, Is.LessThanOrEqualTo(speed));
            Assert.That(_state.SlideRemaining, Is.LessThan(remaining));
            Assert.That(_state.HeadingDegrees, Is.EqualTo(170f));
        }

        [Test]
        public void SlideCannotRestoreMomentumLostToCollision()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            _controller.CommitPose(new PlayerMoveResult(Vector3.zero, Vector3.forward * 2f, true, false));
            _controller.Tick(Frame(Vector2.right), Ground, Dt, 2);
            Assert.That(_state.Velocity.magnitude, Is.LessThanOrEqualTo(2.0001f));
        }

        [Test]
        public void GrabSlowKeepsEscapeAuthorityAndResetClearsPerks()
        {
            _controller.SetMovementEffects(0.2f, 0.8f, 1f);
            _controller.SetGrabSpeedMultiplier(0f);
            _controller.Tick(Frame(Vector2.up), Ground, 0.2f, 1);
            Assert.That(_state.Velocity.z, Is.EqualTo(_profile.WalkSpeed * 0.25f).Within(0.0001f));
            Assert.That(_state.FootstepNoiseMultiplier, Is.EqualTo(0.2f));
            _controller.SetGrabSpeedMultiplier(1f);
            Assert.That(_state.ReboundCooldownMultiplier, Is.EqualTo(0.8f));
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(_state.FootstepNoiseMultiplier, Is.EqualTo(1f));
            Assert.That(_state.ReboundCooldownMultiplier, Is.EqualTo(1f));
            Assert.That(_state.GrabSpeedMultiplier, Is.EqualTo(1f));
        }

        [TestCase(11.999f, 10f, MovementState.Ground, 0f)]
        [TestCase(12f, 6f, MovementState.Stumble, 0.2f)]
        [TestCase(18f, 6f, MovementState.Stumble, 0.2f)]
        [TestCase(18.001f, 3f, MovementState.Stumble, 0.5f)]
        public void LandingThresholdsRetainMomentumWithoutLock(float impact, float speed, MovementState movement, float stumble)
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = new Vector3(0f, -impact, 10f);
            var result = _controller.Tick(Frame(), Ground, Dt, 1);
            var landings = Array.FindAll(result.Facts, fact => fact.Kind == TraversalKind.Land);
            Assert.That(landings.Length, Is.EqualTo(1));
            Assert.That(landings[0].Severity, Is.EqualTo(impact > _profile.HardLandingThreshold ? 1f : 0f));
            Assert.That(landings[0].Duration, Is.EqualTo(stumble));
            Assert.That(Speed, Is.EqualTo(speed).Within(0.0001f));
            Assert.That(_state.MovementState, Is.EqualTo(movement));
            Assert.That(_state.StumbleRemaining, Is.EqualTo(stumble));
            Assert.That(_state.InputLockSeconds, Is.Zero);
            _controller.Tick(Frame(pressed: InputButtons.Jump), Ground, Dt, 2);
            Assert.That(_state.Velocity.y, Is.GreaterThan(0f));
        }

        [TestCase(0.6f, 45f, true)]
        [TestCase(0.601f, 45f, false)]
        [TestCase(0.6f, 45.001f, true)]
        [TestCase(0.6f, 90f, true)]
        [TestCase(0.6f, 180f, true)]
        public void ReboundProbeThresholdsAreInclusive(float distance, float angle, bool succeeds)
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            var wall = new MovementProbe(false, Vector3.up, true, distance, Vector3.back, angle, 100);
            PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), wall, Dt, 1);
            Assert.That(Array.Exists(result.Facts, fact => fact.Kind == TraversalKind.Rebound), Is.EqualTo(succeeds));
        }

        [Test]
        public void ReboundRequiresCooldownAndCannotChainFromSameWallUntilLanding()
        {
            var wall = new MovementProbe(false, Vector3.up, true, 0.5f, Vector3.back, 0f, 100);
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(pressed: InputButtons.Jump), wall, Dt, 1);
            Assert.That(_state.LastReboundWall, Is.EqualTo(100));
            _state.Velocity = Vector3.forward * 8f;
            var other = new MovementProbe(false, Vector3.up, true, 0.5f, Vector3.back, 0f, 101);
            PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), wall, 0.1f, 2);
            Assert.That(result.Facts, Is.Empty);
            _state.Velocity = Vector3.forward * 8f;
            result = _controller.Tick(Frame(pressed: InputButtons.Jump), wall, 0.5f, 3);
            Assert.That(result.Facts, Is.Empty);
            _state.Velocity = Vector3.forward * 8f;
            result = _controller.Tick(Frame(pressed: InputButtons.Jump), other, Dt, 4);
            Assert.That(result.Facts[0].Kind, Is.EqualTo(TraversalKind.Rebound));
        }

        [Test]
        public void InvalidProbeValuesCannotStartTraversalOrPoisonState()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            var invalid = new MovementProbe(false, Vector3.up, true, float.NaN,
                Vector3.back, 0f, 1, true, 1f, 1.8f, new Vector3(float.NaN, 1f, 0f));
            PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), invalid, Dt, 1);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(float.IsNaN(_state.Velocity.x), Is.False);
            Assert.That(result.Facts[0].Succeeded, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.Tick(Frame(), Ground, float.NaN, 2));
        }

        [Test]
        public void RejectedVaultIsCountedOnceDuringABufferedJumpPress()
        {
            _state.MovementState = MovementState.Air;
            var blocked = new MovementProbe(false, Vector3.up, vaultCandidate: true,
                vaultHeight: 1f, vaultClearance: 0f, vaultTarget: Vector3.forward);
            PlayerTickResult first = _controller.Tick(Frame(pressed: InputButtons.Jump), blocked, Dt, 1);
            PlayerTickResult buffered = _controller.Tick(Frame(), blocked, Dt, 2);
            Assert.That(first.Facts.Length, Is.EqualTo(1));
            Assert.That(first.Facts[0].Succeeded, Is.False);
            Assert.That(buffered.Facts, Is.Empty);
        }

        [TestCase(1f, 0.25f, TraversalKind.Vault, 2f, false)]
        [TestCase(1.5f, 0.35f, TraversalKind.Mantle, 2f, false)]
        [TestCase(1f, 0.25f, TraversalKind.Vault, 3.10000038f, false)]
        [TestCase(1f, 0.25f, TraversalKind.Vault, 3.28915703f, false)]
        [TestCase(1f, 0.25f, TraversalKind.Vault, 3.28887312f, false)]
        [TestCase(1f, 0.25f, TraversalKind.Vault, 3.28887312f, true)]
        public void TraversalPreservesEntrySpeedAndPublishesOneResolvedOutcome(float height, float duration,
            TraversalKind kind, float distance, bool injured)
        {
            ExerciseResolvedTraversal(height, duration, kind, distance, injured, 10f, -1, true);
        }

        [TestCase(1f, false)]
        [TestCase(1f, true)]
        [TestCase(1.5f, false)]
        [TestCase(1.5f, true)]
        public void TraversalAdmissionHonorsTheHealthyOrInjuredDistanceBudget(float height, bool injured)
        {
            float duration = height > _profile.VaultMaximumHeight ? _profile.MantleDuration : _profile.VaultDuration;
            TraversalKind kind = height > _profile.VaultMaximumHeight ? TraversalKind.Mantle : TraversalKind.Vault;
            foreach (float offset in new[] { -0.0001f, 0f, 0.000005f, 0.00002f })
            {
                _controller.Reset(new EntityId(1), Vector3.zero, 0f);
                if (injured) _controller.ApplyHit(_profile.LungeDamage);
                float budget = _controller.MaximumMovementSpeed * duration;
                var probe = new MovementProbe(false, Vector3.up, vaultCandidate: true,
                    vaultHeight: height, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, height, budget + offset));
                PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), probe, Dt, 1);
                bool accepted = offset <= 0.00001f;
                Assert.That(result.Traversing, Is.EqualTo(accepted), "Distance offset " + offset);
                Assert.That(_state.InputLockSeconds, Is.EqualTo(accepted ? duration * (2f / 3f) : 0f));
                if (accepted) Assert.That(result.Facts, Is.Empty, "Admission is not a successful landing.");
                else
                {
                    Assert.That(result.Facts.Length, Is.EqualTo(1));
                    Assert.That(result.Facts[0].Kind, Is.EqualTo(kind));
                    Assert.That(result.Facts[0].Succeeded, Is.False);
                }
            }
        }

        [TestCase("_vaultDuration", 1f)]
        [TestCase("_mantleDuration", 1.5f)]
        [TestCase("_maxDesignSpeed", 1f)]
        public void TraversalAdmissionRejectsNonFiniteAndNonPositiveOperands(string field, float height)
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1f })
            {
                SetProfileFloat(field, invalid);
                _controller.Reset(new EntityId(1), Vector3.zero, 0f);
                AssertTraversalRejected(new Vector3(0f, height, 1f), height);
            }
        }

        [TestCase("effective speed")]
        [TestCase("budget")]
        [TestCase("budget underflow")]
        [TestCase("horizontal delta")]
        [TestCase("distance")]
        public void TraversalAdmissionRejectsFiniteArithmeticOverflow(string operand)
        {
            Vector3 target = new Vector3(0f, 1f, 1f);
            if (operand == "effective speed")
            {
                SetProfileFloat("_maxDesignSpeed", float.MaxValue);
                SetProfileFloat("_injuredSpeedMultiplier", 2f);
                _controller.Reset(new EntityId(1), Vector3.zero, 0f);
                _controller.ApplyHit(_profile.LungeDamage);
            }
            else if (operand == "budget")
            {
                SetProfileFloat("_maxDesignSpeed", float.MaxValue);
                SetProfileFloat("_vaultDuration", 2f);
                _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            }
            else if (operand == "budget underflow")
            {
                SetProfileFloat("_maxDesignSpeed", float.Epsilon);
                _controller.Reset(new EntityId(1), Vector3.zero, 0f);
                target = Vector3.up;
            }
            else if (operand == "horizontal delta")
            {
                _state.Position = new Vector3(-float.MaxValue, 0f, 0f);
                target = new Vector3(float.MaxValue, 1f, 0f);
            }
            else target = new Vector3(1e20f, 1f, 0f);
            AssertTraversalRejected(target, 1f);
        }

        [TestCase("_vaultMinimumHeight")]
        [TestCase("_mantleMaximumHeight")]
        public void TraversalAdmissionPreservesRejectionOfNaNHeightBounds(string field)
        {
            SetProfileFloat(field, float.NaN);
            AssertTraversalRejected(new Vector3(0f, 1f, 1f), 1f);
        }

        [Test]
        public void InjuryDuringTraversalCapsMotionWithoutExtendingLockOrInventingSuccess()
        {
            // Recovery now raises the injured cap, so this admitted boundary remains reachable.
            ExerciseResolvedTraversal(1f, 0.25f, TraversalKind.Vault, 3.5f, false, 14f, 2, true);
            Assert.That(_profile.VaultDuration, Is.EqualTo(0.25f));
            Assert.That(_profile.MantleDuration, Is.EqualTo(0.35f));
            Assert.That(_profile.HardStumbleDuration, Is.EqualTo(0.5f));
        }

        private void AssertTraversalRejected(Vector3 target, float height)
        {
            var probe = new MovementProbe(false, Vector3.up, vaultCandidate: true,
                vaultHeight: height, vaultClearance: 1.8f, vaultTarget: target);
            PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), probe, Dt, 1);
            Assert.That(result.Traversing, Is.False);
            Assert.That(_state.InputLockSeconds, Is.Zero);
            Assert.That(_state.VaultRemaining, Is.Zero);
            Assert.That(result.Facts.Length, Is.EqualTo(1));
            Assert.That(result.Facts[0].Succeeded, Is.False);
        }

        private void SetProfileFloat(string name, float value)
        {
            var field = typeof(PlayerProfile).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(_profile, value);
        }

        [Test]
        public void MantleAppliesLiveHeadLookAndSteersOnlyItsLastThird()
        {
            SetProfileFloat("_mantleDuration", 0.6f);
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: 1.5f, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, 1.5f, 2f));
            var first = _controller.Tick(Frame(Vector2.right, InputButtons.Jump, look: new Vector2(10f, 3f)), probe, 0.2f, 1);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(10f));
            Assert.That(_state.HeadLookDelta, Is.EqualTo(new Vector2(0f, 3f)));
            Assert.That(first.TraversalOffset, Is.EqualTo(Vector3.zero));
            var second = _controller.Tick(Frame(Vector2.right, look: new Vector2(5f, 4f)), probe, 0.2f, 2);
            Assert.That(second.TraversalOffset.magnitude, Is.LessThan(0.000001f));
            Assert.That(_state.HeadLookDelta.y, Is.EqualTo(4f));
            var final = _controller.Tick(Frame(Vector2.right), probe, 0.2f, 3);
            Assert.That(final.TraversalOffset.x, Is.GreaterThan(0f));
            Assert.That(final.TraversalProgress, Is.EqualTo(1f));
            Assert.That(_state.InputLockSeconds, Is.Zero);
        }

        [TestCase(1f, true)]
        [TestCase(1.001f, false)]
        public void TraversalDurationNeverExceedsOneSecond(float duration, bool admitted)
        {
            SetProfileFloat("_vaultDuration", duration);
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: 1f, vaultClearance: 1.8f, vaultTarget: Vector3.forward);
            Assert.That(_controller.Tick(Frame(pressed: InputButtons.Jump), probe, Dt, 1).Traversing, Is.EqualTo(admitted));
            Assert.That(_state.InputLockSeconds, Is.LessThanOrEqualTo(1f));
        }

        [TestCase(0.1201f, 8f)]
        [TestCase(0.12f, 11f)]
        [TestCase(0.0001f, 11f)]
        [TestCase(0f, 8f)]
        public void FreshTraversalJumpCancelsAndBoostsOnlyInsideTheEndWindow(float remaining, float expectedSpeed)
        {
            _state.MovementState = MovementState.Vault;
            _state.VaultDuration = _profile.MantleDuration;
            _state.VaultRemaining = remaining;
            _state.VaultKind = TraversalKind.Mantle;
            _state.VaultExitVelocity = Vector3.forward * 8f;
            var result = _controller.Tick(Frame(pressed: InputButtons.Jump), default, Dt, 1);
            Assert.That(result.Traversing, Is.False);
            Assert.That(Speed, Is.EqualTo(expectedSpeed).Within(0.0001f));
            Assert.That(_state.Velocity.y, Is.EqualTo(_profile.JumpSpeed - _profile.Gravity * Dt));
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(_state.InputLockSeconds, Is.Zero);
            Assert.That(result.Facts[0].Kind, Is.EqualTo(TraversalKind.Jump));
            Assert.That(_state.JumpBufferRemaining, Is.Zero);
        }

        [Test]
        public void HeldJumpDoesNotBoostAndMissedWindowCompletesNormally()
        {
            _state.Velocity = Vector3.forward * 8f;
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: 1f, vaultClearance: 1.8f, vaultTarget: Vector3.forward);
            _controller.Tick(Frame(pressed: InputButtons.Jump), probe, 0.2f, 1);
            var end = _controller.Tick(Frame(held: InputButtons.Jump), probe, 0.05f, 2);
            Assert.That(end.Traversing, Is.True);
            Assert.That(end.TraversalProgress, Is.EqualTo(1f));
            Assert.That(Speed, Is.EqualTo(8f));
        }

        [TestCase(false, 1f)]
        [TestCase(false, 1.5f)]
        [TestCase(true, 1f)]
        public void VaultMantleAndAutomaticLedgeShareTheBoostAndSpeedCap(bool ledge, float height)
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 13f;
            var probe = new MovementProbe(false, Vector3.up, vaultCandidate: !ledge,
                vaultHeight: height, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, height, 1f));
            _controller.Tick(Frame(pressed: ledge ? InputButtons.None : InputButtons.Jump), probe, Dt, 1);
            for (int tick = 2; _state.VaultRemaining > _profile.TraversalBoostWindow; tick++)
                _controller.Tick(Frame(), probe, Dt, tick);
            _controller.Tick(Frame(pressed: InputButtons.Jump), probe, Dt, 30);
            Assert.That(Speed, Is.EqualTo(_profile.MaxDesignSpeed).Within(0.0001f));
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
        }

        [Test]
        public void BlockedTraversalCancelConsumesPressWithoutDelayedJumpOrBoost()
        {
            _state.Velocity = Vector3.forward * 8f;
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: 1f, vaultClearance: 1.8f, vaultTarget: Vector3.forward);
            _controller.Tick(Frame(pressed: InputButtons.Jump), probe, 0.15f, 1);
            var blocked = new MovementProbe(false, Vector3.up, standingBlocked: true);
            Assert.That(_controller.Tick(Frame(pressed: InputButtons.Jump), blocked, Dt, 2).Traversing, Is.True);
            Assert.That(_state.JumpBufferRemaining, Is.Zero);
            Assert.That(_controller.Tick(Frame(), probe, 0.1f, 3).TraversalProgress, Is.EqualTo(1f));
            Assert.That(Speed, Is.EqualTo(8f));
        }

        [Test]
        public void AirborneUntaggedLedgeGrabsWithoutJumpButAuthoredRouteStillRequiresPress()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 4f + Vector3.down;
            var authored = new MovementProbe(false, Vector3.up, vaultCandidate: true,
                vaultHeight: 1f, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, 1f, 1f));
            Assert.That(_controller.Tick(Frame(), authored, Dt, 1).Traversing, Is.False);
            var ledge = new MovementProbe(false, Vector3.up, vaultHeight: 1f,
                vaultClearance: 1.8f, vaultTarget: new Vector3(0f, 1f, 1f));
            Assert.That(_controller.Tick(Frame(), ledge, Dt, 2).Traversing, Is.True);
            Assert.That(_state.VaultKind, Is.EqualTo(TraversalKind.Mantle));
            _controller.Tick(Frame(pressed: InputButtons.Jump), ledge, Dt, 3);
            Assert.That(_controller.Tick(Frame(), ledge, Dt, 4).Traversing, Is.False, "Cancel cannot immediately regrab.");
        }

        [Test]
        public void StandingJumpHasSmallAirSteeringFloorAndMouseAloneBendsSlide()
        {
            _controller.Tick(Frame(pressed: InputButtons.Jump), Ground, Dt, 1);
            _controller.Tick(Frame(Vector2.right), default, 0.1f, 2);
            Assert.That(Speed, Is.EqualTo(_profile.AirControlSpeedFloor).Within(0.0001f));
            Assert.That(_state.Velocity.x, Is.GreaterThan(0f));
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(pressed: InputButtons.Crouch), Ground, Dt, 1);
            _controller.Tick(Frame(look: new Vector2(30f, 0f)), Ground, 0.1f, 2);
            Assert.That(Vector3.Angle(Vector3.forward, _state.Velocity), Is.EqualTo(10f).Within(0.001f));
            Assert.That(Speed, Is.LessThanOrEqualTo(_profile.MaxDesignSpeed));
        }

        [Test]
        public void FailedVaultCutsSpeedForConfiguredDurationWithoutLockOrRepeatedFact()
        {
            _state.Velocity = Vector3.forward * 8f;
            var blocked = new MovementProbe(true, Vector3.up, vaultCandidate: true, vaultHeight: 1f);
            _controller.Tick(Frame(Vector2.up, InputButtons.Jump, InputButtons.Sprint), blocked, Dt, 1);
            Assert.That(Speed, Is.EqualTo(8f * _profile.StumbleSpeedMultiplier).Within(0.0001f));
            Assert.That(_state.StumbleStartedSeconds, Is.EqualTo(0.3f));
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Stumble));
            _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, 0.299f, 2);
            Assert.That(Speed, Is.LessThanOrEqualTo(4.8001f));
            Assert.That(_state.StumbleStartedSeconds, Is.Zero);
            _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, 0.002f, 3);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
            Assert.That(Speed, Is.GreaterThan(4.8f));
            Assert.That(_state.InputLockSeconds, Is.Zero);
        }

        [Test]
        public void MissedGapKeepsHorizontalMomentumUntilLowerRouteLanding()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(), default, 0.3f, 1);
            Assert.That(Speed, Is.EqualTo(8f));
            Assert.That(_state.Velocity.y, Is.LessThan(0f));
            _controller.CommitPose(new PlayerMoveResult(Vector3.down * 2f, Vector3.forward * 8f, true, false));
            var land = _controller.Tick(Frame(), Ground, Dt, 2);
            Assert.That(_state.Grounded, Is.True);
            Assert.That(Speed, Is.EqualTo(8f));
            Assert.That(land.Facts[0].Kind, Is.EqualTo(TraversalKind.Land));
        }

        [Test]
        public void ScriptedTraversalSteeringCancelSlideAndStumbleReplaysIdenticalCommands()
        {
            var otherState = new PlayerBehaviorState();
            var other = new PlayerController(otherState, _profile, new System.Random(77));
            other.Reset(new EntityId(1), Vector3.zero, 0f);
            var presenter = new PlayerMoverPresenter();
            for (int tick = 0; tick < 90; tick++)
            {
                var probe = tick == 5 || tick == 35 ? new MovementProbe(false, Vector3.up,
                    vaultCandidate: true, vaultHeight: 1.5f, vaultClearance: 1.8f,
                    vaultTarget: _state.Position + new Vector3(0f, 1.5f, 2f))
                    : tick == 65 ? new MovementProbe(true, Vector3.up, vaultCandidate: true, vaultHeight: 1f) : Ground;
                var input = Frame(Vector2.right, tick == 5 || tick == 35 || tick == 51 || tick == 65
                    ? InputButtons.Jump : tick == 62 ? InputButtons.Crouch : InputButtons.None,
                    InputButtons.Sprint, new Vector2(1f, 0.5f));
                var a = _controller.Tick(input, probe, Dt, tick);
                var b = other.Tick(input, probe, Dt, tick);
                Assert.That(b.Displacement, Is.EqualTo(a.Displacement), "Command at " + tick);
                Assert.That(b.Traversing, Is.EqualTo(a.Traversing));
                Assert.That(b.TraversalProgress, Is.EqualTo(a.TraversalProgress));
                Assert.That(b.TraversalOffset, Is.EqualTo(a.TraversalOffset));
                Assert.That(b.Facts, Is.EqualTo(a.Facts));
                _controller.CommitPose(ResolvePureCommand(presenter, a, _state, probe, _controller.MaximumMovementSpeed));
                other.CommitPose(ResolvePureCommand(presenter, b, otherState, probe, other.MaximumMovementSpeed));
                Assert.That(otherState.Position, Is.EqualTo(_state.Position));
                Assert.That(otherState.Velocity, Is.EqualTo(_state.Velocity));
                Assert.That(otherState.VaultProgress, Is.EqualTo(_state.VaultProgress));
                Assert.That(otherState.StumbleStartedSeconds, Is.EqualTo(_state.StumbleStartedSeconds));
            }
        }

        private static PlayerMoveResult ResolvePureCommand(PlayerMoverPresenter presenter, PlayerTickResult command,
            PlayerBehaviorState state, MovementProbe probe, float maximumSpeed)
        {
            Vector3 desired = command.Traversing ? presenter.TraversalPosition(command.TraversalStart, command.TraversalTarget,
                command.TraversalProgress, command.TraversalHeight, 0.08f, 0.25f, 0.95f) + command.TraversalOffset : state.Position + command.Displacement;
            Vector3 displacement = presenter.LimitHorizontalDisplacement(desired - state.Position, maximumSpeed, Dt);
            return new PlayerMoveResult(state.Position + displacement, command.Traversing ? displacement / Dt : state.Velocity,
                !command.Traversing && probe.Grounded && state.Velocity.y <= 0f, false);
        }

        private void ExerciseResolvedTraversal(float height, float duration, TraversalKind kind,
            float distance, bool injured, float entrySpeed, int damageTick, bool expectedSuccess)
        {
            var replayState = new PlayerBehaviorState();
            var replay = new PlayerController(replayState, _profile, new System.Random(77));
            replay.Reset(new EntityId(1), Vector3.zero, 0f);
            _state.Velocity = replayState.Velocity = Vector3.forward * entrySpeed;
            if (injured)
            {
                _controller.ApplyHit(_profile.LungeDamage);
                replay.ApplyHit(_profile.LungeDamage);
            }
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: height, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, height, distance));
            var presenter = new PlayerMoverPresenter();
            int steps = Mathf.RoundToInt(duration / Dt);
            int facts = 0;
            for (int i = 0; i < steps; i++)
            {
                // Apply external damage in the same pre-tick order; InputProbeRecord does not record it.
                if (i == damageTick)
                {
                    _controller.ApplyHit(_profile.LungeDamage);
                    replay.ApplyHit(_profile.LungeDamage);
                }
                InputFrame frame = Frame(pressed: i == 0 ? InputButtons.Jump : InputButtons.None);
                PlayerTickResult result = _controller.Tick(frame, probe, Dt, i);
                Assert.That(result.Traversing, Is.True);
                Assert.That(result.Facts, Is.Empty, "Only the resolved completion may publish an outcome.");
                Vector3 before = _state.Position;
                Vector3 desired = presenter.TraversalPosition(result.TraversalStart, result.TraversalTarget,
                    result.TraversalProgress, result.TraversalHeight, 0.08f, 0.4f, 0.9f);
                Vector3 displacement = presenter.LimitHorizontalDisplacement(desired - before, _controller.MaximumMovementSpeed, Dt);
                Vector3 resolved = before + displacement;
                Vector3 actualVelocity = (resolved - before) / Dt;
                Assert.That(new Vector2(actualVelocity.x, actualVelocity.z).magnitude,
                    Is.LessThanOrEqualTo(_controller.MaximumMovementSpeed + 0.0001f), "Resolved speed at " + i);
                var resolution = new MovementResolution(resolved, actualVelocity, false, false, resolved + Vector3.up);
                var record = new InputProbeRecord(InputProbeRecord.CurrentSchemaVersion, i, frame, probe, Dt, resolution);
                _controller.CommitPose(new PlayerMoveResult(resolved, actualVelocity, false, false));
                _controller.CommitFrame(resolution.EyePosition, record, result.Facts);
                Assert.That(_state.InputLockSeconds, Is.EqualTo(Mathf.Max(0f, duration * (2f / 3f) - i * Dt)).Within(0.000001f));
                Assert.That(_state.LastTraversalFacts.Count, Is.EqualTo(i == steps - 1 ? 1 : 0));
                foreach (PlayerTraversalFact fact in _state.LastTraversalFacts)
                {
                    facts++;
                    Assert.That(fact.Kind, Is.EqualTo(kind));
                    Assert.That(fact.Succeeded, Is.EqualTo(expectedSuccess));
                }
                replay.Replay(record);
                Assert.That(replayState.Position, Is.EqualTo(_state.Position), "Replay position at " + i);
                Assert.That(replayState.Velocity, Is.EqualTo(_state.Velocity), "Replay velocity at " + i);
                Assert.That(replayState.MovementState, Is.EqualTo(_state.MovementState));
                Assert.That(replayState.Health, Is.EqualTo(_state.Health));
                Assert.That(replayState.InputLockSeconds, Is.EqualTo(_state.InputLockSeconds));
                Assert.That(replayState.LastTraversalFacts, Is.EqualTo(_state.LastTraversalFacts));
            }
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(_state.VaultRemaining, Is.Zero);
            Assert.That(Speed, Is.EqualTo(Mathf.Min(entrySpeed, _controller.MaximumMovementSpeed)).Within(0.0001f));
            Assert.That(Vector3.Distance(_state.Position, probe.VaultTarget) <= _profile.VaultCompletionTolerance,
                Is.EqualTo(expectedSuccess));
            Assert.That(facts, Is.EqualTo(1));
            Assert.That(_controller.Tick(Frame(), default, Dt, steps).Traversing, Is.False);
            Assert.That(_state.InputLockSeconds, Is.Zero);
            Assert.That(_state.LastTraversalFacts, Is.Empty);
        }

        [Test]
        public void CollisionBlockedVaultReportsFailureAtCompletion()
        {
            var probe = new MovementProbe(true, Vector3.up, vaultCandidate: true,
                vaultHeight: 1f, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, 1f, 2f));
            PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), probe, 0.25f, 1);
            _controller.CommitPose(new PlayerMoveResult(Vector3.zero, Vector3.zero, true, false));
            _controller.CommitFrame(Vector3.up, new InputProbeRecord(1, 1, default, probe, 0.25f), result.Facts);
            Assert.That(_state.LastTraversalFacts.Count, Is.EqualTo(1));
            Assert.That(_state.LastTraversalFacts[0].Succeeded, Is.False);
        }

        [Test]
        public void HealthThresholdsClampSpeedAndDeathIsIdempotent()
        {
            _state.Velocity = Vector3.forward * 14f;
            _state.VaultExitVelocity = Vector3.forward * 14f;
            PlayerHitResult hit = _controller.ApplyHit(_profile.LungeDamage);
            Assert.That(_state.Health, Is.EqualTo(50f));
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Injured));
            Assert.That(Speed, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(_state.VaultExitVelocity.magnitude, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(hit.Changed, Is.True);
            Assert.That(hit.Died, Is.False);
            _controller.AdvanceRecovery(_state.GraceWindow.EndTick);
            Assert.That(Speed, Is.EqualTo(13.3f).Within(0.0001f));
            Assert.That(_state.VaultExitVelocity.magnitude, Is.EqualTo(13.3f).Within(0.0001f));
            _controller.ApplyHit(25f);
            Assert.That(_state.Health, Is.EqualTo(_profile.CriticalThreshold));
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Critical));
            _controller.AdvanceRecovery(_state.GraceWindow.EndTick);
            hit = _controller.ApplyHit(25f);
            Assert.That(hit.Died, Is.True);
            Assert.That(_state.IsAlive, Is.False);
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Dead));
            Assert.That(_controller.ApplyHit(50f).Changed, Is.False);
            Assert.That(_controller.ApplyHit(float.NaN).Changed, Is.False);
            Assert.That(_controller.ApplyHit(-50f).Changed, Is.False);
        }

        [Test]
        public void GraceStartsOnAcceptedHitAndEndsExclusivelyWithoutRestacking()
        {
            _controller.Tick(Frame(), Ground, Dt, 10);
            PlayerHitResult accepted = _controller.ApplyHit(10f, HitSeverity.Light);
            GraceWindowFact window = accepted.GraceStarted.Value;
            Assert.That(accepted.Changed, Is.True);
            Assert.That(window.PlayerId, Is.EqualTo(_state.Id));
            Assert.That(window.StartTick, Is.EqualTo(10));
            Assert.That(window.EndTick, Is.EqualTo(82));
            Assert.That(window.Severity, Is.EqualTo(HitSeverity.Light));
            long boostEnd = _state.HitBoostEndTick;
            foreach (long tick in new[] { 10L, 11L, 81L })
            {
                _controller.Tick(Frame(), Ground, Dt, tick);
                PlayerHitResult absorbed = _controller.ApplyHit(100f, HitSeverity.Heavy);
                Assert.That(absorbed.AbsorbedByGrace, Is.True);
                Assert.That(absorbed.Changed || absorbed.Died || absorbed.GraceStarted.HasValue, Is.False);
                Assert.That(_state.Health, Is.EqualTo(90f));
                Assert.That(_state.HitBoostEndTick, Is.EqualTo(boostEnd));
                Assert.That(_state.GraceWindow, Is.EqualTo(window));
            }
            Assert.That(_controller.AdvanceRecovery(window.EndTick).Value, Is.EqualTo(window));
            Assert.That(_controller.AdvanceRecovery(window.EndTick), Is.Null, "End publishes once.");
            Assert.That(_controller.ApplyHit(10f).Changed, Is.True, "The end tick is outside grace.");
            Assert.That(_state.Health, Is.EqualTo(80f));
        }

        [TestCase(HitSeverity.Light, 1.12f, 36)]
        [TestCase(HitSeverity.Heavy, 1.25f, 72)]
        public void HitBoostScalesTargetsAndCapUntilItsExclusiveEnd(HitSeverity severity, float multiplier, int durationTicks)
        {
            _controller.ApplyHit(1f, severity);
            Assert.That(_state.HitBoostEndTick, Is.EqualTo(durationTicks));
            Assert.That(_controller.MaximumMovementSpeed, Is.EqualTo(_profile.MaxDesignSpeed * multiplier).Within(0.0001f));
            for (int tick = 1; tick < durationTicks; tick++)
                _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, Dt, tick);
            Assert.That(Speed, Is.EqualTo(_profile.SprintSpeed * multiplier).Within(0.0001f));
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(multiplier));
            Assert.That(_controller.ApplyHit(1f).AbsorbedByGrace, Is.True);
            Assert.That(_state.HitBoostEndTick, Is.EqualTo(durationTicks));
            _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, Dt, durationTicks);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
            Assert.That(_controller.MaximumMovementSpeed, Is.EqualTo(_profile.MaxDesignSpeed));
            Assert.That(Speed, Is.LessThan(_profile.SprintSpeed * multiplier));
        }

        [Test]
        public void LightBoostExpiresWhileGraceStillAbsorbsHeavyHits()
        {
            _controller.ApplyHit(1f, HitSeverity.Light);
            _controller.AdvanceRecovery(36);
            Assert.That(_state.GraceActive, Is.True);
            Assert.That(_controller.ApplyHit(50f, HitSeverity.Heavy).AbsorbedByGrace, Is.True);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
            Assert.That(_state.HitBoostEndTick, Is.EqualTo(36));
        }

        [Test]
        public void RecoveryUsesInjectedTickRateAndCancellationAndResetClearIt()
        {
            _controller.Reset(new EntityId(1), Vector3.zero, 0f, 0.1f);
            _controller.ApplyHit(1f);
            Assert.That(_state.GraceWindow.EndTick, Is.EqualTo(12));
            Assert.That(_controller.EndRecovery().Value.EndTick, Is.Zero);
            Assert.That(_controller.EndRecovery(), Is.Null);
            Assert.That(_state.GraceActive, Is.False);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
            _controller.ApplyHit(1f);
            _controller.SetLookBackEnabled(false);
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(_state.GraceActive, Is.False);
            Assert.That(_state.HitBoostEndTick, Is.Zero);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
            Assert.That(_state.LookBackEnabled, Is.True);
        }

        [TestCase(0.1f, 1.2f, 12)]
        [TestCase(0.02f, 1.2f, 60)]
        [TestCase(0.1f, 1.20001f, 13)]
        [TestCase(0.1f, 1.25f, 13)]
        [TestCase(0.1f, 0.000001f, 1)]
        [TestCase(0.1f, 0f, 0)]
        public void RecoveryRoundingOnlySnapsFloatErrorAtIntegralBoundaries(float step, float seconds, long ticks)
        {
            SetProfileFloat("_hitGraceSeconds", seconds);
            SetProfileFloat("_heavyHitBoostSeconds", seconds);
            _controller.Reset(new EntityId(1), Vector3.zero, 0f, step);
            _controller.AdvanceRecovery(7);
            _controller.ApplyHit(1f);
            Assert.That(_state.GraceWindow.EndTick, Is.EqualTo(7 + ticks));
            Assert.That(_state.HitBoostEndTick, Is.EqualTo(7 + ticks));
            if (ticks > 0) Assert.That(_controller.AdvanceRecovery(7 + ticks - 1), Is.Null);
            _controller.AdvanceRecovery(7 + ticks);
            Assert.That(_state.GraceActive, Is.False);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidDamageDoesNotStartRecoveryAndLethalHitHasNoLingeringWindow()
        {
            foreach (float damage in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.That(_controller.ApplyHit(damage).GraceStarted, Is.Null);
            Assert.That(_state.GraceActive, Is.False);
            PlayerHitResult fatal = _controller.ApplyHit(_profile.MaximumHealth);
            Assert.That(fatal.Died, Is.True);
            Assert.That(fatal.GraceStarted.Value.EndTick, Is.EqualTo(fatal.GraceStarted.Value.StartTick));
            Assert.That(_state.GraceActive, Is.False);
            Assert.That(_state.HitBoostMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DisabledSnapLeavesBodyAndHeadLookNormalAndMovementFactPublishesTheDecision()
        {
            _controller.SetLookBackEnabled(false);
            var held = Frame(held: InputButtons.LookBack, look: new Vector2(20f, 3f));
            CommitAction(held, Ground, Vector3.zero, true, 1);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(20f));
            Assert.That(_state.HeadLookDelta, Is.EqualTo(new Vector2(0f, 3f)));
            Assert.That(_state.LastMovementSample.LookBack, Is.False);
            _controller.SetLookBackEnabled(true);
            CommitAction(held, Ground, Vector3.zero, true, 2);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(40f));
            Assert.That(_state.LastMovementSample.LookBack, Is.True);
            Assert.That(_state.LastMovementSample.HeadLookDelta, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RunModifiersClampHealthAndApplySpeedWithoutMutatingProfile()
        {
            _controller.SetHealthRecoveryEffects(0f); // Isolate injury speed from passive healing.
            _controller.ApplyRunModifiers(200f, 80f, 1.25f);
            Assert.That(_state.Health, Is.EqualTo(80f));
            Assert.That(_state.MaxHealth, Is.EqualTo(80f));
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Healthy));
            for (int tick = 0; tick < 60; tick++) _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, Dt, tick);
            Assert.That(Speed, Is.EqualTo(_profile.SprintSpeed * 1.25f).Within(0.0001f));
            _controller.ApplyRunModifiers(40f, 80f, 0.8f);
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Injured));
            for (int tick = 0; tick < 60; tick++) _controller.Tick(Frame(Vector2.up), Ground, Dt, tick + 60);
            Assert.That(Speed, Is.EqualTo(_profile.WalkSpeed * 0.8f * _profile.InjuredSpeedMultiplier).Within(0.0001f));
            Assert.That(_profile.MaximumHealth, Is.EqualTo(100f));
            Assert.That(_profile.SprintSpeed, Is.EqualTo(8f));
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(_state.Health, Is.EqualTo(_profile.MaximumHealth));
            Assert.That(_state.MaxHealth, Is.EqualTo(_profile.MaximumHealth));
            Assert.That(_state.MovementSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(_state.SprintSpeed, Is.EqualTo(_profile.SprintSpeed));
        }

        [Test]
        public void InvalidRunModifierDoesNotPartiallyChangePlayerState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ApplyRunModifiers(70f, 80f, float.NaN));
            Assert.That(_state.Health, Is.EqualTo(100f));
            Assert.That(_state.MaxHealth, Is.EqualTo(100f));
            Assert.That(_state.MovementSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ResetClearsAllPreviousLifeTimersHealthNoiseAndInventory()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch, InputButtons.LookBack), Ground, Dt, 1);
            _controller.ApplyHit(100f);
            _state.LastReboundWall = 42;
            _state.VaultRemaining = 0.2f;
            _state.Inventory = new InventorySnapshot("old", "old");
            _controller.Reset(new EntityId(9), new Vector3(2f, 3f, 4f), 90f);
            Assert.That(_state.Health, Is.EqualTo(100f));
            Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(_state.RecentNoises, Is.Empty);
            Assert.That(_state.LookBack, Is.False);
            Assert.That(_state.LastReboundWall, Is.Zero);
            Assert.That(_state.VaultRemaining, Is.Zero);
            Assert.That(_state.SlideRemaining, Is.Zero);
            Assert.That(_state.Inventory.SlotOne, Is.Empty);
            Assert.That(_state.Inventory.SlotTwo, Is.Empty);
            Assert.That(_state.Id, Is.EqualTo(new EntityId(9)));
        }

        [Test]
        public void NoiseHistoryIsBoundedChronologicalAndTickStamped()
        {
            for (int i = 1; i <= 40; i++)
            {
                _state.Velocity = Vector3.forward * 8f;
                _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, 0.4f, i);
            }
            Assert.That(_state.RecentNoises.Count, Is.EqualTo(_profile.NoiseCapacity));
            Assert.That(_state.RecentNoises[0].Tick, Is.EqualTo(25));
            Assert.That(_state.RecentNoises[15].Tick, Is.EqualTo(40));
            Assert.That(_state.RecentNoises[0].Source, Is.EqualTo(_state.Id));
        }

        [Test]
        public void RegenerationWaitsAfterHitAndHealsOnlyTheRemainderOfABoundaryTick()
        {
            Assert.That(_profile.HealthRegenerationPerSecond, Is.EqualTo(1.5f));
            Assert.That(_profile.HealthRegenerationDelay, Is.EqualTo(4f));
            _controller.ApplyHit(50f);
            _controller.Tick(Frame(), Ground, 3.75f, 225);
            Assert.That(_state.Health, Is.EqualTo(50f));
            _controller.Tick(Frame(), Ground, 0.5f, 255);
            Assert.That(_state.Health, Is.EqualTo(50.375f));
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Healthy));
            _controller.Tick(Frame(), Ground, 1f, 315);
            Assert.That(_state.Health, Is.EqualTo(51.875f));
        }

        [Test]
        public void AcceptedHitsRestartDelayButAbsorbedAndInvalidHitsDoNot()
        {
            _controller.ApplyHit(10f);
            _controller.Tick(Frame(), Ground, 0.5f, 30);
            Assert.That(_controller.ApplyHit(10f).AbsorbedByGrace, Is.True);
            _controller.ApplyHit(float.NaN);
            Assert.That(_state.RegenerationDelayRemaining, Is.EqualTo(3.5d));
            _controller.Tick(Frame(), Ground, 1f, 90);
            Assert.That(_controller.ApplyHit(10f).Changed, Is.True);
            _controller.Tick(Frame(), Ground, 4f, 330);
            Assert.That(_state.Health, Is.EqualTo(80f));
            _controller.Tick(Frame(), Ground, 0.5f, 360);
            Assert.That(_state.Health, Is.EqualTo(80.75f));
        }

        [Test]
        public void RegenerationUsesProfileRateCapsAtEffectiveMaximumAndNeverRevives()
        {
            SetProfileFloat("_healthRegenerationPerSecond", 3f);
            SetProfileFloat("_healthRegenerationDelay", 2f);
            _controller.BeginFloorHealth(80f, 1f);
            _controller.ApplyHit(10f);
            _controller.Tick(Frame(), Ground, 3f, 180);
            Assert.That(_state.Health, Is.EqualTo(73f));
            _controller.Tick(Frame(), Ground, 10f, 780);
            Assert.That(_state.Health, Is.EqualTo(80f));
            _controller.ApplyHit(80f);
            _controller.Tick(Frame(), Ground, 100f, 6780);
            Assert.That(_state.Health, Is.Zero);
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Dead));
        }

        [TestCase(0f, 50f)]
        [TestCase(0.5f, 51.5f)]
        [TestCase(1f, 53f)]
        public void HealthHooksScaleRegenerationWithoutChangingTheProfile(float multiplier, float expected)
        {
            _controller.SetHealthRecoveryEffects(multiplier);
            _controller.ApplyHit(50f);
            _controller.Tick(Frame(), Ground, 6f, 360);
            Assert.That(_state.Health, Is.EqualTo(expected));
            Assert.That(_profile.HealthRegenerationPerSecond, Is.EqualTo(1.5f));
        }

        [Test]
        public void FloorHealthUsesEffectiveMaximumAndHooksDefaultNeutralAfterLifeReset()
        {
            _controller.ApplyHit(60f);
            _controller.BeginFloorHealth(80f, 1.2f);
            Assert.That(_state.Health, Is.EqualTo(80f));
            Assert.That(_state.RegenerationDelayRemaining, Is.Zero);
            Assert.That(_state.MovementSpeedMultiplier, Is.EqualTo(1.2f));
            _controller.SetHealthRecoveryEffects(0f, 0.5f);
            Assert.That(_state.Health, Is.EqualTo(80f), "Setting hooks is not itself a floor start.");
            _controller.BeginFloorHealth(60f, 1f);
            Assert.That(_state.Health, Is.EqualTo(30f));
            Assert.That(_state.MaxHealth, Is.EqualTo(60f));
            _controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(_state.RegenerationMultiplier, Is.EqualTo(1f));
            Assert.That(_state.FloorStartHealthFraction, Is.EqualTo(1f));
            Assert.That(_state.RegenerationDelayRemaining, Is.Zero);
            _controller.BeginFloorHealth(120f, 1f);
            Assert.That(_state.Health, Is.EqualTo(120f));
            Assert.That(_profile.MaximumHealth, Is.EqualTo(100f));
        }

        [TestCase(float.NaN, 1f)] [TestCase(-1f, 1f)] [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(1f, 0f)] [TestCase(1f, 1.1f)] [TestCase(1f, float.NaN)]
        public void InvalidHealthHooksDoNotPartiallyChangeState(float regeneration, float fraction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _controller.SetHealthRecoveryEffects(regeneration, fraction));
            Assert.That(_state.RegenerationMultiplier, Is.EqualTo(1f));
            Assert.That(_state.FloorStartHealthFraction, Is.EqualTo(1f));
        }

        [TestCase(false)] [TestCase(true)]
        public void HoldingSlideChangesNeitherPostureNorWalkingOrSprintingSpeedNoiseOrCadence(bool sprint)
        {
            var uprightState = new PlayerBehaviorState();
            var upright = new PlayerController(uprightState, _profile, new System.Random(77));
            upright.Reset(new EntityId(1), Vector3.zero, 0f);
            InputButtons held = sprint ? InputButtons.Sprint : InputButtons.None;
            for (int tick = 1; tick <= 120; tick++)
            {
                upright.Tick(Frame(Vector2.up, held: held), Ground, Dt, tick);
                _controller.Tick(Frame(Vector2.up, held: held | InputButtons.Crouch), Ground, Dt, tick);
                Assert.That(_state.Crouched, Is.False);
                Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
                Assert.That(_state.Velocity, Is.EqualTo(uprightState.Velocity));
                Assert.That(_state.RecentNoises, Is.EqualTo(uprightState.RecentNoises));
            }
            Assert.That(Speed, Is.EqualTo(sprint ? _profile.SprintSpeed : _profile.WalkSpeed));
            Assert.That(_state.RecentNoises.Count, Is.GreaterThan(1));
        }

        [TestCase("walk", NoiseSourceKind.Footstep)] [TestCase("sprint", NoiseSourceKind.Footstep)]
        [TestCase("landing", NoiseSourceKind.Landing)] [TestCase("slide", NoiseSourceKind.Slide)]
        [TestCase("vault", NoiseSourceKind.Vault)] [TestCase("mantle", NoiseSourceKind.Vault)]
        [TestCase("ledge", NoiseSourceKind.Vault)] [TestCase("rebound", NoiseSourceKind.Rebound)]
        public void EveryNoiseEmissionCarriesItsSourceKind(string action, NoiseSourceKind expected)
        {
            MovementProbe probe = Ground;
            InputFrame frame = Frame(Vector2.up);
            float loudness = _profile.TraversalLoudness;
            _state.Velocity = Vector3.forward * 8f;
            if (action == "walk" || action == "sprint")
            {
                frame = Frame(Vector2.up, held: action == "sprint" ? InputButtons.Sprint : InputButtons.None);
                loudness = action == "sprint" ? _profile.SprintLoudness : _profile.WalkingLoudness;
            }
            else if (action == "slide") { frame = Frame(pressed: InputButtons.Crouch); loudness = _profile.SlideLoudness; }
            else if (action == "landing")
            {
                frame = Frame(); // Do not emit a separate walking step on the landing tick.
                _state.MovementState = MovementState.Air;
                _state.Velocity = Vector3.down;
            }
            else if (action == "rebound")
            {
                _state.MovementState = MovementState.Air;
                probe = new MovementProbe(false, Vector3.up, true, 0.5f, Vector3.back, 0f, 1);
                frame = Frame(pressed: InputButtons.Jump);
            }
            else
            {
                _state.MovementState = MovementState.Air;
                float height = action == "mantle" ? 1.5f : 1f;
                probe = new MovementProbe(false, Vector3.up, vaultCandidate: action != "ledge",
                    vaultHeight: height, vaultClearance: 1.8f, vaultTarget: new Vector3(0f, height, 1f));
                frame = Frame(pressed: action == "ledge" ? InputButtons.None : InputButtons.Jump);
            }
            _controller.Tick(frame, probe, Dt, 7);
            Assert.That(_state.RecentNoises.Count, Is.EqualTo(1));
            NoiseEvent noise = _state.RecentNoises[0];
            Assert.That(noise.SourceKind, Is.EqualTo(expected));
            Assert.That(noise.Loudness, Is.EqualTo(loudness));
            Assert.That(noise.Source, Is.EqualTo(_state.Id));
            Assert.That(noise.Tick, Is.EqualTo(7));
            Assert.That(noise.Position, Is.EqualTo(_state.Position));
        }

        [Test]
        public void RecordedFramesProbesAndCollisionResolutionsReplayTheCommittedTrajectory()
        {
            var records = new List<InputProbeRecord>();
            var positions = new List<Vector3>();
            var velocities = new List<Vector3>();
            var states = new List<MovementState>();
            for (int i = 0; i < 60; i++)
            {
                InputFrame input = Frame(Vector2.up, i == 10 ? InputButtons.Jump : InputButtons.None,
                    i > 40 ? InputButtons.LookBack : InputButtons.None, new Vector2(0.2f, 0f));
                MovementProbe probe = i > 10 && i < 26 ? default : Ground;
                PlayerTickResult result = _controller.Tick(input, probe, Dt, i);
                Vector3 position = _state.Position + result.Displacement;
                Vector3 velocity = _state.Velocity;
                // Recorded wall collision arrests horizontal motion; landing resolves the floor height.
                if (i == 15)
                {
                    position.x = _state.Position.x; position.z = _state.Position.z;
                    velocity.x = velocity.z = 0f;
                }
                bool grounded = probe.Grounded && velocity.y <= 0f;
                if (grounded) { position.y = 0f; velocity.y = 0f; }
                var resolution = new MovementResolution(position, velocity, grounded, false, position + Vector3.up * 1.6f);
                var record = new InputProbeRecord(1, i, input, probe, Dt, resolution);
                _controller.CommitPose(new PlayerMoveResult(position, velocity, grounded, false));
                _controller.CommitFrame(resolution.EyePosition, record, result.Facts);
                records.Add(record); positions.Add(_state.Position); velocities.Add(_state.Velocity); states.Add(_state.MovementState);
            }
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            for (int i = 0; i < records.Count; i++)
            {
                _controller.Replay(records[i]);
                Assert.That(_state.Position, Is.EqualTo(positions[i]), "position at " + i);
                Assert.That(_state.Velocity, Is.EqualTo(velocities[i]), "velocity at " + i);
                Assert.That(_state.MovementState, Is.EqualTo(states[i]), "movement at " + i);
            }
            Assert.That(positions[positions.Count - 1].sqrMagnitude, Is.GreaterThan(1f));
            Assert.Throws<ArgumentException>(() => _controller.Replay(new InputProbeRecord(1, 100, default, default)));
        }
    }
}
