// ============================================================================
// ProceduralCastlePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Exercises castle geometry, topology and raised objectives across fixed seeds.
//   Tests reject sealed portals, unreachable room graphs and unsafe cake clearance
//   separately from the native navigation admission performed by the Driver.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Cover early/mid/capped layouts, central exit hubs and both optional link types.
//   - Directly verify castle-module output reserves entrances and supports ordinary ascent.
//   - Require roof coverage over every floor and sealed walls between unequal heights.
//   - Reject floating wall components and keep hub spawns clear of the central door.
// DEPENDENCIES:
//   - Domain.Procedural, Core values, NUnit and temporary Unity config instances.
// USAGE NOTES:
//   Pure geometry checks do not claim native Player or Hunter walking success.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using Worsen.Domain.Player;
namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralCastlePresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driver;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(_config); Object.DestroyImmediate(_driver); }

        [TestCase(1)] [TestCase(3)] [TestCase(20)]
        public void TwentySeedsKeepExitHubFourDoorsEveryCakeClearAndElevatedRoutes(int round)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = Generate(seed, round);
                Assert.That(layout.Manifest, Is.EqualTo(Generate(seed, round).Manifest));
                var hubDoors = layout.Doors.Where(d => d.FromRoomId == layout.Graph.ExitRoomId || d.ToRoomId == layout.Graph.ExitRoomId).ToArray();
                Assert.That(hubDoors.Length, Is.EqualTo(4), "Exit hub must connect all four cardinal directions.");
                Assert.That(hubDoors.All(d => !d.IsOptional), Is.True);
                Assert.That(layout.Graph.ExitPosition.x, Is.EqualTo(layout.Graph.Rooms[0].Center.x));
                Assert.That(layout.Graph.ExitPosition.z, Is.EqualTo(layout.Graph.Rooms[0].Center.z));
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                Assert.That(blocks.Count, Is.LessThan(layout.Graph.Rooms.Count * 65), "Geometry budget must remain bounded.");
                foreach (var anchor in layout.Graph.Anchors)
                {
                    var standing = new Bounds(anchor.Position + Vector3.up * 0.91f, new Vector3(0.6f, 1.8f, 0.6f));
                    Assert.That(blocks.Any(b => new Bounds(b.Center, b.Size).Intersects(standing)), Is.False,
                        "Blocked cake on seed " + seed + " at " + anchor.Position);
                    Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Floor &&
                        new Bounds(b.Center, b.Size).Contains(anchor.Position - Vector3.up * 0.06f)), Is.True);
                }
                foreach (var door in layout.Doors.Where(d => !d.IsOptional))
                {
                    var standing = new Bounds(door.Center + Vector3.up * 1.1f, new Vector3(0.6f, 2f, 0.6f));
                    Assert.That(blocks.Any(b => new Bounds(b.Center, b.Size).Intersects(standing)), Is.False);
                }
                foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
                    Assert.That(LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, actor).Values.All(d => d >= 0), Is.True);
                Assert.That(layout.Graph.Anchors.Any(a => a.Position.y > 2f), Is.True);
                Assert.That(layout.Modules.Select(m => m.Kind).Distinct().Count(), Is.GreaterThanOrEqualTo(6));
            }
        }
        [Test]
        public void OptionalWindowCurseDoesNotAlterRoomsRequiredDoorsOrSlideLinks()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var ordinary = Generate(seed, 3);
                var cursed = Generate(seed, 3, false, 0f);
                Assert.That(cursed.Cells, Is.EqualTo(ordinary.Cells));
                Assert.That(cursed.Doors.Count(d => !d.IsOptional), Is.EqualTo(ordinary.Doors.Count(d => !d.IsOptional)));
                Assert.That(cursed.Doors.Count(d => d.TraversalKind == TraversalSurfaceKind.SlideGate),
                    Is.EqualTo(ordinary.Doors.Count(d => d.TraversalKind == TraversalSurfaceKind.SlideGate)));
                Assert.That(cursed.Doors.Any(d => d.TraversalKind == TraversalSurfaceKind.Vault), Is.False);
                Assert.That(ordinary.Graph.Edges.Where(e => e.Access == TraversalAccess.Player).Count(),
                    Is.EqualTo(ordinary.Doors.Count(d => d.IsOptional)));
            }
        }
        [Test]
        public void RefugeAndCloisterHaveDifferentCeilingEnclosures()
        {
            var refuge = Generate(8, 1, true);
            Assert.That(refuge.Modules.All(m => m.Kind == ProceduralModuleKind.MerchantRefuge), Is.True);
            var normal = Generate(8, 1);
            var blocks = new ProceduralGeometryPresenter().Build(normal, _config, _driver);
            var cloister = normal.Modules.First(m => m.Kind == ProceduralModuleKind.BrokenCloister);
            var room = normal.Graph.Rooms[cloister.RoomId - 1];
            Assert.That(room.Size.y, Is.GreaterThan(refuge.Graph.Rooms.Max(r => r.Size.y)),
                "The broad cloister is distinguished by more enclosed headroom, never exposed sky.");
            Assert.That(blocks.Where(b => b.RoomId == room.Id && b.Kind == ProceduralSurfaceKind.Ceiling)
                .Any(b => new Bounds(b.Center, b.Size).Contains(new Vector3(room.Center.x, room.Size.y + _driver.CeilingThickness * 0.5f, room.Center.z))), Is.True);
            Assert.That(normal.PresentationRooms.All(r => !r.OpenSky), Is.True);
        }

        [TestCase(7f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void CastleRejectsInvalidBroadRoomCeilingHeights(float highCeiling)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_highCeilingHeight").floatValue = highCeiling;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<System.ArgumentException>(() => Generate(8, 1));
        }

        [TestCase(1, 10f)]
        [TestCase(3, 12f)]
        [TestCase(99, 9f)]
        public void AllPlayableFloorsHaveCompleteRoofsAndTallRoomsHaveSealedUpperWalls(int round, float highCeiling)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_highCeilingHeight").floatValue = highCeiling;
            settings.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = Generate(seed, round);
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                var walls = blocks.Where(b => b.Kind == ProceduralSurfaceKind.Wall).Select(b => new Bounds(b.Center, b.Size)).ToArray();
                foreach (var room in layout.Graph.Rooms)
                {
                    var family = layout.Modules[room.Id - 1].Kind;
                    bool tall = family == ProceduralModuleKind.BrokenCloister || family == ProceduralModuleKind.BrokenGallery;
                    Assert.That(room.Bounds.min.y, Is.EqualTo(0f).Within(0.001f));
                    Assert.That(room.Bounds.max.y, Is.EqualTo(tall ? highCeiling : _config.CastleHeight).Within(0.001f));
                    var sample = layout.PresentationRooms.Single(r => r.RoomId == room.Id);
                    Assert.That(sample.Bounds, Is.EqualTo(room.Bounds), "Collapse and decoration must receive the complete enclosed volume.");
                    Assert.That(sample.OpenSky, Is.False);
                    var roofs = blocks.Where(b => b.RoomId == room.Id && b.Kind == ProceduralSurfaceKind.Ceiling)
                        .Select(b => new Bounds(b.Center, b.Size)).ToArray();
                    float roofY = room.Bounds.max.y + _driver.CeilingThickness * 0.5f;
                    // Cover the entire footprint, including room center and all former canopy gaps.
                    for (int x = -4; x <= 4; x++)
                    for (int z = -4; z <= 4; z++)
                    {
                        var overhead = new Vector3(room.Center.x + x * room.Size.x / 8f, roofY, room.Center.z + z * room.Size.z / 8f);
                        Assert.That(roofs.Any(b => b.Contains(overhead)), Is.True, "Uncovered roof in seed " + seed + " room " + room.Id);
                    }
                    // Every actual stair tread/gallery/floor support has a roof with standing headroom.
                    foreach (var floor in blocks.Where(b => b.RoomId == room.Id && b.Kind == ProceduralSurfaceKind.Floor))
                    {
                        float standingHead = floor.Center.y + floor.Size.y * 0.5f + 1.8f;
                        Assert.That(room.Bounds.max.y, Is.GreaterThan(standingHead));
                        Assert.That(roofs.Any(b => b.Contains(new Vector3(floor.Center.x, roofY, floor.Center.z))), Is.True);
                    }
                    // Probe above every doorway and immediately below the roof on all four walls.
                    // This catches the gap where a taller room shares a wall owned by a shorter room.
                    foreach (float y in new[] { (_config.DoorHeight + room.Size.y) * 0.5f, room.Size.y - 0.05f })
                    for (int side = 0; side < 4; side++)
                    for (int offset = -3; offset <= 3; offset++)
                    {
                        float sign = side < 2 ? 1f : -1f;
                        bool alongX = side == 0 || side == 2;
                        var point = new Vector3(room.Center.x, y, room.Center.z) +
                            (alongX ? Vector3.forward : Vector3.right) * (sign * _config.RoomSize * 0.5f) +
                            (alongX ? Vector3.right : Vector3.forward) * (offset * _config.RoomSize / 8f);
                        Assert.That(walls.Any(b => b.Contains(point)), Is.True, "Unsealed upper wall in seed " + seed + " room " + room.Id);
                    }
                }
            }
        }
        [Test]
        public void BothDirectionsOfEveryWindowAndSlideReserveTheEntireCapsuleArc()
        {
            var mover = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            try
            {
                var movement = new PlayerMoverPresenter();
                for (int seed = 0; seed < 20; seed++)
                {
                    var layout = Generate(seed, 3);
                    var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                    Assert.That(layout.Doors.Any(d => d.TraversalKind == TraversalSurfaceKind.Vault), Is.True);
                    Assert.That(layout.Doors.Any(d => d.TraversalKind == TraversalSurfaceKind.SlideGate), Is.True);
                    foreach (var surface in blocks.Where(b => b.SurfaceId >= 80000 &&
                        (b.TraversalKind == TraversalSurfaceKind.Vault || b.TraversalKind == TraversalSurfaceKind.SlideGate)))
                    foreach (bool reverse in new[] { false, true })
                    {
                        var across = (surface.EndpointB - surface.EndpointA).normalized * (reverse ? -1f : 1f);
                        var start = (reverse ? surface.EndpointB : surface.EndpointA) - across * 0.35f;
                        var end = reverse ? surface.EndpointA : surface.EndpointB;
                        float height = surface.TraversalKind == TraversalSurfaceKind.Vault ? mover.Height : mover.Height * mover.SlideHeightRatio;
                        for (int step = 0; step <= 100; step++)
                        {
                            float progress = step / 100f;
                            var feet = surface.TraversalKind == TraversalSurfaceKind.Vault ?
                                movement.TraversalPosition(start, end, progress, surface.Center.y + surface.Size.y * 0.5f,
                                    mover.TraversalLift, mover.TraversalRisePortion, mover.TraversalTraverseEnd) : Vector3.Lerp(start, end, progress);
                            // A full-height swept box conservatively encloses the capsule; ignore only base floor contact.
                            var clearance = new Bounds(feet + Vector3.up * (height * 0.5f), new Vector3(mover.Radius * 2f, height - 0.02f, mover.Radius * 2f));
                            Assert.That(blocks.Where(b => !(b.Kind == ProceduralSurfaceKind.Floor && b.Center.y + b.Size.y * 0.5f <= 0.001f))
                                .Any(b => new Bounds(b.Center, b.Size).Intersects(clearance)), Is.False,
                                "Seed " + seed + " surface " + surface.SurfaceId + " reverse " + reverse + " progress " + progress);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(mover); }
        }

        [Test]
        public void CastleModuleBuildKeepsEntrancesClearAndSupportsOrdinaryAscentToObjectives()
        {
            var layout = Generate(8, 3);
            var blocks = new ProceduralCastlePresenter().Build(layout, _config, _driver);
            var mover = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            try
            {
                var exitClearance = new Bounds(layout.Graph.ExitPosition + Vector3.up * 1.5f, new Vector3(3f, 3f, 3f));
                Assert.That(blocks.Any(b => new Bounds(b.Center, b.Size).Intersects(exitClearance)), Is.False,
                    "Castle furniture and structure must leave room for the central exit door and its approach.");
                foreach (var door in layout.Doors)
                foreach (float sign in new[] { -1f, 1f })
                {
                    var normal = door.AlongX ? Vector3.forward : Vector3.right;
                    var feet = door.Center + normal * (_driver.LandingOffset * sign);
                    var standing = new Bounds(feet + Vector3.up * (mover.Height * 0.5f + 0.01f),
                        new Vector3(mover.Radius * 2f, mover.Height, mover.Radius * 2f));
                    Assert.That(blocks.Any(b => new Bounds(b.Center, b.Size).Intersects(standing)), Is.False,
                        "A module must not obstruct either side of an ordinary or optional entrance.");
                }

                var stairRoom = layout.Modules.First(m => m.Kind == ProceduralModuleKind.OpenStairHall).RoomId;
                var floors = blocks.Where(b => b.RoomId == stairRoom && b.Kind == ProceduralSurfaceKind.Floor).ToArray();
                // Identify the physical staircase from its ground-supported blocks, not block order or count.
                var steps = floors.Where(b => Mathf.Abs(b.Center.y - b.Size.y * 0.5f) < 0.001f)
                    .OrderBy(b => b.Center.y + b.Size.y * 0.5f).ToArray();
                Assert.That(steps.Length, Is.GreaterThan(1), "Raised objectives require an actual ordinary ascent.");
                float previousTop = 0f;
                for (int index = 0; index < steps.Length; index++)
                {
                    var step = steps[index];
                    float top = step.Center.y + step.Size.y * 0.5f;
                    Assert.That(top - previousTop, Is.InRange(0.001f, mover.StepHeight + 0.001f),
                        "Every riser must fit the base movement kit without a jump or purchase.");
                    Assert.That(Mathf.Max(step.Size.x, step.Size.z), Is.GreaterThan(mover.Radius * 2f + mover.SkinWidth * 2f));
                    if (index > 0)
                    {
                        var previous = new Bounds(steps[index - 1].Center, steps[index - 1].Size);
                        previous.Expand(0.002f);
                        Assert.That(previous.Intersects(new Bounds(step.Center, step.Size)), Is.True,
                            "Consecutive stair supports must meet instead of leaving a gap.");
                    }
                    var headroom = new Bounds(new Vector3(step.Center.x, top + (mover.Height + mover.StepHeight) * 0.5f, step.Center.z),
                        new Vector3(mover.Radius * 2f, mover.Height - mover.StepHeight, mover.Radius * 2f));
                    Assert.That(blocks.Any(b => new Bounds(b.Center, b.Size).Intersects(headroom)), Is.False,
                        "The stair ascent needs standing headroom above its step allowance.");
                    previousTop = top;
                }
                Assert.That(previousTop, Is.EqualTo(_config.UpperDeckHeight).Within(0.001f));
                var raisedObjectives = layout.Graph.Anchors.Where(a => a.RoomId == stairRoom && a.Position.y > 1f).ToArray();
                Assert.That(raisedObjectives.Length, Is.GreaterThan(0));
                foreach (var anchor in raisedObjectives)
                    Assert.That(floors.Any(b => new Bounds(b.Center, b.Size).Contains(anchor.Position - Vector3.up * (_config.AnchorHeight + 0.01f))), Is.True,
                        "The module must supply physical support beneath its raised objectives.");
                var finalStep = new Bounds(steps[steps.Length - 1].Center, steps[steps.Length - 1].Size);
                finalStep.Expand(0.002f);
                Assert.That(floors.Any(b => b.Center.y - b.Size.y * 0.5f > 0.1f && finalStep.Intersects(new Bounds(b.Center, b.Size))), Is.True,
                    "The ordinary ascent must meet the gallery supporting the objectives.");
            }
            finally { Object.DestroyImmediate(mover); }
        }

        [TestCase(1)] [TestCase(3)] [TestCase(99)]
        public void EveryWallConnectsToGroundWithoutRelyingOnAHiddenCeiling(int round)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = Generate(seed, round);
                var solids = new ProceduralGeometryPresenter().Build(layout, _config, _driver)
                    .Where(b => b.Kind != ProceduralSurfaceKind.Ceiling).ToArray();
                var bounds = solids.Select(b => new Bounds(b.Center, b.Size)).ToArray();
                var supported = new bool[solids.Length];
                var frontier = new System.Collections.Generic.Queue<int>();
                for (int index = 0; index < solids.Length; index++)
                    if (bounds[index].min.y <= 0.001f)
                    { supported[index] = true; frontier.Enqueue(index); }
                // A lintel can transfer its weight sideways into grounded piers or walls.
                // Only physical contact counts; roof blocks cannot disguise a floating panel.
                while (frontier.Count > 0)
                {
                    var contact = bounds[frontier.Dequeue()];
                    contact.Expand(0.002f);
                    for (int index = 0; index < solids.Length; index++)
                        if (!supported[index] && contact.Intersects(bounds[index]))
                        { supported[index] = true; frontier.Enqueue(index); }
                }
                for (int index = 0; index < solids.Length; index++)
                    if (solids[index].Kind == ProceduralSurfaceKind.Wall)
                        Assert.That(supported[index], Is.True, "Floating wall in seed " + seed + " room " + solids[index].RoomId + " at " + solids[index].Center);
            }
        }

        [TestCase(1, false)] [TestCase(3, false)] [TestCase(99, false)] [TestCase(1, true)]
        public void CastleStartsInsideExitHubOnClearGroundFacingTheFreestandingDoor(int round, bool refuge)
        {
            var mover = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            try
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    var layout = Generate(seed, round, refuge);
                    var hub = layout.Graph.Rooms.Single(r => r.Id == layout.Graph.ExitRoomId);
                    Assert.That(hub.Bounds.Contains(layout.PlayerSpawnPosition), Is.True);
                    Assert.That(layout.Graph.ExitPosition.x, Is.EqualTo(hub.Center.x));
                    Assert.That(layout.Graph.ExitPosition.z, Is.EqualTo(hub.Center.z));
                    Assert.That(layout.HunterSpawnPositions.Any(hub.Bounds.Contains), Is.False, "Hunters must not start in the player's hub.");
                    Assert.That(layout.PresentationRooms.Single(r => r.RoomId == hub.Id).OptionalRoom, Is.False);
                    Assert.That(Vector3.Dot(layout.PlayerSpawnRotation * Vector3.forward,
                        (layout.Graph.ExitPosition - layout.PlayerSpawnPosition).normalized), Is.GreaterThan(0.999f));
                    var standing = new Bounds(layout.PlayerSpawnPosition + Vector3.up * (mover.Height * 0.5f),
                        new Vector3(mover.Radius * 2f, mover.Height, mover.Radius * 2f));
                    // Includes both wooden supports and the entire opening sweep of the central door.
                    var doorEnvelope = new Bounds(layout.Graph.ExitPosition + Vector3.up * 1.5f, new Vector3(2.6f, 3f, 2.1f));
                    Assert.That(doorEnvelope.Intersects(standing), Is.False, "Spawn must clear the freestanding door and its moving leaves.");
                    var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                    Assert.That(blocks.Any(b => new Bounds(b.Center, b.Size).Intersects(standing)), Is.False);
                    var ground = new Vector3(layout.PlayerSpawnPosition.x, -0.01f, layout.PlayerSpawnPosition.z);
                    Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Floor && new Bounds(b.Center, b.Size).Contains(ground)), Is.True);
                    foreach (var cake in layout.Graph.Anchors)
                    {
                        var separation = cake.Position - layout.PlayerSpawnPosition;
                        Assert.That(new Vector2(separation.x, separation.z).magnitude, Is.GreaterThanOrEqualTo(1f), "Starting at the hub must not collect cake automatically.");
                    }
                }
            }
            finally { Object.DestroyImmediate(mover); }
        }

        private ProceduralLayout Generate(int seed, int round, bool refuge = false, float windows = 1f)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round, refuge, windows);
    }
}
