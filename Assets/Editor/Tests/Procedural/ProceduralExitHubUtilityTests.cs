// ============================================================================
// ProceduralExitHubUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises exit-hub socket selection against the actual four theme manifests.
//   These managed tests isolate hub suitability from whole-catalogue admission,
//   which has independent content errors and requires a separate Unity gate.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify real-room exit/player floor support, separation and reproducibility.
//   - Reject single-door, gimmick and collision-filled hub candidates.
//   - Preserve irregular rooms whose bounding-box centre is not floor.
//   - Exercise production socket attachment before branches can consume the budget.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural and the existing bounded editor JSON reader.
// USAGE NOTES:
//   Projects manifest fields used by selection without repairing source content.
//   Does not assert these catalogues pass ProceduralRoomManifestSetup.Parse or bake.
//   No Unity objects, native calls, imports or assets are created.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;
using Json = Worsen.Editor.Procedural.ProceduralManifestJsonSetup.Value;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralExitHubUtilityTests
    {
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void RealThemesOfferSupportedSeparatedHubSockets(string theme)
        {
            var catalogue = Read(theme);
            int admitted = 0;
            foreach (var room in catalogue.Templates)
            {
                if (!Select(catalogue, room, out var spawn, out var exit)) continue;
                admitted++;
                Assert.That(room.Doors.Length, Is.GreaterThanOrEqualTo(2), room.Id);
                Assert.That(room.Gimmick, Is.EqualTo("none"));
                Assert.That(ProceduralTemplateUtility.Inside(room, spawn, .6f), Is.True, room.Id);
                Assert.That(ProceduralTemplateUtility.Inside(room, exit, 1.6f), Is.True, room.Id);
                Assert.That((exit - spawn).magnitude, Is.GreaterThanOrEqualTo(3.2f), room.Id);
                Assert.That(room.Cake.All(p => (p - exit).magnitude >= 1.5f), Is.True, room.Id);
                Assert.That(Select(catalogue, room, out var repeatSpawn, out var repeatExit), Is.True);
                Assert.That(repeatSpawn, Is.EqualTo(spawn)); Assert.That(repeatExit, Is.EqualTo(exit));
                for (int turn = 0; turn < 4; turn++)
                {
                    var placed = new ProceduralTemplateRoom { Template = room, Turns = turn, Offset = new Vector2Int(17, -11) };
                    var volumes = ProceduralTemplateUtility.Volumes(room.Footprint.Select(c =>
                        ProceduralTemplateUtility.Cell(c, turn) + placed.Offset), new Vector2(3f, 5f), room.Height);
                    foreach (var p in new[] { spawn, exit })
                    {
                        var world = ProceduralTemplateUtility.Point(placed, p, new Vector2(3f, 5f));
                        Assert.That(volumes.Any(b => world.x >= b.min.x && world.x <= b.max.x &&
                            world.y >= b.min.y && world.y <= b.max.y && world.z >= b.min.z && world.z <= b.max.z), Is.True, room.Id);
                    }
                }
                TestContext.WriteLine(theme + " hub candidate: " + room.Id);
            }
            Assert.That(admitted, Is.GreaterThan(0), theme + " needs at least one usable hub template.");
        }

        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void ProductionAttachmentBuildsTwoHubEntrancesBeforeBranches(string theme)
        {
            var catalogue = Read(theme);
            var starts = catalogue.Templates.Where(t => Select(catalogue, t, out _, out _)).ToArray();
            Assert.That(starts, Is.Not.Empty);
            var attach = typeof(ProceduralTemplateController).GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int seed = 0; seed < 16; seed++)
            {
                var random = new System.Random(seed);
                var config = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
                var controller = new ProceduralTemplateController(config, random);
                var hub = new ProceduralTemplateRoom { RoomId = 1, Template = starts[seed % starts.Length], Turns = seed % 4 };
                var placed = new List<ProceduralTemplateRoom> { hub };
                var occupied = new HashSet<Vector2Int>(ProceduralTemplateUtility.OccupiedCells(hub));
                var doors = new List<ProceduralDoorPlan>(); var gaps = new HashSet<Vector2Int>(); var sites = new List<ProceduralGapSite>();
                for (int attempt = 0; attempt < 2048 && placed.Count < 3; attempt++)
                    attach.Invoke(controller, new object[] { catalogue.Templates[random.Next(catalogue.Templates.Length)],
                        0, 0, placed, occupied, gaps, doors, sites });
                Assert.That(placed.Count, Is.EqualTo(3), theme + " seed=" + seed);
                Assert.That(hub.OpenDoors.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(doors.Count(d => d.FromRoomId == hub.RoomId || d.ToRoomId == hub.RoomId), Is.GreaterThanOrEqualTo(2));
                Assert.That(doors.All(d => !d.IsOptional), Is.True);
                var allCells = placed.SelectMany(ProceduralTemplateUtility.OccupiedCells).ToArray();
                Assert.That(allCells.Distinct().Count(), Is.EqualTo(allCells.Length));
                foreach (var door in doors)
                foreach (int id in new[] { door.FromRoomId, door.ToRoomId })
                {
                    var room = placed.Single(r => r.RoomId == id);
                    Assert.That(room.OpenDoors.Any(i => ProceduralTemplateUtility.Point(room,
                        ProceduralTemplateUtility.Door(room.Template.Doors[i]), Vector2.zero) == door.Center), Is.True);
                }
            }
        }

        [Test]
        public void RealBentNeighbourCentreIsOutsideFloorAtTheOldSampleRadius()
        {
            var room = Read("School").Templates.Single(t => t.Id == "school_stairwell_bend");
            var center = new Vector3(room.Footprint.Max(c => c.x) + 1f, 0f, room.Footprint.Max(c => c.y) + 1f);
            Assert.That(center, Is.EqualTo(new Vector3(6f, 0f, 6f)));
            Assert.That(ProceduralTemplateUtility.Inside(room, center), Is.False);
            float nearestFloor = room.Footprint.Min(c =>
            {
                float dx = Mathf.Max(c.x * 2f - center.x, 0f, center.x - (c.x * 2f + 2f));
                float dz = Mathf.Max(c.y * 2f - center.z, 0f, center.z - (c.y * 2f + 2f));
                return Mathf.Sqrt(dx * dx + dz * dz);
            });
            // Even the floor edge is 2m away before wall thickness/agent erosion.
            Assert.That(nearestFloor, Is.EqualTo(2f));
            Assert.That(room.Footprint.Any(c => ProceduralTemplateUtility.Inside(room,
                new Vector3(c.x * 2f + 1f, 0f, c.y * 2f + 1f), .6f)), Is.True);
        }

        [Test]
        public void NotchCentreDoesNotDisqualifyAnOtherwiseSupportedHub()
        {
            var catalogue = ProceduralTemplateTestData.Catalogue();
            var room = catalogue.Templates.Single(t => t.Id == "castle_medium_l");
            Assert.That(ProceduralTemplateUtility.Inside(room, new Vector3(4f, 0f, 4f), 1.6f), Is.False);
            Assert.That(Select(catalogue, room, out _, out var exit), Is.True);
            Assert.That(ProceduralTemplateUtility.Inside(room, exit, 1.6f), Is.True);
            Assert.That(exit, Is.Not.EqualTo(new Vector3(4f, 0f, 4f)));
        }

        [TestCase("single-door")] [TestCase("gimmick")] [TestCase("blocked")]
        public void UnsafeTemplateCannotBecomeTheHub(string mutation)
        {
            var catalogue = ProceduralTemplateTestData.Catalogue(); var room = catalogue.Templates[3];
            Assert.That(Select(catalogue, room, out _, out _), Is.True);
            if (mutation == "single-door") room.Doors = room.Doors.Take(1).ToArray();
            if (mutation == "gimmick") room.Gimmick = "freeze";
            if (mutation == "blocked")
            {
                catalogue.Kit = catalogue.Kit.Concat(new[] { new ProceduralKitPiece { Id = "blocker", Kind = "prop", Size = new Vector3(20f, 3f, 20f) } }).ToArray();
                room.Pieces = room.Pieces.Concat(new[] { new ProceduralTemplatePiece { Id = "blocker", Position = new Vector3(4f, 0f, 4f), RotY = 37f } }).ToArray();
            }
            Assert.That(Select(catalogue, room, out _, out _), Is.False);
        }

        private static bool Select(ProceduralTemplateCatalogue catalogue, ProceduralRoomTemplate room, out Vector3 spawn, out Vector3 exit)
            => ProceduralExitHubUtility.TrySelect(catalogue, room, 1.6f, 3.2f, 1.5f, 2.8f, out spawn, out exit);

        private static ProceduralTemplateCatalogue Read(string theme)
        {
            string root = "Assets/Art/Environment/" + theme;
            var kit = ProceduralManifestJsonSetup.Parse(File.ReadAllText(root + "/Kit/" + theme + "Kit.manifest.json"));
            var rooms = ProceduralManifestJsonSetup.Parse(File.ReadAllText(root + "/Rooms/" + theme + "Rooms.manifest.json"));
            return new ProceduralTemplateCatalogue
            {
                Theme = rooms["theme"].Text, WallHeight = Number(kit["wallHeight"]),
                Kit = kit["pieces"].Items.Select(p => new ProceduralKitPiece { Id = p["id"].Text, Kind = p["kind"].Text, Size = Point(p["size"]) }).ToArray(),
                Templates = rooms["templates"].Items.Select(t => new ProceduralRoomTemplate
                {
                    Id = t["id"].Text, Kind = t["kind"].Text, Gimmick = t["gimmick"].Text,
                    SizeClass = t["sizeClass"].Text, Shape = t["shape"].Text, Height = Number(t["height"]),
                    Footprint = t["footprint"].Items.Select(c => new Vector2Int(Integer(c[0]), Integer(c[1]))).ToArray(),
                    Doors = t["doors"].Items.Select(d => new ProceduralTemplateDoor { Cell = new Vector2Int(Integer(d["cell"][0]), Integer(d["cell"][1])),
                        Side = d["side"].Text, Span = d["span"] == null ? 1 : Integer(d["span"]) }).ToArray(),
                    Cake = t["anchors"]["cake"].Items.Select(Point).ToArray(),
                    Pieces = t["pieces"].Items.Select(p => new ProceduralTemplatePiece { Id = p["id"].Text, Position = Point(p["pos"]), RotY = Number(p["rotY"]) }).ToArray()
                }).ToArray()
            };
        }
        private static float Number(Json value) => float.Parse(value.Text, CultureInfo.InvariantCulture);
        private static int Integer(Json value) => int.Parse(value.Text, CultureInfo.InvariantCulture);
        private static Vector3 Point(Json value) => new Vector3(Number(value[0]), Number(value[1]), Number(value[2]));
    }
}
