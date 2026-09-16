// ============================================================================
// PlayerDriverTests.cs
// ============================================================================
// PURPOSE:
//   Regresses physical capsule stepping at a rounded edge and preserves rejection
//   of missing support, excessive height and insufficient overhead clearance.
//   Vault cases exercise actual probes and swept movement from both endpoint
//   approaches, then replay the recorded resolutions and committed outcomes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Exercise the actual Driver and Unity casts against temporary BoxColliders.
//   - Bound every horizontal displacement and check continuous top support.
//   - Keep tall walls, low ceilings and unsupported edges blocking or airborne.
//   - Require fixed-lock vault completion at the closest clear face and in air.
//   - Preserve endpoint rejection and honest intermediate collision failure.
//   - Repeatedly jump, land and continue climbing real inclines at walk and sprint speed.
// DEPENDENCIES:
//   PlayerDriver/default config, NUnit, Unity physics and UnityEditor serialization.
//   PlayerController/Core replay records and temporary LevelMarker endpoint metadata.
// USAGE NOTES:
//   Root executes under the Unity lease. These are engine tests, not pure math.
//   Fixtures create their own geometry and in-memory default config away from
//   authored scenes, destroy both in teardown and never alter global physics.
//   The separate Level integration fixture remains the ordinary Run-tick gate.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    public sealed class PlayerDriverTests
    {
        private static readonly Vector3 Origin = new Vector3(2000f, 0f, 0f);
        private GameObject arrangement, actor;
        private PlayerMoverDriverConfig config;
        private PlayerDriver driver;
        private CapsuleCollider capsule;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            arrangement = new GameObject("[Test] Player step collision geometry");
            arrangement.transform.position = Origin;
            Box("Base floor", new Vector3(3f, -0.25f, 0f), new Vector3(12f, 0.5f, 4f));
        }

        [TearDown]
        public void TearDown()
        {
            if (actor != null) Object.DestroyImmediate(actor);
            if (arrangement != null) Object.DestroyImmediate(arrangement);
            if (config != null) Object.DestroyImmediate(config);
        }

        [TestCase(MovementState.Ground)]
        [TestCase(MovementState.Slide)]
        [TestCase(MovementState.Vault)]
        public void TraversalCommandsNeverShowLegacyLimbs(MovementState movement)
        {
            Spawn(Vector3.zero);
            var visuals = new GameObject("[Test] Legacy limb visuals");
            visuals.transform.SetParent(actor.transform, false);
            PlayerLimbStandIn limbs = visuals.AddComponent<PlayerLimbStandIn>();
            var serializedLimbs = new SerializedObject(limbs);
            foreach (string field in new[] { "_leftHand", "_rightHand", "_leftFoot", "_rightFoot" })
            {
                var limb = new GameObject(field);
                limb.transform.SetParent(visuals.transform, false);
                serializedLimbs.FindProperty(field).objectReferenceValue = limb;
            }
            serializedLimbs.ApplyModifiedPropertiesWithoutUndo();
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("_limbs").objectReferenceValue = limbs;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            driver.ShowMovement(movement);
            foreach (Transform limb in visuals.transform) Assert.That(limb.gameObject.activeSelf, Is.False);
        }

        [TestCase(20f, false)]
        [TestCase(20f, true)]
        [TestCase(40f, false)]
        [TestCase(40f, true)]
        public void RepeatedUphillJumpsLandAndKeepClimbing(float slopeDegrees, bool sprint)
        {
            float tangent = Mathf.Tan(slopeDegrees * Mathf.Deg2Rad);
            float cosine = Mathf.Cos(slopeDegrees * Mathf.Deg2Rad);
            BoxCollider ramp = Box("Continuous uphill surface", new Vector3(30f, 30f * tangent - 0.25f / cosine, 0f),
                new Vector3(80f / cosine, 0.5f, 4f));
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, slopeDegrees);
            Spawn(new Vector3(1f, tangent + 0.15f, 0f));
            PlayerProfile profile = ScriptableObject.CreateInstance<PlayerProfile>();
            try
            {
                const float dt = 1f / 60f;
                var state = new PlayerBehaviorState();
                var controller = new PlayerController(state, profile, new System.Random(417));
                controller.Reset(new EntityId(417), driver.Position, 90f);
                int jumps = 0, lands = 0, lastJump = -100, finalLanding = -1;
                Vector3 finalLandingPosition = Vector3.zero;
                for (int tick = 0; tick < 600; tick++)
                {
                    MovementProbe probe = driver.Probe();
                    bool jump = jumps < 3 && tick > 30 && tick - lastJump >= 40
                        && state.MovementState == MovementState.Ground && state.Grounded && probe.Grounded;
                    var frame = new InputFrame(Vector2.up, Vector2.zero,
                        sprint ? InputButtons.Sprint : InputButtons.None,
                        jump ? InputButtons.Jump : InputButtons.None, InputButtons.None);
                    PlayerTickResult decision = controller.Tick(frame, probe, dt, tick);
                    foreach (PlayerTraversalFact fact in decision.Facts)
                    {
                        if (fact.Kind == TraversalKind.Jump && fact.Succeeded) { jumps++; lastJump = tick; }
                        if (fact.Kind == TraversalKind.Land && jumps > lands)
                        {
                            lands++;
                            if (lands == 3) { finalLanding = tick; finalLandingPosition = driver.Position; }
                        }
                    }
                    PlayerMoveResult result = driver.Move(decision.Displacement, state.Velocity,
                        decision.Crouched, state.HeadingDegrees, dt);
                    controller.CommitPose(result);
                    Physics.SyncTransforms();
                    AssertNoPenetration();
                    if (finalLanding >= 0 && tick - finalLanding >= 90) break;
                }
                Assert.That(jumps, Is.EqualTo(3), "The player must regain jump control after each uphill landing.");
                Assert.That(lands, Is.EqualTo(3), "Real support must transition back from Air after all three jumps.");
                Assert.That(driver.Position.x - finalLandingPosition.x, Is.GreaterThan(2f), "Climbing must continue after landing.");
                Assert.That(driver.Position.y - finalLandingPosition.y, Is.GreaterThan(0.5f));
                Assert.That(state.MovementState, Is.EqualTo(MovementState.Ground));
                Assert.That(state.Grounded && driver.Probe().Grounded, Is.True);
                TestContext.WriteLine($"Slope {slopeDegrees} degrees, sprint={sprint}: jumps={jumps}, lands={lands}, final={driver.Position - Origin}");
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase(0f)]
        [TestCase(22.5f)]
        public void OrdinaryStepRetainsSupportWithoutExtraHorizontalTravel(float yaw)
        {
            arrangement.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box("0.2m legal step", new Vector3(3f, 0.1f, 0f), new Vector3(2f, 0.2f, 3f));
            Spawn(Vector3.zero, yaw);
            bool reachedTop = false;
            for (int tick = 0; tick < 50; tick++)
            {
                Vector3 before = driver.Position;
                PlayerMoveResult result = Advance();
                Vector3 local = arrangement.transform.InverseTransformPoint(result.Position);
                Vector3 change = result.Position - before;
                Assert.That(new Vector2(change.x, change.z).magnitude, Is.LessThanOrEqualTo(4f / 60f + 0.001f), "No support look-ahead teleport.");
                Assert.That(change.y, Is.LessThanOrEqualTo(config.StepHeight + 0.001f));
                if (local.y > 0.15f)
                {
                    reachedTop = true;
                    Assert.That(result.Grounded && driver.Probe().Grounded, Is.True, "The capsule lost real edge/top support.");
                    Assert.That(local.y, Is.EqualTo(0.2f).Within(0.005f));
                }
                AssertNoPenetration();
            }
            Assert.That(reachedTop, Is.True);
            Assert.That(arrangement.transform.InverseTransformPoint(driver.Position).x, Is.GreaterThan(3.2f));
        }

        [TestCase(0.32f)]
        [TestCase(2f)]
        public void StepHigherThanConfiguredLimitAndTallWallRemainBlocking(float height)
        {
            Box("Unclimbable step/wall", new Vector3(3f, height * 0.5f, 0f), new Vector3(2f, height, 3f));
            Spawn(Vector3.zero);
            for (int tick = 0; tick < 60; tick++) { Advance(); AssertNoPenetration(); }
            Vector3 local = driver.Position - Origin;
            Assert.That(local.x, Is.LessThan(1.8f));
            Assert.That(local.y, Is.LessThan(0.05f));
        }

        [Test]
        public void LowCeilingRejectsAnOtherwiseLegalStep()
        {
            Box("0.2m legal step", new Vector3(3f, 0.1f, 0f), new Vector3(2f, 0.2f, 3f));
            Box("Ceiling leaves less than step headroom", new Vector3(2f, 2f, 0f), new Vector3(5f, 0.2f, 3f));
            Spawn(Vector3.zero);
            for (int tick = 0; tick < 60; tick++) { Advance(); AssertNoPenetration(); }
            Assert.That(driver.Position.x - Origin.x, Is.LessThan(1.8f));
            Assert.That(driver.Position.y, Is.LessThan(0.15f));
        }

        [Test]
        public void ShortStepUnderCeilingUsesAvailableHeadroomAtSprintSpeed()
        {
            const float lipHeight = 0.16f;
            Box("Short legal lip", new Vector3(3f, lipHeight * 0.5f, 0f), new Vector3(2f, lipHeight, 3f));
            BoxCollider ceiling = Box("Ceiling permits only 0.22m rise", new Vector3(2f, 2.12f, 0f), new Vector3(6f, 0.2f, 3f));
            Spawn(Vector3.zero);
            var presenter = new PlayerMoverPresenter();
            presenter.Capsule(driver.Position, config.Height, config.Radius, out Vector3 bottom, out Vector3 top);
            RaycastHit[] upward = Physics.CapsuleCastAll(bottom, top, config.Radius - config.SkinWidth,
                Vector3.up, config.StepHeight, config.CollisionMask, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider != null && !hit.collider.transform.IsChildOf(actor.transform))
                .OrderBy(hit => hit.distance).ToArray();
            Assert.That(upward, Is.Not.Empty, "The original full-height upward guard must encounter the ceiling.");
            Assert.That(upward[0].collider, Is.SameAs(ceiling));
            float availableRise = presenter.TravelBeforeHit(Vector3.up * config.StepHeight, upward[0].distance, config.SkinWidth).y;
            Assert.That(availableRise, Is.GreaterThan(lipHeight));
            Assert.That(availableRise, Is.LessThan(config.StepHeight));
            TestContext.WriteLine($"Short lip={lipHeight:R}; full raise={config.StepHeight:R}; ceiling hit={upward[0].distance:R}; safe raise={availableRise:R}");

            bool reachedTop = false;
            Vector3 velocity = Vector3.right * 8f;
            for (int tick = 0; tick < 27; tick++)
            {
                Vector3 before = driver.Position;
                PlayerMoveResult result = driver.Move(velocity / 60f, velocity, false, 90f, 1f / 60f);
                Physics.SyncTransforms();
                Vector3 delta = result.Position - before;
                Assert.That(new Vector2(delta.x, delta.z).magnitude, Is.LessThanOrEqualTo(8f / 60f + 0.001f));
                Assert.That(delta.y, Is.LessThanOrEqualTo(availableRise + 0.001f));
                Assert.That(result.Grounded && driver.Probe().Grounded, Is.True, "A legal step must retain actual support without a projected bounce.");
                if (result.Position.y > 0.1f)
                {
                    reachedTop = true;
                    Assert.That(result.Position.y, Is.EqualTo(lipHeight).Within(0.005f));
                }
                AssertNoPenetration();
            }
            Assert.That(reachedTop, Is.True);
            Assert.That(driver.Position.x - Origin.x, Is.GreaterThan(3.2f));
            Assert.That(driver.Position.y, Is.EqualTo(lipHeight).Within(0.005f));
        }

        [Test]
        public void SupportWithinFootprintIsRequeriedAndDisappearsWithTheCollider()
        {
            BoxCollider support = Box("Platform edge", new Vector3(1f, 0.1f, 0f), new Vector3(2f, 0.2f, 3f));
            Spawn(new Vector3(2f + config.Radius - config.SkinWidth - 0.01f, 0.2f, 0f));
            Assert.That(driver.Probe().Grounded, Is.True, "Actual top support remains inside the shrunken footprint.");
            Object.DestroyImmediate(support.gameObject);
            Physics.SyncTransforms();
            Assert.That(driver.Probe().Grounded, Is.False, "Grounded cannot persist without a current physical surface.");
        }

        [Test]
        public void SurfaceBeyondShrunkenFootprintDoesNotGrantGroundSupport()
        {
            Box("Platform edge", new Vector3(1f, 0.1f, 0f), new Vector3(2f, 0.2f, 3f));
            Spawn(new Vector3(2f + config.Radius - config.SkinWidth + 0.01f, 0.2f, 0f));
            Assert.That(driver.Probe().Grounded, Is.False);
            Assert.That(driver.Move(Vector3.zero, Vector3.zero, false, 90f, 1f / 60f).Grounded, Is.False);
        }

        [Test]
        public void SteepCapsuleObstructionOccludesLowerPeripheralSupport()
        {
            Box("Lower floor", new Vector3(0f, 0.075f, 0f), new Vector3(4f, 0.15f, 3f));
            BoxCollider pad = Box("Temporary starting support", new Vector3(0f, 0.225f, 0f), new Vector3(2f, 0.15f, 2f));
            Spawn(Vector3.up * 0.3f);
            Assert.That(driver.Probe().Grounded, Is.True);
            Object.DestroyImmediate(pad.gameObject);
            Vector3 normal = Quaternion.Euler(0f, 0f, 60f) * Vector3.up;
            BoxCollider wedge = Box("60 degree obstruction", -normal * 0.05f, new Vector3(2f, 0.1f, 2f));
            wedge.transform.localRotation = Quaternion.Euler(0f, 0f, 60f);
            Physics.SyncTransforms();

            new PlayerMoverPresenter().Capsule(driver.Position, config.Height, config.Radius, out Vector3 bottom, out Vector3 top);
            RaycastHit[] contacts = Physics.CapsuleCastAll(bottom, top, config.Radius - config.SkinWidth,
                Vector3.down, config.GroundSnapDistance, config.CollisionMask, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider != null && !hit.collider.transform.IsChildOf(actor.transform))
                .OrderBy(hit => hit.distance).ToArray();
            Assert.That(contacts, Is.Not.Empty);
            Assert.That(contacts[0].collider, Is.SameAs(wedge), "The real full-volume descent must encounter the steep obstruction first.");
            Assert.That(Vector3.Angle(contacts[0].normal, Vector3.up), Is.GreaterThan(config.SlopeLimitDegrees));
            Assert.That(contacts[0].distance, Is.LessThan(0.15f + config.SkinWidth));

            Assert.That(driver.Probe().Grounded, Is.False, "A lower rim ray cannot bypass the capsule obstruction.");
            Vector3 before = driver.Position;
            PlayerMoveResult result = driver.Move(Vector3.zero, Vector3.zero, false, 90f, 1f / 60f);
            Physics.SyncTransforms();
            Assert.That(result.Grounded, Is.False);
            Assert.That(result.Position, Is.EqualTo(before));
            AssertNoPenetration();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosestClearVaultFaceCompletesAndReplaysInBothDirections(bool reverse)
        {
            BoxCollider obstacle = AddVaultSurface();
            float face = reverse ? obstacle.bounds.max.x : obstacle.bounds.min.x;
            float direction = reverse ? -1f : 1f;
            float start = face - direction * (config.Radius + config.SkinWidth);
            Spawn(new Vector3(start - Origin.x, 0f, 0f), reverse ? 180f : 0f);
            Assert.That(Mathf.Abs(driver.Position.x - face) - capsule.radius,
                Is.EqualTo(config.SkinWidth).Within(0.0002f), "The first Jump starts at capsule radius plus skin from the face.");
            Assert.That(Physics.ComputePenetration(capsule, actor.transform.position, actor.transform.rotation,
                obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out _), Is.False);
            ExerciseVault(reverse, airborne: false, admitted: true, succeeds: true, expectCeiling: false);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AirborneVaultProbeCompletesAndReplaysInBothDirections(bool reverse)
        {
            AddVaultSurface();
            Spawn(new Vector3(reverse ? 3.6f : 1.4f, 0.4f, 0f), reverse ? 180f : 0f);
            Assert.That(driver.Probe().Grounded, Is.False, "The fresh probe must come from an unsupported airborne capsule.");
            ExerciseVault(reverse, airborne: true, admitted: true, succeeds: true, expectCeiling: false);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BlockedVaultLandingRejectsOnePressAndReplays(bool lowCeiling)
        {
            AddVaultSurface();
            Box(lowCeiling ? "Low landing ceiling" : "Occupied landing",
                new Vector3(3.6f, lowCeiling ? 1.75f : 0.5f, 0f),
                new Vector3(0.6f, lowCeiling ? 0.2f : 1f, 2f));
            Spawn(new Vector3(1.4f, 0f, 0f));
            ExerciseVault(reverse: false, airborne: false, admitted: false, succeeds: false, expectCeiling: false);
        }

        [Test]
        public void IntermediateVaultCeilingStopsCastsAndReportsFailedCompletion()
        {
            AddVaultSurface();
            // Standing at either endpoint is clear, but the capsule cannot fit
            // between the waist obstacle and this beam during its vertical rise.
            Box("Intermediate beam", new Vector3(2.1f, 2.1f, 0f), new Vector3(1.8f, 0.2f, 2f));
            Spawn(new Vector3(1.4f, 0f, 0f));
            ExerciseVault(reverse: false, airborne: false, admitted: true, succeeds: false, expectCeiling: true);
        }

        private BoxCollider AddVaultSurface()
        {
            BoxCollider obstacle = Box("Paired waist vault", new Vector3(2.5f, 0.5f, 0f), new Vector3(1f, 1f, 2f));
            var marker = obstacle.gameObject.AddComponent<LevelMarker>();
            using var serialized = new SerializedObject(marker);
            serialized.FindProperty("_id").intValue = 92501;
            serialized.FindProperty("_kind").enumValueIndex = (int)LevelMarkerKind.VaultSurface;
            serialized.FindProperty("_targetPosition").vector3Value = Origin + Vector3.right * 1.4f;
            serialized.FindProperty("_oppositeTargetPosition").vector3Value = Origin + Vector3.right * 3.6f;
            serialized.FindProperty("_hasEndpointPair").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            return obstacle;
        }

        private void ExerciseVault(bool reverse, bool airborne, bool admitted, bool succeeds, bool expectCeiling)
        {
            const float dt = 1f / 60f;
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            try
            {
                var state = new PlayerBehaviorState();
                var replayState = new PlayerBehaviorState();
                var controller = new PlayerController(state, profile, new System.Random(25));
                var replay = new PlayerController(replayState, profile, new System.Random(25));
                controller.Reset(new EntityId(25), driver.Position, driver.Heading);
                replay.Reset(new EntityId(25), driver.Position, driver.Heading);
                Vector3 entryVelocity = Vector3.right * (reverse ? -profile.SprintSpeed : profile.SprintSpeed)
                    + (airborne ? Vector3.up * 2f : Vector3.zero);
                PlayerMoveResult initial = driver.Move(Vector3.zero, entryVelocity, false, driver.Heading, dt);
                controller.CommitPose(initial);
                replay.CommitPose(initial);
                MovementProbe firstProbe = driver.Probe();
                Vector3 target = Origin + Vector3.right * (reverse ? 1.4f : 3.6f);
                Assert.That(firstProbe.VaultCandidate, Is.True, "The fixture must use a real candidate cast.");
                Assert.That(firstProbe.VaultTarget, Is.EqualTo(target), "The approach must select the opposite paired endpoint.");
                Assert.That(firstProbe.VaultClearance > 0f, Is.EqualTo(admitted));
                Assert.That(firstProbe.StandingBlocked, Is.False);
                AssertNoPenetration();

                int durationTicks = Mathf.RoundToInt(profile.VaultDuration / dt);
                int lockedTicks = 0, outcomeCount = 0, ceilingTicks = 0;
                for (int tick = 1; tick <= durationTicks + 2; tick++)
                {
                    MovementProbe probe = driver.Probe();
                    var frame = new InputFrame(Vector2.up, Vector2.zero,
                        tick == 1 ? InputButtons.Jump : InputButtons.None,
                        tick == 1 ? InputButtons.Jump : InputButtons.None,
                        tick == 2 ? InputButtons.Jump : InputButtons.None);
                    Vector3 before = driver.Position;
                    PlayerTickResult decision = controller.Tick(frame, probe, dt, tick);
                    Assert.That(decision.Traversing, Is.EqualTo(admitted && tick <= durationTicks), "The lock cannot be extended to rescue contact.");
                    PlayerMoveResult movement = decision.Traversing
                        ? driver.MoveTraversal(decision.TraversalStart, decision.TraversalTarget, decision.TraversalProgress,
                            decision.TraversalHeight, state.Velocity, state.HeadingDegrees, dt, controller.MaximumMovementSpeed)
                        : driver.Move(decision.Displacement, state.Velocity, decision.Crouched, state.HeadingDegrees, dt);
                    Physics.SyncTransforms();
                    controller.CommitPose(movement);
                    var resolution = new MovementResolution(movement.Position, movement.Velocity, movement.Grounded, movement.Ceiling, driver.EyePosition);
                    var record = new InputProbeRecord(InputProbeRecord.CurrentSchemaVersion, tick, frame, probe, dt, resolution);
                    controller.CommitFrame(driver.EyePosition, record, decision.Facts);
                    replay.Replay(record);
                    AssertVaultReplay(state, replayState);
                    Vector3 delta = movement.Position - before;
                    Assert.That(new Vector2(delta.x, delta.z).magnitude,
                        Is.LessThanOrEqualTo(controller.MaximumMovementSpeed * dt + 0.0002f), "Actual movement must retain the per-tick cap.");
                    AssertNoPenetration();
                    if (state.InputLockSeconds > 0f) lockedTicks++;
                    if (movement.Ceiling) ceilingTicks++;
                    foreach (PlayerTraversalFact fact in state.LastTraversalFacts.Where(f => f.Kind == TraversalKind.Vault || f.Kind == TraversalKind.Mantle))
                    {
                        outcomeCount++;
                        Assert.That(fact.Kind, Is.EqualTo(TraversalKind.Vault));
                        Assert.That(fact.Succeeded, Is.EqualTo(succeeds));
                        Assert.That(fact.Tick, Is.EqualTo(admitted ? durationTicks : 1));
                        Assert.That(fact.Duration, Is.EqualTo(profile.VaultDuration));
                        float residual = Vector3.Distance(movement.Position, target);
                        if (admitted)
                        {
                            Assert.That(residual <= profile.VaultCompletionTolerance, Is.EqualTo(succeeds), "Only the resolved endpoint establishes success.");
                            Assert.That(new Vector2(state.Velocity.x, state.Velocity.z).magnitude,
                                Is.EqualTo(profile.SprintSpeed).Within(0.0001f), "Completion restores the entry speed.");
                        }
                        TestContext.WriteLine($"Vault outcome tick={tick}; succeeded={fact.Succeeded}; residual={residual:R}; ceilingTicks={ceilingTicks}; position={movement.Position:R}");
                    }
                }
                Assert.That(outcomeCount, Is.EqualTo(1), "One ordinary press must yield one final Vault outcome.");
                Assert.That(lockedTicks, Is.EqualTo(admitted ? durationTicks : 0));
                Assert.That(state.InputLockSeconds, Is.Zero);
                if (expectCeiling) Assert.That(ceilingTicks, Is.GreaterThan(0), "The intermediate beam must produce actual ceiling contact.");
            }
            finally { Object.DestroyImmediate(profile); }
        }

        private static void AssertVaultReplay(PlayerBehaviorState actual, PlayerBehaviorState replay)
        {
            Assert.That(replay.Position, Is.EqualTo(actual.Position));
            Assert.That(replay.Velocity, Is.EqualTo(actual.Velocity));
            Assert.That(replay.MovementState, Is.EqualTo(actual.MovementState));
            Assert.That(replay.Grounded, Is.EqualTo(actual.Grounded));
            Assert.That(replay.InputLockSeconds, Is.EqualTo(actual.InputLockSeconds));
            Assert.That(replay.VaultRemaining, Is.EqualTo(actual.VaultRemaining));
            Assert.That(replay.LastMovementSample, Is.EqualTo(actual.LastMovementSample));
            Assert.That(replay.LastTraversalFacts.ToArray(), Is.EqualTo(actual.LastTraversalFacts.ToArray()));
        }

        private void Spawn(Vector3 localPosition, float yaw = 0f)
        {
            actor = new GameObject("[Test] Player Driver");
            actor.transform.SetPositionAndRotation(arrangement.transform.TransformPoint(localPosition), Quaternion.Euler(0f, 90f + yaw, 0f));
            driver = actor.AddComponent<PlayerDriver>();
            capsule = actor.GetComponent<CapsuleCollider>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            driver.Initialize();
            Physics.SyncTransforms();
            driver.Move(Vector3.zero, Vector3.zero, false, 90f + yaw, 1f / 60f);
        }
        private PlayerMoveResult Advance()
        {
            Vector3 velocity = arrangement.transform.right * 4f;
            PlayerMoveResult result = driver.Move(velocity / 60f, velocity, false, actor.transform.eulerAngles.y, 1f / 60f);
            Physics.SyncTransforms();
            return result;
        }
        private BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject("[Test] " + name);
            box.transform.SetParent(arrangement.transform, false);
            box.transform.localPosition = position;
            var collider = box.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }
        private void AssertNoPenetration()
        {
            foreach (Collider obstacle in arrangement.GetComponentsInChildren<Collider>())
                if (Physics.ComputePenetration(capsule, actor.transform.position, actor.transform.rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth))
                    Assert.That(depth, Is.LessThanOrEqualTo(config.SkinWidth * 2f + 0.001f), obstacle.name);
        }
    }
}
