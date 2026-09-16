// ============================================================================
// DirectorHintIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Observes a real FloorLoop delayed hint from Session history through Director
//   registry delivery, Hunter belief, planning and physical path movement.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Director integration.
// KEY RESPONSIBILITIES:
//   - Cover Horror progression and Shift-to-run while preserving fixture motion intent.
//   - Distinguish a historical Player pose from the current pose at delivery.
//   - Verify default hint age, uncertainty, aged belief and actual investigation.
//   - Bound timing by consecutive real Session ticks and detect early/duplicate hints.
// DEPENDENCIES:
//   - Core facts; Director, Player, Hunter, Chase; Session.Run; FloorLoopSceneRoot.
//   - Presentation.Input, Input System, Unity Test Framework, NavMesh path inspection
//     and runtime spawn reflection.
// USAGE NOTES:
//   Coordinator runs under the Unity lease. Before scene factories initialize,
//   only the runtime Player spawn is moved to the upper-left room. Hunter spawn,
//   seed, factories, physics and all profiles/configs remain the authored defaults.
//   Normal Run input walks north briefly after 18.5 seconds to distinguish old
//   history from current position. No gameplay Tick, hint receiver or private
//   gameplay-state mutator is called. The owned gameplay map temporarily binds no
//   hardware controls; its copied prior device filter is restored in finally. Live
//   source selection clears buffered hardware input while retaining real gates.
//   Synthetic input follows the normal producer. Background execution is restored in finally;
//   UnityTearDown exits Play Mode. This arranged ~23-second smoke is not a default-
//   spawn benchmark, complete capture, relief transition, exit-cadence, full-floor
//   pressure-gap, arrival guarantee or human no-wandering acceptance measurement.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Editor.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;

namespace Worsen.Tests.Director
{
    public sealed class DirectorHintIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/FloorLoop.unity";
        private const long WalkStartTick = 1111, WalkEndTick = 1170, ResponseTicks = 120, MaximumTicks = 1500;
        private static readonly Vector3 ArrangedSpawn = new Vector3(-38f, 0.1f, 22f);

        [UnityTest]
        public IEnumerator FloorHistoryHintProducesAgedBeliefAndPhysicalInvestigation()
        {
            yield return new EnterPlayMode();
            // Captured state must be allocated after EnterPlayMode's domain reload.
            bool previousBackground = Application.runInBackground;
            try
            {
                Application.runInBackground = true;
                yield return ObserveFloor();
            }
            finally { Application.runInBackground = previousBackground; }
        }

        private static IEnumerator ObserveFloor()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<FloorLoopSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                "Start from the Test Framework isolated bootstrap scene.");
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));
            var trial = new Trial();
            SceneManager.sceneLoaded += trial.OnLoaded;
            try
            {
                TestContext.Progress.WriteLine("Director delayed-hint smoke: runtime initial Player spawn override " +
                    ArrangedSpawn + "; default Hunter spawn/seed/profiles; normal walk input at ticks " + WalkStartTick +
                    ".." + WalkEndTick + ". This is an arranged integration run, not default-spawn or full-floor acceptance.");
                AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build FloorLoop before running this fixture.");
                float watchdog = Time.realtimeSinceStartup + 60f;
                for (int frame = 0; frame < 36000 && !trial.Finished && trial.Failure.Length == 0 &&
                    Time.realtimeSinceStartup < watchdog; frame++) yield return null;
                Assert.That(trial.Failure, Is.Empty, trial.Describe());
                Assert.That(load.isDone && trial.Ready, Is.True, "FloorLoop did not assemble: " + trial.Describe());
                Assert.That(trial.Finished, Is.True, "Session stalled: " + trial.Describe());
                trial.AssertOutcome();
                TestContext.Progress.WriteLine("Director delayed-hint smoke passed: " + trial.Describe() +
                    "; relief transitions, exit cadence, complete-floor pressure gaps and human acceptance remain separate.");
            }
            finally
            {
                SceneManager.sceneLoaded -= trial.OnLoaded;
                trial.Dispose();
            }
        }

        private sealed class Trial : IDisposable
        {
            private RunSessionManager run;
            private InputManager input;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private bool deviceFilterCaptured;
            private DirectorManager director;
            private PlayerManager player;
            private HunterManager hunter;
            private HunterDriver driver;
            private HunterDriverState motorState;
            private HunterBehaviorState hunterState;
            private ChaseManager chase;
            private DirectorConfig config;
            private HunterProfile hunterProfile;
            private readonly Dictionary<UnityEngine.Object, string> configSnapshots = new Dictionary<UnityEngine.Object, string>();
            private readonly Dictionary<long, Vector3> history = new Dictionary<long, Vector3>();
            private readonly List<DirectorPressureSample> pressure = new List<DirectorPressureSample>();
            private readonly List<HintPayload> hints = new List<HintPayload>();
            private int readyCount, investigateTicks, completePathTicks, hintPressureCount;
            private long firstInvestigateTick;
            private Vector3 historicalPose, currentAtHint, beliefAtHint, hunterAtHint, previousHunterPose;
            private float confidenceAtHint, investigateTravel, investigateDisplacement;
            private double stepSeconds;
            public bool Ready;
            public long Ticks;
            public string Failure = "";
            public bool Finished => Ready && (Ticks >= MaximumTicks ||
                (hints.Count > 0 && Ticks >= hints[0].DeliveredTick + ResponseTicks));

            public void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ScenePath) return;
                try
                {
                    var root = One<FloorLoopSceneRoot>();
                    Assert.That(Read(root, "_spawnPosition"), Is.EqualTo(FloorLoopLevelSetup.SpawnPosition));
                    Assert.That(Read(root, "_hunterSpawnPosition"), Is.EqualTo(FloorLoopLevelSetup.HunterSpawnPosition));
                    Assert.That(Read(root, "_seed"), Is.EqualTo(1));
                    // sceneLoaded precedes Start: configure the factory request, never an existing actor's facts.
                    FieldInfo spawn = typeof(FloorLoopSceneRoot).GetField("_spawnPosition",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(spawn, Is.Not.Null);
                    spawn.SetValue(root, ArrangedSpawn);
                    FloorLoopSceneRoot.SceneReady += OnReady;
                }
                catch (Exception error) { Fail("Runtime spawn arrangement failed: " + error); }
            }

            private void OnReady(SceneKey scene)
            {
                if (scene != SceneKey.FloorLoop) return;
                try
                {
                    readyCount++;
                    var root = One<FloorLoopSceneRoot>();
                    run = One<RunSessionManager>(); input = One<InputManager>(); director = One<DirectorManager>();
                    player = One<PlayerManager>(); hunter = One<HunterManager>(); chase = One<ChaseManager>();
                    driver = hunter.GetComponent<HunterDriver>();
                    motorState = (HunterDriverState)Read(driver, "_state");
                    hunterState = (HunterBehaviorState)hunter.ReadOnlyState;
                    config = (DirectorConfig)Read(root, "_directorConfig");
                    hunterProfile = (HunterProfile)Read(root, "_hunterProfile");
                    Assert.That(Read(root, "_director"), Is.SameAs(director));
                    Assert.That(Read(hunter, "_profile"), Is.SameAs(hunterProfile));
                    Assert.That(run.Tick, Is.Zero);
                    Assert.That(run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                    Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(ArrangedSpawn));
                    Assert.That(hunterState.Position, Is.EqualTo(FloorLoopLevelSetup.HunterSpawnPosition));
                    Assert.That(config.EvaluationIntervalSeconds, Is.EqualTo(0.5f));
                    Assert.That(config.HeatThresholdSeconds, Is.EqualTo(20f));
                    Assert.That(config.HintAgeSeconds, Is.EqualTo(3f));
                    Assert.That(config.HintRadiusMeters, Is.EqualTo(8f));
                    Assert.That(config.HintConfidence, Is.EqualTo(0.5f));
                    Assert.That(config.ReliefMinimumSeconds, Is.EqualTo(10f));
                    Assert.That(config.HintCadenceSeconds, Is.EqualTo(5f));
                    Assert.That(hunterProfile.MemoryDecaySeconds, Is.EqualTo(8f));
                    Remember(config); Remember(hunterProfile);
                    Remember((UnityEngine.Object)Read(root, "_playerProfile"));
                    Remember((UnityEngine.Object)Read(driver, "_config"));
                    gameplay = (InputActionMap)Read(input.GetComponent<PlayerInputDriver>(), "_actions");
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray())
                        : (ReadOnlyArray<InputDevice>?)null;
                    deviceFilterCaptured = true;
                    gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions)
                        Assert.That(action.controls.Count, Is.Zero, "Synthetic trial must bind no hardware controls.");
                    // Clear buffered hardware/cancellation edges before tick one without disabling real gates.
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    stepSeconds = Time.fixedDeltaTime;
                    director.OnHintIssued += OnHint; director.OnPressureSampled += OnPressure;
                    input.FramePublished += SupplyInput; run.TickAdvanced += OnTick;
                    Ready = true;
                }
                catch (Exception error) { Fail("Readiness inspection failed: " + error); }
            }

            private static InputFrame InputAt(long tick)
            {
                // Unmodified movement walks; east-facing negative strafe moves north.
                return tick >= WalkStartTick && tick <= WalkEndTick
                    ? new InputFrame(Vector2.left, Vector2.zero, InputButtons.None, InputButtons.None, InputButtons.None)
                    : default;
            }
            private void SupplyInput(InputFrame produced)
            {
                if (!produced.Equals(default(InputFrame)))
                    Fail("Isolated gameplay producer emitted non-neutral input: move=" + produced.Move +
                        "; look=" + produced.LookDelta + "; held=" + produced.Held +
                        "; pressed=" + produced.Pressed + "; released=" + produced.Released);
                run.ReceiveInput(InputAt(run.Tick + 1));
            }
            private void OnPressure(DirectorPressureSample sample)
            {
                pressure.Add(sample);
                if (sample.HintIssued) hintPressureCount++;
            }

            private void OnHint(HintPayload hint)
            {
                hints.Add(hint);
                if (hints.Count != 1) { Fail("Director duplicated its hint during the bounded response window."); return; }
                try
                {
                    Assert.That(hint.Hunter, Is.EqualTo(hunter.Id));
                    Assert.That(hint.Player, Is.EqualTo(player.Id));
                    Assert.That(hint.DeliveredTick, Is.EqualTo(run.Tick));
                    Assert.That(hint.ObservedTick, Is.LessThan(hint.DeliveredTick));
                    Assert.That(hint.AgeSeconds, Is.EqualTo(config.HintAgeSeconds).Within(0.0001f));
                    Assert.That(hint.Radius, Is.EqualTo(config.HintRadiusMeters));
                    Assert.That(hint.Confidence, Is.EqualTo(config.HintConfidence));
                    double deliveredSeconds = hint.DeliveredTick * stepSeconds;
                    Assert.That(deliveredSeconds, Is.InRange(config.HeatThresholdSeconds - 0.00001,
                        config.HeatThresholdSeconds + config.EvaluationIntervalSeconds + stepSeconds));
                    Assert.That(history.ContainsKey(hint.ObservedTick) && history.ContainsKey(hint.ObservedTick + 1), Is.True);
                    double sampledSeconds = deliveredSeconds - hint.AgeSeconds;
                    Assert.That(sampledSeconds, Is.InRange(hint.ObservedTick * stepSeconds - 0.00001,
                        (hint.ObservedTick + 1) * stepSeconds + 0.00001));
                    float fraction = Mathf.Clamp01((float)((sampledSeconds - hint.ObservedTick * stepSeconds) / stepSeconds));
                    historicalPose = Vector3.Lerp(history[hint.ObservedTick], history[hint.ObservedTick + 1], fraction);
                    currentAtHint = player.ReadOnlyState.Position;
                    Assert.That(Vector3.Distance(hint.Position, historicalPose), Is.LessThan(0.002f),
                        "The Director must use actual previously committed Player history.");
                    Assert.That(Vector3.Distance(hint.Position, currentAtHint), Is.GreaterThan(2f),
                        "Late physical walking must distinguish stale history from the current Player pose.");
                    Assert.That(hunterState.PlayerVisible || chase.ReadOnlyState.HasActiveChase, Is.False);
                    // Director publishes only after its normal registry delivery; inspect the resulting belief here.
                    beliefAtHint = hunterState.LastKnownPosition; confidenceAtHint = hunterState.BeliefConfidence;
                    hunterAtHint = hunterState.Position; previousHunterPose = hunterAtHint;
                    Assert.That(hunterState.LastKnownTick, Is.EqualTo(hint.ObservedTick));
                    Assert.That(Vector3.Distance(beliefAtHint, hint.Position), Is.InRange(0.001f, hint.Radius + 0.001f));
                    Assert.That(beliefAtHint.y, Is.EqualTo(hint.Position.y).Within(0.0001f));
                    float expectedConfidence = hint.Confidence * (1f - hint.AgeSeconds / hunterProfile.MemoryDecaySeconds);
                    Assert.That(confidenceAtHint, Is.EqualTo(expectedConfidence).Within(0.0001f));
                }
                catch (Exception error) { Fail("Real hint delivery inspection failed: " + error); }
            }

            private void OnTick(InputFrame input, float dt, long tick)
            {
                try
                {
                    Assert.That(tick, Is.EqualTo(Ticks + 1));
                    Assert.That(dt, Is.EqualTo(1f / 60f).Within(0.000001f));
                    Ticks = tick;
                    Assert.That(input.Equals(InputAt(tick)), Is.True, "External input changed the arranged route.");
                    history.Add(tick, player.ReadOnlyState.Position);
                    if (hints.Count == 0)
                    {
                        Assert.That(hunterState.BeliefConfidence, Is.Zero, "Sight/hearing preempted the delayed-hint trial.");
                        Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                        Assert.That(Vector3.Distance(hunterState.Position, player.ReadOnlyState.Position),
                            Is.GreaterThanOrEqualTo(config.ProximityRadiusMeters), "Real proximity reset the heat window.");
                        return;
                    }
                    HintPayload hint = hints[0];
                    if (tick <= hint.DeliveredTick) return;
                    bool investigates = hunterState.CurrentAction == HunterAction.InvestigateHint &&
                        hunterState.LastKnownTick == hint.ObservedTick && !hunterState.PlayerVisible &&
                        hunterState.BeliefConfidence > 0f && Vector3.Distance(hunterState.LastKnownPosition, beliefAtHint) < 0.001f;
                    if (tick == hint.DeliveredTick + 1)
                        Assert.That(investigates, Is.True, "The next real Hunter tick did not investigate the delivered belief.");
                    if (investigates)
                    {
                        if (firstInvestigateTick == 0) firstInvestigateTick = tick;
                        investigateTicks++;
                        Assert.That(Vector3.Distance(motorState.LastTarget, beliefAtHint), Is.LessThan(0.001f));
                        if (driver.PathAvailable && motorState.Path.status == NavMeshPathStatus.PathComplete &&
                            motorState.Steering.Corners.Length >= 2)
                        {
                            completePathTicks++;
                            investigateTravel += Vector3.ProjectOnPlane(hunterState.Position - previousHunterPose, Vector3.up).magnitude;
                            investigateDisplacement = Mathf.Max(investigateDisplacement,
                                Vector3.ProjectOnPlane(hunterState.Position - hunterAtHint, Vector3.up).magnitude);
                        }
                    }
                    previousHunterPose = hunterState.Position;
                }
                catch (Exception error) { Fail("Session observation failed: " + error); }
            }

            public void AssertOutcome()
            {
                Assert.That(readyCount, Is.EqualTo(1));
                Assert.That(hints.Count, Is.EqualTo(1), "The default threshold must produce one actual hint.");
                Assert.That(Ticks, Is.GreaterThanOrEqualTo(hints[0].DeliveredTick + ResponseTicks));
                Assert.That(hintPressureCount, Is.EqualTo(1));
                Assert.That(firstInvestigateTick, Is.EqualTo(hints[0].DeliveredTick + 1));
                Assert.That(investigateTicks, Is.GreaterThanOrEqualTo(10));
                Assert.That(completePathTicks, Is.GreaterThanOrEqualTo(10));
                Assert.That(investigateTravel, Is.GreaterThan(1f));
                Assert.That(investigateDisplacement, Is.GreaterThan(1f), "Actual Hunter pose must leave the delivery point.");
                int evaluationTicks = Mathf.RoundToInt(config.EvaluationIntervalSeconds / Time.fixedDeltaTime);
                Assert.That(pressure.Count, Is.EqualTo((int)(Ticks / evaluationTicks)));
                for (int i = 0; i < pressure.Count; i++)
                {
                    DirectorPressureSample sample = pressure[i];
                    Assert.That(sample.Tick, Is.EqualTo((i + 1L) * evaluationTicks));
                    Assert.That(sample.Player, Is.EqualTo(player.Id));
                    if (sample.Tick <= hints[0].DeliveredTick)
                    {
                        Assert.That(sample.IsChasing || sample.IsWithinProximity, Is.False);
                        Assert.That(sample.HintIssued, Is.EqualTo(sample.Tick == hints[0].DeliveredTick));
                    }
                    if (sample.HintIssued)
                    {
                        Assert.That(sample.HeatSeconds, Is.GreaterThanOrEqualTo(config.HeatThresholdSeconds));
                        Assert.That(sample.ReliefSeconds, Is.GreaterThanOrEqualTo(config.ReliefMinimumSeconds));
                    }
                }
                foreach (var snapshot in configSnapshots)
                    Assert.That(JsonUtility.ToJson(snapshot.Key), Is.EqualTo(snapshot.Value), snapshot.Key.name + " was modified.");
            }

            private void Remember(UnityEngine.Object configObject)
            { Assert.That(configObject, Is.Not.Null); configSnapshots.Add(configObject, JsonUtility.ToJson(configObject)); }
            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }
            public string Describe() => "ticks=" + Ticks + "; hints=" + hints.Count + "; observed/delivered=" +
                (hints.Count == 0 ? "none" : hints[0].ObservedTick + "/" + hints[0].DeliveredTick) +
                "; historical/current=" + historicalPose + "/" + currentAtHint + "; noisy belief=" + beliefAtHint +
                "; aged confidence=" + confidenceAtHint + "; investigate/path ticks=" + investigateTicks + "/" + completePathTicks +
                "; investigate travel/displacement=" + investigateTravel + "/" + investigateDisplacement +
                "; hunter=" + (hunterState == null ? "unavailable" : hunterState.Position.ToString());
            public void Dispose()
            {
                FloorLoopSceneRoot.SceneReady -= OnReady;
                try
                {
                    if (director != null) { director.OnHintIssued -= OnHint; director.OnPressureSampled -= OnPressure; }
                    if (input != null) input.FramePublished -= SupplyInput;
                    if (run != null) run.TickAdvanced -= OnTick;
                }
                finally
                {
                    if (deviceFilterCaptured && gameplay != null) gameplay.devices = previousDevices;
                }
            }
        }

        private static object Read(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "Missing object while reading " + name);
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing inspected field " + target.GetType().Name + "." + name);
            return field.GetValue(target);
        }
        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return values[0];
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
