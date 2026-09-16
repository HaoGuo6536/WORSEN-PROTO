// ============================================================================
// ReverseVaultIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Checks both approach directions of the authored bidirectional Player route's
//   three waist vaults with real casts, normal Run input and resolved landings.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level physical integration.
// KEY RESPONSIBILITIES:
//   - Compare west/east approaches to unchanged markers 210, 211 and 212.
//   - Require the resolved capsule to land beyond the opposite collider face.
// DEPENDENCIES:
//   - Core, Player/Level/Hunter, Run/Input and authored TagArena scene wiring.
//   - Unity physics, Input System, UnityEditor, NUnit and Test Framework.
// USAGE NOTES:
//   Coordinator owns publication and Unity lease. Thin entry iterators create all
//   captured state after EnterPlayMode reload. Each marker gets a fresh scene;
//   sceneLoaded changes only the runtime spawn request before the real factory.
//   Facing uses Run look input; no live-pose reset, fake probe, tuning or geometry.
//   The owned gameplay action map's device filter is copied/restored, buffered
//   hardware edges are cleared through Live source, and physical frames must be
//   neutral. Hunters are disabled before tick one for this free-traversal trial.
//   Subscriptions/background setting are restored; Test Framework restores scenes.
//   This checks authored bidirectional movement, not chased-route balance or replay.
// ============================================================================
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class ReverseVaultIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";
        private const float Dt = 1f / 60f;

        [UnityTest]
        public IEnumerator AuthoredBidirectionalVaultsLandBeyondTheirEastFaceFromWest()
        {
            yield return new EnterPlayMode();
            yield return ExerciseDirection(1);
        }

        [UnityTest]
        public IEnumerator AuthoredBidirectionalVaultsLandBeyondTheirWestFaceFromEast()
        {
            yield return new EnterPlayMode();
            yield return ExerciseDirection(-1);
        }

        private static IEnumerator ExerciseDirection(int direction)
        {
            bool previousBackground = Application.runInBackground;
            Hash128 sceneHash = AssetDatabase.GetAssetDependencyHash(ArenaPath);
            try
            {
                Application.runInBackground = true;
                Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty);
                foreach (int markerId in new[] { 210, 211, 212 })
                {
                    using (var trial = new Trial(markerId, direction))
                    {
                        SceneManager.sceneLoaded += trial.ArrangeSpawn;
                        try
                        {
                            var load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
                            Assert.That(load, Is.Not.Null);
                            yield return Until(() => trial.Ready || trial.Failure.Length > 0, 20f);
                            Assert.That(trial.Failure, Is.Empty, trial.Describe());
                            Assert.That(load.isDone && trial.Ready, Is.True, "Scene readiness: " + trial.Describe());
                            yield return Until(() => trial.Done || trial.Failure.Length > 0, 15f);
                            Assert.That(trial.Failure, Is.Empty, trial.Describe());
                            Assert.That(trial.Done, Is.True, "Run tick timeout: " + trial.Describe());
                            Assert.That(AssetDatabase.GetAssetDependencyHash(ArenaPath), Is.EqualTo(sceneHash));
                            trial.AssertOutcome();
                        }
                        finally { SceneManager.sceneLoaded -= trial.ArrangeSpawn; }
                    }
                }
            }
            finally { Application.runInBackground = previousBackground; }
        }

        private sealed class Trial : IDisposable
        {
            private readonly int markerId, direction;
            private LevelMarker marker;
            private Collider obstacle;
            private Bounds originalBounds;
            private Vector3 spawn, offeredTarget, outcome;
            private string markerSnapshot, profileSnapshot, moverSnapshot;
            private PlayerProfile profile;
            private PlayerMoverDriverConfig mover;
            private PlayerManager player;
            private CapsuleCollider capsule;
            private RunSessionManager run;
            private InputManager input;
            private InputActionMap gameplay;
            private ReadOnlyArray<InputDevice>? previousDevices;
            private bool deviceFilterSaved, sawTraversal, penetrated, excessiveHorizontalStep, jumpIssued;
            private int observed, neutralFrames, successes, failures, jumpTick;
            private InputButtons previousHeld;
            private Vector3 previousPosition;
            private InputFrame supplied;
            public bool Ready;
            public string Failure = "";
            public bool Done => observed >= 90;

            public Trial(int markerId, int direction) { this.markerId = markerId; this.direction = direction; }

            public void ArrangeSpawn(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != ArenaPath) return;
                try
                {
                    var root = One<TagArenaSceneRoot>();
                    marker = UnityEngine.Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None)
                        .Single(item => item.SurfaceId == markerId);
                    obstacle = marker.GetComponent<Collider>();
                    Assert.That(obstacle, Is.Not.Null);
                    Assert.That(marker.Kind, Is.EqualTo(TraversalSurfaceKind.Vault));
                    originalBounds = obstacle.bounds;
                    Assert.That(Vector3.Distance(originalBounds.size, new Vector3(0.8f, 1f, 4f)), Is.LessThan(0.001f));
                    spawn = new Vector3(originalBounds.center.x - direction * 1.4f, 0.05f, originalBounds.center.z);
                    Field(typeof(TagArenaSceneRoot), "_spawnPosition").SetValue(root, spawn);
                    markerSnapshot = EditorJsonUtility.ToJson(marker);
                    profile = (PlayerProfile)new SerializedObject(root).FindProperty("_playerProfile").objectReferenceValue;
                    Assert.That(profile, Is.Not.Null);
                    profileSnapshot = EditorJsonUtility.ToJson(profile);
                    TagArenaSceneRoot.SceneReady += ObserveReady;
                }
                catch (Exception error) { Fail("Spawn arrangement: " + error); }
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
                    var route = One<LevelManager>().ReadOnlyState.Graph.Edges.Single(edge => edge.Id == 105);
                    Assert.That(route.Bidirectional, Is.True, "The authored route must retain its two directions.");
                    Assert.That(route.Access, Is.EqualTo(TraversalAccess.Player));
                    foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                        hunter.gameObject.SetActive(false);
                    Assert.That(HunterRegistry.Items.Count, Is.Zero);
                    capsule = player.GetComponent<CapsuleCollider>();
                    mover = (PlayerMoverDriverConfig)new SerializedObject(player.GetComponent<PlayerDriver>())
                        .FindProperty("_config").objectReferenceValue;
                    Assert.That(mover, Is.Not.Null); Assert.That(capsule, Is.Not.Null);
                    moverSnapshot = EditorJsonUtility.ToJson(mover);
                    gameplay = (InputActionMap)Field(typeof(PlayerInputDriver), "_actions")
                        .GetValue(input.GetComponent<PlayerInputDriver>());
                    Assert.That(gameplay, Is.Not.Null);
                    previousDevices = gameplay.devices.HasValue
                        ? new ReadOnlyArray<InputDevice>(gameplay.devices.Value.ToArray()) : (ReadOnlyArray<InputDevice>?)null;
                    deviceFilterSaved = true;
                    gameplay.devices = Array.Empty<InputDevice>();
                    foreach (InputAction action in gameplay.actions) Assert.That(action.controls.Count, Is.Zero);
                    Assert.That(input.SetSource(InputSource.Live), Is.True);
                    previousPosition = spawn;
                    input.FramePublished += SupplyInput;
                    run.PlayerProbeRecorded += Observe;
                    Ready = true;
                }
                catch (Exception error) { Fail("Readiness: " + error); }
            }

            private void SupplyInput(InputFrame physical)
            {
                if (Done || Failure.Length > 0) { run.ReceiveInput(default); return; }
                try
                {
                    Assert.That(physical.Equals(default(InputFrame)), Is.True, "Physical producer was not neutral.");
                    neutralFrames++;
                    float heading = direction > 0 ? 90f : 270f;
                    Vector2 move = Vector2.zero;
                    InputButtons held = InputButtons.None;
                    if (observed >= 10 && !jumpIssued)
                    {
                        // Approach within the default duration/speed envelope instead
                        // of asking a stationary vault to cover the full probe range.
                        if (direction * (originalBounds.center.x - player.ReadOnlyState.Position.x) > 1f)
                        { move = Vector2.up; held = InputButtons.None; }
                        else { held = InputButtons.Jump; jumpIssued = true; jumpTick = observed + 1; }
                    }
                    supplied = new InputFrame(move,
                        new Vector2(Mathf.DeltaAngle(player.ReadOnlyState.HeadingDegrees, heading), 0f),
                        held, held & ~previousHeld, previousHeld & ~held);
                    previousHeld = held;
                    run.ReceiveInput(supplied);
                }
                catch (Exception error) { Fail("Input: " + error); run.ReceiveInput(default); }
            }

            private void Observe(InputProbeRecord record)
            {
                if (Done || Failure.Length > 0) return;
                try
                {
                    observed++;
                    Assert.That(neutralFrames, Is.EqualTo(observed));
                    Assert.That(record.Tick, Is.EqualTo(observed));
                    Assert.That(record.Input, Is.EqualTo(supplied));
                    Assert.That(record.DeltaTime, Is.EqualTo(Dt).Within(0.000001f));
                    Assert.That(record.Resolution.Present && player.ReadOnlyState.IsAlive, Is.True);
                    Vector3 position = record.Resolution.Position;
                    excessiveHorizontalStep |= Mathf.Abs(position.x - previousPosition.x) > profile.MaxDesignSpeed * record.DeltaTime + 0.005f;
                    previousPosition = position;
                    Assert.That(Mathf.Abs(position.z - originalBounds.center.z), Is.LessThan(0.05f), "The capsule left the straight vault lane.");
                    if (observed == 10 || (record.Input.Pressed & InputButtons.Jump) != 0)
                    {
                        Assert.That(record.Probe.Grounded && record.Probe.VaultCandidate, Is.True, "Real forward cast missed the authored vault.");
                        Assert.That(record.Probe.VaultHeight, Is.EqualTo(1f).Within(0.03f));
                        Assert.That(record.Probe.VaultClearance, Is.GreaterThan(0f));
                        offeredTarget = record.Probe.VaultTarget;
                    }
                    sawTraversal |= player.LastMovementSample.MovementState == MovementState.Vault;
                    penetrated |= Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth) && depth > mover.SkinWidth + 0.01f;
                    foreach (var fact in player.LastTraversalFacts.Where(item => item.Kind == TraversalKind.Vault))
                    {
                        if (fact.Succeeded) successes++; else failures++;
                        outcome = position;
                    }
                }
                catch (Exception error) { Fail("Observed tick: " + error); }
            }

            public void AssertOutcome()
            {
                Assert.That(obstacle.bounds, Is.EqualTo(originalBounds));
                Assert.That(EditorJsonUtility.ToJson(marker), Is.EqualTo(markerSnapshot));
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(profileSnapshot));
                Assert.That(EditorJsonUtility.ToJson(mover), Is.EqualTo(moverSnapshot));
                Assert.That(successes, Is.EqualTo(1), Describe());
                Assert.That(failures, Is.Zero, Describe());
                Assert.That(sawTraversal, Is.True, Describe());
                Assert.That(penetrated || excessiveHorizontalStep, Is.False, Describe());
                Assert.That(direction * (outcome.x - originalBounds.center.x),
                    Is.GreaterThan(originalBounds.extents.x + capsule.radius + 0.05f), "Vault fact landed on the approach side. " + Describe());
                Assert.That(direction * (player.ReadOnlyState.Position.x - originalBounds.center.x),
                    Is.GreaterThan(originalBounds.extents.x + capsule.radius + 0.05f), "Capsule did not finish beyond the opposite face. " + Describe());
                Assert.That(player.LastProbeRecord.Resolution.Grounded, Is.True, Describe());
                Assert.That(player.ReadOnlyState.Position.y, Is.EqualTo(0f).Within(0.06f), Describe());
                TestContext.Progress.WriteLine(Describe());
            }

            public string Describe() => "marker=" + markerId + "; direction=" + direction + "; ticks=" + observed +
                "; spawn=" + spawn + "; jump tick=" + jumpTick + "; offered target=" + offeredTarget + "; resolved outcome=" + outcome +
                "; final=" + (player != null ? player.ReadOnlyState.Position.ToString() : "unavailable") +
                "; success/failure=" + successes + "/" + failures + "; traversal=" + sawTraversal +
                "; penetration=" + penetrated + "; excess horizontal step=" + excessiveHorizontalStep;
            private void Fail(string message) { if (Failure.Length == 0) Failure = message; }

            public void Dispose()
            {
                TagArenaSceneRoot.SceneReady -= ObserveReady;
                if (input != null) input.FramePublished -= SupplyInput;
                if (run != null) { run.PlayerProbeRecorded -= Observe; run.ReceiveInput(default); }
                if (gameplay != null && deviceFilterSaved) gameplay.devices = previousDevices;
            }
        }

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
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
        [UnityTearDown] public IEnumerator RestoreEditor() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
