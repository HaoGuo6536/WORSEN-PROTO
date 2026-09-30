// ============================================================================
// FloorCollapseUtility.cs
// ============================================================================
// PURPOSE:
//   Finds the live shortest escape routes that shuffled collapse must preserve.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Respect directed Player-access edges and closed rooms; break route ties by room id.
//   - Protect every living player's route, failing closed when occupancy is unknown.
// DEPENDENCIES:
//   Core level values and injected Player read-only views; no engine operations.
// USAGE NOTES:
//   Stateless. A player already in a disconnected pocket has no route to preserve.
//   Protection includes the occupied room; progression resumes after the player moves.
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using Worsen.Core;
using Worsen.Domain.Player;

namespace Worsen.Domain.Floor
{
    public static class FloorCollapseUtility
    {
        public static HashSet<int> EscapeRooms(LevelGraph graph, IReadOnlyDictionary<int, RoomPhase> phases,
            IReadOnlyList<IReadOnlyPlayerState> players)
        {
            var protectedRooms = new HashSet<int> { graph.ExitRoomId };
            var next = graph.Rooms.ToDictionary(room => room.Id, room => new List<int>());
            var previous = graph.Rooms.ToDictionary(room => room.Id, room => new List<int>());
            foreach (var edge in graph.Edges)
            {
                if ((edge.Access & TraversalAccess.Player) == 0 || phases[edge.FromRoomId] == RoomPhase.Closed ||
                    phases[edge.ToRoomId] == RoomPhase.Closed) continue;
                next[edge.FromRoomId].Add(edge.ToRoomId); previous[edge.ToRoomId].Add(edge.FromRoomId);
                if (edge.Bidirectional)
                { next[edge.ToRoomId].Add(edge.FromRoomId); previous[edge.FromRoomId].Add(edge.ToRoomId); }
            }
            var distance = new Dictionary<int, int> { [graph.ExitRoomId] = 0 };
            var queue = new Queue<int>(); queue.Enqueue(graph.ExitRoomId);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                foreach (int neighbor in previous[room])
                    if (!distance.ContainsKey(neighbor)) { distance[neighbor] = distance[room] + 1; queue.Enqueue(neighbor); }
            }
            foreach (var player in players.Where(value => value != null && value.Id.IsValid && value.IsAlive))
            {
                var occupied = graph.Rooms.Where(room => room.ContainsXZ(player.Position) &&
                    room.Cells.Any(cell => cell.Contains(player.Position))).OrderBy(room => room.Id).ToArray();
                if (occupied.Length == 0) { protectedRooms.UnionWith(next.Keys); continue; }
                foreach (var room in occupied)
                {
                    if (!distance.ContainsKey(room.Id)) continue;
                    int current = room.Id;
                    protectedRooms.Add(current);
                    while (current != graph.ExitRoomId)
                    {
                        current = next[current].Where(id => distance.TryGetValue(id, out int d) && d == distance[current] - 1).Min();
                        protectedRooms.Add(current);
                    }
                }
            }
            return protectedRooms;
        }
    }
}
