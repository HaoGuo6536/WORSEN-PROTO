// ============================================================================
// FloorAllocationDeterminismTests.cs
// ============================================================================
// PURPOSE:
//   Compare collection-gated collapse with an independent deterministic schedule oracle.
//   Seeded activation and injected time check ordered facts and snapshot ownership.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Compare activated pocket transitions, time/id ordering and ten-second phases.
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
        public void PocketFactsMatchDeterministicScheduleAndTenSecondThresholds(int seed, bool fast)
        {
            var random = new System.Random(seed);
            var config = FloorCakeRulesTests.Config();
            var state = new FloorBehaviorState();
            var graph = LevelGraphUtility.Build(Enumerable.Range(1, 33).Select(id =>
                new LevelRoom(id, Position(id), Vector3.one * 8f, pocket: id != 33)).ToArray(),
                Enumerable.Range(1, 32).Select(id => new LevelEdge(id, id, 33, true)).ToArray(),
                new[] { new LevelAnchor(1, 33, CakeAnchorType.Flow, Position(33)) }, 33, Position(33));
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Position(33) };
            var controller = new FloorController(state, config, new System.Random(seed));
            controller.Initialize(graph, new[] { player }, requiredCakeCount: 1, fasterCollapse: fast, shuffledCollapse: true);
            foreach (int id in Enumerable.Range(1, 32).OrderBy(_ => random.Next()))
                Assert.That(controller.ActivatePocket(id), Is.True);
            Assert.That(controller.Tick(10000f, 0), Is.Empty, "Activated pockets cannot collapse before collection.");
            Assert.That(state.RoomPhases.Values, Is.All.EqualTo(RoomPhase.Open));
            Assert.That(controller.DrainCakeLosses(), Is.Empty);
            Assert.That(controller.Collect(player.Id, 1, PickupKind.Cake, 0, out _), Is.True);
            var expectedPhases = graph.Rooms.ToDictionary(r => r.Id, _ => RoomPhase.Open);
            double total = 60d * (1d + config.CollapseLogGrowth * Math.Log(33d / config.CollapseReferenceRooms));
            double spacing = (total - 10d) / 31d;
            var order = new[] { RoomPhase.Telegraph, RoomPhase.Tearing, RoomPhase.Encroaching, RoomPhase.Closed };
            double[] thresholds = { 0d, config.CollapseTelegraphSeconds,
                config.CollapseTelegraphSeconds + config.CollapseTearingSeconds, 10d };
            var transitions = Enumerable.Range(1, 32).SelectMany(id => order.Select((phase, i) =>
                (room: id, phase, at: (id - 1) * spacing + thresholds[i])))
                .OrderBy(value => value.at).ThenBy(value => value.room).ToArray();
            var retained = new List<IReadOnlyList<RoomPhaseChangedFact>>();
            var copies = new List<RoomPhaseChangedFact[]>();
            double elapsed = 0d; int next = 0;
            for (int tick = 1; tick <= 240; tick++)
            {
                float dt = random.Next(4) * .5f; elapsed += dt;
                var expected = new List<RoomPhaseChangedFact>();
                while (next < transitions.Length && transitions[next].at <= elapsed)
                {
                    var transition = transitions[next++];
                    expectedPhases[transition.room] = transition.phase;
                    expected.Add(new RoomPhaseChangedFact(transition.room, transition.phase, tick));
                }
                var actual = controller.Tick(dt, tick);
                Assert.That(actual, Is.EqualTo(expected), "tick " + tick);
                Assert.That(state.RoomPhases, Is.EquivalentTo(expectedPhases));
                retained.Add(actual); copies.Add(actual.ToArray());
                Assert.That(controller.DrainCakeLosses(), Is.Empty);
            }
            Assert.That(next, Is.EqualTo(transitions.Length));
            Assert.That(state.RoomPhases[33], Is.EqualTo(RoomPhase.Open));
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
    }
}
