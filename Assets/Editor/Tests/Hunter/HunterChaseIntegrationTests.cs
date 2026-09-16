// ============================================================================
// HunterChaseIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the built TagArena through real Hunter sight, navigation and capsule
//   contacts. It follows those observations through Chase and Session until two
//   default lunges damage the stationary Player and finish the run.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter integration.
// KEY RESPONSIBILITIES:
//   - Observe the real fixed-tick sight, confirmed chase, contact and damage chain.
//   - Verify catch identities, terminal capture ordering and the stopped run tick.
//   - Observe actual focus and Input gate transitions without suppressing interruption.
//   - Scope gameplay to synthetic neutral input while preserving live capture gates.
//   - Persist original capture metadata and observed file paths in NUnit result output.
// DEPENDENCIES:
//   - Core facts; Domain Player/Hunter/Chase; Session.Run; Presentation.Input/Telemetry.
//   - TagArenaSceneRoot, Unity physics, NUnit and the Unity Test Framework.
// USAGE NOTES:
//   Coordinator holds the Unity lease. The fixture loads the already-built arena
//   and changes only runtime initial spawn fields before its factories run. All
//   profile/config assets remain unchanged; no scenes or assets are saved.
//   Application.runInBackground is restored in finally; UnityTearDown exits Play
//   Mode. Locals are created after EnterPlayMode's domain reload. This arranged
//   stationary-target smoke is not the default-spawn or 30-chase tuning benchmark.
//   Requires actual stable GameView/application/editor focus before scene load;
//   a later real focus interruption still makes the capture assertion fail.
//   The gameplay map temporarily binds no hardware; its prior device filter is
//   restored in finally. Neutral input is observed after the actual input producer.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Tests.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Hunter
{
    public sealed class HunterChaseIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";
        private static readonly Vector3 PlayerSpawn = new Vector3(-20f, 0f, 0f);
        private static readonly Vector3 HunterSpawn = new Vector3(-13f, 0f, 0f);

        [UnityTest]
        public IEnumerator RealSightAndLungeContactsProduceConfirmedCatchesAndEndTheRun()
        {
            yield return new EnterPlayMode();
            bool previousBackground = Application.runInBackground;
            try
            {
                Application.runInBackground = true;
                yield return RunArenaAssertions();
            }
            finally { Application.runInBackground = previousBackground; }
        }

        private static IEnumerator RunArenaAssertions()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty,
                "Start from the Test Framework isolated bootstrap scene.");
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f).Within(0.000001f));
            var trial = new Trial();
            var gateTrace = new CaptureGateTrace("HunterChase");
            SceneManager.sceneLoaded += trial.ArrangeInitialSpawns;
            try
            {
                yield return gateTrace.AdmitStableGameViewFocus();
                TestContext.WriteLine("Arranged Hunter physics smoke: runtime Player spawn=(-20,0,0), " +
                    "Hunter spawn=(-13,0,0), stationary input, original TagArena profiles. " +
                    "Capture is not a default-spawn chase benchmark or 30-chase acceptance dataset.");
                AsyncOperation load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, "Build TagArena before running this fixture.");
                yield return Until(() => trial.Ready || trial.Failure.Length > 0, 15f);
                Assert.That(trial.Failure, Is.Empty);
                Assert.That(load.isDone && trial.Ready, Is.True, "Arena did not assemble: " + trial.Describe());
                gateTrace.Mark("Hunter trial readiness observed");
                yield return Until(() => trial.Summaries.Count > 0 || trial.Failure.Length > 0, 20f);
                Assert.That(trial.Failure, Is.Empty, trial.Describe());
                trial.AssertOutcome();

                long stoppedTick = trial.Run.Tick;
                int stoppedSamples = trial.CompletedTicks;
                int stoppedFrame = Time.frameCount;
                double stoppedFixedTime = Time.fixedTimeAsDouble;
                yield return Until(() => Time.fixedTimeAsDouble >= stoppedFixedTime + 0.1 &&
                    Time.frameCount > stoppedFrame, 5f);
                Assert.That(Time.fixedTimeAsDouble, Is.GreaterThanOrEqualTo(stoppedFixedTime + 0.1),
                    "Unity itself must keep stepping while the ended Session remains stopped.");
                Assert.That(Time.frameCount, Is.GreaterThan(stoppedFrame));
                Assert.That(trial.Run.Tick, Is.EqualTo(stoppedTick));
                Assert.That(trial.CompletedTicks, Is.EqualTo(stoppedSamples));
                Assert.That(trial.Summaries.Count, Is.EqualTo(1));
                Assert.That(trial.CaptureCompletions, Is.EqualTo(new[] { true }));
                Assert.That(trial.Input.LastRecordingError, Is.Empty, gateTrace.Describe());
                TestContext.WriteLine("Arranged Hunter smoke passed: " + trial.Describe() +
                    "; capture=" + trial.Input.LastRecordingPath +
                    "; physical loss, route skill, tuning statistics and human acceptance remain separate.");
            }
            finally
            {
                trial.WriteCaptureIdentity();
                SceneManager.sceneLoaded -= trial.ArrangeInitialSpawns;
                trial.Dispose();
                gateTrace.Dispose();
            }
        }

        private sealed class Trial : IDisposable
        {
            public RunSessionManager Run;
            public InputManager Input;
            private RunSessionManager captureRun;
            private TelemetryManager captureTelemetry;
            private RunCaptureMetadata originalCapture;
            private int captureStarts;
            private string originalCsvPath = "";
            private PlayerManager player;
            private HunterManager hunter;
            private HunterDriver driver;
            private ChaseManager chase;
            private HunterProfile profile;
            private ChaseConfig chaseConfig;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private string profileBefore, chaseBefore;
            private long previousTick;
            private int readyCount, deaths, neutralFrames;
            private double initialFixedTime;
            private bool pathObserved;
            public bool Ready;
            public string Failure = "";
            public int CompletedTicks;
            public readonly List<RunSummary> Summaries = new List<RunSummary>();
            public readonly List<bool> CaptureCompletions = new List<bool>();
            private readonly List<HunterSighting> sightings = new List<HunterSighting>();
            private readonly List<HunterHit> hits = new List<HunterHit>();
            private readonly List<long> contactTicks = new List<long>();
            private readonly List<ChaseFact> starts = new List<ChaseFact>();
            private readonly List<ChaseFact> catches = new List<ChaseFact>();
            private readonly List<float> health = new List<float>();
            private readonly List<string> terminalOrder = new List<string>();

            public void ArrangeInitialSpawns(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ArenaPath) return;
                try
                {
                    TagArenaSceneRoot root = One<TagArenaSceneRoot>();
                    // sceneLoaded precedes Start: the real factories initialize both
                    // physics and read-only state from these runtime-only initial poses.
                    Field("_spawnPosition").SetValue(root, PlayerSpawn);
                    Field("_hunterSpawnPosition").SetValue(root, HunterSpawn);
                    profile = (HunterProfile)Field("_hunterProfile").GetValue(root);
                    chaseConfig = (ChaseConfig)Field("_chaseConfig").GetValue(root);
                    Assert.That(profile, Is.Not.Null);
                    Assert.That(chaseConfig, Is.Not.Null);
                    profileBefore = JsonUtility.ToJson(profile);
                    chaseBefore = JsonUtility.ToJson(chaseConfig);
                    // CaptureStarted is emitted by the scene routers before ObserveReady.
                    // Subscribe during sceneLoaded, before SceneRoot.Start opens capture.
                    captureRun = RunSessionManager.Instance != null ? RunSessionManager.Instance : (RunSessionManager)Field("_run").GetValue(root);
                    if (captureRun != null) captureRun.CaptureStarted += OnCaptureStarted;
                    // Scene routers have subscribed in OnEnable by sceneLoaded.
                    // Observe readiness after them, before the first simulation tick.
                    TagArenaSceneRoot.SceneReady += ObserveReady;
                }
                catch (Exception error) { Fail("Spawn arrangement failed: " + error); }
            }

            private void ObserveReady(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                try
                {
                    readyCount++;
                    Run = One<RunSessionManager>(); Input = One<InputManager>();
                    player = One<PlayerManager>(); hunter = One<HunterManager>();
                    driver = hunter.GetComponent<HunterDriver>(); chase = One<ChaseManager>();
                    Assert.That(Run.Tick, Is.Zero, "Observers must attach before the first real tick.");
                    Assert.That(Run.Phase, Is.EqualTo(RunPhase.FirstSweep));
                    Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(HunterRegistry.Items.Count, Is.EqualTo(1));
                    Assert.That(hunter.ReadOnlyState.TargetId, Is.EqualTo(player.Id));
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(PlayerSpawn));
                    Assert.That(hunter.ReadOnlyState.Position, Is.EqualTo(HunterSpawn));
                    Assert.That(player.transform.position, Is.EqualTo(player.ReadOnlyState.Position));
                    Assert.That(hunter.transform.position, Is.EqualTo(hunter.ReadOnlyState.Position));
                    Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f));
                    Assert.That(profile.LungeDamage, Is.EqualTo(50));
                    Assert.That(profile.LungeWindupSeconds, Is.EqualTo(0.25f));
                    Assert.That(profile.LungeActiveSeconds, Is.EqualTo(0.3f));
                    Assert.That(profile.LungeRecoverySeconds, Is.EqualTo(0.8f));
                    Assert.That(profile.LungeDistance, Is.EqualTo(4f));
                    Assert.That(profile.LungeSpeed, Is.EqualTo(18f));
                    Assert.That(chaseConfig.ConfirmationSeconds, Is.EqualTo(0.3f));
                    gameplay = (InputActionMap)typeof(PlayerInputDriver)
                        .GetField("_actions", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(Input.GetComponent<PlayerInputDriver>());
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray())
                        : (ReadOnlyArray<InputDevice>?)null;
                    gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions)
                        Assert.That(action.controls.Count, Is.Zero, "Synthetic trial must bind no hardware controls.");
                    // Clear buffered hardware/cancelled edges before tick one. This
                    // retains real owner/focus/input gates and the active recording.
                    Assert.That(Input.SetSource(InputSource.Live), Is.True);
                    hunter.OnSighting += OnSighting; hunter.OnLungeHit += OnHit;
                    driver.OnLungeContact += OnContact;
                    Input.FramePublished += NeutralInput; Run.TickAdvanced += OnTick;
                    Run.ChaseStarted += OnStarted; Run.ChaseEnded += OnCaught;
                    Run.HealthChanged += OnHealth; Run.PlayerDied += OnDeath;
                    Run.CaptureEnded += OnCapture; Run.RunEnded += OnEnded;
                    initialFixedTime = Time.fixedTimeAsDouble;
                    Ready = true;
                }
                catch (Exception error) { Fail("Readiness observation failed: " + error); }
            }

            private void OnCaptureStarted(RunCaptureMetadata metadata)
            {
                captureStarts++;
                if (captureStarts != 1) return;
                originalCapture = metadata;
                captureTelemetry = TelemetryManager.Instance;
                originalCsvPath = captureTelemetry != null ? captureTelemetry.LastOutputPath : "";
            }

            public void WriteCaptureIdentity()
            {
                string metadata = captureStarts == 0 ? "original CaptureStarted metadata=<unobserved>" :
                    "original SessionId=" + originalCapture.SessionId + "; seed=" + originalCapture.Seed +
                    "; startTick=" + originalCapture.StartTick + "; fixedDeltaTime=" + originalCapture.FixedDeltaTime +
                    "; sourceRevision=" + originalCapture.SourceRevision + "; configHash=" + originalCapture.ConfigSnapshotHash;
                TestContext.WriteLine("Arranged Hunter capture identity: " + metadata + "; CaptureStartedCount=" + captureStarts +
                    "; path association=" + (captureStarts == 1 ? "single observed capture" : "not established: capture count is not one") +
                    "; input LastRecordingPath=" + (Input != null ? Input.LastRecordingPath : "<unavailable>") +
                    "; CSV path at original CaptureStarted=" + (string.IsNullOrEmpty(originalCsvPath) ? "<unavailable>" : originalCsvPath) +
                    "; observed endTick=" + (Run != null ? Run.Tick.ToString() : "<unavailable>") +
                    "; CaptureEnded completions=" + string.Join(",", CaptureCompletions) +
                    "; telemetry LastError=" + (captureTelemetry != null ? captureTelemetry.LastError : "<unavailable>") +
                    "; paths are observed provenance, not independent file-integrity validation; stationary arranged smoke only.");
            }

            private void NeutralInput(InputFrame produced)
            {
                if (Run.Phase == RunPhase.Ended) return;
                neutralFrames++;
                if (!produced.Equals(default(InputFrame)))
                    Fail("Isolated gameplay producer emitted non-neutral input: " + DescribeInput(produced));
                Run.ReceiveInput(default);
            }
            private void OnSighting(HunterSighting fact) => sightings.Add(fact);
            private void OnHit(HunterHit hit) => hits.Add(hit);
            private void OnContact(Collider collider)
            {
                if (collider.GetComponentInParent<PlayerManager>() == player) contactTicks.Add(Run.Tick);
            }
            private void OnStarted(ChaseFact fact) => starts.Add(fact);
            private void OnCaught(ChaseFact fact)
            { catches.Add(fact); terminalOrder.Add("catch"); }
            private void OnHealth(EntityId id, float current, float maximum)
            {
                if (id != player.Id || maximum != 100f) Fail("Health relay has the wrong Player identity or maximum.");
                health.Add(current);
            }
            private void OnDeath(EntityId id, Vector3 killer)
            {
                deaths++;
                if (id != player.Id || hits.Count == 0 || killer != hits[hits.Count - 1].HunterPosition)
                    Fail("Death did not preserve the real lunge's target and killer position.");
                terminalOrder.Add("death");
            }
            private void OnCapture(long tick, bool complete)
            {
                CaptureCompletions.Add(complete);
                if (tick != Run.Tick) Fail("Capture did not close on the terminal fixed tick.");
                terminalOrder.Add("capture");
            }
            private void OnEnded(RunSummary summary)
            { Summaries.Add(summary); terminalOrder.Add("end"); }
            private void OnTick(InputFrame input, float dt, long tick)
            {
                CompletedTicks++;
                if (tick != previousTick + 1 || Mathf.Abs(dt - 1f / 60f) > 0.000001f)
                    Fail("Session did not produce consecutive 60 Hz ticks.");
                previousTick = tick;
                if (neutralFrames != CompletedTicks) Fail("Each real tick must follow one synthetic neutral publication.");
                if (!input.Equals(default(InputFrame)))
                    Fail("Stationary smoke received non-neutral input: " + DescribeInput(input));
                if (tick > 1200) Fail("Hunter did not finish within the bounded 20 simulated seconds.");
                if (!player.LastProbeRecord.Resolution.Present || !player.LastProbeRecord.Resolution.Grounded ||
                    Vector3.Distance(player.ReadOnlyState.Position, PlayerSpawn) > 0.1f)
                    Fail("The stationary Player did not remain on the authored connector floor.");
                if (Vector3.Distance(hunter.transform.position, hunter.ReadOnlyState.Position) > 0.001f)
                    Fail("Hunter physics and published pose diverged.");
                pathObserved |= driver.PathAvailable &&
                    Vector3.Distance(hunter.ReadOnlyState.Position, HunterSpawn) > 0.1f;
            }

            public void AssertOutcome()
            {
                Assert.That(Summaries.Count, Is.EqualTo(1), "Run did not end: " + Describe());
                Assert.That(readyCount, Is.EqualTo(1));
                Assert.That(Time.fixedTimeAsDouble, Is.GreaterThan(initialFixedTime));
                Assert.That(CompletedTicks, Is.InRange(2, 1200));
                Assert.That(neutralFrames, Is.EqualTo(CompletedTicks));
                Assert.That(pathObserved, Is.True, "Hunter never moved along a real available navigation path.");
                Assert.That(sightings.Count(fact => fact.Visible), Is.GreaterThan(1));
                Assert.That(sightings.All(fact => fact.Hunter == hunter.Id && fact.Target == player.Id), Is.True);
                Assert.That(hits.Count, Is.EqualTo(2), "Each active lunge must emit one accepted hit.");
                Assert.That(health, Is.EqualTo(new[] { 50f, 0f }));
                Assert.That(starts.Count, Is.EqualTo(2));
                Assert.That(catches.Count, Is.EqualTo(2));
                Assert.That(starts.Select(fact => fact.ChaseId).Distinct().Count(), Is.EqualTo(2));
                long firstSight = sightings.First(fact => fact.Visible).Tick;
                Assert.That((starts[0].Tick - firstSight + 1) * Time.fixedDeltaTime,
                    Is.GreaterThanOrEqualTo(chaseConfig.ConfirmationSeconds - 0.00001f));
                for (int i = 0; i < hits.Count; i++)
                {
                    Assert.That(contactTicks, Does.Contain(hits[i].Tick), "Hit lacks a real target capsule contact.");
                    Assert.That(hits[i].Hunter, Is.EqualTo(hunter.Id));
                    Assert.That(hits[i].Target, Is.EqualTo(player.Id));
                    Assert.That(hits[i].Damage, Is.EqualTo(profile.LungeDamage));
                    Assert.That(hits[i].Reason, Is.EqualTo(ChaseEndReason.Lunge));
                    Assert.That(starts[i].Phase, Is.EqualTo(ChasePhase.Confirmed));
                    Assert.That(starts[i].Tick, Is.LessThan(hits[i].Tick));
                    Assert.That(catches[i].ChaseId, Is.EqualTo(starts[i].ChaseId));
                    Assert.That(catches[i].Tick, Is.EqualTo(hits[i].Tick));
                    Assert.That(catches[i].Player, Is.EqualTo(player.Id));
                    Assert.That(catches[i].Hunter, Is.EqualTo(hunter.Id));
                    Assert.That(catches[i].EndReason, Is.EqualTo(ChaseEndReason.Lunge));
                }
                Assert.That(deaths, Is.EqualTo(1));
                Assert.That(terminalOrder, Is.EqualTo(new[] { "catch", "catch", "death", "capture", "end" }));
                Assert.That(player.ReadOnlyState.IsAlive, Is.False);
                Assert.That(chase.ReadOnlyState.HasActiveChase, Is.False);
                Assert.That(Run.Phase, Is.EqualTo(RunPhase.Ended));
                Assert.That(Summaries[0].EndReason, Is.EqualTo(RunEndReason.Died));
                Assert.That(Summaries[0].ChaseCount, Is.EqualTo(2));
                Assert.That(Summaries[0].ChasesEscaped, Is.Zero);
                Assert.That(Summaries[0].Scene, Is.EqualTo(SceneKey.TagArena));
                Assert.That(Summaries[0].Seed, Is.EqualTo(Run.Seed));
                Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(profileBefore));
                Assert.That(JsonUtility.ToJson(chaseConfig), Is.EqualTo(chaseBefore));
            }

            public string Describe() => "ready=" + Ready + "; ticks=" + CompletedTicks +
                "; visible=" + sightings.Count(fact => fact.Visible) + "; contacts=" + contactTicks.Count +
                "; hits=" + hits.Count + "; starts=" + starts.Count + "; catches=" + catches.Count +
                "; player=" + (player == null ? "absent" : player.ReadOnlyState.Position.ToString()) +
                "; hunter=" + (hunter == null ? "absent" : hunter.ReadOnlyState.Position.ToString());
            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }
            private static string DescribeInput(InputFrame input) => "move=" + input.Move +
                "; look=" + input.LookDelta + "; held=" + input.Held +
                "; pressed=" + input.Pressed + "; released=" + input.Released;
            public void Dispose()
            {
                if (captureRun != null) captureRun.CaptureStarted -= OnCaptureStarted;
                TagArenaSceneRoot.SceneReady -= ObserveReady;
                if (hunter != null) { hunter.OnSighting -= OnSighting; hunter.OnLungeHit -= OnHit; }
                if (driver != null) driver.OnLungeContact -= OnContact;
                if (Input != null)
                {
                    Input.FramePublished -= NeutralInput;
                    if (gameplay != null) gameplay.devices = previousDevices;
                }
                if (Run != null)
                {
                    Run.TickAdvanced -= OnTick;
                    Run.ChaseStarted -= OnStarted; Run.ChaseEnded -= OnCaught;
                    Run.HealthChanged -= OnHealth; Run.PlayerDied -= OnDeath;
                    Run.CaptureEnded -= OnCapture; Run.RunEnded -= OnEnded;
                }
            }
        }

        private static FieldInfo Field(string name) => typeof(TagArenaSceneRoot).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing spawn wiring: " + name);
        private static T One<T>() where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(values.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return values[0];
        }
        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            for (int frame = 0; frame < 3600 && !condition() && Time.realtimeSinceStartup < deadline; frame++)
                yield return null;
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
