// ============================================================================
// HunterNavigationUtility.cs
// ============================================================================
// PURPOSE:
//   Selects Hunter room routes from immutable topology and observed positions.
//   These computations never query hidden player state or pretend room edges
//   contain authored doorway positions. Physical clearance remains the Driver's job.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Find directed, Hunter-accessible paths excluding closed rooms and an optional edge.
//   - Produce deterministic search legs and purposeful room targets.
// DEPENDENCIES:
//   - Core Level graph values and UnityEngine value math only.
// USAGE NOTES:
//   Room id zero means unknown. Doorway positions are boundary approximations from
//   room bounds until Level exposes portal positions; parallel routes exclude the
//   direct first edge. Search repeats loss, near/far sweep, doorway, beyond, return.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    public static class HunterNavigationUtility
    {
        public static int RoomAt(LevelGraph graph, Vector3 point)
        {
            if (graph != null) foreach (LevelRoom room in graph.Rooms)
                if (room.Bounds.Contains(point)) return room.Id;
            return 0;
        }
        public static Vector3 RoomTarget(LevelGraph graph, int id, Vector3 fallback)
        {
            if (graph != null) foreach (LevelRoom room in graph.Rooms)
                if (room.Id == id) return new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
            return fallback;
        }
        public static List<int> Path(LevelGraph graph, int start, int end, ISet<int> blocked, int excludedEdge = 0)
        {
            var result = new List<int>();
            if (graph == null || start == 0 || end == 0 || (blocked != null && blocked.Contains(end))) return result;
            var parents = new Dictionary<int, int> { [start] = 0 };
            var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                if (room == end)
                {
                    for (int next = end; next != 0; next = parents[next]) result.Add(next);
                    result.Reverse(); return result;
                }
                foreach (LevelEdge edge in graph.Edges)
                {
                    if (edge.Id == excludedEdge || (edge.Access & TraversalAccess.Hunter) == 0) continue;
                    int next = edge.FromRoomId == room ? edge.ToRoomId : edge.Bidirectional && edge.ToRoomId == room ? edge.FromRoomId : 0;
                    if (next == 0 || parents.ContainsKey(next) || (blocked != null && blocked.Contains(next))) continue;
                    parents.Add(next, room); queue.Enqueue(next);
                }
            }
            return result;
        }
        public static List<int> ParallelPath(LevelGraph graph, int start, int end, ISet<int> blocked)
        {
            List<int> direct = Path(graph, start, end, blocked);
            if (direct.Count < 2) return new List<int>();
            foreach (LevelEdge edge in graph.Edges)
                if ((edge.FromRoomId == start && edge.ToRoomId == direct[1]) ||
                    (edge.Bidirectional && edge.ToRoomId == start && edge.FromRoomId == direct[1]))
                    return Path(graph, start, end, blocked, edge.Id);
            return new List<int>();
        }
        public static Vector3 Doorway(LevelGraph graph, int from, int to, Vector3 fallback)
        {
            if (graph == null || from == 0 || to == 0 || from == to) return fallback;
            Vector3 other = RoomTarget(graph, to, fallback);
            foreach (LevelRoom room in graph.Rooms)
            {
                if (room.Id != from) continue;
                Vector3 center = RoomTarget(graph, from, fallback);
                Vector3 offset = other - center; offset.y = 0f;
                float x = Mathf.Abs(offset.x) > 0.0001f ? room.Bounds.extents.x / Mathf.Abs(offset.x) : float.PositiveInfinity;
                float z = Mathf.Abs(offset.z) > 0.0001f ? room.Bounds.extents.z / Mathf.Abs(offset.z) : float.PositiveInfinity;
                float fraction = Mathf.Min(1f, Mathf.Min(x, z));
                return center + offset * fraction;
            }
            return fallback;
        }
        public static List<Vector3> Search(LevelGraph graph, Vector3 loss, Vector3 velocity,
            int previousRoom, int observedRoom, float expansion, ISet<int> blocked)
        {
            Vector3 heading = new Vector3(velocity.x, 0f, velocity.z);
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            var route = new List<Vector3> { loss, loss + side * expansion, loss - side * expansion * 2f };
            // Keep expansion samples in the observed room rather than inventing navigable space.
            if (graph != null) foreach (LevelRoom room in graph.Rooms)
                if (room.Id == observedRoom)
                    for (int i = 1; i < route.Count; i++) route[i] = room.Bounds.ClosestPoint(route[i]);
            int beyond = observedRoom;
            int from = previousRoom;
            if (from == 0 || from == observedRoom)
            {
                from = observedRoom; float best = float.NegativeInfinity;
                if (graph != null) foreach (LevelRoom room in graph.Rooms)
                {
                    if (room.Id == from || Path(graph, from, room.Id, blocked).Count != 2) continue;
                    float score = Vector3.Dot((RoomTarget(graph, room.Id, loss) - loss).normalized, heading);
                    if (score > best) { best = score; beyond = room.Id; }
                }
            }
            route.Add(Doorway(graph, from, beyond, loss));
            route.Add(RoomTarget(graph, beyond, loss));
            route.Add(loss);
            return route;
        }
        public static int PreferredRoom(LevelGraph graph, int start, Vector3 position, IReadOnlyList<LevelAnchor> cakes,
            bool exitOpen, int lastPickup, ISet<int> blocked)
        {
            int best = 0; int bestPriority = 0; float nearest = float.PositiveInfinity;
            if (graph == null) return best;
            foreach (LevelRoom room in graph.Rooms)
            {
                if (Path(graph, start, room.Id, blocked).Count == 0) continue;
                int priority = exitOpen && room.Id == graph.ExitRoomId ? 3 : room.Id == lastPickup ? 2 : 0;
                if (cakes != null) foreach (LevelAnchor cake in cakes)
                    if (cake.RoomId == room.Id) { priority = System.Math.Max(priority, 1); break; }
                float distance = Vector3.SqrMagnitude(RoomTarget(graph, room.Id, position) - position);
                if (priority > 0 && (priority > bestPriority || (priority == bestPriority && distance < nearest)))
                { bestPriority = priority; nearest = distance; best = room.Id; }
            }
            return best;
        }
    }
}
