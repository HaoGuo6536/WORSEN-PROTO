// ============================================================================
// ProceduralNavFallbackSeedTests.cs
// ============================================================================
// PURPOSE:
//   Replays the batch-18 seed through production layout calculations. Separates
//   the reported hunter socket from template hubs and checks the generated shell
//   without claiming that managed checks can replace a native navigation bake.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Identify the reported position using the seeded coarse growth and spawn rules.
//   - Separately prove that safe tile sockets replace coarse boundary-wall spawns.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural and Core graph values.
// USAGE NOTES:
//   The partial replay uses only managed config fields and production private
//   methods; no Unity objects, imports, scenes or navigation data are created.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralNavFallbackSeedTests
    {
        [Test]
        public void Batch18PositionIsAnOrganicHunterSocketNotAnExitHubSocket()
        {
            const int seed = 133745427, round = 2;
            var config = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
            Set(config, "_initialRoomCount", 7); Set(config, "_roomsPerRound", 2); Set(config, "_maximumRoomCount", 15);
            Set(config, "_castleModules", true); Set(config, "_multiCellStartRound", 1);
            Set(config, "_oneCellWeight", .55f); Set(config, "_twoCellWeight", .3f); Set(config, "_threeCellWeight", .15f);
            Set(config, "_lShapeWeight", .4f); Set(config, "_gapStartRound", 2); Set(config, "_gapProbability", .35f);
            Set(config, "_maximumGapCells", 3); Set(config, "_pocketProbability", .5f); Set(config, "_pocketRoomCount", 2);
            Set(config, "_roomSize", 12f); Set(config, "_doorOffset", 2f);
            Set(config, "_spawnHeight", .1f); Set(config, "_spawnSideOffset", 4f);
            var random = new System.Random(ProceduralController.LayoutSeed(seed, round));
            var controller = new ProceduralController(new ProceduralBehaviorState(), config, random);
            int count = (int)Method("RoomCount").Invoke(controller, new object[] { round });
            var cells = (List<List<Vector2Int>>)Method("GrowCells").Invoke(controller, new object[] { count, round, null });
            Method("CreateDoors").Invoke(controller, new object[] { cells, count, 1f });
            int family = random.Next(5);
            var positions = new List<Vector3>();

            for (int index = 0; index < cells.Count; index++)
            {
                var footprint = cells[index];
                var room = new LevelRoom(index + 1,
                    new Vector3((footprint.Min(c => c.x) + footprint.Max(c => c.x)) * 6f, 1.6f,
                        (footprint.Min(c => c.y) + footprint.Max(c => c.y)) * 6f),
                    new Vector3((footprint.Max(c => c.x) - footprint.Min(c => c.x) + 1) * 12f, 3.2f,
                        (footprint.Max(c => c.y) - footprint.Min(c => c.y) + 1) * 12f));
                var module = new ProceduralRoomModule(room.Id, index == 0 ? ProceduralModuleKind.ExitHub :
                    (ProceduralModuleKind)((int)ProceduralModuleKind.TorchGallery + (index - 1 + family) % 5),
                    random.Next(2) == 0, cells[index], traversalObstacles: false);
                var position = (Vector3)Method("Approach").Invoke(controller, new object[] { room, module, 1f });
                positions.Add(position);
                TestContext.WriteLine("room=" + room.Id + ", cells=" + string.Join(";", cells[index]) + ", hunter=" + position);
            }
            var reported = new Vector3(4f, .1f, -24f);
            Assert.That(positions[6], Is.EqualTo(reported));
            Assert.That(positions[0], Is.Not.EqualTo(reported));
        }

        [Test]
        public void CarvedBoundaryReplacesLegacySocketWithClearSupportedTileCentres()
        {
            // Deliberately constructed boundary hazard, NOT a claim that batch 18
            // used this carving. Exact native failure needs the full gate descriptor.
            var layout = new ProceduralLayout();
            Property(layout, "Doors", Array.Empty<ProceduralDoorPlan>());
            Property(layout, "PlayerSpawnPosition", new Vector3(0f, .1f, 0f));
            var shape = new ProceduralOrganicRoom(1, ProceduralRoomShape.Rectangle,
                new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one });
            Property(layout, "OrganicRooms", new[] { shape });
            var rooms = new[] { new LevelRoom(1, new Vector3(2f, 1.6f, 2f), new Vector3(4f, 3.2f, 4f)) };
            Property(layout, "Graph", new LevelGraph(rooms, Array.Empty<LevelEdge>(),
                new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, new Vector3(1f, .1f, 1f)) }, 1, Vector3.zero));
            var config = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
            Set(config, "_doorWidth", 3.2f); Set(config, "_doorHeight", 2.8f);
            var driver = (ProceduralDriverConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralDriverConfig));
            Set(driver, "_wallThickness", .3f); Set(driver, "_floorThickness", .2f); Set(driver, "_ceilingThickness", .2f);
            var blocks = new ProceduralOrganicShellPresenter().Build(layout, config, driver);
            var reported = new Vector3(4f, .1f, 2f);
            Assert.That(rooms[0].ContainsXZ(reported), Is.True, "Inclusive footprint containment used to admit this wall socket.");
            Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Wall && Contains(b, reported + Vector3.up)), Is.True,
                "The old coarse socket intersects the refined room's boundary wall.");
            var candidates = ProceduralOrganicUtility.SpawnCandidates(layout, new[] { reported });
            Assert.That(candidates, Is.Not.Empty); CollectionAssert.DoesNotContain(candidates, reported);
            foreach (var candidate in candidates)
            {
                Assert.That(shape.Tiles.Any(t => ProceduralOrganicUtility.Center(layout, t, .1f) == candidate), Is.True);
                Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Wall && Contains(b, candidate + Vector3.up)), Is.False);
            }
        }

        private static bool Contains(ProceduralBlock block, Vector3 point)
        {
            var q = block.Rotation;
            var local = new Quaternion(-q.x, -q.y, -q.z, q.w) * (point - block.Center);
            // Match inclusive bounds at a wall face despite subtraction round-off.
            return Math.Abs(local.x) <= block.Size.x * .5f + .00001f &&
                Math.Abs(local.y) <= block.Size.y * .5f + .00001f && Math.Abs(local.z) <= block.Size.z * .5f + .00001f;
        }
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static MethodInfo Method(string name) => typeof(ProceduralController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
