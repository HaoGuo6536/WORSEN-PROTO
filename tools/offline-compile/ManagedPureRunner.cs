// ============================================================================
// ManagedPureRunner.cs
// ============================================================================
// PURPOSE:
//   Runs the compiled Edit Mode assembly on the bundled managed runtime without
//   an editor connection. NUnit supplies reflection discovery and fixture
//   semantics; a separate process supplies a killable per-test time boundary.
// ARCHITECTURAL ROLE:
//   Offline verification tool · no runtime layer · integration tooling.
// KEY RESPONSIBILITIES:
//   - Discover and filter NUnit cases, accounting for Unity-only exclusions.
//   - Execute NUnit in a monitored child and preserve lifecycle failures.
//   - Record complete case, namespace and fixture totals as durable evidence.
// DEPENDENCIES:
//   - The existing NUnit framework, compiled test dependencies and .NET runtime.
//   - PureTestPolicy classifies editor-only calls and engine exceptions.
// USAGE NOTES:
//   No Unity process or service is invoked. Timeouts kill only this harness's
//   child; unfinished cases resume in a new child, repeating one-time hooks.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

public static class ManagedPureRunner
{
    public sealed class CaseResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Fixture { get; set; }
        public string Namespace { get; set; }
        public string Outcome { get; set; }
        public string Message { get; set; }
        public string Stack { get; set; }
        public double Seconds { get; set; }
    }

    public sealed class Event
    {
        public string Kind { get; set; }
        public string Id { get; set; }
        public List<string> Descendants { get; set; }
        public List<CaseResult> Cases { get; set; }
        public string Message { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public static int Main(string[] args)
    {
        try
        {
            string root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
                string path = Path.Combine(root, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            return args[0] == "worker" ? Worker(args) : Monitor(args);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("HARNESS_ERROR " + error);
            return 2;
        }
    }

    private static int Monitor(string[] args)
    {
        // run <assembly> <filter> <timeout-ms> <dotnet> <summary>
        string assembly = args[1], filter = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(args[2].Substring(7))),
            dotnet = args[4], summaryPath = args[5];
        int timeout = int.Parse(args[3]);
        var wall = Stopwatch.StartNew();
        var results = new Dictionary<string, CaseResult>();
        var completed = new HashSet<string>();
        var infrastructure = new List<string>();
        bool manifestSeen = false;
        int attempt = 0;
        do
        {
            string prefix = Path.Combine(Path.GetDirectoryName(summaryPath), "worker-" + (++attempt));
            string excluded = prefix + "-exclude.json", events = prefix + "-events.jsonl";
            File.WriteAllText(excluded, JsonSerializer.Serialize(completed.ToArray()));
            var start = new ProcessStartInfo(dotnet) { UseShellExecute = false };
            foreach (string arg in new[] { Assembly.GetExecutingAssembly().Location, "worker", assembly, filter, events, excluded })
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            using var custody = new ChildCustody(process);
            var idle = Stopwatch.StartNew();
            var activeScopes = new List<Event>();
            bool done = false, timedOut = false;
            using var stream = new FileStream(events, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string pending = "";
            while (true)
            {
                // Read complete lines only: a concurrent flush can expose a partial JSON event.
                string chunk = reader.ReadToEnd();
                pending += chunk;
                int newline;
                while ((newline = pending.IndexOf('\n')) >= 0)
                {
                    string line = pending.Substring(0, newline).TrimEnd((char)13);
                    pending = pending.Substring(newline + 1);
                    if (line.Length == 0) continue;
                    var item = JsonSerializer.Deserialize<Event>(line);
                    idle.Restart();
                    if (item.Kind == "manifest")
                    {
                        manifestSeen = true;
                        foreach (var test in item.Cases)
                        {
                            if (!results.ContainsKey(test.Id)) results.Add(test.Id, test);
                            else if (results[test.Id].Name != test.Name) infrastructure.Add("Discovery identity changed after timeout: " + test.Id);
                            if (test.Outcome != "pending") completed.Add(test.Id);
                        }
                    }
                    else if (item.Kind == "start") activeScopes.Add(item);
                    else if (item.Kind == "result")
                    {
                        foreach (var test in item.Cases) { results[test.Id] = test; completed.Add(test.Id); }
                        activeScopes.RemoveAll(scope => scope.Id == item.Id);
                    }
                    else if (item.Kind == "done")
                    {
                        foreach (var test in item.Cases) { results[test.Id] = test; completed.Add(test.Id); }
                        done = true;
                    }
                    else if (item.Kind == "error") infrastructure.Add(item.Message);
                }
                if (process.HasExited && chunk.Length == 0) break;
                if (idle.ElapsedMilliseconds > timeout)
                {
                    timedOut = true;
                    process.Kill(true);
                    process.WaitForExit();
                    List<string> active = activeScopes.LastOrDefault()?.Descendants;
                    var victims = (active ?? new List<string>()).Where(id => results.ContainsKey(id) && !completed.Contains(id)).ToList();
                    // A one-time teardown can time out after all fixture leaves finished.
                    if (victims.Count == 0 && active != null) victims = active.Where(results.ContainsKey).ToList();
                    if (victims.Count == 0) infrastructure.Add("Worker timed out during discovery or between tests.");
                    foreach (string id in victims)
                    {
                        results[id].Outcome = "failed";
                        results[id].Message = (results[id].Message == null ? "" : results[id].Message + "\n") +
                            "Per-test/lifecycle timeout after " + timeout + " ms; harness child terminated.";
                        completed.Add(id);
                    }
                    break;
                }
                Thread.Sleep(20);
            }
            process.WaitForExit();
            if (!timedOut && (!done || process.ExitCode != 0))
                infrastructure.Add("Worker ended without a complete NUnit result (exit " + process.ExitCode + ").");
            if (infrastructure.Count != 0) break;
        } while (results.Values.Any(test => test.Outcome == "pending"));

        foreach (var test in results.Values.Where(test => test.Outcome == "pending"))
        {
            test.Outcome = "failed";
            test.Message = "No completed result: " + string.Join("; ", infrastructure);
        }
        var cases = results.Values.OrderBy(test => test.Name, StringComparer.Ordinal).ThenBy(test => test.Id).ToList();
        var totals = Totals(cases);
        var namespaces = cases.GroupBy(test => test.Namespace).OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => Totals(group));
        var fixtures = cases.GroupBy(test => test.Fixture).OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => Totals(group));
        int pure = cases.Count(test => test.Outcome == "passed" || test.Outcome == "failed");
        int exit = infrastructure.Count != 0 || !manifestSeen || cases.Count == 0 ? 2 : totals["failed"] > 0 ? 1 : 0;
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(new {
            Scope = "Headless NUnit reflection execution; not Unity Test Framework verification.",
            Assembly = assembly, Filter = filter, TimeoutMilliseconds = timeout,
            CapturedUtc = DateTime.UtcNow.ToString("o"), ExitCode = exit,
            WallSeconds = wall.Elapsed.TotalSeconds, Total = cases.Count, Totals = totals,
            Pure = pure, PureFraction = cases.Count == 0 ? 0d : (double)pure / cases.Count,
            Namespaces = namespaces, Fixtures = fixtures,
            EnvironmentDominatedFixtures = fixtures.Where(pair => pair.Value["environment"] > pair.Value.Values.Sum() / 2d).Select(pair => pair.Key).ToArray(),
            InfrastructureErrors = infrastructure, Cases = cases
        }, JsonOptions));
        Console.WriteLine("PURE_RESULT passed=" + totals["passed"] + " failed=" + totals["failed"] +
            " environment=" + totals["environment"] + " skipped=" + totals["skipped"]);
        foreach (var test in cases.Where(test => test.Outcome == "failed"))
            Console.WriteLine("FAIL " + test.Name + "\n" + test.Message + "\n" + test.Stack);
        foreach (string error in infrastructure) Console.WriteLine("HARNESS_ERROR " + error);
        return exit;
    }

    private static Dictionary<string, int> Totals(IEnumerable<CaseResult> cases) =>
        new[] { "passed", "failed", "environment", "skipped" }.ToDictionary(key => key, key => cases.Count(test => test.Outcome == key));

    private static int Worker(string[] args)
    {
        using var sink = new StreamWriter(new FileStream(args[3], FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        void Send(Event item) => sink.WriteLine(JsonSerializer.Serialize(item));
        try
        {
            Assembly assembly = Assembly.LoadFrom(args[1]);
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            // Zero workers means deterministic direct execution; no NUnit timeout/thread abort.
            ITest tree = runner.Load(assembly, new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0,
                ["WorkDirectory"] = Path.GetDirectoryName(args[3]) });
            var excluded = new HashSet<string>(JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[4])));
            var cases = Leaves(tree).Where(test => Matches(test.FullName, args[2])).ToDictionary(test => test.Id, Describe);
            foreach (ITest test in Leaves(tree).Where(test => cases.ContainsKey(test.Id)))
            {
                CaseResult entry = cases[test.Id];
                string skip = SkipReason(test);
                if (skip != null) { entry.Outcome = "skipped"; entry.Message = skip; }
                else
                {
                    string editorCall = PureTestPolicy.FindEditorCall(test);
                    if (editorCall != null) { entry.Outcome = "environment"; entry.Message = "UnityEditor API dependency: " + editorCall; }
                }
            }
            // UnityTest is a Unity extension, not an NUnit test builder. Account for
            // coroutine-only methods even if the standard builder omits them.
            foreach (Type type in assembly.GetTypes().Where(type => !type.IsAbstract))
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (!PureTestPolicy.Has(method, "UnityTestAttribute")) continue;
                if (cases.Values.Any(test => test.Fixture == type.FullName && test.Name.StartsWith(type.FullName + "." + method.Name, StringComparison.Ordinal))) continue;
                string name = type.FullName + "." + method.Name;
                if (!Matches(name, args[2])) continue;
                string id = "unity:" + type.FullName + ":" + method.MetadataToken;
                cases[id] = new CaseResult { Id = id, Name = name, Fixture = type.FullName,
                    Namespace = NamespaceOf(type.FullName), Outcome = "skipped", Message = "UnityTest coroutine requires Unity." };
            }
            Send(new Event { Kind = "manifest", Cases = cases.Values.ToList() });
            var selected = new HashSet<string>(cases.Values.Where(test => test.Outcome == "pending" && !excluded.Contains(test.Id)).Select(test => test.Id));
            if (selected.Count > 0)
            {
                var listener = new Listener(Send, cases, selected);
                ITestResult result = runner.Run(listener, new SelectionFilter(selected));
                File.WriteAllText(args[3] + ".nunit.xml", result.ToXml(true).OuterXml);
                ApplyResults(result, cases, selected, null);
            }
            Send(new Event { Kind = "done", Cases = cases.Values.Where(test => !excluded.Contains(test.Id)).ToList() });
            return 0;
        }
        catch (Exception error)
        {
            Send(new Event { Kind = "error", Message = error.ToString() });
            return 2;
        }
    }

    private static bool Matches(string name, string filter) => string.IsNullOrEmpty(filter) ||
        name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
        Regex.IsMatch(name, filter, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static string NamespaceOf(string fixture)
    {
        string[] parts = fixture.Split('.');
        return parts.Length > 3 && parts[0] == "Worsen" && parts[1] == "Tests" ? "Worsen.Tests." + parts[2] : string.Join(".", parts.Take(Math.Max(0, parts.Length - 1)));
    }

    private static CaseResult Describe(ITest test) => new CaseResult { Id = test.Id, Name = test.FullName,
        Fixture = test.ClassName ?? test.TypeInfo?.FullName ?? "<unknown>",
        Namespace = NamespaceOf(test.ClassName ?? "<unknown>"), Outcome = "pending" };

    private static IEnumerable<ITest> Leaves(ITest test)
    {
        if (!test.IsSuite) yield return test;
        foreach (ITest child in test.Tests)
        foreach (ITest leaf in Leaves(child)) yield return leaf;
    }

    private static string SkipReason(ITest test)
    {
        if (test.Method != null && PureTestPolicy.Has(test.Method.MethodInfo, "UnityTestAttribute")) return "UnityTest coroutine requires Unity.";
        for (Type type = test.TypeInfo?.Type; type != null; type = type.BaseType)
            if (type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(method => PureTestPolicy.Has(method, "UnitySetUpAttribute") || PureTestPolicy.Has(method, "UnityTearDownAttribute")))
                return "UnitySetUp/UnityTearDown coroutine lifecycle requires Unity.";
        for (ITest node = test; node != null; node = node.Parent)
        {
            if (node.RunState == RunState.Ignored || node.RunState == RunState.Explicit || node.RunState == RunState.Skipped)
                return node.RunState + ": " + node.Properties.Get("_SKIPREASON");
            if (node.Properties["Category"].Cast<object>().Any(value => (string)value == "RequiresFocus")) return "Category RequiresFocus.";
        }
        return null;
    }

    private static void ApplyResults(ITestResult result, Dictionary<string, CaseResult> cases, HashSet<string> selected, ITestResult parentError)
    {
        // A fixture setup/teardown failure applies to all selected descendants.
        // Preserve independent test failures instead of hiding them behind an engine error.
        ITestResult inherited = result.Test.IsSuite && result.ResultState.Status == TestStatus.Failed &&
            result.ResultState.Site != FailureSite.Child ? result : parentError;
        if (!result.Test.IsSuite && selected.Contains(result.Test.Id))
        {
            CaseResult entry = cases[result.Test.Id];
            if (inherited != null && (result.ResultState.Status != TestStatus.Failed || string.IsNullOrEmpty(result.StackTrace)))
                SetResult(entry, inherited);
            else
            {
                SetResult(entry, result);
                if (inherited != null)
                {
                    var lifecycle = new CaseResult();
                    SetResult(lifecycle, inherited);
                    // Neither an engine test error nor engine cleanup may hide
                    // an independent assertion/lifecycle failure.
                    if (lifecycle.Outcome == "failed") entry.Outcome = "failed";
                    entry.Message += "\nFixture lifecycle: " + lifecycle.Message;
                    entry.Stack += "\nFixture lifecycle: " + lifecycle.Stack;
                }
            }
        }
        foreach (ITestResult child in result.Children) ApplyResults(child, cases, selected, inherited);
    }

    private static void SetResult(CaseResult entry, ITestResult result)
    {
        entry.Seconds = result.Duration;
        entry.Message = result.Message;
        entry.Stack = result.StackTrace;
        entry.Outcome = result.ResultState.Status == TestStatus.Passed ? "passed" :
            result.ResultState.Status == TestStatus.Skipped ? "skipped" :
            result.ResultState.Status == TestStatus.Inconclusive ? "failed" :
            PureTestPolicy.IsEnvironment(result.Message, result.StackTrace, result.ResultState.Label) ? "environment" : "failed";
    }

    private sealed class ChildCustody : IDisposable
    {
        private readonly Process child;
        public ChildCustody(Process child) { this.child = child; }
        public void Dispose()
        {
            if (!child.HasExited) child.Kill(true);
            child.WaitForExit();
        }
    }

    private sealed class SelectionFilter : TestFilter
    {
        private readonly HashSet<string> selected;
        public SelectionFilter(HashSet<string> selected) { this.selected = selected; }
        public override bool Match(ITest test) => selected.Contains(test.Id);
        public override bool IsExplicitMatch(ITest test) => false;
        public override TNode AddToXml(TNode parentNode, bool recursive) => parentNode.AddElement("filter");
    }

    private sealed class Listener : ITestListener
    {
        private readonly Action<Event> send;
        private readonly Dictionary<string, CaseResult> cases;
        private readonly HashSet<string> selected;
        public Listener(Action<Event> send, Dictionary<string, CaseResult> cases, HashSet<string> selected)
        { this.send = send; this.cases = cases; this.selected = selected; }
        public void TestStarted(ITest test) => send(new Event { Kind = "start", Id = test.Id,
            Descendants = Leaves(test).Where(leaf => selected.Contains(leaf.Id)).Select(leaf => leaf.Id).ToList() });
        public void TestFinished(ITestResult result)
        {
            if (!result.Test.IsSuite && selected.Contains(result.Test.Id))
            {
                SetResult(cases[result.Test.Id], result);
                send(new Event { Kind = "result", Id = result.Test.Id, Cases = new List<CaseResult> { cases[result.Test.Id] } });
            }
            else if (result.Test.IsSuite)
            {
                ApplyResults(result, cases, selected, null);
                send(new Event { Kind = "result", Id = result.Test.Id, Cases = Leaves(result.Test).Where(leaf => selected.Contains(leaf.Id)).Select(leaf => cases[leaf.Id]).ToList() });
            }
        }
        public void TestOutput(TestOutput output) { /* NUnit retains output in its own results. */ }
    }
}
