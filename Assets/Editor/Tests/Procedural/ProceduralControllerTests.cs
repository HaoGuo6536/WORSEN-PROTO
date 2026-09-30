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
//   - Check seeded variation, bounded growth, loops and diverse typed cake candidates.
//   - Check candidate support, clearance, stable identities and rejected invalid graphs.
//   - Keep presentation room bounds aligned with enclosed playable room volumes.
//   - Expect a validated spawn subset, not every non-player room, under first-contact rules.
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
            Assert.That(layout.Graph.Anchors.Count, Is.InRange(expected * _config.MinimumCandidatesPerRoom,
                expected * _config.MaximumCandidatesPerRoom));
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

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
        public void TwentySeedsHaveDiverseBoundedDeterministicCandidates(bool castle, bool refuge)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = castle;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var types = new System.Collections.Generic.HashSet<CakeAnchorType>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var layout = Generate(seed, 3, refuge);
                Assert.That(layout.Graph.Anchors, Is.EqualTo(Generate(seed, 3, refuge).Graph.Anchors),
                    "Order, ids, room ids, types and positions must all repeat.");
                Assert.That(layout.Graph.Anchors.Select(a => a.Id).Distinct().Count(), Is.EqualTo(layout.Graph.Anchors.Count));
                foreach (var room in layout.Graph.Rooms)
                {
                    var anchors = layout.Graph.Anchors.Where(anchor => anchor.RoomId == room.Id).ToArray();
                    Assert.That(anchors.Length, Is.InRange(_config.MinimumCandidatesPerRoom, _config.MaximumCandidatesPerRoom));
                    Assert.That(anchors.Select(a => a.Type).Distinct().Count(), Is.GreaterThan(1), "Seed " + seed + " room " + room.Id);
                    Assert.That(anchors.Select(a => a.Position).Distinct().Count(), Is.EqualTo(anchors.Length));
                    Assert.That(anchors.All(anchor => room.Bounds.Contains(anchor.Position)), Is.True);
                    foreach (var anchor in anchors) types.Add(anchor.Type);
                    foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
                    {
                        Assert.That(LevelGraphUtility.TopologicalDistancesFrom(layout.Graph, 1, actor)[room.Id], Is.GreaterThanOrEqualTo(0));
                        Assert.That(LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, actor)[room.Id], Is.GreaterThanOrEqualTo(0));
                    }
                }
            }
            Assert.That(types, Is.EquivalentTo(castle && !refuge ? (CakeAnchorType[])Enum.GetValues(typeof(CakeAnchorType)) :
                new[] { CakeAnchorType.Flow, CakeAnchorType.Detour, CakeAnchorType.Risk }));
        }

        [TestCase(1)] [TestCase(3)] [TestCase(5)]
        public void FixedCandidateBudgetIsHonoredWithoutRenumberingSockets(int count)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_minimumCandidatesPerRoom").intValue = count;
            settings.FindProperty("_maximumCandidatesPerRoom").intValue = count;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var first = Generate(19, 3);
            Assert.That(first.Graph.Rooms.All(room => first.Graph.Anchors.Count(a => a.RoomId == room.Id) == count), Is.True);
            settings.FindProperty("_minimumCandidatesPerRoom").intValue = 5;
            settings.FindProperty("_maximumCandidatesPerRoom").intValue = 5;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var second = Generate(19, 3);
            foreach (var anchor in first.Graph.Anchors)
            foreach (var other in second.Graph.Anchors.Where(a => a.RoomId == anchor.RoomId && a.Position == anchor.Position))
                Assert.That(other.Id, Is.EqualTo(anchor.Id));
        }

        [Test]
        public void ZeroPreferenceExcludesTypeAndImpossibleBudgetsThrow()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_flowPreference").floatValue = 0f;
            settings.FindProperty("_minimumCandidatesPerRoom").intValue = 3;
            settings.FindProperty("_maximumCandidatesPerRoom").intValue = 3;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Generate(19, 3).Graph.Anchors.Any(a => a.Type == CakeAnchorType.Flow), Is.False);
            settings.FindProperty("_detourPreference").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<InvalidOperationException>(() => Generate(19, 3));
        }

        [TestCase("_precisionPreference", -1f)] [TestCase("_riskPreference", float.NaN)]
        [TestCase("_verticalPreference", float.PositiveInfinity)] [TestCase("_candidatePerimeterInset", 0f)]
        [TestCase("_candidatePerimeterInset", 6f)]
        public void InvalidCandidateSettingsAreRejected(string field, float value)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty(field).floatValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentException>(() => Generate(1, 1));
        }

        [TestCase(0, 5)] [TestCase(4, 3)] [TestCase(3, 6)]
        public void InvalidCandidateCountsAreRejected(int minimum, int maximum)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_minimumCandidatesPerRoom").intValue = minimum;
            settings.FindProperty("_maximumCandidatesPerRoom").intValue = maximum;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentException>(() => Generate(1, 1));
        }

        [TestCase(false)] [TestCase(true)]
        public void GraphValidationStillRejectsUnreachableOrOutOfRoomCake(bool outsideRoom)
        {
            var layout = Generate(1, 1);
            var target = layout.Graph.Anchors.First(a => a.RoomId == 2);
            var anchors = layout.Graph.Anchors.Select(a => outsideRoom && a.Id == target.Id ?
                new LevelAnchor(a.Id, a.RoomId, a.Type, a.Position + Vector3.up * 100f) : a).ToArray();
            var edges = layout.Graph.Edges.Where(e => outsideRoom || (e.FromRoomId != 2 && e.ToRoomId != 2)).ToArray();
            var graph = LevelGraphUtility.Build(layout.Graph.Rooms, edges, anchors, layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
            var validate = typeof(ProceduralController).GetMethod("ValidateGraph",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => validate.Invoke(null, new object[] { graph }));
            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [TestCase(false)] [TestCase(true)]
        public void CandidateTypesMatchSupportedClearModuleDestinations(bool castle)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = castle;
            settings.FindProperty("_initialRoomCount").intValue = 7;
            settings.FindProperty("_minimumCandidatesPerRoom").intValue = 5;
            settings.FindProperty("_maximumCandidatesPerRoom").intValue = 5;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    var layout = Generate(seed, 3);
                    var blocks = new ProceduralGeometryPresenter().Build(layout, _config, driver);
                    foreach (var anchor in layout.Graph.Anchors)
                    {
                        var room = layout.Graph.Rooms.Single(r => r.Id == anchor.RoomId);
                        var module = layout.Modules.Single(m => m.RoomId == room.Id);
                        var local = anchor.Position - new Vector3(room.Center.x, 0f, room.Center.z);
                        if (!module.AlongX) local = new Vector3(local.z, local.y, local.x);
                        Assert.That(blocks.Any(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Floor &&
                            new Bounds(Vector3.zero, b.Size).Contains(Quaternion.Inverse(b.Rotation) *
                                (anchor.Position - Vector3.up * (_config.AnchorHeight + 0.01f) - b.Center))), Is.True);
                        var standing = new Bounds(anchor.Position + Vector3.up * 0.91f, new Vector3(0.6f, 1.8f, 0.6f));
                        foreach (var block in blocks.Where(b => b.HasCollision))
                        {
                            var bounds = new Bounds(block.Center, Vector3.zero);
                            for (int corner = 0; corner < 8; corner++)
                                bounds.Encapsulate(block.Center + block.Rotation * new Vector3(
                                    (corner & 1) == 0 ? -block.Size.x : block.Size.x,
                                    (corner & 2) == 0 ? -block.Size.y : block.Size.y,
                                    (corner & 4) == 0 ? -block.Size.z : block.Size.z) * 0.5f);
                            Assert.That(bounds.Intersects(standing), Is.False, "Blocked " + anchor.Type + " seed " + seed);
                        }
                        if (anchor.Type == CakeAnchorType.Precision || anchor.Type == CakeAnchorType.Vertical || anchor.Position.y > 1f)
                        {
                            Assert.That(anchor.Position.y, Is.EqualTo(_config.UpperDeckHeight + _config.AnchorHeight));
                            Assert.That(blocks.Any(b => b.RoomId == room.Id && b.Role == ProceduralBlockRole.StairRamp), Is.True);
                            if (anchor.Type == CakeAnchorType.Vertical)
                                Assert.That(local.x, Is.EqualTo(-3f).Within(0.001f), "Vertical is above the stair head.");
                            if (anchor.Type == CakeAnchorType.Precision)
                                Assert.That(module.Kind == ProceduralModuleKind.SplitLevelLibrary ? local.x : local.z,
                                    Is.EqualTo(module.Kind == ProceduralModuleKind.SplitLevelLibrary ? 3.45f : 1.65f).Within(0.001f));
                        }
                        else if (anchor.Type == CakeAnchorType.Flow)
                            Assert.That(layout.Doors.Any(d => !d.IsOptional && (d.FromRoomId == room.Id || d.ToRoomId == room.Id) &&
                                Vector3.Distance(d.Center, anchor.Position - Vector3.up * _config.AnchorHeight) <= _config.CandidatePerimeterInset + 0.001f), Is.True);
                        else
                        {
                            float side = _config.RoomSize * 0.5f - _config.CandidatePerimeterInset;
                            Assert.That(Mathf.Abs(local.x), Is.EqualTo(side).Within(0.001f));
                            Assert.That(Mathf.Abs(local.z), Is.EqualTo(side).Within(0.001f));
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(driver); }
        }

        [Test]
        public void SpawnExitAndEnemyPositionsAvoidPartitionMidline()
        {
            var layout = Generate(7, 3);
            Assert.That(layout.Graph.Rooms[0].Bounds.Contains(layout.PlayerSpawnPosition), Is.True);
            Assert.That(layout.Graph.Rooms[layout.Graph.ExitRoomId - 1].Bounds.Contains(layout.Graph.ExitPosition), Is.True);
            Assert.That(layout.HunterSpawnPositions.Count, Is.InRange(1, layout.Graph.Rooms.Count - 2));
            foreach (var position in layout.HunterSpawnPositions)
                Assert.That(ProceduralSpawnUtility.Validate(layout, position, _config.DoorWidth,
                    layout.MinimumHunterSpawnRooms, out _), Is.True);
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

        private ProceduralLayout Generate(int seed, int round, bool refuge = false)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round, refuge);
    }
}
