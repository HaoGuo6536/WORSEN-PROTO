// ============================================================================
// PlayerTraversalLaunchTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the owner's 2026-10-01 knee vault, airborne grab and buffered launch
//   requirements through the real pure movement tick and committed resolutions.
//   Managed profile data permits headless execution without a Unity process.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Verify untagged admission, wall-jump precedence and failed-grab momentum.
//   - Verify early, timely, late and held traversal jump input with resolved completion.
//   - Verify momentum decay after landing, collision authority and speed caps.
//   - Verify live look, last-third steering, pooled reset and deterministic replay.
// DEPENDENCIES:
//   - Player Controller/state/profile/presenter, Core movement values and NUnit.
// USAGE NOTES:
//   Explicit provisional fixture values mirror profile defaults. The uninitialized
//   SO is managed data only, never passed to Unity APIs or destroyed. DriverTests
//   separately exercise real collider probes; these supplied probes are not PhysX evidence.
//   Completion grace ages before a fresh press; buffered traversal input is distinct.
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
    public sealed class PlayerTraversalLaunchTests
    {
        private const float Dt = 1f / 60f;
        private PlayerProfile profile;
        private PlayerBehaviorState state;
        private PlayerController controller;
        private readonly PlayerMoverPresenter presenter = new PlayerMoverPresenter();
        private long tick;
        private static readonly MovementProbe Ground = new MovementProbe(true, Vector3.up);

        [SetUp]
        public void Setup()
        {
            profile = (PlayerProfile)FormatterServices.GetUninitializedObject(typeof(PlayerProfile));
            Tune("_maximumHealth", 100f); Tune("_sprintSpeed", 8f); Tune("_walkSpeed", 4f);
            Tune("_maxDesignSpeed", 14f); Tune("_maximumExternalMotionSpeed", 14f);
            Tune("_groundAcceleration", 60f); Tune("_groundFriction", 70f);
            Tune("_jumpSpeed", 5.5f); Tune("_jumpBuffer", .1f); Tune("_coyoteTime", .1f);
            Tune("_airAcceleration", 25f); Tune("_airControlSpeedFloor", 2f); Tune("_gravity", 18f);
            Tune("_reboundDistance", .6f); Tune("_reboundJumpWindow", .15f);
            Tune("_reboundUpwardBoost", 3f); Tune("_reboundCooldown", .4f); Tune("_wallJumpOutwardRatio", .5f);
            Tune("_vaultMinimumHeight", .35f); Tune("_vaultMaximumHeight", 1.2f); Tune("_mantleMaximumHeight", 2f);
            Tune("_vaultDuration", .25f); Tune("_mantleDuration", .35f); Tune("_vaultCompletionTolerance", .05f);
            Tune("_traversalBoostWindow", .22f);
            Tune("_traversalBoostSpeed", 6f); Tune("_traversalBoostUpwardSpeed", 3f);
            Tune("_traversalMomentumDuration", .5f); Tune("_traversalSteeringSpeed", 2f);
            Tune("_ledgeReach", 1.2f); Tune("_ledgeMinimumHeight", .5f); Tune("_ledgeMaximumHeight", 2.2f);
            Tune("_ledgeRegrabDelay", .2f); Tune("_failedVaultStumbleDuration", .3f); Tune("_stumbleSpeedMultiplier", .6f);
            Tune("_softLandingThreshold", 12f); Tune("_hardLandingThreshold", 18f);
            Tune("_softLandingRetention", .6f); Tune("_hardLandingRetention", .3f);
            Tune("_injuredThreshold", 50f); Tune("_criticalThreshold", 25f); Tune("_injuredSpeedMultiplier", .95f);
            state = new PlayerBehaviorState();
            controller = new PlayerController(state, profile, new System.Random(13));
            controller.Reset(new EntityId(1), Vector3.zero, 0f);
            state.Velocity = Vector3.forward * 8f;
            tick = 0;
        }

        private void Tune(string field, float value)
            => typeof(PlayerProfile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, value);
        private static InputFrame Frame(bool press = false, Vector2 move = default, Vector2 look = default, bool held = false)
            => new InputFrame(move, look, InputButtons.Sprint | (held ? InputButtons.Jump : InputButtons.None),
                press ? InputButtons.Jump : InputButtons.None, InputButtons.None);
        private static MovementProbe Edge(float height, bool ground = false, bool tagged = false, bool wall = false, float clearance = 1.8f)
            => new MovementProbe(ground, Vector3.up, wall, .3f, Vector3.back, 0f, wall ? 10 : 0,
                tagged, height, clearance, new Vector3(0f, height, .8f));
        private float Speed => new Vector2(state.Velocity.x, state.Velocity.z).magnitude;
        private PlayerTickResult Step(InputFrame frame, MovementProbe probe, float dt = Dt, bool resolve = true)
        {
            PlayerTickResult result = controller.Tick(frame, probe, dt, ++tick);
            if (resolve) Commit(result, frame, probe, dt);
            return result;
        }
        private InputProbeRecord Commit(PlayerTickResult result, InputFrame frame, MovementProbe probe, float dt)
        {
            Vector3 position = result.Traversing ? (state.VaultIsLedge
                ? presenter.LedgePosition(result.TraversalStart, result.TraversalTarget, result.TraversalProgress, .08f, .55f)
                : presenter.TraversalPosition(result.TraversalStart, result.TraversalTarget, result.TraversalProgress,
                    result.TraversalHeight, .08f, .25f, .95f)) + result.TraversalOffset : state.Position + result.Displacement;
            Vector3 velocity = result.Traversing ? (position - state.Position) / dt : state.Velocity;
            bool grounded = result.Traversing ? result.TraversalProgress >= 1f : probe.Grounded && velocity.y <= 0f;
            controller.CommitPose(new PlayerMoveResult(position, velocity, grounded, false));
            var resolution = new MovementResolution(position, velocity, grounded, false, position + Vector3.up * 1.6f);
            var record = new InputProbeRecord(InputProbeRecord.CurrentSchemaVersion, tick, frame, probe, dt, resolution);
            controller.CommitFrame(resolution.EyePosition, record, result.Facts);
            return record;
        }
        private void Finish()
        {
            for (int i = 0; i < 60 && state.MovementState == MovementState.Vault; i++) Step(Frame(), default);
            Assert.That(state.MovementState, Is.EqualTo(MovementState.Air));
            Assert.That(state.CompletedTraversal.HasValue, Is.True);
            Assert.That(state.CompletedTraversal.Value.Succeeded, Is.True);
        }
        private void Launch()
        {
            Step(Frame(true), Edge(.65f, true));
            Step(Frame(true), default);
            Finish();
            Assert.That(IsLaunch(Step(Frame(), Ground)), Is.True);
        }
        private static bool IsLaunch(PlayerTickResult result)
            => Array.Exists(result.Facts, f => f.Kind == TraversalKind.Jump && f.Succeeded && f.Duration > 0f);

        [TestCase(.35f)] [TestCase(.4f)] [TestCase(.65f)] [TestCase(.8f)] [TestCase(.9f)]
        public void KneeObstacleWithoutTagVaultsBeforeWallJumpAndPreservesMomentum(float height)
        {
            var probe = Edge(height, true, wall: true);
            Assert.That(probe.VaultCandidate, Is.False);
            Assert.That(Step(Frame(true), probe).Traversing, Is.True);
            Assert.That(state.VaultKind, Is.EqualTo(TraversalKind.Vault));
            Assert.That(state.LastReboundWall, Is.Zero);
            Finish();
            Assert.That(Speed, Is.EqualTo(8f).Within(.00001f));
            Assert.That(state.Position.y, Is.EqualTo(height).Within(.00001f));
            Assert.That(IsLaunch(Step(Frame(move: Vector2.up), Ground)), Is.False, "Admission press is not a second press.");
            Assert.That(Speed, Is.EqualTo(8f).Within(.00001f));
        }

        [TestCase(.5f)] [TestCase(1.4f)] [TestCase(2.2f)]
        public void AirborneUntaggedLedgeGrabsAndPullsUpWithoutJump(float height)
        {
            state.MovementState = MovementState.Air;
            state.Velocity += Vector3.down * 4f;
            Assert.That(Step(Frame(), Edge(height)).Traversing, Is.True);
            Assert.That(state.VaultKind, Is.EqualTo(TraversalKind.Mantle));
            Assert.That(state.VaultDuration, Is.InRange(.25f, .35f));
            Assert.That(state.Position.z, Is.Zero.Within(.00001f), "Grab holds the near face while pulling up.");
            Finish();
            Assert.That(state.Position, Is.EqualTo(new Vector3(0f, height, .8f)));
            Assert.That(Speed, Is.EqualTo(8f).Within(.00001f));
        }

        [TestCase(.49f)] [TestCase(2.21f)]
        public void MissedGrabDoesNotAddAStumbleOrKillForwardMotion(float height)
        {
            state.MovementState = MovementState.Air;
            var result = Step(Frame(), Edge(height));
            Assert.That(result.Traversing, Is.False);
            Assert.That(state.StumbleRemaining, Is.Zero);
            Assert.That(Speed, Is.EqualTo(8f).Within(.00001f));
            Assert.That(state.Velocity.y, Is.LessThan(0f));
        }

        [TestCase(false, .65f)] [TestCase(false, 1.5f)] [TestCase(true, 1.4f)]
        public void EarlyPressWaitsForResolvedVaultMantleOrLedgeInsteadOfCancelling(bool ledge, float height)
        {
            state.MovementState = MovementState.Air;
            Step(Frame(!ledge), Edge(height, tagged: !ledge));
            Assert.That(Step(Frame(true), default).Traversing, Is.True);
            Assert.That(state.TraversalLaunchBuffered, Is.True);
            Assert.That(state.JumpBufferRemaining, Is.Zero);
            Finish();
            var launched = Step(Frame(), Ground);
            Assert.That(IsLaunch(launched), Is.True);
            Assert.That(Speed, Is.EqualTo(14f).Within(.00001f));
            Assert.That(state.Velocity.y, Is.EqualTo(3f - 18f * Dt).Within(.00001f));
            Assert.That(IsLaunch(Step(Frame(), default)), Is.False);
        }

        [Test]
        public void TimelyNearTopPressDoesNotSkipTheLastSweptSegment()
        {
            Step(Frame(true), Edge(.65f, true));
            while (state.VaultRemaining > .1f) Step(Frame(), default);
            Assert.That(state.VaultRemaining, Is.LessThan(profile.TraversalBoostWindow));
            Assert.That(Step(Frame(true), default).Traversing, Is.True);
            Assert.That(state.TraversalLaunchReady, Is.False, "Only CommitPose at the endpoint may authorize the launch.");
            Finish();
            Assert.That(IsLaunch(Step(Frame(), Ground)), Is.True);
        }

        // Owner 2026-10-01: the whole provisional 0.22s window is usable at the top,
        // not an undisclosed shorter post-completion sub-window.
        [TestCase(.2f, true)] [TestCase(.221f, false)]
        public void PostCompletionPressInsideOrOutsideGraceHasDistinctLaunchOutcome(float delay, bool expected)
        {
            Step(Frame(true), Edge(.65f, true)); Finish();
            Step(Frame(move: Vector2.up), Ground, delay);
            Assert.That(IsLaunch(Step(Frame(true, Vector2.up), Ground)), Is.EqualTo(expected));
            Assert.That(Speed, Is.EqualTo(expected ? 14f : 8f).Within(.00001f));
        }

        [TestCase(.2f, true)] [TestCase(.22f, false)] [TestCase(1.01f, false)]
        public void FreshCompletionPressUsesGraceAfterAdvancingTheCurrentTick(float dt, bool expected)
        {
            Step(Frame(true), Edge(.65f, true)); Finish();
            state.MovementState = MovementState.Ground;
            state.Velocity = Vector3.forward * 2f;
            Assert.That(IsLaunch(Step(Frame(true), Ground, dt)), Is.EqualTo(expected));
            Assert.That(Speed, Is.EqualTo(expected ? 8f : 2f).Within(.00001f));
            Assert.That(state.TraversalLaunchReady, Is.False);
        }

        [Test]
        public void HeldJumpAndNoSecondPressCompleteWithoutLaunch()
        {
            Step(Frame(true, held: true), Edge(.65f, true));
            while (state.MovementState == MovementState.Vault) Step(Frame(held: true), default);
            Assert.That(IsLaunch(Step(Frame(held: true), Ground)), Is.False);
            Assert.That(state.TraversalLaunchBuffered, Is.False);
        }

        [Test]
        public void LaunchSurvivesAirAndLandingThenDecaysOverHalfASecondOfGroundTime()
        {
            Launch();
            for (int i = 0; i < 18; i++) Step(Frame(move: Vector2.up), default);
            Assert.That(Speed, Is.EqualTo(14f).Within(.00001f));
            controller.CommitPose(new PlayerMoveResult(state.Position, Vector3.forward * Speed, true, false));
            Step(Frame(move: Vector2.up), Ground, .05f);
            Assert.That(Speed, Is.EqualTo(13.4f).Within(.001f), "No first-ground-frame acceleration clamp.");
            Step(Frame(move: Vector2.up), Ground, .2f);
            Assert.That(Speed, Is.EqualTo(11f).Within(.001f));
            Step(Frame(move: Vector2.up), Ground, .25f);
            Assert.That(Speed, Is.EqualTo(8f).Within(.001f));
            Assert.That(state.TraversalMomentumRemaining, Is.Zero.Within(.00001f));
        }

        [Test]
        public void CollisionRemovedLaunchVelocityIsNeverReapplied()
        {
            Launch();
            controller.CommitPose(new PlayerMoveResult(state.Position, Vector3.zero, true, false));
            Step(Frame(), Ground);
            Assert.That(Speed, Is.Zero);
        }

        [Test]
        public void FailedResolvedGrabCannotLaunchOrStumbleAndRetainsEntryMomentum()
        {
            Step(Frame(), Edge(1.4f));
            Step(Frame(true), default);
            var final = Step(Frame(), default, 1f, false);
            controller.CommitPose(new PlayerMoveResult(Vector3.zero, Vector3.zero, false, false));
            Assert.That(final.Traversing, Is.True);
            Assert.That(state.CompletedTraversal.Value.Succeeded, Is.False);
            Assert.That(state.StumbleRemaining, Is.Zero);
            Assert.That(Speed, Is.EqualTo(8f).Within(.00001f));
            Assert.That(IsLaunch(Step(Frame(), default)), Is.False);
        }

        [Test]
        public void LookRemainsLiveAndOnlyLastThirdSteersThePullUp()
        {
            var first = Step(Frame(move: Vector2.right, look: new Vector2(10f, 3f)), Edge(1.4f), .1f);
            var second = Step(Frame(move: Vector2.right, look: new Vector2(5f, 4f)), default, .1f);
            Assert.That(first.TraversalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(second.TraversalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(state.HeadingDegrees, Is.EqualTo(15f));
            Assert.That(state.HeadLookDelta.y, Is.EqualTo(4f));
            var third = Step(Frame(move: Vector2.right), default, .15f);
            Assert.That(third.TraversalOffset.x, Is.GreaterThan(.2f));
            Assert.That(state.VaultProgress, Is.EqualTo(1f));
            Assert.That(state.InputLockSeconds, Is.Zero);
            Assert.That(Speed, Is.EqualTo(8f).Within(.00001f));
        }

        [Test]
        public void InvalidAutomaticGrabFallsThroughToRequestedWallJump()
        {
            state.MovementState = MovementState.Air;
            var result = Step(Frame(true), Edge(2.21f, wall: true));
            Assert.That(Array.Exists(result.Facts, f => f.Kind == TraversalKind.Rebound), Is.True);
            Assert.That(state.Velocity.z, Is.LessThan(0f));
        }

        [Test]
        public void ReachableAirborneLedgeWinsOverRequestedWallJump()
        {
            state.MovementState = MovementState.Air;
            var result = Step(Frame(true), Edge(1.4f, wall: true));
            Assert.That(result.Traversing, Is.True);
            Assert.That(state.VaultIsLedge, Is.True);
            Assert.That(state.LastReboundWall, Is.Zero);
            Assert.That(state.TraversalLaunchBuffered, Is.False, "The grab admission press cannot also launch.");
            Assert.That(Array.Exists(result.Facts, f => f.Kind == TraversalKind.Rebound), Is.False);
        }

        [Test]
        public void LaunchHasPriorityOverNearbyWallOnCompletion()
        {
            Step(Frame(true), Edge(.65f, true)); Finish();
            Assert.That(IsLaunch(Step(Frame(true), new MovementProbe(true, Vector3.up, true, .3f, Vector3.back, 0f, 10))), Is.True);
            Assert.That(state.LastReboundWall, Is.Zero);
        }

        [Test]
        public void ResetAndExternalMotionClearBufferedLaunch()
        {
            Step(Frame(true), Edge(.65f, true)); Step(Frame(true), default); Finish();
            controller.ApplyExternalVelocity(Vector3.left * 3f, ExternalMotionKind.Impulse);
            Assert.That(IsLaunch(Step(Frame(), Ground)), Is.False);
            Assert.That(state.TraversalLaunchBuffered || state.TraversalLaunchReady, Is.False);
            controller.Reset(new EntityId(2), Vector3.zero, 0f);
            Assert.That(state.TraversalMomentumRemaining, Is.Zero);
            Assert.That(state.TraversalLaunchGrace, Is.Zero);
        }

        [Test]
        public void BufferedLaunchAndPostLandingDecayReplayExactly()
        {
            var replayState = new PlayerBehaviorState();
            var replay = new PlayerController(replayState, profile, new System.Random(13));
            replay.Reset(new EntityId(1), Vector3.zero, 0f);
            replayState.Velocity = state.Velocity;
            for (int i = 0; i < 90; i++)
            {
                var probe = i == 0 ? Edge(.65f, true) : i > 36 ? Ground : default;
                var frame = Frame(i == 0 || i == 1, Vector2.up);
                var result = Step(frame, probe, Dt, false);
                replay.Replay(Commit(result, frame, probe, Dt));
                Assert.That(replayState.Position, Is.EqualTo(state.Position));
                Assert.That(replayState.Velocity, Is.EqualTo(state.Velocity));
                Assert.That(replayState.TraversalMomentumRemaining, Is.EqualTo(state.TraversalMomentumRemaining));
                Assert.That(replayState.TraversalLaunchBuffered, Is.EqualTo(state.TraversalLaunchBuffered));
                Assert.That(replayState.LastTraversalFacts, Is.EqualTo(state.LastTraversalFacts));
            }
        }
    }
}
