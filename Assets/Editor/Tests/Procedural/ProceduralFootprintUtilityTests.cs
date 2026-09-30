// ============================================================================
// ProceduralFootprintUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises larger rooms and reserved gaps against the exact cell model used by
//   generation, shells and spawn checks. Declared seed samples measure distribution,
//   connected usable area and ordinary route growth without a navigation bake.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check footprints, apertures, support, pocket isolation and full determinism.
//   - Reject pocket objectives and retain their failure in the bounded retry journal.
//   - Verify Core/presentation cell publication, pocket flags, ceiling heights and storeys.
// DEPENDENCIES:
//   - Core, Domain.Procedural, NUnit and temporary Unity configuration instances.
// USAGE NOTES:
//   Samples are seeds 0..99 for weights and 0..49 for geometry/growth. A 1m ground
//   grid sweeps a 0.6m by 1.8m standing box against collision bounds. This conservative
//   area/route measurement excludes pockets and is not native NavMesh evidence.
//   Physical traversal and Environment/Floor integration remain coordinator gates.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralFootprintUtilityTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driver;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_driver); }
        private void Set(string field, object value) => typeof(ProceduralConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config, value);
        private ProceduralLayout Generate(int seed, int round = 3, bool refuge = false) => new ProceduralController(
            new ProceduralBehaviorState(), _config, new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round, refuge);
        private static bool Bent(ProceduralRoomModule m) => m.Cells.Count == 3 &&
            m.Cells.Select(c => c.x).Distinct().Count() == 2 && m.Cells.Select(c => c.y).Distinct().Count() == 2;

        [Test]
        public void HundredSeedsRespectWeightsOutsideTheFixedHubLoop()
        {
            var sampled = Enumerable.Range(0, 100).SelectMany(seed => Generate(seed, 10).Modules
                .Where(m => m.RoomId > 6 && m.PocketId == 0)).ToArray();
            foreach (var pair in new[] { (1, 0.55), (2, 0.3), (3, 0.15) })
                Assert.That(sampled.Count(m => m.Cells.Count == pair.Item1) / (double)sampled.Length,
                    Is.EqualTo(pair.Item2).Within(0.06));
            Assert.That(sampled.Count(Bent) / (double)sampled.Count(m => m.Cells.Count == 3), Is.EqualTo(0.4).Within(0.13));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
        public void FiftySeedsKeepRequiredContentConnectedAndOptionalPocketsSealed(bool castle, bool refuge)
        {
            Set("_castleModules", castle); Set("_gapProbability", 1f); Set("_pocketProbability", 1f);
            for (int seed = 0; seed < 50; seed++)
            {
                var layout = Generate(seed, 3, refuge);
                Assert.DoesNotThrow(() => ProceduralFootprintUtility.Validate(layout), "seed " + seed);
                Assert.That(layout.Cells.Distinct().Count(), Is.EqualTo(layout.Cells.Count));
                Assert.That(layout.GapCells.Count, Is.InRange(1, _config.MaximumGapCells));
                Assert.That(layout.GapCells.Intersect(layout.Cells), Is.Empty);
                Assert.That(layout.GapSites, Is.Not.Empty);
                Assert.That(layout.Modules.Count(m => m.PocketId != 0), Is.EqualTo(_config.PocketRoomCount));
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                Assert.That(blocks.Where(b => b.SurfaceId != 0).Select(b => b.SurfaceId).Distinct().Count(),
                    Is.EqualTo(blocks.Count(b => b.SurfaceId != 0)));
                foreach (var module in layout.Modules)
                {
                    var room = layout.Graph.Rooms[module.RoomId - 1];
                    var volumes = ProceduralFootprintUtility.Volumes(layout, room);
                    var anchors = (module.PocketId == 0 ? layout.Graph.Anchors : layout.PocketAnchors)
                        .Where(a => a.RoomId == room.Id).ToArray();
                    Assert.That(anchors.Length, Is.InRange(3, 5));
                    foreach (var volume in volumes) Assert.That(anchors.Any(a => volume.Bounds.Contains(a.Position)), Is.True);
                    Assert.That(layout.Graph.Anchors.Any(a => a.RoomId == room.Id), Is.EqualTo(module.PocketId == 0));
                    foreach (var a in volumes)
                    foreach (var b in volumes.Where(b => b.Center.x > a.Center.x || b.Center.z > a.Center.z))
                    {
                        if (Mathf.Abs(Vector3.Distance(a.Center, b.Center) - _config.RoomSize) > 0.01f) continue;
                        var seam = (a.Center + b.Center) * 0.5f; seam.y = 1f;
                        Assert.That(blocks.Where(block => block.Kind == ProceduralSurfaceKind.Wall)
                            .Any(block => Contains(block, seam)), Is.False, "Interior seam seed " + seed);
                    }
                }
                foreach (var anchor in layout.Graph.Anchors.Concat(layout.PocketAnchors))
                {
                    Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Floor && b.HasCollision &&
                        Contains(b, anchor.Position - Vector3.up * (_config.AnchorHeight + 0.01f))), Is.True);
                    var standing = new Bounds(anchor.Position + Vector3.up * 0.91f, new Vector3(0.6f, 1.8f, 0.6f));
                    Assert.That(blocks.Where(b => b.HasCollision).Any(b => WorldBounds(b).Intersects(standing)), Is.False,
                        "Blocked anchor " + anchor.Id + " seed " + seed);
                }
                foreach (var door in layout.Doors)
                {
                    var normal = door.AlongX ? Vector3.forward : Vector3.right;
                    var a = ProceduralFootprintUtility.At(layout, door.Center + normal * 0.1f + Vector3.up);
                    var b = ProceduralFootprintUtility.At(layout, door.Center - normal * 0.1f + Vector3.up);
                    Assert.That(new[] { a.Id, b.Id }, Is.EquivalentTo(new[] { door.FromRoomId, door.ToRoomId }));
                    Assert.That(layout.Modules[a.Id - 1].PocketId, Is.EqualTo(layout.Modules[b.Id - 1].PocketId));
                    if (!door.IsOptional) Assert.That(blocks.Any(block => Contains(block, door.Center + Vector3.up)), Is.False);
                }
                foreach (var site in layout.GapSites)
                {
                    Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Wall && b.HasCollision && Contains(b, site.Edge + Vector3.up)), Is.True);
                    var view = site.Edge + Vector3.up * 2f;
                    Assert.That(blocks.Any(b => b.HasRenderer && Contains(b, view)), Is.False);
                    Assert.That(blocks.Any(b => b.Role == ProceduralBlockRole.CollisionOnly && Contains(b, view)), Is.True);
                }
                foreach (var gap in layout.GapCells)
                {
                    var point = new Vector3(_config.Origin.x + gap.x * _config.RoomSize, -0.01f, _config.Origin.y + gap.y * _config.RoomSize);
                    Assert.That(blocks.Any(b => Contains(b, point)), Is.False);
                }
                foreach (var spawn in layout.HunterSpawnPositions)
                    Assert.That(ProceduralSpawnUtility.Validate(layout, spawn, _config.DoorWidth, layout.MinimumHunterSpawnRooms, out _), Is.True);
                var presenter = new ProceduralInteractablePresenter();
                var objects = presenter.Build(layout, _config, _driver, blocks, new System.Random(seed));
                Assert.That(objects.Select(p => p.State.Id).Distinct().Count(), Is.EqualTo(objects.Count));
                foreach (var prop in objects.Where(p => p.State.Kind == InteractableKind.KnockableProp))
                    Assert.That(ProceduralFootprintUtility.At(layout, prop.State.Position).Id, Is.EqualTo(prop.State.RoomId));
                var repeated = Generate(seed, 3, refuge);
                Assert.That(layout.Manifest, Is.EqualTo(repeated.Manifest));
                Assert.That(blocks, Is.EqualTo(new ProceduralGeometryPresenter().Build(repeated, _config, _driver)));
                Assert.That(presenter.Manifest(objects), Is.EqualTo(presenter.Manifest(presenter.Build(repeated, _config, _driver,
                    blocks, new System.Random(seed)))));
            }
        }

        [TestCase(2, 0f)] [TestCase(3, 0f)] [TestCase(3, 1f)]
        public void ForcedFootprintsAndRoundGatesAreHonored(int size, float bent)
        {
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", size == 2 ? 1f : 0f); Set("_threeCellWeight", size == 3 ? 1f : 0f);
            Set("_lShapeWeight", bent); Set("_multiCellStartRound", 3); Set("_gapStartRound", 3);
            Set("_gapProbability", 1f); Set("_pocketProbability", 1f);
            Assert.That(Generate(19, 2).Modules.All(m => m.Cells.Count == 1), Is.True);
            Assert.That(Generate(19, 2).GapCells, Is.Empty);
            var layout = Generate(19);
            foreach (var module in layout.Modules.Where(m => m.RoomId > 6 && m.PocketId == 0))
            { Assert.That(module.Cells.Count, Is.EqualTo(size)); Assert.That(Bent(module), Is.EqualTo(bent == 1f)); }
            Assert.That(layout.GapSites, Is.Not.Empty);
        }

        [Test]
        public void RequiredPocketAnchorFailsAndRetryRetainsSeedAndReason()
        {
            Set("_gapProbability", 1f); Set("_pocketProbability", 1f);
            var layout = Generate(17);
            var graph = LevelGraphUtility.Build(layout.Graph.Rooms, layout.Graph.Edges,
                layout.Graph.Anchors.Concat(layout.PocketAnchors.Take(1)).ToArray(), layout.Graph.ExitRoomId, layout.Graph.ExitPosition);
            typeof(ProceduralLayout).GetProperty(nameof(ProceduralLayout.Graph)).SetValue(layout, graph);
            var failure = Assert.Throws<InvalidOperationException>(() => ProceduralFootprintUtility.Validate(layout));
            Assert.That(failure.Message, Does.Contain("Required anchor is in a pocket"));
            var state = new ProceduralBehaviorState();
            var retry = new ProceduralGenerationController(state); retry.Begin(17, 3, 1);
            Assert.That(retry.Fail(failure.Message, layout.Manifest), Is.True);
            Assert.That(state.AttemptSeed, Is.EqualTo(unchecked(17 + ProceduralGenerationController.SeedStride)));
            Assert.That(state.GenerationManifest, Does.Contain(Uri.EscapeDataString(failure.Message)));
            Assert.DoesNotThrow(() => ProceduralFootprintUtility.Validate(Generate(state.AttemptSeed)));
            Assert.That(retry.Fail("forced second failure", layout.Manifest), Is.False);
            Assert.That(state.UsedFallback, Is.True); Assert.That(state.GenerationSucceeded, Is.False);
        }

        [Test]
        public void SmallAndLargeConfigsGrowConnectedUsableAreaAndShortestWalkingRoutes()
        {
            Set("_gapProbability", 1f); Set("_pocketProbability", 1f);
            var areas = new double[2]; var routes = new double[2];
            for (int size = 0; size < 2; size++)
            {
                Set("_initialRoomCount", size == 0 ? 7 : 24); Set("_maximumRoomCount", size == 0 ? 7 : 24);
                for (int seed = 0; seed < 50; seed++)
                {
                    var layout = Generate(seed);
                    var measured = MeasureGroundRoutes(layout);
                    areas[size] += measured.Area;
                    routes[size] += measured.Route;
                }
                areas[size] /= 50; routes[size] /= 50;
            }
            TestContext.WriteLine("seeds=0..49; standing-clear connected ground area m2: small=" + areas[0] + ", large=" + areas[1] +
                "; longest shortest swept-grid walk m: small=" + routes[0] + ", large=" + routes[1]);
            Assert.That(areas[1], Is.GreaterThan(areas[0] * 2));
            Assert.That(routes[1], Is.GreaterThan(routes[0] * 1.5));
        }

        [Test]
        public void SingleRoomPocketsSupportMaximumCandidateBudgetAndNotchesRejectSpawns()
        {
            Set("_gapProbability", 1f); Set("_pocketProbability", 1f); Set("_pocketRoomCount", 1);
            Set("_minimumCandidatesPerRoom", 5); Set("_maximumCandidatesPerRoom", 5);
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", 0f); Set("_threeCellWeight", 1f); Set("_lShapeWeight", 1f);
            for (int seed = 0; seed < 50; seed++)
            {
                var layout = Generate(seed);
                Assert.That(layout.PocketAnchors.Count, Is.EqualTo(5));
                foreach (var anchor in layout.PocketAnchors)
                    Assert.That(ProceduralSpawnUtility.Validate(layout, anchor.Position, _config.DoorWidth, 1, out _), Is.False);
                foreach (var module in layout.Modules.Where(Bent))
                {
                    var room = layout.Graph.Rooms[module.RoomId - 1];
                    for (int x = module.Cells.Min(c => c.x); x <= module.Cells.Max(c => c.x); x++)
                    for (int y = module.Cells.Min(c => c.y); y <= module.Cells.Max(c => c.y); y++)
                    {
                        if (module.Cells.Contains(new Vector2Int(x, y))) continue;
                        var notch = new Vector3(_config.Origin.x + x * _config.RoomSize, 0.1f, _config.Origin.y + y * _config.RoomSize);
                        Assert.That(room.Bounds.Contains(notch), Is.True);
                        Assert.That(ProceduralFootprintUtility.At(layout, notch).Id, Is.Zero);
                        Assert.That(ProceduralSpawnUtility.Validate(layout, notch, _config.DoorWidth, 1, out _), Is.False);
                    }
                }
            }
        }

        [TestCase("_oneCellWeight", -1f)] [TestCase("_twoCellWeight", float.NaN)]
        [TestCase("_threeCellWeight", float.PositiveInfinity)] [TestCase("_lShapeWeight", 2f)]
        [TestCase("_gapProbability", -1f)] [TestCase("_pocketProbability", float.NaN)]
        public void InvalidFootprintAndGapSettingsAreRejected(string field, float value)
        { Set(field, value); Assert.Throws<ArgumentException>(() => Generate(7)); }

        [TestCase(false)] [TestCase(true)]
        public void PublishedCellsPreserveFootprintsPocketsCeilingsAndRoomIdentity(bool castle)
        {
            Set("_castleModules", castle); Set("_oneCellWeight", 0f); Set("_twoCellWeight", 0f);
            Set("_threeCellWeight", 1f); Set("_lShapeWeight", 1f); Set("_storeyProbability", 1f);
            Set("_gapProbability", 1f); Set("_pocketProbability", 1f); Set("_origin", new Vector2(31f, -17f));
            bool high = false, ordinaryOptional = false;
            for (int seed = 0; seed < 12; seed++)
            {
                var layout = Generate(seed);
                foreach (var module in layout.Modules)
                {
                    var room = layout.Graph.Rooms.Single(r => r.Id == module.RoomId);
                    float height = !castle ? _config.RoomHeight :
                        module.Kind == ProceduralModuleKind.BrokenCloister || module.Kind == ProceduralModuleKind.BrokenGallery ?
                        _config.HighCeilingHeight : _config.CastleHeight;
                    high |= castle && height == _config.HighCeilingHeight && module.Cells.Count == 3;
                    var expected = module.Cells.Select(c => new Bounds(new Vector3(_config.Origin.x + c.x * _config.RoomSize,
                        height * .5f, _config.Origin.y + c.y * _config.RoomSize), new Vector3(_config.RoomSize, height, _config.RoomSize))).ToArray();
                    Assert.That(room.Cells, Is.EqualTo(expected));
                    Assert.That(room.Pocket, Is.EqualTo(module.PocketId != 0));
                    if (module.Cells.Count == 1) Assert.That(room.Cells.Single(), Is.EqualTo(room.Bounds));
                    var sample = layout.PresentationRooms.Single(r => r.RoomId == room.Id);
                    Assert.That(sample.Cells, Is.EqualTo(room.Cells));
                    if (room.Pocket) Assert.That(sample.OptionalRoom, Is.True);
                    ordinaryOptional |= sample.OptionalRoom && !room.Pocket;
                    foreach (var volume in ProceduralFootprintUtility.Volumes(layout, room))
                        Assert.That(volume.Pocket, Is.EqualTo(room.Pocket));
                    if (Bent(module))
                    {
                        var notch = new Vector3(room.Bounds.min.x + _config.RoomSize * .5f, 0f,
                            room.Bounds.min.z + _config.RoomSize * .5f);
                        foreach (int x in new[] { 0, 1 })
                        foreach (int z in new[] { 0, 1 })
                        {
                            var point = notch + new Vector3(x * _config.RoomSize, 0f, z * _config.RoomSize);
                            Assert.That(room.ContainsXZ(point), Is.EqualTo(expected.Any(c => c.Contains(point))));
                        }
                    }
                }
                foreach (var storey in layout.Storeys)
                {
                    var room = layout.Graph.Rooms.Single(r => r.Id == storey.RoomId);
                    Assert.That(room.Cells.Count, Is.EqualTo(layout.Modules.Single(m => m.RoomId == storey.RoomId).Cells.Count));
                    Assert.That(room.ContainsXZ(storey.Origin + Vector3.up * storey.Height), Is.True);
                    Assert.That(layout.Graph.Rooms.Any(r => r.Id == storey.UpperRegionId), Is.False);
                }
                if (castle) Assert.That(layout.Storeys, Is.Not.Empty);
            }
            if (castle) { Assert.That(high, Is.True); Assert.That(ordinaryOptional, Is.True); }
        }

        private (float Area, float Route) MeasureGroundRoutes(ProceduralLayout layout)
        {
            var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
            var obstacles = blocks.Where(b => b.HasCollision).Select(WorldBounds)
                .Where(b => b.max.y > 0.01f && b.min.y < 1.81f).ToArray();
            var free = new HashSet<Vector2Int>();
            Vector3 Point(Vector2Int c) => new Vector3(c.x + 0.5f, 0.91f, c.y + 0.5f);
            foreach (var cell in layout.Modules.Where(m => m.PocketId == 0).SelectMany(m => m.Cells))
            for (int x = 0; x < 12; x++)
            for (int z = 0; z < 12; z++)
            {
                var c = new Vector2Int(cell.x * 12 + x - 6, cell.y * 12 + z - 6);
                var standing = new Bounds(Point(c), new Vector3(0.6f, 1.8f, 0.6f));
                if (!obstacles.Any(b => b.Intersects(standing))) free.Add(c);
            }
            var origin = free.OrderBy(c => (Point(c) - layout.PlayerSpawnPosition).sqrMagnitude).First();
            var distances = new Dictionary<Vector2Int, int> { [origin] = 0 };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var step in new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                {
                    var next = current + step;
                    if (!free.Contains(next) || distances.ContainsKey(next)) continue;
                    var swept = new Bounds((Point(current) + Point(next)) * 0.5f,
                        new Vector3(0.6f + Math.Abs(step.x), 1.8f, 0.6f + Math.Abs(step.y)));
                    if (obstacles.Any(b => b.Intersects(swept))) continue;
                    distances.Add(next, distances[current] + 1); queue.Enqueue(next);
                }
            }
            foreach (var module in layout.Modules.Where(m => m.PocketId == 0))
                Assert.That(distances.Keys.Any(c => ProceduralFootprintUtility.At(layout, Point(c)).Id == module.RoomId), Is.True);
            return (distances.Count, distances.Values.Max());
        }
        private static bool Contains(ProceduralBlock block, Vector3 point)
            => new Bounds(Vector3.zero, block.Size).Contains(Quaternion.Inverse(block.Rotation) * (point - block.Center));
        private static Bounds WorldBounds(ProceduralBlock block)
        {
            var bounds = new Bounds(block.Center, Vector3.zero);
            for (int i = 0; i < 8; i++) bounds.Encapsulate(block.Center + block.Rotation * new Vector3(
                (i & 1) == 0 ? -block.Size.x : block.Size.x, (i & 2) == 0 ? -block.Size.y : block.Size.y,
                (i & 4) == 0 ? -block.Size.z : block.Size.z) * 0.5f);
            return bounds;
        }
    }
}
