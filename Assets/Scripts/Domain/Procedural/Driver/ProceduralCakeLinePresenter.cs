// ============================================================================
// ProceduralCakeLinePresenter.cs
// ============================================================================
// PURPOSE:
//   Replaces staging sockets with evenly spaced straight runs on walking routes.
//   Uses the completed collision shell rather than template cake markers, so the
//   same route calculation handles authored, carved and coarse rooms without art edits.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Route door approaches and room spines through supported, agent-clear ground.
//   - Compress routes into straight runs and sample deterministic pickup identities.
//   - Reserve exit, doorway and traversal staging space without bridging gaps.
//   - Publish line representatives for bounded native admission and manifest counts.
//   - Rebind threshold-freeze identity to a real line cake in the same room.
// DEPENDENCIES:
//   - Own collision/layout/configuration and Core immutable graph contracts.
// USAGE NOTES:
//   Owner playtest decision 2026-09-30 supersedes PLAN-019 sparse destinations.
//   A 0.25m search lattice is algorithm resolution, not cake spacing. Ground lines
//   never use vaults or ramps as shortcuts; each storey gets its own walking lines. Native
//   admission still proves reachability; no engine APIs or random draws occur here.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralCakeLinePresenter
    {
        private const float Step = .25f;
        private const float Epsilon = .0001f;

        public void Apply(ProceduralLayout layout, ProceduralConfig config, IReadOnlyList<ProceduralBlock> blocks,
            float radius, float height)
        {
            if (layout?.Graph == null || ReferenceEquals(config, null) || blocks == null) throw new ArgumentNullException();
            float spacing = config.RouteCakeSpacing;
            // Knockables are separate native bake sources, not part of the static shell.
            blocks = blocks.Concat(layout.Interactables.Where(p => p.State.Kind == InteractableKind.KnockableProp)
                .Select(p => new ProceduralBlock(p.State.RoomId, ProceduralSurfaceKind.Wall, p.State.Position, p.Size))).ToArray();
            if (!(radius > 0f) || float.IsInfinity(radius) || !(height > 0f) || float.IsInfinity(height) ||
                !(spacing >= Step) || float.IsInfinity(spacing)) throw new ArgumentException("Invalid cake line dimensions.");
            var lines = new List<ProceduralCakeLine>();
            var anchors = new List<LevelAnchor>();
            int identity = 40000000; // Separate from staging, puzzle, Passage and interactable identities.
            foreach (var room in layout.Graph.Rooms.OrderBy(r => r.Id))
            foreach (float elevation in new[] { 0f }.Concat(layout.Storeys.Where(s => s.RoomId == room.Id).Select(s => s.Height)))
            {
                if (room.Pocket) continue; // Passage keeps its separately reserved golden rewards.
                var local = blocks.Where(b => b.HasCollision && OverlapsXZ(b, room.Bounds, height + radius)).ToArray();
                var floors = local.Where(b => b.RoomId == room.Id && b.Kind == ProceduralSurfaceKind.Floor &&
                    b.Role != ProceduralBlockRole.StairRamp && b.Role != ProceduralBlockRole.PlayerOnly &&
                    Math.Abs(b.Center.y + b.Size.y * .5f - elevation) < Epsilon).ToArray();
                var obstacles = local.Where(b => !floors.Contains(b)).ToArray();
                var nodes = new HashSet<Vector2Int>();
                // Include inflated obstacle faces as well as the regular lattice.
                // Otherwise a valid narrow aisle between furniture can fall entirely
                // between 0.25m grid rows. Exact sweeps still prove each edge.
                var xs = Coordinates(0); var zs = Coordinates(2);
                Vector3 Point(Vector2Int key) => new Vector3(xs[key.x], elevation, zs[key.y]);
                float[] Coordinates(int axisIndex)
                {
                    float min = room.Bounds.min[axisIndex], max = room.Bounds.max[axisIndex];
                    var values = new SortedSet<float>();
                    for (int i = (int)Math.Ceiling(min / Step); i <= (int)Math.Floor(max / Step); i++) values.Add(i * Step);
                    foreach (var obstacle in obstacles.Where(b => b.Center.y - b.Size.y * .5f < elevation + height && b.Center.y + b.Size.y * .5f > elevation))
                    {
                        var right = obstacle.Rotation * Vector3.right; var forward = obstacle.Rotation * Vector3.forward;
                        float halfSize = (Math.Abs(right[axisIndex]) * obstacle.Size.x + Math.Abs(forward[axisIndex]) * obstacle.Size.z) * .5f + radius + .002f;
                        foreach (float p in new[] { obstacle.Center[axisIndex] - halfSize, obstacle.Center[axisIndex] + halfSize })
                            if (p > min && p < max && !values.Any(v => Math.Abs(v - p) < Epsilon)) values.Add(p);
                    }
                    return values.ToArray();
                }
                if ((long)xs.Length * zs.Length > 262144)
                    throw new InvalidOperationException("Cake route search exceeds bounded room lattice.");
                for (int x = 0; x < xs.Length; x++) for (int z = 0; z < zs.Length; z++)
                {
                    var key = new Vector2Int(x, z); var p = Point(key);
                    if (room.ContainsXZ(p) && Clear(p, p, floors, obstacles, radius, height)) nodes.Add(key);
                }
                if (nodes.Count == 0) throw new InvalidOperationException("No supported cake route in room " + room.Id);
                Vector2Int Near(Vector3 p, IEnumerable<Vector2Int> source = null) => (source ?? nodes).OrderBy(n => (Point(n) - new Vector3(p.x, elevation, p.z)).sqrMagnitude)
                    .ThenBy(n => n.x).ThenBy(n => n.y).First();
                var walkingDoors = layout.Doors.Where(d => !d.IsOptional && Math.Abs(d.Center.y - elevation) < Epsilon && (d.FromRoomId == room.Id || d.ToRoomId == room.Id))
                    .OrderBy(d => d.Center.x).ThenBy(d => d.Center.z).ToArray();
                // The nearest clear centre can be a tiny isolated space between
                // props. Choose the centre from the entry's walkable component.
                var entry = elevation == 0f ? (walkingDoors.Length == 0 ? room.Center : walkingDoors[0].Center) :
                    layout.VerticalRoutes.Single(r => r.RoomId == room.Id && r.Kind == ProceduralVerticalKind.Ramp).Points.Last();
                var component = Flood(Near(entry));
                var root = Near(room.Center, component.Keys);
                var previous = Flood(root);
                Dictionary<Vector2Int, Vector2Int> Flood(Vector2Int origin)
                {
                    var reached = new Dictionary<Vector2Int, Vector2Int> { [origin] = origin };
                    var queue = new Queue<Vector2Int>(); queue.Enqueue(origin);
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        foreach (var direction in ProceduralTemplateUtility.Directions())
                        {
                            var next = current + direction;
                            if (!nodes.Contains(next) || reached.ContainsKey(next) ||
                                !Clear(Point(current), Point(next), floors, obstacles, radius, height)) continue;
                            reached.Add(next, current); queue.Enqueue(next);
                        }
                    }
                    return reached;
                }
                var targets = new List<Vector2Int>();
                foreach (var door in walkingDoors)
                {
                    var near = Near(door.Center);
                    // A sealed or disconnected door cannot silently produce misleading routes.
                    if (!previous.ContainsKey(near)) throw new InvalidOperationException("Disconnected cake door route in room " + room.Id +
                        " template=" + layout.TemplateRooms.FirstOrDefault(r => r.RoomId == room.Id)?.Template.Id +
                        " root=" + Point(root) + " door=" + Point(near));
                    targets.Add(near);
                }
                bool alongX = room.Size.x >= room.Size.z;
                if (elevation > 0f)
                    alongX = component.Keys.Max(n => xs[n.x]) - component.Keys.Min(n => xs[n.x]) >=
                        component.Keys.Max(n => zs[n.y]) - component.Keys.Min(n => zs[n.y]);
                var axis = alongX ? Vector3.right : Vector3.forward;
                float half = (alongX ? room.Size.x : room.Size.z) * .5f;
                var targetNodes = elevation > 0f ? component.Keys : null;
                if (elevation > 0f)
                    foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                        targets.Add(Near(new Vector3(room.Center.x + x * room.Size.x * .5f, elevation,
                            room.Center.z + z * room.Size.z * .5f), component.Keys));
                targets.Add(Near(Point(root) - axis * half, targetNodes)); targets.Add(Near(Point(root) + axis * half, targetNodes));
                var emitted = new List<Vector3>();
                int before = anchors.Count;
                // Join both halves before sampling: a small room may fit a full
                // row even when neither half from its centre fits two pickups.
                var ends = targets.Skip(targets.Count - 2).ToArray();
                if (previous.ContainsKey(ends[0]) && previous.ContainsKey(ends[1]))
                {
                    var left = Path(ends[0]); var right = Path(ends[1]);
                    int common = 0;
                    while (common < Math.Min(left.Count, right.Count) && left[common] == right[common]) common++;
                    EmitPath(left.Skip(common - 1).Reverse().Concat(right.Skip(common)).ToList());
                }
                foreach (var target in targets.Distinct())
                {
                    if (!previous.ContainsKey(target)) continue; // A disconnected decorative island is not a walking route.
                    EmitPath(Path(target));
                }
                // A gallery's through-route may sit entirely in reserved landing
                // space. Find a straight row in the same reachable component,
                // proving every segment with the unchanged capsule/support test.
                if (elevation > 0f && anchors.Count == before)
                foreach (bool horizontal in new[] { true, false })
                foreach (var row in previous.Keys.GroupBy(n => horizontal ? n.y : n.x).OrderBy(g => g.Key))
                {
                    if (anchors.Count != before) break;
                    var run = new List<Vector2Int>();
                    foreach (var point in row.OrderBy(n => horizontal ? n.x : n.y))
                    {
                        if (run.Count != 0 && ((point - run.Last()).sqrMagnitude != 1 ||
                            !Clear(Point(run.Last()), Point(point), floors, obstacles, radius, height)))
                        { EmitPath(run); run.Clear(); }
                        run.Add(point);
                    }
                    if (run.Count != 0) EmitPath(run);
                }
                // Tiny dead-end closets may have no straight run after doorway
                // clearance. They need no pickup; freeze triggers still require one.
                if (anchors.Count == before && layout.FreezeRooms.Any(f => f.RoomId == room.Id))
                    throw new InvalidOperationException("No cake line fits freeze room " + room.Id);

                List<Vector2Int> Path(Vector2Int target)
                {
                    var path = new List<Vector2Int> { target };
                    while (path[path.Count - 1] != root) path.Add(previous[path[path.Count - 1]]);
                    path.Reverse(); return path;
                }
                void EmitPath(List<Vector2Int> path)
                {
                    int start = 0;
                    for (int i = 1; i <= path.Count; i++)
                    {
                        if (i < path.Count && (i == 1 || path[i] - path[i - 1] == path[i - 1] - path[i - 2])) continue;
                        Emit(Point(path[start]), Point(path[i - 1])); start = i - 1;
                    }
                }

                void Emit(Vector3 from, Vector3 to)
                {
                    float length = Vector3.Distance(from, to);
                    if (length < spacing) return;
                    var direction = (to - from) / length;
                    int count = (int)Math.Floor(length / spacing) + 1;
                    float inset = (length - (count - 1) * spacing) * .5f;
                    var run = new List<LevelAnchor>();
                    for (int i = 0; i < count; i++)
                    {
                        var p = from + direction * (inset + i * spacing);
                        bool reserved = Reserved(layout, room.Id, p, config, radius, height) || local.Any(b =>
                            b.TraversalKind != TraversalSurfaceKind.None && NearTraversal(p, b, radius, height)) ||
                            emitted.Any(other => (other - p).sqrMagnitude < spacing * spacing - Epsilon);
                        if (reserved) { Flush(); continue; }
                        var anchor = new LevelAnchor(++identity, room.Id, CakeAnchorType.Flow, p + Vector3.up * config.AnchorHeight);
                        run.Add(anchor); anchors.Add(anchor); emitted.Add(p);
                    }
                    Flush();
                    void Flush()
                    {
                        if (run.Count == 0) return;
                        lines.Add(new ProceduralCakeLine(Array.AsReadOnly(run.ToArray()))); run.Clear();
                    }
                }
            }
            layout.Graph = LevelGraphUtility.Build(layout.Graph.Rooms, layout.Graph.Edges, anchors,
                layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
            layout.CakeLines = lines.AsReadOnly();
            layout.FreezeRooms = layout.FreezeRooms.Select(f => new ProceduralFreezePlan(f.DoorIndex, f.RoomId, f.BehindRoomId,
                anchors.Where(a => a.RoomId == f.RoomId).OrderBy(a => (a.Position - layout.Doors[f.DoorIndex].Center).sqrMagnitude)
                    .ThenBy(a => a.Id).First().Id, f.Hunter)).ToArray();
            layout.InteractableManifest += "|cake-lines-v1|spacing=" + spacing.ToString("R", CultureInfo.InvariantCulture) +
                "|cakes=" + anchors.Count + "|lines=" + lines.Count + string.Concat(anchors.Select(a => "|Cake:" + a.Id + "," + a.RoomId + "," +
                    a.Position.x.ToString("R", CultureInfo.InvariantCulture) + "," + a.Position.z.ToString("R", CultureInfo.InvariantCulture)));
        }

        private static bool Reserved(ProceduralLayout layout, int roomId, Vector3 point, ProceduralConfig config, float radius, float height)
        {
            float clearance = Math.Max(config.TemplateExitCakeClearance, radius * 2f);
            var exitDelta = point - layout.Graph.ExitPosition; exitDelta.y = 0f;
            if (roomId == layout.Graph.ExitRoomId && Math.Abs(point.y - layout.Graph.ExitPosition.y) < 3.21f && exitDelta.sqrMagnitude < clearance * clearance) return true;
            var spawnDelta = point - layout.PlayerSpawnPosition; spawnDelta.y = 0f;
            if (Math.Abs(point.y - layout.PlayerSpawnPosition.y) < height && spawnDelta.sqrMagnitude < clearance * clearance) return true;
            foreach (var door in layout.Doors.Where(d => d.FromRoomId == roomId || d.ToRoomId == roomId))
            {
                if (Math.Abs(point.y - door.Center.y) >= config.DoorHeight) continue;
                var d = point - door.Center;
                float across = Math.Abs(door.AlongX ? d.z : d.x), along = Math.Abs(door.AlongX ? d.x : d.z);
                if (across < radius * 2f && along < config.DoorWidth * .5f + radius) return true;
            }
            foreach (var route in layout.VerticalRoutes.Where(r => r.RoomId == roomId))
                if (route.Points.Any(p => (p - point).sqrMagnitude < radius * radius * 4f)) return true;
            return false;
        }

        private static bool Clear(Vector3 from, Vector3 to, ProceduralBlock[] floors, ProceduralBlock[] obstacles, float radius, float height)
        {
            // Exact interval union along nine standing-square traces: seams pass,
            // but even a narrow unsupported gap along a run fails closed.
            foreach (float x in new[] { -radius, 0f, radius }) foreach (float z in new[] { -radius, 0f, radius })
                if (!Supported(from + new Vector3(x, -.01f, z), to + new Vector3(x, -.01f, z), floors)) return false;
            from.y += height * .5f; to.y += height * .5f;
            foreach (var block in obstacles)
                // Do not admit exact tangencies: quarter-turn float error otherwise
                // creates isolated boundary nodes that cannot be reached by a sweep.
                if (ProceduralNavFallbackPresenter.Blocked(block, from, to, radius + .001f, height)) return false;
            return true;
        }

        private static bool Supported(Vector3 from, Vector3 to, ProceduralBlock[] floors)
        {
            var intervals = new List<(float low, float high)>();
            foreach (var floor in floors)
            {
                var q = floor.Rotation; var inverse = new Quaternion(-q.x, -q.y, -q.z, q.w);
                var a = inverse * (from - floor.Center); var b = inverse * (to - floor.Center);
                float low = 0f, high = 1f; bool intersects = true;
                for (int axis = 0; axis < 3; axis++)
                {
                    float half = floor.Size[axis] * .5f + Epsilon, delta = b[axis] - a[axis];
                    if (Math.Abs(delta) < Epsilon)
                    { if (Math.Abs(a[axis]) > half) { intersects = false; break; } continue; }
                    float enter = (-half - a[axis]) / delta, leave = (half - a[axis]) / delta;
                    low = Math.Max(low, Math.Min(enter, leave)); high = Math.Min(high, Math.Max(enter, leave));
                    if (low > high) { intersects = false; break; }
                }
                if (intersects)
                {
                    if (low <= Epsilon && high >= 1f - Epsilon) return true;
                    intervals.Add((low, high));
                }
            }
            float reached = 0f;
            foreach (var interval in intervals.OrderBy(i => i.low))
            {
                if (interval.low > reached + Epsilon) return false;
                reached = Math.Max(reached, interval.high);
                if (reached >= 1f - Epsilon) return true;
            }
            return false;
        }

        private static bool NearTraversal(Vector3 p, ProceduralBlock block, float radius, float height)
        {
            if (p.y > Math.Max(block.EndpointA.y, block.EndpointB.y) + height ||
                p.y + height < Math.Min(block.EndpointA.y, block.EndpointB.y)) return false;
            var a = block.EndpointA; var b = block.EndpointB; a.y = b.y = p.y;
            var delta = b - a;
            float t = delta.sqrMagnitude < Epsilon ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, delta) / delta.sqrMagnitude);
            return (p - (a + delta * t)).sqrMagnitude < radius * radius * 4f;
        }
        private static bool OverlapsXZ(ProceduralBlock block, Bounds bounds, float padding)
        {
            // Bounding sphere radius conservatively includes rotated boxes in the broad phase.
            float reach = block.Size.magnitude * .5f + padding;
            return block.Center.x + reach >= bounds.min.x && block.Center.x - reach <= bounds.max.x &&
                block.Center.z + reach >= bounds.min.z && block.Center.z - reach <= bounds.max.z;
        }
    }
}
