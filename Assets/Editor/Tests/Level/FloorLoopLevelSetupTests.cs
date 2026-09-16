// ============================================================================
// FloorLoopLevelSetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks the FloorLoop authoring snapshot against collapse and navigation
//   prerequisites without creating scene objects or mutating generated assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level.
// KEY RESPONSIBILITIES:
//   - Reject uncovered floors, misplaced anchors, isolated rooms and steep seams.
// DEPENDENCIES:
//   - Level content builder, Domain LevelController, Core records and NUnit.
// USAGE NOTES:
//   Value-only authoring checks. The coordinator separately bakes navigation and
//   probes actual paths and movement; topology alone cannot establish walkability.
// ============================================================================

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Editor.Level;

namespace Worsen.Tests.Level
{
    public sealed class FloorLoopLevelSetupTests
    {
        private static LevelGraph Graph()
        {
            var state = new LevelBehaviorState();
            new LevelController(state).Rebuild(FloorLoopLevelSetup.CreateMarkerRecords());
            Assert.That(state.IsReady, Is.True);
            return state.Graph;
        }

        [Test]
        public void AuthoringSnapshotHasStableGlobalIdentitiesAndOneExit()
        {
            var first = FloorLoopLevelSetup.CreateMarkerRecords();
            var second = FloorLoopLevelSetup.CreateMarkerRecords();
            Assert.That(first.Select(record => record.Id).Distinct().Count(), Is.EqualTo(first.Count));
            Assert.That(first.Select(record => record.Id), Is.All.GreaterThan(0));
            Assert.That(second, Is.EqualTo(first));
            var graph = Graph();
            Assert.That(graph.Rooms.Select(room => room.Id), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(graph.ExitRoomId, Is.EqualTo(6));
            Assert.That(StrictlyInside(graph.Rooms.Single(room => room.Id == graph.ExitRoomId), graph.ExitPosition), Is.True);
        }

        [Test]
        public void EighteenWeightedAnchorsAreOnTheFloorInsideTheirExactRoom()
        {
            var graph = Graph();
            Assert.That(graph.Anchors.Count, Is.EqualTo(18));
            Assert.That(graph.Anchors.Count, Is.GreaterThanOrEqualTo(14));
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(CakeAnchorType)), graph.Anchors.Select(anchor => anchor.Type).Distinct());
            foreach (var anchor in graph.Anchors)
            {
                var room = graph.Rooms.Single(candidate => candidate.Id == anchor.RoomId);
                Assert.That(StrictlyInside(room, anchor.Position), Is.True, "Anchor " + anchor.Id);
                Assert.That(anchor.Position.y, Is.EqualTo(FloorLoopLevelSetup.FloorHeight(anchor.Position.x) + 0.1f).Within(0.0001f));
                Assert.That(graph.Rooms.Count(candidate => StrictlyInside(candidate, anchor.Position)), Is.EqualTo(1));
            }
            Assert.That(graph.Anchors.GroupBy(anchor => anchor.RoomId).Select(group => group.Count()), Is.All.EqualTo(3));
        }

        [Test]
        public void SpawnsAreSeparatedAndStrictlyOwnedByDifferentRooms()
        {
            var graph = Graph();
            var player = FloorLoopLevelSetup.SpawnPosition;
            var hunter = FloorLoopLevelSetup.HunterSpawnPosition;
            Assert.That(graph.Rooms.Single(room => StrictlyInside(room, player)).Id, Is.EqualTo(1));
            Assert.That(graph.Rooms.Single(room => StrictlyInside(room, hunter)).Id, Is.EqualTo(6));
            Assert.That(Vector3.Distance(player, hunter), Is.GreaterThan(14f));
            Assert.That(player.y - FloorLoopLevelSetup.FloorHeight(player.x), Is.InRange(0f, 0.2f));
            Assert.That(hunter.y - FloorLoopLevelSetup.FloorHeight(hunter.x), Is.InRange(0f, 0.2f));
        }

        [TestCase(TraversalAccess.Player)]
        [TestCase(TraversalAccess.Hunter)]
        public void EveryRoomAndAnchorIsReachableByOrdinaryBidirectionalRoutes(TraversalAccess actor)
        {
            var graph = Graph();
            Assert.That(graph.Edges.Count, Is.EqualTo(11));
            foreach (var edge in graph.Edges)
            {
                Assert.That(edge.Access, Is.EqualTo(TraversalAccess.All));
                Assert.That(edge.Bidirectional, Is.True);
            }
            foreach (var room in graph.Rooms)
            {
                Assert.That(LevelGraphUtility.TopologicalDistancesFrom(graph, room.Id, actor).Values, Is.All.GreaterThanOrEqualTo(0));
                var distinctNeighbors = graph.Edges.Where(edge => edge.FromRoomId == room.Id || edge.ToRoomId == room.Id)
                    .Select(edge => edge.FromRoomId == room.Id ? edge.ToRoomId : edge.FromRoomId).Distinct().Count();
                Assert.That(distinctNeighbors, Is.GreaterThanOrEqualTo(2), "Room " + room.Id + " needs a braid alternative.");
            }
        }

        [Test]
        public void EveryDoorwayLiesOnBothConnectedRoomBoundaries()
        {
            var graph = Graph();
            foreach (var marker in FloorLoopLevelSetup.CreateMarkerRecords().Where(marker => marker.Kind == LevelMarkerKind.RoomLink))
            {
                Assert.That(Contains(graph.Rooms.Single(room => room.Id == marker.RoomId).Bounds, marker.Position), Is.True);
                Assert.That(Contains(graph.Rooms.Single(room => room.Id == marker.TargetRoomId).Bounds, marker.Position), Is.True);
                Assert.That(Mathf.Max(marker.Size.x, marker.Size.z), Is.EqualTo(6f));
                Assert.That(marker.Position.y, Is.EqualTo(FloorLoopLevelSetup.FloorHeight(marker.Position.x)));
            }
        }

        [Test]
        public void WholeWalkableFootprintAndActorHeadroomHaveCollapseCoverage()
        {
            var graph = Graph();
            for (var x = -41.5f; x < 42f; x += 1f)
            {
                for (var z = -27.5f; z < 28f; z += 1f)
                {
                    var feet = new Vector3(x, FloorLoopLevelSetup.FloorHeight(x) + 0.05f, z);
                    var owners = graph.Rooms.Where(room => StrictlyInside(room, feet)).ToArray();
                    Assert.That(owners.Length, Is.EqualTo(1), "Uncovered or overlapping floor at " + feet);
                    Assert.That(Contains(owners[0].Bounds, feet + Vector3.up * 1.8f), Is.True);
                }
            }
            Assert.That(graph.Rooms.Sum(room => room.Size.x * room.Size.z), Is.EqualTo(84f * 56f));
        }

        [Test]
        public void AdjacentRoomsShareBoundariesWithoutOverlappingInteriors()
        {
            var rooms = Graph().Rooms;
            for (var left = 0; left < rooms.Count; left++)
            {
                for (var right = left + 1; right < rooms.Count; right++)
                {
                    var a = rooms[left].Bounds;
                    var b = rooms[right].Bounds;
                    var overlapX = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
                    var overlapZ = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
                    Assert.That(overlapX <= 0f || overlapZ <= 0f, Is.True);
                }
            }
        }

        [Test]
        public void ThreeFloorBandsJoinContinuouslyWithOrdinaryRampSlopes()
        {
            Assert.That(new[] { -28f, 0f, 28f }.Select(FloorLoopLevelSetup.FloorHeight), Is.EqualTo(new[] { 0f, 3f, 6f }));
            for (var x = -42f; x < 41.9f; x += 0.1f)
            {
                var rise = FloorLoopLevelSetup.FloorHeight(x + 0.1f) - FloorLoopLevelSetup.FloorHeight(x);
                Assert.That(rise, Is.InRange(-0.0001f, 0.0251f));
            }
        }

        private static bool Contains(Bounds bounds, Vector3 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.y >= bounds.min.y && point.y <= bounds.max.y &&
                   point.z >= bounds.min.z && point.z <= bounds.max.z;
        }

        private static bool StrictlyInside(LevelRoom room, Vector3 point)
        {
            var bounds = room.Bounds;
            return point.x > bounds.min.x && point.x < bounds.max.x &&
                   point.y > bounds.min.y && point.y < bounds.max.y &&
                   point.z > bounds.min.z && point.z < bounds.max.z;
        }
    }
}
