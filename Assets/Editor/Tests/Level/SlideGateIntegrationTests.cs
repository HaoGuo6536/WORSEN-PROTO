// ============================================================================
// SlideGateIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies real standing and sliding capsules against TagArena's authored gate,
//   including jump suppression beneath its ceiling and standing after passage.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level physical integration.
// KEY RESPONSIBILITIES:
//   - Drive synthetic input through normal Run ticks and inspect committed probes.
//   - Check actual collider height, clearance and penetration without fake probes.
// DEPENDENCIES:
//   - Core, Player, Level/Hunter inspection, Run/Input and the TagArena scene root.
//   - NUnit, Unity Test Framework, UnityEditor read-only asset inspection and physics.
// USAGE NOTES:
//   Coordinator owns the Unity lease. Test Framework isolates/restores the scene.
//   Each trial uses an explicitly placed initial pose and a freshly initialized
//   life with the existing identity/profile/shared random; movement then uses Run.
//   Hunters are disabled only in this isolated scene until unload. Background
//   simulation is restored in finally; no profile, geometry or asset is modified.
//   This tests the low passage, not whether a player can jump over a finite gate,
//   hardware input, chased movement or capture/replay acceptance.
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
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;

namespace Worsen.Tests.Level
{
    public sealed class SlideGateIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";

        [UnityTest]
        public IEnumerator AuthoredLowGateRequiresSlideBlocksUnderCeilingJumpAndRestoresStanding()
        {
            yield return new EnterPlayMode();
            yield return ExerciseGate();
        }

        private static IEnumerator ExerciseGate()
        {
            bool previousBackground = Application.runInBackground;
            InputManager input = null;
            Application.runInBackground = true;
            try
            {
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                    "Use the Test Framework's isolated bootstrap scene.");
                var load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build TagArena before this physical fixture.");
                yield return Until(() => load.isDone && RunSessionManager.Instance != null &&
                    RunSessionManager.Instance.Scene == SceneKey.TagArena && RunSessionManager.Instance.Tick >= 3,
                    "TagArena did not start ticking.");
                var run = RunSessionManager.Instance;
                input = InputManager.Instance;
                Assert.That(input, Is.Not.Null);
                input.SetInputEnabled(false);
                foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                {
                    Assert.That(hunter.gameObject.scene.path, Is.EqualTo(ArenaPath));
                    hunter.gameObject.SetActive(false);
                }
                Assert.That(HunterRegistry.Items.Count, Is.Zero);
                var player = One<PlayerManager>();
                var capsule = player.GetComponent<CapsuleCollider>();
                var rootFields = new SerializedObject(One<TagArenaSceneRoot>());
                var profile = rootFields.FindProperty("_playerProfile").objectReferenceValue as PlayerProfile;
                var driverFields = new SerializedObject(player.GetComponent<PlayerDriver>());
                var config = driverFields.FindProperty("_config").objectReferenceValue as PlayerMoverDriverConfig;
                Assert.That(profile, Is.Not.Null); Assert.That(config, Is.Not.Null); Assert.That(capsule, Is.Not.Null);
                var marker = UnityEngine.Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).Single(item => item.SurfaceId == 220);
                Assert.That(marker.Kind, Is.EqualTo(TraversalSurfaceKind.SlideGate));
                var gate = marker.GetComponent<BoxCollider>();
                Assert.That(gate, Is.Not.Null); Assert.That(gate.enabled && !gate.isTrigger, Is.True);
                var bounds = gate.bounds;
                Assert.That(bounds.min.y, Is.EqualTo(1.05f).Within(0.001f));
                Assert.That(config.Height, Is.EqualTo(1.8f).Within(0.001f));
                Assert.That(config.Height * config.SlideHeightRatio, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That((config.CollisionMask & (1 << gate.gameObject.layer)) != 0, Is.True);
                var profileHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(profile));
                var configHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(config));
                var start = new Vector3(bounds.min.x - 2.5f, 0.05f, bounds.center.z);

                PlaceInitialPose(player, profile, run, start);
                yield return DriveTicks(run, 10, _ => default, null);
                Assert.That(player.LastProbeRecord.Resolution.Grounded, Is.True);
                float standingMaxX = float.NegativeInfinity;
                bool standingHeightChanged = false;
                yield return DriveTicks(run, 60, _ => Forward(player, InputButtons.None, InputButtons.None, InputButtons.None), record =>
                {
                    standingMaxX = Mathf.Max(standingMaxX, record.Resolution.Position.x);
                    standingHeightChanged |= Mathf.Abs(capsule.height - config.Height) > 0.001f;
                });
                Assert.That(standingHeightChanged, Is.False);
                Assert.That(standingMaxX, Is.LessThanOrEqualTo(bounds.min.x - config.Radius + config.SkinWidth + 0.01f),
                    "A standing capsule passed through the low opening.");
                Assert.That(standingMaxX, Is.GreaterThan(start.x + 1f), "The standing trial never reached the gate.");
                Assert.That(player.ReadOnlyState.Velocity.x, Is.LessThan(0.05f), "Standing movement was not blocked.");

                PlaceInitialPose(player, profile, run, start);
                yield return DriveTicks(run, 10, _ => default, null);
                bool crouchPressed = false, jumpPressedUnderGate = false;
                bool sawSlidingInside = false, sawReleasedCrouchBlocked = false, sawStandingAfterExit = false;
                bool grewUnderGate = false, penetratedGate = false, invalidResolution = false;
                int slides = 0, blockedJumpSamples = 0;
                float maximumUnderGateFeetY = float.NegativeInfinity;
                InputButtons previousHeld = InputButtons.None;
                yield return DriveTicks(run, 150, _ =>
                {
                    InputButtons pressed = InputButtons.None;
                    if (!crouchPressed && new Vector2(player.ReadOnlyState.Velocity.x, player.ReadOnlyState.Velocity.z).magnitude >= profile.SlideMinimumSpeed)
                    { pressed = InputButtons.Crouch; crouchPressed = true; }
                    else if (!jumpPressedUnderGate && player.LastProbeRecord.Probe.StandingBlocked &&
                        player.ReadOnlyState.Position.x >= bounds.min.x && player.ReadOnlyState.Position.x <= bounds.max.x)
                    { pressed = InputButtons.Jump; jumpPressedUnderGate = true; }
                    var frame = Forward(player, pressed, pressed, previousHeld & ~pressed);
                    previousHeld = pressed;
                    if (player.ReadOnlyState.Position.x > bounds.max.x + config.Radius + config.SkinWidth)
                        frame = new InputFrame(Vector2.zero, frame.LookDelta, frame.Held, frame.Pressed, frame.Released);
                    return frame;
                }, record =>
                {
                    var position = record.Resolution.Position;
                    bool inside = position.x >= bounds.min.x && position.x <= bounds.max.x;
                    invalidResolution |= !record.Resolution.Present || float.IsNaN(position.sqrMagnitude) ||
                        float.IsInfinity(position.sqrMagnitude) || position.y < -0.05f;
                    slides += player.LastTraversalFacts.Count(fact => fact.Kind == TraversalKind.Slide && fact.Succeeded);
                    sawSlidingInside |= inside && player.LastMovementSample.MovementState == MovementState.Slide &&
                        Mathf.Abs(capsule.height - config.Height * config.SlideHeightRatio) < 0.001f;
                    sawReleasedCrouchBlocked |= inside && record.Probe.StandingBlocked && (record.Input.Held & InputButtons.Crouch) == 0;
                    if (inside) maximumUnderGateFeetY = Mathf.Max(maximumUnderGateFeetY, position.y);
                    grewUnderGate |= inside && capsule.bounds.max.y > bounds.min.y + 0.01f;
                    penetratedGate |= Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                        gate, gate.transform.position, gate.transform.rotation, out _, out float distance) && distance > config.SkinWidth + 0.005f;
                    if ((record.Input.Pressed & InputButtons.Jump) != 0 && record.Probe.StandingBlocked && inside &&
                        !player.LastTraversalFacts.Any(fact => fact.Kind == TraversalKind.Jump && fact.Succeeded) &&
                        record.Resolution.Velocity.y <= 0.01f && player.LastMovementSample.MovementState == MovementState.Ground
                        && Mathf.Abs(capsule.height - config.Height * config.SlideHeightRatio) < 0.001f)
                        blockedJumpSamples++;
                    sawStandingAfterExit |= position.x > bounds.max.x + config.Radius + config.SkinWidth &&
                        !record.Probe.StandingBlocked && player.LastMovementSample.MovementState == MovementState.Ground &&
                        Mathf.Abs(capsule.height - config.Height) < 0.001f;
                });
                Assert.That(invalidResolution || grewUnderGate || penetratedGate, Is.False,
                    "The actual capsule grew into, penetrated, or fell below the authored gate/floor.");
                Assert.That(slides, Is.EqualTo(1));
                Assert.That(sawSlidingInside && sawReleasedCrouchBlocked, Is.True, "No reduced capsule was observed beneath the real blocking ceiling.");
                Assert.That(jumpPressedUnderGate, Is.True, "The trial never attempted jump while under the gate.");
                Assert.That(blockedJumpSamples, Is.EqualTo(1), "The real ceiling probe did not suppress the under-gate jump.");
                Assert.That(maximumUnderGateFeetY, Is.LessThan(bounds.min.y - config.Height * config.SlideHeightRatio + 0.01f));
                Assert.That(sawStandingAfterExit, Is.True, "Standing height did not restore after clearing the gate after cancellation.");
                Assert.That(player.ReadOnlyState.Position.x, Is.GreaterThan(bounds.max.x + config.Radius));
                Assert.That(player.ReadOnlyState.Health, Is.EqualTo(profile.MaximumHealth));
                Assert.That(gate.bounds, Is.EqualTo(bounds));
                Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(profile)), Is.EqualTo(profileHash));
                Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(config)), Is.EqualTo(configHash));
                TestContext.Progress.WriteLine("Slide gate220: clearance=" + bounds.min.y + "; standing maxX=" + standingMaxX +
                    "; under-gate max feetY=" + maximumUnderGateFeetY + "; blocked jump samples=" + blockedJumpSamples +
                    "; final position=" + player.ReadOnlyState.Position + "; final capsule height=" + capsule.height);
            }
            finally
            {
                if (input != null) input.SetInputEnabled(false);
                Application.runInBackground = previousBackground;
            }
        }

        private static void PlaceInitialPose(PlayerManager player, PlayerProfile profile, RunSessionManager run, Vector3 position)
        {
            var id = player.Id;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 90f, 0f));
            player.Initialize(profile, new EntityContext(id, run.RandomSource));
            Physics.SyncTransforms();
        }

        private static InputFrame Forward(PlayerManager player, InputButtons held, InputButtons pressed, InputButtons released)
            => new InputFrame(Vector2.up, new Vector2(Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, 90f), 0f), held | InputButtons.Sprint, pressed, released);

        private static IEnumerator DriveTicks(RunSessionManager run, int required, Func<int, InputFrame> next, Action<InputProbeRecord> observe)
        {
            int ticks = 0;
            Action before = () => run.ReceiveInput(ticks < required ? next(ticks) : default);
            Action<InputProbeRecord> record = value => { if (ticks >= required) return; observe?.Invoke(value); ticks++; };
            run.BeforeTick += before;
            run.PlayerProbeRecorded += record;
            try { yield return Until(() => ticks >= required, "The normal Run tick did not complete " + required + " slide-gate steps."); }
            finally { run.BeforeTick -= before; run.PlayerProbeRecorded -= record; }
        }

        private static T One<T>() where T : Component
        {
            var values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return values[0];
        }

        private static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            for (int frame = 0; frame < 3600 && !condition() && Time.realtimeSinceStartup < deadline; frame++) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
