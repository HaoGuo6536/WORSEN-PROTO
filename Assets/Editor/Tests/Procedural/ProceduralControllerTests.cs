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
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<ProceduralConfig>();
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

        private ProceduralLayout Generate(int seed, int round)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
    }
}
