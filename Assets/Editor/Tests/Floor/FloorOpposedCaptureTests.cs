// ============================================================================
// FloorOpposedCaptureTests.cs
// ============================================================================
// PURPOSE:
//   Retains a declared engine cohort of opposed FloorLoop runs, including adverse
//   outcomes, with original capture identity and passive tick/event observations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor acceptance capture.
// KEY RESPONSIBILITIES:
//   - Cover Horror progression and Shift-to-run while preserving fixture motion intent.
//   - Execute seeds 1–30 in order, stopping after a run reaches 30 ended chases.
//   - Preserve authored spawns/configs and the ordinary physical no-verb follower.
//   - Separate capture integrity from measured gameplay targets and censoring.
// DEPENDENCIES:
//   - Core; Domain Floor/Player/Hunter/Chase/Director/Level; Session; Input/Telemetry.
//   - Existing CaptureGateTrace; Unity Test Framework, Editor/navigation/file APIs.
// USAGE NOTES:
//   Coordinator owns Unity admission. Explicit tests avoid an accidental long
//   cohort in the ordinary suite. No pose, health, Hunter, tuning or clock writes.
//   Only the runtime SceneRoot seed and gameplay-map device filter are arranged.
//   Original capture is closed before owned service teardown. Timeout/route
//   failure calls SuspendForSceneLoad, never an artificial complete RunEnded.
//   Fresh services are necessary because canonical Session seeds are immutable.
//   The route uses Held=None (8 m/s default), no verbs; it is not participant
//   evidence or representative p90/comfort/120–240-second pacing acceptance.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.Input;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Worsen.Tests.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorOpposedCaptureTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private const string TestPath = "Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs";
        private static readonly int[] DeclaredSeeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
        private bool restoreBackground, previousBackground;

        [UnityTest, Explicit("Finite opposed capture smoke; coordinator selects this method."), Timeout(420000)]
        public IEnumerator Seed01SmokeRetainsNaturalOrIncompleteOutcome()
        {
            yield return new EnterPlayMode();
            previousBackground = Application.runInBackground; restoreBackground = true;
            Application.runInBackground = true;
            try { yield return CaptureCohort(true); }
            finally { RestoreBackground(); }
        }

        [UnityTest, Explicit("Up to 30 attempts/three hours; coordinator selects this method."), Timeout(11700000)]
        public IEnumerator DeclaredSeeds01Through30RetainOpposedCohort()
        {
            yield return new EnterPlayMode();
            previousBackground = Application.runInBackground; restoreBackground = true;
            Application.runInBackground = true;
            try { yield return CaptureCohort(false); }
            finally { RestoreBackground(); }
        }

        private static IEnumerator CaptureCohort(bool smoke)
        {
            var cohort = new Cohort { cohortId = Guid.NewGuid().ToString("N"), startedUtc = DateTime.UtcNow.ToString("o"),
                mode = smoke ? "smoke" : "cohort", declaredSeeds = (int[])DeclaredSeeds.Clone() };
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "GoalCompletion",
                "opposed-cohort-017", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + cohort.cohortId));
            Directory.CreateDirectory(folder);
            string manifest = Path.Combine(folder, "cohort.json");
            Write(manifest, cohort); // Declaration exists before any declared attempt starts.
            var focus = new CaptureGateTrace("OpposedCohort-" + cohort.cohortId);
            try
            {
                // EnterPlayMode may start the editor's open scene. Preserve its
                // interrupted paths explicitly; it is outside the declared cohort.
                var preludeRun = RunSessionManager.Instance;
                var preludeInput = InputManager.Instance;
                var preludeTelemetry = TelemetryManager.Instance;
                var preludeRoots = CanonicalRoots();
                if (preludeRun != null) preludeRun.SuspendForSceneLoad();
                cohort.preludeInput = preludeInput == null ? "" : preludeInput.LastRecordingPath;
                cohort.preludeCsv = preludeTelemetry == null ? "" : preludeTelemetry.LastOutputPath;
                yield return UnloadOwnedRuntime(preludeRoots);
                yield return focus.AdmitStableGameViewFocus();
                foreach (int seed in DeclaredSeeds)
                {
                    var attempt = new Attempt(seed, folder);
                    var entry = new AttemptIndex { seed = seed, reportPath = attempt.ReportPath, outcome = "attempt-started", integrityPassed = false };
                    cohort.attempts.Add(entry); // Reserve denominator before any setup/yield can fail.
                    Write(manifest, cohort);
                    bool returned = false;
                    try { yield return attempt.Execute(); returned = true; }
                    finally
                    {
                        if (!returned) attempt.MarkInterrupted();
                        try { attempt.Close(); } catch (Exception error) { attempt.Fail("Closure exception: " + error); }
                        try { attempt.Dispose(); } catch (Exception error) { attempt.Fail("Cleanup exception: " + error); }
                        try { attempt.Save(); } catch (Exception error) { attempt.Fail("Report write failed: " + error); }
                        entry.outcome = attempt.Report.outcome; entry.completedChases = attempt.CompletedChases;
                        entry.integrityPassed = attempt.Report.integrityFailures.Count == 0;
                        entry.integrityError = string.Join(" | ", attempt.Report.integrityFailures);
                        cohort.completedChases = cohort.attempts.Sum(item => item.completedChases);
                        if (!entry.integrityPassed) cohort.stopReason = "integrity-failure";
                        else if (cohort.completedChases >= 30) cohort.stopReason = "completed-chase-coverage-reached";
                        else if (smoke) cohort.stopReason = "declared-one-seed-smoke";
                        else if (seed == DeclaredSeeds.Last()) cohort.stopReason = "thirty-attempt-limit";
                        Write(manifest, cohort);
                    }
                    yield return UnloadOwnedRuntime(attempt.OwnedRoots);
                    if (cohort.stopReason != "running") break;
                }
            }
            finally
            {
                try { focus.Dispose(); }
                finally
                {
                    if (cohort.stopReason == "running") cohort.stopReason = "runner-interrupted";
                    cohort.finishedUtc = DateTime.UtcNow.ToString("o");
                    Write(manifest, cohort);
                    TestContext.WriteLine("Declared opposed cohort: " + manifest);
                }
            }
            Assert.That(cohort.attempts.Count, Is.GreaterThan(0), "No declared attempt produced evidence.");
            Assert.That(cohort.attempts.All(item => item.integrityPassed), Is.True, "Capture integrity failed; inspect retained reports: " + manifest);
            // Numeric targets and natural/short/timeout outcomes are evidence,
            // never assertions. Coverage is reported by the offline helper.
        }

        private static GameObject[] CanonicalRoots() => new Component[] { RunSessionManager.Instance, InputManager.Instance,
            TelemetryManager.Instance, SceneFlowManager.Instance, DebugOverlayManager.Instance, AudioManager.Instance }
            .Where(item => item != null).Select(item => item.gameObject).Distinct().ToArray();

        private static IEnumerator UnloadOwnedRuntime(GameObject[] roots)
        {
            // These exact canonical root identities were captured from this test's
            // Play Mode lifetime; no general persistent-object scan or name match.
            var holding = SceneManager.CreateScene("OpposedCohortBetweenAttempts");
            SceneManager.SetActiveScene(holding);
            var previous = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            { var scene = SceneManager.GetSceneAt(index); if (scene != holding) previous.Add(scene); }
            foreach (var scene in previous)
            {
                var operation = SceneManager.UnloadSceneAsync(scene);
                double unloadDeadline = Time.realtimeSinceStartupAsDouble + 10d;
                if (operation != null)
                {
                    while (!operation.isDone && Time.realtimeSinceStartupAsDouble < unloadDeadline) yield return null;
                    Assert.That(operation.isDone, Is.True, "Runtime scene unloading exceeded ten wall seconds.");
                }
            }
            foreach (var root in roots)
            {
                if (root == null) continue;
                var run = root.GetComponent<RunSessionManager>();
                if (run != null) run.SuspendForSceneLoad();
                root.SetActive(false); // Pair Driver/Orchestrator events before destruction.
                Object.Destroy(root);
            }
            int frame = Time.frameCount;
            double deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while ((Time.frameCount <= frame || roots.Any(root => root != null)) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(frame), "Teardown requires a later real frame.");
            Assert.That(roots.All(root => root == null), Is.True, "Owned canonical services did not finish destruction.");
            Assert.That(CanonicalRoots(), Is.Empty, "A canonical service survived owned teardown.");
            Assert.That(PlayerRegistry.Items, Is.Empty); Assert.That(HunterRegistry.Items, Is.Empty);
        }

        private sealed class Attempt : IDisposable
        {
            public readonly AttemptReport Report;
            public readonly string ReportPath;
            public GameObject[] OwnedRoots = Array.Empty<GameObject>();
            public int CompletedChases => Report.chases.Count(row => row.endTick >= row.startTick && row.reason != "Unknown");
            private readonly string folder;
            private FloorLoopSceneRoot root;
            private RunSessionManager run;
            private InputManager input;
            private TelemetryManager telemetry;
            private PlayerManager player;
            private HunterManager hunter;
            private FloorManager floor;
            private ChaseManager chase;
            private DirectorManager director;
            private LevelGraph graph;
            private PlayerProfile profile;
            private FloorDriverConfig pathConfig;
            private DirectorConfig directorConfig;
            private InputReplayDriverState recording;
            private InputDriverState gates;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private readonly Dictionary<Object, string> configs = new Dictionary<Object, string>();
            private bool ready, closed, devicesSaved, boundTick, observedSettled;
            private int captureCount, readyCount, producedFrames;
            private InputFrame expectedInput;
            private Route route;
            private int target = -1, corner;
            private double nextPlan, lastImprovement, wallStart;
            private float bestLength = float.PositiveInfinity;

            public Attempt(int seed, string cohortFolder)
            {
                folder = Path.Combine(cohortFolder, "seed-" + seed.ToString("D2"));
                Directory.CreateDirectory(folder);
                ReportPath = Path.Combine(folder, "attempt.json");
                Report = new AttemptReport { seed = seed, startedUtc = DateTime.UtcNow.ToString("o") };
            }
            public void Fail(string message) { if (!Report.integrityFailures.Contains(message)) Report.integrityFailures.Add(message); }
            public void MarkInterrupted()
            {
                Fail("Runner/setup interrupted the attempted seed before Execute returned.");
                if (!Report.naturalTerminal) Report.outcome = ready ? "runner-interrupted-incomplete" : "startup-interrupted";
            }
            private void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            public IEnumerator Execute()
            {
                wallStart = Time.realtimeSinceStartupAsDouble;
                SceneManager.sceneLoaded += Loaded;
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                if (load == null) { Fail("FloorLoop could not load."); yield break; }
                while ((!load.isDone || !ready) && Report.integrityFailures.Count == 0 && Time.realtimeSinceStartupAsDouble - wallStart < 20d) yield return null;
                if (!load.isDone || !ready) { Fail("Original readiness was not observed before tick one."); yield break; }
                // Observers/follower are already live from tick zero. Even a very
                // short natural outcome must get its later-frame uniqueness check.
                while (!observedSettled && Report.integrityFailures.Count == 0 && Time.realtimeSinceStartupAsDouble - wallStart < 30d)
                { CheckDeferredUniqueness(); if (!observedSettled) yield return null; }
                while (!Report.naturalTerminal && Report.integrityFailures.Count == 0 && Report.routeFailure.Length == 0 &&
                    run.ElapsedSeconds < 300d && Time.realtimeSinceStartupAsDouble - wallStart < 360d)
                {
                    CheckDeferredUniqueness();
                    yield return null;
                }
                if (!Report.naturalTerminal)
                    Report.outcome = Report.integrityFailures.Count > 0 ? "integrity-failure" : Report.routeFailure.Length > 0 ?
                        "route-failure-incomplete" : run.ElapsedSeconds >= 300d ? "timeout-sim" : "timeout-wall";
            }
            private void Loaded(Scene scene, LoadSceneMode _)
            {
                if (scene.path != ScenePath) return;
                try
                {
                    root = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<FloorLoopSceneRoot>(true)).Single();
                    Require(RunSessionManager.Instance == null, "Previous canonical Session must be gone before the next seed.");
                    Field(root, "_seed").SetValue(root, Report.seed); // Only pre-Start seed arrangement.
                    run = Read<RunSessionManager>(root, "_run");
                    floor = Read<FloorManager>(root, "_floor"); chase = Read<ChaseManager>(root, "_chase");
                    director = Read<DirectorManager>(root, "_director");
                    Remember(Read<PlayerProfile>(root, "_playerProfile")); Remember(Read<HunterProfile>(root, "_hunterProfile"));
                    Remember(Read<ChaseConfig>(root, "_chaseConfig")); Remember(Read<FloorConfig>(root, "_floorConfig"));
                    directorConfig = Read<DirectorConfig>(root, "_directorConfig"); Remember(directorConfig);
                    Report.authoredPlayerSpawn = Read<Vector3>(root, "_spawnPosition");
                    Report.authoredHunterSpawn = Read<Vector3>(root, "_hunterSpawnPosition");
                    run.CaptureStarted += CaptureStarted; run.CaptureEnded += CaptureEnded; run.RunEnded += RunEnded;
                    run.HealthChanged += Health; run.ChaseStarted += ChaseStarted; run.ChaseEnded += ChaseEnded;
                    run.ChasePhaseChanged += ChasePhase; run.TelemetryPublished += Telemetry;
                    floor.OnExitOpened += ExitOpened; floor.OnPickupCollected += Pickup;
                    director.OnHintIssued += Hint; director.OnPressureSampled += Pressure; director.OnIntrusion += Intrusion;
                    FloorLoopSceneRoot.SceneReady += Ready;
                    Report.files.Add(Stamp(ScenePath)); Report.files.Add(Stamp(TestPath));
                    Report.files.Add(Stamp("Assets/Greybox/FloorLoop/FloorLoopNavMesh.asset"));
                    Report.files.Add(Stamp("Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs"));
                    Report.files.Add(Stamp("ProjectSettings/TimeManager.asset"));
                    foreach (string path in Directory.GetFiles("Assets/Resources/ScriptableObjects", "*.asset", SearchOption.AllDirectories)) Report.files.Add(Stamp(path));
                    foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)) Report.files.Add(Stamp(path));
                }
                catch (Exception error) { Fail("Before Start: " + error); }
            }
            private void CaptureStarted(RunCaptureMetadata metadata)
            {
                if (++captureCount != 1) { Fail("Repeated CaptureStarted; original identity retained."); return; }
                Report.captureSessionId = metadata.SessionId; Report.startTick = metadata.StartTick;
                Report.fixedDeltaTime = metadata.FixedDeltaTime; Report.sourceRevision = metadata.SourceRevision;
                Report.configHash = metadata.ConfigSnapshotHash; Report.randomConsumptionOrder = metadata.RandomConsumptionOrder;
                input = InputManager.Instance; telemetry = TelemetryManager.Instance;
                Report.csvPathAtStart = telemetry == null ? "" : telemetry.LastOutputPath;
                if (metadata.Seed != Report.seed || metadata.StartTick != 0 || run.Tick != 0) Fail("Original capture seed/tick mismatch.");
                try
                {
                    if (metadata.SourceRevision != BuildHash("Assets/Scripts", "*.cs") ||
                        metadata.ConfigSnapshotHash != BuildHash("Assets/Resources/ScriptableObjects", "*.asset")) Fail("Capture metadata does not match the current source/config build fingerprint.");
                }
                catch (Exception error) { Fail("Capture provenance read: " + error); }
            }
            private void Ready(SceneKey scene)
            {
                if (scene != SceneKey.FloorLoop) return;
                try
                {
                    Require(++readyCount == 1 && captureCount == 1, "Exactly one original capture/readiness is required.");
                    Require(run == RunSessionManager.Instance && run.Tick == 0 && run.Seed == Report.seed, "Canonical seeded Session must be at tick zero.");
                    Require(input != null && input == Read<InputManager>(root, "_input") && telemetry != null &&
                        telemetry == Read<TelemetryManager>(root, "_telemetry"), "Capture sinks must be the initialized root's canonical services.");
                    OwnedRoots = new Component[] { run, input, telemetry, Read<SceneFlowManager>(root, "_sceneFlow"),
                        Read<DebugOverlayManager>(root, "_overlay"), Read<AudioManager>(root, "_audio") }.Select(item => item.gameObject).Distinct().ToArray();
                    Report.canonicalIds = OwnedRoots.Select(item => item.GetInstanceID()).ToArray();
                    Require(PlayerRegistry.Items.Count == 1 && HunterRegistry.Items.Count == 1, "Exactly one real Player and Hunter are required.");
                    player = PlayerRegistry.Items[0]; hunter = HunterRegistry.Items[0];
                    Report.playerId = player.Id.ToString(); Report.hunterId = hunter.Id.ToString();
                    Require(hunter.isActiveAndEnabled && hunter.gameObject.activeInHierarchy, "Hunter must remain enabled.");
                    Require(player.ReadOnlyState.Position == Report.authoredPlayerSpawn && hunter.ReadOnlyState.Position == Report.authoredHunterSpawn, "Authored spawns changed.");
                    profile = Read<PlayerProfile>(player, "_profile"); pathConfig = Read<FloorDriverConfig>(floor.GetComponent<FloorDriver>(), "_config");
                    Remember(profile); Remember(pathConfig); Remember(Read<PlayerMoverDriverConfig>(player.GetComponent<PlayerDriver>(), "_config"));
                    Remember(Read<HunterMotorDriverConfig>(hunter.GetComponent<HunterDriver>(), "_config"));
                    Require(profile.SprintSpeed == 8f && Time.timeScale == 1f, "Default no-verb speed/time scale changed.");
                    Require(Math.Abs(Time.fixedDeltaTime - 1f / 60f) < 0.000001f && Time.fixedDeltaTime == Report.fixedDeltaTime, "Actual tick does not match capture metadata.");
                    graph = Read<LevelManager>(root, "_level").ReadOnlyState.Graph;
                    Report.selectedAnchors = floor.ReadOnlyState.ActiveCakeAnchors.Select(anchor => anchor.Id).ToArray();
                    Report.requiredCakes = floor.ReadOnlyState.RequiredCakeCount;
                    gameplay = Read<InputActionMap>(input.GetComponent<PlayerInputDriver>(), "_actions");
                    previousDevices = gameplay.devices.HasValue ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    devicesSaved = true; gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions) Require(action.controls.Count == 0, "Gameplay hardware controls remain bound.");
                    Require(input.SetSource(InputSource.Live), "Could not clear buffered hardware values without changing capture gates.");
                    recording = Read<InputReplayDriverState>(input.GetComponent<InputRecorder>(), "_state");
                    gates = Read<InputDriverState>(input.GetComponent<PlayerInputDriver>(), "_state");
                    Require(recording.Metadata.SessionId == Report.captureSessionId && recording.Recorded.Count == 0, "Original input capture identity/start records mismatch.");
                    Require(!string.IsNullOrEmpty(Report.csvPathAtStart), "Original CSV writer path is missing.");
                    Report.readyFrame = Time.frameCount;
                    input.FramePublished += Producer; run.BeforeTick += Supply; run.TickAdvanced += Tick;
                    boundTick = true; ready = true;
                    Report.ticks.Add(Pose()); // Tick zero before any movement.
                }
                catch (Exception error) { Fail("Readiness: " + error); }
            }
            private void CheckDeferredUniqueness()
            {
                if (observedSettled || Time.frameCount <= Report.readyFrame) return;
                observedSettled = true; Report.settledFrame = Time.frameCount;
                foreach (Type type in new[] { typeof(RunSessionManager), typeof(InputManager), typeof(TelemetryManager), typeof(SceneFlowManager),
                    typeof(DebugOverlayManager), typeof(AudioManager), typeof(PlayerManager), typeof(HunterManager) })
                    if (Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1) Fail("Deferred uniqueness failed for " + type.Name);
            }
            private void Producer(InputFrame frame)
            {
                producedFrames++;
                if (!frame.Equals(default(InputFrame))) Fail("Isolated gameplay producer was not neutral.");
            }
            private void Supply()
            {
                expectedInput = default;
                try
                {
                    if (Report.integrityFailures.Count > 0 || Report.routeFailure.Length > 0) { run.ReceiveInput(default); return; }
                    Vector3 position = player.ReadOnlyState.Position;
                    bool exit = floor.ReadOnlyState.ExitState == ExitState.Open;
                    bool changed = exit ? target != 0 : !floor.ReadOnlyState.ActiveCakeAnchors.Any(anchor => anchor.Id == target);
                    if (changed)
                    {
                        route = (exit ? new[] { Query(0, position, graph.ExitPosition) } : floor.ReadOnlyState.ActiveCakeAnchors
                            .Select(anchor => Query(anchor.Id, position, anchor.Position))).Where(item => item != null)
                            .OrderBy(item => item.length).ThenBy(item => item.id).FirstOrDefault();
                        if (route == null) { RouteFailure("No complete route to a remaining target."); return; }
                        target = route.id; corner = 0; bestLength = route.length; lastImprovement = run.ElapsedSeconds; nextPlan = run.ElapsedSeconds + 0.25d;
                    }
                    else if (run.ElapsedSeconds >= nextPlan)
                    {
                        Vector3 destination = exit ? graph.ExitPosition : floor.ReadOnlyState.ActiveCakeAnchors.Single(anchor => anchor.Id == target).Position;
                        route = Query(target, position, destination);
                        if (route == null) { RouteFailure("Current target lost its complete route."); return; }
                        corner = 0; nextPlan = run.ElapsedSeconds + 0.25d;
                        if (route.length < bestLength - 0.1f) { bestLength = route.length; lastImprovement = run.ElapsedSeconds; }
                    }
                    if (run.ElapsedSeconds - lastImprovement > 12d) { RouteFailure("No route-length progress for twelve seconds."); return; }
                    while (corner < route.corners.Length - 1 && FlatDistance(position, route.corners[corner]) <= 0.12f) corner++;
                    Vector3 delta = route.corners[corner] - position; delta.y = 0f;
                    float speed = Mathf.Min(profile.SprintSpeed, delta.magnitude * 3f);
                    Vector3 velocity = delta.sqrMagnitude > 0.000001f ? delta.normalized * speed : Vector3.zero;
                    Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, player.ReadOnlyState.HeadingDegrees, 0f)) * velocity;
                    expectedInput = new InputFrame(new Vector2(local.x, local.z) / profile.SprintSpeed, Vector2.zero, InputButtons.Sprint, InputButtons.None, InputButtons.None);
                    run.ReceiveInput(expectedInput);
                }
                catch (Exception error) { Fail("Follower exception: " + error); run.ReceiveInput(default); }
            }
            private void RouteFailure(string reason)
            { Report.routeFailure = reason; Report.routeFailureTick = run.Tick; run.ReceiveInput(default); }
            private Route Query(int id, Vector3 from, Vector3 to)
            {
                if (!NavMesh.SamplePosition(from, out var start, pathConfig.PathSampleRadius, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(to, out var end, pathConfig.PathSampleRadius, NavMesh.AllAreas)) return null;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) return null;
                var corners = path.corners; float length = 0f;
                for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                return new Route { id = id, length = length, corners = corners };
            }
            private void Tick(InputFrame frame, float dt, long tick)
            {
                try
                {
                    Require(tick == Report.lastTick + 1 && producedFrames == tick, "Missing/duplicate committed tick or input publication.");
                    Require(frame.Equals(expectedInput), "Committed frame differs from the ordinary synthetic command.");
                    Require(recording.Recorded.Count == tick && recording.Metadata.SessionId == Report.captureSessionId,
                        "Original input stream lost a committed tick/identity.");
                    Require(recording.Recording && !recording.CaptureInterrupted && gates.InputEnabled && gates.OwnerEnabled && gates.HasFocus,
                        "Actual input/focus/recording gate failed during capture.");
                    Require(hunter.isActiveAndEnabled && HunterRegistry.Items.Count == 1, "Actual Hunter was removed/disabled.");
                    Report.lastTick = tick; Report.ticks.Add(Pose());
                    CheckDeferredUniqueness();
                }
                catch (Exception error) { Fail("Committed tick: " + error); }
            }
            private TickRow Pose() => new TickRow { tick = run.Tick, seconds = run.ElapsedSeconds,
                playerPosition = player.ReadOnlyState.Position, playerVelocity = player.ReadOnlyState.Velocity,
                movementPosition = run.Tick == 0 ? player.ReadOnlyState.Position : player.LastMovementSample.Position,
                movementVelocity = run.Tick == 0 ? player.ReadOnlyState.Velocity : player.LastMovementSample.Velocity,
                hunterPosition = hunter.ReadOnlyState.Position, hunterVelocity = hunter.ReadOnlyState.Velocity,
                health = player.ReadOnlyState.Health, isChasing = chase.ReadOnlyState.HasActiveChase,
                chasePhase = chase.ReadOnlyState.Phase.ToString(), chaseId = chase.ReadOnlyState.ChaseId,
                distance = Vector3.Distance(player.ReadOnlyState.Position, hunter.ReadOnlyState.Position),
                withinDirectorRadius = Vector3.Distance(player.ReadOnlyState.Position, hunter.ReadOnlyState.Position) < directorConfig.ProximityRadiusMeters,
                cakes = floor.ReadOnlyState.CakeCount, exit = floor.ReadOnlyState.ExitState.ToString(),
                target = target, corner = corner, cornerPosition = route == null ? default : route.corners[corner] };
            private void Health(EntityId id, float health, float maximum) => Report.health.Add(new HealthRow { tick = run.Tick, player = id.ToString(), health = health, maximum = maximum });
            private void Pickup(PickupCollectedFact fact)
            { Report.pickups.Add(new PickupRow { tick = fact.Tick, player = fact.PlayerId.ToString(), anchor = fact.AnchorId, kind = fact.Kind.ToString(), cakeCount = fact.CakeCount, goldenCount = fact.GoldenCount }); nextPlan = 0d; }
            private void ExitOpened(long tick)
            {
                if (Report.exitOpenTick >= 0) { Fail("Duplicate ExitOpened."); return; }
                Report.exitOpenTick = tick; Report.firstSweepSeconds = run.ElapsedSeconds; Report.exitOpenPosition = player.ReadOnlyState.Position;
            }
            private void ChaseStarted(ChaseFact fact)
            {
                if (Report.chases.Any(row => row.id == fact.ChaseId)) { Fail("Duplicate ChaseStarted identity."); return; }
                Report.chases.Add(new ChaseRow { id = fact.ChaseId, startTick = fact.Tick, player = fact.Player.ToString(), hunter = fact.Hunter.ToString() });
            }
            private void ChaseEnded(ChaseFact fact)
            {
                var row = Report.chases.SingleOrDefault(item => item.id == fact.ChaseId);
                if (row == null || row.endTick >= 0) { Fail("Unmatched/duplicate ChaseEnded identity."); return; }
                row.endTick = fact.Tick; row.reason = fact.EndReason.ToString(); row.durationSeconds = (fact.Tick - row.startTick) * (double)Report.fixedDeltaTime;
            }
            private void ChasePhase(ChaseFact fact) => Report.chasePhases.Add(new ChasePhaseRow { tick = fact.Tick, id = fact.ChaseId, phase = fact.Phase.ToString(), player = fact.Player.ToString(), hunter = fact.Hunter.ToString() });
            private void Hint(HintPayload fact) => Report.hints.Add(new HintRow { hunter = fact.Hunter.ToString(), player = fact.Player.ToString(),
                observedTick = fact.ObservedTick, deliveredTick = fact.DeliveredTick, position = fact.Position, ageSeconds = fact.AgeSeconds, radius = fact.Radius, confidence = fact.Confidence });
            private void Pressure(DirectorPressureSample fact) => Report.pressure.Add(new PressureRow { tick = fact.Tick, player = fact.Player.ToString(),
                heatSeconds = fact.HeatSeconds, reliefSeconds = fact.ReliefSeconds, isChasing = fact.IsChasing, isWithinProximity = fact.IsWithinProximity, hintIssued = fact.HintIssued });
            private void Intrusion(IntrusionSample fact) => Report.intrusions.Add(new IntrusionRow { tick = fact.Tick, player = fact.Player.ToString(), durationSeconds = fact.DurationSeconds });
            private void Telemetry(TelemetrySample fact)
            { if (fact.Kind == TelemetrySampleKind.AcceptedHit) Report.acceptedHits.Add(new HitRow { tick = fact.Tick, player = fact.Player.ToString(), chaseId = fact.ChaseId, damage = fact.Value, reason = fact.Outcome.ToString(), eventId = fact.EventId }); }
            private void CaptureEnded(long tick, bool complete)
            {
                Report.captureEndCount++; Report.terminalRequestedComplete = complete; Report.captureEndTick = tick;
                Report.inputPath = input == null ? "" : input.LastRecordingPath;
                Report.csvPath = telemetry == null ? "" : telemetry.LastOutputPath;
            }
            private void RunEnded(RunSummary summary)
            {
                Report.naturalTerminal = true; Report.outcome = summary.EndReason.ToString();
                Report.terminalTick = run.Tick; Report.terminalSeconds = summary.ElapsedSeconds;
                Report.terminalHealth = player.ReadOnlyState.Health;
                if (Report.captureEndCount != 1 || !Report.terminalRequestedComplete) Fail("Natural terminal did not follow exactly one complete capture request.");
            }
            public void Close()
            {
                if (closed) return; closed = true;
                try
                {
                    if (run != null && !Report.naturalTerminal) run.SuspendForSceneLoad();
                    Report.wallSeconds = Time.realtimeSinceStartupAsDouble - wallStart;
                    if (run != null) { Report.observedRunSeconds = run.ElapsedSeconds; Report.observedRunTick = run.Tick; }
                    if (!ready) Fail("Attempt did not reach tick-zero readiness.");
                    if (!observedSettled) Fail("Strict service uniqueness was not observed on a later frame.");
                    if (Report.captureEndCount != 1) Fail("Original capture did not close exactly once.");
                    if (Report.lastTick != Report.observedRunTick || Report.ticks.Count != Report.lastTick + 1) Fail("Tick-zero/all-committed pose coverage mismatch.");
                    if (recording != null)
                    {
                        Report.inputComplete = recording.CaptureComplete; Report.inputInterrupted = recording.CaptureInterrupted;
                        Report.inputCount = recording.Recorded.Count; Report.inputRecordedEndTick = recording.EndTick;
                        if (recording.Metadata.SessionId != Report.captureSessionId || recording.Recorded.Count != Report.lastTick) Fail("Retained input records/identity mismatch.");
                        if (recording.CaptureInterrupted) Fail("Input producer was interrupted during capture.");
                    }
                    Report.inputError = input == null ? "unavailable" : input.LastRecordingError;
                    Report.csvError = telemetry == null ? "unavailable" : telemetry.LastError;
                    VerifyFiles();
                    foreach (var pair in configs) if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value) Fail("Runtime config changed: " + pair.Key);
                    foreach (var stamp in Report.files) if (!File.Exists(stamp.path) || Hash(stamp.path) != stamp.sha256) Fail("Source/config/scene drift: " + stamp.path);
                }
                catch (Exception error) { Fail("Capture closure/integrity: " + error); }
                if (Report.outcome == "unfinished") Report.outcome = ready ? "runner-interrupted-incomplete" : "startup-failure";
            }
            private void VerifyFiles()
            {
                Require(!string.IsNullOrEmpty(Report.inputPath) && File.Exists(Report.inputPath), "Original input artifact missing.");
                Require(!string.IsNullOrEmpty(Report.csvPath) && File.Exists(Report.csvPath), "Original CSV artifact missing.");
                Report.retainedInput = Path.Combine(folder, "input.winput"); Report.retainedCsv = Path.Combine(folder, "telemetry.csv");
                File.Copy(Report.inputPath, Report.retainedInput, false); File.Copy(Report.csvPath, Report.retainedCsv, false);
                Report.captureFiles.Add(Stamp(Report.inputPath)); Report.captureFiles.Add(Stamp(Report.csvPath));
                Require(Report.csvPath == Report.csvPathAtStart, "CSV writer identity changed during the attempt.");
                byte[] bytes = File.ReadAllBytes(Report.inputPath);
                Require(recording != null && bytes.SequenceEqual(new InputRecordingPresenter().Encode(recording)),
                    "Input bytes differ from the untouched original recorder state, including incomplete payloads.");
                using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
                {
                    Require(reader.ReadInt32() == 0x57525031 && reader.ReadInt32() == 1, "Input schema mismatch.");
                    Require(reader.ReadString() == Report.captureSessionId && reader.ReadInt32() == Report.seed && reader.ReadSingle() == Report.fixedDeltaTime, "Input identity/timing mismatch.");
                    Require(reader.ReadString() == Report.sourceRevision && reader.ReadString() == Report.configHash && reader.ReadString() == Report.randomConsumptionOrder && reader.ReadInt64() == Report.startTick, "Input provenance mismatch.");
                    Require(reader.ReadInt64() == Report.captureEndTick && reader.ReadBoolean() == Report.inputComplete && reader.ReadInt32() == Report.inputCount, "Input terminal header mismatch.");
                }
                if (Report.inputComplete)
                {
                    Require(new InputRecordingPresenter().TryDecode(bytes, out var metadata, out var records, out var error), "Complete input decode failed: " + error);
                    Require(metadata.SessionId == Report.captureSessionId && records.Length == Report.lastTick, "Decoded input identity/count mismatch.");
                    for (int i = 0; i < records.Length; i++)
                        Require(records[i].Tick == i + 1 && records[i].Resolution.Present &&
                            records[i].Resolution.Position == Report.ticks[i + 1].movementPosition && records[i].Resolution.Velocity == Report.ticks[i + 1].movementVelocity,
                            "Decoded input/committed pose mismatch at " + (i + 1));
                }
                var rows = File.ReadAllLines(Report.csvPath).Skip(1).Select(Csv).ToArray();
                string Meta(string key) => rows.Single(row => row[0] == "metadata" && row[4] == key)[5];
                Require(Meta("session_id") == Report.captureSessionId && Meta("seed") == Report.seed.ToString(CultureInfo.InvariantCulture), "CSV identity mismatch.");
                Require(Meta("source_revision") == Report.sourceRevision && Meta("config_snapshot_hash") == Report.configHash &&
                    Meta("random_consumption_order") == Report.randomConsumptionOrder && long.Parse(Meta("start_tick"), CultureInfo.InvariantCulture) == Report.startTick &&
                    long.Parse(Meta("end_tick"), CultureInfo.InvariantCulture) == Report.captureEndTick, "CSV provenance/end tick mismatch.");
                foreach (var row in rows.Where(row => row[0] == "summary")) Report.csvSummary.Add(new KeyValue { key = row[4], value = row[5] });
                Report.csvComplete = bool.Parse(Report.csvSummary.Single(row => row.key == "Complete").value);
                Require(Report.inputComplete == Report.naturalTerminal && Report.csvComplete == Report.naturalTerminal &&
                    Report.terminalRequestedComplete == Report.naturalTerminal, "Requested/actual complete flags disagree with the natural or incomplete outcome.");
                Require(Report.csvError.Length == 0, "CSV writer reported failure: " + Report.csvError);
            }
            private void Remember(Object asset)
            { Require(asset != null, "Wired config missing."); if (!configs.ContainsKey(asset)) configs.Add(asset, EditorJsonUtility.ToJson(asset)); }
            public void Save() { Write(ReportPath, Report); TestContext.WriteLine("Opposed seed " + Report.seed + ": " + Report.outcome + "; report=" + ReportPath); }
            public void Dispose()
            {
                SceneManager.sceneLoaded -= Loaded; FloorLoopSceneRoot.SceneReady -= Ready;
                if (run != null)
                {
                    run.CaptureStarted -= CaptureStarted; run.CaptureEnded -= CaptureEnded; run.RunEnded -= RunEnded;
                    run.HealthChanged -= Health; run.ChaseStarted -= ChaseStarted; run.ChaseEnded -= ChaseEnded;
                    run.ChasePhaseChanged -= ChasePhase; run.TelemetryPublished -= Telemetry;
                    if (boundTick) { run.BeforeTick -= Supply; run.TickAdvanced -= Tick; }
                }
                if (floor != null) { floor.OnExitOpened -= ExitOpened; floor.OnPickupCollected -= Pickup; }
                if (director != null) { director.OnHintIssued -= Hint; director.OnPressureSampled -= Pressure; director.OnIntrusion -= Intrusion; }
                if (input != null && boundTick) input.FramePublished -= Producer;
                if (devicesSaved && gameplay != null) gameplay.devices = previousDevices;
            }
        }

        private static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(owner.GetType().Name, name);
        private static T Read<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        private static FileStamp Stamp(string path) => new FileStamp { path = path, sha256 = Hash(path) };
        private static string BuildHash(string directory, string pattern)
        {
            var text = new StringBuilder();
            foreach (string path in Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
                text.Append(path.Replace('\\', '/')).Append(Environment.NewLine).Append(File.ReadAllText(path)).Append(Environment.NewLine);
            using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
        private static void Write(string path, object value) => File.WriteAllText(path, JsonUtility.ToJson(value, true));
        private static string[] Csv(string line)
        {
            var cells = new List<string>(); var text = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { text.Append('"'); i++; } else quoted = !quoted; }
                else if (c == ',' && !quoted) { cells.Add(text.ToString()); text.Clear(); } else text.Append(c);
            }
            if (quoted) throw new InvalidDataException("Unexpected multiline CSV value.");
            cells.Add(text.ToString()); if (cells.Count != 10) throw new InvalidDataException("CSV column count differs from schema."); return cells.ToArray();
        }
        private void RestoreBackground() { if (restoreBackground) { Application.runInBackground = previousBackground; restoreBackground = false; } }
        [UnityTearDown] public IEnumerator RestoreEditor() { RestoreBackground(); if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Serializable] private sealed class Cohort
        {
            public string cohortId, startedUtc, finishedUtc, mode, stopReason = "running", preludeInput, preludeCsv;
            public string scope = "Declared opposed engine cohort; authored spawns/default configs; no-verb greedy physical follower; not participant or representative p90 evidence";
            public int[] declaredSeeds; public int completedChases; public int chaseStopThreshold = 30, attemptLimit = 30;
            public int simulatedSecondsPerAttempt = 300, wallSecondsPerAttempt = 360;
            public List<AttemptIndex> attempts = new List<AttemptIndex>();
        }
        [Serializable] private sealed class AttemptIndex { public int seed, completedChases; public string reportPath, outcome, integrityError; public bool integrityPassed; }
        [Serializable] private sealed class AttemptReport
        {
            public int seed, requiredCakes, readyFrame, settledFrame, inputCount, captureEndCount;
            public int[] selectedAnchors, canonicalIds;
            public string startedUtc, captureSessionId, playerId, hunterId, sourceRevision, configHash, randomConsumptionOrder,
                csvPathAtStart, inputPath, csvPath, retainedInput, retainedCsv, inputError, csvError;
            public string outcome = "unfinished", routeFailure = "";
            public bool naturalTerminal, terminalRequestedComplete, inputComplete, csvComplete, inputInterrupted;
            public long startTick, lastTick, observedRunTick, inputRecordedEndTick, captureEndTick = -1, terminalTick = -1, exitOpenTick = -1, routeFailureTick = -1;
            public float fixedDeltaTime, terminalHealth;
            public double firstSweepSeconds = -1, terminalSeconds = -1, observedRunSeconds, wallSeconds;
            public Vector3 authoredPlayerSpawn, authoredHunterSpawn, exitOpenPosition;
            public List<string> integrityFailures = new List<string>();
            public List<TickRow> ticks = new List<TickRow>(); public List<ChaseRow> chases = new List<ChaseRow>();
            public List<PressureRow> pressure = new List<PressureRow>(); public List<HintRow> hints = new List<HintRow>();
            public List<HealthRow> health = new List<HealthRow>(); public List<PickupRow> pickups = new List<PickupRow>();
            public List<ChasePhaseRow> chasePhases = new List<ChasePhaseRow>(); public List<IntrusionRow> intrusions = new List<IntrusionRow>();
            public List<HitRow> acceptedHits = new List<HitRow>(); public List<KeyValue> csvSummary = new List<KeyValue>();
            public List<FileStamp> files = new List<FileStamp>(); public List<FileStamp> captureFiles = new List<FileStamp>();
        }
        [Serializable] private sealed class TickRow { public long tick; public double seconds; public Vector3 playerPosition, playerVelocity, movementPosition, movementVelocity, hunterPosition, hunterVelocity, cornerPosition;
            public float health, distance; public bool isChasing, withinDirectorRadius; public string chasePhase, exit; public int chaseId, cakes, target, corner; }
        [Serializable] private sealed class ChaseRow { public int id; public long startTick, endTick = -1; public string player, hunter, reason = "Unknown"; public double durationSeconds = -1; }
        [Serializable] private sealed class ChasePhaseRow { public int id; public long tick; public string phase, player, hunter; }
        [Serializable] private sealed class PressureRow { public long tick; public string player; public float heatSeconds, reliefSeconds; public bool isChasing, isWithinProximity, hintIssued; }
        [Serializable] private sealed class HintRow { public string player, hunter; public long observedTick, deliveredTick; public Vector3 position; public float ageSeconds, radius, confidence; }
        [Serializable] private sealed class IntrusionRow { public long tick; public string player; public float durationSeconds; }
        [Serializable] private sealed class HealthRow { public long tick; public string player; public float health, maximum; }
        [Serializable] private sealed class HitRow { public long tick; public int chaseId; public long eventId; public float damage; public string reason, player; }
        [Serializable] private sealed class PickupRow { public long tick; public int anchor, cakeCount, goldenCount; public string kind, player; }
        [Serializable] private sealed class FileStamp { public string path, sha256; }
        [Serializable] private sealed class KeyValue { public string key, value; }
        private sealed class Route { public int id; public float length; public Vector3[] corners; }
    }
}
