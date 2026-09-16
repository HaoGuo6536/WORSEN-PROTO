// ============================================================================
// HunterRouteComparisonTests.cs
// ============================================================================
// PURPOSE:
//   Compares ordinary pursuit along the authored straight corridor with repeated
//   cuts around the mid-room pillar under prospective route contract 020. Three
//   paired repetitions retain every real Session tick, including acceleration,
//   occlusion, failed turns and terminal damage. Between-cut deltas are diagnostics;
//   route-level success does not establish isolated-cut or human tag-feel acceptance.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter physical integration.
// KEY RESPONSIBILITIES:
//   - Cover Horror progression and Shift-to-run while preserving fixture motion intent.
//   - Compare three matched pairs for exactly 240 ordinary Run ticks per arm.
//   - Reserve all six reports before collection; preserve failed/censored attempts.
//   - Assess full-horizon advantage while retaining every negative cut interval.
//   - Share scene, hardware isolation and evidence handling with the CutOff trial.
//   - Preserve original capture identity and explicitly incomplete benchmark ends.
// DEPENDENCIES:
//   Core; Domain Player/Hunter/Chase/Level; Session.Run; Presentation Input/Telemetry;
//   TagArena factories; existing CaptureGateTrace; Unity physics/navigation and NUnit.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Only scene-loaded initial spawn requests
//   change, before factories initialize; no live pose, AI state or tuning changes.
//   Both arms preserve factory Player90/Hunter270 headings and the first input turn.
//   The straight arm starts inside the Hunter corridor; Player gate access is not
//   claimed. The scene, navigation and assets remain unchanged. Finally restores
//   nullable gameplay device filters, subscriptions and Application.runInBackground.
//   Tick-zero readiness binds canonical Input; inert deferred duplicates must then
//   disappear after an observed later rendered frame, without skipping any ticks.
//   Thin test entries survive domain reload. Complete diagnostic rows are not a
//   completed run, replay, 30-chase population or participant acceptance result.
//   Original 016 per-interval RED evidence remains historical; 020 requires fresh
//   observations and never retries, replaces or extends an unfavorable arm.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Tests.Player;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Hunter
{
    public sealed class HunterRouteComparisonTests
    {
        [UnityTest]
        public IEnumerator RepeatedMidRoomCutsGainDistanceWhileTheStraightCorridorCloses()
        {
            yield return new EnterPlayMode();
            yield return CompareRoutes();
        }

        private static IEnumerator CompareRoutes()
        {
            const int repetitions = 3;
            var arms = new AuthoredHunterTrial[repetitions * 2];
            var manifest = new FixedHorizonComparisonReport
            {
                utc = DateTime.UtcNow.ToString("o"),
                protocol = "fixed-horizon-route-comparison-020",
                priorSourceSha256 = "FAC8137A0CED585673BDC82549F10D5E5F469C002D9B770600B6F5CB0D3774BB",
                repetitions = repetitions, ticksPerArm = 240,
                reportPaths = new string[arms.Length], armStatus = new string[arms.Length]
            };
            for (int pair = 0; pair < repetitions; pair++)
            {
                var straight = new AuthoredHunterTrial(AuthoredHunterRoute.Straight);
                var cuts = new AuthoredHunterTrial(AuthoredHunterRoute.MidCuts);
                arms[pair * 2] = straight; arms[pair * 2 + 1] = cuts;
                straight.Report.pairedReportPath = cuts.ReportPath;
                cuts.Report.pairedReportPath = straight.ReportPath;
            }
            string manifestPath = Path.Combine(Path.GetDirectoryName(arms[0].ReportPath),
                "FixedHorizonComparison020-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".json");
            for (int arm = 0; arm < arms.Length; arm++)
            {
                manifest.reportPaths[arm] = arms[arm].ReportPath; manifest.armStatus[arm] = "reserved";
                arms[arm].Report.scope += " Prospective contract 020: three fixed 240-tick pairs; route-level advantage only, not isolated-cut or human tag-feel acceptance.";
                arms[arm].Report.events.Add("comparison-020:pair:" + (arm / 2 + 1) + ":reserved:manifest:" + manifestPath);
                arms[arm].WriteReport();
            }
            Action saveManifest = () => File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            saveManifest();
            TestContext.WriteLine("Prospective route comparison 020 reserved six arms: " + manifestPath);
            try
            {
                // Observe all declared arms before assessing outcomes. Failed/censored
                // observations remain in place; an escaping interruption leaves the
                // reserved denominator and active arm visible without replacement.
                for (int arm = 0; arm < arms.Length; arm++)
                {
                    manifest.armStatus[arm] = "started"; saveManifest();
                    yield return AuthoredHunterTrial.Exercise(arms[arm]);
                    manifest.armStatus[arm] = "observed"; manifest.observedArms++; saveManifest();
                }
                manifest.collectionComplete = true;
                // Retain same-tick controls and negative intervals for every pair
                // before an assertion can stop assessment of a later pair.
                for (int pair = 0; pair < repetitions; pair++)
                {
                    var straight = arms[pair * 2]; var cuts = arms[pair * 2 + 1];
                    foreach (AuthoredCutWindow window in cuts.Report.windows)
                        if (window.startTick > 0 && window.endTick <= straight.Report.rows.Count)
                        {
                            window.controlAvailable = true;
                            window.straightDistanceChange = straight.Report.rows[(int)window.endTick - 1].distance -
                                straight.Report.rows[(int)window.startTick - 1].distance;
                        }
                    straight.WriteReport(); cuts.WriteReport();
                    foreach (AuthoredCutWindow window in cuts.Report.windows)
                        TestContext.WriteLine("Pair " + (pair + 1) + " cut interval " + window.startTick + ".." + window.endTick +
                            ": actual distance change=" + window.distanceChange.ToString("R", CultureInfo.InvariantCulture) +
                            "; same-tick straight change=" + window.straightDistanceChange.ToString("R", CultureInfo.InvariantCulture) +
                            "; control available=" + window.controlAvailable + "; includes every tick, sight state and attack phase; diagnostic only.");
                }
                saveManifest();
                for (int pair = 0; pair < repetitions; pair++)
                {
                    var straight = arms[pair * 2]; var cuts = arms[pair * 2 + 1];
                    straight.AssertCommon(); cuts.AssertCommon();
                    Assert.That(straight.Report.rows.Count, Is.EqualTo(240));
                    Assert.That(cuts.Report.rows.Count, Is.EqualTo(240));
                    Assert.That(straight.Report.initialDistance, Is.EqualTo(cuts.Report.initialDistance));
                    Assert.That(straight.Report.initialHunterForward, Is.EqualTo(cuts.Report.initialHunterForward));
                    Assert.That(straight.Report.initialPlayerHeading, Is.EqualTo(cuts.Report.initialPlayerHeading));
                    Assert.That(straight.Report.sourceRevision, Is.EqualTo(arms[0].Report.sourceRevision));
                    Assert.That(cuts.Report.sourceRevision, Is.EqualTo(arms[0].Report.sourceRevision));
                    Assert.That(straight.Report.configHash, Is.EqualTo(arms[0].Report.configHash));
                    Assert.That(cuts.Report.configHash, Is.EqualTo(arms[0].Report.configHash));
                    Assert.That(straight.Report.rows.Last().distance, Is.LessThan(straight.Report.initialDistance),
                        "The unchanged faster Hunter must close physical distance over the full straight arm. " + straight.ReportPath);
                    Assert.That(straight.Report.rows.All(row => Math.Abs(row.playerPosition.z + 15f) < 0.05f), Is.True,
                        "The straight comparison may not leave its authored corridor.");
                    Assert.That(cuts.Report.turns.Count, Is.GreaterThanOrEqualTo(3),
                        "Repeated cuts require three observed waypoint crossings, not repeated input flags.");
                    Assert.That(cuts.Report.windows.Count, Is.GreaterThanOrEqualTo(2));
                    foreach (AuthoredCutWindow window in cuts.Report.windows) Assert.That(window.controlAvailable, Is.True);
                    float straightNet = straight.Report.rows.Last().distance - straight.Report.initialDistance;
                    float cutsNet = cuts.Report.rows.Last().distance - cuts.Report.initialDistance;
                    TestContext.WriteLine("Contract 020 pair " + (pair + 1) + " full ticks 0..240: straight net=" +
                        straightNet.ToString("R", CultureInfo.InvariantCulture) + "; MidCuts net=" +
                        cutsNet.ToString("R", CultureInfo.InvariantCulture) + "; route-level advantage=" +
                        (cutsNet - straightNet).ToString("R", CultureInfo.InvariantCulture));
                    Assert.That(cuts.Report.rows.Last().distance, Is.GreaterThan(cuts.Report.initialDistance),
                        "The complete MidCuts route must gain physical distance over the fixed horizon. " + cuts.ReportPath);
                    Assert.That(cutsNet, Is.GreaterThan(straightNet),
                        "The full-horizon MidCuts net gain must exceed its paired straight net change. " + cuts.ReportPath);
                }
                manifest.assertionsPassed = true;
            }
            finally
            {
                for (int arm = 0; arm < manifest.armStatus.Length; arm++)
                    if (manifest.armStatus[arm] == "started") manifest.armStatus[arm] = "interrupted";
                manifest.finishedUtc = DateTime.UtcNow.ToString("o"); saveManifest();
            }
        }

        [Serializable]
        private sealed class FixedHorizonComparisonReport
        {
            public string utc, finishedUtc, protocol, priorSourceSha256;
            public int repetitions, ticksPerArm, observedArms;
            public string[] reportPaths, armStatus;
            public bool collectionComplete, assertionsPassed;
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }

    internal enum AuthoredHunterRoute { Straight, MidCuts, BraidCutOff }

    // One shared lifecycle is necessary to keep the paired comparison and CutOff
    // observations on identical real input, capture and cleanup contracts.
    internal sealed class AuthoredHunterTrial : IDisposable
    {
        private const string ScenePath = "Assets/Scenes/TagArena.unity";
        private const string NavigationPath = "Assets/Greybox/TagArena/TagArenaNavMesh.asset";
        private const float Dt = 1f / 60f;
        private readonly AuthoredHunterRoute route;
        private readonly Vector3 playerSpawn, hunterSpawn;
        private readonly Vector3[] waypoints;
        private readonly Dictionary<Object, string> snapshots = new Dictionary<Object, string>();
        private RunSessionManager run;
        private InputManager input;
        private TelemetryManager telemetry;
        private PlayerManager player;
        private HunterManager hunter;
        private HunterDriver driver;
        private HunterBehaviorState hunterState;
        private HunterDriverState motor;
        private ChaseManager chase;
        private LevelManager level;
        private PlayerProfile playerProfile;
        private HunterProfile hunterProfile;
        private HunterMotorDriverConfig motorConfig;
        private InputReplayDriverState recording;
        private InputDriverState gates;
        private InputActionMap gameplay;
        private ReadOnlyArray<InputDevice>? previousDevices;
        private InputFrame supplied;
        private int captures, readyCount, frames, waypoint, circuits, observedRoom;
        private int tickBudget = 240;
        private long routeFinishedTick = -1;
        private Vector3 previousHunter, legStart;
        private double initialFixedTime;
        private bool devicesSaved, disposed, closed, ready, done;
        private readonly List<int> observedRooms = new List<int>();
        public readonly AuthoredHunterReport Report = new AuthoredHunterReport();
        public readonly string ReportPath;

        public AuthoredHunterTrial(AuthoredHunterRoute route)
        {
            this.route = route;
            bool straight = route == AuthoredHunterRoute.Straight;
            bool braid = route == AuthoredHunterRoute.BraidCutOff;
            playerSpawn = straight ? new Vector3(14f, 0f, -15f) : braid ? new Vector3(20f, 0f, 4f) : new Vector3(-2f, 0f, -2f);
            hunterSpawn = straight ? new Vector3(22f, 0f, -15f) : braid ? new Vector3(28f, 0f, 4f) : new Vector3(6f, 0f, -2f);
            waypoints = straight ? Array.Empty<Vector3>() : braid
                ? new[] { new Vector3(4f, 0f, 4f), new Vector3(4f, 0f, -4f), new Vector3(22f, 0f, -4f), new Vector3(22f, 0f, 4f), playerSpawn }
                : new[] { new Vector3(-8f, 0f, -2f), new Vector3(-8f, 0f, 8f), new Vector3(-2f, 0f, 8f), playerSpawn };
            Report.route = route.ToString(); Report.utc = DateTime.UtcNow.ToString("o");
            Report.scope = "Authored geometry, unchanged defaults, arranged initial spawns, normal Session input/sensors/motors. " +
                "Complete per-tick diagnostic denominator; benchmark cutoff is an incomplete gameplay capture. No replay or population/participant acceptance.";
            Report.arrangement = "Factory Player heading90 and Hunter270 retained. Comparison mirrored west before data collection to match Hunter alignment; " +
                "normal Player input supplies the same180-degree first turn in both arms. Straight spawn is inside the Hunter-only corridor and proves no Player gate access.";
            Report.playerSpawn = playerSpawn; Report.hunterSpawn = hunterSpawn; Report.waypoints = waypoints;
            ReportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "GoalCompletion", "authored-routes",
                route + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".json"));
        }

        public static IEnumerator Exercise(AuthoredHunterTrial trial)
        {
            bool previousBackground = Application.runInBackground;
            var focus = new CaptureGateTrace("Authored-" + trial.route);
            Application.runInBackground = true;
            SceneManager.sceneLoaded += trial.Arrange;
            try
            {
                yield return focus.AdmitStableGameViewFocus();
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                if (load == null) { trial.Fail("Built TagArena could not be loaded."); yield break; }
                yield return Until(() => trial.ready || trial.Report.failures.Count > 0, 20f);
                if (!load.isDone || !trial.ready) { trial.Fail("SceneReady was not observed before the first tick."); yield break; }
                yield return Until(() => { trial.ObserveDeferredTeardown(); return trial.done || trial.Report.runEnded; }, trial.tickBudget * Dt * 5f + 20f);
                if (!trial.done && !trial.Report.runEnded) trial.Fail("Real fixed ticks did not reach the finite deadline.");
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.Arrange;
                try { trial.Close(); }
                finally
                {
                    trial.Dispose(); focus.Dispose(); Application.runInBackground = previousBackground;
                    trial.WriteReport();
                }
            }
        }

        private void Arrange(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ScenePath) return;
            try
            {
                var root = One<TagArenaSceneRoot>();
                // These are the only reflective writes: runtime factory requests,
                // before Start. All state/probe/counter reflection below is read-only.
                Field(typeof(TagArenaSceneRoot), "_spawnPosition").SetValue(root, playerSpawn);
                Field(typeof(TagArenaSceneRoot), "_hunterSpawnPosition").SetValue(root, hunterSpawn);
                playerProfile = Read<PlayerProfile>(root, "_playerProfile"); hunterProfile = Read<HunterProfile>(root, "_hunterProfile");
                Remember(playerProfile); Remember(hunterProfile); Remember(Read<ChaseConfig>(root, "_chaseConfig"));
                run = RunSessionManager.Instance != null ? RunSessionManager.Instance : Read<RunSessionManager>(root, "_run");
                run.CaptureStarted += OnCapture; run.CaptureEnded += OnCaptureEnded; run.RunEnded += OnRunEnded;
                TagArenaSceneRoot.SceneReady += OnReady;
                foreach (string path in new[] { ScenePath, NavigationPath, "Assets/Editor/Level/TagArenaLevelSetup.cs",
                    "Assets/Scripts/Domain/Hunter/Controller/HunterController.cs", "Assets/Scripts/Domain/Hunter/Driver/HunterDriver.cs",
                    "Assets/Scripts/Domain/Hunter/Driver/HunterSteeringPresenter.cs", "Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs",
                    "Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs", "Assets/Editor/Tests/Hunter/HunterCutOffIntegrationTests.cs" })
                    Report.files.Add(Stamp(path));
            }
            catch (Exception error) { Fail("Initial arrangement: " + error); }
        }

        private void OnCapture(RunCaptureMetadata metadata)
        {
            captures++;
            if (captures != 1) { Fail("Repeated capture startup; original identity retained."); return; }
            Report.sessionId = metadata.SessionId; Report.seed = metadata.Seed; Report.startTick = metadata.StartTick;
            Report.fixedDeltaTime = metadata.FixedDeltaTime; Report.sourceRevision = metadata.SourceRevision;
            Report.configHash = metadata.ConfigSnapshotHash; Report.consumptionOrder = metadata.RandomConsumptionOrder;
            telemetry = TelemetryManager.Instance;
            Report.csvPathAtCaptureStart = telemetry != null ? telemetry.LastOutputPath : "";
        }

        private void OnReady(SceneKey scene)
        {
            if (scene != SceneKey.TagArena) return;
            try
            {
                readyCount++;
                var root = One<TagArenaSceneRoot>();
                input = InputManager.Instance;
                Assert.That(input, Is.Not.Null); Assert.That(input.isActiveAndEnabled, Is.True);
                Assert.That(Read<InputManager>(root, "_input"), Is.SameAs(input));
                Assert.That(Read<RunSessionManager>(root, "_run"), Is.SameAs(run));
                Report.readyFrame = Time.frameCount;
                Report.canonicalInputId = input.GetInstanceID(); Report.canonicalRunId = run.GetInstanceID();
                var inputs = Object.FindObjectsByType<InputManager>(FindObjectsSortMode.None);
                Report.inputObjectsAtReady = inputs.Length;
                Assert.That(inputs.Count(item => item.isActiveAndEnabled), Is.EqualTo(1));
                foreach (InputManager duplicate in inputs.Where(item => item != input))
                {
                    Assert.That(duplicate.enabled, Is.False); Assert.That(Read<bool>(duplicate, "_initialized"), Is.False);
                    Assert.That(Read<bool>(duplicate, "_subscribed"), Is.False);
                    Assert.That(duplicate.GetComponent<PlayerInputDriver>(), Is.Not.Null);
                    Assert.That(duplicate.GetComponent<PlayerInputDriver>().enabled, Is.False);
                }
                player = One<PlayerManager>(); hunter = One<HunterManager>();
                chase = One<ChaseManager>(); level = One<LevelManager>();
                driver = hunter.GetComponent<HunterDriver>(); motor = Read<HunterDriverState>(driver, "_state");
                hunterState = (HunterBehaviorState)hunter.ReadOnlyState; motorConfig = Read<HunterMotorDriverConfig>(driver, "_config");
                Remember(motorConfig); Remember(Read<PlayerMoverDriverConfig>(player.GetComponent<PlayerDriver>(), "_config"));
                Assert.That(run.Tick, Is.Zero); Assert.That(captures, Is.EqualTo(1));
                Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1)); Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                Assert.That(player.ReadOnlyState.Position, Is.EqualTo(playerSpawn)); Assert.That(hunterState.Position, Is.EqualTo(hunterSpawn));
                Assert.That(hunterState.TargetId, Is.EqualTo(player.Id)); Assert.That(level.ReadOnlyState.IsReady, Is.True);
                Assert.That(playerProfile.SprintSpeed, Is.EqualTo(8f)); Assert.That(playerProfile.GroundAcceleration, Is.EqualTo(60f));
                Assert.That(hunterProfile.Acceleration, Is.EqualTo(20f)); Assert.That(hunterProfile.TurnRate, Is.EqualTo(240f));
                Assert.That(hunterProfile.ChaseSpeedMultiplier, Is.EqualTo(1.12f)); Assert.That(hunterProfile.SensorIntervalTicks, Is.EqualTo(4));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(Dt)); Assert.That(Report.fixedDeltaTime, Is.EqualTo(Dt));
                Assert.That(Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None), Is.Empty);
                VerifyRouteGeometry();
                Report.initialDistance = Vector3.Distance(playerSpawn, hunterSpawn);
                Report.initialPlayerHeading = player.ReadOnlyState.HeadingDegrees; Report.initialHunterForward = hunterState.Forward;
                Report.initialPlayerVelocity = player.ReadOnlyState.Velocity; Report.initialHunterVelocity = hunterState.Velocity;
                Assert.That(Report.initialPlayerVelocity, Is.EqualTo(Vector3.zero)); Assert.That(Report.initialHunterVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(Report.initialPlayerHeading, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(Vector3.Distance(Report.initialHunterForward, Vector3.left), Is.LessThan(0.0001f));
                Report.playerId = player.Id.ToString(); Report.hunterId = hunter.Id.ToString();
                Report.playerSprintSpeed = playerProfile.SprintSpeed;
                Report.hunterChaseSpeed = playerProfile.SprintSpeed * hunterProfile.ChaseSpeedMultiplier;
                if (route == AuthoredHunterRoute.BraidCutOff)
                {
                    // Two52m circuits plus the worst-case full velocity reversal
                    // budget at each commanded corner; this is a test deadline,
                    // not a movement tuning override. Successful runs stop earlier
                    // at the second actual circuit plus one real sensor interval.
                    tickBudget = Mathf.CeilToInt((104f / playerProfile.SprintSpeed +
                        10f * 2f * playerProfile.SprintSpeed / playerProfile.GroundAcceleration) / Dt) + hunterProfile.SensorIntervalTicks;
                }
                Report.tickBudget = tickBudget;
                gameplay = Read<InputActionMap>(input.GetComponent<PlayerInputDriver>(), "_actions");
                previousDevices = gameplay.devices.HasValue
                    ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                devicesSaved = true; gameplay.devices = Array.Empty<InputDevice>();
                foreach (InputAction action in gameplay.actions) Assert.That(action.controls.Count, Is.Zero);
                Assert.That(input.SetSource(InputSource.Live), Is.True);
                recording = Read<InputReplayDriverState>(input.GetComponent<InputRecorder>(), "_state");
                gates = Read<InputDriverState>(input.GetComponent<PlayerInputDriver>(), "_state");
                Assert.That(recording.Metadata.SessionId, Is.EqualTo(Report.sessionId)); Assert.That(recording.Recorded.Count, Is.Zero);
                input.FramePublished += Supply; run.TickAdvanced += OnTick;
                hunter.OnSighting += OnSighting; hunter.OnLungeHit += OnHit;
                run.ChaseStarted += OnStarted; run.ChaseEnded += OnEnded;
                previousHunter = hunterState.Position; legStart = playerSpawn; initialFixedTime = Time.fixedTimeAsDouble;
                ready = true;
            }
            catch (Exception error) { Fail("Readiness: " + error); }
        }

        private void VerifyRouteGeometry()
        {
            var graph = level.ReadOnlyState.Graph;
            Assert.That(graph.Edges.Any(edge => edge.Id == 104 && edge.Access == TraversalAccess.Hunter), Is.True);
            Assert.That(graph.Edges.Any(edge => edge.Id == 102), Is.True); Assert.That(graph.Edges.Any(edge => edge.Id == 103), Is.True);
            var pillar = Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).Single(marker => marker.SurfaceId == 202);
            Bounds bounds = pillar.GetComponent<Collider>().bounds;
            Assert.That(bounds.center, Is.EqualTo(new Vector3(-5f, 2f, 3f)));
            Assert.That(bounds.size, Is.EqualTo(new Vector3(3f, 4f, 6f)));
            Report.pillarCenter = bounds.center; Report.pillarSize = bounds.size;
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(item => item.name == "Hunter-only Straight Corridor"), Is.True);
        }

        private void ObserveDeferredTeardown()
        {
            if (!ready || Report.servicesSettled || Time.frameCount <= Report.readyFrame) return;
            try
            {
                Assert.That(One<InputManager>(), Is.SameAs(input)); Assert.That(One<RunSessionManager>(), Is.SameAs(run));
                Assert.That(InputManager.Instance, Is.SameAs(input)); Assert.That(RunSessionManager.Instance, Is.SameAs(run));
                Report.servicesSettled = true; Report.servicesSettledFrame = Time.frameCount;
            }
            catch (Exception error) { Fail("Deferred service teardown: " + error); }
        }

        private void Supply(InputFrame physical)
        {
            if (!ready || closed || Report.runEnded) return;
            frames++;
            if (!physical.Equals(default(InputFrame))) Fail("Isolated gameplay producer emitted non-neutral hardware input.");
            float heading = 270f;
            Vector2 move = Vector2.up;
            if (waypoints.Length > 0)
            {
                // Aim at the authored waypoint through ordinary heading input.
                // This corrects collision/inertial lateral error without moving
                // the body or silently widening the waypoint crossing predicate.
                Vector3 delta = waypoints[waypoint] - player.ReadOnlyState.Position;
                heading = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                if (routeFinishedTick >= 0) move = Vector2.zero;
            }
            supplied = new InputFrame(move, new Vector2(Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, heading), 0f),
                InputButtons.Sprint, InputButtons.None, InputButtons.None);
            run.ReceiveInput(supplied);
        }

        private void OnTick(InputFrame frame, float dt, long tick)
        {
            if (!ready || closed) return;
            try
            {
                InputProbeRecord record = player.LastProbeRecord;
                var row = new AuthoredHunterRow
                {
                    tick = tick, dt = dt, fixedTime = Time.fixedTimeAsDouble, frame = Time.frameCount, schemaVersion = record.SchemaVersion,
                    inputMove = frame.Move, lookDelta = frame.LookDelta, held = (int)frame.Held, pressed = (int)frame.Pressed, released = (int)frame.Released,
                    playerPosition = record.Resolution.Position, playerVelocity = record.Resolution.Velocity,
                    playerHeading = player.ReadOnlyState.HeadingDegrees, health = player.ReadOnlyState.Health,
                    grounded = record.Resolution.Grounded, resolutionPresent = record.Resolution.Present,
                    ceiling = record.Resolution.Ceiling, eyePosition = record.Resolution.EyePosition,
                    movement = player.ReadOnlyState.MovementState.ToString(), probe = new AuthoredPlayerProbe(record.Probe),
                    hunterBefore = previousHunter, hunterPosition = hunterState.Position, hunterVelocity = hunterState.Velocity,
                    hunterForward = hunterState.Forward, visible = hunterState.PlayerVisible,
                    lastKnownPosition = hunterState.LastKnownPosition, lastKnownTick = hunterState.LastKnownTick, belief = hunterState.BeliefConfidence,
                    action = hunterState.CurrentAction.ToString(), lunge = hunterState.LungePhase.ToString(),
                    navigationTarget = Read<Vector3>(hunterState, "NavigationTarget"), replans = Read<int>(hunterState, "ReplanCount"),
                    lastRoom = Read<int>(hunterState, "LastRoom"), loopDetected = Read<bool>(hunterState, "LoopDetected"),
                    roomHistory = Read<List<int>>(hunterState, "RecentRooms").ToArray(),
                    pathAvailable = driver.PathAvailable, pathCooldown = motor.PathCooldown, cornerIndex = motor.Steering.CornerIndex,
                    corners = motor.Steering.Corners.ToArray(), pathTarget = motor.LastTarget,
                    chase = chase.ReadOnlyState.Phase.ToString(), chaseId = chase.ReadOnlyState.ChaseId,
                    distance = Vector3.Distance(player.ReadOnlyState.Position, hunterState.Position),
                    waypoint = waypoint, circuits = circuits, inputRecords = recording.Recorded.Count,
                    inputGate = gates.InputEnabled, ownerGate = gates.OwnerEnabled, focusGate = gates.HasFocus,
                    captureInterrupted = recording.CaptureInterrupted,
                    traversals = player.LastTraversalFacts.Select(fact => fact.Kind + ":" + fact.Succeeded + ":" + fact.Tick).ToArray()
                };
                Report.rows.Add(row);
                if (tick != Report.rows.Count || record.Tick != tick || frames != Report.rows.Count) Fail("Nonconsecutive input/Run/probe tick denominator.");
                if (!frame.Equals(supplied) || !frame.Equals(record.Input)) Fail("Actual Session input differs from supplied/recorded input.");
                if (dt != Dt || record.DeltaTime != dt || record.SchemaVersion != InputProbeRecord.CurrentSchemaVersion || !record.Resolution.Present)
                    Fail("Missing resolution or wrong recorded schema/real fixed delta.");
                if (!Finite(row.playerPosition) || !Finite(row.playerVelocity) || !Finite(row.hunterPosition) || !Finite(row.hunterVelocity)) Fail("Nonfinite actual motion.");
                if (!row.inputGate || !row.ownerGate || !row.focusGate || row.captureInterrupted || row.inputRecords != Report.rows.Count)
                    Fail("Real capture/input gates failed or did not retain every tick.");
                if (Math.Abs(row.fixedTime - initialFixedTime - tick * (double)dt) > 0.001) Fail("Session ticks did not correspond to observed Unity fixed time.");
                if (Vector3.Distance(player.transform.position, row.playerPosition) > 0.0001f || Vector3.Distance(hunter.transform.position, row.hunterPosition) > 0.0001f)
                    Fail("Component/state positions do not match actual transforms.");
                if (row.playerPosition.y < -0.1f || row.hunterPosition.y < -0.1f) Fail("Body fell below the authored floor.");
                ObserveRoom(row); ObserveCutOff(row); AdvanceRoute(row);
                previousHunter = row.hunterPosition;
                bool boundary = tick >= tickBudget || (routeFinishedTick >= 0 && tick >= routeFinishedTick + hunterProfile.SensorIntervalTicks);
                if (boundary)
                {
                    done = true; Report.cutoffTick = tick;
                    Report.cutoffReason = tick >= tickBudget ? "fixed tick budget" : "two actual circuits plus one sensor interval";
                    // Preserve an actual pending death and its normal complete
                    // terminal capture; otherwise use the real scene-load close.
                    if (player.ReadOnlyState.IsAlive) Close();
                }
            }
            catch (Exception error)
            {
                Fail("Tick " + tick + ": " + error);
                if (tick >= tickBudget) { done = true; Close(); }
            }
        }

        private void AdvanceRoute(AuthoredHunterRow row)
        {
            if (waypoints.Length == 0 || routeFinishedTick >= 0) return;
            Vector3 target = waypoints[waypoint]; Vector3 direction = (target - legStart).normalized;
            Vector3 delta = row.playerPosition - target; delta.y = 0f;
            // Advance only after actual feet cross the waypoint plane within
            // one Player radius laterally. No teleport or state repair is used.
            float lateral = Vector3.Cross(delta, direction).magnitude;
            float radius = player.GetComponent<CapsuleCollider>().radius;
            if (Vector3.Dot(delta, direction) < 0f || lateral > radius) return;
            int old = waypoint; legStart = target; waypoint = (waypoint + 1) % waypoints.Length;
            if (waypoint == 0) circuits++;
            float oldHeading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            Vector3 next = waypoints[waypoint] - legStart;
            float turn = Mathf.Abs(Mathf.DeltaAngle(oldHeading, Mathf.Atan2(next.x, next.z) * Mathf.Rad2Deg));
            if (turn > 1f) Report.turns.Add(new AuthoredRouteTurn { tick = row.tick, fromWaypoint = old, position = row.playerPosition, degrees = turn });
            if (route == AuthoredHunterRoute.BraidCutOff && circuits == 2) routeFinishedTick = row.tick;
        }

        private void ObserveRoom(AuthoredHunterRow row)
        {
            foreach (LevelRoom room in level.ReadOnlyState.Graph.Rooms)
            {
                if (!room.Bounds.Contains(row.hunterBefore) || room.Id == observedRoom) continue;
                observedRoom = room.Id; observedRooms.Add(room.Id);
                Report.roomEntries.Add(new AuthoredRoomEntry { tick = row.tick, room = room.Id, hunterPosition = row.hunterBefore });
                break;
            }
        }

        private void ObserveCutOff(AuthoredHunterRow row)
        {
            if (row.action != HunterAction.CutOff.ToString()) return;
            row.cutOffObserved = true;
            Vector3 predicted = row.playerPosition + player.ReadOnlyState.Velocity * hunterProfile.CutOffPredictionSeconds;
            var reachable = LevelGraphUtility.TopologicalDistancesFrom(level.ReadOnlyState.Graph, row.lastRoom, TraversalAccess.Hunter);
            LevelRoom selected = level.ReadOnlyState.Graph.Rooms.Where(room => reachable[room.Id] >= 0)
                .OrderBy(room => Vector3.SqrMagnitude(RoomTarget(room) - predicted)).First();
            row.expectedIntercept = RoomTarget(selected); row.expectedInterceptRoom = selected.Id;
            row.observedRoomVisits = observedRooms.Count(room => room == row.lastRoom);
            if (Vector3.Distance(row.navigationTarget, row.expectedIntercept) > 0.0001f) Fail("CutOff did not select the closest reachable predicted room target.");
        }

        private static Vector3 RoomTarget(LevelRoom room) => new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
        private void OnSighting(HunterSighting fact) => Report.events.Add("sighting:" + fact.Tick + ":" + fact.Visible + ":" + fact.Distance.ToString("R", CultureInfo.InvariantCulture));
        private void OnHit(HunterHit hit) => Report.events.Add("hit:" + hit.Tick + ":" + hit.Damage);
        private void OnStarted(ChaseFact fact) { Report.chaseStarts++; Report.events.Add("chase-start:" + fact.Tick + ":" + fact.ChaseId); }
        private void OnEnded(ChaseFact fact) => Report.events.Add("chase-end:" + fact.Tick + ":" + fact.ChaseId + ":" + fact.EndReason);
        private void OnCaptureEnded(long tick, bool complete)
        {
            Report.captureEnds.Add(new AuthoredCaptureEnd { tick = tick, complete = complete });
            Report.inputPath = input != null ? input.LastRecordingPath : "";
            Report.csvPath = telemetry != null ? telemetry.LastOutputPath : "";
        }
        private void OnRunEnded(RunSummary summary) { Report.runEnded = true; Report.events.Add("run-end:" + run.Tick + ":" + summary.EndReason); }

        private void Close()
        {
            if (closed) return;
            closed = true;
            if (run != null && !Report.runEnded) run.SuspendForSceneLoad();
            Report.observedTicks = Report.rows.Count; Report.actualCircuits = circuits; Report.routeFinishedTick = routeFinishedTick;
            Report.inputPath = input != null ? input.LastRecordingPath : "";
            Report.csvPath = telemetry != null ? telemetry.LastOutputPath : "";
            Report.inputError = input != null ? input.LastRecordingError : "unavailable";
            Report.telemetryError = telemetry != null ? telemetry.LastError : "unavailable";
            if (recording != null)
            {
                Report.recordedTicks = recording.Recorded.Count; Report.recordedEndTick = recording.EndTick;
                Report.recordingComplete = recording.CaptureComplete; Report.recordingInterrupted = recording.CaptureInterrupted;
            }
            foreach (var snapshot in snapshots)
                if (snapshot.Key == null || EditorJsonUtility.ToJson(snapshot.Key) != snapshot.Value) Fail("A profile/config asset changed during the trial.");
            foreach (AuthoredFileStamp file in Report.files)
                if (!File.Exists(file.path) || Hash(file.path) != file.sha256) Fail("Source/scene/navigation file drift: " + file.path);
            for (int index = 0; index + 1 < Report.turns.Count; index++)
            {
                long start = Report.turns[index].tick, end = Report.turns[index + 1].tick;
                if (start > 0 && end <= Report.rows.Count)
                    Report.windows.Add(new AuthoredCutWindow { startTick = start, endTick = end,
                        distanceChange = Report.rows[(int)end - 1].distance - Report.rows[(int)start - 1].distance });
            }
            foreach (string path in new[] { Report.inputPath, Report.csvPath })
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) Report.captureFiles.Add(Stamp(path));
        }

        public void AssertCommon()
        {
            Assert.That(Report.failures, Is.Empty, ReportPath);
            Assert.That(readyCount, Is.EqualTo(1)); Assert.That(captures, Is.EqualTo(1));
            Assert.That(Report.servicesSettled, Is.True, "Strict service uniqueness must follow an actual rendered frame.");
            Assert.That(Report.startTick, Is.Zero); Assert.That(Report.sessionId, Is.Not.Null.And.Not.Empty);
            Assert.That(Report.chaseStarts, Is.GreaterThan(0), "Real sensed confirmation must occur. " + ReportPath);
            Assert.That(Report.runEnded, Is.False, "A natural terminal run before the benchmark boundary is retained as a failed/censored route. " + ReportPath);
            Assert.That(Report.recordedTicks, Is.EqualTo(Report.observedTicks));
            Assert.That(Report.recordedEndTick, Is.EqualTo(Report.observedTicks));
            Assert.That(Report.captureEnds.Count, Is.EqualTo(1)); Assert.That(Report.captureEnds[0].complete, Is.False);
            Assert.That(Report.recordingComplete || Report.recordingInterrupted, Is.False);
            Assert.That(Report.inputError, Is.Empty); Assert.That(Report.telemetryError, Is.Empty);
            Assert.That(Report.inputPath, Is.Not.Empty); Assert.That(Report.csvPath, Is.Not.Empty);
        }

        public void WriteReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(Report, true));
            TestContext.WriteLine("Authored Hunter " + route + " diagnostic: " + ReportPath +
                "; original SessionId=" + Report.sessionId + "; ticks=" + Report.observedTicks + "; input=" + Report.inputPath +
                "; CSV=" + Report.csvPath + "; capture complete=" + Report.recordingComplete +
                "; incomplete benchmark cutoff is not a gameplay loss, catch or finished replay; failures=" + string.Join(" | ", Report.failures));
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            TagArenaSceneRoot.SceneReady -= OnReady;
            if (input != null) input.FramePublished -= Supply;
            if (run != null)
            {
                run.TickAdvanced -= OnTick; run.CaptureStarted -= OnCapture; run.CaptureEnded -= OnCaptureEnded;
                run.RunEnded -= OnRunEnded; run.ChaseStarted -= OnStarted; run.ChaseEnded -= OnEnded;
            }
            if (hunter != null) { hunter.OnSighting -= OnSighting; hunter.OnLungeHit -= OnHit; }
            if (devicesSaved && gameplay != null) gameplay.devices = previousDevices;
        }

        private void Remember(Object asset)
        {
            Assert.That(asset, Is.Not.Null); snapshots[asset] = EditorJsonUtility.ToJson(asset);
            string path = AssetDatabase.GetAssetPath(asset); if (!string.IsNullOrEmpty(path)) Report.files.Add(Stamp(path));
        }
        private void Fail(string failure) { if (!Report.failures.Contains(failure)) Report.failures.Add(failure); }
        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        private static T One<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Single();
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(type.FullName, name);
        private static T Read<T>(object owner, string name) => (T)Field(owner.GetType(), name).GetValue(owner);
        private static AuthoredFileStamp Stamp(string path) => new AuthoredFileStamp { path = path, sha256 = Hash(path) };
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""); }
        private static IEnumerator Until(Func<bool> complete, float seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            for (int iteration = 0; iteration < 60000 && !complete() && Time.realtimeSinceStartupAsDouble < deadline; iteration++) yield return null;
        }
    }

    [Serializable] internal sealed class AuthoredHunterReport
    {
        public string utc, route, scope, arrangement, sessionId, sourceRevision, configHash, consumptionOrder, playerId, hunterId;
        public string inputPath = "", csvPath = "", csvPathAtCaptureStart = "", inputError = "", telemetryError = "", cutoffReason = "", pairedReportPath = "";
        public int seed, tickBudget, observedTicks, recordedTicks, actualCircuits, chaseStarts;
        public int readyFrame, servicesSettledFrame, canonicalInputId, canonicalRunId, inputObjectsAtReady;
        public long startTick, recordedEndTick, routeFinishedTick, cutoffTick;
        public float fixedDeltaTime, initialDistance, initialPlayerHeading, playerSprintSpeed, hunterChaseSpeed;
        public Vector3 playerSpawn, hunterSpawn, initialHunterForward, initialPlayerVelocity, initialHunterVelocity, pillarCenter, pillarSize;
        public Vector3[] waypoints;
        public bool runEnded, recordingComplete, recordingInterrupted, servicesSettled;
        public List<AuthoredHunterRow> rows = new List<AuthoredHunterRow>();
        public List<AuthoredRouteTurn> turns = new List<AuthoredRouteTurn>();
        public List<AuthoredCutWindow> windows = new List<AuthoredCutWindow>();
        public List<AuthoredRoomEntry> roomEntries = new List<AuthoredRoomEntry>();
        public List<AuthoredCaptureEnd> captureEnds = new List<AuthoredCaptureEnd>();
        public List<AuthoredFileStamp> files = new List<AuthoredFileStamp>(), captureFiles = new List<AuthoredFileStamp>();
        public List<string> events = new List<string>(), failures = new List<string>();
    }
    [Serializable] internal sealed class AuthoredHunterRow
    {
        public long tick, lastKnownTick; public double fixedTime; public float dt, playerHeading, health, distance, belief, pathCooldown;
        public int frame, schemaVersion, held, pressed, released, replans, lastRoom, cornerIndex, waypoint, circuits, inputRecords, chaseId, expectedInterceptRoom, observedRoomVisits;
        public Vector2 inputMove, lookDelta;
        public Vector3 playerPosition, playerVelocity, eyePosition, hunterBefore, hunterPosition, hunterVelocity, hunterForward, lastKnownPosition, navigationTarget, pathTarget, expectedIntercept;
        public Vector3[] corners; public int[] roomHistory; public string[] traversals;
        public string movement, action, lunge, chase;
        public bool grounded, ceiling, resolutionPresent, visible, loopDetected, pathAvailable, inputGate, ownerGate, focusGate, captureInterrupted, cutOffObserved;
        public AuthoredPlayerProbe probe;
    }
    [Serializable] internal sealed class AuthoredPlayerProbe
    {
        public bool grounded, wallDetected, vaultCandidate, standingBlocked;
        public float wallDistance, wallAngle, vaultHeight, vaultClearance; public int wallId;
        public Vector3 groundNormal, wallNormal, vaultTarget;
        public AuthoredPlayerProbe(MovementProbe p)
        { grounded = p.Grounded; groundNormal = p.GroundNormal; wallDetected = p.WallDetected; wallDistance = p.WallDistance;
            wallNormal = p.WallNormal; wallAngle = p.WallAngleDegrees; wallId = p.WallId; vaultCandidate = p.VaultCandidate;
            vaultHeight = p.VaultHeight; vaultClearance = p.VaultClearance; vaultTarget = p.VaultTarget; standingBlocked = p.StandingBlocked; }
    }
    [Serializable] internal sealed class AuthoredFileStamp { public string path, sha256; }
    [Serializable] internal sealed class AuthoredCaptureEnd { public long tick; public bool complete; }
    [Serializable] internal sealed class AuthoredRouteTurn { public long tick; public int fromWaypoint; public Vector3 position; public float degrees; }
    [Serializable] internal sealed class AuthoredCutWindow { public long startTick, endTick; public float distanceChange, straightDistanceChange; public bool controlAvailable; }
    [Serializable] internal sealed class AuthoredRoomEntry { public long tick; public int room; public Vector3 hunterPosition; }
}
