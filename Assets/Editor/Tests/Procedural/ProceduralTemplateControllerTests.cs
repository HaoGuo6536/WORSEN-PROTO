// ============================================================================
// ProceduralTemplateControllerTests.cs
// ============================================================================
// PURPOSE:
//   Tests seeded floors against complete stub manifests before art integration.
//   Assertions require actual template provenance, not an organic floor that merely
//   happens to be deterministic, and separately check explicit fallback admission.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify template-only floors, socket connections, pacing and landmarks.
//   - Verify missing/invalid/exhausted catalogue fallback and capacity failure.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural, Editor parser and temporary Unity configs.
// USAGE NOTES:
//   Native navigation remains the coordinator's gate; no test calls Unity CLI.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralTemplateControllerTests
    {
        private ProceduralConfig _config;
        private ProceduralRoomCatalogueData _data;
        private ProceduralChallengeConfig _challenges;
        private ProceduralOrganicConfig _organic;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _data = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            _challenges = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            _organic = ScriptableObject.CreateInstance<ProceduralOrganicConfig>(); Set(_config, "_organic", _organic);
            var json = ProceduralTemplateTestData.Json(ProceduralTemplateTestData.Catalogue());
            Set(_data, "_catalogues", new[] { ProceduralRoomManifestSetup.Parse(json.kit, json.rooms) });
            Set(_config, "_roomCatalogue", _data); Set(_config, "_challenges", _challenges); Set(_config, "_gapProbability", 0f);
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_data); UnityEngine.Object.DestroyImmediate(_challenges); UnityEngine.Object.DestroyImmediate(_organic); }
        [Test] public void StubCatalogueProducesOnlyDeterministicTemplateRoomsAndHallways()
        {
            for (int seed = 0; seed < 16; seed++)
            {
                var layout = Generate(seed, 1);
                Assert.That(layout.UsesTemplates, Is.True, layout.TemplateFallbackReason);
                Assert.That(layout.TemplateRooms.Count, Is.EqualTo(layout.Graph.Rooms.Count));
                Assert.That(layout.OrganicRooms, Is.Empty);
                Assert.That(layout.TemplateRooms.Any(r => r.Template.Kind == "hallway"), Is.True);
                Assert.That(layout.Manifest, Is.EqualTo(Generate(seed, 1).Manifest));
                Assert.That(layout.TemplateRooms.Where(r => r.Template.SizeClass == "hall").Select(r => r.Template.Id).Distinct().Count(),
                    Is.EqualTo(layout.TemplateRooms.Count(r => r.Template.SizeClass == "hall")));
                Assert.That(layout.HunterSpawnPositions, Is.Not.Empty); ProceduralFootprintUtility.Validate(layout);
                Assert.That(Vector3.Distance(layout.PlayerSpawnPosition, layout.Graph.ExitPosition), Is.GreaterThanOrEqualTo(3.2f));
                foreach (var door in layout.Doors)
                    foreach (int id in new[] { door.FromRoomId, door.ToRoomId })
                    {
                        var room = layout.TemplateRooms.Single(r => r.RoomId == id);
                        Assert.That(room.OpenDoors.Any(i => ProceduralTemplateUtility.Point(room, ProceduralTemplateUtility.Door(room.Template.Doors[i]), layout.Origin) == door.Center), Is.True);
                    }
            }
        }
        [Test] public void GimmickTagsHonorMinRoundAndSharedCurve()
        {
            for (int round = 1; round <= 9; round++)
            {
                var layout = Generate(17, round);
                Assert.That(layout.UsesTemplates, Is.True, layout.TemplateFallbackReason);
                Assert.That(layout.TemplateRooms.All(r => r.Template.MinRound <= round), Is.True);
                Assert.That(layout.TemplateRooms.Count(r => r.Template.Gimmick != "none"), Is.LessThanOrEqualTo(ProceduralGimmickUtility.Budget(_challenges, round)));
            }
        }
        [TestCase("missing")] [TestCase("invalid")] [TestCase("budget")]
        public void UnavailableTemplatePathRecordsOrganicFallback(string reason)
        {
            if (reason == "missing") Set(_config, "_roomCatalogue", null);
            if (reason == "invalid") _data.Catalogues[0].Module = 3f;
            if (reason == "budget") Set(_config, "_templatePlacementBudget", 1);
            var layout = Generate(7, 1);
            Assert.That(layout.UsesTemplates, Is.False); Assert.That(layout.TemplateFallbackReason, Is.Not.Empty);
            Assert.That(layout.Manifest, Does.Contain("organic-fallback="));
            Assert.That(layout.OrganicRooms, Is.Not.Empty);
        }
        [Test] public void InsufficientCapacityCannotSilentlyAdmitRetainedHunters()
            => Assert.Throws<InvalidOperationException>(() => new ProceduralController(new ProceduralBehaviorState(), _config, new System.Random(7))
                .Generate(7, 1, requiredHunterCount: 10000));
        private ProceduralLayout Generate(int seed, int round) => new ProceduralController(new ProceduralBehaviorState(), _config,
            new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
