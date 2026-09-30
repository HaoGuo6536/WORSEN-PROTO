// ============================================================================
// ProceduralStoreyUtility.cs
// ============================================================================
// PURPOSE:
//   Adds a second walkable storey to selected connected extension cells. A local
//   region graph distinguishes upstairs from downstairs so a room-level walking
//   edge cannot conceal a one-way trap or an item-dependent objective route.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Sample storeys with injected randomness without changing primary-cell modules.
//   - Describe ramps, staged ledges, optional rebounds and four downward alternatives.
//   - Validate directed base-kit objective/exit routes and retain reproducible plans.
// DEPENDENCIES:
//   - Own config/layout and Core graph values; no Player assembly or engine calls.
// USAGE NOTES:
//   One upper storey per eligible multi-cell castle room; hubs, refuges and pockets
//   are unchanged. Core room identity remains the collapse unit for both storeys.
//   Region ids are local validation nodes, not new published Core room identities.
//   Rebound routes are optional and never counted towards required reachability.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralStoreyUtility
    {
        public static void Apply(ProceduralLayout layout, ProceduralConfig config, System.Random random)
        {
            ValidateConfig(config);
            if (!config.CastleModules || layout.RoundIndex < config.MultiFloorStartRound) return;
            var storeys = new List<ProceduralStoreyPlan>();
            var routes = new List<ProceduralVerticalRoute>();
            var anchors = layout.Graph.Anchors.ToArray();
            foreach (var module in layout.Modules)
            {
                if (module.PocketId != 0 || module.Kind == ProceduralModuleKind.MerchantRefuge ||
                    module.RoomId == layout.Graph.ExitRoomId || module.Cells.Count < 2) continue;
                if (random.NextDouble() >= config.StoreyProbability) continue;
                var cell = module.Cells[1];
                var origin = new Vector3(layout.Origin.x + cell.x * layout.CellSize, 0f,
                    layout.Origin.y + cell.y * layout.CellSize);
                var storey = new ProceduralStoreyPlan(module.RoomId, origin, config.StoreyHeight,
                    (ProceduralVerticalKind)((int)ProceduralVerticalKind.FloorHole + random.Next(4)));
                storeys.Add(storey);
                routes.AddRange(Routes(storey, config));
                // Move an existing socket only when vertical objectives are enabled;
                // otherwise preserve the original ground type and its placement semantics.
                int index = Array.FindIndex(anchors, a => a.RoomId == module.RoomId &&
                    Mathf.Abs(a.Position.x - origin.x) < layout.CellSize * 0.5f &&
                    Mathf.Abs(a.Position.z - origin.z) < layout.CellSize * 0.5f);
                if (index >= 0 && config.VerticalPreference > 0f)
                    anchors[index] = new LevelAnchor(anchors[index].Id, module.RoomId, CakeAnchorType.Vertical,
                        origin + new Vector3(-3f, storey.Height + config.AnchorHeight, 2.6f));
            }
            layout.Storeys = storeys.AsReadOnly();
            layout.VerticalRoutes = routes.AsReadOnly();
            layout.Graph = LevelGraphUtility.Build(layout.Graph.Rooms, layout.Graph.Edges, anchors,
                layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
        }

        public static IReadOnlyList<ProceduralVerticalRoute> Routes(ProceduralStoreyPlan storey, ProceduralConfig config)
        {
            var result = new List<ProceduralVerticalRoute>();
            int lower = storey.RoomId, upper = storey.UpperRegionId;
            float h = storey.Height, reach = config.BaseLedgeReach * 0.4f;
            Add(ProceduralVerticalKind.Ramp, lower, upper, true, TraversalAccess.All,
                new Vector3(-3f, 0f, -3.6f), new Vector3(-3f, h, 1.2f));
            var climb = new[] { new Vector3(3f, 0f, -2f - reach), new Vector3(3f, h * 0.5f, -2f + reach),
                new Vector3(3f, h * 0.5f, -reach), new Vector3(3f, h, reach) };
            Add(ProceduralVerticalKind.LedgeClimb, lower, upper, false, TraversalAccess.Player, climb);
            if (config.BaseReboundSupported)
                Add(ProceduralVerticalKind.ReboundClimb, lower, upper, false, TraversalAccess.Player, climb);
            bool hole = storey.Drop == ProceduralVerticalKind.FloorHole || storey.Drop == ProceduralVerticalKind.Shaft;
            if (storey.Drop == ProceduralVerticalKind.CollapsedRamp)
                Add(storey.Drop, upper, lower, false, TraversalAccess.Player,
                    new Vector3(0f, h, -0.3f), new Vector3(0f, h * 0.5f, -2.5f), new Vector3(0f, 0f, -3.3f));
            else Add(storey.Drop, upper, lower, false, TraversalAccess.Player,
                new Vector3(0f, h, hole ? 0.4f : 0.05f), new Vector3(0f, 0f, hole ? 2f : -2.05f));
            return result.AsReadOnly();

            void Add(ProceduralVerticalKind kind, int from, int to, bool both, TraversalAccess access, params Vector3[] points)
                => result.Add(new ProceduralVerticalRoute(90000 + lower * 10 + (int)kind, lower, kind, from, to,
                    both, access, Array.AsReadOnly(points.Select(p => p + storey.Origin).ToArray())));
        }

        public static void Validate(ProceduralLayout layout, ProceduralConfig config)
        {
            ValidateConfig(config);
            // Check objectives first so forced one-way traps retain an actionable retry reason.
            ValidateObjectives(layout);
            foreach (var storey in layout.Storeys)
            {
                var room = layout.Graph.Rooms.Single(r => r.Id == storey.RoomId);
                var module = layout.Modules.Single(m => m.RoomId == storey.RoomId);
                if (module.PocketId != 0 || storey.Height != config.StoreyHeight ||
                    room.Bounds.max.y < storey.Height + 2.8f || !room.Bounds.Contains(storey.Origin + Vector3.up))
                    throw new InvalidOperationException("Storey has no connected footprint or standing headroom.");
                var routes = layout.VerticalRoutes.Where(r => r.RoomId == storey.RoomId).ToArray();
                if (!routes.Any(r => r.Kind == ProceduralVerticalKind.Ramp && r.Access == TraversalAccess.All && r.Bidirectional) ||
                    !routes.Any(r => r.Kind == ProceduralVerticalKind.LedgeClimb) || !routes.Any(r => r.Kind == storey.Drop))
                    throw new InvalidOperationException("Every storey needs a hunter ramp, a ledge climb and a drop.");
                foreach (var route in routes)
                {
                    bool down = route.Kind >= ProceduralVerticalKind.FloorHole;
                    if (route.FromRegion != (down ? storey.UpperRegionId : storey.RoomId) ||
                        route.ToRegion != (down ? storey.RoomId : storey.UpperRegionId))
                        throw new InvalidOperationException("Vertical route direction disagrees with its storeys.");
                    if (route.Kind != ProceduralVerticalKind.Ramp && (route.Access != TraversalAccess.Player || route.Bidirectional))
                        throw new InvalidOperationException("Vertical shortcuts must be directed and player-only.");
                    if (route.Points == null || route.Points.Count < 2 || route.Points.Any(p => !Finite(p.x) || !Finite(p.y) || !Finite(p.z)))
                        throw new InvalidOperationException("Vertical route has invalid endpoints.");
                    if (Region(layout, storey.RoomId, route.Points[0]) != route.FromRegion ||
                        Region(layout, storey.RoomId, route.Points.Last()) != route.ToRegion)
                        throw new InvalidOperationException("Vertical endpoints disagree with their region identities.");
                    if (route.Kind == ProceduralVerticalKind.LedgeClimb || route.Kind == ProceduralVerticalKind.ReboundClimb)
                        for (int i = 1; i < route.Points.Count; i++)
                        {
                            var delta = route.Points[i] - route.Points[i - 1];
                            if (delta.y == 0f) continue; // Walking across the staging platform.
                            if (delta.y < config.BaseLedgeMinimumHeight || delta.y > config.BaseLedgeMaximumHeight ||
                                new Vector2(delta.x, delta.z).magnitude > config.BaseLedgeReach)
                                throw new InvalidOperationException("Climb exceeds the mirrored base ledge envelope.");
                        }
                    if (route.Kind >= ProceduralVerticalKind.FloorHole &&
                        (route.FromRegion != storey.UpperRegionId || route.ToRegion != storey.RoomId ||
                         route.Points.Last().y != 0f || ProceduralFootprintUtility.At(layout, route.Points.Last()).Id != storey.RoomId))
                        throw new InvalidOperationException("One-way drop has no valid lower landing.");
                }
            }
        }

        public static void ValidateObjectives(ProceduralLayout layout)
        {
            var rooms = layout.Graph.Rooms.Concat(layout.Storeys.Select(s => new LevelRoom(s.UpperRegionId,
                s.Origin + Vector3.up * s.Height, new Vector3(8f, 1f, 8f)))).ToArray();
            var edges = layout.Graph.Edges.Concat(layout.VerticalRoutes.Where(r => r.Kind != ProceduralVerticalKind.ReboundClimb)
                .Select(r => new LevelEdge(r.Id, r.FromRegion, r.ToRegion, r.Bidirectional, r.Access))).ToArray();
            int exit = Region(layout, layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
            var graph = LevelGraphUtility.Build(rooms, edges, Array.Empty<LevelAnchor>(), exit, layout.Graph.ExitPosition);
            int spawn = Region(layout, ProceduralFootprintUtility.At(layout, layout.PlayerSpawnPosition).Id, layout.PlayerSpawnPosition);
            var from = LevelGraphUtility.TopologicalDistancesFrom(graph, spawn, TraversalAccess.Player);
            var to = LevelGraphUtility.DistancesTo(graph, exit, TraversalAccess.Player);
            if (to[spawn] < 0) throw new InvalidOperationException("Player spawn has no base-kit exit route.");
            foreach (var anchor in layout.Graph.Anchors)
            {
                int region = Region(layout, anchor.RoomId, anchor.Position);
                if (from[region] < 0 || to[region] < 0)
                    throw new InvalidOperationException("Required anchor " + anchor.Id + " has no directed base-kit spawn/exit route (one-way trap).");
            }
            foreach (var drop in layout.VerticalRoutes.Where(r => r.Kind >= ProceduralVerticalKind.FloorHole))
                if (to[drop.ToRegion] < 0) throw new InvalidOperationException("One-way drop landing has no onward exit route.");
            var hunters = LevelGraphUtility.DistancesTo(graph, spawn, TraversalAccess.Hunter);
            foreach (var position in layout.HunterSpawnPositions)
                if (hunters[Region(layout, ProceduralFootprintUtility.At(layout, position).Id, position)] < 0)
                    throw new InvalidOperationException("Hunter spawn has no hunter-legal route to the player region.");
        }

        public static int Region(ProceduralLayout layout, int roomId, Vector3 position)
        {
            foreach (var s in layout.Storeys)
                if (s.RoomId == roomId && position.y >= s.Height && Mathf.Abs(position.x - s.Origin.x) <= 4f &&
                    Mathf.Abs(position.z - s.Origin.z) <= 4f) return s.UpperRegionId;
            return roomId;
        }

        public static string Manifest(ProceduralLayout layout, ProceduralConfig config)
        {
            var text = new StringBuilder("|storeys-v1:").Append(config.MultiFloorStartRound).Append(',').Append(config.BaseReboundSupported ? 1 : 0);
            Values(config.StoreyProbability, config.StoreyHeight, config.BaseLedgeMinimumHeight, config.BaseLedgeMaximumHeight, config.BaseLedgeReach);
            foreach (var s in layout.Storeys)
            { text.Append("|Storey:").Append(s.RoomId).Append(',').Append(s.UpperRegionId).Append(',').Append((int)s.Drop); Values(s.Origin.x, s.Origin.y, s.Origin.z, s.Height); }
            foreach (var r in layout.VerticalRoutes)
            {
                text.Append("|Vertical:").Append(r.Id).Append(',').Append((int)r.Kind).Append(',').Append(r.FromRegion).Append(',')
                    .Append(r.ToRegion).Append(',').Append(r.Bidirectional ? 1 : 0).Append(',').Append((int)r.Access);
                foreach (var p in r.Points) Values(p.x, p.y, p.z);
            }
            return text.ToString();
            void Values(params float[] values) { foreach (float v in values) text.Append(',').Append(v.ToString("R", CultureInfo.InvariantCulture)); }
        }

        private static void ValidateConfig(ProceduralConfig c)
        {
            if (c.MultiFloorStartRound < 1 || !Finite(c.StoreyProbability) || c.StoreyProbability < 0f || c.StoreyProbability > 1f ||
                !Finite(c.StoreyHeight) || c.StoreyHeight < 3f || c.StoreyHeight > 3.6f ||
                !Finite(c.BaseLedgeMinimumHeight) || c.BaseLedgeMinimumHeight <= 0f ||
                !Finite(c.BaseLedgeMaximumHeight) || c.StoreyHeight * 0.5f < c.BaseLedgeMinimumHeight || c.StoreyHeight * 0.5f > c.BaseLedgeMaximumHeight ||
                !Finite(c.BaseLedgeReach) || c.BaseLedgeReach < 0.9f || c.BaseLedgeReach > 1.5f)
                throw new ArgumentException("Storey template needs a 3..3.6m rise and two supported base-kit ledges with 0.9..1.5m reach.");
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
