// ============================================================================
// ProceduralBiomeUtility.cs
// ============================================================================
// PURPOSE:
//   Partitions a connected room graph into seeded graph-Voronoi biomes.
//   Positive shared edge costs and deterministic multi-source expansion ensure
//   every region has a path to its seed instead of isolated colour islands.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Grow a run-stable enabled theme pool by round, starting with one biome.
//   - Draw a subset and seed rooms, then assign contiguous variable-size regions.
//   - Resolve room-local theme data without changing legacy floor-level contracts.
// DEPENDENCIES:
//   - Own config/layout and Core immutable graph values only.
// USAGE NOTES:
//   Graph distances, not Euclidean distance through walls, define this Voronoi map.
//   Pocket rooms inherit their source biome after placement; they are not seeds.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralBiomeUtility
    {
        public static ProceduralThemeData[] Pool(ProceduralThemeConfig config, int round, int runSeed)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (ReferenceEquals(config, null)) return Array.Empty<ProceduralThemeData>();
            if (config.BiomeRoundsPerUnlock < 1) throw new ArgumentException("Invalid biome pool cadence.");
            var remaining = new[] { config.Castle, config.HospitalEnabled ? config.Hospital : null,
                config.SchoolEnabled ? config.School : null, config.BasementEnabled ? config.Basement : null }.Where(t => t != null).ToList();
            if (remaining.Count == 0 || remaining.Select(t => t.Id).Distinct().Count() != remaining.Count)
                throw new ArgumentException("Biome pool requires unique themes.");
            var random = new Random(runSeed); var ordered = new List<ProceduralThemeData>();
            while (remaining.Count > 0) { int index = random.Next(remaining.Count); ordered.Add(remaining[index]); remaining.RemoveAt(index); }
            return ordered.Take((int)Math.Min(ordered.Count, 1L + ((long)round - 1) / config.BiomeRoundsPerUnlock)).ToArray();
        }
        public static IReadOnlyDictionary<int, ProceduralThemeData> Partition(LevelGraph graph,
            IReadOnlyList<ProceduralThemeData> pool, Random random)
        {
            if (graph == null || pool == null || pool.Count == 0 || random == null) throw new ArgumentException("Missing biome inputs.");
            var rooms = graph.Rooms.Where(r => !r.Pocket).OrderBy(r => r.Id).ToArray();
            if (rooms.Length == 0) throw new ArgumentException("No connected rooms for biomes.");
            int maximum = Math.Min(pool.Count, rooms.Length), count = maximum == 1 ? 1 : random.Next(2, maximum + 1);
            var themes = pool.ToList();
            for (int i = themes.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = themes[i]; themes[i] = themes[j]; themes[j] = t; }
            var remaining = rooms.Select(r => r.Id).ToList();
            var distance = rooms.ToDictionary(r => r.Id, r => double.PositiveInfinity);
            var owner = new Dictionary<int, int>(); var settled = new HashSet<int>();
            for (int i = 0; i < count; i++) { int index = random.Next(remaining.Count), id = remaining[index]; remaining.RemoveAt(index); distance[id] = 0d; owner[id] = i; }
            // The same positive noisy cost is used in both directions. Once a room
            // settles, its predecessor is in the same region, proving contiguity.
            var costs = graph.Edges.OrderBy(e => e.Id).ToDictionary(e => e.Id, e => 1d + random.NextDouble());
            while (settled.Count < rooms.Length)
            {
                int id = rooms.Where(r => !settled.Contains(r.Id)).OrderBy(r => distance[r.Id]).ThenBy(r => r.Id).First().Id;
                if (double.IsPositiveInfinity(distance[id])) throw new ArgumentException("Biome room graph is disconnected.");
                settled.Add(id);
                foreach (var edge in graph.Edges.Where(e => e.Access == TraversalAccess.All).OrderBy(e => e.Id))
                {
                    int next = edge.FromRoomId == id ? edge.ToRoomId : edge.Bidirectional && edge.ToRoomId == id ? edge.FromRoomId : 0;
                    if (!distance.ContainsKey(next) || settled.Contains(next)) continue;
                    double candidate = distance[id] + costs[edge.Id];
                    if (candidate < distance[next]) { distance[next] = candidate; owner[next] = owner[id]; }
                }
            }
            return rooms.ToDictionary(r => r.Id, r => themes[owner[r.Id]]);
        }
        public static ProceduralThemeData Theme(ProceduralLayout layout, int roomId)
            => layout.RoomThemes.TryGetValue(roomId, out var theme) ? theme : layout.Theme;
    }
}
