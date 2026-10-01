// ============================================================================
// ProceduralTemplateStoreyUtility.cs
// ============================================================================
// PURPOSE:
//   Fits the existing ramp-and-gallery storey contract inside authored rooms.
//   Furniture is never removed to make a stair fit: ascent wells, landings and
//   ground door approaches must be clear before an upper gallery is selected.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Choose supported template gallery origins with injected seeded probability.
//   - Reuse organic vertical routes and extend room headroom without moving props.
//   - Preserve source catalogue objects and stable room/collapse identities.
// DEPENDENCIES:
//   - Own template/layout/configuration and Core graph values only.
// USAGE NOTES:
//   The existing eight-metre storey module is fitted in world axes. Small rooms,
//   refuges and pockets stay flat. Exit approach/swing stays clear on the ground.
//   Hoist invariant footprint work only; candidate order, random draws and fit
//   predicates stay identical so performance changes cannot choose another gallery.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralTemplateStoreyUtility
    {
        public static void Apply(ProceduralLayout layout, ProceduralConfig config, System.Random random, Action<string> trace = null)
        {
            if (layout.RoundIndex < config.MultiFloorStartRound || config.StoreyProbability <= 0f) return;
            var plans = new List<ProceduralStoreyPlan>();
            var selectedExit = layout.Graph.ExitPosition;
            foreach (var room in layout.TemplateRooms)
            {
                var catalogue = room.Catalogue ?? layout.TemplateCatalogue;
                var kit = catalogue.Kit.ToDictionary(p => p.Id);
                if (room.PocketId != 0 || room.Template.ReservedAreas.Length != 0 ||
                    layout.Modules.Single(m => m.RoomId == room.RoomId).Kind == ProceduralModuleKind.MerchantRefuge) continue;
                if (random.NextDouble() >= config.StoreyProbability) continue;
                // The canonical exit is taller than the ordinary 3.2m gallery.
                // Use the existing maximum supported two-ledge rise above its hub.
                float rise = room.RoomId == layout.Graph.ExitRoomId ? Math.Max(config.StoreyHeight, 3.6f) : config.StoreyHeight;
                var cells = new HashSet<Vector2Int>(ProceduralTemplateUtility.OccupiedCells(room));
                var bounds = layout.Graph.Rooms.Single(r => r.Id == room.RoomId).Bounds;
                var obstacles = room.Template.Pieces.Where(p => kit[p.Id].Kind != "floor" && kit[p.Id].Kind != "ceiling" &&
                    kit[p.Id].Kind != "decal").Select(p =>
                {
                    var piece = kit[p.Id]; double angle = (p.RotY + room.Turns * 90f) * Math.PI / 180d;
                    var size = new Vector3((float)(Math.Abs(Math.Cos(angle)) * piece.Size.x + Math.Abs(Math.Sin(angle)) * piece.Size.z), piece.Size.y,
                        (float)(Math.Abs(Math.Sin(angle)) * piece.Size.x + Math.Abs(Math.Cos(angle)) * piece.Size.z));
                    return new Bounds(ProceduralTemplateUtility.Point(room, p.Position, layout.Origin) + Vector3.up * (piece.Size.y * .5f), size);
                }).ToArray();
                IReadOnlyList<Vector2Int> groundNodes = null;
                bool found = false;
                int candidates = 0, collision = 0, sockets = 0, portals = 0, disconnected = 0, clearSockets = 0;
                int firstDrop = random.Next(4);
                bool Supported(float x, float z)
                {
                    // Footprint support depends on the origin, not the 64 orientation/
                    // mirror/advance/drop variants. Keep their original nested order.
                    bool supported = true;
                    for (int dx = -4; dx <= 4; dx++) for (int dz = -4; dz <= 4; dz++)
                        supported &= cells.Contains(new Vector2Int((int)Math.Floor(x - layout.Origin.x + dx), (int)Math.Floor(z - layout.Origin.y + dz)));
                    return supported;
                }
                for (float x = bounds.min.x + 4.25f; x <= bounds.max.x - 4.25f && !found; x += .25f)
                for (float z = bounds.min.z + 4.25f; z <= bounds.max.z - 4.25f && !found; z += .25f)
                if (Supported(x, z))
                for (int turn = 0; turn < 4 && !found; turn++)
                for (int mirror = 0; mirror < 2 && !found; mirror++)
                for (int advance = 0; advance <= 2 && !found; advance += 2)
                for (int drop = 0; drop < 4 && !found; drop++)
                {
                    var origin = new Vector3(x, 0f, z);
                    candidates++;
                    var plan = new ProceduralStoreyPlan(room.RoomId, origin, rise,
                        (ProceduralVerticalKind)((int)ProceduralVerticalKind.FloorHole + (firstDrop + drop) % 4), turn, mirror == 1, advance);
                    var routes = ProceduralStoreyUtility.Routes(plan, config);
                    var reserved = new List<Bounds>
                    {
                        Reserve(new Vector3(3f, (rise * .5f + 2f) * .5f, -1f + advance), new Vector3(2f, rise * .5f + 2f, 2f)),
                        Reserve(new Vector3(3f, rise * .75f, advance), new Vector3(2f, rise * .5f, .1f))
                    };
                    // Reserve the sloped walking volume, not an empty floor-to-roof
                    // box under the entire ascent. Low furniture can remain below it.
                    for (float step = -4f; step < 1.6f; step += .2f)
                    {
                        float low = Math.Max(0f, Math.Min(1f, (step + 3.6f) / 4.8f)) * rise - .3f;
                        float high = Math.Max(0f, Math.Min(1f, (step + .2f + 3.6f) / 4.8f)) * rise + 2f;
                        reserved.Add(Reserve(new Vector3(-3f, (low + high) * .5f, step + .1f), new Vector3(2f, high - low, .2f)));
                    }
                    Bounds Reserve(Vector3 p, Vector3 size)
                    {
                        var rotated = ProceduralTemplateUtility.Rotate(size, turn);
                        return new Bounds(ProceduralStoreyUtility.Point(plan, p), new Vector3(Math.Abs(rotated.x), size.y, Math.Abs(rotated.z)));
                    }
                    // Standing reservations are destinations, not walls: do not
                    // erase their ground nodes (or inflate their clearance twice).
                    var physical = new List<Bounds>(reserved);
                    if (plan.Drop == ProceduralVerticalKind.CollapsedRamp)
                    {
                        var broken = Reserve(new Vector3(0f, rise * .75f - .15f, -1.75f), new Vector3(2f, rise * .5f + .3f, 1.8f));
                        reserved.Add(broken); physical.Add(broken);
                    }
                    if (plan.Drop == ProceduralVerticalKind.Shaft)
                        foreach (float side in new[] { -.95f, .95f })
                        {
                            var wall = Reserve(new Vector3(side, (rise - .3f) * .5f, 1.9f), new Vector3(.3f, rise - .3f, 1.4f));
                            reserved.Add(wall); physical.Add(wall);
                        }
                    reserved.AddRange(routes.SelectMany(r => r.Points).Select(p => new Bounds(p + Vector3.up * 1.01f, new Vector3(1.1f, 2f, 1.1f))));
                    if (reserved.Any(r => obstacles.Any(o => o.Intersects(r)))) { collision++; continue; }
                    if (!UpperUsable(plan, routes, obstacles, config.RouteCakeSpacing)) { disconnected++; continue; }
                    var player = layout.PlayerSpawnPosition; var exit = selectedExit;
                    bool Connected(Vector3 p, Vector3 e)
                    {
                        // Furniture and footprint are unchanged for every candidate in
                        // this room. Filter only its physical reservations each time.
                        if (groundNodes == null) groundNodes = GroundNodes(room, layout, obstacles);
                        return GroundConnected(room, layout, groundNodes, physical, p, e);
                    }
                    float Facing(Vector3 p, Vector3 e) => (ProceduralExitHubUtility.ApproachYaw(
                        ProceduralTemplateUtility.Rotate(p - e, (4 - room.Turns) % 4), Vector3.zero) + room.Turns * 90f) % 360f;
                    bool SocketClear(Vector3 p, Vector3 e) => !physical.Any(r =>
                        r.Intersects(ProceduralExitHubUtility.DoorEnvelope(e, Facing(p, e))) ||
                        r.Intersects(new Bounds(p + Vector3.up, new Vector3(1.1f, 2f, 1.1f))));
                    if (room.RoomId == layout.Graph.ExitRoomId && !SocketClear(player, exit))
                    {
                        Vector3 World(Vector3 p) => ProceduralTemplateUtility.Point(room, p, layout.Origin);
                        if (!ProceduralExitHubUtility.TrySelect(catalogue, room.Template, config.TemplateExitClearance,
                            config.TemplateExitSpawnDistance, config.TemplateExitCakeClearance, config.DoorHeight, out var p, out var e,
                            (p0, e0) => { if (!SocketClear(World(p0), World(e0))) return false; clearSockets++;
                                return Connected(World(p0), World(e0)); })) { sockets++; continue; }
                        player = World(p) + Vector3.up * config.SpawnHeight; exit = World(e);
                    }
                    if (layout.Doors.Where(d => d.FromRoomId == room.RoomId || d.ToRoomId == room.RoomId)
                        .Any(d => physical.Any(r => r.min.y < 2f &&
                            Math.Abs(r.center.x - d.Center.x) <= r.extents.x + (d.AlongX ? .502f : 1.502f) &&
                            Math.Abs(r.center.z - d.Center.z) <= r.extents.z + (d.AlongX ? 1.502f : .502f)))) { portals++; continue; }
                    if (!Connected(player, exit)) { disconnected++; continue; }
                    if (room.RoomId == layout.Graph.ExitRoomId)
                    {
                        layout.PlayerSpawnPosition = player; selectedExit = exit;
                        layout.ExitDoorYaw = Facing(player, exit);
                        double facing = Math.Atan2(exit.x - player.x, exit.z - player.z) * .5d;
                        layout.PlayerSpawnRotation = new Quaternion(0f, (float)Math.Sin(facing), 0f, (float)Math.Cos(facing));
                    }
                    plans.Add(plan); found = true;
                }
                if (candidates > 0) trace?.Invoke($"STOREY_FIT {room.Template.Id} candidates={candidates} collision={collision} sockets={sockets} clearSockets={clearSockets} portals={portals} disconnected={disconnected} fit={found}");
            }
            layout.Storeys = plans.AsReadOnly();
            layout.VerticalRoutes = plans.SelectMany(s => ProceduralStoreyUtility.Routes(s, config)).ToArray();
            var rooms = layout.Graph.Rooms.Select(r =>
            {
                if (!plans.Any(s => s.RoomId == r.Id)) return r;
                float rise = plans.Single(s => s.RoomId == r.Id).Height;
                float h = Math.Max(r.Size.y + rise, rise + 2.8f);
                return new LevelRoom(r.Id, new Vector3(r.Center.x, h * .5f, r.Center.z), new Vector3(r.Size.x, h, r.Size.z),
                    cells: r.Cells.Select(b => new Bounds(new Vector3(b.center.x, h * .5f, b.center.z), new Vector3(b.size.x, h, b.size.z))).ToArray(), pocket: r.Pocket);
            }).ToArray();
            layout.Graph = LevelGraphUtility.Build(rooms, layout.Graph.Edges, layout.Graph.Anchors, layout.Graph.ExitRoomId, selectedExit);
        }
        private static bool UpperUsable(ProceduralStoreyPlan plan, IReadOnlyList<ProceduralVerticalRoute> routes, Bounds[] furniture, float spacing)
        {
            var nodes = new HashSet<Vector2Int>();
            Vector3 World(Vector2Int n) => ProceduralStoreyUtility.Point(plan, new Vector3(n.x * .25f, plan.Height, n.y * .25f));
            bool hole = plan.Drop == ProceduralVerticalKind.FloorHole || plan.Drop == ProceduralVerticalKind.Shaft;
            bool Slab(float x, float z) => Math.Abs(x) < 4f && Math.Abs(z) < 4f &&
                !(x < -2f && z < 1.2f || x > 2f && z < plan.LedgeAdvance || Math.Abs(x) < .8f && (hole ? z > 1.2f && z < 2.6f : z < -1f));
            for (int x = -14; x <= 14; x++) for (int z = -14; z <= 14; z++)
            {
                var n = new Vector2Int(x, z); var p = World(n);
                bool support = true;
                foreach (float dx in new[] { -.51f, .51f }) foreach (float dz in new[] { -.51f, .51f })
                    support &= Slab(x * .25f + dx, z * .25f + dz);
                if (support && !furniture.Any(b => b.max.y > plan.Height && b.min.y < plan.Height + 2f &&
                    Math.Abs(p.x - b.center.x) < b.extents.x + .51f && Math.Abs(p.z - b.center.z) < b.extents.z + .51f)) nodes.Add(n);
            }
            if (nodes.Count == 0) return false;
            var entry = routes.Single(r => r.Kind == ProceduralVerticalKind.Ramp).Points.Last();
            var root = nodes.OrderBy(n => (World(n) - entry).sqrMagnitude).First();
            if ((World(root) - entry).sqrMagnitude > 1f) return false;
            var reached = new HashSet<Vector2Int> { root }; var queue = new Queue<Vector2Int>(); queue.Enqueue(root);
            var directions = ProceduralTemplateUtility.Directions().ToArray();
            while (queue.Count != 0)
            {
                var n = queue.Dequeue();
                foreach (var d in directions) if (nodes.Contains(n + d) && reached.Add(n + d)) queue.Enqueue(n + d);
            }
            var routePoints = routes.SelectMany(r => r.Points).ToArray();
            foreach (bool horizontal in new[] { true, false })
            foreach (var row in reached.GroupBy(n => horizontal ? n.y : n.x))
            {
                int run = 0, last = int.MinValue;
                foreach (var n in row.OrderBy(n => horizontal ? n.x : n.y))
                {
                    int at = horizontal ? n.x : n.y;
                    bool clear = !routePoints.Any(p => (p - World(n)).sqrMagnitude < 1.01f);
                    run = clear ? (at == last + 1 ? run + 1 : 1) : 0; last = at;
                    if ((run - 1) * .25f >= spacing) return true;
                }
            }
            return false;
        }
        private static IReadOnlyList<Vector2Int> GroundNodes(ProceduralTemplateRoom room, ProceduralLayout layout, Bounds[] furniture)
        {
            var obstacles = furniture.Where(b => b.min.y < 2f && b.max.y > .01f).ToArray();
            var cells = new HashSet<Vector2Int>(ProceduralTemplateUtility.OccupiedCells(room));
            var nodes = new List<Vector2Int>();
            Vector3 Point(Vector2Int key) => new Vector3(key.x * .25f + layout.Origin.x, 0f, key.y * .25f + layout.Origin.y);
            // Recomputing Max across the footprint at every quarter-metre node
            // made a failed ground fit quadratic before its flood fill even began.
            int minX = cells.Min(c => c.x) * 4, maxX = (cells.Max(c => c.x) + 1) * 4;
            int minZ = cells.Min(c => c.y) * 4, maxZ = (cells.Max(c => c.y) + 1) * 4;
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                var key = new Vector2Int(x, z); var p = Point(key);
                if (obstacles.Any(b => Math.Abs(p.x - b.center.x) < b.extents.x + .5f && Math.Abs(p.z - b.center.z) < b.extents.z + .5f)) continue;
                bool inside = true;
                foreach (float dx in new[] { -.51f, .51f }) foreach (float dz in new[] { -.51f, .51f })
                    inside &= cells.Contains(new Vector2Int((int)Math.Floor(p.x - layout.Origin.x + dx), (int)Math.Floor(p.z - layout.Origin.y + dz)));
                if (inside) nodes.Add(key);
            }
            return nodes;
        }
        private static bool GroundConnected(ProceduralTemplateRoom room, ProceduralLayout layout, IReadOnlyList<Vector2Int> groundNodes,
            List<Bounds> reserved, Vector3 player, Vector3 exit)
        {
            var obstacles = reserved.Where(b => b.min.y < 2f && b.max.y > .01f).ToArray();
            var nodes = new HashSet<Vector2Int>();
            Vector3 Point(Vector2Int key) => new Vector3(key.x * .25f + layout.Origin.x, 0f, key.y * .25f + layout.Origin.y);
            // Preserve x/z insertion order (and nearest-node tie breaking) while
            // applying the exact same clearance predicate to candidate reservations.
            foreach (var key in groundNodes)
            {
                var p = Point(key);
                if (!obstacles.Any(b => Math.Abs(p.x - b.center.x) < b.extents.x + .5f && Math.Abs(p.z - b.center.z) < b.extents.z + .5f)) nodes.Add(key);
            }
            if (nodes.Count == 0) return false;
            var targets = room.OpenDoors.Select(i =>
            {
                var door = room.Template.Doors[i]; var n = ProceduralTemplateUtility.Direction(door.Side);
                return ProceduralTemplateUtility.Point(room, ProceduralTemplateUtility.Door(door) - new Vector3(n.x, 0f, n.y), layout.Origin);
            }).Concat(room.Template.HunterSpawn.Select(p => ProceduralTemplateUtility.Point(room, p, layout.Origin))).ToArray();
            if (room.RoomId == layout.Graph.ExitRoomId) targets = targets.Concat(new[] { player, exit }).ToArray();
            if (targets.Length == 0) return true;
            var nearest = targets.Select(p => nodes.OrderBy(n => (Point(n) - p).sqrMagnitude).First()).ToArray();
            for (int i = 0; i < targets.Length; i++) if ((Point(nearest[i]) - targets[i]).sqrMagnitude > .26f) return false;
            var reached = new HashSet<Vector2Int> { nearest[0] }; var queue = new Queue<Vector2Int>(); queue.Enqueue(nearest[0]);
            var directions = ProceduralTemplateUtility.Directions().ToArray();
            while (queue.Count != 0)
            {
                var key = queue.Dequeue();
                foreach (var d in directions) if (nodes.Contains(key + d) && reached.Add(key + d)) queue.Enqueue(key + d);
            }
            return nearest.All(reached.Contains);
        }
    }
}
