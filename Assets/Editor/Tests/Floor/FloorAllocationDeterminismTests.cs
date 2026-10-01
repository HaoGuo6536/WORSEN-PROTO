// ============================================================================
// FloorAllocationDeterminismTests.cs
// ============================================================================
// PURPOSE:
//   Compare scratch-based collapse with the old allocating LINQ ordering and routes.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Compare seeded pocket transition streams including simultaneous starts.
//   - Compare live escape routes with the pre-cleanup implementation.
//   - Check result ownership, graph replacement and warmed scratch allocations.
// DEPENDENCIES:
//   NUnit, Core/Floor/Player values and managed reflection; no native objects.
// USAGE NOTES:
//   LINQ is intentional in the test oracle; production paths must reuse scratch.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorAllocationDeterminismTests
    {
        [TestCase(7, false)] [TestCase(77, true)] [TestCase(2026, false)]
        public void PocketFactsMatchLegacyOrderAndThresholds(int seed, bool fast)
        {
            var random = new System.Random(seed);
            var config = FloorCakeRulesTests.Config();
            var state = new FloorBehaviorState();
            typeof(FloorBehaviorState).GetProperty("IsReady").SetValue(state, true);
            FloorCakeRulesTests.Set(state, "FasterCollapse", fast);
            var starts = Field<Dictionary<int, double>>(state, "PocketStarts");
            var phases = Field<Dictionary<int, RoomPhase>>(state, "MutableRoomPhases");
            foreach (int id in Enumerable.Range(1, 32).OrderBy(_ => random.Next()))
            { starts.Add(id, random.Next(4)); phases.Add(id, RoomPhase.Open); }
            var expectedPhases = phases.ToDictionary(p => p.Key, p => p.Value);
            var controller = new FloorController(state, config, new System.Random(seed));
            var retained = new List<IReadOnlyList<RoomPhaseChangedFact>>();
            var copies = new List<RoomPhaseChangedFact[]>();
            double elapsed = 0d;
            for (int tick = 1; tick <= 40; tick++)
            {
                float dt = random.Next(4) * .5f; elapsed += dt;
                var expected = new List<RoomPhaseChangedFact>();
                foreach (var pocket in starts.OrderBy(p => p.Value).ThenBy(p => p.Key).ToArray())
                {
                    double age = (elapsed - pocket.Value) * (fast ? config.FasterCollapseMultiplier : 1f);
                    var order = new[] { RoomPhase.Telegraph, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed };
                    double[] thresholds = { 0d, config.TelegraphDuration, config.TelegraphDuration + config.TearingDuration,
                        config.TelegraphDuration + config.TearingDuration + config.EncroachingDuration };
                    int completed = Array.IndexOf(order, expectedPhases[pocket.Key]);
                    for (int i = 0; i < order.Length; i++)
                        if (age >= thresholds[i] && completed < i)
                        {
                            expectedPhases[pocket.Key] = order[i]; completed = i;
                            expected.Add(new RoomPhaseChangedFact(pocket.Key, order[i], tick));
                        }
                }
                var actual = controller.Tick(dt, tick);
                Assert.That(actual, Is.EqualTo(expected), "tick " + tick);
                Assert.That(state.RoomPhases, Is.EquivalentTo(expectedPhases));
                retained.Add(actual); copies.Add(actual.ToArray());
            }
            controller.Reset();
            for (int i = 0; i < retained.Count; i++) Assert.That(retained[i], Is.EqualTo(copies[i]));
        }

        [TestCase(7)] [TestCase(77)] [TestCase(2026)]
        public void ReusedRoutesMatchLegacyAcrossClosuresMovementAndGraphReplacement(int seed)
        {
            var random = new System.Random(seed);
            var scratch = new FloorCollapseBehaviorState();
            for (int generation = 0; generation < 4; generation++)
            {
                var rooms = Enumerable.Range(1, 12).Select(id => new LevelRoom(id, Position(id), Vector3.one * 8f)).ToArray();
                var edges = new List<LevelEdge>();
                for (int i = 0; i < 48; i++) edges.Add(new LevelEdge(i + 1, random.Next(1, 13), random.Next(1, 13),
                    random.Next(2) == 0, access: random.Next(4) == 0 ? TraversalAccess.Hunter : TraversalAccess.Player));
                var graph = new LevelGraph(rooms, edges, Array.Empty<LevelAnchor>(), 12, Position(12));
                var phases = rooms.ToDictionary(r => r.Id, _ => RoomPhase.Open);
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f };
                var players = new IReadOnlyPlayerState[] { player, null };
                for (int tick = 0; tick < 30; tick++)
                {
                    phases[random.Next(1, 12)] = random.Next(2) == 0 ? RoomPhase.Closed : RoomPhase.Open;
                    player.Position = Position(random.Next(1, 15));
                    var expected = LegacyEscapeRooms(graph, phases, players);
                    Assert.That(FloorCollapseUtility.EscapeRooms(graph, phases, players, scratch), Is.EquivalentTo(expected));
                }
                player.Position = Position(12);
                for (int warm = 0; warm < 8; warm++) FloorCollapseUtility.EscapeRooms(graph, phases, players, scratch);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int tick = 0; tick < 100; tick++) FloorCollapseUtility.EscapeRooms(graph, phases, players, scratch);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
            }
        }

        // Pre-cleanup algorithm, kept independent of the new scratch overload.
        private static HashSet<int> LegacyEscapeRooms(LevelGraph graph, IReadOnlyDictionary<int, RoomPhase> phases,
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
                    room.Cells.Any(cell => FloorBoundsUtility.Contains(cell, player.Position))).OrderBy(room => room.Id).ToArray();
                if (occupied.Length == 0) { protectedRooms.UnionWith(next.Keys); continue; }
                foreach (var room in occupied)
                {
                    if (!distance.ContainsKey(room.Id)) continue;
                    int current = room.Id; protectedRooms.Add(current);
                    while (current != graph.ExitRoomId)
                    {
                        current = next[current].Where(id => distance.TryGetValue(id, out int d) && d == distance[current] - 1).Min();
                        protectedRooms.Add(current);
                    }
                }
            }
            return protectedRooms;
        }
        private static Vector3 Position(int room) => new Vector3(room * 10f, 0f, 0f);
        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
