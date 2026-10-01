// ============================================================================
// PlayerTraversalSmoothnessPlayModeTests.cs
// ============================================================================
// PURPOSE:
//   Measures the real output camera on every rendered frame of a deterministic vault.
//   A temporary waist-high arrangement follows the existing Player/Level fixtures;
//   only normal Run fixed ticks drive the Player and the production camera routing.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player integration.
// KEY RESPONSIBILITIES:
//   - Observe the final Game camera, not the rig or a synthetic presenter pose.
//   - Report zero-motion frames, consecutive displacement ratios and second differences.
//   - Fail inadequate render cadence and compare an explicitly labelled tick-held control.
//   - Restore frame pacing/background settings, event subscriptions and temporary geometry.
// DEPENDENCIES:
//   Core, Player/Level/Hunter, Run/Input/Camera, TagArena, Unity rendering and NUnit.
// USAGE NOTES:
//   Coordinator-only Unity execution; one Play Mode entry with the required long timeout.
//   No shared assets, timeScale or fixedDeltaTime changes. Requires a focused Game View.
//   Records every arc camera frame through its one-step presentation delay. Only the
//   zero-motion count uses a mid-arc window starting after two ticks. Thresholds
//   retain the existing very short fall phase; this is not owner comfort acceptance.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000), Category("RequiresFocus")]
    public sealed class PlayerTraversalSmoothnessPlayModeTests
    {
        private int previousVSync, previousTarget;
        private bool previousBackground, restoreSettings;
        private GameObject arrangement;
        private RunSessionManager run;
        private PlayerManager player;
        private UnityEngine.Camera output;
        private CameraDriverConfig cameraConfig;
        private float arcStart = -1f, duration, latestHeight;
        private int suppliedTicks, successes, failures, lastRenderFrame = -1;
        private long lastRecordedTick = -1;
        private int distinctTicks;
        private readonly List<Vector3> frames = new List<Vector3>(), heldFrames = new List<Vector3>();
        private readonly List<float> times = new List<float>();
        private bool observing;

        [UnityTest]
        public IEnumerator VaultCameraMovesBetweenFixedTicksAt144Hz()
        {
            yield return new EnterPlayMode();
            yield return Exercise();
        }

        private IEnumerator Exercise()
        {
            previousVSync = QualitySettings.vSyncCount;
            previousTarget = Application.targetFrameRate;
            previousBackground = Application.runInBackground;
            restoreSettings = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 144;
            Application.runInBackground = true;
            using (var focus = new CaptureGateTrace("VaultSmoothness"))
            {
                yield return focus.AdmitStableGameViewFocus();
                var load = SceneManager.LoadSceneAsync("Assets/Scenes/TagArena.unity", LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null);
                yield return Until(() => load.isDone && RunSessionManager.Instance != null && RunSessionManager.Instance.Tick >= 3);
                run = RunSessionManager.Instance;
                InputManager.Instance.SetInputEnabled(false);
                foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                    hunter.gameObject.SetActive(false);
                player = One<PlayerManager>();
                var profile = (PlayerProfile)new SerializedObject(One<TagArenaSceneRoot>()).FindProperty("_playerProfile").objectReferenceValue;
                var camera = One<CameraManager>();
                var fields = new SerializedObject(One<CameraDriver>());
                output = (UnityEngine.Camera)fields.FindProperty("_outputCamera").objectReferenceValue;
                cameraConfig = (CameraDriverConfig)new SerializedObject(camera).FindProperty("_config").objectReferenceValue;
                Assert.That(output != null && cameraConfig != null && camera.IsReady, Is.True);
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(.000001f));
                duration = profile.VaultDuration;
                Assert.That(duration, Is.EqualTo(.25f).Within(.000001f), "The benchmark retains the authored timing.");
                arrangement = new GameObject("[Test] Vault smoothness arrangement");
                arrangement.transform.position = new Vector3(1000f, 0f, 0f);
                Box("Floor", new Vector3(3f, -.25f, 0f), new Vector3(12f, .5f, 4f));
                var obstacle = Box("Waist vault", new Vector3(2.5f, .5f, 0f), new Vector3(1f, 1f, 2f));
                var marker = obstacle.gameObject.AddComponent<LevelMarker>();
                var markerFields = new SerializedObject(marker);
                markerFields.FindProperty("_id").intValue = 92502;
                markerFields.FindProperty("_kind").enumValueIndex = (int)LevelMarkerKind.VaultSurface;
                markerFields.FindProperty("_targetPosition").vector3Value = arrangement.transform.position + Vector3.right * 3.6f;
                markerFields.ApplyModifiedPropertiesWithoutUndo();
                player.transform.SetPositionAndRotation(arrangement.transform.position + new Vector3(1.4f, .05f, 0f), Quaternion.Euler(0f, 90f, 0f));
                player.Initialize(profile, new EntityContext(player.Id, run.RandomSource));
                camera.ResetView();
                Physics.SyncTransforms();
                run.BeforeTick += Supply;
                run.PlayerFacts.TraversalProgressed += Progress;
                run.PlayerTraversalPublished += Outcome;
                RenderPipelineManager.endCameraRendering += Rendered;
                observing = true;
                yield return Until(() => arcStart >= 0f && Time.time > arcStart + duration + 2f * Time.fixedDeltaTime);
                Assert.That(successes, Is.EqualTo(1));
                Assert.That(failures, Is.Zero);
                Assert.That(Vector3.Distance(player.LastMovementSample.Position, marker.Target), Is.LessThan(.06f));
                Assert.That(frames.Count, Is.GreaterThanOrEqualTo(20), "Insufficient rendered samples; not a smoothness pass.");
                Assert.That(distinctTicks, Is.GreaterThanOrEqualTo(10));
                float maxDt = 0f;
                for (int i = 1; i < times.Count; i++) maxDt = Mathf.Max(maxDt, times[i] - times[i - 1]);
                float measuredHz = (times.Count - 1) / (times[times.Count - 1] - times[0]);
                var measured = VaultFrameMetrics.Measure(frames);
                var held = VaultFrameMetrics.Measure(heldFrames);
                var midArc = VaultFrameMetrics.Measure(frames.Where((_, i) => times[i] >= arcStart + 2f * Time.fixedDeltaTime).ToArray());
                var heldMidArc = VaultFrameMetrics.Measure(heldFrames.Where((_, i) => times[i] >= arcStart + 2f * Time.fixedDeltaTime).ToArray());
                TestContext.WriteLine($"VAULT_RENDER requestedHz=144; measuredHz={measuredHz:R}; maxFrameSeconds={maxDt:R}; ticks={distinctTicks}; actual={measured}; tickHeldControl={held}; midArc={midArc}; heldMidArc={heldMidArc}");
                Assert.That(measuredHz, Is.GreaterThan(100f), "Renderer did not run sufficiently faster than the 60 Hz tick.");
                Assert.That(maxDt, Is.LessThan(.01f), "A slow frame invalidated the 144 Hz measurement; rerun with adequate render capacity.");
                Assert.That(heldMidArc.ZeroMotionFrames, Is.GreaterThan(0), "Control must expose the old camera's sample-and-hold defect.");
                Assert.That(held.MaximumRatio, Is.GreaterThan(12f));
                Assert.That(held.MaximumSecondDifference, Is.GreaterThan(.7f));
                Assert.That(midArc.ZeroMotionFrames, Is.Zero);
                Assert.That(measured.MaximumRatio, Is.LessThan(12f));
                Assert.That(measured.MaximumSecondDifference, Is.LessThan(.7f));
            }
        }

        private void Supply()
        {
            bool jump = suppliedTicks++ == 30; // Warm up render/physics before the measured arc.
            run.ReceiveInput(new InputFrame(Vector2.zero, Vector2.zero,
                jump ? InputButtons.Jump : InputButtons.None,
                jump ? InputButtons.Jump : InputButtons.None,
                suppliedTicks == 32 ? InputButtons.Jump : InputButtons.None));
        }

        private void Progress(EntityId id, long tick, TraversalKind kind, float progress, bool active)
        {
            if (id != player.Id || kind != TraversalKind.Vault) return;
            if (arcStart < 0f && active) arcStart = Time.fixedTime - Time.fixedDeltaTime;
            latestHeight = cameraConfig.VaultHeight.Evaluate(progress);
        }

        private void Outcome(PlayerTraversalFact fact)
        {
            if (fact.Id != player.Id || fact.Kind != TraversalKind.Vault) return;
            if (fact.Succeeded) successes++; else failures++;
        }

        private void Rendered(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (camera != output || arcStart < 0f || lastRenderFrame == Time.frameCount) return;
            lastRenderFrame = Time.frameCount;
            float now = Time.time;
            if (now >= arcStart + duration + Time.fixedDeltaTime) return;
            // No resampling, presenter invocation, pose writes or dropped slow frames.
            frames.Add(camera.transform.position);
            heldFrames.Add(player.LastMovementSample.EyePosition + Vector3.up * latestHeight);
            times.Add(now);
            if (lastRecordedTick != player.LastMovementSample.Tick) distinctTicks++;
            lastRecordedTick = player.LastMovementSample.Tick;
        }

        private BoxCollider Box(string name, Vector3 center, Vector3 size)
        {
            var item = new GameObject("[Test] " + name);
            item.transform.SetParent(arrangement.transform, false);
            item.transform.localPosition = center;
            var collider = item.AddComponent<BoxCollider>(); collider.size = size;
            return collider;
        }

        private static T One<T>() where T : Component
            => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Vault benchmark timed out.");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            try
            {
                if (observing)
                {
                    RenderPipelineManager.endCameraRendering -= Rendered;
                    if (run != null)
                    {
                        run.BeforeTick -= Supply; run.PlayerFacts.TraversalProgressed -= Progress;
                        run.PlayerTraversalPublished -= Outcome; run.ReceiveInput(default);
                    }
                    observing = false;
                }
                if (arrangement != null) UnityEngine.Object.DestroyImmediate(arrangement);
            }
            finally
            {
                if (restoreSettings)
                {
                    QualitySettings.vSyncCount = previousVSync;
                    Application.targetFrameRate = previousTarget;
                    Application.runInBackground = previousBackground;
                    restoreSettings = false;
                }
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
