// ============================================================================
// ProceduralTemplateValidationUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises manifest admission before any generated object can reach Unity.
//   Each mutation breaks one contract family while starting from a complete kit
//   and room catalogue, so rejection cannot be attributed to missing test content.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Reject broken connectivity, sockets, anchors, identities and enclosure.
//   - Verify strict nested-array parsing of the art-worker manifest format.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural and Editor.Procedural.
// USAGE NOTES:
//   No imported assets, scene or native navigation are used.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralTemplateValidationUtilityTests
    {
        [Test] public void StubManifestsRoundTripThroughStrictParser()
        {
            var c = ProceduralTemplateTestData.Catalogue(); var json = ProceduralTemplateTestData.Json(c);
            var parsed = ProceduralRoomManifestSetup.Parse(json.kit, json.rooms);
            Assert.That(parsed.Templates.Select(t => t.Id), Is.EqualTo(c.Templates.Select(t => t.Id)));
            Assert.That(parsed.Templates[4].Footprint, Is.EqualTo(c.Templates[4].Footprint));
        }
        [TestCase("disconnected")] [TestCase("interior-door")] [TestCase("outside-anchor")]
        [TestCase("unknown-piece")] [TestCase("overlap")] [TestCase("open-wall")]
        [TestCase("near-wall")] [TestCase("density")] [TestCase("spawn")] [TestCase("gold")]
        [TestCase("round-arc")] [TestCase("rotation")] [TestCase("size")]
        [TestCase("early-gimmick")] [TestCase("weight")] [TestCase("height")]
        [TestCase("duplicate")] [TestCase("catalogue")] [TestCase("theme")] [TestCase("module")]
        [TestCase("missing-doors")] [TestCase("side")] [TestCase("missing-light")] [TestCase("wall-elevation")]
        [TestCase("filename")] [TestCase("closed-piece")] [TestCase("wide-hallway")]
        [TestCase("closed-overlap")]
        public void BrokenContractIsRejected(string mutation)
        {
            var c = ProceduralTemplateTestData.Catalogue(); var t = c.Templates[3];
            switch (mutation)
            {
                case "disconnected": t.Footprint[0] = new Vector2Int(100, 100); break;
                case "interior-door": t.Doors[0].Cell = Vector2Int.one; break;
                case "outside-anchor": t.Cake[0] = Vector3.one * 100f; break;
                case "near-wall": t.Cake[0] = new Vector3(.5f, 0f, 1f); break;
                case "unknown-piece": t.Pieces[0].Id = "hospital_wall_2m"; break;
                case "overlap": t.Pieces = t.Pieces.Concat(new[] { t.Pieces[0] }).ToArray(); break;
                case "open-wall": t.Pieces = t.Pieces.Where(p => p.Position.z != 0f).ToArray(); break;
                case "density": t.Cake = Array.Empty<Vector3>(); break;
                case "spawn": t.HunterSpawn = Array.Empty<Vector3>(); break;
                case "gold": t.GoldenCake = new[] { Vector3.one, Vector3.one * 2f }; break;
                case "round-arc": t.Shape = "round"; break;
                case "rotation": t.Pieces[0].RotY = 35f; break;
                case "size": t.SizeClass = "small"; break;
                case "early-gimmick": t.Gimmick = "freeze"; t.MinRound = 2; break;
                case "weight": t.Weight = float.NaN; break;
                case "height": t.Height = 3.2f; break;
                case "duplicate": c.Templates[0].Id = t.Id; break;
                case "catalogue": c.Templates = c.Templates.Take(9).ToArray(); break;
                case "theme": c.Theme = "hospital"; break;
                case "module": c.Module = 1f; break;
                case "missing-doors": t.Doors = Array.Empty<ProceduralTemplateDoor>(); break;
                case "side": t.Doors[0].Side = "south"; break;
                case "missing-light": t.Light = Array.Empty<Vector3>(); break;
                case "wall-elevation": t.Pieces[0].Position += Vector3.up; break;
                case "filename": c.Kit[0].File = "../Castle_wall_2m.fbx"; break;
                case "closed-piece": t.Doors[0].ClosedWith = new[] { new ProceduralTemplatePiece { Id = "missing" } }; break;
                case "closed-overlap": t.Doors[0].ClosedWith = new[] { t.Pieces[0] }; break;
                case "wide-hallway": t.Kind = "hallway"; break;
            }
            Assert.Throws<ArgumentException>(() => ProceduralTemplateValidationUtility.Validate(c), mutation);
        }
        [Test]
        public void RoundArcShellProducesOneOrderedBoundaryAndRejectsAnOpenSeam()
        {
            var catalogue = ProceduralTemplateTestData.Catalogue();
            catalogue.Kit = catalogue.Kit.Concat(new[] { new ProceduralKitPiece {
                Id = "wall_arc_r4", File = "Castle_wall_arc_r4.fbx", Kind = "arc", Size = new Vector3(2f, 7f, .5f) } }).ToArray();
            var pieces = new System.Collections.Generic.List<ProceduralTemplatePiece>();
            // Two semicircles with straight, grid-aligned doorway vestibules.
            foreach (int end in new[] { 0, 1 }) for (int i = 0; i < 6; i++)
            {
                float yaw = -75f + i * 30f + end * 180f;
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                pieces.Add(new ProceduralTemplatePiece { Id = "wall_arc_r4", RotY = yaw,
                    Position = new Vector3(4f, 0f, end == 0 ? 10f : 4f) + rotation * Vector3.forward * (4f * Mathf.Cos(15f * Mathf.Deg2Rad)) });
            }
            foreach (int x in new[] { 0, 8 }) foreach (int z in new[] { 5, 7, 9 })
                pieces.Add(new ProceduralTemplatePiece { Id = "wall_2m", Position = new Vector3(x, 0f, z), RotY = 90f });
            var room = new ProceduralRoomTemplate {
                Id = "castle_round_large", Kind = "room", Shape = "round", SizeClass = "large", Height = 7f,
                Gimmick = "none", MinRound = 1, Weight = 1f,
                Footprint = Enumerable.Range(0, 4).SelectMany(x => Enumerable.Range(0, 7).Select(z => new Vector2Int(x, z))).ToArray(),
                Doors = new[] { new ProceduralTemplateDoor { Cell = new Vector2Int(0, 3), Side = "W" }, new ProceduralTemplateDoor { Cell = new Vector2Int(3, 3), Side = "E" } },
                Cake = Enumerable.Range(1, 7).Select(z => new Vector3(4f, 0f, z + 3f)).ToArray(),
                Light = new[] { new Vector3(4f, 3f, 7f) }, HunterSpawn = new[] { new Vector3(4f, 0f, 3f) }, Pieces = pieces.ToArray() };
            ProceduralTemplateValidationUtility.ValidateRoom(catalogue, room);
            var placed = new ProceduralTemplateRoom { Template = room, Turns = 1, Offset = new Vector2Int(3, 2) };
            Assert.That(ProceduralTemplateValidationUtility.RoundBoundary(catalogue, placed, Vector2.zero), Has.Length.EqualTo(pieces.Count));
            room.Pieces[0].Position += Vector3.forward * .1f;
            var error = Assert.Throws<ArgumentException>(() => ProceduralTemplateValidationUtility.ValidateRoom(catalogue, room));
            Assert.That(error.Message, Does.Contain("round enclosure has an open or branching seam"));
        }
        [Test]
        public void OrganicFallbackPublishesItsCurvedShellAndVestibuleBoundary()
        {
            var layout = new ProceduralLayout();
            typeof(ProceduralLayout).GetProperty("OrganicRooms").SetValue(layout, new[] {
                new ProceduralOrganicRoom(2, ProceduralRoomShape.Round, Array.Empty<Vector2Int>(), Vector3.zero, Vector3.forward) });
            var boundary = ProceduralTemplateValidationUtility.PresentationBoundary(layout, 2);
            Assert.That(boundary.Count, Is.GreaterThan(3));
            Assert.That(boundary.Last(), Is.EqualTo(new Vector3(2f, 0f, 8f)));
            Assert.That(ProceduralTemplateValidationUtility.PresentationBoundary(layout, 3), Is.Null);
        }
    }
}
