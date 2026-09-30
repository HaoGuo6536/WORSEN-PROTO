// ============================================================================
// FloorCollapseUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks escape-route protection independently of collapse scheduling.
//   Directed access, closed rooms and uncertain occupancy must not strand players.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Floor.
// KEY RESPONSIBILITIES:
//   - Verify deterministic ties, all living players and conservative unknown positions.
// DEPENDENCIES:
//   - Core graph values, Floor utility, Player data and NUnit; no engine objects.
// USAGE NOTES:
//   Pure Edit Mode tests with explicit topology and positions.
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
    public sealed class FloorCollapseUtilityTests
    {
        private static Vector3 Position(int room) => Vector3.right * (room * 10f);
        private static PlayerBehaviorState Player(int id, int room, float health = 100f) =>
            new PlayerBehaviorState { Id = new EntityId(id), Position = Position(room), Health = health };
        private static LevelGraph Graph(params LevelEdge[] edges) => new LevelGraph(
            Enumerable.Range(1, 5).Select(id => new LevelRoom(id, Position(id), Vector3.one * 8f)).ToArray(),
            edges, Array.Empty<LevelAnchor>(), 5, Position(5));
        private static Dictionary<int, RoomPhase> Phases() => Enumerable.Range(1, 5).ToDictionary(id => id, _ => RoomPhase.Open);

        [Test]
        public void EqualLengthRoutesChooseLowestRoomIdRegardlessOfEdgeOrder()
        {
            var edges = new[] { new LevelEdge(1, 1, 3, false), new LevelEdge(2, 3, 5, false),
                new LevelEdge(3, 1, 2, false), new LevelEdge(4, 2, 5, false) };
            foreach (var ordered in new[] { edges, edges.Reverse().ToArray() })
                Assert.That(FloorCollapseUtility.EscapeRooms(Graph(ordered), Phases(), new[] { Player(1, 1) }),
                    Is.EquivalentTo(new[] { 1, 2, 5 }));
        }
        [Test]
        public void ClosedRoomsReverseOnlyEdgesAndHunterOnlyEdgesCannotSupplyAnEscape()
        {
            var graph = Graph(new LevelEdge(1, 1, 2, false), new LevelEdge(2, 2, 5, false),
                new LevelEdge(3, 5, 1, false), new LevelEdge(4, 1, 5, false, access: TraversalAccess.Hunter),
                new LevelEdge(5, 1, 3, false), new LevelEdge(6, 3, 4, false), new LevelEdge(7, 4, 5, false));
            var phases = Phases(); phases[2] = RoomPhase.Closed;
            Assert.That(FloorCollapseUtility.EscapeRooms(graph, phases, new[] { Player(1, 1) }), Is.EquivalentTo(new[] { 1, 3, 4, 5 }));
        }
        [Test]
        public void LivingPlayersUnionTheirRoutesButDeadInvalidAndDisconnectedPlayersDoNot()
        {
            var graph = Graph(new LevelEdge(1, 1, 2, true), new LevelEdge(2, 2, 5, true), new LevelEdge(3, 3, 5, true));
            var players = new IReadOnlyPlayerState[] { Player(1, 1), Player(2, 3), Player(3, 4), Player(4, 99, 0), Player(0, 99), null };
            Assert.That(FloorCollapseUtility.EscapeRooms(graph, Phases(), players), Is.EquivalentTo(new[] { 1, 2, 3, 5 }));
            Assert.That(FloorCollapseUtility.EscapeRooms(graph, Phases(), Array.Empty<IReadOnlyPlayerState>()), Is.EquivalentTo(new[] { 5 }));
        }
        [Test]
        public void UnknownLivingOccupancyProtectsEveryRoom()
        {
            Assert.That(FloorCollapseUtility.EscapeRooms(Graph(), Phases(), new[] { Player(1, 99) }),
                Is.EquivalentTo(new[] { 1, 2, 3, 4, 5 }));
        }
    }
}
