// ============================================================================
// FloorCollapseUtility.cs
// ============================================================================
// PURPOSE:
//   Finds the live shortest escape routes that shuffled collapse must preserve.
//   Reuses supplied working storage while rebuilding live connectivity, so room
//   closure and player movement remain visible without per-tick graph allocations.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Respect directed Player-access edges and closed rooms; break route ties by room id.
//   - Protect every living player's route, failing closed when occupancy is unknown.
// DEPENDENCIES:
//   Core level values and injected Player read-only views; no engine operations.
// USAGE NOTES:
//   Stateless; managed bounds math keeps occupancy independent of native engine calls.
//   A player already in a disconnected pocket has no route to preserve.
//   Protection includes the occupied room; progression resumes after the player moves.
//   The scratch overload returns a borrowed set valid until its next use. The
//   original overload retains its independently owned-result contract.
// ============================================================================
using System.Collections.Generic;

using Worsen.Core;
using Worsen.Domain.Player;

namespace Worsen.Domain.Floor
{
    public static class FloorCollapseUtility
    {
        public static HashSet<int> EscapeRooms(LevelGraph graph, IReadOnlyDictionary<int, RoomPhase> phases,
            IReadOnlyList<IReadOnlyPlayerState> players)
            => EscapeRooms(graph, phases, players, new FloorCollapseBehaviorState());

        public static HashSet<int> EscapeRooms(LevelGraph graph, IReadOnlyDictionary<int, RoomPhase> phases,
            IReadOnlyList<IReadOnlyPlayerState> players, FloorCollapseBehaviorState scratch)
        {
            var protectedRooms = scratch.ProtectedRooms; protectedRooms.Clear(); protectedRooms.Add(graph.ExitRoomId);
            var next = scratch.Next;
            var previous = scratch.Previous;
            if (!ReferenceEquals(scratch.Graph, graph))
            {
                next.Clear(); previous.Clear();
                for (int i = 0; i < graph.Rooms.Count; i++)
                {
                    int id = graph.Rooms[i].Id;
                    next.Add(id, new List<int>()); previous.Add(id, new List<int>());
                }
                scratch.Graph = graph;
            }
            foreach (var neighbors in next.Values) neighbors.Clear();
            foreach (var neighbors in previous.Values) neighbors.Clear();
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                if ((edge.Access & TraversalAccess.Player) == 0 || phases[edge.FromRoomId] == RoomPhase.Closed ||
                    phases[edge.ToRoomId] == RoomPhase.Closed) continue;
                next[edge.FromRoomId].Add(edge.ToRoomId); previous[edge.ToRoomId].Add(edge.FromRoomId);
                if (edge.Bidirectional)
                { next[edge.ToRoomId].Add(edge.FromRoomId); previous[edge.FromRoomId].Add(edge.ToRoomId); }
            }
            var distance = scratch.Distance; distance.Clear(); distance.Add(graph.ExitRoomId, 0);
            var queue = scratch.Queue; queue.Clear(); queue.Enqueue(graph.ExitRoomId);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                foreach (int neighbor in previous[room])
                    if (!distance.ContainsKey(neighbor)) { distance[neighbor] = distance[room] + 1; queue.Enqueue(neighbor); }
            }
            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                if (player == null || !player.Id.IsValid || !player.IsAlive) continue;
                var occupied = scratch.Occupied; occupied.Clear();
                for (int r = 0; r < graph.Rooms.Count; r++)
                {
                    var room = graph.Rooms[r];
                    // Full cell containment already implies ContainsXZ. Calling the
                    // Core helper here boxes its IReadOnlyList cell enumerator.
                    if (room.Cells == null) continue;
                    for (int c = 0; c < room.Cells.Count; c++)
                        if (FloorBoundsUtility.Contains(room.Cells[c], player.Position)) { occupied.Add(room.Id); break; }
                }
                occupied.Sort();
                if (occupied.Count == 0)
                {
                    foreach (int id in next.Keys) protectedRooms.Add(id);
                    continue;
                }
                foreach (int room in occupied)
                {
                    if (!distance.ContainsKey(room)) continue;
                    int current = room;
                    protectedRooms.Add(current);
                    while (current != graph.ExitRoomId)
                    {
                        int selected = int.MaxValue;
                        foreach (int id in next[current])
                            if (distance.TryGetValue(id, out int d) && d == distance[current] - 1 && id < selected) selected = id;
                        current = selected;
                        protectedRooms.Add(current);
                    }
                }
            }
            return protectedRooms;
        }
    }
}
