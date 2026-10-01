// ============================================================================
// ProceduralTemplateAnchorTests.cs
// ============================================================================
// PURPOSE:
//   Checks every real room catalogue against the collision commands actually
//   built by the template presenter. A managed swept-envelope flood proves that
//   required points share walkable floor, rather than merely a connected room id.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check cake density, floor support, agent clearance and door connectivity.
//   - Check authored hunters and selected player, exit and shrine positions.
//   - Exercise all quarter turns and every nonempty open-door subset.
//   - Reject synthetic gaps, isolated pockets and radius-only obstructions.
// DEPENDENCIES:
//   - NUnit, Core, Domain.Procedural and the production manifest parser.
// USAGE NOTES:
//   No engine objects or native APIs. Agent dimensions come from the checked-in
//   Humanoid settings; a 0.25m search lattice is a test resolution, not a tunable.
//   Sweeps are conservative boxes. Native voxelization remains a separate gate.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralTemplateAnchorTests
    {
        public static IEnumerable<TestCaseData> Catalogues()
        {
            foreach (var theme in new[] { "Castle", "Hospital", "School", "Basement" })
            foreach (var room in Read(theme).Templates)
                yield return new TestCaseData(theme, room.Id);
        }

        [TestCaseSource(nameof(Catalogues))]
        public void EveryRequiredAnchorHasSupportedClearConnectedFloor(string theme, string id)
        {
            var catalogue = Read(theme); var template = catalogue.Templates.Single(t => t.Id == id);
            var config = Config(); var driver = Empty<ProceduralDriverConfig>();
            Field(driver, "_floorThickness", .3f); Field(driver, "_ceilingThickness", .3f); Field(driver, "_wallThickness", .3f);
            float radius = Agent("agentRadius"), height = Agent("agentHeight");
            int minimum = Math.Max(template.SizeClass == "closet" ? 1 : 2, (template.Footprint.Length * 2 + 8) / 9);
            Assert.That(template.Cake.Distinct().Count(), Is.GreaterThanOrEqualTo(minimum), id + " cake density");
            var failures = new HashSet<string>();
            for (int turn = 0; turn < 4; turn++)
            for (int mask = 1; mask < (1 << template.Doors.Length); mask++)
            {
                var room = new ProceduralTemplateRoom { RoomId = 2, Template = template, Turns = turn,
                    Offset = new Vector2Int(7, -5), SubcellOffset = Vector2Int.one,
                    OpenDoors = Enumerable.Range(0, template.Doors.Length).Where(i => (mask & (1 << i)) != 0).ToArray() };
                Vector3 World(Vector3 p) => ProceduralTemplateUtility.Point(room, p, Vector2.zero);
                var blocks = new ProceduralTemplateGeometryPresenter().Build(catalogue, room, config, driver);
                var map = new WalkMap(template, blocks, World, radius, height);
                var targets = new List<(string label, Vector3 point)>();
                Add("cake", template.Cake); Add("goldenCake", template.GoldenCake); Add("hunterSpawn", template.HunterSpawn);
                if (ProceduralExitHubUtility.TrySelect(catalogue, template, 1.6f, 3.2f, 1.5f, 2.8f, out var player, out var exit))
                { targets.Add(("player", player)); targets.Add(("exit", exit)); }
                var layout = new ProceduralLayout();
                Property(layout, "HunterSpawnPositions", template.HunterSpawn.Select(World).ToArray());
                Property(layout, "Doors", Array.Empty<ProceduralDoorPlan>());
                var volumes = ProceduralTemplateUtility.Volumes(ProceduralTemplateUtility.OccupiedCells(room), Vector2.zero, template.Height, 1f);
                var bounds = volumes[0]; foreach (var volume in volumes) bounds.Encapsulate(volume);
                Property(layout, "Graph", new LevelGraph(new[] { new LevelRoom(2, bounds.center, bounds.size, cells: volumes) },
                    Array.Empty<LevelEdge>(), template.Cake.Select((p, i) => new LevelAnchor(i + 1, 2, CakeAnchorType.Flow, World(p))).ToArray(), 2, World(template.Cake[0])));
                // Use a distinct exit id to exercise ordinary site production in this room.
                Property(layout, "Graph", new LevelGraph(layout.Graph.Rooms, layout.Graph.Edges, layout.Graph.Anchors, 1, World(template.Cake[0])));
                // The graph-distance consumer needs an actual connected exit room.
                var hub = new LevelRoom(1, new Vector3(-100f, 3f, -100f), new Vector3(6f, 6f, 6f));
                Property(layout, "Graph", new LevelGraph(new[] { hub, layout.Graph.Rooms[0] },
                    new[] { new LevelEdge(1, 1, 2, true, TraversalAccess.All) }, layout.Graph.Anchors, 1, hub.Center));
                Property(layout, "PlayerSpawnPosition", hub.Center);
                foreach (var site in new ProceduralShrineSitePresenter().Build(layout, config, blocks, radius, height))
                {
                    var local = ProceduralTemplateUtility.Rotate(site.Position - World(Vector3.zero), (4 - turn) % 4);
                    targets.Add(("shrine", local));
                }
                var door = template.Doors[room.OpenDoors[0]];
                var normal = ProceduralTemplateUtility.Direction(door.Side);
                var start = ProceduralTemplateUtility.Door(door) - new Vector3(normal.x, 0f, normal.y);
                var reached = map.Flood(start);
                foreach (int index in room.OpenDoors)
                {
                    var socket = template.Doors[index]; var n = ProceduralTemplateUtility.Direction(socket.Side);
                    var inner = ProceduralTemplateUtility.Door(socket) - new Vector3(n.x, 0f, n.y);
                    targets.Add(("door approach " + index, inner));
                    if (!map.CollisionClear(inner, ProceduralTemplateUtility.Door(socket)))
                        failures.Add("door throat " + index + " blocked");
                }
                foreach (var target in targets)
                    if (!map.Reached(target.point, reached))
                    {
                        var suggestions = template.Footprint.Select(c => new Vector3(c.x * 2f + 1f, 0f, c.y * 2f + 1f))
                            .Where(p => map.Reached(p, reached)).OrderBy(p => (p - target.point).sqrMagnitude).Take(4);
                        failures.Add(target.label + " " + target.point + " blocked/unsupported/disconnected; nearest=" + string.Join(";", suggestions));
                    }
                void Add(string label, Vector3[] points)
                { for (int i = 0; i < points.Length; i++) targets.Add((label + "[" + i + "]", points[i])); }
            }
            Assert.That(failures, Is.Empty, id + ":\n" + string.Join("\n", failures));
        }

        [Test]
        public void ManagedFloodRejectsGapPocketAndRadiusOnlyOverlap()
        {
            var room = new ProceduralRoomTemplate { Height = 3f,
                Footprint = new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) } };
            var floor = new ProceduralBlock(1, ProceduralSurfaceKind.Floor, new Vector3(3f, -.1f, 1f), new Vector3(6f, .2f, 2f));
            var wall = new ProceduralBlock(1, ProceduralSurfaceKind.Wall, new Vector3(3f, 1f, 1f), new Vector3(.2f, 2f, 2f));
            var map = new WalkMap(room, new[] { floor, wall }, p => p, .5f, 2f);
            Assert.That(map.Clear(new Vector3(5f, 0f, 1f)), Is.True);
            Assert.That(map.Reached(new Vector3(5f, 0f, 1f), map.Flood(new Vector3(1f, 0f, 1f))), Is.False, "Clear but isolated pocket.");
            Assert.That(map.Clear(new Vector3(2.5f, 0f, 1f)), Is.False, "Agent radius, not just the point.");
            room.Footprint = new[] { Vector2Int.zero, new Vector2Int(2, 0) };
            map = new WalkMap(room, new[] { floor }, p => p, .5f, 2f);
            Assert.That(map.Reached(new Vector3(5f, 0f, 1f), map.Flood(new Vector3(1f, 0f, 1f))), Is.False, "Missing footprint cannot bridge a gap.");
            map = new WalkMap(room, Array.Empty<ProceduralBlock>(), p => p, .5f, 2f);
            Assert.That(map.Clear(new Vector3(1f, 0f, 1f)), Is.False, "Footprint alone does not supply collision support.");
        }

        private sealed class WalkMap
        {
            private const float Step = .25f;
            private readonly HashSet<Vector2Int> _cells;
            private readonly ProceduralBlock[] _obstacles, _floors;
            private readonly Func<Vector3, Vector3> _world;
            private readonly float _radius, _height;
            private readonly Dictionary<Vector2Int, bool> _clear = new Dictionary<Vector2Int, bool>();
            public WalkMap(ProceduralRoomTemplate room, IEnumerable<ProceduralBlock> blocks, Func<Vector3, Vector3> world, float radius, float height)
            {
                _cells = new HashSet<Vector2Int>(room.Footprint); _world = world; _radius = radius; _height = height;
                _obstacles = blocks.Where(b => b.HasCollision && b.Kind != ProceduralSurfaceKind.Floor && b.Center.y - b.Size.y * .5f < height).ToArray();
                _floors = blocks.Where(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Floor).ToArray();
            }
            public bool Clear(Vector3 p)
            {
                if (!ProceduralTemplateUtility.Finite(p)) return false;
                // Every cell overlapped by the conservative standing square must exist.
                for (int x = (int)Math.Floor((p.x - _radius + .0001f) / 2f); x <= (int)Math.Floor((p.x + _radius - .0001f) / 2f); x++)
                for (int z = (int)Math.Floor((p.z - _radius + .0001f) / 2f); z <= (int)Math.Floor((p.z + _radius - .0001f) / 2f); z++)
                    if (!_cells.Contains(new Vector2Int(x, z))) return false;
                foreach (float x in new[] { -_radius, 0f, _radius })
                foreach (float z in new[] { -_radius, 0f, _radius })
                {
                    var foot = _world(p + new Vector3(x, 0f, z)); foot.y = -.01f;
                    if (!_floors.Any(b => Inside(b, foot))) return false;
                }
                return CollisionClear(p, p);
            }
            public bool CollisionClear(Vector3 from, Vector3 to)
            {
                from = _world(from); to = _world(to); from.y = to.y = _height * .5f;
                foreach (var block in _obstacles)
                {
                    var a = Local(block, from); var b = Local(block, to);
                    // Apply the same contact tolerance to parallel and moving axes;
                    // world quarter-turn roundoff must not prevent moving off a face.
                    var half = block.Size * .5f + new Vector3(_radius, _height * .5f, _radius) - Vector3.one * .0001f;
                    float low = 0f, high = 1f; bool intersects = true;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        float delta = b[axis] - a[axis];
                        if (Math.Abs(delta) < .00001f)
                        { if (Math.Abs(a[axis]) >= half[axis]) { intersects = false; break; } continue; }
                        float enter = (-half[axis] - a[axis]) / delta, leave = (half[axis] - a[axis]) / delta;
                        low = Math.Max(low, Math.Min(enter, leave)); high = Math.Min(high, Math.Max(enter, leave));
                        if (low >= high) { intersects = false; break; }
                    }
                    if (intersects) return false;
                }
                return true;
            }
            private bool Safe(Vector2Int key)
            {
                if (!_clear.TryGetValue(key, out bool value)) _clear[key] = value = Clear(Point(key));
                return value;
            }
            public HashSet<Vector2Int> Flood(Vector3 start)
            {
                var seen = new HashSet<Vector2Int>(); var queue = new Queue<Vector2Int>();
                if (!Clear(start)) return seen;
                foreach (var key in Near(start))
                    if (Safe(key) && CollisionClear(start, Point(key)) && seen.Add(key)) queue.Enqueue(key);
                while (queue.Count > 0)
                {
                    var key = queue.Dequeue();
                    foreach (var direction in ProceduralTemplateUtility.Directions())
                    {
                        var next = key + direction;
                        if (!seen.Contains(next) && Safe(next) && Clear((Point(key) + Point(next)) * .5f) &&
                            CollisionClear(Point(key), Point(next))) { seen.Add(next); queue.Enqueue(next); }
                    }
                }
                return seen;
            }
            public bool Reached(Vector3 p, HashSet<Vector2Int> reached)
                => Clear(p) && Near(p).Any(k => reached.Contains(k) && Clear((p + Point(k)) * .5f) && CollisionClear(p, Point(k)));
            private static IEnumerable<Vector2Int> Near(Vector3 p)
            {
                int x = (int)Math.Floor(p.x / Step), z = (int)Math.Floor(p.z / Step);
                for (int a = 0; a <= 1; a++) for (int b = 0; b <= 1; b++) yield return new Vector2Int(x + a, z + b);
            }
            private static Vector3 Point(Vector2Int p) => new Vector3(p.x * Step, 0f, p.y * Step);
            private static Vector3 Local(ProceduralBlock b, Vector3 p)
            { var q = b.Rotation; return new Quaternion(-q.x, -q.y, -q.z, q.w) * (p - b.Center); }
            private static bool Inside(ProceduralBlock b, Vector3 p)
            { p = Local(b, p); return Math.Abs(p.x) <= b.Size.x * .5f + .0001f && Math.Abs(p.y) <= b.Size.y * .5f + .0001f && Math.Abs(p.z) <= b.Size.z * .5f + .0001f; }
        }
        private static float Agent(string name) => float.Parse(File.ReadLines("ProjectSettings/NavMeshAreas.asset")
            .First(l => l.TrimStart().StartsWith(name + ":", StringComparison.Ordinal)).Split(':')[1], CultureInfo.InvariantCulture);
        private static ProceduralConfig Config()
        {
            var c = Empty<ProceduralConfig>(); Field(c, "_doorWidth", 3.2f); Field(c, "_doorHeight", 2.8f);
            Field(c, "_roomSize", 12f); Field(c, "_shrineSiteEnvelope", new Vector3(.6f, 1.8f, .6f));
            Field(c, "_shrineSiteInset", 1.5f); Field(c, "_shrineSiteClearance", 2f); Field(c, "_shrineSiteLateralFraction", .2f);
            return c;
        }
        private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static ProceduralTemplateCatalogue Read(string theme) => ProceduralRoomManifestSetup.Parse(
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Kit/" + theme + "Kit.manifest.json"),
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Rooms/" + theme + "Rooms.manifest.json"));
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
