// ============================================================================
// PlayerTraversalIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Drives an authored TagArena route through real fixed ticks and collision probes,
//   then checks decisions replayed from its persisted input/probe/resolution segment.
//   Passive camera observations and original screenshots retain limb presentation
//   during those same natural slide/vault windows for a separate visual review.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player integration.
// KEY RESPONSIBILITIES:
//   - Exercise slide, slide-jump, waist vault and rebound using synthetic Run input.
//   - Retain actual collision results and compare replayed movement/traversal decisions.
//   - Observe actual focus, owner/input gates and recording transitions for failures.
//   - Bind unedited deferred Game View PNGs to Player camera renders and limb geometry.
// DEPENDENCIES:
//   - Player, Level/Hunter/Chase inspection, Run/Input services and the built TagArena.
//   - NUnit, Unity Test Framework, UnityEditor read-only asset provenance and file reads.
//   - Player camera render callbacks and screenshot requests; no render overrides.
// USAGE NOTES:
//   Coordinator owns the Unity lease. Test Framework isolates/restores the scene.
//   Hunters are deactivated for this free-movement segment and disappear at unload.
//   No teleport, asset generation, fake probes or fabricated movement resolutions.
//   Cursor/input gates and runInBackground are restored through normal teardown.
//   Saves normal .winput evidence; a failed trial is saved explicitly incomplete.
//   Diagnostic focus/update subscriptions are paired; observation never opens a gate.
//   Before scene load, requests real GameView focus once and requires 0.5 s stable
//   application/editor focus with render progress. Later focus loss still fails capture.
//   This does not prove hardware input, chased traversal, slide-gate clearance,
//   free-speed percentiles, all verbs or deterministic physics from input alone.
//   Up to two PNGs per naturally rendered Slide/Vault are diagnostic observations;
//   renderer visibility/frustum flags never stand in for pixels or human readability.
//   Screenshot requests, subsequent renders and file completion are recorded apart.
//   The observer allocates no texture or render target and changes no route/input.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using CameraDriver = Worsen.Presentation.Camera.CameraDriver;

namespace Worsen.Tests.Player
{
    public sealed class PlayerTraversalIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";

        [UnityTest]
        public IEnumerator FreeMovementTraversalsPersistAndReplayActualCollisionResults()
        {
            yield return new EnterPlayMode();
            // The Test Framework restores the outer iterator position after domain reload.
            yield return ExerciseArena();
        }

        private static IEnumerator ExerciseArena()
        {
            bool previousBackground = Application.runInBackground;
            RunSessionManager run = null;
            InputManager input = null;
            Trial trial = null;
            LimbRenderEvidence limbEvidence = null;
            bool captureStarted = false, captureSaved = false;
            var gateTrace = new CaptureGateTrace("PlayerTraversal");
            Application.runInBackground = true;
            try
            {
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty);
                yield return gateTrace.AdmitStableGameViewFocus();
                AsyncOperation load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build TagArena before running this fixture.");
                yield return Until(() => load.isDone && RunSessionManager.Instance != null &&
                    RunSessionManager.Instance.Scene == SceneKey.TagArena && RunSessionManager.Instance.Tick >= 3,
                    "TagArena did not become ready.");
                run = RunSessionManager.Instance;
                input = InputManager.Instance;
                Assert.That(input, Is.Not.Null);
                gateTrace.Mark("before fixture SetInputEnabled(false)");
                input.SetInputEnabled(false);
                gateTrace.Mark("after fixture SetInputEnabled(false)");
                foreach (HunterManager hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                {
                    Assert.That(hunter.gameObject.scene.path, Is.EqualTo(ArenaPath));
                    hunter.gameObject.SetActive(false);
                }
                Assert.That(HunterRegistry.Items.Count, Is.Zero, "Free movement requires no ticking Hunters.");
                PlayerManager player = One<PlayerManager>();
                ChaseManager chase = One<ChaseManager>();
                yield return Until(() => player.ReadOnlyState.Velocity.sqrMagnitude < 0.0001f &&
                    player.LastProbeRecord.Resolution.Grounded, "Player did not settle before capture.");
                Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                Assert.That(player.ReadOnlyState.Position.x, Is.InRange(-20.25f, -19.75f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));

                var root = new SerializedObject(One<TagArenaSceneRoot>());
                var profile = (PlayerProfile)root.FindProperty("_playerProfile").objectReferenceValue;
                string source = root.FindProperty("_sourceRevision").stringValue;
                string configHash = root.FindProperty("_configSnapshotHash").stringValue;
                Assert.That(source, Is.Not.Empty);
                Assert.That(configHash, Is.Not.Empty);
                Assert.That(profile, Is.Not.Null);
                var driver = new SerializedObject(player.GetComponent<PlayerDriver>());
                var driverConfig = (PlayerMoverDriverConfig)driver.FindProperty("_config").objectReferenceValue;
                Assert.That(driverConfig, Is.Not.Null);
                configHash += ";profile:" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(profile)) +
                    ";driver:" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(driverConfig));
                LevelMarker[] markers = UnityEngine.Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);
                LevelMarker vault = markers.Single(marker => marker.SurfaceId == 201);
                LevelMarker rebound = markers.Single(marker => marker.SurfaceId == 202);
                Assert.That(vault.Kind, Is.EqualTo(TraversalSurfaceKind.Vault));
                Assert.That(rebound.Kind, Is.EqualTo(TraversalSurfaceKind.Rebound));
                Assert.That(vault.Target, Is.EqualTo(new Vector3(-4f, 0f, -4.5f)));

                trial = new Trial(run, input, player, chase, profile, driverConfig, vault.Target,
                    rebound.GetComponent<Collider>().bounds.min.x);
                var metadata = new RunCaptureMetadata("synthetic-free-traversal-" + Guid.NewGuid().ToString("N"),
                    run.Seed, Time.fixedDeltaTime, source, configHash,
                    "synthetic Run input; scene Hunters disabled; PlayerController performs no random draws", run.Tick);
                gateTrace.Mark("before synthetic BeginRecording");
                input.BeginRecording(metadata);
                gateTrace.Mark("after synthetic BeginRecording");
                captureStarted = true;
                limbEvidence = new LimbRenderEvidence(run, player, metadata);
                run.BeforeTick += trial.BeforeTick;
                run.PlayerProbeRecorded += trial.Record;
                run.ChaseStarted += trial.ChaseStarted;
                float deadline = Time.realtimeSinceStartup + 90f;
                for (int frame = 0; frame < 7200 && !trial.Done && trial.Failure.Length == 0 &&
                    Time.realtimeSinceStartup < deadline; frame++) yield return null;
                captureSaved = trial.CaptureSaved;
                Assert.That(trial.Failure, Is.Empty, trial.Describe());
                Assert.That(trial.Done, Is.True, "Traversal timed out: " + trial.Describe());
                Assert.That(trial.Chases, Is.Zero);
                Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                Assert.That(trial.SawReducedCapsule && trial.SawSlideJump && trial.SawAir,
                    Is.True, "The real slide/capsule/jump chain was incomplete.");
                Assert.That(trial.Vaults, Is.EqualTo(1));
                Assert.That(trial.Rebounds, Is.EqualTo(1));
                Assert.That(trial.Samples.Count, Is.InRange(60, 1800));
                Assert.That(input.LastRecordingError, Is.Empty, gateTrace.Describe());
                Assert.That(captureSaved, Is.True, "The exact final fixed tick was not saved.");
                string path = trial.CapturePath;
                Assert.That(File.Exists(path), Is.True);
                Assert.That(new InputRecordingPresenter().TryDecode(File.ReadAllBytes(path), out var decodedMetadata,
                    out var records, out string error), Is.True, error);
                Assert.That(decodedMetadata.SourceRevision, Is.EqualTo(source));
                Assert.That(decodedMetadata.ConfigSnapshotHash, Is.EqualTo(configHash));
                Assert.That(decodedMetadata.StartTick, Is.EqualTo(metadata.StartTick));
                Assert.That(records.Length, Is.EqualTo(trial.Samples.Count));
                trial.AssertReplay(records, decodedMetadata.Seed);
                TestContext.Progress.WriteLine("Synthetic free traversal recording: " + path +
                    "; ticks=" + records.Length + "; resolved vaults=1; rebounds=1; chased acceptance not tested.");
            }
            finally
            {
                try
                {
                    if (run != null && trial != null)
                    {
                        run.BeforeTick -= trial.BeforeTick;
                        run.PlayerProbeRecorded -= trial.Record;
                        run.ChaseStarted -= trial.ChaseStarted;
                    }
                    if (input != null)
                    {
                        if (captureStarted && !captureSaved && trial != null && trial.Samples.Count > 0)
                        {
                            input.SaveRecording(run.Tick, false);
                            TestContext.Progress.WriteLine("Incomplete synthetic traversal recording: " + input.LastRecordingPath);
                        }
                        input.SetInputEnabled(false);
                    }
                }
                finally
                {
                    limbEvidence?.Finish(input == null ? "" : input.LastRecordingPath,
                        trial != null && trial.CaptureSaved, trial == null ? "Trial not constructed." : trial.Failure);
                    gateTrace.Dispose();
                    Application.runInBackground = previousBackground;
                }
            }
        }

        // Test-only passive render diagnostics. It never controls the simulation,
        // capture gates, camera, limbs or the native recording/replay assertions.
        private sealed class LimbRenderEvidence
        {
            private const int MaximumObservations = 7200;
            private readonly RunSessionManager run;
            private readonly PlayerManager player;
            private readonly RenderReport report;
            private readonly string reportPath;
            private readonly List<LimbTarget> targets = new List<LimbTarget>();
            private UnityEngine.Camera outputCamera;
            private Transform visualRoot;
            private int lastFrame = -1;
            private bool attached, finished;

            public LimbRenderEvidence(RunSessionManager run, PlayerManager player, RunCaptureMetadata metadata)
            {
                this.run = run; this.player = player;
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "PLAN-003",
                    "limb-visual", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")));
                reportPath = Path.Combine(directory, "render-evidence.json");
                report = new RenderReport { directory = directory, sessionId = metadata.SessionId, seed = metadata.Seed,
                    startTick = metadata.StartTick, fixedDeltaTime = metadata.FixedDeltaTime, sourceRevision = metadata.SourceRevision,
                    configSnapshotHash = metadata.ConfigSnapshotHash, randomConsumptionOrder = metadata.RandomConsumptionOrder };
                try
                {
                    Directory.CreateDirectory(directory);
                    report.sceneDependencyHash = AssetDatabase.GetAssetDependencyHash(ArenaPath).ToString();
                    foreach (string path in new[] { ArenaPath, "Assets/Prefabs/Player/Player.prefab",
                        "Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs", "Assets/Scripts/Domain/Player/Driver/PlayerLimbStandIn.cs",
                        "Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs", "Assets/Scripts/Domain/Player/Driver/PlayerMoverPresenter.cs",
                        "Assets/Scripts/Presentation/Camera/Driver/CameraDriver.cs", "Assets/Scripts/Presentation/Camera/Driver/CameraFeedbackPresenter.cs",
                        "Assets/Resources/ScriptableObjects/Domain/Player/PlayerMoverDriverConfig.asset",
                        "Assets/Resources/ScriptableObjects/Presentation/Camera/CameraDriverConfig.asset" })
                        report.sourceHashes.Add(path + ":" + Hash(path));
                    using var driver = new SerializedObject(player.GetComponent<PlayerDriver>());
                    visualRoot = (Transform)driver.FindProperty("_visualRoot").objectReferenceValue;
                    var limbs = (PlayerLimbStandIn)driver.FindProperty("_limbs").objectReferenceValue;
                    if (limbs == null) throw new InvalidOperationException("PlayerDriver has no serialized limb stand-in.");
                    using var limbFields = new SerializedObject(limbs);
                    foreach (string name in new[] { "_leftHand", "_rightHand", "_leftFoot", "_rightFoot" })
                    {
                        var item = (GameObject)limbFields.FindProperty(name).objectReferenceValue;
                        if (item == null) throw new InvalidOperationException("Missing limb reference " + name);
                        var mesh = item.GetComponent<MeshFilter>();
                        targets.Add(new LimbTarget { role = name, item = item, renderer = item.GetComponent<Renderer>(), mesh = mesh == null ? null : mesh.sharedMesh });
                    }
                    CameraDriver[] cameras = UnityEngine.Object.FindObjectsByType<CameraDriver>(FindObjectsSortMode.None)
                        .Where(item => item.gameObject.scene.path == ArenaPath).ToArray();
                    if (cameras.Length != 1) throw new InvalidOperationException("Expected one authored Player CameraDriver.");
                    using var cameraFields = new SerializedObject(cameras[0]);
                    outputCamera = (UnityEngine.Camera)cameraFields.FindProperty("_outputCamera").objectReferenceValue;
                    if (outputCamera == null || !outputCamera.isActiveAndEnabled) throw new InvalidOperationException("Player output camera is not enabled.");
                    report.cameraName = outputCamera.name; report.cameraId = outputCamera.GetInstanceID();
                    run.PlayerProbeRecorded += ObserveTick;
                    RenderPipelineManager.endCameraRendering += ObserveCamera;
                    attached = true;
                }
                catch (Exception error) { Error("Observer setup: " + error); }
            }

            private void ObserveTick(InputProbeRecord record)
            {
                if (finished) return;
                try
                {
                    if (report.ticks.Count >= MaximumObservations) { report.omittedTicks++; return; }
                    var movement = player.LastMovementSample;
                    report.ticks.Add(new TickObservation { tick = record.Tick, frame = Time.frameCount, movement = movement.MovementState.ToString(),
                        inputHeld = (int)record.Input.Held, inputPressed = (int)record.Input.Pressed, inputReleased = (int)record.Input.Released,
                        inputMove = record.Input.Move, inputLook = record.Input.LookDelta, position = record.Resolution.Position,
                        velocity = record.Resolution.Velocity, stateVelocity = player.ReadOnlyState.Velocity, eye = record.Resolution.EyePosition,
                        grounded = record.Resolution.Grounded, capsuleHeight = player.GetComponent<CapsuleCollider>().height,
                        limbsActive = targets.Select(target => target.item != null && target.item.activeInHierarchy).ToArray() });
                }
                catch (Exception error) { Error("Tick observation: " + error); }
            }

            private void ObserveCamera(ScriptableRenderContext context, UnityEngine.Camera camera)
            {
                if (finished || camera == null) return;
                try
                {
                    if (camera.cameraType == CameraType.Game)
                    {
                        if (report.gameCameraPasses.Count < MaximumObservations)
                            report.gameCameraPasses.Add(new CameraPass { frame = Time.frameCount, cameraId = camera.GetInstanceID(), name = camera.name,
                                selectedOutput = camera == outputCamera, targetDisplay = camera.targetDisplay, pixelRect = camera.pixelRect,
                                targetTexture = camera.targetTexture == null ? "none" : camera.targetTexture.name });
                        else report.omittedCameraPasses++;
                    }
                    if (camera != outputCamera || lastFrame == Time.frameCount) return;
                    lastFrame = Time.frameCount;
                    RenderObservation observation = Snapshot();
                    if (report.renders.Count < MaximumObservations) report.renders.Add(observation); else report.omittedRenders++;
                    foreach (var capture in report.captures)
                    {
                        if (capture.firstSubsequentRender == null && observation.frame > capture.request.frame)
                            capture.firstSubsequentRender = observation;
                        PollPng(capture, observation, "selected camera endCameraRendering");
                    }
                    if (observation.movement != MovementState.Slide.ToString() && observation.movement != MovementState.Vault.ToString()) return;
                    var previous = report.captures.Where(capture => capture.label == observation.movement).ToArray();
                    if (previous.Length >= 2 || (previous.Length > 0 && observation.frame - previous[previous.Length - 1].request.frame < 2)) return;
                    if (outputCamera.targetTexture != null || outputCamera.cameraType != CameraType.Game)
                    { Error("PNG request omitted: selected output is not a direct Game camera backbuffer."); return; }
                    var request = new CaptureObservation { label = observation.movement,
                        path = Path.Combine(report.directory, observation.movement + "-" + (previous.Length + 1).ToString("00") +
                            "-frame" + observation.frame + "-tick" + observation.tick + ".png"), request = observation };
                    report.captures.Add(request);
                    // Unity owns deferred Game View capture. No camera render, target
                    // replacement, pixel editing, simulation wait or state manufacture.
                    ScreenCapture.CaptureScreenshot(request.path, 1);
                }
                catch (Exception error) { Error("Render observation/capture request: " + error); }
            }

            private RenderObservation Snapshot()
            {
                var sample = player.LastMovementSample;
                var observation = new RenderObservation { frame = Time.frameCount, tick = run.Tick, movementTick = sample.Tick,
                    realtimeSeconds = Time.realtimeSinceStartupAsDouble, presentationSeconds = Time.timeAsDouble, presentationDelta = Time.deltaTime,
                    movement = sample.MovementState.ToString(), position = sample.Position, velocity = sample.Velocity,
                    stateVelocity = player.ReadOnlyState.Velocity, eye = sample.EyePosition, heading = sample.HeadingDegrees,
                    inputLockSeconds = sample.InputLockSeconds, lookBack = sample.LookBack,
                    cameraId = outputCamera.GetInstanceID(), cameraPosition = outputCamera.transform.position,
                    cameraRotation = outputCamera.transform.rotation, cameraForward = outputCamera.transform.forward,
                    cameraUp = outputCamera.transform.up, verticalFov = outputCamera.fieldOfView, aspect = outputCamera.aspect,
                    pixelRect = outputCamera.pixelRect, screenWidth = Screen.width, screenHeight = Screen.height,
                    nearClip = outputCamera.nearClipPlane, farClip = outputCamera.farClipPlane, cullingMask = outputCamera.cullingMask,
                    projection = outputCamera.projectionMatrix, worldToCamera = outputCamera.worldToCameraMatrix,
                    visualPosition = visualRoot == null ? Vector3.zero : visualRoot.position,
                    visualRotation = visualRoot == null ? Quaternion.identity : visualRoot.rotation };
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(outputCamera);
                foreach (var target in targets)
                {
                    if (target.item == null) continue;
                    var renderer = target.renderer;
                    var limb = new LimbObservation { role = target.role, objectName = target.item.name, objectId = target.item.GetInstanceID(),
                        activeSelf = target.item.activeSelf, activeInHierarchy = target.item.activeInHierarchy,
                        localPosition = target.item.transform.localPosition, worldPosition = target.item.transform.position,
                        worldRotation = target.item.transform.rotation, lossyScale = target.item.transform.lossyScale,
                        layer = target.item.layer, cameraIncludesLayer = (outputCamera.cullingMask & (1 << target.item.layer)) != 0,
                        expectedActiveForMovement = false,
                        hasRenderer = renderer != null, rendererEnabled = renderer != null && renderer.enabled,
                        forceRenderingOff = renderer != null && renderer.forceRenderingOff,
                        rendererIsVisibleAnyCamera = renderer != null && renderer.isVisible,
                        rendererWorldBounds = renderer == null ? default : renderer.bounds,
                        boundsIntersectPlayerCameraFrustum = renderer != null && GeometryUtility.TestPlanesAABB(planes, renderer.bounds),
                        meshName = target.mesh == null ? "missing" : target.mesh.name,
                        meshLocalBounds = target.mesh == null ? default : target.mesh.bounds,
                        meshVertexCount = target.mesh == null ? 0 : target.mesh.vertexCount,
                        centerViewport = outputCamera.WorldToViewportPoint(target.item.transform.position) };
                    if (target.mesh != null)
                    {
                        Bounds bounds = target.mesh.bounds;
                        limb.meshBoundsCornersWorld = new Vector3[8]; limb.meshBoundsCornersViewport = new Vector3[8];
                        for (int index = 0; index < 8; index++)
                        {
                            var corner = bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f));
                            Vector3 world = target.item.transform.TransformPoint(corner);
                            limb.meshBoundsCornersWorld[index] = world;
                            limb.meshBoundsCornersViewport[index] = outputCamera.WorldToViewportPoint(world);
                        }
                    }
                    if (renderer != null)
                        limb.materials = renderer.sharedMaterials.Select(material => material == null ? "missing" :
                            material.name + ";shader=" + (material.shader == null ? "missing" : material.shader.name) + ";queue=" + material.renderQueue).ToArray();
                    observation.limbs.Add(limb);
                }
                return observation;
            }

            private void PollPng(CaptureObservation capture, RenderObservation observation, string phase)
            {
                if (capture.fileComplete || !File.Exists(capture.path)) return;
                try
                {
                    using (var stream = new FileStream(capture.path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (stream.Length < 45) return;
                        byte[] header = new byte[24], tail = new byte[12];
                        if (stream.Read(header, 0, header.Length) != header.Length) return;
                        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                        if (!header.Take(8).SequenceEqual(signature)) { Error("PNG signature mismatch: " + capture.path); return; }
                        stream.Seek(-12, SeekOrigin.End);
                        if (stream.Read(tail, 0, tail.Length) != tail.Length) return;
                        byte[] iend = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
                        if (!tail.SequenceEqual(iend)) return;
                        capture.width = BigEndian(header, 16); capture.height = BigEndian(header, 20); capture.bytes = stream.Length;
                        if (capture.width <= 0 || capture.height <= 0) return;
                        stream.Position = 0;
                        using (var sha = SHA256.Create()) capture.sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                        capture.fileComplete = true;
                    }
                    if (capture.fileComplete)
                    {
                        capture.observedComplete = observation; capture.fileObservedUtc = DateTime.UtcNow.ToString("o");
                        capture.fileObservedPhase = phase; capture.fileObservedFrame = Time.frameCount;
                        capture.fileObservedTick = run == null ? -1 : run.Tick;
                        capture.fileObservedRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
                    }
                }
                catch (IOException) { /* A deferred writer may not have finished; retain the pending request. */ }
            }

            public void Finish(string recordingPath, bool captureSaved, string trialFailure)
            {
                if (finished) return;
                finished = true;
                if (attached)
                { run.PlayerProbeRecorded -= ObserveTick; RenderPipelineManager.endCameraRendering -= ObserveCamera; attached = false; }
                try
                {
                    foreach (var capture in report.captures) PollPng(capture, null, "fixture finalization outside render callback");
                    report.nativeRecordingPath = recordingPath; report.nativeCaptureSaved = captureSaved; report.trialFailure = trialFailure;
                    if (!string.IsNullOrEmpty(recordingPath) && File.Exists(recordingPath)) report.nativeRecordingSha256 = Hash(recordingPath);
                    report.finishedUtc = DateTime.UtcNow.ToString("o");
                    report.finishObservedRunTick = run == null ? -1 : run.Tick;
                    report.pendingPngs = report.captures.Count(capture => !capture.fileComplete);
                    Directory.CreateDirectory(report.directory);
                    File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                    TestContext.WriteLine("Passive Player limb render evidence: " + reportPath);
                    foreach (var capture in report.captures)
                        TestContext.WriteLine("Original deferred " + capture.label + " PNG: " + capture.path + "; fileComplete=" + capture.fileComplete +
                            "; requestFrame=" + capture.request.frame + "; requestTick=" + capture.request.tick + "; sha256=" + capture.sha256);
                }
                catch (Exception error) { TestContext.WriteLine("Limb render diagnostic finalization failed: " + error); }
            }

            private void Error(string error) { if (report.errors.Count < 32) report.errors.Add(error); else report.omittedErrors++; }
            private static string Hash(string path)
            { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
            private static int BigEndian(byte[] value, int offset) => (value[offset] << 24) | (value[offset + 1] << 16) | (value[offset + 2] << 8) | value[offset + 3];

            private sealed class LimbTarget { public string role; public GameObject item; public Renderer renderer; public Mesh mesh; }
            [Serializable] private sealed class RenderReport
            {
                public string directory, sessionId, sourceRevision, configSnapshotHash, randomConsumptionOrder, sceneDependencyHash;
                public int seed, cameraId, omittedTicks, omittedRenders, omittedCameraPasses, omittedErrors, pendingPngs;
                public long startTick, finishObservedRunTick;
                public float fixedDeltaTime;
                public string cameraName, nativeRecordingPath, nativeRecordingSha256, trialFailure, finishedUtc;
                public bool nativeCaptureSaved;
                public string scope = "Passive diagnostic; original deferred Game View PNG requests follow the selected Player camera render. Renderer.isVisible includes other cameras; frustum/bounds checks are geometric, not pixel visibility or human readability. No route/input/timing/renderer/camera overrides. Pending files and observation limits remain explicit.";
                public List<string> sourceHashes = new List<string>(), errors = new List<string>();
                public List<TickObservation> ticks = new List<TickObservation>();
                public List<RenderObservation> renders = new List<RenderObservation>();
                public List<CameraPass> gameCameraPasses = new List<CameraPass>();
                public List<CaptureObservation> captures = new List<CaptureObservation>();
            }
            [Serializable] private sealed class TickObservation
            {
                public long tick;
                public int frame, inputHeld, inputPressed, inputReleased;
                public string movement;
                public Vector2 inputMove, inputLook;
                public Vector3 position, velocity, stateVelocity, eye;
                public bool grounded;
                public float capsuleHeight;
                public bool[] limbsActive;
            }
            [Serializable] private sealed class RenderObservation
            {
                public long tick, movementTick;
                public int frame, cameraId, screenWidth, screenHeight, cullingMask;
                public double realtimeSeconds, presentationSeconds;
                public float presentationDelta, verticalFov, aspect, nearClip, farClip, heading, inputLockSeconds;
                public bool lookBack;
                public string movement;
                public Vector3 position, velocity, stateVelocity, eye, cameraPosition, cameraForward, cameraUp, visualPosition;
                public Quaternion cameraRotation, visualRotation;
                public Rect pixelRect;
                public Matrix4x4 projection, worldToCamera;
                public List<LimbObservation> limbs = new List<LimbObservation>();
            }
            [Serializable] private sealed class LimbObservation
            {
                public string role, objectName, meshName;
                public int objectId, layer, meshVertexCount;
                public bool activeSelf, activeInHierarchy, expectedActiveForMovement, hasRenderer, rendererEnabled, forceRenderingOff;
                public bool rendererIsVisibleAnyCamera, boundsIntersectPlayerCameraFrustum, cameraIncludesLayer;
                public Vector3 localPosition, worldPosition, lossyScale, centerViewport;
                public Quaternion worldRotation;
                public Bounds rendererWorldBounds, meshLocalBounds;
                public Vector3[] meshBoundsCornersWorld, meshBoundsCornersViewport;
                public string[] materials;
            }
            [Serializable] private sealed class CameraPass
            { public int frame, cameraId, targetDisplay; public string name, targetTexture; public bool selectedOutput; public Rect pixelRect; }
            [Serializable] private sealed class CaptureObservation
            {
                public string label, path, sha256, fileObservedUtc, fileObservedPhase;
                public bool fileComplete;
                public int width, height, fileObservedFrame;
                public long bytes, fileObservedTick;
                public double fileObservedRealtimeSeconds;
                public RenderObservation request, firstSubsequentRender, observedComplete;
                public string timing = "Requested after selected Player camera endCameraRendering. Unity defers full Game View capture until frame end; original pixels may include UI and other Game camera passes. Request, next render and file-completion observations are distinct; disk-write time is not pixel timestamp.";
            }
        }

        private enum Stage { Accelerate, Slide, SlideJump, ToVault, Vault, ToRebound, Rebound, Settle, Complete }

        private sealed class Sample
        {
            public InputProbeRecord Record;
            public PlayerMovementSample Movement;
            public Vector3 StateVelocity;
            public PlayerTraversalFact[] Facts;
        }

        private sealed class Trial
        {
            private readonly RunSessionManager run;
            private readonly InputManager input;
            private readonly PlayerManager player;
            private readonly ChaseManager chase;
            private readonly PlayerProfile profile;
            private readonly PlayerMoverDriverConfig config;
            private readonly Vector3 vaultTarget, initialPosition;
            private readonly float reboundFace, initialHeading;
            private readonly CapsuleCollider capsule;
            private Stage stage;
            private int stageTicks, waypoint;
            private bool vaultJump, firstReboundJump, secondReboundJump, reboundLanded;
            private InputButtons previousHeld;
            private MovementState previousMovement;
            public readonly List<Sample> Samples = new List<Sample>();
            public string Failure = "";
            public int Chases, Vaults, Rebounds;
            public bool SawReducedCapsule, SawSlideJump, SawAir;
            public bool CaptureSaved;
            public string CapturePath = "";
            public bool Done => stage == Stage.Complete;

            public Trial(RunSessionManager run, InputManager input, PlayerManager player, ChaseManager chase, PlayerProfile profile,
                PlayerMoverDriverConfig config, Vector3 vaultTarget, float reboundFace)
            {
                this.run = run; this.input = input; this.player = player; this.chase = chase; this.profile = profile;
                this.config = config; this.vaultTarget = vaultTarget; this.reboundFace = reboundFace;
                initialPosition = player.ReadOnlyState.Position; initialHeading = player.ReadOnlyState.HeadingDegrees;
                previousMovement = player.ReadOnlyState.MovementState;
                capsule = player.GetComponent<CapsuleCollider>();
            }

            public void BeforeTick()
            {
                if (Done || Failure.Length > 0) { run.ReceiveInput(default); return; }
                Vector2 move = Vector2.up;
                float heading = 90f;
                InputButtons press = InputButtons.None, held = InputButtons.Sprint;
                switch (stage)
                {
                    case Stage.Slide: if (stageTicks == 0) press = InputButtons.Crouch; held = InputButtons.Sprint | InputButtons.Crouch; break;
                    case Stage.SlideJump: if (stageTicks == 0) press = InputButtons.Jump; break;
                    case Stage.ToVault:
                    case Stage.ToRebound:
                        Vector3 delta = Waypoint() - player.ReadOnlyState.Position; delta.y = 0f;
                        heading = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                        move = delta.magnitude <= 0.25f ? Vector2.zero : Vector2.up * Mathf.Min(1f, delta.magnitude * 1.5f);
                        held = InputButtons.None; break;
                    case Stage.Vault:
                        heading = 0f; held = InputButtons.None;
                        MovementProbe probe = player.LastProbeRecord.Probe;
                        if (!vaultJump && probe.VaultCandidate && probe.VaultClearance > 0f &&
                            Vector3.Distance(player.ReadOnlyState.Position, vaultTarget) <= 2.4f)
                        { press = InputButtons.Jump; vaultJump = true; }
                        if (vaultJump) move = Vector2.zero;
                        break;
                    case Stage.Rebound:
                        MovementProbe wall = player.LastProbeRecord.Probe;
                        if (!firstReboundJump && player.ReadOnlyState.Position.x >= reboundFace - 1.7f)
                        { press = InputButtons.Jump; firstReboundJump = true; }
                        else if (firstReboundJump && !secondReboundJump && player.ReadOnlyState.MovementState == MovementState.Air &&
                            wall.WallDetected && wall.WallId == 202 && wall.WallDistance <= profile.ReboundDistance &&
                            wall.WallAngleDegrees <= profile.ReboundAngle)
                        { press = InputButtons.Jump; secondReboundJump = true; }
                        break;
                    case Stage.Settle: move = Vector2.zero; break;
                }
                held |= press;
                var frame = new InputFrame(move, new Vector2(Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, heading), 0f),
                    held, press, previousHeld & ~held);
                previousHeld = held;
                run.ReceiveInput(frame);
            }

            public void Record(InputProbeRecord record)
            {
                if (Done || Failure.Length > 0) return;
                var sample = new Sample { Record = record, Movement = player.LastMovementSample,
                    StateVelocity = player.ReadOnlyState.Velocity, Facts = player.LastTraversalFacts.ToArray() };
                Samples.Add(sample); stageTicks++;
                if (Samples.Count >= 1800 || stageTicks > 480) Fail("Bounded fixed-tick limit exceeded.");
                if (!record.Resolution.Present || record.DeltaTime != Time.fixedDeltaTime ||
                    !Finite(record.Resolution.Position) || !Finite(record.Resolution.Velocity) || record.Resolution.Position.y < -0.1f)
                    Fail("Missing/nonfinite collision resolution or a fall below the authored floor.");
                if (chase.ReadOnlyState.HasActiveChase || player.ReadOnlyState.Health != profile.MaximumHealth)
                    Fail("The free-movement trial entered chase or took damage.");
                SawReducedCapsule |= sample.Movement.MovementState == MovementState.Slide &&
                    Mathf.Abs(capsule.height - config.Height * config.SlideHeightRatio) < 0.001f;
                SawAir |= !record.Probe.Grounded && !record.Resolution.Grounded && record.Resolution.Position.y > initialPosition.y + 0.1f;
                bool landed = sample.Facts.Any(fact => fact.Kind == TraversalKind.Land && fact.Succeeded);
                foreach (PlayerTraversalFact fact in sample.Facts)
                {
                    if (fact.Kind == TraversalKind.Jump && fact.Succeeded && previousMovement == MovementState.Slide)
                        SawSlideJump = !record.Resolution.Grounded && record.Resolution.Velocity.y > 0f;
                    if (fact.Kind == TraversalKind.Vault)
                    {
                        if (!fact.Succeeded || Vector3.Distance(record.Resolution.Position, vaultTarget) > profile.VaultCompletionTolerance + 0.001f)
                            Fail("Authored vault did not resolve at its landing target.");
                        else Vaults++;
                    }
                    if (fact.Kind == TraversalKind.Rebound)
                    {
                        if (!fact.Succeeded || !record.Probe.WallDetected || record.Probe.WallId != 202 ||
                            Vector3.Dot(record.Resolution.Velocity, record.Probe.WallNormal) <= 0f)
                            Fail("Rebound did not reflect the actual committed velocity away from marker 202.");
                        else Rebounds++;
                    }
                }
                switch (stage)
                {
                    case Stage.Accelerate:
                        if (Speed() >= Mathf.Max(profile.SlideMinimumSpeed, profile.SprintSpeed * 0.99f)) Advance(Stage.Slide); break;
                    case Stage.Slide: if (stageTicks >= 10) Advance(Stage.SlideJump); break;
                    case Stage.SlideJump: if (landed && SawSlideJump) Advance(Stage.ToVault); break;
                    case Stage.ToVault:
                    case Stage.ToRebound:
                        Vector3 distance = Waypoint() - player.ReadOnlyState.Position; distance.y = 0f;
                        if (distance.magnitude <= 0.25f && Speed() <= 0.3f && record.Resolution.Grounded)
                        { if (++waypoint == 2) Advance(stage == Stage.ToVault ? Stage.Vault : Stage.Rebound); }
                        break;
                    case Stage.Vault: if (Vaults == 1) Advance(Stage.ToRebound); break;
                    case Stage.Rebound: if (Rebounds == 1) Advance(Stage.Settle); break;
                    case Stage.Settle:
                        reboundLanded |= landed;
                        if (reboundLanded && record.Resolution.Grounded && Speed() < 0.1f) Advance(Stage.Complete); break;
                }
                previousMovement = sample.Movement.MovementState;
                if (Done && Failure.Length == 0)
                {
                    // Close on this committed tick; a render frame may contain further fixed ticks.
                    CaptureSaved = input.SaveRecording(record.Tick, true);
                    CapturePath = input.LastRecordingPath;
                    if (!CaptureSaved) Fail("The completed segment could not be saved: " + input.LastRecordingError);
                }
            }

            public void ChaseStarted(ChaseFact fact) { Chases++; Fail("ChaseStarted during free movement."); }
            public string Describe() => "stage=" + stage + "; stageTicks=" + stageTicks + "; total=" + Samples.Count +
                "; position=" + player.ReadOnlyState.Position + "; movement=" + player.ReadOnlyState.MovementState;
            private void Fail(string message) { if (Failure.Length == 0) Failure = message + " " + Describe(); }
            private void Advance(Stage next) { stage = next; stageTicks = waypoint = 0; }
            private float Speed() { Vector3 velocity = player.ReadOnlyState.Velocity; return new Vector2(velocity.x, velocity.z).magnitude; }
            private Vector3 Waypoint() => stage == Stage.ToVault
                ? (waypoint == 0 ? new Vector3(-9f, 0f, -8f) : new Vector3(-4f, 0f, -8f))
                : (waypoint == 0 ? new Vector3(-9f, 0f, -4.5f) : new Vector3(-9f, 0f, 3f));

            public void AssertReplay(InputProbeRecord[] records, int seed)
            {
                var state = new PlayerBehaviorState();
                var replay = new PlayerController(state, profile, new System.Random(seed));
                replay.Reset(player.Id, initialPosition, initialHeading);
                for (int i = 0; i < records.Length; i++)
                {
                    Sample expected = Samples[i];
                    Assert.That(records[i], Is.EqualTo(expected.Record), "Persisted record at " + i);
                    replay.Replay(records[i]);
                    Assert.That(state.Position, Is.EqualTo(expected.Movement.Position), "Resolved position at " + i);
                    Assert.That(state.Velocity, Is.EqualTo(expected.StateVelocity), "Decision velocity at " + i);
                    Assert.That(state.MovementState, Is.EqualTo(expected.Movement.MovementState), "Decision state at " + i);
                    Assert.That(state.HeadingDegrees, Is.EqualTo(expected.Movement.HeadingDegrees), "Heading at " + i);
                    Assert.That(state.LastMovementSample.InputLockSeconds, Is.EqualTo(expected.Movement.InputLockSeconds), "Lock at " + i);
                    Assert.That(state.LastTraversalFacts.ToArray(), Is.EqualTo(expected.Facts), "Resolved facts at " + i);
                }
                Assert.That(records.Any(record => record.Probe.VaultCandidate && record.Probe.VaultTarget == vaultTarget), Is.True);
                Assert.That(records.Any(record => record.Probe.WallDetected && record.Probe.WallId == 202), Is.True);
            }
            private static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
        }

        private static T One<T>() where T : Component
        {
            T[] objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(objects.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return objects[0];
        }
        private static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            for (int frame = 0; frame < 3600 && !condition() && Time.realtimeSinceStartup < deadline; frame++) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }

    // Test-only shared diagnostics. Reflects passive Input state without changing it;
    // records real global focus callbacks and state changes observed by Editor update.
    internal sealed class CaptureGateTrace : IDisposable
    {
        private static readonly FieldInfo DriverState = typeof(PlayerInputDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo RecorderState = typeof(InputRecorder).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<string> rows = new List<string>();
        private readonly string path;
        private string previous = "";
        private int omitted;
        private int focusTransitions;
        private bool disposed;

        public CaptureGateTrace(string label)
        {
            path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "GoalCompletion", "capture-flake",
                label + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".txt"));
            Application.focusChanged += OnPlayerFocus;
            EditorApplication.focusChanged += OnEditorFocus;
            EditorApplication.update += Poll;
            Mark("observer attached");
        }

        private void OnPlayerFocus(bool focused) { focusTransitions++; Mark("Application.focusChanged=" + focused); }
        private void OnEditorFocus(bool focused) { focusTransitions++; Mark("EditorApplication.focusChanged=" + focused); }
        private void Poll() => Snapshot("observed state change", false);
        public void Mark(string reason) => Snapshot(reason, true);

        public IEnumerator AdmitStableGameViewFocus()
        {
            EditorWindow gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().FullName == "UnityEditor.GameView");
            Assert.That(gameView, Is.Not.Null, "An existing Game View is required for actual focus admission. " + Describe());
            Mark("request actual GameView.Focus once before scene/capture");
            gameView.Focus();
            double deadline = Time.realtimeSinceStartupAsDouble + 10.0;
            double stableSince = -1.0;
            int stableFrame = -1, observedTransitions = focusTransitions;
            for (int frame = 0; frame < 6000 && Time.realtimeSinceStartupAsDouble < deadline; frame++)
            {
                bool focused = Application.isFocused && UnityEditorInternal.InternalEditorUtility.isApplicationActive &&
                    EditorWindow.focusedWindow == gameView;
                if (!focused || observedTransitions != focusTransitions)
                { stableSince = -1.0; stableFrame = -1; }
                observedTransitions = focusTransitions;
                if (focused && stableSince < 0.0)
                { stableSince = Time.realtimeSinceStartupAsDouble; stableFrame = Time.frameCount; }
                if (focused && Time.realtimeSinceStartupAsDouble - stableSince >= 0.5 && Time.frameCount > stableFrame)
                {
                    Mark("actual GameView focus admitted after stable 0.5 s and render progress");
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Actual Game View/application/editor focus did not remain stable for 0.5 s within 10 s. " +
                "Keep Unity foreground for the capture window; no focus callback or capture gate was overridden. " + Describe());
        }

        private void Snapshot(string reason, bool force)
        {
            if (disposed) return;
            try
            {
                InputManager manager = InputManager.Instance;
                PlayerInputDriver driver = manager == null ? null : manager.GetComponent<PlayerInputDriver>();
                InputRecorder recorder = manager == null ? null : manager.GetComponent<InputRecorder>();
                var gates = driver == null ? null : (InputDriverState)DriverState.GetValue(driver);
                var recording = recorder == null ? null : (InputReplayDriverState)RecorderState.GetValue(recorder);
                EditorWindow window = EditorWindow.focusedWindow;
                string state = "appFocus=" + Application.isFocused + "; editorFocus=" + UnityEditorInternal.InternalEditorUtility.isApplicationActive +
                    "; window=" + (window == null ? "none" : window.GetType().FullName + ":" + window.titleContent.text) +
                    "; manager=" + (manager == null ? "none" : manager.GetInstanceID() + "/enabled=" + manager.enabled + "/active=" + manager.gameObject.activeInHierarchy) +
                    "; driver=" + (driver == null ? "none" : driver.GetInstanceID() + "/enabled=" + driver.enabled + "/active=" + driver.gameObject.activeInHierarchy) +
                    "; gates=" + (gates == null ? "none" : "input=" + gates.InputEnabled + "/owner=" + gates.OwnerEnabled + "/focus=" + gates.HasFocus) +
                    "; recorder=" + (recording == null ? "none" : "recording=" + recording.Recording + "/complete=" + recording.CaptureComplete +
                        "/interrupted=" + recording.CaptureInterrupted + "/source=" + recording.Source + "/error=" + recording.Error) +
                    "; cursor=" + Cursor.lockState + "/visible=" + Cursor.visible;
                if (!force && state == previous) return;
                previous = state;
                RunSessionManager run = RunSessionManager.Instance;
                string row = DateTime.UtcNow.ToString("o") + "; frame=" + Time.frameCount + "; tick=" + (run == null ? -1 : run.Tick) +
                    "; retainedRecords=" + (recording == null ? -1 : recording.Recorded.Count) + "; reason=" + reason + "; " + state;
                if (rows.Count < 256) rows.Add(row); else omitted++;
            }
            catch (Exception exception)
            {
                if (rows.Count < 256) rows.Add("Diagnostic read failed: " + exception); else omitted++;
            }
        }

        public string Describe()
        {
            Mark("assertion checkpoint");
            Save();
            return "Capture gate timeline: " + path + "; omitted=" + omitted + Environment.NewLine + string.Join(Environment.NewLine, rows);
        }
        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, rows.Concat(new[] { "omitted=" + omitted }));
        }
        public void Dispose()
        {
            if (disposed) return;
            Mark("observer disposed");
            Application.focusChanged -= OnPlayerFocus;
            EditorApplication.focusChanged -= OnEditorFocus;
            EditorApplication.update -= Poll;
            disposed = true;
            Save();
        }
    }
}
