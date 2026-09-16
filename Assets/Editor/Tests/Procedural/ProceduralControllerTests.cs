// ============================================================================
// ProceduralControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies reproducibility, growth and reachability of generated closed rooms.
//   These tests inspect generated data without a scene so layout regressions
//   cannot be hidden by a successful navigation bake or a visual screenshot.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check seeded variation, bounded growth, loops and complete straight cake lines.
//   - Keep presentation room bounds aligned with enclosed playable room volumes.
// DEPENDENCIES:
//   - Domain.Procedural, Core contracts, NUnit and UnityEditor serialized setup.
// USAGE NOTES:
//   Config instances are temporary and are destroyed after every test.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralControllerTests
    {
        private ProceduralConfig _config;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = false;
            settings.FindProperty("_initialRoomCount").intValue = 5;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        [Test]
        public void SameSeedRoundAndConfigHaveIdenticalManifest()
            => Assert.That(Generate(431, 4).Manifest, Is.EqualTo(Generate(431, 4).Manifest));

        [Test]
        public void SeedsChangePhysicalTopologyAndDoorPlacement()
        {
            var shapes = Enumerable.Range(1, 24).Select(seed => string.Join(";", Generate(seed, 2).Graph.Rooms
                .Select(room => room.Center.x + "," + room.Center.z))).Distinct().Count();
            Assert.That(shapes, Is.GreaterThan(12));
        }

        [TestCase(1, 5)] [TestCase(2, 7)] [TestCase(3, 9)] [TestCase(6, 15)] [TestCase(int.MaxValue, 15)]
        public void RoomBudgetGrowsAndCapsWithoutIntegerOverflow(int round, int expected)
        {
            var layout = Generate(9, round);
            Assert.That(layout.Graph.Rooms.Count, Is.EqualTo(expected));
            Assert.That(layout.Graph.Anchors.Count, Is.EqualTo(expected * 10));
        }

        [Test]
        public void ManySeedsHaveUniqueCellsConnectedDoorwaysAndAnAlternateLoop()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                var layout = Generate(seed, 1 + seed % 8);
                Assert.That(layout.Cells.Distinct().Count(), Is.EqualTo(layout.Cells.Count));
                Assert.That(layout.Graph.Edges.Count, Is.GreaterThanOrEqualTo(layout.Graph.Rooms.Count));
                foreach (var edge in layout.Graph.Edges)
                {
                    var a = layout.Cells[edge.FromRoomId - 1]; var b = layout.Cells[edge.ToRoomId - 1];
                    Assert.That(Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y), Is.EqualTo(1));
                    Assert.That(edge.Bidirectional, Is.True); Assert.That(edge.Access, Is.EqualTo(TraversalAccess.All));
                }
                foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
                {
                    Assert.That(LevelGraphUtility.TopologicalDistancesFrom(layout.Graph, 1, actor).Values.All(distance => distance >= 0), Is.True);
                    Assert.That(LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, actor).Values.All(distance => distance >= 0), Is.True);
                }
            }
        }

        [Test]
        public void EveryRoomHasTwoCompleteEvenlySpacedStraightCakeLines()
        {
            var layout = Generate(64, 4);
            foreach (var module in layout.Modules)
            {
                var room = layout.Graph.Rooms[module.RoomId - 1];
                var anchors = layout.Graph.Anchors.Where(anchor => anchor.RoomId == room.Id).ToArray();
                Assert.That(anchors.Length, Is.EqualTo(10));
                var lines = anchors.GroupBy(anchor => module.AlongX ? anchor.Position.z : anchor.Position.x).ToArray();
                Assert.That(lines.Length, Is.EqualTo(2));
                foreach (var line in lines)
                {
                    var positions = line.OrderBy(anchor => module.AlongX ? anchor.Position.x : anchor.Position.z).ToArray();
                    Assert.That(positions.Length, Is.EqualTo(5));
                    for (int index = 1; index < positions.Length; index++)
                        Assert.That(Vector3.Distance(positions[index - 1].Position, positions[index].Position), Is.EqualTo(2f).Within(0.0001f));
                }
                Assert.That(anchors.All(anchor => room.Bounds.Contains(anchor.Position)), Is.True);
            }
        }

        [Test]
        public void SpawnExitAndEnemyPositionsAvoidPartitionMidline()
        {
            var layout = Generate(7, 3);
            Assert.That(layout.Graph.Rooms[0].Bounds.Contains(layout.PlayerSpawnPosition), Is.True);
            Assert.That(layout.Graph.Rooms[layout.Graph.ExitRoomId - 1].Bounds.Contains(layout.Graph.ExitPosition), Is.True);
            Assert.That(layout.HunterSpawnPositions.Count, Is.EqualTo(layout.Graph.Rooms.Count - 1));
            var firstRoom = layout.Graph.Rooms[0];
            var delta = layout.PlayerSpawnPosition - firstRoom.Center;
            Assert.That(layout.Modules[0].AlongX ? Mathf.Abs(delta.z) : Mathf.Abs(delta.x), Is.EqualTo(4f));
        }

        [Test]
        public void InvalidBudgetIsRejectedWithoutPublishingReadyState()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_initialRoomCount").intValue = 3; serialized.ApplyModifiedPropertiesWithoutUndo();
            var state = new ProceduralBehaviorState();
            var controller = new ProceduralController(state, _config, new System.Random(1));
            Assert.Throws<ArgumentException>(() => controller.Generate(1, 1));
            Assert.That(state.IsReady, Is.False); Assert.That(state.Layout, Is.Null);
        }

        [Test]
        public void GenerateNeedsExplicitPhysicalAdmissionAndResetInvalidatesIt()
        {
            var state = new ProceduralBehaviorState();
            var controller = new ProceduralController(state, _config, new System.Random(1));
            controller.Generate(1, 1); Assert.That(state.IsReady, Is.False);
            controller.Admit(); Assert.That(state.IsReady, Is.True);
            controller.Reset(); Assert.That(state.IsReady, Is.False); Assert.That(state.Layout, Is.Null);
        }

        [Test]
        public void CastlePresentationRoomsPreservePortalFactsAndOnlyMarkSafeRedundantRooms()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = Generate(seed, 3);
                Assert.That(layout.PresentationRooms.Count, Is.EqualTo(layout.Graph.Rooms.Count));
                Assert.That(layout.PresentationRooms.Any(r => r.OptionalRoom), Is.True, "Loop must supply a safe optional-effect candidate.");
                foreach (var sample in layout.PresentationRooms)
                {
                    var room = layout.Graph.Rooms.Single(r => r.Id == sample.RoomId);
                    Assert.That(sample.Bounds, Is.EqualTo(room.Bounds));
                    Assert.That(sample.PortalCenters, Is.EqualTo(layout.Doors.Where(d => d.FromRoomId == room.Id || d.ToRoomId == room.Id).Select(d => d.Center).ToArray()));
                    Assert.That(sample.OpenSky, Is.False, "All castle rooms are enclosed, including the higher-ceiling families.");
                    if (!sample.OptionalRoom) continue;
                    Assert.That(sample.RoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                    Assert.That(sample.Bounds.Contains(layout.PlayerSpawnPosition), Is.False, "Actual spawn room must be protected.");
                    var reduced = LevelGraphUtility.Build(layout.Graph.Rooms.Where(r => r.Id != sample.RoomId).ToArray(),
                        layout.Graph.Edges.Where(e => e.FromRoomId != sample.RoomId && e.ToRoomId != sample.RoomId && e.Access == TraversalAccess.All).ToArray(),
                        layout.Graph.Anchors.Where(a => a.RoomId != sample.RoomId).ToArray(), layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
                    foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
                        Assert.That(LevelGraphUtility.DistancesTo(reduced, reduced.ExitRoomId, actor).Values.All(d => d >= 0), Is.True);
                }
            }
        }

        [Test]
        public void CastlePlayerSpawnsClearOfEveryCakeAcrossFamiliesAndRounds()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = true;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var families = new System.Collections.Generic.HashSet<ProceduralModuleKind>();
            foreach (int round in new[] { 1, 2, 20 })
            foreach (int seed in Enumerable.Range(0, 40).Concat(new[] { 1701, 1980825774 }))
            {
                var layout = Generate(seed, round);
                families.Add(layout.Modules[1].Kind);
                foreach (var anchor in layout.Graph.Anchors)
                {
                    var delta = anchor.Position - layout.PlayerSpawnPosition;
                    Assert.That(new Vector2(delta.x, delta.z).magnitude, Is.GreaterThanOrEqualTo(1f),
                        "Spawn must not collect cake without input: seed " + seed + " round " + round + " anchor " + anchor.Id);
                }
            }
            Assert.That(families.Count, Is.EqualTo(5), "Exercise hub starts beside every possible neighboring combat room family.");
        }

        private ProceduralLayout Generate(int seed, int round)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
    }
}
