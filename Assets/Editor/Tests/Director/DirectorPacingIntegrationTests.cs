// ============================================================================
// DirectorPacingIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Director pacing through real FloorLoop actors and ordered Run ticks.
//   One arranged trial observes physical sight loss, relief and exit-open cadence;
//   another follows a delayed hint until the actual Hunter enters player proximity.
//   Tick-complete sidecars retain the observations and their deliberate limitations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Director integration.
// KEY RESPONSIBILITIES:
//   - Observe real Chase transitions and frozen default Director timing twice per seed.
//   - Record every pose, pressure observation, hint belief and no-proximity interval.
//   - Distinguish finite motor arrival from emitted hints and censored observation gaps.
// DEPENDENCIES:
//   - Core; Domain Player, Hunter, Chase, Floor; Session.Run; FloorLoopSceneRoot.
//   - Presentation.Input device isolation; Unity physics/navigation; Editor asset reads.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Runtime-only initial spawn wiring, low
//   containment rails and an occlusion screen arrange the cadence trial; Floor.Collect
//   arranges its exit transition and is not a physical collection measurement.
//   The travel trial changes only initial Player spawn, then uses stationary input.
//   Real Run ticks and Hunter motors continue; no fake probes, state writes, direct
//   ticks, tuning changes, teleports, asset saves or time-scale changes are performed.
//   Devices are isolated on the owned gameplay map and its prior filter is restored.
//   SceneReady resolves canonical persistent services before deferred duplicate
//   destruction; uniqueness is required on the first observed later frame. Disabled
//   pending Input duplicates must have no initialized state or subscription. Repeated
//   trials retain identical canonical Run/Input identities and distinct capture IDs.
//   Default heat (20 s) dominates relief (10 s), so this does not independently prove
//   the relief predicate with heat held eligible. Neither trial completes a floor or
//   supplies participant/no-wandering acceptance. Capture is interrupted at a declared
//   cutoff; sidecar observations, not capture-complete status, delimit the population.
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
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Editor.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Director
{
    public sealed class DirectorPacingIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private const float Dt = 1f / 60f;
        private static readonly Vector3 CadencePlayer = new Vector3(-39f, 0.1f, 22f);
        private static readonly Vector3 CadenceHunter = new Vector3(-16f, 0.1f, 22f);
        private static readonly Vector3 TravelPlayer = new Vector3(-38f, 0.1f, 22f);

        [UnityTest, Timeout(240000)]
        public IEnumerator PhysicalLossReliefAndExitCadenceRepeatAtTheSameSeed()
        {
            yield return new EnterPlayMode();
            yield return ExerciseRepeatedCadence();
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator HistoricalHintReachesActualProximityWithEveryGapRetained()
        {
            yield return new EnterPlayMode();
            yield return ExerciseTrial(TrialKind.Travel, new TrialReport());
        }

        private static IEnumerator ExerciseRepeatedCadence()
        {
            var first = new TrialReport();
            var second = new TrialReport();
            yield return ExerciseTrial(TrialKind.Cadence, first);
            yield return ExerciseTrial(TrialKind.Cadence, second);
            Assert.That(second.seed, Is.EqualTo(first.seed));
            Assert.That(second.sourceRevision, Is.EqualTo(first.sourceRevision));
            Assert.That(second.captureConfigHash, Is.EqualTo(first.captureConfigHash));
            Assert.That(second.sessionId, Is.Not.EqualTo(first.sessionId), "Each replay must have a distinct capture identity.");
            Assert.That(second.canonicalInputInstanceId, Is.EqualTo(first.canonicalInputInstanceId), "Reload must retain the canonical Input service.");
            Assert.That(second.canonicalRunInstanceId, Is.EqualTo(first.canonicalRunInstanceId), "Reload must retain the canonical Run service.");
            Assert.That(second.hints.Select(hint => hint.deliveredTick), Is.EqualTo(first.hints.Select(hint => hint.deliveredTick)));
            Assert.That(second.hints.Select(hint => hint.observedTick), Is.EqualTo(first.hints.Select(hint => hint.observedTick)));
            Assert.That(second.chaseFacts.Select(fact => fact.tick), Is.EqualTo(first.chaseFacts.Select(fact => fact.tick)));
            Assert.That(second.exitOpenedTick, Is.EqualTo(first.exitOpenedTick));
            for (int i = 0; i < first.hints.Count; i++)
                Assert.That(Vector3.Distance(first.hints[i].belief, second.hints[i].belief), Is.LessThan(0.001f),
                    "Same seeded factory and Hunter decisions must reproduce delivered uncertainty.");
            TestContext.WriteLine("DIRECTOR-PACING repeated seed timing/belief passed; reports=" +
                first.reportPath + " ; " + second.reportPath + ". These are contained sensor/cadence trials, not floor-travel acceptance.");
        }

        private static IEnumerator ExerciseTrial(TrialKind kind, TrialReport report)
        {
            bool previousBackground = Application.runInBackground;
            var trial = new Trial(kind, report);
            try
            {
                Application.runInBackground = true;
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(Dt).Within(0.000001f));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                SceneManager.sceneLoaded += trial.Arrange;
                var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build and enable FloorLoop before running this fixture.");
                double deadline = Time.realtimeSinceStartupAsDouble + (kind == TrialKind.Cadence ? 85d : 100d);
                while (!trial.Done && report.failure.Length == 0 && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    yield return null;
                    trial.ObserveDeferredTeardown();
                }
                Assert.That(report.failure, Is.Empty, trial.Describe());
                Assert.That(load.isDone && trial.Ready, Is.True, "FloorLoop readiness was not observed.");
                Assert.That(trial.Done, Is.True, "Finite observation cutoff not reached: " + trial.Describe());
                trial.AssertOutcome();
                report.passed = true;
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.Arrange;
                try { trial.Dispose(); }
                finally
                {
                    Application.runInBackground = previousBackground;
                    trial.WriteReport();
                }
            }
        }

        private enum TrialKind { Cadence, Travel }

        private sealed class Trial : IDisposable
        {
            private readonly TrialKind kind;
            private readonly TrialReport report;
            private readonly Dictionary<Object, string> snapshots = new Dictionary<Object, string>();
            private RunSessionManager run;
            private InputManager input;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private bool devicesSaved;
            private PlayerManager player;
            private HunterManager hunter;
            private HunterDriver driver;
            private HunterDriverState motor;
            private HunterBehaviorState hunterState;
            private ChaseManager chase;
            private FloorManager floor;
            private DirectorManager director;
            private DirectorConfig config;
            private HunterProfile hunterProfile;
            private HunterMotorDriverConfig motorConfig;
            private GameObject geometry;
            private BoxCollider screen;
            private int captures, readiness, producedFrames;
            private long gapStart = -1;
            private Vector3 previousHunter;
            private bool disposed;
            public bool Ready { get; private set; }
            public bool Done { get; private set; }

            public Trial(TrialKind kind, TrialReport report)
            {
                this.kind = kind; this.report = report;
                report.utc = DateTime.UtcNow.ToString("o"); report.kind = kind.ToString();
                report.scope = kind == TrialKind.Cadence
                    ? "Contained physical sight/loss, relief accumulation and public-collection exit cadence; no arrival or full floor."
                    : "Stationary arranged Player; authored Hunter spawn and navigation; initial and later proximity gaps through finite cutoff; no full floor.";
                report.measurementSemantics = "Distances are committed poses at every 60 Hz tick, including tick zero. A threshold crossing is bracketed by adjacent poses, not timed within a physics step. Closed gaps retain earliest/latest crossing ticks and duration bounds; censored gaps have no finite completed-duration upper bound. measuredTravelSeconds and initialGapSeconds are sampled upper bounds. initialGap<=heat+age+measuredTravel is algebraic consistency of hint emission time, not an independent travel-performance bound. Actual first proximity and the independent 3600-tick response deadline are separate observations.";
                report.captureCompleteExpected = false;
            }

            public void Arrange(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ScenePath) return;
                try
                {
                    var root = One<FloorLoopSceneRoot>();
                    Assert.That(Read<int>(root, "_seed"), Is.EqualTo(1));
                    Assert.That(Read<Vector3>(root, "_spawnPosition"), Is.EqualTo(FloorLoopLevelSetup.SpawnPosition));
                    Assert.That(Read<Vector3>(root, "_hunterSpawnPosition"), Is.EqualTo(FloorLoopLevelSetup.HunterSpawnPosition));
                    SpawnField("_spawnPosition").SetValue(root, kind == TrialKind.Cadence ? CadencePlayer : TravelPlayer);
                    if (kind == TrialKind.Cadence)
                    {
                        SpawnField("_hunterSpawnPosition").SetValue(root, CadenceHunter);
                        geometry = new GameObject("Director Pacing - Temporary Physical Containment");
                        SceneManager.MoveGameObjectToScene(geometry, scene);
                        Box("West low rail", new Vector3(-18f, 0.5f, 22f), new Vector3(0.4f, 1f, 6.4f));
                        Box("East low rail", new Vector3(-14f, 0.5f, 22f), new Vector3(0.4f, 1f, 6.4f));
                        Box("North low rail", new Vector3(-16f, 0.5f, 25f), new Vector3(4.4f, 1f, 0.4f));
                        Box("South low rail", new Vector3(-16f, 0.5f, 19f), new Vector3(4.4f, 1f, 0.4f));
                        screen = Box("Sight screen", new Vector3(-30f, 1.5f, 22f), new Vector3(0.4f, 3f, 8f));
                        screen.enabled = false;
                        Physics.SyncTransforms();
                    }
                    run = RunSessionManager.Instance != null ? RunSessionManager.Instance : Read<RunSessionManager>(root, "_run");
                    run.CaptureStarted += OnCapture;
                    FloorLoopSceneRoot.SceneReady += OnReady;
                }
                catch (Exception error) { Fail("Initial arrangement: " + error); }
            }

            private void OnCapture(RunCaptureMetadata metadata)
            {
                captures++;
                if (captures != 1) { Fail("Repeated capture startup; first identity retained."); return; }
                report.sessionId = metadata.SessionId; report.seed = metadata.Seed;
                report.sourceRevision = metadata.SourceRevision; report.captureConfigHash = metadata.ConfigSnapshotHash;
                report.startTick = metadata.StartTick; report.fixedDeltaTime = metadata.FixedDeltaTime;
            }

            private void OnReady(SceneKey scene)
            {
                if (scene != SceneKey.FloorLoop) return;
                try
                {
                    readiness++;
                    var root = One<FloorLoopSceneRoot>();
                    input = InputManager.Instance;
                    Assert.That(input, Is.Not.Null, "SceneReady must expose the initialized canonical Input service.");
                    Assert.That(Read<InputManager>(root, "_input"), Is.SameAs(input));
                    Assert.That(Read<RunSessionManager>(root, "_run"), Is.SameAs(run));
                    Assert.That(input.isActiveAndEnabled, Is.True);
                    report.canonicalInputInstanceId = input.GetInstanceID(); report.canonicalRunInstanceId = run.GetInstanceID();
                    report.serviceReadyFrame = Time.frameCount;
                    InputManager[] inputObjects = Object.FindObjectsByType<InputManager>(FindObjectsSortMode.None);
                    report.inputObjectsAtReady = inputObjects.Length;
                    Assert.That(inputObjects.Count(item => item.isActiveAndEnabled), Is.EqualTo(1), "Only the canonical Input publisher may remain enabled.");
                    foreach (var duplicate in inputObjects.Where(item => item != input))
                    {
                        Assert.That(duplicate.enabled, Is.False, "Pending duplicate must be disabled before SceneReady.");
                        Assert.That(Read<bool>(duplicate, "_initialized"), Is.False);
                        Assert.That(Read<bool>(duplicate, "_subscribed"), Is.False);
                        var duplicateDriver = duplicate.GetComponent<PlayerInputDriver>();
                        Assert.That(duplicateDriver, Is.Not.Null); Assert.That(duplicateDriver.enabled, Is.False);
                    }
                    player = One<PlayerManager>(); hunter = One<HunterManager>();
                    chase = One<ChaseManager>(); floor = One<FloorManager>(); director = One<DirectorManager>();
                    driver = hunter.GetComponent<HunterDriver>(); motor = Read<HunterDriverState>(driver, "_state");
                    hunterState = (HunterBehaviorState)hunter.ReadOnlyState;
                    config = Read<DirectorConfig>(root, "_directorConfig");
                    hunterProfile = Read<HunterProfile>(root, "_hunterProfile");
                    motorConfig = Read<HunterMotorDriverConfig>(driver, "_config");
                    Assert.That(Read<DirectorManager>(root, "_director"), Is.SameAs(director));
                    Assert.That(Read<FloorManager>(root, "_floor"), Is.SameAs(floor));
                    Assert.That(run.Tick, Is.Zero); Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                    Assert.That(captures, Is.EqualTo(1)); Assert.That(report.startTick, Is.Zero);
                    Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1)); Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(config.HeatThresholdSeconds, Is.EqualTo(20f));
                    Assert.That(config.HintAgeSeconds, Is.EqualTo(3f)); Assert.That(config.HintRadiusMeters, Is.EqualTo(8f));
                    Assert.That(config.ReliefMinimumSeconds, Is.EqualTo(10f));
                    Assert.That(config.HintCadenceSeconds, Is.EqualTo(5f)); Assert.That(config.ExitOpenHintCadenceSeconds, Is.EqualTo(2.5f));
                    Assert.That(config.EvaluationIntervalSeconds, Is.EqualTo(0.5f));
                    Assert.That(config.ProximityRadiusMeters, Is.EqualTo(20f));
                    report.heatThreshold = config.HeatThresholdSeconds; report.hintAge = config.HintAgeSeconds;
                    report.reliefMinimum = config.ReliefMinimumSeconds; report.proximityRadius = config.ProximityRadiusMeters;
                    Remember(config); Remember(hunterProfile); Remember(motorConfig);
                    Remember(Read<PlayerProfile>(root, "_playerProfile"));
                    Remember(Read<ChaseConfig>(root, "_chaseConfig")); Remember(Read<FloorConfig>(root, "_floorConfig"));
                    Remember(Read<PlayerMoverDriverConfig>(player.GetComponent<PlayerDriver>(), "_config"));
                    report.files.Add(Stamp(ScenePath)); report.files.Add(Stamp(FloorLoopLevelSetup.NavigationAssetPath));
                    report.files.Add(Stamp("Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs"));
                    gameplay = Read<InputActionMap>(input.GetComponent<PlayerInputDriver>(), "_actions");
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    devicesSaved = true; gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions) Assert.That(action.controls.Count, Is.Zero);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    input.FramePublished += SupplyInput; run.TickAdvanced += OnTick;
                    director.OnHintIssued += OnHint; director.OnPressureSampled += OnPressure;
                    chase.OnChaseStarted += OnStarted; chase.OnChaseEnded += OnEnded;
                    floor.OnExitOpened += OnExitOpened;
                    previousHunter = hunterState.Position;
                    report.playerSpawn = player.ReadOnlyState.Position; report.hunterSpawn = hunterState.Position;
                    Assert.That(report.playerSpawn, Is.EqualTo(kind == TrialKind.Cadence ? CadencePlayer : TravelPlayer));
                    Assert.That(report.hunterSpawn, Is.EqualTo(kind == TrialKind.Cadence ? CadenceHunter : FloorLoopLevelSetup.HunterSpawnPosition));
                    float distance = Vector3.Distance(report.playerSpawn, report.hunterSpawn);
                    Assert.That(distance, Is.GreaterThanOrEqualTo(config.ProximityRadiusMeters));
                    gapStart = 0;
                    report.poses.Add(Pose(0, distance));
                    Ready = true;
                }
                catch (Exception error) { Fail("Readiness: " + error); }
            }

            private void SupplyInput(InputFrame frame)
            {
                if (Done || report.failure.Length > 0) return;
                producedFrames++;
                if (!frame.Equals(default(InputFrame))) Fail("Isolated producer received hardware/non-neutral input.");
                run.ReceiveInput(default);
            }

            public void ObserveDeferredTeardown()
            {
                if (!Ready || report.serviceSettledFrame != 0 || Time.frameCount <= report.serviceReadyFrame) return;
                try
                {
                    Assert.That(One<InputManager>(), Is.SameAs(input), "All deferred Input duplicates must be destroyed after the readiness frame.");
                    Assert.That(One<PlayerInputDriver>(), Is.SameAs(input.GetComponent<PlayerInputDriver>()));
                    Assert.That(One<RunSessionManager>(), Is.SameAs(run));
                    Assert.That(InputManager.Instance, Is.SameAs(input)); Assert.That(RunSessionManager.Instance, Is.SameAs(run));
                    report.inputObjectsAfterTeardown = 1; report.serviceSettledFrame = Time.frameCount;
                }
                catch (Exception error) { Fail("Deferred service teardown: " + error); }
            }

            private void OnHint(HintPayload hint)
            {
                if (Done) return;
                try
                {
                    Assert.That(hint.Hunter, Is.EqualTo(hunter.Id)); Assert.That(hint.Player, Is.EqualTo(player.Id));
                    Assert.That(hint.DeliveredTick, Is.EqualTo(run.Tick));
                    Assert.That(hint.AgeSeconds, Is.EqualTo(config.HintAgeSeconds).Within(0.0001f));
                    Assert.That(hint.Radius, Is.EqualTo(config.HintRadiusMeters));
                    Assert.That(hunterState.LastKnownTick, Is.EqualTo(hint.ObservedTick), "Actual Hunter must accept the delivered hint.");
                    Assert.That(hunterState.PlayerVisible, Is.False);
                    Assert.That(hunterState.BeliefConfidence, Is.GreaterThan(0f));
                    Assert.That(Vector3.Distance(hunterState.LastKnownPosition, hint.Position), Is.LessThanOrEqualTo(hint.Radius + 0.001f));
                    report.hints.Add(new HintRow {
                        deliveredTick = hint.DeliveredTick, observedTick = hint.ObservedTick, age = hint.AgeSeconds,
                        radius = hint.Radius, position = hint.Position, belief = hunterState.LastKnownPosition,
                        confidence = hunterState.BeliefConfidence, hunterPosition = hunterState.Position,
                        playerPosition = player.ReadOnlyState.Position, exitOpen = floor.ReadOnlyState.ExitState == ExitState.Open
                    });
                }
                catch (Exception error) { Fail("Hint delivery: " + error); }
            }
            private void OnPressure(DirectorPressureSample sample)
            {
                if (Done) return;
                report.pressure.Add(new PressureRow { tick = sample.Tick, heat = sample.HeatSeconds,
                    relief = sample.ReliefSeconds, chasing = sample.IsChasing, near = sample.IsWithinProximity,
                    hinted = sample.HintIssued, exitOpen = floor.ReadOnlyState.ExitState == ExitState.Open });
            }
            private void OnStarted(ChaseFact fact)
            {
                if (Done) return;
                report.chaseFacts.Add(new ChaseRow { kind = "start", tick = fact.Tick, id = fact.ChaseId, reason = fact.EndReason.ToString() });
            }
            private void OnEnded(ChaseFact fact)
            {
                if (Done) return;
                report.chaseFacts.Add(new ChaseRow { kind = "end", tick = fact.Tick, id = fact.ChaseId, reason = fact.EndReason.ToString() });
            }
            private void OnExitOpened(long tick) { if (!Done) report.exitOpenedTick = tick; }

            private void OnTick(InputFrame frame, float dt, long tick)
            {
                if (Done || report.failure.Length > 0) return;
                try
                {
                    Assert.That(tick, Is.EqualTo(report.cutoffTick + 1)); report.cutoffTick = tick;
                    Assert.That(InputManager.Instance, Is.SameAs(input)); Assert.That(RunSessionManager.Instance, Is.SameAs(run));
                    Assert.That(producedFrames, Is.EqualTo(tick)); Assert.That(frame.Equals(default(InputFrame)), Is.True);
                    Assert.That(dt, Is.EqualTo(Dt)); Assert.That(Time.timeScale, Is.EqualTo(1f));
                    Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f), "Combat/closure preempted the declared observation.");
                    Assert.That(Vector3.ProjectOnPlane(player.ReadOnlyState.Position - report.playerSpawn, Vector3.up).magnitude,
                        Is.LessThan(0.05f), "The stationary arrangement moved.");
                    float distance = Vector3.Distance(player.ReadOnlyState.Position, hunterState.Position);
                    report.motorTravel += Vector3.Distance(previousHunter, hunterState.Position); previousHunter = hunterState.Position;
                    report.poses.Add(Pose(tick, distance));
                    RecordGap(tick, distance < config.ProximityRadiusMeters);
                    if (kind == TrialKind.Cadence) AdvanceCadence(tick, distance);
                    else AdvanceTravel(tick, distance);
                    Assert.That(tick, Is.LessThanOrEqualTo(kind == TrialKind.Cadence ? 2700L : 5400L), "Independent finite tick ceiling exceeded.");
                }
                catch (Exception error) { Fail("Tick " + tick + ": " + error); }
            }

            private void AdvanceCadence(long tick, float distance)
            {
                Assert.That(distance, Is.GreaterThan(20f), "Physical containment must preserve a real pressure gap.");
                Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep).Or.EqualTo(RunPhase.ExitOpen).Or.EqualTo(RunPhase.Collapse));
                if (tick == 60)
                {
                    Assert.That(report.chaseFacts.Count(fact => fact.kind == "start"), Is.EqualTo(1));
                    Assert.That(chase.ReadOnlyState.HasActiveChase, Is.True);
                    Assert.That(report.motorTravel, Is.GreaterThan(0.2f), "Hunter must physically advance before the fence holds it.");
                    Assert.That(VisibleProbe(), Is.True, "Initial low fence must leave real target rays clear.");
                    screen.enabled = true; Physics.SyncTransforms(); report.screenClosedTick = tick;
                }
                if (screen.enabled) AssertOcclusion();
                if (report.hints.Count == 2 && report.exitOpenedTick == 0)
                {
                    Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
                    var anchors = floor.ReadOnlyState.ActiveCakeAnchors.ToArray();
                    Assert.That(anchors.Length, Is.EqualTo(floor.ReadOnlyState.RequiredCakeCount));
                    foreach (var anchor in anchors) floor.Collect(player.Id, anchor.Id, PickupKind.Cake);
                    Assert.That(report.exitOpenedTick, Is.EqualTo(tick));
                    Assert.That(floor.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                }
                if (report.hints.Count >= 4) Done = true;
            }

            private void AdvanceTravel(long tick, float distance)
            {
                Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                if (report.hints.Count == 0)
                {
                    Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False, "Early perception invalidated the delayed-hint travel population.");
                    Assert.That(hunterState.BeliefConfidence, Is.Zero);
                    Assert.That(distance, Is.GreaterThanOrEqualTo(config.ProximityRadiusMeters));
                    return;
                }
                var hint = report.hints[0];
                if (tick == hint.deliveredTick + 1)
                {
                    Assert.That(hunterState.CurrentAction, Is.EqualTo(HunterAction.InvestigateHint));
                    Assert.That(Vector3.Distance(motor.LastTarget, hint.belief), Is.LessThan(0.001f));
                    report.firstInvestigationTick = tick;
                }
                if (hunterState.CurrentAction == HunterAction.InvestigateHint) report.investigationTicks++;
                if (driver.PathAvailable && motor.Path != null && motor.Path.status == NavMeshPathStatus.PathComplete)
                    report.completePathTicks++;
                if (report.firstProximityTick == 0 && distance < config.ProximityRadiusMeters)
                {
                    report.firstProximityTick = tick;
                    report.measuredTravelSeconds = (tick - hint.deliveredTick) * (double)Dt;
                    report.travelCrossingLowerSeconds = (tick - 1 - hint.deliveredTick) * (double)Dt;
                    report.initialGapLowerSeconds = (tick - 1) * (double)Dt;
                    report.initialGapSeconds = tick * (double)Dt;
                    report.initialBoundSeconds = config.HeatThresholdSeconds + config.HintAgeSeconds + report.measuredTravelSeconds;
                }
                Assert.That(tick - hint.deliveredTick, Is.LessThanOrEqualTo(3600L),
                    "Actual Hunter did not establish and retain proximity within the independent 60 s response observation.");
                if (report.firstProximityTick > 0 && tick >= report.firstProximityTick + 30) Done = true;
            }

            private PoseRow Pose(long tick, float distance) => new PoseRow { tick = tick, player = player.ReadOnlyState.Position,
                hunter = hunterState.Position, distance = distance, visible = hunterState.PlayerVisible,
                action = hunterState.CurrentAction.ToString(), pathComplete = driver.PathAvailable && motor.Path != null &&
                    motor.Path.status == NavMeshPathStatus.PathComplete, chaseActive = chase.ReadOnlyState.HasActiveChase,
                chaseId = chase.ReadOnlyState.ChaseId, belief = hunterState.LastKnownPosition, beliefTick = hunterState.LastKnownTick };

            private void RecordGap(long tick, bool near)
            {
                if (!near && gapStart < 0) gapStart = tick - 1;
                if (!near || gapStart < 0) return;
                report.gaps.Add(Gap(tick, false)); gapStart = -1;
            }

            private GapRow Gap(long tick, bool censored) => new GapRow {
                startTick = gapStart, endTick = tick, censored = censored, beginsAtObservationStart = gapStart == 0,
                departureEarliestTick = gapStart, departureLatestTick = gapStart == 0 ? 0 : gapStart + 1,
                arrivalEarliestTick = censored ? -1 : tick - 1, arrivalLatestTick = censored ? -1 : tick,
                sampledSpanSeconds = (tick - gapStart) * (double)Dt,
                durationLowerSeconds = Math.Max(0, tick - gapStart - (gapStart == 0 ? 0 : 1) - (censored ? 0 : 1)) * (double)Dt,
                hasFiniteDurationUpperBound = !censored,
                durationUpperSeconds = censored ? -1d : (tick - gapStart) * (double)Dt
            };

            public void AssertOutcome()
            {
                Assert.That(Done, Is.True); Assert.That(captures, Is.EqualTo(1)); Assert.That(readiness, Is.EqualTo(1));
                Assert.That(report.serviceSettledFrame, Is.GreaterThan(report.serviceReadyFrame), "Deferred destruction must be observed, not presumed.");
                Assert.That(report.inputObjectsAfterTeardown, Is.EqualTo(1));
                Assert.That(report.poses.Count, Is.EqualTo(report.cutoffTick + 1), "Initial pose and every completed tick must remain in the denominator.");
                Assert.That(report.pressure.Count, Is.EqualTo(report.cutoffTick / 30));
                for (int i = 0; i < report.pressure.Count; i++) Assert.That(report.pressure[i].tick, Is.EqualTo((i + 1L) * 30));
                Assert.That(report.hints.Count, Is.EqualTo(report.pressure.Count(row => row.hinted)));
                foreach (var row in report.pressure.Where(row => row.chasing))
                { Assert.That(row.relief, Is.Zero); Assert.That(row.heat, Is.Zero); Assert.That(row.hinted, Is.False); }
                foreach (var row in report.pressure.Where(row => row.relief < config.ReliefMinimumSeconds))
                    Assert.That(row.hinted, Is.False, "No hint is allowed throughout the observed sub-minimum relief interval.");
                foreach (var asset in snapshots) Assert.That(JsonUtility.ToJson(asset.Key), Is.EqualTo(asset.Value), asset.Key.name + " tuning changed.");
                foreach (var file in report.files) Assert.That(Stamp(file.path).sha256, Is.EqualTo(file.sha256), file.path + " changed during observation.");
                if (kind == TrialKind.Cadence) AssertCadence();
                else
                {
                    Assert.That(report.firstProximityTick, Is.GreaterThan(report.hints[0].deliveredTick));
                    Assert.That(report.firstInvestigationTick, Is.EqualTo(report.hints[0].deliveredTick + 1));
                    Assert.That(report.investigationTicks, Is.GreaterThanOrEqualTo(10));
                    Assert.That(report.completePathTicks, Is.GreaterThanOrEqualTo(10));
                    Assert.That(report.motorTravel, Is.GreaterThan(10f));
                    Assert.That(report.gaps.Count, Is.GreaterThanOrEqualTo(1));
                    Assert.That(report.gaps[0].startTick, Is.Zero);
                    Assert.That(report.gaps[0].endTick, Is.EqualTo(report.firstProximityTick));
                    Assert.That(report.initialGapSeconds, Is.LessThanOrEqualTo(report.initialBoundSeconds),
                        "Emission-time algebraic consistency failed; this is separate from actual arrival and its finite response deadline.");
                    report.initialBoundConsistencyPassed = true;
                }
                TestContext.WriteLine("DIRECTOR-PACING observed " + Describe() + "; this is not complete-floor or participant acceptance.");
            }

            private void AssertCadence()
            {
                Assert.That(report.chaseFacts.Count, Is.EqualTo(2));
                var start = report.chaseFacts.Single(fact => fact.kind == "start");
                var end = report.chaseFacts.Single(fact => fact.kind == "end");
                Assert.That(start.id, Is.EqualTo(end.id)); Assert.That(end.reason, Is.EqualTo(ChaseEndReason.Lost.ToString()));
                Assert.That(end.tick, Is.GreaterThan(report.screenClosedTick));
                Assert.That(report.pressure.Count(row => row.chasing), Is.GreaterThan(3));
                var relief = report.pressure.Where(row => row.tick >= end.tick &&
                    (row.tick - end.tick + 1) * (double)Dt <= config.ReliefMinimumSeconds).ToArray();
                Assert.That(relief.Length, Is.GreaterThanOrEqualTo(19));
                foreach (var row in relief)
                {
                    Assert.That(row.chasing || row.hinted, Is.False);
                    Assert.That(row.relief, Is.EqualTo((row.tick - end.tick + 1) * (double)Dt).Within(0.0001));
                }
                Assert.That(report.hints.Count, Is.EqualTo(4));
                Assert.That((report.hints[0].deliveredTick - end.tick + 1) * (double)Dt,
                    Is.InRange(20d, 20.5d + Dt));
                Assert.That(report.hints.Select(hint => hint.exitOpen), Is.EqualTo(new[] { false, false, true, true }));
                Assert.That(report.hints[1].deliveredTick - report.hints[0].deliveredTick, Is.EqualTo(300));
                Assert.That(report.hints[2].deliveredTick - report.hints[1].deliveredTick, Is.EqualTo(150));
                Assert.That(report.hints[3].deliveredTick - report.hints[2].deliveredTick, Is.EqualTo(150));
                Assert.That(report.exitOpenedTick, Is.EqualTo(report.hints[1].deliveredTick));
                Assert.That(report.pressure.Where(row => row.tick > report.exitOpenedTick).All(row => row.exitOpen), Is.True);
                Assert.That(report.poses.All(row => row.distance >= 20f), Is.True);
                report.reliefPredicateIsolated = false;
            }

            private bool VisibleProbe()
            {
                SightProbe value = driver.ProbeSight(player.ReadOnlyState.Position,
                    collider => collider.GetComponentInParent<PlayerManager>() == player);
                return value.HeadVisible || value.ChestVisible || value.HipsVisible;
            }
            private void AssertOcclusion()
            {
                Assert.That(VisibleProbe(), Is.False);
                Vector3 origin = driver.Position + Vector3.up * motorConfig.EyeHeight;
                Vector3 heights = motorConfig.TargetSampleHeights;
                foreach (float height in new[] { heights.x, heights.y, heights.z })
                {
                    Vector3 delta = player.ReadOnlyState.Position + Vector3.up * height - origin;
                    Assert.That(screen.Raycast(new Ray(origin, delta.normalized), out _, delta.magnitude), Is.True,
                        "The declared screen must physically intersect all target rays.");
                }
            }
            private BoxCollider Box(string name, Vector3 center, Vector3 size)
            {
                var item = new GameObject(name); item.transform.SetParent(geometry.transform, false); item.transform.position = center;
                var collider = item.AddComponent<BoxCollider>(); collider.size = size; return collider;
            }
            private void Remember(Object asset)
            {
                Assert.That(asset, Is.Not.Null); snapshots.Add(asset, JsonUtility.ToJson(asset));
                string path = AssetDatabase.GetAssetPath(asset); Assert.That(path, Is.Not.Empty); report.files.Add(Stamp(path));
            }
            private void Fail(string message) { if (report.failure.Length == 0) report.failure = message; }
            public string Describe() => "kind=" + kind + "; ticks=" + report.cutoffTick + "; hints=" + report.hints.Count +
                "; chase facts=" + report.chaseFacts.Count + "; proximity tick=" + report.firstProximityTick +
                "; motor travel=" + report.motorTravel + "; session=" + report.sessionId;

            public void Dispose()
            {
                if (disposed) return; disposed = true;
                FloorLoopSceneRoot.SceneReady -= OnReady;
                if (input != null) input.FramePublished -= SupplyInput;
                if (director != null) { director.OnHintIssued -= OnHint; director.OnPressureSampled -= OnPressure; }
                if (chase != null) { chase.OnChaseStarted -= OnStarted; chase.OnChaseEnded -= OnEnded; }
                if (floor != null) floor.OnExitOpened -= OnExitOpened;
                if (run != null)
                {
                    run.CaptureStarted -= OnCapture; run.TickAdvanced -= OnTick;
                    // Public scene-load suspension records an interrupted capture and prevents
                    // unobserved post-cutoff ticks while the next trial scene is loading.
                    run.SuspendForSceneLoad();
                }
                if (devicesSaved && gameplay != null) gameplay.devices = previousDevices;
                if (geometry != null) Object.Destroy(geometry);
                if (gapStart >= 0) report.gaps.Add(Gap(report.cutoffTick, true));
            }
            public void WriteReport()
            {
                string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Logs", "AgentValidation", "DirectorPacing");
                Directory.CreateDirectory(directory);
                report.reportPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") +
                    "-" + kind + "-seed-" + report.seed + "-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(report.reportPath, JsonUtility.ToJson(report, true));
                TestContext.WriteLine("DIRECTOR-PACING REPORT " + report.reportPath);
            }
        }

        [Serializable] private sealed class TrialReport
        {
            public string utc, kind, scope, measurementSemantics, sessionId, sourceRevision, captureConfigHash, reportPath;
            public string failure = "";
            public int seed, investigationTicks, completePathTicks;
            public int canonicalInputInstanceId, canonicalRunInstanceId, serviceReadyFrame, serviceSettledFrame,
                inputObjectsAtReady, inputObjectsAfterTeardown;
            public long startTick, cutoffTick, screenClosedTick, exitOpenedTick, firstInvestigationTick, firstProximityTick;
            public bool passed, captureCompleteExpected, reliefPredicateIsolated, initialBoundConsistencyPassed;
            public float fixedDeltaTime, heatThreshold, hintAge, reliefMinimum, proximityRadius, motorTravel;
            public Vector3 playerSpawn, hunterSpawn;
            public double measuredTravelSeconds, travelCrossingLowerSeconds, initialGapSeconds, initialGapLowerSeconds, initialBoundSeconds;
            public List<FileRow> files = new List<FileRow>();
            public List<PoseRow> poses = new List<PoseRow>();
            public List<PressureRow> pressure = new List<PressureRow>();
            public List<HintRow> hints = new List<HintRow>();
            public List<ChaseRow> chaseFacts = new List<ChaseRow>();
            public List<GapRow> gaps = new List<GapRow>();
        }
        [Serializable] private sealed class PoseRow
        { public long tick, beliefTick; public Vector3 player, hunter, belief; public float distance;
            public bool visible, pathComplete, chaseActive; public int chaseId; public string action; }
        [Serializable] private sealed class PressureRow
        { public long tick; public float heat, relief; public bool chasing, near, hinted, exitOpen; }
        [Serializable] private sealed class HintRow
        { public long deliveredTick, observedTick; public float age, radius, confidence;
            public Vector3 position, belief, hunterPosition, playerPosition; public bool exitOpen; }
        [Serializable] private sealed class ChaseRow { public string kind, reason; public long tick; public int id; }
        [Serializable] private sealed class GapRow
        { public long startTick, endTick, departureEarliestTick, departureLatestTick, arrivalEarliestTick, arrivalLatestTick;
            public bool censored, beginsAtObservationStart, hasFiniteDurationUpperBound;
            public double sampledSpanSeconds, durationLowerSeconds, durationUpperSeconds; }
        [Serializable] private sealed class FileRow { public string path, sha256; }

        private static FileRow Stamp(string path)
        {
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            using (var stream = File.OpenRead(absolute))
            using (var sha = SHA256.Create())
                return new FileRow { path = path, sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") };
        }
        private static FieldInfo SpawnField(string name) => typeof(FloorLoopSceneRoot).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing initial spawn wiring: " + name);
        private static T Read<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "Missing object while inspecting " + name);
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing field " + target.GetType().Name + "." + name); return (T)field.GetValue(target);
        }
        private static T One<T>() where T : Component
        {
            T[] values = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have exactly one active instance."); return values[0];
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
