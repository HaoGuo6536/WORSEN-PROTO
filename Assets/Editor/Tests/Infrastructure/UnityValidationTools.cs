// ============================================================================
// UnityValidationTools.cs
// ============================================================================
//
// PURPOSE:
//   Captures editor and gameplay observations through compiled APIs when a remote
//   evaluator cannot inspect the editor reliably. Persists complete test results
//   across domain reloads without relying on static counters or changing tests.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test validation (§11) · Infrastructure.
//
// KEY RESPONSIBILITIES:
//   - Write configured state snapshots and complete NUnit result evidence.
//   - Re-register passive test callbacks after reload and pair their cleanup.
//   - Constrain every output to this project's Logs/AgentValidation directory.
//
// DEPENDENCIES:
//   - UnityEditor and installed TestRunner API; inspected internal IsRunActive
//     method is read through reflection and reports unknown if unavailable.
//   - Player Registry/read-only state, Run Session, Input and Telemetry observers.
//
// USAGE NOTES:
//   Editor-only; never starts tests, changes test semantics, saves assets or
//   mutates scene objects. Configure Logs/AgentValidation/active-output.txt with
//   an absolute or project-relative output directory before importing or using
//   Worsen/Validation/Capture State. Missing/invalid configuration is a no-op.
//   Test observers report IO failures to observer-errors.log, never Unity's log.
//   Full result-tree leaf counts are authoritative; callback journals are partial
//   progress evidence only. Short result filenames retain full run identity in
//   metadata; JSON summaries report XML export failures explicitly and survive
//   those failures. Engine integration tests still require Unity.
//   Installed Test Framework 1.6.0 creates UTC result timestamps, but its OADate
//   reload serialization drops DateTime.Kind. Restore that UTC interpretation
//   only for result timestamps; retain supplied UTC and convert explicit Local.
//   OADate's millisecond precision and native result start/duration are unchanged.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Domain.Player;
using Worsen.Presentation.Input;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;

namespace Worsen.Tests.Infrastructure
{
    [InitializeOnLoad]
    public static class UnityValidationTools
    {
        private static readonly ResultObserver Observer = new ResultObserver();
        private static bool registered;
        private static bool watching;
        private static double nextConfigurationCheck;
        private static string lastObserverError = "";

        static UnityValidationTools() { RefreshRegistration(); }

        [MenuItem("Worsen/Validation/Capture State")]
        public static void CaptureState()
        {
            RefreshRegistration();
            Observe(directory => WriteState(directory));
        }

        private static void RefreshRegistration()
        {
            if (!TryOutputDirectory(out _)) { StopObserving(); return; }
            if (!registered)
            {
                try { TestRunnerApi.RegisterTestCallback(Observer); registered = true; }
                catch (Exception error) { RecordError(error); }
            }
            if (watching) return;
            AssemblyReloadEvents.beforeAssemblyReload += StopObserving;
            EditorApplication.update += CheckConfiguration;
            watching = true;
        }

        private static void CheckConfiguration()
        {
            if (EditorApplication.timeSinceStartup < nextConfigurationCheck) return;
            nextConfigurationCheck = EditorApplication.timeSinceStartup + 1d;
            RefreshRegistration();
        }

        private static void StopObserving()
        {
            if (registered)
            {
                try { TestRunnerApi.UnregisterTestCallback(Observer); }
                catch (Exception error) { RecordError(error); }
                registered = false;
            }
            AssemblyReloadEvents.beforeAssemblyReload -= StopObserving;
            EditorApplication.update -= CheckConfiguration;
            watching = false;
        }

        private static bool TryOutputDirectory(out string directory)
        {
            directory = null;
            try
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string allowed = Path.GetFullPath(Path.Combine(project, "Logs", "AgentValidation"));
                string configuration = Path.Combine(allowed, "active-output.txt");
                if (!File.Exists(configuration)) return false;
                string configured = File.ReadAllText(configuration).Trim();
                if (configured.Length == 0) return false;
                string resolved = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(project, configured));
                string prefix = allowed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.Equals(allowed, StringComparison.OrdinalIgnoreCase) && !resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                // Reject junction/symlink ancestors: lexical containment must not
                // permit writing through a reparse point outside the repository.
                for (var ancestor = new DirectoryInfo(resolved); ancestor != null; ancestor = ancestor.Parent)
                {
                    if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                    if (ancestor.FullName.Equals(project, StringComparison.OrdinalIgnoreCase)) break;
                }
                directory = resolved;
                return true;
            }
            catch (Exception) { return false; }
        }

        private static void Observe(Action<string> action)
        {
            if (!TryOutputDirectory(out string directory)) return;
            try { Directory.CreateDirectory(directory); action(directory); }
            catch (Exception error) { RecordError(error); }
        }

        private static void RecordError(Exception error)
        {
            lastObserverError = error.ToString();
            if (!TryOutputDirectory(out string directory)) return;
            try
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "observer-errors.log"), DateTime.UtcNow.ToString("o") + " " + lastObserverError + Environment.NewLine);
            }
            catch (Exception) { /* Observation failure must never change test outcomes. */ }
        }

        private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N");
        private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonUtility.ToJson(value, true));

        private static void WriteState(string directory)
        {
            var snapshot = new EditorSnapshot {
                capturedUtc = DateTime.UtcNow.ToString("o"), dataPath = Application.dataPath,
                projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")), unityVersion = Application.unityVersion,
                processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                activeScene = SceneManager.GetActiveScene().path,
                isPlaying = EditorApplication.isPlaying,
                isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                isCompiling = EditorApplication.isCompiling, isUpdating = EditorApplication.isUpdating,
                isBuildingPlayer = BuildPipeline.isBuildingPlayer, callbacksRegistered = registered,
                lastObserverError = lastObserverError
            };
            try
            {
                MethodInfo method = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(bool)) throw new MissingMethodException("TestRunnerApi.IsRunActive is unavailable.");
                snapshot.testRunActive = (bool)method.Invoke(null, null);
                snapshot.testRunStateKnown = true;
            }
            catch (Exception error) { snapshot.testRunStateError = error.GetType().Name + ": " + error.Message; }

            var scenes = new Dictionary<int, Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++) { Scene scene = SceneManager.GetSceneAt(i); scenes[scene.handle] = scene; }
            foreach (GameObject root in Resources.FindObjectsOfTypeAll<GameObject>().Where(item => item != null && item.transform.parent == null && item.scene.IsValid() && item.scene.isLoaded && !EditorUtility.IsPersistent(item)))
            {
                scenes[root.scene.handle] = root.scene;
                Component[] components = root.GetComponentsInChildren<Component>(true);
                snapshot.roots.Add(new RootSnapshot {
                    sceneName = root.scene.name, scenePath = root.scene.path, name = root.name, instanceId = root.GetInstanceID(),
                    active = root.activeInHierarchy, position = root.transform.position,
                    componentCount = components.Length,
                    componentTypes = components.GroupBy(item => item == null ? "<missing script>" : item.GetType().FullName)
                        .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new ComponentCount { typeName = group.Key, count = group.Count() }).ToList()
                });
            }
            foreach (Scene scene in scenes.Values.OrderBy(item => (int)item.handle))
            {
                var value = new SceneSnapshot { name = scene.name, path = scene.path, handle = scene.handle, isLoaded = scene.isLoaded, isDirty = scene.isDirty, rootCount = scene.isLoaded ? scene.rootCount : 0 };
                snapshot.scenes.Add(value);
                if (scene.isDirty) snapshot.dirtyScenes.Add(value);
            }
            snapshot.dirtyAssetPaths = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(item => item != null && EditorUtility.IsPersistent(item) && EditorUtility.IsDirty(item))
                .Select(AssetDatabase.GetAssetPath).Where(path => !string.IsNullOrEmpty(path)).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToList();
            snapshot.playerRegistryCount = PlayerRegistry.Items.Count;
            foreach (PlayerManager player in PlayerRegistry.Items)
            {
                if (player == null) { snapshot.nullPlayerRegistryEntries++; continue; }
                var sample = player.LastMovementSample;
                var state = player.ReadOnlyState;
                snapshot.players.Add(new PlayerSnapshot {
                    name = player.name, entityId = player.Id.Value, initialized = state != null,
                    committedTick = sample.Tick, committedPosition = sample.Position, committedVelocity = sample.Velocity,
                    committedEyePosition = sample.EyePosition, committedHeadingDegrees = sample.HeadingDegrees,
                    horizontalSpeed = new Vector2(sample.Velocity.x, sample.Velocity.z).magnitude,
                    movementState = sample.MovementState.ToString(), stateTick = state == null ? 0 : state.Tick,
                    health = state == null ? 0f : state.Health, maxHealth = state == null ? 0f : state.MaxHealth,
                    alive = state != null && state.IsAlive, transformPosition = player.transform.position
                });
            }
            RunSessionManager session = RunSessionManager.Instance;
            snapshot.session = new SessionSnapshot { available = session != null, tick = session == null ? 0 : session.Tick,
                phase = session == null ? "unavailable" : session.Phase.ToString(), seed = session == null ? 0 : session.Seed, elapsedSeconds = session == null ? 0d : session.ElapsedSeconds };
            InputManager input = InputManager.Instance;
            snapshot.input = new ServiceSnapshot { available = input != null, mode = input == null ? "unavailable" : input.Source.ToString(),
                error = input == null ? "InputManager unavailable." : input.LastRecordingError, outputPath = input == null ? "" : input.LastRecordingPath };
            TelemetryManager telemetry = TelemetryManager.Instance;
            snapshot.telemetry = new ServiceSnapshot { available = telemetry != null, error = telemetry == null ? "TelemetryManager unavailable." : telemetry.LastError,
                outputPath = telemetry == null ? "" : telemetry.LastOutputPath };
            foreach (UnityEngine.Camera camera in Resources.FindObjectsOfTypeAll<UnityEngine.Camera>().Where(item => item != null && item.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(item)))
                snapshot.cameras.Add(new CameraSnapshot { name = camera.name, scene = camera.gameObject.scene.name, enabled = camera.enabled,
                    active = camera.gameObject.activeInHierarchy, position = camera.transform.position, rotation = camera.transform.rotation,
                    fieldOfView = camera.fieldOfView, nearClip = camera.nearClipPlane, farClip = camera.farClipPlane });
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(Path.Combine(directory, "state-" + Stamp() + ".json"), json);
            File.WriteAllText(Path.Combine(directory, "latest-state.json"), json);
        }

        private static RunMarker ReadMarker(string directory)
        {
            string path = Path.Combine(directory, "run-active.json");
            if (File.Exists(path))
            {
                RunMarker existing = JsonUtility.FromJson<RunMarker>(File.ReadAllText(path));
                if (existing != null && existing.status == "running" && !string.IsNullOrEmpty(existing.runId) && existing.runId.All(c => char.IsLetterOrDigit(c) || c == '-')) return existing;
            }
            var recovered = new RunMarker { runId = Stamp(), startedUtc = DateTime.UtcNow.ToString("o"), status = "running", recoveredWithoutRunStarted = true };
            WriteJson(path, recovered);
            return recovered;
        }

        private static string ResultTimestampUtc(DateTime timestamp)
        {
            // Test Framework 1.6.0 UnityWorkItem creates UTC times. Its reload
            // serializers use FromOADate, preserving UTC clock fields but losing
            // Kind. ToUniversalTime would otherwise apply the local offset again.
            DateTime utc = timestamp.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
                : timestamp.ToUniversalTime();
            return utc.ToString("o");
        }

        private static LeafResult Leaf(ITestResultAdaptor result) => new LeafResult {
            id = result.Test == null ? "" : result.Test.Id, fullName = result.FullName, status = result.TestStatus.ToString(),
            resultState = result.ResultState, duration = result.Duration, asserts = result.AssertCount,
            message = result.Message, stackTrace = result.StackTrace, output = result.Output,
            startedUtc = ResultTimestampUtc(result.StartTime), endedUtc = ResultTimestampUtc(result.EndTime)
        };

        private static void CollectLeaves(ITestResultAdaptor result, List<LeafResult> leaves)
        {
            if (result.Test != null && !result.Test.IsSuite) { leaves.Add(Leaf(result)); return; }
            if (result.HasChildren) foreach (ITestResultAdaptor child in result.Children) CollectLeaves(child, leaves);
        }

        private sealed class ResultObserver : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) => Observe(directory => WriteJson(Path.Combine(directory, "run-active.json"),
                new RunMarker { runId = Stamp(), startedUtc = DateTime.UtcNow.ToString("o"), status = "running", rootName = testsToRun.FullName, expectedCases = testsToRun.TestCaseCount }));
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test == null || result.Test.IsSuite) return;
                Observe(directory => {
                    RunMarker marker = ReadMarker(directory);
                    File.AppendAllText(Path.Combine(directory, "run-" + marker.runId + "-callbacks.jsonl"), JsonUtility.ToJson(Leaf(result)) + Environment.NewLine);
                });
            }
            public void RunFinished(ITestResultAdaptor result) => Observe(directory => {
                RunMarker marker = ReadMarker(directory);
                string prefix = "result-" + Guid.NewGuid().ToString("N");
                string xmlPath = Path.Combine(directory, prefix + ".xml");
                var leaves = new List<LeafResult>();
                CollectLeaves(result, leaves);
                string xmlStatus = "saved", xmlError = "";
                try
                {
                    TestRunnerApi.SaveResultToFile(result, xmlPath);
                    if (!File.Exists(xmlPath) || new FileInfo(xmlPath).Length == 0)
                        throw new IOException("TestRunnerApi did not write the requested NUnit XML: " + xmlPath);
                }
                catch (Exception error)
                {
                    xmlStatus = "failed";
                    xmlError = error.ToString();
                    RecordError(error);
                }
                var summary = new RunSummary {
                    runId = marker.runId, rootName = result.FullName, startedUtc = ResultTimestampUtc(result.StartTime),
                    endedUtc = ResultTimestampUtc(result.EndTime), savedUtc = DateTime.UtcNow.ToString("o"),
                    resultState = result.ResultState, duration = result.Duration, xmlPath = xmlPath, xmlStatus = xmlStatus, xmlError = xmlError,
                    total = leaves.Count, passed = leaves.Count(item => item.status == "Passed"), failed = leaves.Count(item => item.status == "Failed"),
                    skipped = leaves.Count(item => item.status == "Skipped"), inconclusive = leaves.Count(item => item.status == "Inconclusive"),
                    reportedPassed = result.PassCount, reportedFailed = result.FailCount, reportedSkipped = result.SkipCount, reportedInconclusive = result.InconclusiveCount,
                    recoveredWithoutRunStarted = marker.recoveredWithoutRunStarted, leaves = leaves
                };
                summary.other = summary.total - summary.passed - summary.failed - summary.skipped - summary.inconclusive;
                summary.aggregateCountsMatch = summary.passed == result.PassCount && summary.failed == result.FailCount && summary.skipped == result.SkipCount && summary.inconclusive == result.InconclusiveCount;
                WriteJson(Path.Combine(directory, prefix + ".json"), summary);
                WriteJson(Path.Combine(directory, "latest-test-summary.json"), summary);
                File.WriteAllLines(Path.Combine(directory, prefix + "-leaves.jsonl"), leaves.Select(item => JsonUtility.ToJson(item)));
                marker.status = "finished"; marker.endedUtc = summary.endedUtc;
                WriteJson(Path.Combine(directory, "run-active.json"), marker);
                WriteState(directory);
            });
        }

        [Serializable] private sealed class EditorSnapshot {
            public int schemaVersion = 1, processId, playerRegistryCount, nullPlayerRegistryEntries;
            public string capturedUtc, projectPath, dataPath, unityVersion, activeScene, testRunStateError = "", lastObserverError;
            public bool isPlaying, isPlayingOrWillChangePlaymode, isCompiling, isUpdating, isBuildingPlayer, testRunStateKnown, testRunActive, callbacksRegistered;
            public List<SceneSnapshot> scenes = new List<SceneSnapshot>(), dirtyScenes = new List<SceneSnapshot>();
            public List<string> dirtyAssetPaths = new List<string>();
            public List<RootSnapshot> roots = new List<RootSnapshot>();
            public List<PlayerSnapshot> players = new List<PlayerSnapshot>();
            public List<CameraSnapshot> cameras = new List<CameraSnapshot>();
            public SessionSnapshot session; public ServiceSnapshot input, telemetry;
        }
        [Serializable] private sealed class SceneSnapshot { public string name, path; public int handle, rootCount; public bool isLoaded, isDirty; }
        [Serializable] private sealed class ComponentCount { public string typeName; public int count; }
        [Serializable] private sealed class RootSnapshot { public string name, sceneName, scenePath; public int instanceId, componentCount; public bool active; public Vector3 position; public List<ComponentCount> componentTypes; }
        [Serializable] private sealed class PlayerSnapshot { public string name, movementState; public int entityId; public long committedTick, stateTick; public bool initialized, alive; public Vector3 committedPosition, committedVelocity, committedEyePosition, transformPosition; public float committedHeadingDegrees, horizontalSpeed, health, maxHealth; }
        [Serializable] private sealed class SessionSnapshot { public bool available; public long tick; public string phase; public int seed; public double elapsedSeconds; }
        [Serializable] private sealed class ServiceSnapshot { public bool available; public string mode, error, outputPath; }
        [Serializable] private sealed class CameraSnapshot { public string name, scene; public bool enabled, active; public Vector3 position; public Quaternion rotation; public float fieldOfView, nearClip, farClip; }
        [Serializable] private sealed class RunMarker { public string runId, startedUtc, endedUtc, status, rootName; public int expectedCases; public bool recoveredWithoutRunStarted; }
        [Serializable] private sealed class LeafResult { public string id, fullName, status, resultState, message, stackTrace, output, startedUtc, endedUtc; public double duration; public int asserts; }
        [Serializable] private sealed class RunSummary { public string runId, rootName, startedUtc, endedUtc, savedUtc, resultState, xmlPath, xmlStatus, xmlError; public double duration; public int total, passed, failed, skipped, inconclusive, other, reportedPassed, reportedFailed, reportedSkipped, reportedInconclusive; public bool aggregateCountsMatch, recoveredWithoutRunStarted; public List<LeafResult> leaves; }
    }
}
