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
    public sealed class ProceduralGeometryPresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driverConfig;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driverConfig = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); var settings = new SerializedObject(_config); settings.FindProperty("_castleModules").boolValue = false; settings.FindProperty("_initialRoomCount").intValue = 5; settings.ApplyModifiedPropertiesWithoutUndo(); }
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
        private ProceduralLayout Generate(int seed)
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, 3))).Generate(seed, 3);
    }
}
