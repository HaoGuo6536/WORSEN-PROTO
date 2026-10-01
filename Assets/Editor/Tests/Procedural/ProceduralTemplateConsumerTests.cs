// ============================================================================
// ProceduralTemplateConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Replays accepted art manifests through socket and collision consumers without
//   an editor or imported meshes. Independent geometry assertions catch lost span,
//   misplaced closures and render-only content accidentally sealing walking routes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check real manifest spans, boundary centres and rotated socket widths.
//   - Check authored frames, leaves, decals and Castle compound collision.
//   - Check hub and navigation clearance against the same command roles.
//   - Preserve all 62 authored vault tags, endpoints and unique surface identities.
// DEPENDENCIES:
//   - NUnit, Core, Domain.Procedural and the production editor manifest parser.
// USAGE NOTES:
//   Uninitialized configs supply managed serialized fields only. No Unity objects,
//   imports, scene writes or native navigation calls are made by these cases.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralTemplateConsumerTests
    {
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void RealManifestsPreserveEverySpanAndFrameCentre(string theme)
        {
            var catalogue = Read(theme);
            var source = ProceduralManifestJsonSetup.Parse(File.ReadAllText(RoomPath(theme)))["templates"].Items;
            int wide = 0, narrow = 0;
            for (int t = 0; t < source.Count; t++)
            for (int i = 0; i < source[t]["doors"].Items.Count; i++)
            {
                var raw = source[t]["doors"][i]; var door = catalogue.Templates[t].Doors[i];
                int expected = raw["span"] == null ? 1 : int.Parse(raw["span"].Text);
                Assert.That(door.Span, Is.EqualTo(expected));
                if (expected == 2) wide++; else narrow++;
                var center = ProceduralTemplateUtility.Door(door);
                var closure = door.ClosedWith.Aggregate(Vector3.zero, (sum, p) => sum + p.Position) / door.ClosedWith.Length;
                var normal = ProceduralTemplateUtility.Direction(door.Side);
                Assert.That(normal.x == 0 ? center.x : center.z, Is.EqualTo(normal.x == 0 ? closure.x : closure.z).Within(.0001f), catalogue.Templates[t].Id);
                Assert.That((closure - center).magnitude, Is.LessThanOrEqualTo(.401f));
            }
            Assert.That(wide, Is.GreaterThan(0)); Assert.That(narrow, Is.GreaterThan(0));
        }

        [TestCase("N", 1, 5f, 8f)] [TestCase("S", 1, 5f, 6f)]
        [TestCase("E", 1, 6f, 7f)] [TestCase("W", 1, 4f, 7f)]
        [TestCase("N", 2, 6f, 8f)] [TestCase("S", 2, 6f, 6f)]
        [TestCase("E", 2, 6f, 8f)] [TestCase("W", 2, 4f, 8f)]
        public void SocketCentreWidthAndSubcellVolumesRotateTogether(string side, int span, float x, float z)
        {
            var door = new ProceduralTemplateDoor { Cell = new Vector2Int(2, 3), Side = side, Span = span };
            Assert.That(ProceduralTemplateUtility.Door(door), Is.EqualTo(new Vector3(x, 0f, z)));
            Assert.That(ProceduralTemplateUtility.SocketWidth(door), Is.EqualTo(span * 2f));
            var expected = new Vector3(x, 0f, z);
            for (int turn = 0; turn < 4; turn++)
            {
                var room = new ProceduralTemplateRoom { Template = new ProceduralRoomTemplate { Height = 7f,
                    Footprint = new[] { new Vector2Int(2, 3) } }, Turns = turn, Offset = new Vector2Int(-5, 3), SubcellOffset = Vector2Int.one };
                Assert.That(ProceduralTemplateUtility.Point(room, ProceduralTemplateUtility.Door(door), new Vector2(3f, -2f)),
                    Is.EqualTo(expected + new Vector3(-6f, 0f, 5f)));
                var volumes = ProceduralTemplateUtility.Volumes(ProceduralTemplateUtility.OccupiedCells(room), new Vector2(3f, -2f), 7f, 1f);
                Assert.That(volumes.Sum(b => b.size.x * b.size.z), Is.EqualTo(4f));
                var tileCenter = ProceduralTemplateUtility.Point(room, new Vector3(5f, 0f, 7f), new Vector2(3f, -2f));
                Assert.That(volumes.Single().center, Is.EqualTo(tileCenter + Vector3.up * 3.5f));
                expected = new Vector3(expected.z, 0f, -expected.x);
            }
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(3)] [TestCase(2)]
        public void InvalidOrOffEdgeSpanIsRejected(int span)
        {
            var catalogue = Read("School"); var room = catalogue.Templates.First();
            room.Doors[0].Span = span;
            if (span == 2) room.Doors[0].Cell = new Vector2Int(room.Footprint.Max(c => c.x), 0);
            Assert.Throws<ArgumentException>(() => ProceduralTemplateValidationUtility.ValidateRoom(catalogue, room));
        }

        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void EveryRealFrameUsesAuthoredClosureAndClearWidthAtEveryTurn(string theme)
        {
            var catalogue = Read(theme);
            foreach (var template in catalogue.Templates)
            for (int index = 0; index < template.Doors.Length; index++)
            for (int turn = 0; turn < 4; turn++)
            {
                var socket = template.Doors[index];
                var pivot = socket.ClosedWith.Aggregate(Vector3.zero, (sum, p) => sum + p.Position) / socket.ClosedWith.Length;
                var frame = template.Pieces.Single(p => catalogue.Kit.Single(k => k.Id == p.Id).Kind == "door" && (p.Position - pivot).sqrMagnitude < .0001f);
                var room = new ProceduralTemplateRoom { RoomId = 1, Template = template, Turns = turn, Offset = new Vector2Int(7, -5),
                    SubcellOffset = new Vector2Int(1, 0), OpenDoors = new[] { index } };
                var opened = Build(catalogue, room);
                var world = ProceduralTemplateUtility.Point(room, pivot, Vector2.zero);
                var tangent = Yaw(turn * 90f + frame.RotY) * Vector3.right;
                // Measure the frame aperture independently of authored furniture.
                // Hospital handrails project past the jamb at waist height. Props
                // retain collision; admission failures are tested separately below.
                var frameCollision = opened.Where(b => b.HasCollision && b.PieceId == frame.Id).ToArray();
                for (int sign = -1; sign <= 1; sign++)
                    Assert.That(frameCollision.Any(b => Contains(b, world + tangent * (sign * 1.59f) + Vector3.up)), Is.False,
                        template.Id + " open socket=" + index + " turn=" + turn + " sign=" + sign);
                foreach (int sign in new[] { -1, 1 })
                    Assert.That(frameCollision.Any(b => Contains(b, world + tangent * (sign * 1.61f) + Vector3.up)), Is.True, template.Id + " jamb");
                var navigation = Navigation(room, world);
                Property(navigation, "Doors", new[] { new ProceduralDoorPlan(1, 2, world,
                    ProceduralTemplateUtility.Rotate(ProceduralTemplateUtility.Direction(socket.Side), turn).x == 0) });
                // This asserts the changed portal commands, not a new promise that
                // every catalogue prop layout passes whole-room native admission.
                var portal = opened.Where(b => b.PieceId != null && (catalogue.Kit.Single(k => k.Id == b.PieceId).Kind == "door" ||
                    b.Role == ProceduralBlockRole.VisualOnly || b.Role == ProceduralBlockRole.KitVisual)).ToArray();
                Assert.DoesNotThrow(() => new ProceduralNavFallbackPresenter().ValidateTemplate(navigation, portal, .3f, 1.8f), template.Id);
                Assert.That(opened.Any(b => b.HasCollision && Contains(b, world + Vector3.up)), Is.False, template.Id + " socket centre");
                var visual = opened.Single(b => b.Role == ProceduralBlockRole.KitVisual && b.PieceId == frame.Id && (b.PiecePosition - world).sqrMagnitude < .0001f);
                Assert.That(visual.HasCollision, Is.False);
                room.OpenDoors = Array.Empty<int>();
                var closed = Build(catalogue, room);
                Assert.That(closed.Any(b => b.Role == ProceduralBlockRole.KitVisual && (b.PiecePosition - world).sqrMagnitude < .0001f && b.PieceId == frame.Id), Is.False);
                Assert.That(closed.Any(b => b.HasCollision && Contains(b, world + Vector3.up)), Is.True);
                foreach (var replacement in socket.ClosedWith)
                    Assert.That(closed.Any(b => b.PieceId == replacement.Id && (b.PiecePosition - ProceduralTemplateUtility.Point(room, replacement.Position, Vector2.zero)).sqrMagnitude < .0001f), Is.True);
            }
        }

        [TestCase("Castle", "castle_guard_room", "door_iron_strapped")]
        [TestCase("Hospital", "hospital_ward_bed_bays", "door_double_porthole_4m")]
        [TestCase("School", "school_classroom", "prop_classroom_door_leaf")]
        [TestCase("Basement", "basement_pump_room", "prop_bulkhead_leaf")]
        public void OpenLeavesAreVisualOnlyAndSingleLeavesSwingAboutTheirOuterStile(string theme, string id, string leafId)
        {
            var catalogue = Read(theme); var template = catalogue.Templates.Single(t => t.Id == id);
            var piece = catalogue.Kit.Single(k => k.Id == leafId);
            // Basement exports a leaf but currently places none. Exercise the same
            // paired placement convention with the actual exported kit dimensions.
            if (theme == "Basement")
            {
                var center = ProceduralTemplateUtility.Door(template.Doors[0]);
                template.Pieces = template.Pieces.Concat(new[] {
                    new ProceduralTemplatePiece { Id = leafId, Position = center + Vector3.right * .8f, RotY = 180f },
                    new ProceduralTemplatePiece { Id = leafId, Position = center - Vector3.right * .8f, RotY = 180f } }).ToArray();
            }
            for (int turn = 0; turn < 4; turn++)
            {
                var room = new ProceduralTemplateRoom { RoomId = 1, Template = template, Turns = turn, OpenDoors = new[] { 0 } };
                var pivot = template.Doors[0].ClosedWith.Aggregate(Vector3.zero, (sum, p) => sum + p.Position) / template.Doors[0].ClosedWith.Length;
                var leaves = template.Pieces.Where(p => p.Id == leafId && Math.Abs(p.Position.z - pivot.z) < .001f).ToArray();
                var blocks = Build(catalogue, room).Where(b => b.PieceId == leafId).ToArray();
                Assert.That(blocks.Length, Is.EqualTo(leaves.Length));
                foreach (var block in blocks)
                {
                    Assert.That(block.HasCollision, Is.False); Assert.That(block.HasRenderer, Is.True);
                    Assert.That(block.TraversalKind, Is.EqualTo(TraversalSurfaceKind.None));
                }
                foreach (var leaf in leaves)
                {
                    var rotation = Yaw(turn * 90f + leaf.RotY);
                    var local = new Quaternion(-rotation.x, -rotation.y, -rotation.z, rotation.w) * ProceduralTemplateUtility.Rotate(leaf.Position - pivot, turn);
                    float sign = local.x > .001f ? 1f : -1f;
                    var hinge = ProceduralTemplateUtility.Point(room, leaf.Position, Vector2.zero) + rotation * Vector3.right * (sign * piece.Size.x * .5f);
                    if (theme == "Hospital") Assert.That(blocks.Single().PiecePosition, Is.EqualTo(ProceduralTemplateUtility.Point(room, leaf.Position, Vector2.zero)));
                    else Assert.That(blocks.Any(b => (b.PiecePosition + b.Rotation * Vector3.right * (sign * piece.Size.x * .5f) - hinge).magnitude < .001f &&
                        Math.Abs(Vector3.Dot(b.Rotation * Vector3.right, rotation * Vector3.right)) < .001f), Is.True);
                }
                room.OpenDoors = Array.Empty<int>();
                Assert.That(Build(catalogue, room).Any(b => b.PieceId == leafId), Is.False);
            }
        }

        [TestCase("wall_arc_r4", 4f, 30f)] [TestCase("wall_arc_r6", 6f, 20f)] [TestCase("wall_arc_r8", 8f, 15f)]
        [TestCase("tower_wall_arc_r4", 4f, 30f)]
        public void CastleArcCollisionCoversRadialShellWithoutFillingItsInterior(string id, float radius, float sweep)
        {
            var catalogue = Read("Castle");
            var template = new ProceduralRoomTemplate { Height = 7f, Pieces = new[] {
                new ProceduralTemplatePiece { Id = id, Position = new Vector3(6f, 0f, -3f), RotY = 37f } } };
            for (int turn = 0; turn < 4; turn++)
            {
                var room = new ProceduralTemplateRoom { Template = template, Turns = turn };
                var blocks = Build(catalogue, room); var visual = blocks.Single(b => !b.HasCollision);
                Assert.That(blocks.Count(b => b.HasCollision), Is.EqualTo(4));
                var pivot = ProceduralTemplateUtility.Point(room, template.Pieces[0].Position, Vector2.zero);
                var rotation = Yaw(turn * 90f + 37f);
                Assert.That(visual.PiecePosition, Is.EqualTo(pivot));
                for (int i = 0; i <= 4; i++)
                foreach (float r in new[] { radius - .4f, radius + .4f })
                {
                    double angle = (-sweep * .5f + sweep * i / 4f) * Math.PI / 180d;
                    var sample = pivot + rotation * new Vector3(r * (float)Math.Sin(angle), 1f, r * (float)Math.Cos(angle) - radius);
                    Assert.That(blocks.Any(b => b.HasCollision && Contains(b, sample)), Is.True, id + " radial vertex " + i);
                }
                Assert.That(blocks.Any(b => b.HasCollision && Contains(b, pivot + rotation * new Vector3(0f, 1f, -1f))), Is.False);
            }
        }

        [Test]
        public void CastleTangentPiersDoNotSealTheMiddleAndNavPreflightAgrees()
        {
            var catalogue = Read("Castle"); var template = new ProceduralRoomTemplate { Height = 7f,
                Pieces = new[] { new ProceduralTemplatePiece { Id = "wall_round_tangent_r4" } } };
            var room = new ProceduralTemplateRoom { RoomId = 1, Template = template };
            var blocks = Build(catalogue, room);
            Assert.That(blocks.Count(b => b.Role == ProceduralBlockRole.KitVisual), Is.EqualTo(1));
            Assert.That(blocks.Count(b => b.HasCollision), Is.EqualTo(2));
            foreach (int sign in new[] { -1, 1 })
                Assert.That(blocks.Any(b => b.HasCollision && Contains(b, new Vector3(sign * 2f, 1f, -.4f))), Is.True);
            var layout = Navigation(room, Vector3.zero);
            Assert.DoesNotThrow(() => new ProceduralNavFallbackPresenter().ValidateTemplate(layout, blocks, .3f, 1.8f));
        }

        [TestCase(0f)] [TestCase(.02f)] [TestCase(2f)]
        public void DecalsDoNotReserveHubClearanceOrBlockNavigation(float height)
        {
            var catalogue = ProceduralTemplateTestData.Catalogue(); var template = catalogue.Templates[3];
            Assert.That(ProceduralExitHubUtility.TrySelect(catalogue, template, 1.6f, 3.2f, 1.5f, 2.8f, out var spawn, out var exit), Is.True);
            catalogue.Kit = catalogue.Kit.Concat(new[] { new ProceduralKitPiece { Id = "decal_probe", Kind = "decal", Size = new Vector3(20f, height, 20f) } }).ToArray();
            template.Pieces = template.Pieces.Concat(new[] { new ProceduralTemplatePiece { Id = "decal_probe", Position = new Vector3(4f, .01f, 4f) } }).ToArray();
            Assert.That(ProceduralExitHubUtility.TrySelect(catalogue, template, 1.6f, 3.2f, 1.5f, 2.8f, out var nextSpawn, out var nextExit), Is.True);
            Assert.That(nextSpawn, Is.EqualTo(spawn)); Assert.That(nextExit, Is.EqualTo(exit));
            var room = new ProceduralTemplateRoom { RoomId = 1, Template = template };
            var block = Build(catalogue, room).Single(b => b.PieceId == "decal_probe");
            Assert.That(block.HasCollision, Is.False); Assert.That(block.HasRenderer, Is.True);
            Assert.DoesNotThrow(() => new ProceduralNavFallbackPresenter().ValidateTemplate(Navigation(room, exit), new[] { block }, .3f, 1.8f));
            catalogue.Kit.Last().Kind = "prop";
            Assert.That(ProceduralExitHubUtility.TrySelect(catalogue, template, 1.6f, 3.2f, 1.5f, 2.8f, out _, out _), Is.False);
        }

        [TestCase("Castle", "castle_chapel", "prop_bench")]
        [TestCase("Hospital", "hospital_ward_bed_bays", "curtain_track_bay")]
        public void AuthoredApproachObstructionsStillFailClosed(string theme, string id, string obstacle)
        {
            var catalogue = Read(theme); var template = catalogue.Templates.Single(t => t.Id == id);
            var room = new ProceduralTemplateRoom { RoomId = 1, Template = template, OpenDoors = new[] { 1 } };
            var pivot = template.Doors[1].ClosedWith.Aggregate(Vector3.zero, (sum, p) => sum + p.Position) / template.Doors[1].ClosedWith.Length;
            var layout = Navigation(room, pivot);
            Property(layout, "Doors", new[] { new ProceduralDoorPlan(1, 2, pivot, template.Doors[1].Side == "N") });
            var blocks = Build(catalogue, room);
            Assert.That(blocks.Any(b => b.PieceId == obstacle && b.HasCollision), Is.True);
            Assert.DoesNotThrow(() => new ProceduralNavFallbackPresenter().ValidateTemplate(layout, blocks, .5f, 2f),
                "The corrected source now leaves this doorway clear.");
            // Preserve the negative control explicitly instead of requiring the
            // shipped catalogue to retain its old obstructed furniture placement.
            var moved = template.Pieces.First(p => p.Id == obstacle);
            moved.Position = pivot; moved.RotY = 0f;
            blocks = Build(catalogue, room);
            Assert.That(Assert.Throws<InvalidOperationException>(() => new ProceduralNavFallbackPresenter()
                .ValidateTemplate(layout, blocks, .5f, 2f)).Message, Does.Contain("piece=" + obstacle));
        }

        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void RoomToHallwayAttachmentUsesMetreOffsetsWithoutOverlap(string theme)
        {
            var catalogue = Read(theme);
            var hubTemplate = catalogue.Templates.First(t => t.Kind == "room" && t.Doors.All(d => d.Span == 1) &&
                ProceduralExitHubUtility.TrySelect(catalogue, t, 1.6f, 3.2f, 1.5f, 2.8f, out _, out _));
            var hallway = catalogue.Templates.First(t => t.Kind == "hallway" && t.Doors.All(d => d.Span == 2));
            var config = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
            var attach = typeof(ProceduralTemplateController).GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Instance);
            var data = (ProceduralRoomCatalogueData)FormatterServices.GetUninitializedObject(typeof(ProceduralRoomCatalogueData));
            Field(data, "_catalogues", new[] { catalogue }); Field(config, "_roomCatalogue", data);
            for (int turn = 0; turn < 4; turn++)
            {
                var controller = new ProceduralTemplateController(config, new System.Random(13 + turn));
                var hub = new ProceduralTemplateRoom { RoomId = 1, Template = hubTemplate, Catalogue = catalogue, Turns = turn };
                var placed = new System.Collections.Generic.List<ProceduralTemplateRoom> { hub };
                var occupied = new System.Collections.Generic.HashSet<Vector2Int>(ProceduralTemplateUtility.OccupiedCells(hub));
                var doors = new System.Collections.Generic.List<ProceduralDoorPlan>();
                for (int attempt = 0; attempt < 2048 && placed.Count < 2; attempt++)
                    attach.Invoke(controller, new object[] { hallway, 0, 0, placed, occupied,
                        new System.Collections.Generic.HashSet<Vector2Int>(), doors, new System.Collections.Generic.List<ProceduralGapSite>(), 0, null });
                Assert.That(placed.Count, Is.EqualTo(2), theme + " turn=" + turn);
                Assert.That(placed[1].SubcellOffset, Is.Not.EqualTo(Vector2Int.zero));
                var cells = placed.SelectMany(ProceduralTemplateUtility.OccupiedCells).ToArray();
                Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Length));
                foreach (var door in doors)
                foreach (var room in placed)
                    Assert.That(room.OpenDoors.Any(i => ProceduralTemplateUtility.Point(room,
                        ProceduralTemplateUtility.Door(room.Template.Doors[i]), Vector2.zero) == door.Center), Is.True);
            }
        }

        [Test]
        public void EveryAuthoredVaultBecomesOneCollisionBearingVaultBlockAtEveryRoomTurn()
        {
            int authored = 0;
            foreach (string theme in new[] { "Castle", "Hospital", "School", "Basement" })
            {
                var catalogue = Read(theme);
                var raw = ProceduralManifestJsonSetup.Parse(File.ReadAllText(RoomPath(theme)))["templates"].Items;
                for (int t = 0; t < raw.Count; t++)
                {
                    var template = catalogue.Templates[t];
                    var indices = Enumerable.Range(0, raw[t]["pieces"].Items.Count)
                        .Where(i => raw[t]["pieces"][i]["traversal"]?.Text == "vault").ToArray();
                    authored += indices.Length;
                    for (int turn = 0; turn < 4; turn++)
                    {
                        var room = new ProceduralTemplateRoom { RoomId = t + 1, Template = template, Turns = turn,
                            Offset = new Vector2Int(7, -5), SubcellOffset = Vector2Int.one };
                        var blocks = Build(catalogue, room);
                        var vaults = blocks.Where(b => b.TraversalKind == TraversalSurfaceKind.Vault).ToArray();
                        Assert.That(vaults.Length, Is.EqualTo(indices.Length), theme + "/" + template.Id);
                        Assert.That(vaults.Select(b => b.SurfaceId).Distinct().Count(), Is.EqualTo(vaults.Length));
                        foreach (int i in indices)
                        {
                            var piece = template.Pieces[i]; var kit = catalogue.Kit.Single(k => k.Id == piece.Id);
                            Assert.That(piece.TraversalKind, Is.EqualTo(TraversalSurfaceKind.Vault));
                            Assert.That(kit.TraversalKind, Is.EqualTo(TraversalSurfaceKind.Vault));
                            var block = vaults.Single(b => b.PiecePosition == ProceduralTemplateUtility.Point(room, piece.Position, Vector2.zero));
                            Assert.That(block.HasCollision, Is.True); Assert.That(block.SurfaceId, Is.Not.Zero);
                            Assert.That(block.EndpointA, Is.EqualTo(ProceduralTemplateUtility.Point(room, piece.EndpointA, Vector2.zero)));
                            Assert.That(block.EndpointB, Is.EqualTo(ProceduralTemplateUtility.Point(room, piece.EndpointB, Vector2.zero)));
                        }
                        Assert.That(blocks.Where(b => b.TraversalKind == TraversalSurfaceKind.None).All(b => b.SurfaceId == 0), Is.True);
                    }
                }
            }
            Assert.That(authored, Is.EqualTo(62));
        }

        [TestCase("\"traversal\": \"vault\"", "\"traversal\": \"ladder\"")]
        [TestCase("\"collision\": true", "\"collision\": false")]
        [TestCase("\"endpointA\"", "\"missingEndpointA\"")]
        public void MalformedVaultMetadataFailsClosed(string before, string after)
        {
            string kit = File.ReadAllText("Assets/Art/Environment/Castle/Kit/CastleKit.manifest.json");
            string rooms = File.ReadAllText(RoomPath("Castle"));
            Assert.That(rooms, Does.Contain(before));
            Assert.Throws<ArgumentException>(() => ProceduralRoomManifestSetup.Parse(kit, rooms.Replace(before, after)));
        }

        private static ProceduralLayout Navigation(ProceduralTemplateRoom room, Vector3 point)
        {
            var layout = new ProceduralLayout();
            Property(layout, "TemplateRooms", new[] { room });
            Property(layout, "Graph", new LevelGraph(Array.Empty<LevelRoom>(), Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, point));
            Property(layout, "PlayerSpawnPosition", point);
            Property(layout, "HunterSpawnPositions", Array.Empty<Vector3>());
            Property(layout, "Doors", new[] { new ProceduralDoorPlan(1, 2, point, true) });
            return layout;
        }
        private static System.Collections.Generic.IReadOnlyList<ProceduralBlock> Build(ProceduralTemplateCatalogue catalogue, ProceduralTemplateRoom room)
        {
            var config = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
            var driver = (ProceduralDriverConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralDriverConfig));
            Field(config, "_doorWidth", 3.2f); Field(config, "_doorHeight", 2.8f);
            Field(driver, "_floorThickness", .3f); Field(driver, "_ceilingThickness", .3f); Field(driver, "_wallThickness", .3f);
            return new ProceduralTemplateGeometryPresenter().Build(catalogue, room, config, driver);
        }
        private static Quaternion Yaw(float degrees) => new Quaternion(0f, (float)Math.Sin(degrees * Math.PI / 360d), 0f, (float)Math.Cos(degrees * Math.PI / 360d));
        private static bool Contains(ProceduralBlock block, Vector3 point)
        {
            var q = block.Rotation; var local = new Quaternion(-q.x, -q.y, -q.z, q.w) * (point - block.Center);
            return Math.Abs(local.x) <= block.Size.x * .5f + .0001f && Math.Abs(local.y) <= block.Size.y * .5f + .0001f && Math.Abs(local.z) <= block.Size.z * .5f + .0001f;
        }
        private static string RoomPath(string theme) => "Assets/Art/Environment/" + theme + "/Rooms/" + theme + "Rooms.manifest.json";
        private static ProceduralTemplateCatalogue Read(string theme) => ProceduralRoomManifestSetup.Parse(
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Kit/" + theme + "Kit.manifest.json"), File.ReadAllText(RoomPath(theme)));
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
