// ============================================================================
// TagArenaConnectedTraversalTests.cs
// ============================================================================
// PURPOSE:
//   Exercises one finite connected Player route through the shipped TagArena
//   clutter lane, rising line, upper bridge and one-way drop with native ticks.
//   The complete trace distinguishes connected collision behavior from isolated
//   marker checks and exposes the first blocked junction without altering it.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Level physical integration.
// KEY RESPONSIBILITIES:
//   - Traverse actual markers in spatial order: 210, 220, 211, 221, 212, then 106.
//   - Observe real PlayerDriver probes/resolutions and Session movement/events.
//   - Preserve all observed ticks, original capture provenance, stops and failures.
// DEPENDENCIES:
//   Core; Level/Player/Hunter; Run/Input; TagArena SceneRoot and its real factory.
//   Unity physics, Input System, UnityEditor, NUnit and Unity Test Framework.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Only initial runtime factory spawn is
//   arranged at the authored west join; no subsequent pose/state writes, fake
//   probes, direct ticks, generated geometry, tuning or time changes are made.
//   Hunters are disabled before tick one for isolation. Device filters, runtime
//   spawn field, subscriptions and background setting are restored in finally.
//   A full exact-record .winput diagnostic is explicitly incomplete even when
//   this route passes: the fixture does not manufacture a completed run capture.
//   Every observed tick is retained, including those after a failure before
//   iterator cleanup. A 2400-tick/90-wall-second cutoff bounds the native trial.
//   This is synthetic route evidence, not hardware, chase, speed-population or
//   participant acceptance. Test Framework restores the outer editor scene.
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

namespace Worsen.Tests.Level
{
    public sealed class TagArenaConnectedTraversalTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";
        private const float Dt = 1f / 60f;
        private const int MaximumTicks = 2400;
        private static readonly Vector3 Spawn = new Vector3(-21f, 0.05f, 5f);

        [UnityTest, Timeout(120000)]
        public IEnumerator NativeClutterRisingLineBridgeAndDropFormOneConnectedRoute()
        {
            yield return new EnterPlayMode();
            yield return Exercise();
        }

        private static IEnumerator Exercise()
        {
            bool previousBackground = Application.runInBackground;
            using (var trial = new Trial())
            {
                Application.runInBackground = true;
                SceneManager.sceneLoaded += trial.ArrangeBeforeSpawn;
                try
                {
                    var load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                    Assert.That(load, Is.Not.Null, "Build and enable the authored TagArena before this fixture.");
                    double deadline = Time.realtimeSinceStartupAsDouble + 20d;
                    while (!trial.Ready && trial.Failure.Length == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(trial.Failure, Is.Empty, trial.Describe());
                    Assert.That(load.isDone && trial.Ready, Is.True, "Readiness deadline: " + trial.Describe());
                    deadline = Time.realtimeSinceStartupAsDouble + 90d;
                    while (!trial.Done && trial.Failure.Length == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (!trial.Done && trial.Failure.Length == 0) trial.Fail("90 wall-second cutoff reached.");
                    Assert.That(trial.Failure, Is.Empty, trial.Describe());
                    Assert.That(trial.Done, Is.True, trial.Describe());
                    trial.AssertOutcome();
                }
                finally
                {
                    SceneManager.sceneLoaded -= trial.ArrangeBeforeSpawn;
                    Application.runInBackground = previousBackground;
                }
            }
        }

        private enum Phase
        {
            Settle, WestJoin, ClutterEntry, Vault210, Slide220, Vault211, Slide221, Vault212,
            EastCorner, EastJoin, AtriumNorth, AtriumWest, RampApproach, RampFoot, RampRise,
            UpperDeck, BridgeEntry, BridgeRun, Drop, LandStop, Complete
        }

        private sealed class Sample
        {
            public InputProbeRecord Record;
            public InputFrame Supplied;
            public PlayerMovementSample Movement;
            public PlayerTraversalFact[] Facts;
            public readonly List<PlayerTraversalFact> PublishedFacts = new List<PlayerTraversalFact>();
            public Vector3 StateVelocity;
            public string Phase, FailureAtObservation;
            public float CapsuleHeight, Penetration;
            public bool MovementPublished;
        }

        private sealed class Trial : IDisposable
        {
            private readonly List<Sample> samples = new List<Sample>();
            private readonly List<string> stamps = new List<string>();
            private readonly List<string> transitions = new List<string>();
            private readonly Dictionary<int, LevelMarker> markers = new Dictionary<int, LevelMarker>();
            private readonly Dictionary<int, string> markerJson = new Dictionary<int, string>();
            private readonly Dictionary<int, Bounds> markerBounds = new Dictionary<int, Bounds>();
            private readonly List<GameObject> disabledHunters = new List<GameObject>();
            private readonly List<int> completedObstacles = new List<int>();
            private readonly string reportPath;
            private readonly double wallStart;
            private readonly string sceneHash;
            private TagArenaSceneRoot root;
            private Vector3 originalSpawn;
            private bool restoreSpawn;
            private RunSessionManager run, captureRun;
            private InputManager input;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private bool restoreDevices;
            private PlayerManager player;
            private PlayerProfile profile;
            private PlayerMoverDriverConfig mover;
            private CapsuleCollider capsule;
            private Collider[] observedGeometry = Array.Empty<Collider>();
            private string profileSnapshot, moverSnapshot;
            private RunCaptureMetadata metadata;
            private int captures, physicalFrames, stageTicks, stopTicks;
            private Phase phase;
            private InputFrame supplied;
            private InputButtons previousHeld;
            private bool jumpIssued, slideIssued, sawGateUnder, sawGateBlocked, sawDropAir, sawDropLand;
            private long dropAirTick, dropLandTick;
            private Vector3 lastPosition = Spawn, progressPosition = Spawn;
            private int progressTick;
            private bool outcomeVerified;
            public bool Ready;
            public string Failure = "";
            public bool Done => phase == Phase.Complete;

            public Trial()
            {
                wallStart = Time.realtimeSinceStartupAsDouble;
                reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "PLAN-004",
                    "connected-route", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".tsv"));
                sceneHash = AssetDatabase.GetAssetDependencyHash(ArenaPath).ToString();
                foreach (string path in new[] { ArenaPath, "Assets/Editor/Level/TagArenaLevelSetup.cs",
                    "Assets/Editor/Tests/Level/TagArenaConnectedTraversalTests.cs",
                    "Assets/Scripts/Domain/Player/Controller/PlayerController.cs", "Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs",
                    "Assets/Scripts/Domain/Player/Driver/PlayerMoverPresenter.cs", "Assets/Scripts/Domain/Player/Manager/PlayerManager.cs",
                    "Assets/Scripts/Domain/Level/Driver/LevelMarker.cs", "Assets/Scripts/Session/Run/Manager/RunSessionManager.cs",
                    "Assets/Scripts/Orchestrator/Scenes/TagArenaSceneRoot.cs", "ProjectSettings/TimeManager.asset" }) Stamp(path);
            }

            public void ArrangeBeforeSpawn(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ArenaPath) return;
                try
                {
                    root = One<TagArenaSceneRoot>();
                    originalSpawn = (Vector3)Field(typeof(TagArenaSceneRoot), "_spawnPosition").GetValue(root);
                    restoreSpawn = true;
                    Field(typeof(TagArenaSceneRoot), "_spawnPosition").SetValue(root, Spawn);
                    profile = (PlayerProfile)new SerializedObject(root).FindProperty("_playerProfile").objectReferenceValue;
                    Assert.That(profile, Is.Not.Null);
                    profileSnapshot = EditorJsonUtility.ToJson(profile);
                    Stamp(AssetDatabase.GetAssetPath(profile));
                    foreach (int id in new[] { 210, 220, 211, 221, 212, 106 })
                    {
                        var marker = UnityEngine.Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).Single(item => item.SurfaceId == id);
                        markers.Add(id, marker); markerJson.Add(id, EditorJsonUtility.ToJson(marker));
                        markerBounds.Add(id, marker.GetComponent<Collider>().bounds);
                    }
                    captureRun = RunSessionManager.Instance != null ? RunSessionManager.Instance
                        : (RunSessionManager)Field(typeof(TagArenaSceneRoot), "_run").GetValue(root);
                    Assert.That(captureRun, Is.Not.Null);
                    captureRun.CaptureStarted += ObserveCaptureStarted;
                    TagArenaSceneRoot.SceneReady += ObserveReady;
                }
                catch (Exception error) { Fail("Initial factory arrangement: " + error); }
            }

            private void ObserveCaptureStarted(RunCaptureMetadata value)
            {
                captures++;
                if (captures == 1) metadata = value;
                else Fail("Repeated CaptureStarted; original metadata retained.");
            }

            private void ObserveReady(SceneKey scene)
            {
                if (scene != SceneKey.TagArena) return;
                try
                {
                    run = RunSessionManager.Instance; input = InputManager.Instance; player = One<PlayerManager>();
                    Assert.That(run, Is.Not.Null); Assert.That(input, Is.Not.Null);
                    Assert.That(run.Tick, Is.Zero);
                    Assert.That(player.ReadOnlyState.Position, Is.EqualTo(Spawn));
                    Assert.That(Time.fixedDeltaTime, Is.EqualTo(Dt)); Assert.That(Time.timeScale, Is.EqualTo(1f));
                    var graph = One<LevelManager>().ReadOnlyState.Graph;
                    Assert.That(graph.Edges.Single(edge => edge.Id == 105).Access, Is.EqualTo(TraversalAccess.Player));
                    Assert.That(graph.Edges.Single(edge => edge.Id == 106).Bidirectional, Is.False);
                    Assert.That(graph.Edges.Single(edge => edge.Id == 106).Access, Is.EqualTo(TraversalAccess.Player));
                    foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                    { disabledHunters.Add(hunter.gameObject); hunter.gameObject.SetActive(false); }
                    Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    capsule = player.GetComponent<CapsuleCollider>();
                    mover = (PlayerMoverDriverConfig)new SerializedObject(player.GetComponent<PlayerDriver>()).FindProperty("_config").objectReferenceValue;
                    Assert.That(mover, Is.Not.Null); Assert.That(capsule, Is.Not.Null);
                    moverSnapshot = EditorJsonUtility.ToJson(mover); Stamp(AssetDatabase.GetAssetPath(mover));
                    Assert.That(profile.SprintSpeed, Is.EqualTo(8f)); Assert.That(profile.MaxDesignSpeed, Is.EqualTo(14f));
                    Assert.That(mover.Height, Is.EqualTo(1.8f)); Assert.That(mover.Height * mover.SlideHeightRatio, Is.EqualTo(0.9f));
                    Assert.That(mover.StepHeight, Is.EqualTo(0.3f)); Assert.That(mover.SlopeLimitDegrees, Is.EqualTo(50f));
                    foreach (int id in new[] { 210, 211, 212 })
                    {
                        Assert.That(markers[id].Kind, Is.EqualTo(TraversalSurfaceKind.Vault));
                        Assert.That(markerBounds[id].size.y, Is.EqualTo(1f).Within(0.0001f));
                        Assert.That(markers[id].Target, Is.EqualTo(new Vector3(markerBounds[id].center.x + 1.6f, 0f, 15f)));
                    }
                    foreach (int id in new[] { 220, 221 })
                    {
                        Assert.That(markers[id].Kind, Is.EqualTo(TraversalSurfaceKind.SlideGate));
                        Assert.That(markerBounds[id].min.y, Is.EqualTo(1.05f).Within(0.0001f));
                    }
                    Assert.That(markers[106].Target, Is.EqualTo(new Vector3(7.5f, 0f, 4f)));
                    observedGeometry = One<LevelManager>().GetComponentsInChildren<Collider>(true).Where(item => item.enabled && !item.isTrigger).ToArray();
                    foreach (var item in observedGeometry)
                        stamps.Add("Geometry=" + item.name + "; center=" + Vec(item.bounds.center) + "; size=" + Vec(item.bounds.size) +
                            "; rotation=" + Vec(item.transform.eulerAngles));
                    gameplay = (InputActionMap)Field(typeof(PlayerInputDriver), "_actions").GetValue(input.GetComponent<PlayerInputDriver>());
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    restoreDevices = true; gameplay.devices = Array.Empty<InputDevice>();
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
                physicalFrames++;
                supplied = default;
                if (Done || Failure.Length > 0) { run.ReceiveInput(supplied); return; }
                try
                {
                    Assert.That(physical.Equals(default(InputFrame)), Is.True, "Hardware producer must remain neutral.");
                    Vector2 move = Vector2.zero;
                    float heading = player.ReadOnlyState.HeadingDegrees;
                    InputButtons held = InputButtons.Sprint, pressed = InputButtons.None;
                    if (phase >= Phase.Vault210 && phase <= Phase.Vault212)
                    {
                        heading = 90f; move = Vector2.up;
                        int id = Obstacle();
                        var bounds = markerBounds[id];
                        if (IsVault())
                        {
                            var probe = player.LastProbeRecord.Probe;
                            if (!jumpIssued && Vector3.Distance(player.ReadOnlyState.Position, markers[id].Target) <= 2.4f &&
                                probe.VaultCandidate && probe.VaultClearance > 0f && !probe.StandingBlocked &&
                                Vector3.Distance(probe.VaultTarget, markers[id].Target) < 0.01f)
                            { jumpIssued = true; pressed = InputButtons.Jump; }
                            if (jumpIssued) move = Vector2.zero;
                        }
                        else
                        {
                            if (!slideIssued && bounds.min.x - player.ReadOnlyState.Position.x <= 2f &&
                                player.LastProbeRecord.Resolution.Grounded && Speed(player.ReadOnlyState.Velocity) >= profile.SlideMinimumSpeed)
                            { slideIssued = true; pressed = InputButtons.Crouch; }
                            if (slideIssued) held = InputButtons.Sprint | InputButtons.Crouch;
                        }
                    }
                    else if (phase == Phase.Drop)
                    { heading = 270f; move = Vector2.up; held = InputButtons.None; }
                    else if (phase != Phase.Settle && phase != Phase.LandStop)
                    {
                        Vector3 delta = Waypoint() - player.ReadOnlyState.Position; delta.y = 0f;
                        if (delta.magnitude > 0.08f)
                        {
                            heading = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                            move = Vector2.up * Mathf.Min(1f, delta.magnitude * 1.5f);
                        }
                    }
                    held |= pressed;
                    supplied = new InputFrame(move, new Vector2(Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, heading), 0f),
                        held, pressed | (held & ~previousHeld), previousHeld & ~held);
                    previousHeld = held;
                    run.ReceiveInput(supplied);
                }
                catch (Exception error) { Fail("Synthetic input: " + error); supplied = default; run.ReceiveInput(supplied); }
            }

            private void Observe(InputProbeRecord record)
            {
                var sample = new Sample { Record = record, Supplied = supplied, Movement = player.LastMovementSample,
                    StateVelocity = player.ReadOnlyState.Velocity, Facts = player.LastTraversalFacts.ToArray(), Phase = phase.ToString(),
                    CapsuleHeight = capsule.height, FailureAtObservation = Failure };
                samples.Add(sample); // Never discard a failing/stopped tick or any tick observed before unbinding.
                if (Done || Failure.Length > 0) return;
                try
                {
                    stageTicks++;
                    Assert.That(physicalFrames, Is.EqualTo(samples.Count));
                    Assert.That(record.Tick, Is.EqualTo(samples.Count));
                    Assert.That(record.Input, Is.EqualTo(supplied)); Assert.That(record.DeltaTime, Is.EqualTo(Dt));
                    Assert.That(record.Resolution.Present && player.ReadOnlyState.IsAlive, Is.True);
                    Assert.That(Finite(record.Resolution.Position) && Finite(record.Resolution.Velocity), Is.True);
                    Assert.That(record.Resolution.Position.y, Is.GreaterThanOrEqualTo(-0.06f));
                    Assert.That(player.ReadOnlyState.Health, Is.EqualTo(profile.MaximumHealth));
                    Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    Assert.That(FlatDistance(lastPosition, record.Resolution.Position), Is.LessThanOrEqualTo(profile.MaxDesignSpeed * Dt + 0.005f),
                        "Unexpected horizontal discontinuity in native motion.");
                    lastPosition = record.Resolution.Position;
                    foreach (var other in observedGeometry)
                    {
                        if (!other.bounds.Intersects(capsule.bounds)) continue;
                        if (Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out float depth))
                            sample.Penetration = Mathf.Max(sample.Penetration, depth);
                    }
                    Assert.That(sample.Penetration, Is.LessThanOrEqualTo(mover.SkinWidth + 0.015f));
                    Assert.That(sample.Facts.Any(fact => !fact.Succeeded), Is.False, "A native traversal attempt failed.");
                    if (phase >= Phase.Vault210 && phase <= Phase.Vault212)
                    {
                        Assert.That(record.Resolution.Position.z, Is.EqualTo(15f).Within(0.12f), "The full clutter lane must stay within its centerline.");
                        int id = Obstacle();
                        Bounds bounds = markerBounds[id];
                        if (IsVault())
                        {
                            if ((record.Input.Pressed & InputButtons.Jump) != 0)
                            {
                                Assert.That(record.Probe.VaultCandidate && record.Probe.VaultClearance > 0f, Is.True);
                                Assert.That(record.Probe.VaultTarget, Is.EqualTo(markers[id].Target));
                                Assert.That(record.Probe.VaultHeight, Is.EqualTo(1f).Within(0.06f));
                            }
                            var facts = sample.Facts.Where(fact => fact.Kind == TraversalKind.Vault).ToArray();
                            if (facts.Length > 0)
                            {
                                Assert.That(facts.Length, Is.EqualTo(1)); Assert.That(jumpIssued, Is.True);
                                Assert.That(Vector3.Distance(record.Resolution.Position, markers[id].Target), Is.LessThanOrEqualTo(profile.VaultCompletionTolerance + 0.001f));
                                Assert.That(record.Resolution.Position.x, Is.GreaterThan(bounds.max.x + mover.Radius));
                                completedObstacles.Add(id); Advance((Phase)((int)phase + 1));
                            }
                        }
                        else
                        {
                            if (record.Resolution.Position.x >= bounds.min.x && record.Resolution.Position.x <= bounds.max.x)
                            {
                                Assert.That(slideIssued && record.Resolution.Grounded, Is.True);
                                Assert.That(sample.CapsuleHeight, Is.EqualTo(mover.Height * mover.SlideHeightRatio).Within(0.001f));
                                Assert.That(record.Resolution.Position.y + sample.CapsuleHeight, Is.LessThan(bounds.min.y));
                                sawGateUnder = true;
                            }
                            sawGateBlocked |= record.Probe.StandingBlocked;
                            if (record.Resolution.Position.x > bounds.max.x + mover.Radius + 0.1f)
                            {
                                Assert.That(sawGateUnder && sawGateBlocked, Is.True, "No full native low-capsule/standing-obstruction crossing.");
                                completedObstacles.Add(id); Advance((Phase)((int)phase + 1));
                            }
                        }
                    }
                    else if (phase == Phase.Settle)
                    {
                        if (stageTicks >= 10 && record.Resolution.Grounded && Speed(sample.StateVelocity) < 0.01f) Advance(Phase.WestJoin);
                    }
                    else if (phase == Phase.Drop)
                    {
                        if (!record.Probe.Grounded && !record.Resolution.Grounded && record.Resolution.Position.x < markerBounds[106].min.x &&
                            record.Resolution.Position.y > 0.5f)
                        { if (!sawDropAir) dropAirTick = record.Tick; sawDropAir = true; }
                        if (sawDropAir && sample.Facts.Any(fact => fact.Kind == TraversalKind.Land && fact.Succeeded))
                        {
                            sawDropLand = true; dropLandTick = record.Tick;
                            Assert.That(record.Resolution.Grounded, Is.True);
                            Assert.That(record.Resolution.Position.y, Is.EqualTo(0f).Within(0.06f));
                            Assert.That(FlatDistance(record.Resolution.Position, markers[106].Target), Is.LessThan(1.2f));
                            Advance(Phase.LandStop);
                        }
                    }
                    else if (phase == Phase.LandStop)
                    {
                        if (record.Resolution.Grounded && Speed(sample.StateVelocity) < 0.01f) stopTicks++;
                        else stopTicks = 0;
                        if (stopTicks >= 30) Advance(Phase.Complete);
                    }
                    else if (FlatDistance(record.Resolution.Position, Waypoint()) < 0.2f && record.Resolution.Grounded && Speed(sample.StateVelocity) < 0.3f)
                    {
                        Assert.That(record.Resolution.Position.y, Is.EqualTo(Waypoint().y).Within(0.22f), "Waypoint reached at wrong elevation.");
                        Advance((Phase)((int)phase + 1));
                    }
                    if (!Done && samples.Count >= MaximumTicks) Fail("2400 native fixed-tick cutoff reached.");
                    if (!Done && stageTicks > 480) Fail("480-tick phase cutoff: " + phase);
                    if (!Done && phase != Phase.Settle && phase != Phase.LandStop)
                    {
                        if (Vector3.Distance(record.Resolution.Position, progressPosition) > 0.05f)
                        { progressPosition = record.Resolution.Position; progressTick = samples.Count; }
                        else if (samples.Count - progressTick >= 90) Fail("Native route stalled for 90 ticks at " + phase + "; position=" + Vec(record.Resolution.Position));
                    }
                }
                catch (Exception error) { Fail("Committed tick " + record.Tick + " phase " + sample.Phase + ": " + error); }
            }

            private void ObserveMovementEvent(PlayerMovementSample value)
            {
                if (samples.Count == 0 || value.Tick != samples[samples.Count - 1].Record.Tick) { Fail("Unmatched movement event."); return; }
                var sample = samples[samples.Count - 1]; sample.MovementPublished = true;
                if (!value.Equals(sample.Movement)) Fail("Session changed the committed movement sample.");
            }

            private void ObserveTraversalEvent(PlayerTraversalFact value)
            {
                if (samples.Count == 0 || value.Tick != samples[samples.Count - 1].Record.Tick) { Fail("Unmatched traversal event."); return; }
                var sample = samples[samples.Count - 1]; sample.PublishedFacts.Add(value);
                if (!sample.Facts.Contains(value)) Fail("Session published an uncommitted traversal fact.");
            }

            private int Obstacle() => phase == Phase.Vault210 ? 210 : phase == Phase.Slide220 ? 220 : phase == Phase.Vault211 ? 211 : phase == Phase.Slide221 ? 221 : 212;
            private bool IsVault() => phase == Phase.Vault210 || phase == Phase.Vault211 || phase == Phase.Vault212;

            private Vector3 Waypoint()
            {
                switch (phase)
                {
                    case Phase.WestJoin: return new Vector3(-21f, 0f, 15f);
                    case Phase.ClutterEntry: return new Vector3(-18f, 0f, 15f);
                    case Phase.EastCorner: return new Vector3(27f, 0f, 15f);
                    case Phase.EastJoin: return new Vector3(27f, 0f, 11.5f);
                    case Phase.AtriumNorth: return new Vector3(27f, 0f, 8.5f);
                    case Phase.AtriumWest: return new Vector3(13.25f, 0f, 8.5f);
                    case Phase.RampApproach: return new Vector3(13.25f, 0f, -3f);
                    case Phase.RampFoot: return new Vector3(13.25f, 0.08f, -6f);
                    case Phase.RampRise: return new Vector3(27f, 4f, -6f);
                    case Phase.UpperDeck: return new Vector3(26f, 4f, 4f);
                    case Phase.BridgeEntry: return new Vector3(22f, 4f, 4f);
                    case Phase.BridgeRun: return new Vector3(11.2f, 4f, 4f);
                    default: return player.ReadOnlyState.Position;
                }
            }

            private void Advance(Phase next)
            {
                transitions.Add(samples[samples.Count - 1].Record.Tick + ":" + phase + "->" + next);
                phase = next; stageTicks = 0; jumpIssued = slideIssued = sawGateUnder = sawGateBlocked = false;
                progressTick = samples.Count; progressPosition = player.ReadOnlyState.Position;
            }

            public void AssertOutcome()
            {
                Assert.That(captures, Is.EqualTo(1)); Assert.That(metadata.SessionId, Is.Not.Null.And.Not.Empty);
                Assert.That(metadata.StartTick, Is.Zero); Assert.That(metadata.FixedDeltaTime, Is.EqualTo(Dt));
                Assert.That(metadata.SourceRevision, Is.Not.Null.And.Not.Empty); Assert.That(metadata.ConfigSnapshotHash, Is.Not.Null.And.Not.Empty);
                Assert.That(completedObstacles, Is.EqualTo(new[] { 210, 220, 211, 221, 212 }));
                Assert.That(samples.Sum(sample => sample.Facts.Count(fact => fact.Kind == TraversalKind.Vault)), Is.EqualTo(3));
                Assert.That(samples.Sum(sample => sample.Facts.Count(fact => fact.Kind == TraversalKind.Slide)), Is.EqualTo(2));
                Assert.That(samples.Sum(sample => sample.Facts.Count(fact => fact.Kind == TraversalKind.Jump || fact.Kind == TraversalKind.Mantle || fact.Kind == TraversalKind.Rebound)), Is.Zero);
                Assert.That(samples.All(sample => sample.MovementPublished && sample.PublishedFacts.SequenceEqual(sample.Facts)), Is.True);
                Assert.That(samples.Select(sample => sample.Record.Tick), Is.EqualTo(Enumerable.Range(1, samples.Count).Select(value => (long)value)));
                Assert.That(sawDropAir && sawDropLand && dropLandTick > dropAirTick, Is.True);
                Assert.That(samples.Last().Record.Resolution.Grounded && stopTicks >= 30, Is.True);
                Assert.That(Speed(samples.Last().StateVelocity), Is.LessThan(0.01f));
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(profileSnapshot));
                Assert.That(EditorJsonUtility.ToJson(mover), Is.EqualTo(moverSnapshot));
                Assert.That(AssetDatabase.GetAssetDependencyHash(ArenaPath).ToString(), Is.EqualTo(sceneHash));
                foreach (int id in markers.Keys)
                {
                    Assert.That(EditorJsonUtility.ToJson(markers[id]), Is.EqualTo(markerJson[id]));
                    Assert.That(markers[id].GetComponent<Collider>().bounds, Is.EqualTo(markerBounds[id]));
                }
                outcomeVerified = true;
            }

            public string Describe() => "Connected authored route: phase=" + phase + "; ticks=" + samples.Count + "/" + MaximumTicks +
                "; obstacles=" + string.Join(",", completedObstacles) + "; position=" + (player == null ? "unspawned" : Vec(player.ReadOnlyState.Position)) +
                "; originalCaptureSession=" + metadata.SessionId + "; outcomeVerified=" + outcomeVerified + "; diagnostic=" + reportPath;

            public void Fail(string message) { if (Failure.Length == 0) Failure = message; }

            private void Stamp(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                string absolute = Path.GetFullPath(path);
                using (var sha = SHA256.Create())
                    stamps.Add("SHA256=" + path + ":" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(absolute))).Replace("-", ""));
            }

            private void SaveReport()
            {
                if (!outcomeVerified && Failure.Length == 0) Failure = "Fixture assertion/setup interruption; inspect NUnit result. Route was not verified complete.";
                var rows = new StringBuilder();
                rows.AppendLine("# " + Describe());
                rows.AppendLine("# Incomplete diagnostic segment; no completed gameplay recording, population speed or participant claim.");
                rows.AppendLine("# OriginalCaptureStartedSessionId=" + metadata.SessionId + "; captures=" + captures + "; seed=" + metadata.Seed +
                    "; startTick=" + metadata.StartTick + "; lastObservedTick=" + (samples.Count == 0 ? 0 : samples.Last().Record.Tick) +
                    "; fixedDeltaTime=" + Num(metadata.FixedDeltaTime) + "; sourceRevision=" + metadata.SourceRevision +
                    "; captureConfigHash=" + metadata.ConfigSnapshotHash + "; randomConsumptionOrder=" + metadata.RandomConsumptionOrder);
                rows.AppendLine("# SceneDependencyHash=" + sceneHash + "; initialFactorySpawn=" + Vec(Spawn) + "; originalSceneSpawn=" + Vec(originalSpawn));
                rows.AppendLine("# Cutoffs=2400 fixed ticks; 480 ticks per phase; 90 consecutive ticks without 0.05m progress; 90 wall seconds after readiness.");
                rows.AppendLine("# WallSeconds=" + (Time.realtimeSinceStartupAsDouble - wallStart).ToString("R", CultureInfo.InvariantCulture) +
                    "; physicalFrames=" + physicalFrames + "; observedRunTick=" + (run == null ? -1 : run.Tick));
                rows.AppendLine("# Failure=" + Clean(Failure));
                rows.AppendLine("# PhaseTransitions=" + string.Join(";", transitions));
                rows.AppendLine("# PlayerProfile=" + profileSnapshot); rows.AppendLine("# PlayerMoverDriverConfig=" + moverSnapshot);
                foreach (string stamp in stamps) rows.AppendLine("# " + stamp);
                foreach (int id in markerJson.Keys) rows.AppendLine("# Marker=" + id + "; " + markerJson[id]);
                rows.AppendLine("tick\tphase\tdt\tstate\tlockSeconds\tx\ty\tz\tvx\tvy\tvz\tstateVx\tstateVy\tstateVz\theading\tlookBack\theadLookX\theadLookY\tcapsuleHeight\tpenetration\tmoveX\tmoveY\tlookX\tlookY\theld\tpressed\treleased\tprobeGround\tgroundNormal\twallDetected\twallId\twallDistance\twallNormal\twallAngle\tvaultCandidate\tvaultHeight\tvaultClearance\tvaultTarget\tstandingBlocked\tresolvedGround\tceiling\teyePosition\tmovementPublished\tfacts\tpublishedFacts\tfailureAtObservation\tsuppliedInput");
                foreach (var sample in samples)
                {
                    var record = sample.Record; var p = record.Resolution.Position; var v = record.Resolution.Velocity;
                    var probe = record.Probe; var movement = sample.Movement; var frame = record.Input;
                    object[] values = { record.Tick, sample.Phase, record.DeltaTime, movement.MovementState, movement.InputLockSeconds,
                        p.x, p.y, p.z, v.x, v.y, v.z, sample.StateVelocity.x, sample.StateVelocity.y, sample.StateVelocity.z,
                        movement.HeadingDegrees, movement.LookBack, movement.HeadLookDelta.x, movement.HeadLookDelta.y,
                        sample.CapsuleHeight, sample.Penetration, frame.Move.x, frame.Move.y, frame.LookDelta.x, frame.LookDelta.y,
                        frame.Held, frame.Pressed, frame.Released, probe.Grounded, Vec(probe.GroundNormal), probe.WallDetected,
                        probe.WallId, probe.WallDistance, Vec(probe.WallNormal), probe.WallAngleDegrees, probe.VaultCandidate,
                        probe.VaultHeight, probe.VaultClearance, Vec(probe.VaultTarget), probe.StandingBlocked,
                        record.Resolution.Grounded, record.Resolution.Ceiling, Vec(record.Resolution.EyePosition), sample.MovementPublished,
                        Facts(sample.Facts), Facts(sample.PublishedFacts), Clean(sample.FailureAtObservation), Frame(sample.Supplied) };
                    rows.AppendLine(string.Join("\t", values.Select(value => value is float number ? Num(number) : Convert.ToString(value, CultureInfo.InvariantCulture))));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, rows.ToString());
                var diagnostic = new InputReplayDriverState { Metadata = metadata, CaptureComplete = false, CaptureInterrupted = true,
                    EndTick = samples.Count == 0 ? metadata.StartTick : samples.Last().Record.Tick };
                diagnostic.Recorded.AddRange(samples.Select(sample => sample.Record));
                string binaryPath = Path.ChangeExtension(reportPath, ".incomplete.winput");
                File.WriteAllBytes(binaryPath, new InputRecordingPresenter().Encode(diagnostic));
                TestContext.WriteLine("Connected-route full tick TSV: " + reportPath);
                TestContext.WriteLine("Connected-route exact input/probe/resolution diagnostic (incomplete): " + binaryPath);
                TestContext.WriteLine(Describe());
            }

            public void Dispose()
            {
                TagArenaSceneRoot.SceneReady -= ObserveReady;
                if (captureRun != null) captureRun.CaptureStarted -= ObserveCaptureStarted;
                if (input != null) input.FramePublished -= SupplyInput;
                if (run != null)
                {
                    run.PlayerProbeRecorded -= Observe; run.PlayerMovementPublished -= ObserveMovementEvent;
                    run.PlayerTraversalPublished -= ObserveTraversalEvent; run.ReceiveInput(default);
                }
                try { SaveReport(); }
                finally
                {
                    if (gameplay != null && restoreDevices) gameplay.devices = previousDevices;
                    if (root != null && restoreSpawn) Field(typeof(TagArenaSceneRoot), "_spawnPosition").SetValue(root, originalSpawn);
                    foreach (var hunter in disabledHunters) if (hunter != null) hunter.SetActive(true);
                }
            }
        }

        private static string Facts(IEnumerable<PlayerTraversalFact> facts) => string.Join(";", facts.Select(fact =>
            fact.Id + ":" + fact.Tick + ":" + fact.Kind + ":" + fact.Succeeded + ":" + Vec(fact.Direction) + ":" + Num(fact.Duration)));
        private static string Frame(InputFrame frame) => Num(frame.Move.x) + "," + Num(frame.Move.y) + ";" +
            Num(frame.LookDelta.x) + "," + Num(frame.LookDelta.y) + ";" + frame.Held + ";" + frame.Pressed + ";" + frame.Released;
        private static string Num(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Vec(Vector3 value) => Num(value.x) + "," + Num(value.y) + "," + Num(value.z);
        private static string Clean(string value) => (value ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        private static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
        private static float Speed(Vector3 value) => new Vector2(value.x, value.z).magnitude;
        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing runtime fixture wiring: " + type.Name + "." + name);
        private static T One<T>() where T : Component
        {
            var items = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(items.Length, Is.EqualTo(1), typeof(T).Name + " requires one active instance.");
            return items[0];
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
