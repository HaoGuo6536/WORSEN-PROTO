// ============================================================================
// ProceduralSpawnUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the first-contact validator with generated layouts and deliberately
//   visible positions. Pure checks cannot be rescued by scene obstacles or physics.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Reject visibility, insufficient path length and exit-room placements.
//   - Sample deterministic spawns and retain the distance-relaxation trail.
//   - Reject sightlines crossing a multi-cell room's unwalled interior seams.
// DEPENDENCIES:
//   - Core, Domain.Procedural, NUnit and temporary Unity config serialization.
// USAGE NOTES:
//   No scene or navigation bake; coordinator runs these EditMode tests.
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
    public sealed class ProceduralSpawnUtilityTests
    {
        private ProceduralConfig _config;
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<ProceduralConfig>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        [Test]
        public void VisibleNeighborTooCloseAndExitAreRejected()
        {
            var layout = Generate(7, 1);
            var door = layout.Doors.First(d => d.FromRoomId == layout.Graph.ExitRoomId);
            var neighbor = layout.Graph.Rooms.Single(r => r.Id == door.ToRoomId);
            var normal = door.AlongX ? Vector3.forward * Math.Sign(neighbor.Center.z - door.Center.z) :
                Vector3.right * Math.Sign(neighbor.Center.x - door.Center.x);
            Vector3 visible = door.Center + normal * 0.01f + Vector3.up * _config.SpawnHeight;
            Assert.That(ProceduralSpawnUtility.Validate(layout, visible, _config.DoorWidth, 1, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("visible"));
            Assert.That(ProceduralSpawnUtility.Validate(layout, visible, _config.DoorWidth, 2, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("path-too-short-or-unreachable"));
            Assert.That(ProceduralSpawnUtility.Validate(layout, layout.PlayerSpawnPosition, _config.DoorWidth, 1, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("exit-or-player-room"));
            Assert.That(ProceduralSpawnUtility.Validate(layout, new Vector3(float.NaN, 0f, 0f), _config.DoorWidth, 1, out _), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void SeedSampleSelectsOnlyValidDeterministicPositions(bool castle)
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_castleModules").boolValue = castle;
            settings.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 0; seed < 128; seed++)
            {
                var layout = Generate(seed, 1 + seed % 8);
                Assert.That(layout.HunterSpawnPositions, Is.Not.Empty, "seed " + seed);
                Assert.That(layout.Manifest, Is.EqualTo(Generate(seed, 1 + seed % 8).Manifest));
                foreach (var position in layout.HunterSpawnPositions)
                {
                    Assert.That(ProceduralSpawnUtility.Validate(layout, position, _config.DoorWidth,
                        layout.MinimumHunterSpawnRooms, out _), Is.True, "seed " + seed);
                    int room = layout.Graph.Rooms.Single(r => r.Bounds.Contains(position)).Id;
                    Assert.That(LevelGraphUtility.TopologicalDistancesFrom(layout.Graph,
                        layout.Graph.ExitRoomId, TraversalAccess.Hunter)[room], Is.GreaterThanOrEqualTo(layout.MinimumHunterSpawnRooms));
                }
            }
        }

        [Test]
        public void RelaxationIsRecordedAndNeverAllowsVisibleOrExitCandidates()
        {
            var settings = new SerializedObject(_config);
            settings.FindProperty("_minimumHunterSpawnRooms").intValue = 256;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var layout = Generate(9, 1);
            Assert.That(layout.MinimumHunterSpawnRooms, Is.LessThan(256));
            Assert.That(layout.Manifest, Does.Contain("rooms=256:none;rooms=255:none;"));
            Assert.That(layout.Manifest, Does.Contain("rooms=" + layout.MinimumHunterSpawnRooms + ":accepted"));
            var exception = Assert.Throws<InvalidOperationException>(() => ProceduralSpawnUtility.Select(layout,
                _config, new[] { layout.PlayerSpawnPosition }, out _, out _));
            Assert.That(exception.Message, Does.Contain("rooms=1:none"));
        }

        [Test]
        public void TwoHopStraightSightlineIsVisibleButOffsetPortalOccludes()
        {
            var rooms = Enumerable.Range(0, 3).Select(i => new LevelRoom(i + 1,
                new Vector3(i * 12f, 2f, 0f), new Vector3(12f, 4f, 12f))).ToArray();
            var graph = LevelGraphUtility.Build(rooms, new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All),
                new LevelEdge(12, 2, 3, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            var layout = new ProceduralLayout();
            // Test-only synthetic floor: public layout setters are intentionally internal.
            typeof(ProceduralLayout).GetProperty(nameof(ProceduralLayout.Graph)).SetValue(layout, graph);
            typeof(ProceduralLayout).GetProperty(nameof(ProceduralLayout.PlayerSpawnPosition)).SetValue(layout, Vector3.up * 0.1f);
            var portals = new[] { new ProceduralDoorPlan(1, 2, new Vector3(6f, 0f, 0f), false),
                new ProceduralDoorPlan(2, 3, new Vector3(18f, 0f, 0f), false) };
            typeof(ProceduralLayout).GetProperty(nameof(ProceduralLayout.Doors)).SetValue(layout, portals);
            var candidate = new Vector3(24f, 0.1f, 0f);
            Assert.That(ProceduralSpawnUtility.Validate(layout, candidate, 3.2f, 2, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("visible"));
            portals[1] = new ProceduralDoorPlan(2, 3, new Vector3(18f, 0f, 4f), false);
            Assert.That(ProceduralSpawnUtility.Validate(layout, candidate, 3.2f, 2, out _), Is.True);
        }

        private ProceduralLayout Generate(int seed, int round) => new ProceduralController(new ProceduralBehaviorState(),
            _config, new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);

        [Test]
        public void OpenInteriorSeamDoesNotProvideFalseSpawnCover()
        {
            var rooms = new[] {
                new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f)),
                new LevelRoom(2, new Vector3(18f, 2f, 0f), new Vector3(24f, 4f, 12f)),
                new LevelRoom(3, new Vector3(36f, 2f, 0f), new Vector3(12f, 4f, 12f)) };
            var layout = new ProceduralLayout();
            void Set(string property, object value) => typeof(ProceduralLayout).GetProperty(property).SetValue(layout, value);
            Set(nameof(ProceduralLayout.Graph), LevelGraphUtility.Build(rooms,
                new[] { new LevelEdge(1, 1, 2, true), new LevelEdge(2, 2, 3, true) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero));
            Set(nameof(ProceduralLayout.PlayerSpawnPosition), Vector3.up * 0.1f);
            Set(nameof(ProceduralLayout.CellSize), 12f);
            Set(nameof(ProceduralLayout.Cells), Enumerable.Range(0, 4).Select(i => new Vector2Int(i, 0)).ToArray());
            Set(nameof(ProceduralLayout.Modules), new[] {
                new ProceduralRoomModule(1, ProceduralModuleKind.ExitHub, true, new[] { Vector2Int.zero }),
                new ProceduralRoomModule(2, ProceduralModuleKind.TorchGallery, true, new[] { Vector2Int.right, new Vector2Int(2, 0) }),
                new ProceduralRoomModule(3, ProceduralModuleKind.TorchGallery, true, new[] { new Vector2Int(3, 0) }) });
            Set(nameof(ProceduralLayout.Doors), new[] { new ProceduralDoorPlan(1, 2, new Vector3(6f, 0f, 0f), false),
                new ProceduralDoorPlan(2, 3, new Vector3(30f, 0f, 0f), false) });
            Assert.That(ProceduralSpawnUtility.Validate(layout, new Vector3(36f, 0.1f, 0f), 3.2f, 2, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("visible"));
        }
    }
}
