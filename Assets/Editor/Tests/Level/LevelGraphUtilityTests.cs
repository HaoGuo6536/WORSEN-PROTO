// ============================================================================
// LevelGraphUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks graph identity, directed reachability and deterministic snapshots.
//   These tests protect consumers from confusing distance from the exit with
//   distance to it when one-way drops are present.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level.
// KEY RESPONSIBILITIES:
//   - Exercise disconnected graphs, actor filters, validation and immutable data.
// DEPENDENCIES:
//   - Core LevelGraphUtility and NUnit; UnityEngine value types only.
// USAGE NOTES:
//   Edit Mode tests with no scene, engine operations, time or random source.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Tests.Level
{
    public sealed class LevelGraphUtilityTests
    {
        private static LevelRoom Room(int id) => new LevelRoom(id, new Vector3(id, 0f, 0f), Vector3.one);

        private static LevelGraph Graph(params LevelEdge[] edges)
        {
            return LevelGraphUtility.Build(new[] { Room(3), Room(1), Room(2), Room(4) },
                edges, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        }

        [Test]
        public void BreadthFirstDistancesUseShortestPathAndMarkDisconnectedRooms()
        {
            var graph = Graph(new LevelEdge(11, 1, 2, true), new LevelEdge(12, 2, 3, true),
                new LevelEdge(13, 1, 3, true));
            var distances = LevelGraphUtility.TopologicalDistancesFrom(graph, 1);
            Assert.That(distances[1], Is.Zero);
            Assert.That(distances[2], Is.EqualTo(1));
            Assert.That(distances[3], Is.EqualTo(1));
            Assert.That(distances[4], Is.EqualTo(-1));
        }

        [Test]
        public void OneWayDropsRespectDirectionAndReverseDistanceToExit()
        {
            var graph = Graph(new LevelEdge(11, 3, 2, false), new LevelEdge(12, 2, 1, false));
            var fromExit = LevelGraphUtility.TopologicalDistancesFrom(graph, 1);
            var toExit = LevelGraphUtility.DistancesTo(graph, 1);
            Assert.That(fromExit[3], Is.EqualTo(-1));
            Assert.That(toExit[3], Is.EqualTo(2));
            Assert.That(toExit[2], Is.EqualTo(1));
            Assert.That(toExit[4], Is.EqualTo(-1));
        }

        [Test]
        public void ActorRestrictionsMatchPlayerAndHunterAlternatives()
        {
            var graph = Graph(new LevelEdge(11, 1, 2, true, TraversalAccess.Player),
                new LevelEdge(12, 1, 3, true, TraversalAccess.Hunter));
            Assert.That(LevelGraphUtility.TopologicalDistancesFrom(graph, 1, TraversalAccess.Player)[3], Is.EqualTo(-1));
            Assert.That(LevelGraphUtility.TopologicalDistancesFrom(graph, 1, TraversalAccess.Hunter)[2], Is.EqualTo(-1));
            Assert.That(LevelGraphUtility.TopologicalDistancesFrom(graph, 1)[3], Is.EqualTo(1));
        }

        [Test]
        public void BuildSortsEveryIdentityAndCopiesCallerCollections()
        {
            var rooms = new[] { Room(3), Room(1), Room(2) };
            var edges = new[] { new LevelEdge(12, 2, 3, true), new LevelEdge(11, 1, 2, true) };
            var anchors = new[] { new LevelAnchor(22, 2, CakeAnchorType.Risk, Vector3.one),
                new LevelAnchor(21, 1, CakeAnchorType.Flow, Vector3.zero) };
            var graph = LevelGraphUtility.Build(rooms, edges, anchors, 1, Vector3.zero);
            rooms[0] = Room(99);
            anchors[0] = new LevelAnchor(99, 99, CakeAnchorType.Flow, Vector3.zero);
            Assert.That(graph.Rooms.Select(room => room.Id), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(graph.Edges.Select(edge => edge.Id), Is.EqualTo(new[] { 11, 12 }));
            Assert.That(graph.Anchors.Select(anchor => anchor.Id), Is.EqualTo(new[] { 21, 22 }));
            Assert.Throws<NotSupportedException>(() => ((IList<LevelRoom>)graph.Rooms)[0] = Room(7));
        }

        [Test]
        public void OrderingDoesNotDependOnInputMarkerOrder()
        {
            var first = Graph(new LevelEdge(12, 2, 3, true), new LevelEdge(11, 1, 2, true));
            var second = Graph(new LevelEdge(11, 1, 2, true), new LevelEdge(12, 2, 3, true));
            Assert.That(LevelGraphUtility.TopologicalDistancesFrom(first, 1).ToArray(),
                Is.EqualTo(LevelGraphUtility.TopologicalDistancesFrom(second, 1).ToArray()));
        }

        [Test]
        public void DuplicateRoomEdgeAndAnchorIdsFailInsteadOfReplacingData()
        {
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.Build(new[] { Room(1), Room(1) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero));
            Assert.Throws<ArgumentException>(() => Graph(new LevelEdge(11, 1, 2, true), new LevelEdge(11, 2, 3, true)));
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.Build(new[] { Room(1) },
                Array.Empty<LevelEdge>(), new[] { new LevelAnchor(21, 1, CakeAnchorType.Flow, Vector3.zero),
                    new LevelAnchor(21, 1, CakeAnchorType.Risk, Vector3.one) }, 1, Vector3.zero));
        }

        [Test]
        public void MissingRoomsExitAndInvalidAccessAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Graph(new LevelEdge(11, 1, 99, true)));
            Assert.Throws<ArgumentException>(() => Graph(new LevelEdge(11, 1, 2, true, TraversalAccess.None)));
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.Build(new[] { Room(1) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 99, Vector3.zero));
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.Build(new[] { Room(1) },
                Array.Empty<LevelEdge>(), new[] { new LevelAnchor(21, 99, CakeAnchorType.Flow, Vector3.zero) },
                1, Vector3.zero));
        }

        [Test]
        public void InvalidOriginNonFiniteCoordinatesAndNonPositiveSizesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.TopologicalDistancesFrom(Graph(), 99));
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.Build(
                new[] { new LevelRoom(1, Vector3.zero, Vector3.zero) }, Array.Empty<LevelEdge>(),
                Array.Empty<LevelAnchor>(), 1, Vector3.zero));
            Assert.Throws<ArgumentException>(() => LevelGraphUtility.Build(new[] { Room(1) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, new Vector3(float.NaN, 0f, 0f)));
        }
    }
}

