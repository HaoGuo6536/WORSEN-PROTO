// ============================================================================
// NativeTestRunnerSetup.cs
// ============================================================================
// PURPOSE:
//   Starts one asynchronous native Edit Mode run for a coordinator-supplied list
//   of fixtures. Publishes run-specific NUnit XML and a completion marker so a
//   gate never mistakes an earlier or partially written report for this run.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Testing. No runtime gameplay responsibility.
// KEY RESPONSIBILITIES:
//   - Validate requests and configure one TestRunnerApi filter without vendor code.
//   - Use the existing reload-safe validation observer to serialize native results.
//   - Publish atomic completion evidence and restore the previous observer output.
// DEPENDENCIES:
//   UnityEditor; installed TestRunnerApi and Worsen.Tests UnityValidationTools via
//   checked reflection, preserving the existing assembly reference boundaries.
// USAGE NOTES:
//   Coordinator only, under the exclusive Unity lease. Writes only beneath this
//   project's Logs/AgentValidation. SessionState survives Play Mode reloads; an
//   interrupted run never publishes completion. The observer remains the sole
//   NUnit serializer. No tests, assets, scenes or vendor sources are modified.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Worsen.Editor.Testing
{
    [InitializeOnLoad]
    public static class NativeTestRunnerSetup
    {
        private const string PendingKey = "Worsen.Testing.PendingRequest";
        private const string PreviousKey = "Worsen.Testing.PreviousOutput";
        private const string HadPreviousKey = "Worsen.Testing.HadPreviousOutput";
        private static double nextPoll;

        static NativeTestRunnerSetup()
        {
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += StopObserving;
        }

        public static string Run(string requestPath)
        {
            requestPath = Confine(requestPath);
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if (request == null || !Regex.IsMatch(request.run_id, "^[a-f0-9]{32}$") ||
                (request.scope != "selected" && request.scope != "full"))
                throw new ArgumentException("Invalid native test request.");
            if (request.scope == "selected" && (request.fixtures == null || request.fixtures.Length == 0))
                throw new ArgumentException("An empty selection must never become a full run.");
            string output = Confine(request.output);
            if (Directory.Exists(output)) throw new IOException("Refusing existing native run directory.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer ||
                SessionState.GetString(PendingKey, "").Length != 0)
                throw new InvalidOperationException("An idle editor with no pending run is required.");
            Type api = RequiredType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner");
            MethodInfo active = api.GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
            if (active == null || (bool)active.Invoke(null, null))
                throw new InvalidOperationException("TestRunner idle state is unknown or busy.");
            Type filterType = RequiredType("UnityEditor.TestTools.TestRunner.Api.Filter, UnityEditor.TestRunner");
            object filter = Activator.CreateInstance(filterType);
            FieldInfo mode = filterType.GetField("testMode");
            mode.SetValue(filter, Enum.Parse(mode.FieldType, "EditMode"));
            if (request.scope == "selected")
            {
                foreach (string fixture in request.fixtures)
                {
                    if (!Regex.IsMatch(fixture, @"^Worsen\.Tests\.[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+$") ||
                        Type.GetType(fixture + ", Worsen.Tests", false) == null)
                        throw new ArgumentException("Unknown fixture: " + fixture);
                }
                filterType.GetField("assemblyNames").SetValue(filter, new[] { "Worsen.Tests" });
                filterType.GetField("groupNames").SetValue(filter, request.fixtures.Distinct()
                    .Select(name => "^" + Regex.Escape(name) + @"(?:\.|\(|$)").ToArray());
            }
            Array filters = Array.CreateInstance(filterType, 1);
            filters.SetValue(filter, 0);
            Type settingsType = RequiredType("UnityEditor.TestTools.TestRunner.Api.ExecutionSettings, UnityEditor.TestRunner");
            object settings = Activator.CreateInstance(settingsType, new object[] { filters });
            settingsType.GetField("runSynchronously").SetValue(settings, false); // Never omit UnityTests.
            Type observer = RequiredType("Worsen.Tests.Infrastructure.UnityValidationTools, Worsen.Tests");
            MethodInfo register = observer.GetMethod("RefreshRegistration", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo registered = observer.GetField("registered", BindingFlags.Static | BindingFlags.NonPublic);
            if (register == null || registered == null) throw new MissingMethodException("Validation observer contract changed.");
            Directory.CreateDirectory(output);
            string configuration = ConfigurationPath();
            SessionState.SetBool(HadPreviousKey, File.Exists(configuration));
            SessionState.SetString(PreviousKey, File.Exists(configuration) ? File.ReadAllText(configuration) : "");
            File.WriteAllText(configuration, output);
            // Persist BEFORE Execute: an exception after scheduling is ambiguous; retain pending
            // state and let the coordinator reconcile, never start a second run automatically.
            SessionState.SetString(PendingKey, requestPath);
            register.Invoke(null, null);
            if (!(bool)registered.GetValue(null)) throw new InvalidOperationException("Result observer did not register.");
            var runner = ScriptableObject.CreateInstance(api);
            try { return (string)api.GetMethod("Execute").Invoke(runner, new[] { settings }); }
            finally { UnityEngine.Object.DestroyImmediate(runner); }
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1d;
            string pending = SessionState.GetString(PendingKey, "");
            if (pending.Length == 0) return;
            try
            {
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(Confine(pending)));
                string output = Confine(request.output);
                string markerPath = Path.Combine(output, "run-active.json");
                string summaryPath = Path.Combine(output, "latest-test-summary.json");
                if (!File.Exists(markerPath) || !File.Exists(summaryPath)) return;
                var marker = JsonUtility.FromJson<ObserverMarker>(File.ReadAllText(markerPath));
                var summary = JsonUtility.FromJson<ObserverSummary>(File.ReadAllText(summaryPath));
                if (marker.status != "finished" || marker.runId != summary.runId) return;
                if (summary.xmlStatus != "saved" || !summary.aggregateCountsMatch || summary.recoveredWithoutRunStarted)
                    throw new IOException("Incomplete native test evidence; inspect observer-errors.log.");
                string source = Confine(summary.xmlPath);
                if (!string.Equals(Path.GetDirectoryName(source), output, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Observer result escaped this run directory.");
                string destination = Path.Combine(output, "result-native.xml");
                File.Copy(source, destination + ".tmp", true);
                if (!File.Exists(destination)) File.Move(destination + ".tmp", destination);
                var completion = new Completion { run_id = request.run_id, scope = request.scope,
                    xml = destination, observer_run_id = marker.runId };
                string completePath = Path.Combine(output, "complete.json");
                File.WriteAllText(completePath + ".tmp", JsonUtility.ToJson(completion, true));
                if (!File.Exists(completePath)) File.Move(completePath + ".tmp", completePath);
                string configuration = ConfigurationPath();
                if (File.ReadAllText(configuration) == output)
                {
                    if (SessionState.GetBool(HadPreviousKey, false)) File.WriteAllText(configuration, SessionState.GetString(PreviousKey, ""));
                    else File.Delete(configuration);
                }
                SessionState.EraseString(PendingKey);
                SessionState.EraseString(PreviousKey);
                SessionState.EraseBool(HadPreviousKey);
            }
            catch (Exception error)
            {
                // Evidence failure is not a test failure; keep pending state and no success marker.
                string directory = Path.GetDirectoryName(pending);
                if (directory != null) File.WriteAllText(Path.Combine(directory, "native-runner-error.txt"), error.ToString());
            }
        }

        private static void StopObserving()
        {
            EditorApplication.update -= Poll;
            AssemblyReloadEvents.beforeAssemblyReload -= StopObserving;
        }

        private static Type RequiredType(string name) => Type.GetType(name, true);
        private static string ConfigurationPath() => Confine(Path.Combine(Application.dataPath, "..", "Logs", "AgentValidation", "active-output.txt"));
        private static string Confine(string path)
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string allowed = Path.Combine(project, "Logs", "AgentValidation") + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new IOException("Output must be beneath Logs/AgentValidation.");
            for (var ancestor = new DirectoryInfo(Path.GetDirectoryName(full)); ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Output uses a reparse point.");
                if (ancestor.FullName == project) break;
            }
            return full;
        }

        [Serializable] private sealed class Request { public string run_id = "", scope = "", output = ""; public string[] fixtures = Array.Empty<string>(); }
        [Serializable] private sealed class ObserverMarker { public string status = "", runId = ""; }
        [Serializable] private sealed class ObserverSummary {
            public string runId = "", xmlPath = "", xmlStatus = "";
            public bool aggregateCountsMatch = false, recoveredWithoutRunStarted = false;
        }
        [Serializable] private sealed class Completion { public string run_id, scope, xml, observer_run_id; }
    }
}
