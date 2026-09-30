// ============================================================================
// HunterCornerStallSweepWindow.cs
// ============================================================================
// PURPOSE:
//   Runs a declared seed/portal/stair route matrix and retains stall evidence.
//   Missing session-owned route control is reported as BLOCKED, never a clean sweep.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Enumerate both directions of every generated door and supplied stair edge.
//   - Observe Manager facts, retain route outcomes, and write CSV plus summary JSON.
// DEPENDENCIES:
//   - Domain Hunter/Procedural, Unity editor and scene APIs, System file I/O.
// USAGE NOTES:
//   Coordinator runs in HorrorRun Play Mode under its Unity lease. Register Host
//   from a coordinator-owned editor adapter; see ICornerStallSweepHost below.
//   No reflection writes, direct Driver commands, asset saves or steering overrides.
//   Closing/reloading cancels and preserves partial results. Seeds have no default.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Domain.Hunter;
using Worsen.Domain.Procedural;
namespace Worsen.Editor.Hunter
{
    // New integration contract, NOT an existing runtime API. Implement outside the
    // runtime assemblies: BeginSeed must use Progression -> Expedition -> Procedural
    // and await matching AssemblyReady, with the full declared live roster admitted.
    // BeginRoute arranges a start BEFORE measurement, then requests every waypoint
    // through a Session-owned diagnostic path, using normal motor ticks. Completed
    // must mean every waypoint was observed in order, not just an endpoint shortcut.
    // StairEdges must come from actual generated stair data, not copied dimensions.
    // Cancel releases diagnostic ownership; it must not resume an interrupted run.
    public interface ICornerStallSweepHost
    {
        void BeginSeed(int seed);
        bool IsReady { get; }
        string Failure { get; }
        ProceduralManager Procedural { get; }
        IReadOnlyList<CornerStallSweepRoute> StairEdges { get; }
        void BeginRoute(HunterManager hunter, Vector3[] waypoints);
        string RouteOutcome { get; } // Empty while running; Completed or a failure description.
        void EndRoute();
        void Cancel();
    }
    [Serializable]
    public sealed class CornerStallSweepRoute
    {
        public string id;
        public Vector3[] waypoints;
    }
    public sealed class HunterCornerStallSweepWindow : EditorWindow
    {
        public static ICornerStallSweepHost Host { get; set; }
        [SerializeField] private string _seeds = "";
        [SerializeField] private float _doorApproach = 2f;
        [SerializeField] private float _timeout = 30f;
        private ICornerStallSweepHost _host;
        private Summary _summary;
        private string _directory;
        private StreamWriter _csv;
        private HunterManager[] _hunters;
        private readonly List<CornerStallSweepRoute> _routes = new List<CornerStallSweepRoute>();
        private int _seedIndex, _hunterIndex, _routeIndex;
        private bool _running, _waitingFloor, _routeActive;
        private double _deadline;
        [Serializable] private sealed class Summary
        {
            public string status = "Running", reason = "";
            public int[] seeds;
            public float doorApproach, timeoutSeconds;
            public int stalls, completed, failed;
            public List<FloorRecord> floors = new List<FloorRecord>();
            public List<string> outcomes = new List<string>();
        }
        [Serializable] private sealed class FloorRecord
        {
            public int seed, roomCount, doorCount, stairEdges;
            public string manifest;
            public string[] archetypes;
            public string[] motorConfigs;
            public List<CornerStallSweepRoute> routes;
        }
        [MenuItem("Worsen/Hunter/Corner Stall Sweep")]
        public static void Open() => GetWindow<HunterCornerStallSweepWindow>("Corner Stall Sweep");
        private void OnEnable() { EditorApplication.update += Advance; }
        private void OnDisable()
        {
            EditorApplication.update -= Advance;
            if (_running) Finish("Aborted", "Window closed or scripts reloaded.");
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Coordinator lease required. Declared seeds only. Missing Host blocks before generation; never bypass Session or command Drivers.", MessageType.Info);
            using (new EditorGUI.DisabledScope(_running))
            {
                _seeds = EditorGUILayout.TextField("Seeds (comma separated)", _seeds);
                _doorApproach = EditorGUILayout.FloatField("Door approach (m)", _doorApproach);
                _timeout = EditorGUILayout.FloatField("Floor/route timeout (s)", _timeout);
                if (GUILayout.Button("Run declared sweep")) StartSweep();
            }
            if (_running && GUILayout.Button("Cancel and retain evidence")) Finish("Aborted", "Operator cancelled.");
            EditorGUILayout.LabelField(_summary?.status ?? "Not run");
            EditorGUILayout.LabelField(_directory ?? "");
        }
        private void StartSweep()
        {
            try
            {
                if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "HorrorRun")
                    throw new InvalidOperationException("Requires active HorrorRun Play Mode.");
                int[] seeds = _seeds.Split(',').Select(value => int.Parse(value.Trim(), CultureInfo.InvariantCulture)).ToArray();
                if (seeds.Length == 0 || seeds.Distinct().Count() != seeds.Length ||
                    !(_doorApproach > 0) || float.IsInfinity(_doorApproach) || !(_timeout > 0) || float.IsInfinity(_timeout))
                    throw new ArgumentException("Declare unique seeds and finite positive distances/timeouts.");
                _summary = new Summary { seeds = seeds, doorApproach = _doorApproach, timeoutSeconds = _timeout };
                _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/AgentValidation/PLAN-014",
                    "sweep-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);
                _csv = new StreamWriter(Path.Combine(_directory, "stalls.csv")) { AutoFlush = true };
                _csv.WriteLine("seed,route,hunter,tick,position,room,agentRadius,capsuleRadius,motorRadius,action,remaining,nearestObstacle,pathCorners");
                _host = Host; _seedIndex = 0; _routeActive = false; _running = true;
                Save(); // Freeze declaration before touching any generation state.
                if (_host == null) { Finish("BLOCKED", "Coordinator adapter missing: seeded full-roster expedition generation, Session-owned route commands and generated stair-edge data are required."); return; }
                BeginSeed();
            }
            catch (Exception exception)
            {
                if (_running) Finish("Failed", exception.ToString());
                else Debug.LogError(exception.Message);
            }
        }
        private void BeginSeed()
        {
            _waitingFloor = true; _deadline = EditorApplication.timeSinceStartup + _timeout;
            _host.BeginSeed(_summary.seeds[_seedIndex]);
        }
        private void Advance()
        {
            if (!_running) return;
            try
            {
                if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "HorrorRun")
                { Finish("Aborted", "Left HorrorRun Play Mode."); return; }
                if (!string.IsNullOrEmpty(_host.Failure)) { Finish("Failed", _host.Failure); return; }
                if (_waitingFloor)
                {
                    if (!_host.IsReady)
                    { if (EditorApplication.timeSinceStartup > _deadline) Finish("Failed", "Generation timed out."); return; }
                    PrepareFloor(); return;
                }
                if (!_routeActive) { BeginRoute(); return; }
                string outcome = _host.RouteOutcome;
                if (string.IsNullOrEmpty(outcome) && EditorApplication.timeSinceStartup <= _deadline) return;
                if (string.IsNullOrEmpty(outcome)) outcome = "Timeout";
                _summary.outcomes.Add(_summary.seeds[_seedIndex] + ":" + _hunters[_hunterIndex].ArchetypeKey + ":" + _routes[_routeIndex].id + ":" + outcome);
                if (outcome == "Completed") _summary.completed++; else _summary.failed++;
                _host.EndRoute(); _routeActive = false; Save();
                if (++_routeIndex < _routes.Count) return;
                _routeIndex = 0;
                if (++_hunterIndex < _hunters.Length) return;
                Unsubscribe();
                if (++_seedIndex < _summary.seeds.Length) BeginSeed();
                else Finish(_summary.failed == 0 && _summary.stalls == 0 && _summary.completed > 0 ? "CompletedNoStalls" : "Failed", "Route outcomes depend on the coordinator adapter's measured waypoint contract; not owner acceptance.");
            }
            catch (Exception exception) { Finish("Failed", exception.ToString()); }
        }
        private void PrepareFloor()
        {
            ProceduralManager floor = _host.Procedural;
            if (floor == null || !floor.IsReady || floor.Graph == null || _host.StairEdges == null)
                throw new InvalidOperationException("Ready host must expose admitted geometry and explicit stair-edge coverage.");
            if (_host.StairEdges.Count == 0 && floor.RoomModules.Any(module => module.Kind == ProceduralModuleKind.OpenStairHall ||
                module.Kind == ProceduralModuleKind.SplitLevelLibrary || module.Kind == ProceduralModuleKind.BrokenGallery))
                throw new InvalidOperationException("Generated stair modules exist but the adapter supplied zero stair edges.");
            _hunters = HunterRegistry.Items.Where(h => h != null && h.ReadOnlyState != null && h.ReadOnlyState.IsActive).OrderBy(h => h.ArchetypeKey).ThenBy(h => h.Id.ToString()).ToArray();
            if (_hunters.Length == 0) throw new InvalidOperationException("Zero live hunters is not a sweep.");
            _routes.Clear(); int index = 0;
            foreach (var door in floor.Doors)
            {
                Vector3 axis = door.AlongX ? Vector3.forward : Vector3.right;
                AddBoth("door-" + index++ + (door.IsOptional ? "-optional" : ""), new[] { door.Center - axis * _doorApproach, door.Center, door.Center + axis * _doorApproach });
            }
            foreach (var stair in _host.StairEdges) AddBoth("stair-" + stair.id, stair.waypoints);
            if (_routes.Count == 0) throw new InvalidOperationException("Zero routes is not a sweep.");
            _summary.floors.Add(new FloorRecord { seed = _summary.seeds[_seedIndex], roomCount = floor.Graph.Rooms.Count,
                doorCount = floor.Doors.Count, stairEdges = _host.StairEdges.Count, manifest = floor.LayoutManifest,
                archetypes = _hunters.Select(h => h.ArchetypeKey).ToArray(), motorConfigs = _hunters.Select(MotorConfig).ToArray(),
                routes = new List<CornerStallSweepRoute>(_routes) });
            foreach (var hunter in _hunters) hunter.OnStall += RecordStall;
            _waitingFloor = false; _hunterIndex = 0; _routeIndex = 0; Save();
        }
        private void AddBoth(string id, Vector3[] points)
        {
            if (points == null || points.Length < 2) throw new ArgumentException("Route needs at least two measured waypoints: " + id);
            _routes.Add(new CornerStallSweepRoute { id = id + "/forward", waypoints = (Vector3[])points.Clone() });
            _routes.Add(new CornerStallSweepRoute { id = id + "/reverse", waypoints = points.Reverse().ToArray() });
        }
        private void BeginRoute()
        {
            if (_hunters[_hunterIndex] == null) throw new InvalidOperationException("Hunter disappeared during the matrix.");
            _routeActive = true; _deadline = EditorApplication.timeSinceStartup + _timeout;
            _host.BeginRoute(_hunters[_hunterIndex], (Vector3[])_routes[_routeIndex].waypoints.Clone());
        }
        private void RecordStall(HunterStallFact fact)
        {
            _summary.stalls++;
            _csv.WriteLine(string.Join(",", new[] { _summary.seeds[_seedIndex].ToString(CultureInfo.InvariantCulture),
                _routeActive ? _routes[_routeIndex].id : "between-routes", fact.Hunter.ToString(), fact.Tick.ToString(CultureInfo.InvariantCulture),
                Vector(fact.Position), fact.RoomId?.ToString(CultureInfo.InvariantCulture) ?? "unknown", Number(fact.AgentRadius),
                Number(fact.CapsuleRadius), Number(fact.MotorRadius), fact.Action.ToString(), Number(fact.RemainingDistance),
                fact.NearestObstaclePoint.HasValue ? Vector(fact.NearestObstaclePoint.Value) : "unknown",
                string.Join(";", fact.PathCorners.Select(Vector)) }.Select(Csv)));
            Save();
        }
        private void Unsubscribe()
        { if (_hunters != null) foreach (var hunter in _hunters) if (hunter != null) hunter.OnStall -= RecordStall; _hunters = null; }
        private void Finish(string status, string reason)
        {
            _running = false; Unsubscribe();
            try { _host?.Cancel(); }
            catch (Exception exception) { status = "Failed"; reason += " Cleanup: " + exception.Message; }
            finally
            {
                _summary.status = status; _summary.reason = reason;
                try { Save(); } finally { _csv?.Dispose(); _csv = null; _host = null; }
            }
            Debug.Log("Corner stall sweep " + status + ": " + _directory + " — " + reason);
        }
        private void Save() => File.WriteAllText(Path.Combine(_directory, "summary.json"), JsonUtility.ToJson(_summary, true));
        private static string MotorConfig(HunterManager hunter)
        {
            var serialized = new SerializedObject(hunter.GetComponent<HunterDriver>());
            var config = serialized.FindProperty("_config").objectReferenceValue;
            if (config == null) throw new InvalidOperationException("Cannot freeze the live hunter motor configuration.");
            return EditorJsonUtility.ToJson(config);
        }
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => Number(value.x) + " " + Number(value.y) + " " + Number(value.z);
        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
