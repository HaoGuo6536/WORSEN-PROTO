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
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
//   - Verify traversal admission, fixed locks and outcomes under the per-tick speed cap.
//   - Verify hold-to-sprint, uphill landing recovery and clearance-safe slide cancellation.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Edit Mode NUnit tests. A default profile supplies actual prototype tuning; frames, probes and delta time are explicit.
//   Pure traversal resolution has no obstacles; live collision coverage belongs to PlayerDriverTests.
//   Damage is supplied separately at matching replay ticks because input records do not encode hits.
//   Reset after changing profile speed caps so arithmetic tests use the next life's copied values.
//   No other Domain system or Presentation system is referenced.
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
        public void DefaultGroundMovementWalksAndSprintHoldRuns()
        {
            for (int i = 0; i < 60; i++) _controller.Tick(Frame(Vector2.up), Ground, Dt, i);
            Assert.That(Speed, Is.EqualTo(_profile.WalkSpeed).Within(0.0001f));
            for (int i = 0; i < 60; i++) _controller.Tick(Frame(Vector2.up, held: InputButtons.Sprint), Ground, Dt, i + 60);
            Assert.That(Speed, Is.EqualTo(_profile.SprintSpeed).Within(0.0001f));
            for (int i = 0; i < 60; i++) _controller.Tick(Frame(Vector2.up), Ground, Dt, i + 120);
            Assert.That(Speed, Is.EqualTo(_profile.WalkSpeed).Within(0.0001f));
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
        public void BlockedSlideCancellationStopsPropulsionWithoutStandingOrDelayedJump()
        {
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.up, InputButtons.Crouch), Ground, Dt, 1);
            float slidingSpeed = Speed;
            var blocked = new MovementProbe(true, Vector3.up, standingBlocked: true);
            PlayerTickResult cancelled = _controller.Tick(Frame(pressed: InputButtons.Jump), blocked, Dt, 2);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
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
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Ground));
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
            Assert.That(_state.HeadingDegrees, Is.Zero);
            Assert.That(_state.HeadLookDelta, Is.EqualTo(new Vector2(30f, 5f)));
            Assert.That(_state.LookBack, Is.True);
            Assert.That(_state.MovementState, Is.EqualTo(MovementState.Air));
            _controller.Tick(Frame(look: new Vector2(10f, 0f)), default, Dt, 2);
            Assert.That(_state.HeadingDegrees, Is.EqualTo(10f));
            Assert.That(_state.LookBack, Is.False);
        }

        [Test]
        public void LookBackReducesLateralAirAuthorityButKeepsForwardInput()
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.right, held: InputButtons.LookBack), default, 0.1f, 1);
            float reduced = _state.Velocity.x;
            _controller.Reset(new EntityId(1), Vector3.zero, 0f);
            _state.MovementState = MovementState.Air;
            _state.Velocity = Vector3.forward * 8f;
            _controller.Tick(Frame(Vector2.right), default, 0.1f, 1);
            Assert.That(reduced, Is.GreaterThan(0f).And.LessThan(_state.Velocity.x * 0.4f));
        }

        [TestCase(11.999f, 10f, MovementState.Ground, 0f)]
        [TestCase(12f, 6f, MovementState.Stumble, 0.2f)]
        [TestCase(18f, 6f, MovementState.Stumble, 0.2f)]
        [TestCase(18.001f, 3f, MovementState.Stumble, 0.5f)]
        public void LandingThresholdsRetainMomentumWithoutLock(float impact, float speed, MovementState movement, float stumble)
        {
            _state.MovementState = MovementState.Air;
            _state.Velocity = new Vector3(0f, -impact, 10f);
            _controller.Tick(Frame(), Ground, Dt, 1);
            Assert.That(Speed, Is.EqualTo(speed).Within(0.0001f));
            Assert.That(_state.MovementState, Is.EqualTo(movement));
            Assert.That(_state.StumbleRemaining, Is.EqualTo(stumble));
            Assert.That(_state.InputLockSeconds, Is.Zero);
            _controller.Tick(Frame(pressed: InputButtons.Jump), Ground, Dt, 2);
            Assert.That(_state.Velocity.y, Is.GreaterThan(0f));
        }

        [TestCase(0.6f, 45f, true)]
        [TestCase(0.601f, 45f, false)]
        [TestCase(0.6f, 45.001f, false)]
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
            PlayerTickResult result = _controller.Tick(Frame(pressed: InputButtons.Jump), other, 0.1f, 2);
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
                Assert.That(_state.InputLockSeconds, Is.EqualTo(accepted ? duration : 0f));
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
            // This healthy boundary is unreachable after a hit at tick 2 lowers the remaining budget.
            ExerciseResolvedTraversal(1f, 0.25f, TraversalKind.Vault, 3.5f, false, 14f, 2, false);
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
                Assert.That(_state.InputLockSeconds, Is.EqualTo(duration - i * Dt).Within(0.000001f));
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
            Assert.That(Speed, Is.EqualTo(13.3f).Within(0.0001f));
            Assert.That(_state.VaultExitVelocity.magnitude, Is.EqualTo(13.3f).Within(0.0001f));
            Assert.That(hit.Changed, Is.True);
            Assert.That(hit.Died, Is.False);
            _controller.ApplyHit(25f);
            Assert.That(_state.Health, Is.EqualTo(_profile.CriticalThreshold));
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Critical));
            hit = _controller.ApplyHit(25f);
            Assert.That(hit.Died, Is.True);
            Assert.That(_state.IsAlive, Is.False);
            Assert.That(_state.HealthState, Is.EqualTo(PlayerHealthState.Dead));
            Assert.That(_controller.ApplyHit(50f).Changed, Is.False);
            Assert.That(_controller.ApplyHit(float.NaN).Changed, Is.False);
            Assert.That(_controller.ApplyHit(-50f).Changed, Is.False);
        }

        [Test]
        public void RunModifiersClampHealthAndApplySpeedWithoutMutatingProfile()
        {
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
