// ============================================================================
// LevelGraphUtility.cs
// ============================================================================
// PURPOSE:
//   Builds validated, deterministic graph snapshots and computes directed room
//   distances. Level and Floor share this implementation so collapse scheduling
//   and authored route validation cannot silently use different topology rules.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Core · shared Level graph operations.
// KEY RESPONSIBILITIES:
//   - Validate identities and references; traverse permitted forward or reverse edges.
// DEPENDENCIES:
//   - Core LevelGraph value types and System collections only.
// USAGE NOTES:
//   Pure and stateless. Distances are unweighted hop counts, with -1 for rooms
//   unreachable using the selected actor access. All means either actor may use
//   the edge; use Player or Hunter for actor-specific reachability.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Worsen.Core
{
    public static class LevelGraphUtility
    {
        public static LevelGraph Build(IReadOnlyList<LevelRoom> rooms, IReadOnlyList<LevelEdge> edges,
            IReadOnlyList<LevelAnchor> anchors, int exitRoomId, Vector3 exitPosition)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            if (anchors == null) throw new ArgumentNullException(nameof(anchors));
            var roomIds = new HashSet<int>();
            var edgeIds = new HashSet<int>();
            var anchorIds = new HashSet<int>();
            foreach (var room in rooms)
            {
                RequireId(roomIds, room.Id, "room");
                RequireFinite(room.Center, "room center");
                RequireFinite(room.Size, "room size");
                if (room.Size.x <= 0f || room.Size.y <= 0f || room.Size.z <= 0f)
                    throw new ArgumentException("Room size must be positive.");
            }
            if (!roomIds.Contains(exitRoomId))
                throw new ArgumentException("Exit must belong to an authored room.");
            RequireFinite(exitPosition, "exit position");
            foreach (var edge in edges)
            {
                RequireId(edgeIds, edge.Id, "edge");
                if (!roomIds.Contains(edge.FromRoomId) || !roomIds.Contains(edge.ToRoomId))
                    throw new ArgumentException("Every edge endpoint must reference an authored room.");
                if (edge.Access == TraversalAccess.None || (edge.Access & ~TraversalAccess.All) != 0)
                    throw new ArgumentException("An edge must allow at least one known actor.");
            }
            foreach (var anchor in anchors)
            {
                RequireId(anchorIds, anchor.Id, "anchor");
                if (!roomIds.Contains(anchor.RoomId))
                    throw new ArgumentException("Every anchor must belong to an authored room.");
                if (!Enum.IsDefined(typeof(CakeAnchorType), anchor.Type))
                    throw new ArgumentException("Unknown cake anchor type.");
                RequireFinite(anchor.Position, "anchor position");
            }
            return new LevelGraph(rooms.OrderBy(room => room.Id).ToArray(),
                edges.OrderBy(edge => edge.Id).ToArray(), anchors.OrderBy(anchor => anchor.Id).ToArray(),
                exitRoomId, exitPosition);
        }

        public static IReadOnlyDictionary<int, int> TopologicalDistancesFrom(LevelGraph graph, int startRoomId,
            TraversalAccess access = TraversalAccess.All)
        {
            return Distances(graph, startRoomId, access, false);
        }

        public static IReadOnlyDictionary<int, int> DistancesTo(LevelGraph graph, int targetRoomId,
            TraversalAccess access = TraversalAccess.All)
        {
            return Distances(graph, targetRoomId, access, true);
        }

        private static IReadOnlyDictionary<int, int> Distances(LevelGraph graph, int origin,
            TraversalAccess access, bool reverse)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (access == TraversalAccess.None || (access & ~TraversalAccess.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(access));
            var distances = new SortedDictionary<int, int>();
            var neighbors = new Dictionary<int, List<int>>();
            foreach (var room in graph.Rooms)
            {
                distances.Add(room.Id, -1);
                neighbors.Add(room.Id, new List<int>());
            }
            if (!distances.ContainsKey(origin))
                throw new ArgumentException("Distance origin must be an authored room.", nameof(origin));
            foreach (var edge in graph.Edges)
            {
                if ((edge.Access & access) == 0) continue;
                var from = reverse ? edge.ToRoomId : edge.FromRoomId;
                var to = reverse ? edge.FromRoomId : edge.ToRoomId;
                neighbors[from].Add(to);
                if (edge.Bidirectional) neighbors[to].Add(from);
            }
            foreach (var list in neighbors.Values) list.Sort();
            var pending = new Queue<int>();
            distances[origin] = 0;
            pending.Enqueue(origin);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var next in neighbors[current])
                {
                    if (distances[next] >= 0) continue;
                    distances[next] = distances[current] + 1;
                    pending.Enqueue(next);
                }
            }
            return new ReadOnlyDictionary<int, int>(distances);
        }

        private static void RequireId(HashSet<int> ids, int id, string kind)
        {
            if (id <= 0 || !ids.Add(id))
                throw new ArgumentException("Each " + kind + " needs a unique positive id.");
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z))
                throw new ArgumentException(name + " must be finite.");
        }
    }
}

