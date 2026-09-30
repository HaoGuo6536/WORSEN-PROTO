// ============================================================================
// AcousticOcclusionUtility.cs
// ============================================================================
// PURPOSE:
//   Computes one deterministic hearing sample for gameplay hints and the audio mix.
//   It combines geometric distance with portal and closed-door loss, without physics.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Core · shared Hearing calculations.
// KEY RESPONSIBILITIES:
//   - Find the fewest portal hops, then the fewest closed doors among tied paths.
//   - Apply bounded amplitude falloff and an inclusive, positive audible threshold.
// DEPENDENCIES:
//   - Core LevelGraph and Hearing definitions, System buffers and pure Vector3 data.
// USAGE NOTES:
//   Supply a validated, id-sorted LevelGraphUtility.Build snapshot. Sound travels
//   both ways through every edge, regardless of actor access or one-way movement;
//   LevelGraphUtility's directed actor distances therefore cannot supply this path.
//   Missing door entries are open. Same-room queries allocate nothing; cross-room
//   queries rent one integer buffer, returned in finally. No cache or clock is used.
// ============================================================================
using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Core
{
    /// <summary>The shared pure distance-and-portal hearing model.</summary>
    public static class AcousticOcclusionUtility
    {
        public static HearingSample Sample(LevelGraph graph, int sourceRoomId, Vector3 sourcePosition,
            int listenerRoomId, Vector3 listenerPosition, float loudness, HearingModelSettings settings,
            IReadOnlyDictionary<int, bool> closedDoors = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (settings.ReferenceDistance <= 0f) throw new ArgumentException("Construct valid hearing settings.", nameof(settings));
            RequireFinite(sourcePosition); RequireFinite(listenerPosition);
            if (float.IsNaN(loudness) || float.IsInfinity(loudness)) throw new ArgumentOutOfRangeException(nameof(loudness));
            int source = RoomIndex(graph, sourceRoomId), listener = RoomIndex(graph, listenerRoomId);
            if (source < 0 || listener < 0) return new HearingSample(0f, false, -1, false);
            if (source == listener) return Evaluate(sourcePosition, listenerPosition, loudness, settings, 0, 0);

            int count = graph.Rooms.Count;
            int[] buffer = ArrayPool<int>.Shared.Rent(checked(count * 3));
            try
            {
                var queue = new Span<int>(buffer, 0, count);
                var hops = new Span<int>(buffer, count, count);
                var doors = new Span<int>(buffer, count * 2, count);
                hops.Fill(-1); doors.Clear();
                int read = 0, write = 0;
                hops[source] = 0; queue[write++] = source;
                while (read < write)
                {
                    int current = queue[read++];
                    if (current == listener)
                        return Evaluate(sourcePosition, listenerPosition, loudness, settings, hops[current], doors[current]);
                    int roomId = graph.Rooms[current].Id;
                    for (int i = 0; i < graph.Edges.Count; i++)
                    {
                        var edge = graph.Edges[i];
                        int nextId = edge.FromRoomId == roomId ? edge.ToRoomId :
                            edge.ToRoomId == roomId ? edge.FromRoomId : -1;
                        if (nextId < 0) continue;
                        int next = RoomIndex(graph, nextId);
                        int nextHops = hops[current] + 1;
                        int nextDoors = doors[current] +
                            (closedDoors != null && closedDoors.TryGetValue(edge.Id, out bool closed) && closed ? 1 : 0);
                        if (hops[next] < 0)
                        {
                            hops[next] = nextHops; doors[next] = nextDoors; queue[write++] = next;
                        }
                        else if (hops[next] == nextHops && nextDoors < doors[next]) doors[next] = nextDoors;
                    }
                }
                return new HearingSample(0f, false, -1, false);
            }
            finally { ArrayPool<int>.Shared.Return(buffer); }
        }

        private static HearingSample Evaluate(Vector3 source, Vector3 listener, float loudness,
            HearingModelSettings settings, int hops, int closedDoors)
        {
            double x = (double)source.x - listener.x, y = (double)source.y - listener.y, z = (double)source.z - listener.z;
            double distance = Math.Sqrt(x * x + y * y + z * z);
            double falloff = Math.Pow(Math.Max(1d, distance / settings.ReferenceDistance), -settings.Rolloff);
            float perceived = (float)(Math.Max(0d, Math.Min(1d, loudness)) * falloff *
                Math.Pow(settings.PerPortalAttenuation, hops) * Math.Pow(settings.ClosedDoorAttenuation, closedDoors));
            return new HearingSample(perceived, perceived > 0f && perceived >= settings.AudibleThreshold, hops, closedDoors > 0);
        }

        private static int RoomIndex(LevelGraph graph, int id)
        {
            int low = 0, high = graph.Rooms.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int candidate = graph.Rooms[middle].Id;
                if (candidate == id) return middle;
                if (candidate < id) low = middle + 1; else high = middle - 1;
            }
            return -1;
        }

        private static void RequireFinite(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                float.IsNaN(position.z) || float.IsInfinity(position.z))
                throw new ArgumentException("Hearing positions must be finite.", nameof(position));
        }
    }
}
