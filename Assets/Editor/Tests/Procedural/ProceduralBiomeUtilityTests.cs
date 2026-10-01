// ============================================================================
// ProceduralBiomeUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Proves seeded graph-Voronoi identity, contiguous regions and pool progression.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Exercise deterministic assignments, variable sizes and run-stable unlocks.
// DEPENDENCIES:
//   - NUnit, Core and own managed Procedural test data.
// USAGE NOTES:
//   No engine object allocation or native navigation; disconnected input fails closed.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralBiomeUtilityTests
    {
        [Test]
        public void PoolGrowsMonotonicallyAndRoundOneAlwaysHasOneBiome()
        {
            var config = Themes();
            for (int seed = 0; seed < 64; seed++)
            {
                string[] previous = Array.Empty<string>();
                for (int round = 1; round <= 10; round++)
                {
                    var pool = ProceduralBiomeUtility.Pool(config, round, seed);
                    Assert.That(pool.Length, Is.EqualTo(Math.Min(round, 4)));
                    Assert.That(pool.Select(t => t.Id).Take(previous.Length), Is.EqualTo(previous));
                    previous = pool.Select(t => t.Id).ToArray();
                    if (round == 1) Assert.That(ProceduralBiomeUtility.Partition(Graph(), pool, new System.Random(seed)).Values.Select(t => t.Id).Distinct().Count(), Is.EqualTo(1));
                }
            }
        }
        [Test]
        public void GraphVoronoiIsDeterministicContiguousAndVariesRegionSizesAndSubsetCounts()
        {
            var graph = Graph(); var pool = ProceduralBiomeUtility.Pool(Themes(), 8, 93);
            var counts = new HashSet<int>(); bool unequal = false;
            for (int seed = 0; seed < 128; seed++)
            {
                var a = ProceduralBiomeUtility.Partition(graph, pool, new System.Random(seed));
                var b = ProceduralBiomeUtility.Partition(graph, pool, new System.Random(seed));
                Assert.That(a.OrderBy(p => p.Key).Select(p => p.Value.Id), Is.EqualTo(b.OrderBy(p => p.Key).Select(p => p.Value.Id)));
                AssertContiguous(graph, a); var groups = a.GroupBy(p => p.Value.Id).ToArray();
                counts.Add(groups.Length); unequal |= groups.Select(g => g.Count()).Distinct().Count() > 1;
            }
            Assert.That(counts, Is.EquivalentTo(new[] { 2, 3, 4 })); Assert.That(unequal, Is.True);
        }
        [Test]
        public void DisabledThemesStayOutAndDisconnectedGraphIsRejected()
        {
            var config = Themes(); ProceduralTemplateSeamPresenterTests.Field(config, "_schoolEnabled", false);
            Assert.That(ProceduralBiomeUtility.Pool(config, 100, 4).Any(t => t.Id == "school"), Is.False);
            var graph = Graph(); var broken = LevelGraphUtility.Build(graph.Rooms, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            Assert.Throws<ArgumentException>(() => ProceduralBiomeUtility.Partition(broken, new[] { Theme("castle") }, new System.Random(3)));
        }
        internal static void AssertContiguous(LevelGraph graph, IReadOnlyDictionary<int, ProceduralThemeData> map)
        {
            foreach (var group in map.Where(p => !graph.Rooms.Single(r => r.Id == p.Key).Pocket).GroupBy(p => p.Value.Id))
            {
                var ids = new HashSet<int>(group.Select(p => p.Key)); var visited = new HashSet<int> { ids.First() };
                var queue = new Queue<int>(visited);
                while (queue.Count != 0)
                {
                    int current = queue.Dequeue();
                    foreach (var edge in graph.Edges.Where(e => e.Access == TraversalAccess.All && (e.FromRoomId == current || e.ToRoomId == current)))
                    {
                        int next = edge.FromRoomId == current ? edge.ToRoomId : edge.FromRoomId;
                        if (ids.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                Assert.That(visited, Is.EquivalentTo(ids));
            }
        }
        internal static ProceduralThemeData Theme(string id) => new ProceduralThemeData(id, true, Array.Empty<string>(), "", "", "", "", "", default, default, default, 0f);
        internal static ProceduralThemeConfig Themes()
        {
            var config = ProceduralTemplateSeamPresenterTests.Empty<ProceduralThemeConfig>();
            foreach (string id in new[] { "castle", "hospital", "school", "basement" }) ProceduralTemplateSeamPresenterTests.Field(config, "_" + id, Theme(id));
            foreach (string id in new[] { "hospital", "school", "basement" }) ProceduralTemplateSeamPresenterTests.Field(config, "_" + id + "Enabled", true);
            ProceduralTemplateSeamPresenterTests.Field(config, "_biomeRoundsPerUnlock", 1); return config;
        }
        private static LevelGraph Graph() => LevelGraphUtility.Build(
            Enumerable.Range(1, 25).Select(i => new LevelRoom(i, new Vector3(i % 5 * 10f, 1f, i / 5 * 10f), Vector3.one)).ToArray(),
            Enumerable.Range(1, 25).SelectMany(i => new[] { i % 5 == 0 ? 0 : i + 1, i <= 20 ? i + 5 : 0 }
                .Where(j => j > 0).Select(j => new LevelEdge(i * 100 + j, i, j, true))).ToArray(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
    }
}
