// ============================================================================
// LevelControllerTests.cs
// ============================================================================
// PURPOSE:
//   Validates graph assembly from the exact marker records used by the scene.
//   These tests keep missing authoring, stale graphs and duplicate identities
//   observable before any downstream system is initialized.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level.
// KEY RESPONSIBILITIES:
//   - Cover Horror progression and Shift-to-run while preserving fixture motion intent.
//   - Exercise registration snapshots, one-way routes, anchors and invalidation.
// DEPENDENCIES:
//   - Domain LevelController, Core records and NUnit; value-only Unity types.
// USAGE NOTES:
//   Pure Edit Mode tests. No scene, random source or clock is required.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;

namespace Worsen.Tests.Level
{
    public sealed class LevelControllerTests
    {
        [Test]
        public void GeneratedGraphCanReplaceAuthoredGraphAndNullInvalidatesReadiness()
        {
            var state = new LevelBehaviorState();
            var controller = new LevelController(state);
            controller.Rebuild(Records());
            var source = state.Graph;
            controller.LoadGenerated(source);
            Assert.That(state.IsReady, Is.True);
            CollectionAssert.AreEqual(source.Rooms.Select(room => room.Id), state.Graph.Rooms.Select(room => room.Id));
            Assert.Throws<ArgumentNullException>(() => controller.LoadGenerated(null));
            Assert.That(state.IsReady, Is.False);
            Assert.That(state.Graph, Is.Null);
        }

        private static List<LevelMarkerRecord> Records()
        {
            return new List<LevelMarkerRecord>
            {
                new LevelMarkerRecord(2, LevelMarkerKind.Room, 2, 0, Vector3.one, Vector3.one),
                new LevelMarkerRecord(1, LevelMarkerKind.Room, 1, 0, Vector3.zero, Vector3.one),
                new LevelMarkerRecord(10, LevelMarkerKind.RoomLink, 1, 2, Vector3.zero, Vector3.one),
                new LevelMarkerRecord(90, LevelMarkerKind.ExitMarker, 1, 0, Vector3.zero, Vector3.one)
            };
        }

        [Test]
        public void InitialSnapshotProducesReadySortedRoomsWithoutLifecycleOrdering()
        {
            var state = new LevelBehaviorState();
            new LevelController(state).Rebuild(Records());
            Assert.That(state.IsReady, Is.True);
            Assert.That(state.Graph.Rooms.Select(room => room.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(state.Graph.ExitRoomId, Is.EqualTo(1));
        }

        [Test]
        public void EveryAnchorTypeKeepsItsStableIdentityAndPosition()
        {
            var state = new LevelBehaviorState();
            var records = Records();
            foreach (CakeAnchorType type in Enum.GetValues(typeof(CakeAnchorType)))
                records.Add(new LevelMarkerRecord(100 + (int)type, LevelMarkerKind.CakeAnchor,
                    2, 0, new Vector3((int)type, 1f, 2f), Vector3.one, anchorType: type));
            new LevelController(state).Rebuild(records);
            Assert.That(state.Graph.Anchors.Count, Is.EqualTo(5));
            foreach (var anchor in state.Graph.Anchors)
            {
                Assert.That(anchor.Id, Is.EqualTo(100 + (int)anchor.Type));
                Assert.That(anchor.Position.x, Is.EqualTo((int)anchor.Type));
            }
        }

        [Test]
        public void OneWayDropCannotBecomeBidirectionalByMistakenAuthoringFlag()
        {
            var state = new LevelBehaviorState();
            var records = Records();
            records.Add(new LevelMarkerRecord(11, LevelMarkerKind.OneWayDrop, 2, 1,
                Vector3.one, Vector3.one, true, TraversalAccess.Player));
            new LevelController(state).Rebuild(records);
            Assert.That(state.Graph.Edges[1].Bidirectional, Is.False);
            Assert.That(state.Graph.Edges[1].Access, Is.EqualTo(TraversalAccess.Player));
        }

        [Test]
        public void IncompleteRebuildInvalidatesAnEarlierUsableSnapshot()
        {
            var state = new LevelBehaviorState();
            var controller = new LevelController(state);
            var records = Records();
            controller.Rebuild(records);
            records.RemoveAll(record => record.Kind == LevelMarkerKind.ExitMarker);
            Assert.Throws<ArgumentException>(() => controller.Rebuild(records));
            Assert.That(state.IsReady, Is.False);
            Assert.That(state.Graph, Is.Null);
        }

        [Test]
        public void DuplicateMarkerIdsAndMultipleExitsAreRejected()
        {
            var controller = new LevelController(new LevelBehaviorState());
            var records = Records();
            records.Add(records[0]);
            Assert.Throws<ArgumentException>(() => controller.Rebuild(records));
            records = Records();
            records.Add(new LevelMarkerRecord(91, LevelMarkerKind.ExitMarker, 2, 0, Vector3.one, Vector3.one));
            Assert.Throws<ArgumentException>(() => controller.Rebuild(records));
        }

        [Test]
        public void MissingRoomAndMismatchedRoomIdentityAreRejected()
        {
            var controller = new LevelController(new LevelBehaviorState());
            var records = Records();
            records.Add(new LevelMarkerRecord(100, LevelMarkerKind.VaultSurface, 99, 0, Vector3.zero, Vector3.one));
            Assert.Throws<ArgumentException>(() => controller.Rebuild(records));
            records = Records();
            records[0] = new LevelMarkerRecord(2, LevelMarkerKind.Room, 7, 0, Vector3.one, Vector3.one);
            Assert.Throws<ArgumentException>(() => controller.Rebuild(records));
        }

        [Test]
        public void ExplicitInvalidationClearsReadinessAndSnapshot()
        {
            var state = new LevelBehaviorState();
            var controller = new LevelController(state);
            controller.Rebuild(Records());
            controller.Invalidate();
            Assert.That(state.IsReady, Is.False);
            Assert.That(state.Graph, Is.Null);
        }
    }
}
