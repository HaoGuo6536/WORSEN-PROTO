// ============================================================================
// LookBackTraversalIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises held LookBack steering and slide-jump on the built TagArena floor,
//   then observes the real first-person camera's held and released orientations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player integration.
// KEY RESPONSIBILITIES:
//   - Use actual Run fixed ticks, collision probes and committed traversal facts.
//   - Verify frozen body heading, reduced lateral steering and usable slide-jump.
//   - Observe Camera output routed from gameplay, including body look on release.
// DEPENDENCIES:
//   Core, Player/Hunter/Chase, Run/Input, Camera, TagArena and CaptureGateTrace.
//   NUnit, Unity Test Framework and read-only UnityEditor serialized inspection.
// USAGE NOTES:
//   Root owns the Unity lease. The thin entry yields a fresh iterator after reload.
//   Synthetic input follows the real producer through FramePublished; public Input
//   gating excludes hardware. Scene Hunters are disabled for this free movement.
//   No teleports, direct simulation ticks, fabricated probes or config/state edits.
//   Camera endpoint checks do not measure easing duration or subjective comfort.
//   The automatic run recording is interrupted by the explicit hardware gate;
//   this fixture makes no complete-capture, chase-rate or hardware-input claim.
// ============================================================================
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Input;
using Worsen.Session.Run;

namespace Worsen.Tests.Player
{
    public sealed class LookBackTraversalIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";

        [UnityTest]
        public IEnumerator HeldLookBackPreservesSteeringSlideJumpAndCameraReturn()
        {
            yield return new EnterPlayMode();
            yield return ExerciseArena();
        }

        private static IEnumerator ExerciseArena()
        {
            bool previousBackground = Application.runInBackground;
            InputManager input = null;
            RunSessionManager run = null;
            Trial trial = null;
            var trace = new CaptureGateTrace("LookBackTraversal");
            Application.runInBackground = true;
            try
            {
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty);
                yield return trace.AdmitStableGameViewFocus();
                AsyncOperation load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build TagArena before running this fixture.");
                yield return Until(() => load.isDone && RunSessionManager.Instance != null &&
                    RunSessionManager.Instance.Scene == SceneKey.TagArena && RunSessionManager.Instance.Tick >= 3,
                    "TagArena did not become ready.");
                run = RunSessionManager.Instance;
                input = InputManager.Instance;
                Assert.That(input, Is.Not.Null);
                input.SetInputEnabled(false);
                foreach (HunterManager hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                {
                    Assert.That(hunter.gameObject.scene.path, Is.EqualTo(ArenaPath));
                    hunter.gameObject.SetActive(false);
                }
                Assert.That(HunterRegistry.Items.Count, Is.Zero);
                PlayerManager player = One<PlayerManager>();
                ChaseManager chase = One<ChaseManager>();
                CameraManager camera = One<CameraManager>();
                Assert.That(camera.IsReady, Is.True);
                Assert.That(UnityEngine.Camera.main, Is.Not.Null);
                yield return Until(() => player.LastProbeRecord.Resolution.Grounded &&
                    player.ReadOnlyState.Velocity.sqrMagnitude < 0.0001f, "Player did not settle.");
                Assert.That(Vector3.Distance(player.ReadOnlyState.Position, new Vector3(-20f, 0f, 0f)), Is.LessThan(0.25f));
                Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));
                var root = new SerializedObject(One<TagArenaSceneRoot>());
                var profile = (PlayerProfile)root.FindProperty("_playerProfile").objectReferenceValue;
                var cameraConfig = (CameraDriverConfig)new SerializedObject(camera)
                    .FindProperty("_config").objectReferenceValue;
                Assert.That(profile, Is.Not.Null);
                Assert.That(cameraConfig, Is.Not.Null);
                Assert.That(profile.LookBackSteerAuthority, Is.EqualTo(0.35f));
                Assert.That(cameraConfig.LookBackYaw, Is.EqualTo(160f));
                trial = new Trial(run, player, chase, profile, cameraConfig, UnityEngine.Camera.main);
                input.FramePublished += trial.PublishSynthetic;
                run.PlayerProbeRecorded += trial.ObserveCommitted;
                Application.focusChanged += trial.ObserveFocus;
                trace.Mark("synthetic LookBack route begins");
                double deadline = Time.realtimeSinceStartupAsDouble + 30.0;
                for (int frame = 0; frame < 3600 && !trial.Done && trial.Failure.Length == 0 &&
                    Time.realtimeSinceStartupAsDouble < deadline; frame++)
                {
                    yield return null;
                    trial.ObserveCamera();
                }
                trace.Mark("synthetic LookBack route outcome: " + trial.Describe());
                Assert.That(trial.Failure, Is.Empty, trial.Describe() + " " + trace.Describe());
                Assert.That(trial.Done, Is.True, "Bounded route did not finish: " + trial.Describe());
                trial.AssertOutcome();
            }
            finally
            {
                if (input != null && trial != null) input.FramePublished -= trial.PublishSynthetic;
                if (run != null && trial != null) run.PlayerProbeRecorded -= trial.ObserveCommitted;
                if (trial != null) Application.focusChanged -= trial.ObserveFocus;
                if (input != null) input.SetInputEnabled(false);
                trace.Dispose();
                Application.runInBackground = previousBackground;
            }
        }

        private enum Stage { Accelerate, Steer, Slide, Jump, Settle, BackView, Return, Complete }

        private sealed class Trial
        {
            private const float Heading = 90f, HeadDelta = 5f, ReleaseDelta = 7f;
            private readonly RunSessionManager run;
            private readonly PlayerManager player;
            private readonly ChaseManager chase;
            private readonly PlayerProfile profile;
            private readonly CameraDriverConfig cameraConfig;
            private readonly UnityEngine.Camera output;
            private readonly CapsuleCollider capsule;
            private readonly Vector3 initialPosition;
            private readonly float standingHeight;
            private Stage stage;
            private int stageTicks, published, committed, steerSamples, slideSamples;
            private long lastTick;
            private InputFrame sent;
            private InputButtons previousHeld;
            private Vector3 steerStart;
            private float lateralDisplacement, steeringRatioSum, backYaw, returnedYaw;
            private bool jumped, airborne, landed, cameraBack, cameraReturned;
            public string Failure = "";
            public bool Done => stage == Stage.Complete;

            public Trial(RunSessionManager run, PlayerManager player, ChaseManager chase,
                PlayerProfile profile, CameraDriverConfig cameraConfig, UnityEngine.Camera output)
            {
                this.run = run; this.player = player; this.chase = chase;
                this.profile = profile; this.cameraConfig = cameraConfig; this.output = output;
                capsule = player.GetComponent<CapsuleCollider>();
                standingHeight = capsule.height;
                initialPosition = player.ReadOnlyState.Position;
                lastTick = run.Tick;
            }

            public void PublishSynthetic(InputFrame produced)
            {
                if (!produced.Equals(default(InputFrame))) Fail("Hardware-gated producer was not neutral.");
                if (Done || Failure.Length > 0) { run.ReceiveInput(default); return; }
                bool heldBack = stage != Stage.Accelerate && stage != Stage.Return;
                InputButtons held = InputButtons.Sprint | (heldBack ? InputButtons.LookBack : InputButtons.None);
                Vector2 move = stage == Stage.Accelerate || stage == Stage.Steer || stage == Stage.Slide || stage == Stage.Jump
                    ? Vector2.up : Vector2.zero;
                Vector2 look = Vector2.zero;
                if (stage == Stage.Accelerate) look.x = Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, Heading);
                if (stage == Stage.Steer) { move.x = 0.5f; if (stageTicks == 0) look.x = HeadDelta; }
                if (stage == Stage.Slide) held |= InputButtons.Crouch;
                if (stage == Stage.Jump && stageTicks == 0) held |= InputButtons.Jump;
                if (stage == Stage.Return && stageTicks == 0) look.x = ReleaseDelta;
                sent = new InputFrame(move, look, held, held & ~previousHeld, previousHeld & ~held);
                previousHeld = held;
                published++;
                run.ReceiveInput(sent);
            }

            public void ObserveCommitted(InputProbeRecord record)
            {
                if (Done || Failure.Length > 0) return;
                committed++; stageTicks++;
                if (record.Tick != ++lastTick || record.DeltaTime != Time.fixedDeltaTime ||
                    published != committed || !record.Input.Equals(sent)) Fail("Synthetic input/tick correspondence changed.");
                MovementResolution pose = record.Resolution;
                if (!pose.Present || !Finite(pose.Position) || !Finite(pose.Velocity) ||
                    pose.Position.y < -0.1f || pose.Position.x < -21f || pose.Position.x > -7f || Mathf.Abs(pose.Position.z) > 3f)
                    Fail("Real motion left the bounded connector route or lost its collision result.");
                if (!Application.isFocused || chase.ReadOnlyState.HasActiveChase || HunterRegistry.Items.Count != 0 ||
                    player.ReadOnlyState.Health != profile.MaximumHealth) Fail("Free movement lost focus, entered chase or took damage.");
                bool heldBack = (record.Input.Held & InputButtons.LookBack) != 0;
                float expectedHeading = stage == Stage.Return ? Heading + ReleaseDelta : Heading;
                if (Mathf.Abs(Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, expectedHeading)) > 0.001f ||
                    player.ReadOnlyState.LookBack != heldBack || player.LastMovementSample.LookBack != heldBack)
                    Fail("LookBack body-heading/held state diverged from the committed input.");
                if (heldBack && player.LastMovementSample.HeadLookDelta != record.Input.LookDelta)
                    Fail("Held look delta was not routed as head look.");
                if (committed > 900 || stageTicks > 300) Fail("Bounded fixed-tick limit exceeded.");

                switch (stage)
                {
                    case Stage.Accelerate:
                        if (Speed() >= profile.SprintSpeed * 0.99f)
                        { steerStart = pose.Position; Advance(Stage.Steer); }
                        break;
                    case Stage.Steer:
                        if (stageTicks >= 12)
                        {
                            float forward = Vector3.Dot(pose.Velocity, Vector3.right);
                            float lateral = Vector3.Dot(pose.Velocity, Vector3.back);
                            if (!record.Probe.Grounded || !pose.Grounded || forward < profile.SprintSpeed * 0.8f ||
                                Mathf.Abs(lateral / forward - profile.LookBackSteerAuthority * 0.5f) > 0.025f)
                                Fail("Held LookBack did not retain forward motion and reduced lateral steering.");
                            steeringRatioSum += lateral / Mathf.Max(0.001f, forward); steerSamples++;
                        }
                        if (stageTicks == 20)
                        { lateralDisplacement = Vector3.Dot(pose.Position - steerStart, Vector3.back); Advance(Stage.Slide); }
                        break;
                    case Stage.Slide:
                        if (stageTicks == 1 && !player.LastTraversalFacts.Any(fact => fact.Kind == TraversalKind.Slide && fact.Succeeded))
                            Fail("Held LookBack slide lacked a successful committed traversal fact.");
                        if (player.ReadOnlyState.MovementState != MovementState.Slide ||
                            Mathf.Abs(capsule.height - standingHeight * 0.5f) > 0.001f || !pose.Grounded)
                            Fail("Held LookBack did not enter the real reduced slide capsule.");
                        slideSamples++;
                        if (stageTicks == 10) Advance(Stage.Jump);
                        break;
                    case Stage.Jump:
                        if (stageTicks == 1)
                            jumped = player.LastTraversalFacts.Any(fact => fact.Kind == TraversalKind.Jump && fact.Succeeded) &&
                                record.Probe.Grounded && !pose.Grounded && pose.Velocity.y > 0f;
                        airborne |= !record.Probe.Grounded && !pose.Grounded && pose.Position.y > initialPosition.y + 0.1f;
                        landed |= player.LastTraversalFacts.Any(fact => fact.Kind == TraversalKind.Land && fact.Succeeded) && pose.Grounded;
                        if (landed) Advance(Stage.Settle);
                        break;
                    case Stage.Settle: if (pose.Grounded && Speed() < 0.05f) Advance(Stage.BackView); break;
                }
            }

            public void ObserveCamera()
            {
                if (Failure.Length > 0 || output == null) return;
                float yaw = Mathf.Atan2(output.transform.forward.x, output.transform.forward.z) * Mathf.Rad2Deg;
                bool atEye = Vector3.Distance(output.transform.position, player.LastMovementSample.EyePosition) < 0.05f;
                if (stage == Stage.BackView && player.ReadOnlyState.LookBack && atEye &&
                    Mathf.Abs(Mathf.DeltaAngle(yaw, Heading + cameraConfig.LookBackYaw + HeadDelta)) < 2f)
                { backYaw = yaw; cameraBack = true; Advance(Stage.Return); }
                else if (stage == Stage.Return && stageTicks >= 2 && !player.ReadOnlyState.LookBack && atEye &&
                    Mathf.Abs(Mathf.DeltaAngle(yaw, Heading + ReleaseDelta)) < 2f)
                { returnedYaw = yaw; cameraReturned = true; Advance(Stage.Complete); }
            }

            public void ObserveFocus(bool focused) { if (!focused) Fail("Actual focus interrupted the LookBack trial."); }
            public void AssertOutcome()
            {
                Assert.That(steerSamples, Is.EqualTo(9));
                Assert.That(lateralDisplacement, Is.GreaterThan(0.2f));
                Assert.That(slideSamples, Is.EqualTo(10));
                Assert.That(jumped && airborne && landed, Is.True, "Real slide-jump/air/landing chain was incomplete.");
                Assert.That(cameraBack && cameraReturned, Is.True);
                Assert.That(player.ReadOnlyState.LookBack, Is.False);
                Assert.That(Vector3.Dot(player.ReadOnlyState.Position - initialPosition, Vector3.right), Is.GreaterThan(6f));
            }
            public string Describe() => "stage=" + stage + "; ticks=" + committed + "; position=" + player.ReadOnlyState.Position +
                "; steerSamples=" + steerSamples + "; meanLateralForwardRatio=" + (steeringRatioSum / Mathf.Max(1, steerSamples)) +
                "; lateralMetres=" + lateralDisplacement + "; slides=" + slideSamples + "; jump/air/land=" + jumped + "/" + airborne + "/" + landed +
                "; cameraBack/return=" + cameraBack + "/" + cameraReturned + "; cameraWorldYaw=" + backYaw + "/" + returnedYaw;
            private void Advance(Stage next) { stage = next; stageTicks = 0; }
            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }
            private float Speed() => new Vector2(player.ReadOnlyState.Velocity.x, player.ReadOnlyState.Velocity.z).magnitude;
            private static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
        }

        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return values[0];
        }
        private static IEnumerator Until(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15.0;
            for (int frame = 0; frame < 1800 && !condition() && Time.realtimeSinceStartupAsDouble < deadline; frame++) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
