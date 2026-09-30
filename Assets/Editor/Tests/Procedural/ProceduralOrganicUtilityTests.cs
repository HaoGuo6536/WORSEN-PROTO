// ============================================================================
// ProceduralOrganicUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises carved reservations through the real generation and shell pipeline.
//   Graph connectivity is supplemented by an occupied-tile flood fill, physical
//   aperture/anchor checks, and publication/pocket/shrine identity assertions.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Sweep shapes, areas and deterministic room identities with fully wired configs.
//   - Check connected portal landings, candidate support and spawn policy.
//   - Retain a largest-default-floor timing probe for the coordinator.
// DEPENDENCIES:
//   - NUnit, Core, Domain.Procedural and UnityEditor serialization.
// USAGE NOTES:
//   Stopwatch output measures pure generation plus shell construction, not Unity
//   import, GameObject creation, rendering, physics or NavMesh baking.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralOrganicUtilityTests
    {
        private ProceduralConfig _config;
        private ProceduralChallengeConfig _challenges;
        private ProceduralOrganicConfig _organic;
        private ProceduralThemeConfig _themes;
        private ProceduralDriverConfig _driver;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>();
            _challenges = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            _organic = ScriptableObject.CreateInstance<ProceduralOrganicConfig>();
            _themes = ScriptableObject.CreateInstance<ProceduralThemeConfig>();
            _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            var edit = new SerializedObject(_config);
            edit.FindProperty("_challenges").objectReferenceValue = _challenges;
            edit.FindProperty("_organic").objectReferenceValue = _organic;
            edit.FindProperty("_themes").objectReferenceValue = _themes;
            edit.FindProperty("_gapProbability").floatValue = 1f;
            edit.FindProperty("_pocketProbability").floatValue = 1f;
            edit.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown] public void TearDown()
        {
            foreach (var obj in new UnityEngine.Object[] { _config, _challenges, _organic, _themes, _driver })
                UnityEngine.Object.DestroyImmediate(obj);
        }
        private ProceduralLayout Generate(int seed, int round) => new ProceduralController(new ProceduralBehaviorState(), _config,
            new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);

        [Test]
        public void SeedSweepPreservesConnectedCellsAperturesAnchorsAndCollapseIdentity()
        {
            var shapes = new HashSet<ProceduralRoomShape>(); var areas = new HashSet<int>();
            int shrines = 0, bentHallways = 0;
            for (int seed = 0; seed < 32; seed++)
            foreach (int round in new[] { 1, 2, 3, 8 })
            {
                var layout = Generate(seed, round);
                Assert.That(layout.Manifest, Is.EqualTo(Generate(seed, round).Manifest));
                ProceduralFootprintUtility.Validate(layout); ProceduralStoreyUtility.Validate(layout, _config);
                Assert.That(layout.OrganicRooms.Any(r => r.RoomId == layout.Graph.ExitRoomId), Is.False);
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                foreach (var room in layout.OrganicRooms)
                {
                    shapes.Add(room.Shape); areas.Add(room.Tiles.Count);
                    var tiles = new HashSet<Vector2Int>(room.Tiles);
                    var reached = new HashSet<Vector2Int> { room.Tiles[0] };
                    var queue = new Queue<Vector2Int>(); queue.Enqueue(room.Tiles[0]);
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        foreach (var direction in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                        { var next = current + direction; if (tiles.Contains(next) && reached.Add(next)) queue.Enqueue(next); }
                    }
                    Assert.That(reached.Count, Is.EqualTo(tiles.Count));
                    int rectangle = (tiles.Max(v => v.x) - tiles.Min(v => v.x) + 1) * (tiles.Max(v => v.y) - tiles.Min(v => v.y) + 1);
                    if (room.Shape == ProceduralRoomShape.Hallway && rectangle > tiles.Count) bentHallways++;
                    var graphRoom = layout.Graph.Rooms.Single(r => r.Id == room.RoomId);
                    Assert.That(graphRoom.Cells.Sum(c => c.size.x * c.size.z), Is.EqualTo(tiles.Count * 4f));
                    Assert.That(layout.PresentationRooms.Single(r => r.RoomId == room.RoomId).Cells, Is.EqualTo(graphRoom.Cells));
                    foreach (var tile in tiles)
                        Assert.That(ProceduralFootprintUtility.At(layout, ProceduralOrganicUtility.Center(layout, tile, 1f)).Id,
                            Is.EqualTo(room.RoomId));
                    Assert.That(layout.Graph.Anchors.Count(a => a.RoomId == room.RoomId),
                        Is.InRange(_config.MinimumCandidatesPerRoom, _config.MaximumCandidatesPerRoom));
                }
                foreach (var door in layout.Doors.Where(d => !d.IsOptional))
                {
                    var normal = door.AlongX ? Vector3.forward : Vector3.right;
                    var tangent = door.AlongX ? Vector3.right : Vector3.forward;
                    foreach (float side in new[] { -.9f, .9f })
                    {
                        var p = door.Center + normal * side + Vector3.up;
                        Assert.That(new[] { door.FromRoomId, door.ToRoomId }, Does.Contain(ProceduralFootprintUtility.At(layout, p).Id));
                    }
                    foreach (float x in new[] { -1.5f, 0f, 1.5f })
                        Assert.That(blocks.Any(b => b.HasCollision && Contains(b, door.Center + tangent * x + Vector3.up)), Is.False);
                }
                foreach (var anchor in layout.Graph.Anchors.Concat(layout.PocketAnchors))
                {
                    foreach (var shape in layout.OrganicRooms.Where(r => r.RoomId == anchor.RoomId))
                        Assert.That(ProceduralOrganicUtility.Clear(shape, anchor.Position), Is.True);
                    Assert.That(blocks.Any(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Floor &&
                        Contains(b, anchor.Position - Vector3.up * (_config.AnchorHeight + .01f))), Is.True);
                    Assert.That(blocks.Any(b => b.HasCollision && Contains(b, anchor.Position + Vector3.up)), Is.False);
                }
                foreach (var hunter in layout.HunterSpawnPositions)
                    Assert.That(ProceduralSpawnUtility.Validate(layout, hunter, _config.DoorWidth, layout.MinimumHunterSpawnRooms, out _), Is.True);
                var props = new ProceduralInteractablePresenter().Build(layout, _config, _driver, blocks, new System.Random(seed));
                Assert.That(props.Select(p => p.State.Id).Distinct().Count(), Is.EqualTo(props.Count));
                shrines += new ProceduralShrineSitePresenter().Build(layout, _config, blocks).Count;
                if (round >= 2)
                {
                    Assert.That(layout.GapSites, Is.Not.Empty); Assert.That(layout.PocketAnchors, Is.Not.Empty);
                    foreach (var gap in layout.GapSites)
                        Assert.That(blocks.Any(b => b.HasCollision && Contains(b, gap.Edge + Vector3.up)), Is.True);
                }
            }
            Assert.That(shapes, Is.SupersetOf(new[] { ProceduralRoomShape.Rectangle, ProceduralRoomShape.Hallway,
                ProceduralRoomShape.LShape, ProceduralRoomShape.TShape, ProceduralRoomShape.Round }));
            Assert.That(areas.Count, Is.GreaterThan(3)); Assert.That(bentHallways, Is.GreaterThan(0)); Assert.That(shrines, Is.GreaterThan(0));
        }

        [Test]
        public void LargestDefaultFloorReportsPureGenerationAndShellCost()
        {
            Generate(7, 8); // Warm managed code before measuring.
            var watch = Stopwatch.StartNew(); int maxBlocks = 0, maxTiles = 0;
            for (int seed = 0; seed < 32; seed++)
            {
                var layout = Generate(seed, 8);
                maxBlocks = Math.Max(maxBlocks, new ProceduralGeometryPresenter().Build(layout, _config, _driver).Count);
                maxTiles = Math.Max(maxTiles, layout.OrganicRooms.Sum(r => r.Tiles.Count));
                Assert.That(layout.Graph.Rooms.Count, Is.LessThanOrEqualTo(_config.MaximumRoomCount + _config.PocketRoomCount));
                Assert.That(layout.OrganicRooms.All(r => r.Tiles.Count <= 108), Is.True);
            }
            watch.Stop();
            TestContext.WriteLine("32 seeds, round 8; generation+shell ms=" + watch.Elapsed.TotalMilliseconds +
                "; max blocks=" + maxBlocks + "; max organic tiles=" + maxTiles);
        }
        private static bool Contains(ProceduralBlock block, Vector3 point) =>
            new Bounds(Vector3.zero, block.Size).Contains(Quaternion.Inverse(block.Rotation) * (point - block.Center));
    }
}
