// ============================================================================
// PlayerTransitionAcceptanceTests.cs
// ============================================================================
// PURPOSE:
//   Measures real landing and held-LookBack traversal transitions, including the
//   complete interval between the first positive input lock and observed release.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player physical integration.
// KEY RESPONSIBILITIES:
//   - Run off two real ledges; distinguish soft/hard Stumble from input locking.
//   - Hold LookBack across vault, mantle, jump, rebound and landing using Run ticks.
//   - Preserve every observed tick, stopped frame, lock edge and physical outcome.
// DEPENDENCIES:
//   Core; Player/Level/Hunter; Run/Input; TagArena scene factory and event wiring.
//   Unity physics, Input System, UnityEditor, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Root owns the Unity lease. Explicit temporary geometry at x=2000 and a runtime
//   spawn request are arranged before the scene factory; no pose/state is changed
//   after spawn. These benchmarks are not default authored routes or human play.
//   Asset values remain unchanged. Only the gameplay device filter is isolated;
//   neutral physical frames precede supplied frames through real Input/Run wiring.
//   Complete tick TSVs are diagnostics, not complete telemetry/input recordings.
//   Hunters are disabled before the first tick; chased/hardware acceptance remains.
//   Finally restores device filters, subscriptions, background state and geometry;
//   Test Framework restores the editor scene. Thin entries survive domain reload.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;

namespace Worsen.Tests.Player
{
    public sealed class PlayerTransitionAcceptanceTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";
        private static readonly Vector3 Origin = new Vector3(2000f, 0f, 0f);
        private const float Dt = 1f / 60f;

        [UnityTest]
        public IEnumerator PhysicalSoftAndHardLandingsRetainMomentumWithoutHardInputLock()
        {
            yield return new EnterPlayMode();
            yield return Exercise(TrialKind.SoftLanding);
            yield return Exercise(TrialKind.HardLanding);
        }

        [UnityTest]
        public IEnumerator HeldLookBackVaultCompletesAndReleasesItsMeasuredInputLock()
        {
            yield return new EnterPlayMode();
            yield return Exercise(TrialKind.Vault);
        }

        [UnityTest]
        public IEnumerator HeldLookBackMantleCompletesAndReleasesItsMeasuredInputLock()
        {
            yield return new EnterPlayMode();
            yield return Exercise(TrialKind.Mantle);
        }

        [UnityTest]
        public IEnumerator HeldLookBackJumpReboundAndLandingRemainUsableWithoutInputLock()
        {
            yield return new EnterPlayMode();
            yield return Exercise(TrialKind.Rebound);
        }

        private static IEnumerator Exercise(TrialKind kind)
        {
            bool previousBackground = Application.runInBackground;
            Hash128 sceneHash = AssetDatabase.GetAssetDependencyHash(ArenaPath);
            using (var trial = new Trial(kind))
            {
                Application.runInBackground = true;
                SceneManager.sceneLoaded += trial.ArrangeBeforeSpawn;
                try
                {
                    var load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                    Assert.That(load, Is.Not.Null);
                    yield return Until(() => trial.Ready || trial.Failure.Length > 0, 20f);
                    Assert.That(trial.Failure, Is.Empty, trial.Describe());
                    Assert.That(load.isDone && trial.Ready, Is.True, "Scene readiness: " + trial.Describe());
                    yield return Until(() => trial.Done || trial.Failure.Length > 0, 20f);
                    Assert.That(trial.Failure, Is.Empty, trial.Describe());
                    Assert.That(trial.Done, Is.True, "Fixed-tick deadline: " + trial.Describe());
                    Assert.That(AssetDatabase.GetAssetDependencyHash(ArenaPath), Is.EqualTo(sceneHash));
                    trial.AssertOutcome();
                }
                finally
                {
                    SceneManager.sceneLoaded -= trial.ArrangeBeforeSpawn;
                    Application.runInBackground = previousBackground;
                }
            }
        }

        private enum TrialKind { SoftLanding, HardLanding, Vault, Mantle, Rebound }

        private sealed class Sample
        {
            public InputProbeRecord Record;
            public PlayerMovementSample Movement;
            public Vector3 StateVelocity;
            public PlayerTraversalFact[] Facts;
            public string Phase;
            public string LockEdge;
            public float Penetration;
        }

        private sealed class Trial : IDisposable
        {
            private readonly TrialKind kind;
            private readonly List<Sample> samples = new List<Sample>();
            private readonly string reportPath;
            private readonly List<Collider> colliders = new List<Collider>();
            private GameObject arrangement;
            private Vector3 spawn, target;
            private string profileSnapshot, moverSnapshot;
            private PlayerProfile profile;
            private PlayerMoverDriverConfig mover;
            private PlayerManager player;
            private CapsuleCollider capsule;
            private RunSessionManager run;
            private RunSessionManager captureRun;
            private RunCaptureMetadata metadata;
            private int captures;
            private bool outcomeVerified;
            private InputManager input;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private bool restoreDevices, firstJump, secondJump;
            private int physicalFrames, movementEvents, traversalEvents;
            private InputFrame supplied;
            private InputButtons previousHeld;
            private float entrySpeed;
            private long firstLandTick, completionTick;
            public bool Ready;
            public string Failure = "";
            public bool Done => samples.Count >= RequiredTicks;
            private int RequiredTicks => IsLanding ? 300 : 180;
            private bool IsLanding => kind == TrialKind.SoftLanding || kind == TrialKind.HardLanding;
            private bool IsVault => kind == TrialKind.Vault || kind == TrialKind.Mantle;

            public Trial(TrialKind kind)
            {
                this.kind = kind;
                reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "PLAN-003",
                    "transition-acceptance", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + "-" + kind + ".tsv"));
            }

            public void ArrangeBeforeSpawn(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ArenaPath) return;
                try
                {
                    var root = One<TagArenaSceneRoot>();
                    profile = (PlayerProfile)new SerializedObject(root).FindProperty("_playerProfile").objectReferenceValue;
                    Assert.That(profile, Is.Not.Null);
                    profileSnapshot = EditorJsonUtility.ToJson(profile);
                    arrangement = new GameObject("[Test arrangement] Player transition " + kind + "; not shipped Level content");
                    arrangement.transform.position = Origin;
                    Box("Catch floor", new Vector3(20f, -0.25f, 0f), new Vector3(80f, 0.5f, 16f));
                    spawn = Origin + new Vector3(0f, 0.05f, 0f);
                    if (IsLanding)
                    {
                        float height = kind == TrialKind.SoftLanding ? 6f : 12f;
                        Box("Run-off platform " + height + " m", new Vector3(0f, height - 0.25f, 0f), new Vector3(8f, 0.5f, 6f));
                        spawn = Origin + new Vector3(-2f, height + 0.05f, 0f);
                    }
                    else if (IsVault)
                    {
                        float height = kind == TrialKind.Mantle ? 1.6f : 0.8f;
                        var ledge = Box("Traversal ledge " + height + " m", new Vector3(3f, height * 0.5f, 0f), new Vector3(2f, height, 3f));
                        target = Origin + new Vector3(3f, height, 0f);
                        Marker(ledge, 93001, LevelMarkerKind.VaultSurface, target);
                    }
                    else
                    {
                        var wall = Box("Rebound wall; front x=5.75", new Vector3(6f, 3f, 0f), new Vector3(0.5f, 6f, 6f));
                        Marker(wall, 93002, LevelMarkerKind.ReboundSurface, Vector3.zero);
                    }
                    Field(typeof(TagArenaSceneRoot), "_spawnPosition").SetValue(root, spawn);
                    captureRun = RunSessionManager.Instance != null ? RunSessionManager.Instance
                        : (RunSessionManager)Field(typeof(TagArenaSceneRoot), "_run").GetValue(root);
                    Assert.That(captureRun, Is.Not.Null);
                    captureRun.CaptureStarted += ObserveCaptureStarted;
                    Physics.SyncTransforms();
                    TagArenaSceneRoot.SceneReady += ObserveReady;
                }
                catch (Exception error) { Fail("Initial arrangement: " + error); }
            }

            private void ObserveCaptureStarted(RunCaptureMetadata value)
            {
                captures++;
                if (captures == 1) metadata = value;
                else Fail("Repeated CaptureStarted; original metadata retained.");
            }

            private BoxCollider Box(string name, Vector3 position, Vector3 size)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                item.name = "[Test] " + name;
                item.transform.SetParent(arrangement.transform, false);
                item.transform.localPosition = position;
                item.transform.localScale = size;
                var collider = item.GetComponent<BoxCollider>();
                colliders.Add(collider);
                return collider;
            }

            private static void Marker(BoxCollider collider, int id, LevelMarkerKind kind, Vector3 target)
            {
                var marker = collider.gameObject.AddComponent<LevelMarker>();
                var fields = new SerializedObject(marker);
                fields.FindProperty("_id").intValue = id;
                fields.FindProperty("_kind").enumValueIndex = (int)kind;
                fields.FindProperty("_roomId").intValue = 2;
                fields.FindProperty("_size").vector3Value = collider.bounds.size;
                fields.FindProperty("_targetPosition").vector3Value = target;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }

            private void ObserveReady(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                try
                {
                    run = RunSessionManager.Instance; input = InputManager.Instance; player = One<PlayerManager>();
                    Assert.That(run, Is.Not.Null); Assert.That(input, Is.Not.Null);
                    Assert.That(run.Tick, Is.Zero);
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(spawn));
                    Assert.That(PlayerRegistry.Items.Count, Is.EqualTo(1));
                    foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                        hunter.gameObject.SetActive(false);
                    Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    capsule = player.GetComponent<CapsuleCollider>();
                    mover = (PlayerMoverDriverConfig)new SerializedObject(player.GetComponent<PlayerDriver>())
                        .FindProperty("_config").objectReferenceValue;
                    Assert.That(mover, Is.Not.Null); Assert.That(capsule, Is.Not.Null);
                    moverSnapshot = EditorJsonUtility.ToJson(mover);
                    Assert.That(profile.SoftLandingThreshold, Is.EqualTo(12f));
                    Assert.That(profile.HardLandingThreshold, Is.EqualTo(18f));
                    Assert.That(profile.SoftStumbleDuration, Is.EqualTo(0.2f));
                    Assert.That(profile.HardStumbleDuration, Is.EqualTo(0.5f));
                    Assert.That(profile.VaultDuration, Is.EqualTo(0.25f));
                    Assert.That(profile.MantleDuration, Is.EqualTo(0.35f));
                    gameplay = (InputActionMap)Field(typeof(PlayerInputDriver), "_actions").GetValue(input.GetComponent<PlayerInputDriver>());
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    restoreDevices = true;
                    gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions) Assert.That(action.controls.Count, Is.Zero);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    input.FramePublished += SupplyInput;
                    run.PlayerProbeRecorded += Observe;
                    run.PlayerMovementPublished += ObserveMovementEvent;
                    run.PlayerTraversalPublished += ObserveTraversalEvent;
                    Ready = true;
                }
                catch (Exception error) { Fail("Scene readiness: " + error); }
            }

            private void SupplyInput(InputFrame physical)
            {
                if (Done || Failure.Length > 0) { run.ReceiveInput(default); return; }
                try
                {
                    Assert.That(physical.Equals(default(InputFrame)), Is.True, "Hardware producer must remain neutral.");
                    physicalFrames++;
                    Vector2 move = samples.Count < 10 ? Vector2.zero : Vector2.up;
                    InputButtons held = InputButtons.Sprint | (IsLanding ? InputButtons.None : InputButtons.LookBack);
                    InputButtons pressed = InputButtons.None;
                    if (IsLanding && firstLandTick > 0)
                        move = run.Tick - firstLandTick < 45 ? new Vector2(1f, 1f) : Vector2.zero;
                    if (IsVault)
                    {
                        if (!firstJump && samples.Count >= 10 && player.ReadOnlyState.Position.x >= Origin.x + 1.2f &&
                            player.LastProbeRecord.Probe.VaultCandidate && player.LastProbeRecord.Probe.VaultClearance > 0f)
                        { firstJump = true; pressed = InputButtons.Jump; entrySpeed = Speed(player.ReadOnlyState.Velocity); }
                        if (completionTick > 0 && run.Tick - completionTick > 10) move = Vector2.zero;
                    }
                    if (kind == TrialKind.Rebound)
                    {
                        var wall = player.LastProbeRecord.Probe;
                        if (!firstJump && player.ReadOnlyState.Position.x >= Origin.x + 4f)
                        { firstJump = true; pressed = InputButtons.Jump; }
                        else if (firstJump && !secondJump && player.ReadOnlyState.MovementState == MovementState.Air &&
                            wall.WallDetected && wall.WallId == 93002 && wall.WallDistance <= profile.ReboundDistance &&
                            wall.WallAngleDegrees <= profile.ReboundAngle)
                        { secondJump = true; pressed = InputButtons.Jump; }
                        if (secondJump) move = Vector2.zero;
                    }
                    held |= pressed;
                    supplied = new InputFrame(move, IsLanding ? Vector2.zero : new Vector2(0.5f, 0f),
                        held, pressed | (held & ~previousHeld), previousHeld & ~held);
                    previousHeld = held;
                    run.ReceiveInput(supplied);
                }
                catch (Exception error) { Fail("Input: " + error); run.ReceiveInput(default); }
            }

            private void Observe(InputProbeRecord record)
            {
                if (Done || Failure.Length > 0) return;
                var movement = player.LastMovementSample;
                bool wasLocked = samples.Count > 0 && samples[samples.Count - 1].Movement.InputLockSeconds > 0f;
                bool locked = movement.InputLockSeconds > 0f;
                var sample = new Sample { Record = record, Movement = movement,
                    StateVelocity = player.ReadOnlyState.Velocity, Facts = player.LastTraversalFacts.ToArray(),
                    Phase = Phase(), LockEdge = locked && !wasLocked ? "start" : !locked && wasLocked ? "end" : "" };
                samples.Add(sample); // Retain the failing tick before any assertion.
                try
                {
                    foreach (var other in colliders)
                        if (Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out float depth))
                            sample.Penetration = Mathf.Max(sample.Penetration, depth);
                    Assert.That(physicalFrames, Is.EqualTo(samples.Count));
                    Assert.That(record.Tick, Is.EqualTo(samples.Count));
                    Assert.That(record.Input, Is.EqualTo(supplied));
                    Assert.That(record.DeltaTime, Is.EqualTo(Dt));
                    Assert.That(record.Resolution.Present && player.ReadOnlyState.IsAlive, Is.True);
                    Assert.That(float.IsNaN(record.Resolution.Position.sqrMagnitude) || float.IsInfinity(record.Resolution.Position.sqrMagnitude), Is.False);
                    Assert.That(record.Resolution.Position.y, Is.GreaterThanOrEqualTo(-0.05f));
                    Assert.That(sample.Penetration, Is.LessThanOrEqualTo(mover.SkinWidth + 0.01f));
                    Assert.That(movement.InputLockSeconds, Is.InRange(0f, 0.35f));
                    if (!IsLanding)
                    {
                        Assert.That(movement.LookBack, Is.True);
                        Assert.That(movement.HeadingDegrees, Is.EqualTo(90f).Within(0.001f));
                        Assert.That(movement.HeadLookDelta.x, Is.EqualTo(0.5f));
                    }
                    if (sample.Facts.Any(f => f.Kind == TraversalKind.Land) && firstLandTick == 0)
                        firstLandTick = record.Tick;
                    if (sample.Facts.Any(f => f.Kind == TraversalKind.Vault || f.Kind == TraversalKind.Mantle || f.Kind == TraversalKind.Rebound))
                        completionTick = record.Tick;
                    Assert.That(sample.Facts.Any(f => !f.Succeeded), Is.False, "A requested transition failed.");
                }
                catch (Exception error) { Fail("Committed tick: " + error); }
            }

            private void ObserveMovementEvent(PlayerMovementSample value)
            {
                if (samples.Count == 0 || value.Tick != samples[samples.Count - 1].Record.Tick) return;
                try
                {
                    movementEvents++;
                    Assert.That(value, Is.EqualTo(samples[samples.Count - 1].Movement), "Run must publish the committed Player sample unchanged.");
                }
                catch (Exception error) { Fail("Movement event: " + error); }
            }

            private void ObserveTraversalEvent(PlayerTraversalFact value)
            {
                if (samples.Count == 0 || value.Tick != samples[samples.Count - 1].Record.Tick) return;
                try
                {
                    traversalEvents++;
                    Assert.That(samples[samples.Count - 1].Facts.Contains(value), Is.True, "Run must publish the committed traversal fact.");
                }
                catch (Exception error) { Fail("Traversal event: " + error); }
            }

            private string Phase() => samples.Count < 10 ? "settle" : IsLanding
                ? firstLandTick == 0 ? "run-off-and-fall" : "landing-response-and-stop"
                : kind == TrialKind.Rebound ? secondJump ? "rebound-and-land" : firstJump ? "jump-approach" : "approach"
                : completionTick > 0 ? "resumed-movement-and-stop" : firstJump ? "traversal" : "approach";

            public void AssertOutcome()
            {
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(profileSnapshot));
                Assert.That(EditorJsonUtility.ToJson(mover), Is.EqualTo(moverSnapshot));
                Assert.That(captures, Is.EqualTo(1));
                Assert.That(metadata.SessionId, Is.Not.Null.And.Not.Empty);
                Assert.That(metadata.StartTick, Is.Zero);
                Assert.That(metadata.FixedDeltaTime, Is.EqualTo(Dt));
                Assert.That(movementEvents, Is.EqualTo(samples.Count));
                Assert.That(traversalEvents, Is.EqualTo(samples.Sum(s => s.Facts.Length)));
                Assert.That(samples.Last().Record.Resolution.Grounded, Is.True, Describe());
                Assert.That(Speed(samples.Last().StateVelocity), Is.LessThan(0.01f), "Keep the stopped terminal frames in the trial.");
                if (IsLanding) AssertLanding();
                else if (IsVault) AssertVault();
                else AssertRebound();
                outcomeVerified = true;
                TestContext.WriteLine(Describe());
            }

            private void AssertLanding()
            {
                var land = samples.Single(s => s.Facts.Any(f => f.Kind == TraversalKind.Land));
                var fact = land.Facts.Single(f => f.Kind == TraversalKind.Land);
                var prior = samples.Single(s => s.Record.Tick == land.Record.Tick - 1);
                bool hard = kind == TrialKind.HardLanding;
                float retained = hard ? profile.HardLandingRetention : profile.SoftLandingRetention;
                float duration = hard ? profile.HardStumbleDuration : profile.SoftStumbleDuration;
                float measuredDescent = samples.Max(s => Mathf.Max(0f, -s.StateVelocity.y));
                Assert.That(samples.Any(s => !s.Record.Probe.Grounded && !s.Record.Resolution.Grounded &&
                    s.Record.Resolution.Position.x > Origin.x + 4.3f), Is.True, "A real run-off and fall are required.");
                Assert.That(measuredDescent, Is.GreaterThanOrEqualTo(hard ? 18f : 12f));
                if (!hard) Assert.That(measuredDescent, Is.LessThan(18f));
                Assert.That(fact.Duration, Is.EqualTo(duration));
                Assert.That(land.Movement.MovementState, Is.EqualTo(MovementState.Stumble));
                Assert.That(Speed(prior.StateVelocity), Is.GreaterThan(7.5f));
                Assert.That(Speed(land.StateVelocity), Is.EqualTo(Speed(prior.StateVelocity) * retained).Within(0.02f));
                Assert.That(samples.All(s => s.Movement.InputLockSeconds == 0f && s.LockEdge.Length == 0), Is.True);
                var stumble = samples.Where(s => s.Movement.MovementState == MovementState.Stumble).ToArray();
                Assert.That(stumble.Length * (double)Dt, Is.EqualTo(duration).Within(Dt + 0.000001));
                if (hard) Assert.That(stumble.Length * (double)Dt, Is.GreaterThan(0.35));
                Assert.That(stumble.Any(s => s.Record.Tick > land.Record.Tick && Mathf.Abs(s.StateVelocity.z) > 0.1f &&
                    Mathf.Abs(s.Record.Resolution.Position.z - land.Record.Resolution.Position.z) > 0.01f), Is.True,
                    "Stumble must accept steering and physically move before its state duration expires.");
                Assert.That(samples.SkipWhile(s => s.Record.Tick <= stumble.Last().Record.Tick)
                    .Any(s => s.Movement.MovementState == MovementState.Ground), Is.True);
                Assert.That(samples.Sum(s => s.Facts.Count(f => f.Kind != TraversalKind.Land)), Is.Zero,
                    "Run-off landing uses ordinary motion; no injected jump or traversal.");
            }

            private void AssertVault()
            {
                var kindExpected = kind == TrialKind.Mantle ? TraversalKind.Mantle : TraversalKind.Vault;
                var completed = samples.Single(s => s.Facts.Any(f => f.Kind == kindExpected));
                var fact = completed.Facts.Single(f => f.Kind == kindExpected);
                var locked = samples.Where(s => s.Movement.InputLockSeconds > 0f).ToArray();
                float duration = kind == TrialKind.Mantle ? profile.MantleDuration : profile.VaultDuration;
                Assert.That(firstJump, Is.True);
                Assert.That(locked.Length, Is.EqualTo(kind == TrialKind.Mantle ? 21 : 15));
                Assert.That(samples.Count(s => s.LockEdge == "start"), Is.EqualTo(1));
                Assert.That(samples.Count(s => s.LockEdge == "end"), Is.EqualTo(1));
                var released = samples.Single(s => s.LockEdge == "end");
                Assert.That(released.Record.Tick, Is.EqualTo(locked.Last().Record.Tick + 1));
                Assert.That(completed.Record.Tick, Is.EqualTo(locked.Last().Record.Tick));
                Assert.That(locked.First().Movement.InputLockSeconds, Is.EqualTo(duration));
                Assert.That(locked.Select(s => s.Record.Tick), Is.EqualTo(Enumerable.Range(0, locked.Length)
                    .Select(offset => locked.First().Record.Tick + offset)));
                double measuredLock = locked.Sum(s => (double)s.Record.DeltaTime);
                // Float32 1/60 sums to 0.350000018... for 21 ticks. This tolerance
                // covers numeric representation only; a 22nd lock tick still fails.
                Assert.That(measuredLock, Is.LessThanOrEqualTo(0.35 + 0.000001));
                Assert.That(fact.Duration, Is.EqualTo(duration));
                Assert.That(Vector3.Distance(completed.Record.Resolution.Position, target),
                    Is.LessThanOrEqualTo(profile.VaultCompletionTolerance + 0.001f));
                Assert.That(entrySpeed, Is.GreaterThan(7.5f));
                Assert.That(Speed(completed.StateVelocity), Is.EqualTo(entrySpeed).Within(0.01f));
                Assert.That(samples.Any(s => s.Record.Tick > released.Record.Tick && s.Record.Tick <= released.Record.Tick + 8 &&
                    s.Record.Resolution.Position.x > completed.Record.Resolution.Position.x + 0.1f), Is.True,
                    "Forward movement must resume after the observed lock release while LookBack remains held.");
                Assert.That(samples.Sum(s => s.Facts.Count(f => f.Kind == TraversalKind.Vault || f.Kind == TraversalKind.Mantle)), Is.EqualTo(1));
            }

            private void AssertRebound()
            {
                Assert.That(firstJump && secondJump, Is.True);
                var rebound = samples.Single(s => s.Facts.Any(f => f.Kind == TraversalKind.Rebound));
                Assert.That(rebound.Record.Probe.WallDetected && rebound.Record.Probe.WallId == 93002, Is.True);
                Assert.That(rebound.Record.Probe.WallDistance, Is.LessThanOrEqualTo(profile.ReboundDistance));
                Assert.That(rebound.Record.Probe.WallAngleDegrees, Is.LessThanOrEqualTo(profile.ReboundAngle));
                Assert.That(Vector3.Dot(rebound.Record.Resolution.Velocity, rebound.Record.Probe.WallNormal), Is.GreaterThan(0.1f));
                var prior = samples.Single(s => s.Record.Tick == rebound.Record.Tick - 1);
                Assert.That(Vector3.Dot(prior.StateVelocity, rebound.Record.Probe.WallNormal), Is.LessThan(-0.1f));
                Assert.That(rebound.StateVelocity.y, Is.EqualTo(prior.StateVelocity.y + profile.ReboundUpwardBoost - profile.Gravity * Dt).Within(0.02f));
                Assert.That(samples.Sum(s => s.Facts.Count(f => f.Kind == TraversalKind.Jump)), Is.EqualTo(1));
                Assert.That(samples.Sum(s => s.Facts.Count(f => f.Kind == TraversalKind.Rebound)), Is.EqualTo(1));
                Assert.That(samples.Any(s => s.Record.Tick > rebound.Record.Tick && s.Facts.Any(f => f.Kind == TraversalKind.Land)), Is.True);
                Assert.That(samples.All(s => s.Movement.InputLockSeconds == 0f && s.LockEdge.Length == 0), Is.True);
            }

            public string Describe() => "Physical transition=" + kind + "; samples=" + samples.Count + "/" + RequiredTicks +
                "; spawn=" + spawn + "; target=" + target + "; land=" + firstLandTick + "; traversal=" + completionTick +
                "; physical/movement/traversal events=" + physicalFrames + "/" + movementEvents + "/" + traversalEvents +
                "; lock positive ticks=" + samples.Count(s => s.Movement.InputLockSeconds > 0f) +
                "; lock raw seconds=" + samples.Where(s => s.Movement.InputLockSeconds > 0f).Sum(s => (double)s.Record.DeltaTime).ToString("R", CultureInfo.InvariantCulture) +
                "; diagnostic=" + reportPath;

            private void SaveReport()
            {
                var rows = new StringBuilder();
                rows.AppendLine("# " + Describe());
                rows.AppendLine("# Temporary physics benchmark; all observed ticks retained; no complete recording or participant claim.");
                rows.AppendLine("# OutcomeVerified=" + outcomeVerified + "; original SessionId=" + metadata.SessionId +
                    "; seed=" + metadata.Seed + "; captureStartTick=" + metadata.StartTick +
                    "; firstObservedTick=" + (samples.Count == 0 ? -1 : samples[0].Record.Tick) +
                    "; lastObservedTick=" + (samples.Count == 0 ? -1 : samples[samples.Count - 1].Record.Tick) +
                    "; captureSourceRevision=" + metadata.SourceRevision + "; captureConfigHash=" + metadata.ConfigSnapshotHash);
                rows.AppendLine("# Failure=" + Failure.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '));
                rows.AppendLine("# PlayerProfile=" + profileSnapshot);
                rows.AppendLine("# PlayerMoverDriverConfig=" + moverSnapshot);
                foreach (var item in colliders.Where(c => c != null))
                    rows.AppendLine("# Geometry=" + item.name + "; center=" + item.bounds.center.ToString("F5") + "; size=" + item.bounds.size.ToString("F5"));
                rows.AppendLine("tick\tphase\tdt\tstate\tlockSeconds\tderivedLockEdge\tx\ty\tz\tvx\tvy\tvz\tstateVx\tstateVy\tstateVz\tprobeGround\tresolvedGround\tlookBack\theading\tmoveX\tmoveY\theld\tpressed\tvaultCandidate\tvaultHeight\tvaultClearance\twallId\twallDistance\tpenetration\tfacts");
                foreach (var s in samples)
                {
                    var r = s.Record; var m = s.Movement; var p = r.Resolution.Position; var v = r.Resolution.Velocity;
                    object[] values = { r.Tick, s.Phase, r.DeltaTime, m.MovementState, m.InputLockSeconds, s.LockEdge,
                        p.x, p.y, p.z, v.x, v.y, v.z, s.StateVelocity.x, s.StateVelocity.y, s.StateVelocity.z,
                        r.Probe.Grounded, r.Resolution.Grounded, m.LookBack, m.HeadingDegrees, r.Input.Move.x, r.Input.Move.y,
                        r.Input.Held, r.Input.Pressed, r.Probe.VaultCandidate, r.Probe.VaultHeight, r.Probe.VaultClearance,
                        r.Probe.WallId, r.Probe.WallDistance, s.Penetration,
                        string.Join(";", s.Facts.Select(f => f.Kind + ":" + f.Succeeded + ":" + f.Duration.ToString("R", CultureInfo.InvariantCulture))) };
                    rows.AppendLine(string.Join("\t", values.Select(value => value is float number
                        ? number.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture))));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, rows.ToString());
                TestContext.WriteLine("Observed transition trace: " + reportPath);
            }

            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }
            public void Dispose()
            {
                TagArenaSceneRoot.SceneReady -= ObserveReady;
                if (captureRun != null) captureRun.CaptureStarted -= ObserveCaptureStarted;
                if (input != null) input.FramePublished -= SupplyInput;
                if (run != null)
                {
                    run.PlayerProbeRecorded -= Observe;
                    run.PlayerMovementPublished -= ObserveMovementEvent;
                    run.PlayerTraversalPublished -= ObserveTraversalEvent;
                    run.ReceiveInput(default);
                }
                try { SaveReport(); }
                finally
                {
                    if (gameplay != null && restoreDevices) gameplay.devices = previousDevices;
                    if (arrangement != null) UnityEngine.Object.DestroyImmediate(arrangement);
                }
            }
        }

        private static float Speed(Vector3 velocity) => new Vector2(velocity.x, velocity.z).magnitude;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing runtime test wiring: " + type.Name + "." + name);
        private static T One<T>() where T : Component
        {
            var items = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(items.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return items[0];
        }
        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            for (int frame = 0; frame < 10000 && !condition() && Time.realtimeSinceStartupAsDouble < deadline; frame++) yield return null;
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
