// ============================================================================
// ProceduralFurnishedUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Admits real furnished manifests and rejects damaged typed interaction contracts.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Preserve merged content counts, singular sockets and reserved clearances.
//   - Reject malformed lanes, gaps, transitions and premature puzzle admission.
// DEPENDENCIES:
//   - NUnit, production manifest parser and Domain.Procedural.
// USAGE NOTES:
//   Files are read only; no AssetDatabase calls or Unity allocation.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralFurnishedUtilityTests
    {
        [TestCase("Castle", 25)] [TestCase("Hospital", 24)] [TestCase("School", 22)] [TestCase("Basement", 23)]
        public void ExpansionMergesTenTemplatesAndPreservesTypedSockets(string theme, int count)
        {
            var c = Read(theme); Assert.That(c.Templates.Length, Is.EqualTo(count));
            Assert.That(c.Templates.Count(t => t.FurnishingVersion == 1), Is.EqualTo(10));
            Assert.That(c.Templates.Single(t => t.Kind == "shrine").ShrineSockets.Length, Is.EqualTo(1));
            Assert.That(c.Templates.Single(t => t.Kind == "puzzle").PuzzleSockets.Steps.Length, Is.EqualTo(3));
            Assert.That(c.Templates.Count(t => t.Transition != null), Is.EqualTo(2));
        }
        [TestCase("duplicate-shrine")] [TestCase("facing")] [TestCase("envelope")] [TestCase("reservation")]
        [TestCase("furniture")] [TestCase("lane-axis")] [TestCase("lane-step")] [TestCase("gap")]
        [TestCase("transition")] [TestCase("early-puzzle")] [TestCase("missing-lane")]
        [TestCase("missing-gap")] [TestCase("missing-approach")]
        public void BrokenInteractionContractFailsClosed(string mutation)
        {
            var c = Read("Castle"); var shrine = c.Templates.Single(t => t.Kind == "shrine"); var puzzle = c.Templates.Single(t => t.Kind == "puzzle");
            switch (mutation)
            {
                case "duplicate-shrine": shrine.ShrineSockets = new[] { shrine.ShrineSockets[0], shrine.ShrineSockets[0] }; break;
                case "facing": shrine.ShrineSockets[0].Facing = Vector3.one; break;
                case "envelope": shrine.ShrineSockets[0].ModelEnvelope = new Vector3(float.NaN, 2f, 1f); break;
                case "reservation": shrine.ReservedAreas[0].Size = Vector3.one * .1f; break;
                case "furniture": shrine.Pieces = shrine.Pieces.Concat(new[] { new ProceduralTemplatePiece { Id = c.Kit.First(p => p.Kind == "prop").Id, Position = shrine.ShrineSockets[0].Position } }).ToArray(); break;
                case "lane-axis": puzzle.PuzzleSockets.Axis = Vector3.one; break;
                case "lane-step": puzzle.PuzzleSockets.Steps[0] += Vector3.right; break;
                case "gap": shrine.PassageGap.Landing += Vector3.forward; break;
                case "transition": c.Templates.First(t => t.Transition != null).Transition.Doors[0].ClearWidth = 1f; break;
                case "early-puzzle": puzzle.MinRound = 2; break;
                case "missing-lane": puzzle.PuzzleSockets = null; break;
                case "missing-gap": shrine.PassageGap = null; break;
                case "missing-approach": shrine.ReservedAreas = shrine.ReservedAreas.Where(r => r.Id != "passage-approach").ToArray(); break;
            }
            Assert.Throws<ArgumentException>(() => ProceduralTemplateValidationUtility.Validate(c));
        }
        [Test]
        public void ParserRejectsDuplicateMergeAndMismatchedExpansion()
        {
            string root = "Assets/Art/Environment/Castle/";
            string kit = File.ReadAllText(root + "Kit/CastleKit.manifest.json"), rooms = File.ReadAllText(root + "Rooms/CastleRooms.manifest.json");
            string expansion = File.ReadAllText(root + "Rooms/CastleRooms.expansion.manifest.json");
            Assert.Throws<ArgumentException>(() => ProceduralRoomManifestSetup.Parse(kit, rooms, expansion.Replace("\"castle\"", "\"hospital\"")));
            Assert.Throws<ArgumentException>(() => ProceduralRoomManifestSetup.Parse(kit, expansion, expansion));
        }
        internal static ProceduralTemplateCatalogue Read(string theme)
        {
            string root = "Assets/Art/Environment/" + theme + "/";
            return ProceduralRoomManifestSetup.Parse(File.ReadAllText(root + "Kit/" + theme + "Kit.manifest.json"),
                File.ReadAllText(root + "Rooms/" + theme + "Rooms.manifest.json"), File.ReadAllText(root + "Rooms/" + theme + "Rooms.expansion.manifest.json"));
        }
        [Test]
        public void FurniturePredicateRequiresReadinessAndIncludesExpansionIdentity()
        {
            // Exercise only the managed query; no component lifecycle or build is invoked.
            var manager = ProceduralTemplateSeamPresenterTests.Empty<ProceduralManager>();
            var state = new ProceduralBehaviorState(); var layout = new ProceduralLayout();
            ProceduralTemplateSeamPresenterTests.Field(manager, "_state", state);
            ProceduralTemplateSeamPresenterTests.Set(state, "Layout", layout);
            ProceduralTemplateSeamPresenterTests.Set(layout, "TemplateRooms", new[] {
                new ProceduralTemplateRoom { RoomId = 7, Template = Read("Castle").Templates.First(t => t.FurnishingVersion == 1) } });
            Assert.That(manager.RoomHasAuthoredFurniture(7), Is.False);
            ProceduralTemplateSeamPresenterTests.Set(state, "IsReady", true);
            Assert.That(manager.RoomHasAuthoredFurniture(7), Is.True);
            Assert.That(manager.RoomHasAuthoredFurniture(8), Is.False);
        }
    }
}
