// ============================================================================
// FloorTraversalIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Measures one unopposed automated FloorLoop route through ordinary movement,
//   physical cake/exit triggers and independently checked navigation cues.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor scene integration.
// KEY RESPONSIBILITIES:
//   - Cover Horror progression and Shift-to-run while preserving fixture motion intent.
//   - Follow complete NavMesh corners through synthetic Run input after real spawn.
//   - Compare fresh cues against independent path-length/direction queries.
//   - Retain actual timings, provenance, route samples and failures in a JSON log.
// DEPENDENCIES:
//   - Core, Player/Floor/Level/Hunter, Run, Input, SceneRoot, Unity navigation.
//   - NUnit, Unity Test Framework, read-only Editor asset lookup and log-file IO.
// USAGE NOTES:
//   Coordinator holds the Unity lease. No teleport, Driver reinitialization,
//   direct collection, scene/config save, bake or time-scale change is performed.
//   Hunter GameObject is deactivated only for this runtime unopposed route.
//   Gameplay map devices are isolated and the previous filter restored in finally.
//   Input slows geometrically near corners; no waits are added to meet pacing.
//   A measured time, even below 120 seconds, is reported unchanged. This fixture
//   cannot establish human/chased 120–240-second first-sweep acceptance.
//   Background setting/captured locals are established after EnterPlayMode.
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
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorTraversalIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private bool _restoreBackground, _previousBackground;

        [UnityTest, Timeout(390000)]
        public IEnumerator UnopposedMovingRouteCollectsCakesChecksPathCuesAndExits()
        {
            yield return new EnterPlayMode();
            _previousBackground = Application.runInBackground;
            _restoreBackground = true;
            Application.runInBackground = true;
            try { yield return ExerciseRoute(); }
            finally { RestoreBackground(); }
        }

        private static IEnumerator ExerciseRoute()
        {
            var report = new RouteReport { utc = DateTime.UtcNow.ToString("o") };
            RunSessionManager captureRun = null;
            InputActionMap gameplay = null;
            ReadOnlyArray<InputDevice>? previousDevices = null;
            Trial trial = null;
            int captures = 0;
            double wallStart = Time.realtimeSinceStartupAsDouble;
            Action<RunCaptureMetadata> captured = metadata =>
            {
                captures++;
                if (captures != 1) { report.failure = "Unexpected repeated CaptureStarted; original metadata retained."; return; }
                try
                {
                    report.sessionId = metadata.SessionId; report.seed = metadata.Seed;
                    report.startTick = metadata.StartTick; report.fixedDeltaTime = metadata.FixedDeltaTime;
                    report.sourceRevision = metadata.SourceRevision; report.captureConfigHash = metadata.ConfigSnapshotHash;
                    report.startRunSeconds = captureRun.ElapsedSeconds;
                    if (PlayerRegistry.Items.Count == 1) report.spawnPosition = PlayerRegistry.Items[0].ReadOnlyState.Position;
                    else report.failure = "Expected exactly one spawned Player when capture opens.";
                    var input = InputManager.Instance;
                    Assert.That(input, Is.Not.Null, "Capture startup requires the initialized canonical InputManager.");
                    gameplay = Field<InputActionMap>(input.GetComponent<PlayerInputDriver>(), "_actions");
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray())
                        : (ReadOnlyArray<InputDevice>?)null;
                    gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions)
                        Assert.That(action.controls.Count, Is.Zero, "Synthetic route must bind no gameplay hardware controls.");
                    // Clear buffered values/cancelled edges while preserving recording
                    // and the actual input, owner and focus gates before tick one.
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                }
                catch (Exception exception) { report.failure = "Capture/input isolation: " + exception; }
            };
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, _) =>
            {
                if (scene.path != ScenePath) return;
                try
                {
                    var root = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<FloorLoopSceneRoot>(true)).Single();
                    captureRun = RunSessionManager.Instance != null ? RunSessionManager.Instance : Field<RunSessionManager>(root, "_run");
                    captureRun.CaptureStarted += captured;
                }
                catch (Exception exception) { report.failure = "Capture subscription: " + exception; }
            };
            SceneManager.sceneLoaded += loaded;
            try
            {
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Generate and enable FloorLoop before this fixture.");
                double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                while ((!load.isDone || !Ready()) && report.failure.Length == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(report.failure, Is.Empty);
                Assert.That(load.isDone, Is.True, "FloorLoop scene loading did not complete.");
                Assert.That(Ready(), Is.True, "FloorLoop did not initialize and tick.");
                Assert.That(captures, Is.EqualTo(1), "Observe the original capture before SceneRoot.Start publishes readiness.");
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                var run = One<RunSessionManager>();
                var floor = One<FloorManager>();
                var player = One<PlayerManager>();
                var profile = Field<PlayerProfile>(player, "_profile");
                var floorConfig = Field<FloorConfig>(floor, "_config");
                var driverConfig = Field<FloorDriverConfig>(floor.GetComponent<FloorDriver>(), "_config");
                report.controlStartTick = run.Tick; report.controlStartSeconds = run.ElapsedSeconds;
                report.controlStartPosition = player.ReadOnlyState.Position;
                Assert.That(FlatDistance(report.spawnPosition, report.controlStartPosition), Is.LessThan(0.05f),
                    "Player moved before synthetic route control was attached.");
                report.sprintSpeed = profile.SprintSpeed; report.walkSpeed = profile.WalkSpeed;
                report.groundAcceleration = profile.GroundAcceleration;
                report.required = floor.ReadOnlyState.RequiredCakeCount;
                report.selectedAnchors = floor.ReadOnlyState.ActiveCakeAnchors.Select(anchor => anchor.Id).ToArray();
                report.floorConfig = JsonUtility.ToJson(floorConfig);
                report.pathSampleRadius = driverConfig.PathSampleRadius;
                AddStamp(report, ScenePath);
                foreach (var asset in new Object[] { profile, floorConfig, driverConfig }) AddStamp(report, AssetDatabase.GetAssetPath(asset));
                foreach (string source in new[]
                {
                    "Assets/Scripts/Domain/Player/Controller/PlayerController.cs", "Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs",
                    "Assets/Scripts/Domain/Floor/Controller/FloorController.cs", "Assets/Scripts/Domain/Floor/Manager/FloorManager.cs",
                    "Assets/Scripts/Domain/Floor/Driver/FloorDriver.cs", "Assets/Scripts/Domain/Floor/Driver/FloorPresenter.cs",
                    "Assets/Scripts/Session/Run/Manager/RunSessionManager.cs", "Assets/Scripts/Orchestrator/Scenes/FloorLoopSceneRoot.cs",
                    "Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs", "ProjectSettings/TimeManager.asset"
                }) AddStamp(report, source);
                Assert.That(floor.ReadOnlyState.CakeCount, Is.Zero);
                Assert.That(report.required, Is.EqualTo(10));
                Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                One<HunterManager>().gameObject.SetActive(false);
                Assert.That(HunterRegistry.Items.Count, Is.Zero);
                trial = new Trial(run, floor, player, One<InputManager>(), One<LevelManager>().ReadOnlyState.Graph,
                    profile, floorConfig, driverConfig.PathSampleRadius, report);
                trial.Bind();
                while (!trial.Ended && report.failure.Length == 0 && run.ElapsedSeconds - report.startRunSeconds < 300d &&
                    Time.realtimeSinceStartupAsDouble - wallStart < 360d) yield return null;
                if (!trial.Ended && report.failure.Length == 0) trial.Fail("Bound exceeded: 300 simulated seconds or 360 wall seconds.");
                Assert.That(report.failure, Is.Empty, trial.Describe());
                Assert.That(trial.Ended, Is.True);
                Assert.That(report.endReason, Is.EqualTo(RunEndReason.Escaped.ToString()));
                Assert.That(report.pickups.Count(row => row.kind == PickupKind.Cake.ToString()), Is.EqualTo(report.required));
                Assert.That(report.pickups.Where(row => row.kind == PickupKind.Cake.ToString()).Select(row => row.anchor).Distinct().Count(), Is.EqualTo(report.required));
                Assert.That(report.exitOpenTick, Is.GreaterThan(report.controlStartTick));
                Assert.That(report.firstSweepCueChecks, Is.GreaterThanOrEqualTo(5));
                Assert.That(report.discriminatingCueChecks, Is.GreaterThan(0), "At least one compared target must offer a different route direction.");
                Assert.That(report.exitCueChecks, Is.GreaterThan(0));
                Assert.That(report.travelMeters, Is.GreaterThan(10d));
                report.passed = true;
            }
            finally
            {
                trial?.Unbind();
                if (captureRun != null)
                {
                    captureRun.CaptureStarted -= captured;
                    report.observedEndTick = captureRun.Tick;
                    report.observedRunSeconds = captureRun.ElapsedSeconds;
                    captureRun.ReceiveInput(default);
                }
                SceneManager.sceneLoaded -= loaded;
                if (gameplay != null) gameplay.devices = previousDevices;
                report.wallSeconds = Time.realtimeSinceStartupAsDouble - wallStart;
                if (!report.passed && report.failure.Length == 0) report.failure = "Fixture assertion/setup failed; inspect the NUnit result.";
                SaveReport(report);
            }
        }

        private sealed class Trial
        {
            private readonly RunSessionManager _run;
            private readonly FloorManager _floor;
            private readonly PlayerManager _player;
            private readonly InputManager _input;
            private readonly LevelGraph _graph;
            private readonly PlayerProfile _profile;
            private readonly FloorConfig _config;
            private readonly float _sampleRadius;
            private readonly RouteReport _report;
            private Route _route;
            private int _target = -1, _corner;
            private double _nextPlan, _lastImprovement, _nextTrace;
            private float _bestLength = float.PositiveInfinity;
            private bool _inTick;
            private int _producedFrames, _observedFrames;
            private InputFrame _expectedInput;
            private Vector3 _previousPosition;
            private long _previousCueTick = -1;
            private ExitState _previousCueExit;
            public bool Ended { get; private set; }

            public Trial(RunSessionManager run, FloorManager floor, PlayerManager player, InputManager input,
                LevelGraph graph, PlayerProfile profile, FloorConfig config, float sampleRadius, RouteReport report)
            {
                _run = run; _floor = floor; _player = player; _input = input; _graph = graph; _profile = profile;
                _config = config; _sampleRadius = sampleRadius; _report = report; _previousPosition = player.ReadOnlyState.Position;
                _lastImprovement = run.ElapsedSeconds;
            }
            public void Bind()
            {
                _run.BeforeTick += BeforeTick; _run.TickAdvanced += AfterTick;
                _run.PlayerMovementPublished += Movement; _run.FloorDisplayChanged += Cue;
                _run.RunEnded += Finished; _input.FramePublished += HardwareFrame;
                _floor.OnPickupCollected += Pickup; _floor.OnExitOpened += Opened;
            }
            public void Unbind()
            {
                _run.BeforeTick -= BeforeTick; _run.TickAdvanced -= AfterTick;
                _run.PlayerMovementPublished -= Movement; _run.FloorDisplayChanged -= Cue;
                _run.RunEnded -= Finished; _input.FramePublished -= HardwareFrame;
                _floor.OnPickupCollected -= Pickup; _floor.OnExitOpened -= Opened;
            }
            public void Fail(string reason) { if (_report.failure.Length == 0) _report.failure = reason + " " + Describe(); }
            public string Describe() => "tick=" + _run.Tick + "; target=" + _target + "; corner=" + _corner +
                "; position=" + _player.ReadOnlyState.Position + "; cakes=" + _floor.ReadOnlyState.CakeCount;

            private void HardwareFrame(InputFrame frame)
            {
                _producedFrames++;
                if (!frame.Equals(default(InputFrame))) Fail("Isolated gameplay producer emitted non-neutral input.");
            }
            private void BeforeTick()
            {
                _inTick = true;
                try
                {
                    if (_report.failure.Length > 0 || Ended) { _run.ReceiveInput(default); return; }
                    Vector3 position = _player.ReadOnlyState.Position;
                    bool exit = _floor.ReadOnlyState.ExitState == ExitState.Open;
                    bool changed = exit ? _target != 0 : !_floor.ReadOnlyState.ActiveCakeAnchors.Any(anchor => anchor.Id == _target);
                    if (changed)
                    {
                        _route = Candidates(position, exit).OrderBy(route => route.length).ThenBy(route => route.id).FirstOrDefault();
                        if (_route == null) { Fail("No complete route to a remaining target."); _run.ReceiveInput(default); return; }
                        _target = _route.id; _corner = 0; _bestLength = _route.length; _lastImprovement = _run.ElapsedSeconds;
                        _nextPlan = _run.ElapsedSeconds + 0.25d;
                    }
                    else if (_run.ElapsedSeconds >= _nextPlan)
                    {
                        Vector3 target = exit ? _graph.ExitPosition : _floor.ReadOnlyState.ActiveCakeAnchors.Single(anchor => anchor.Id == _target).Position;
                        _route = Query(_target, position, target, _sampleRadius);
                        if (_route == null) { Fail("Current target lost its complete navigation route."); _run.ReceiveInput(default); return; }
                        _corner = 0; _nextPlan = _run.ElapsedSeconds + 0.25d;
                        if (_route.length < _bestLength - 0.1f) { _bestLength = _route.length; _lastImprovement = _run.ElapsedSeconds; }
                    }
                    if (_run.ElapsedSeconds - _lastImprovement > 12d) { Fail("No route-length progress for twelve seconds; possible physical obstruction."); _run.ReceiveInput(default); return; }
                    while (_corner < _route.corners.Length - 1 && FlatDistance(position, _route.corners[_corner]) <= 0.12f) _corner++;
                    Vector3 delta = _route.corners[_corner] - position; delta.y = 0f;
                    // Continuous analog steering; slow near a corner rather than
                    // cutting its clearance or inserting a timed pause.
                    float speed = Mathf.Min(_profile.SprintSpeed, delta.magnitude * 3f);
                    Vector3 worldVelocity = delta.sqrMagnitude > 0.000001f ? delta.normalized * speed : Vector3.zero;
                    Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, _player.ReadOnlyState.HeadingDegrees, 0f)) * worldVelocity;
                    var move = new Vector2(local.x, local.z) / _profile.SprintSpeed;
                    _expectedInput = new InputFrame(move, Vector2.zero, InputButtons.Sprint, InputButtons.None, InputButtons.None);
                    _run.ReceiveInput(_expectedInput);
                }
                catch (Exception exception) { Fail("Follower exception: " + exception); _run.ReceiveInput(default); }
            }
            private void AfterTick(InputFrame frame, float _, long __)
            {
                _inTick = false;
                if (_report.failure.Length > 0) return;
                if (_producedFrames != _observedFrames + 1) Fail("Expected exactly one ordinary input publication before each committed tick.");
                if (!frame.Equals(_expectedInput)) Fail("Committed input differs from the synthetic movement command.");
                _observedFrames = _producedFrames; _report.inputPublications = _producedFrames;
            }
            private void Movement(PlayerMovementSample sample)
            {
                float step = FlatDistance(_previousPosition, sample.Position);
                if (step > _profile.MaxDesignSpeed * Time.fixedDeltaTime + 0.05f) Fail("Discontinuous movement sample.");
                _report.travelMeters += step; _report.movementTicks++; _previousPosition = sample.Position;
                if (_run.ElapsedSeconds < _nextTrace) return;
                _nextTrace = _run.ElapsedSeconds + 0.5d;
                _report.route.Add(new RouteSample { tick = sample.Tick, seconds = _run.ElapsedSeconds, position = sample.Position,
                    velocity = sample.Velocity, target = _target, corner = _corner,
                    cornerPosition = _route == null ? default : _route.corners[_corner], pathLength = _route == null ? -1f : _route.length });
            }
            private void Pickup(PickupCollectedFact fact)
            {
                _report.pickups.Add(new PickupSample { tick = fact.Tick, seconds = _run.ElapsedSeconds, anchor = fact.AnchorId,
                    kind = fact.Kind.ToString(), position = _player.ReadOnlyState.Position, cakeCount = fact.CakeCount, goldenCount = fact.GoldenCount });
                _nextPlan = 0d;
            }
            private void Opened(long tick)
            {
                _report.exitOpenTick = tick;
                _report.firstSweepSeconds = _run.ElapsedSeconds - _report.startRunSeconds;
                _report.firstSweepTicks = tick - _report.startTick;
                _report.exitOpenPosition = _player.ReadOnlyState.Position;
            }
            private void Finished(RunSummary summary)
            {
                Ended = true; _report.endReason = summary.EndReason.ToString(); _report.finishedRunSeconds = summary.ElapsedSeconds;
            }
            private IEnumerable<Route> Candidates(Vector3 position, bool exit)
            {
                if (exit)
                {
                    var route = Query(0, position, _graph.ExitPosition, _sampleRadius);
                    if (route != null) yield return route;
                    yield break;
                }
                foreach (var anchor in _floor.ReadOnlyState.ActiveCakeAnchors)
                {
                    var route = Query(anchor.Id, position, anchor.Position, _sampleRadius);
                    if (route != null) yield return route;
                }
            }
            private void Cue(FloorDisplaySnapshot snapshot)
            {
                // Physics-time pickup snapshots intentionally preserve the previous
                // direction until the scheduled refresh; only compare fresh tick cues.
                if (!_inTick || !_player.ReadOnlyState.IsAlive || _report.failure.Length > 0) return;
                try
                {
                    bool exit = snapshot.Exit == ExitState.Open;
                    var candidates = Candidates(_player.ReadOnlyState.Position, exit).OrderBy(route => route.length).ThenBy(route => route.id).ToArray();
                    var best = candidates.FirstOrDefault();
                    if (snapshot.HasCue != (best != null)) { Fail("Fresh cue availability disagrees with complete navigation paths."); return; }
                    if (_previousCueTick >= 0 && snapshot.Exit == _previousCueExit)
                    {
                        double gap = (_run.Tick - _previousCueTick) * (double)Time.fixedDeltaTime;
                        if (Math.Abs(gap - _config.DirectionCueInterval) > 2d * Time.fixedDeltaTime)
                            Fail("Fresh cue cadence differs from the configured interval: " + gap);
                    }
                    _previousCueTick = _run.Tick; _previousCueExit = snapshot.Exit;
                    if (best == null) return;
                    if (Vector3.Distance(best.direction, snapshot.CueDirection) > 0.001f) { Fail("Fresh cue is not the shortest complete-path direction."); return; }
                    if (exit) _report.exitCueChecks++; else _report.firstSweepCueChecks++;
                    bool discriminating = candidates.Any(route => route.id != best.id && Vector3.Distance(route.direction, best.direction) > 0.1f);
                    if (discriminating) _report.discriminatingCueChecks++;
                    _report.cues.Add(new CueSample { tick = _run.Tick, exit = exit, selectedAnchor = best.id,
                        length = best.length, expected = best.direction, observed = snapshot.CueDirection, alternatives = candidates.Length,
                        discriminating = discriminating });
                }
                catch (Exception exception) { Fail("Cue oracle exception: " + exception); }
            }
        }

        private sealed class Route { public int id; public float length; public Vector3 direction; public Vector3[] corners; }
        private static Route Query(int id, Vector3 from, Vector3 to, float radius)
        {
            if (!NavMesh.SamplePosition(from, out var start, radius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out var end, radius, NavMesh.AllAreas)) return null;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return null;
            var corners = path.corners;
            if (corners.Length < 2) return null;
            float length = 0f;
            for (int index = 1; index < corners.Length; index++) length += Vector3.Distance(corners[index - 1], corners[index]);
            Vector3 direction = Vector3.zero;
            foreach (var point in corners)
            {
                Vector3 delta = point - from; delta.y = 0f;
                if (delta.sqrMagnitude <= 0.0001f) continue;
                direction = delta.normalized; break;
            }
            return new Route { id = id, length = length, direction = direction, corners = corners };
        }
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private static T Field<T>(object owner, string name) where T : class =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) as T;
        private static T One<T>() where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(found, Has.Length.EqualTo(1), typeof(T).Name); return found[0];
        }
        private static bool Ready() => SceneManager.GetActiveScene().path == ScenePath && RunSessionManager.Instance != null &&
            RunSessionManager.Instance.Phase == RunPhase.FirstSweep && RunSessionManager.Instance.Tick >= 3 && PlayerRegistry.Items.Count == 1;
        private static void AddStamp(RouteReport report, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Provenance source is missing", path);
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) report.files.Add(new FileStamp { path = path, sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") });
        }
        private static void SaveReport(RouteReport report)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(project, "Logs", "AgentValidation", "GoalCompletion", "moving-floor");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-seed-" + report.seed + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            TestContext.WriteLine("Moving Floor report: " + path + "; firstSweepSeconds=" + report.firstSweepSeconds +
                "; outcome=" + report.endReason + "; failure=" + report.failure);
        }
        private void RestoreBackground()
        {
            if (!_restoreBackground) return;
            Application.runInBackground = _previousBackground; _restoreBackground = false;
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { RestoreBackground(); if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Serializable] private sealed class RouteReport
        {
            public string scope = "unopposed automated route; Hunter GameObject disabled; gameplay devices isolated; no teleport/direct collection; not human/chased pacing acceptance";
            public string utc, sessionId, sourceRevision, captureConfigHash, floorConfig, endReason = "unfinished", failure = "";
            public bool passed;
            public int seed, required, movementTicks, inputPublications, firstSweepCueChecks, exitCueChecks, discriminatingCueChecks;
            public int[] selectedAnchors;
            public long startTick, controlStartTick, exitOpenTick = -1, firstSweepTicks = -1, observedEndTick;
            public double startRunSeconds, controlStartSeconds, firstSweepSeconds = -1, finishedRunSeconds = -1, observedRunSeconds, wallSeconds, travelMeters;
            public float fixedDeltaTime, sprintSpeed, walkSpeed, groundAcceleration, pathSampleRadius;
            public Vector3 spawnPosition, controlStartPosition, exitOpenPosition;
            public List<FileStamp> files = new List<FileStamp>();
            public List<PickupSample> pickups = new List<PickupSample>();
            public List<RouteSample> route = new List<RouteSample>();
            public List<CueSample> cues = new List<CueSample>();
        }
        [Serializable] private sealed class FileStamp { public string path, sha256; }
        [Serializable] private sealed class PickupSample { public long tick; public double seconds; public int anchor, cakeCount, goldenCount; public string kind; public Vector3 position; }
        [Serializable] private sealed class RouteSample { public long tick; public double seconds; public Vector3 position, velocity, cornerPosition; public int target, corner; public float pathLength; }
        [Serializable] private sealed class CueSample { public long tick; public bool exit, discriminating; public int selectedAnchor, alternatives; public float length; public Vector3 expected, observed; }
    }
}
