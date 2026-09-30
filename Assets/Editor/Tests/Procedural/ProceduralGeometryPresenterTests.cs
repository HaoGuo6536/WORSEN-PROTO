// ============================================================================
// ProceduralGeometryPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks generated enclosure geometry against the graph's door connections.
//   Explicit spatial assertions detect sealed doorways, missing ceilings and
//   navigation bounds that omit geometry without depending on a running scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify complete tiled floors/ceilings and clear shared door apertures.
//   - Require navigation bounds to contain all eight corners of rotated collision boxes.
//   - Keep one-floor-per-room assertions scoped to the single-cell regression config.
// DEPENDENCIES:
//   - Domain.Procedural, NUnit and UnityEngine value types.
// USAGE NOTES:
//   Pure calculations; temporary configuration assets are destroyed after tests.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralGeometryPresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driverConfig;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driverConfig = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = false;
            settings.FindProperty("_initialRoomCount").intValue = 5;
            settings.FindProperty("_twoCellWeight").floatValue = 0f;
            settings.FindProperty("_threeCellWeight").floatValue = 0f;
            settings.FindProperty("_gapProbability").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(_config); Object.DestroyImmediate(_driverConfig); }

        [Test]
        public void EveryRoomHasOneSealedFloorAndCeilingAndEverySharedDoorRemainsOpen()
        {
            for (int seed = 0; seed < 16; seed++)
            {
                var layout = Generate(seed);
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driverConfig);
                Assert.That(blocks.Count(block => block.Kind == ProceduralSurfaceKind.Floor), Is.EqualTo(layout.Graph.Rooms.Count));
                Assert.That(blocks.Count(block => block.Kind == ProceduralSurfaceKind.Ceiling), Is.EqualTo(layout.Graph.Rooms.Count));
                foreach (var room in layout.Graph.Rooms)
                {
                    var floor = blocks.Single(block => block.RoomId == room.Id && block.Kind == ProceduralSurfaceKind.Floor);
                    var ceiling = blocks.Single(block => block.RoomId == room.Id && block.Kind == ProceduralSurfaceKind.Ceiling);
                    Assert.That(floor.Size.x, Is.EqualTo(room.Size.x)); Assert.That(floor.Size.z, Is.EqualTo(room.Size.z));
                    Assert.That(floor.Center.y + floor.Size.y * 0.5f, Is.EqualTo(0f).Within(0.0001f));
                    Assert.That(ceiling.Center.y - ceiling.Size.y * 0.5f, Is.EqualTo(4f).Within(0.0001f));
                }
                foreach (var door in layout.Doors)
                {
                    var opening = new Bounds(door.Center + Vector3.up * 1.35f,
                        door.AlongX ? new Vector3(3f, 2.6f, 0.2f) : new Vector3(0.2f, 2.6f, 3f));
                    Assert.That(blocks.Any(block => new Bounds(block.Center, block.Size).Intersects(opening)), Is.False,
                        "Shared door " + door.FromRoomId + " -> " + door.ToRoomId + " was blocked.");
                }
            }
        }

        [Test]
        public void EveryCakeHasStandingClearanceAndBoundsIncludeAllPhysicalShells()
        {
            var layout = Generate(51);
            var presenter = new ProceduralGeometryPresenter();
            var blocks = presenter.Build(layout, _config, _driverConfig);
            var bounds = presenter.NavigationBounds(blocks, _driverConfig.NavBoundsPadding);
            foreach (var block in blocks)
            {
                Assert.That(bounds.Contains(block.Center - block.Size * 0.5f), Is.True);
                Assert.That(bounds.Contains(block.Center + block.Size * 0.5f), Is.True);
            }
            foreach (var anchor in layout.Graph.Anchors)
            {
                var standing = new Bounds(anchor.Position + Vector3.up * 0.91f, new Vector3(0.6f, 1.8f, 0.6f));
                Assert.That(blocks.Where(block => block.Kind != ProceduralSurfaceKind.Floor)
                    .Any(block => new Bounds(block.Center, block.Size).Intersects(standing)), Is.False);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void NavigationBoundsContainEveryRotatedRampCorner(bool alongX)
        {
            var rotation = Quaternion.LookRotation((alongX ? Vector3.forward : Vector3.right) * 4.8f + Vector3.up * 2.4f, Vector3.up);
            var block = new ProceduralBlock(1, ProceduralSurfaceKind.Floor, new Vector3(20f, 1f, -30f),
                new Vector3(2f, 0.3f, Mathf.Sqrt(4.8f * 4.8f + 2.4f * 2.4f)), role: ProceduralBlockRole.StairRamp, rotation: rotation);
            var bounds = new ProceduralGeometryPresenter().NavigationBounds(new[] { block }, 0.01f);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                Assert.That(bounds.Contains(block.Center + rotation * new Vector3(x * block.Size.x, y * block.Size.y, z * block.Size.z) * 0.5f), Is.True);
        }

        private ProceduralLayout Generate(int seed)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, 3))).Generate(seed, 3);
    }
}
