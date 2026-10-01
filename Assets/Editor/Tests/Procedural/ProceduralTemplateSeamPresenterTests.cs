// ============================================================================
// ProceduralTemplateSeamPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Replays real catalogues through placement, seam resolution and route admission.
//   Counts organic fallbacks over bounded attempts rather than treating retries or
//   omitted seeds as successful template floors.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check single shared collision walls and open connected doorways.
//   - Measure per-theme fallback rates with real furniture and cake routes.
// DEPENDENCIES:
//   - NUnit, Core, Procedural and the production manifest parser.
// USAGE NOTES:
//   Managed collision evidence is not a native NavMesh bake or an art render.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;
using Worsen.Editor.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralTemplateSeamPresenterTests
    {
        [TestCase("Castle")] [TestCase("Hospital")] [TestCase("School")] [TestCase("Basement")]
        public void SeedSweepKeepsTemplateFallbackBelowTwoPercent(string theme)
        {
            var config = Config(theme); var driver = Driver();
            int fallback = 0, retried = 0;
            for (int seed = 0; seed < 64; seed++)
            {
                bool accepted = false; string reason = "";
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    int current = seed + attempt * ProceduralGenerationController.SeedStride;
                    var data = new ProceduralThemeData(theme.ToLowerInvariant(), true, Array.Empty<string>(), "", "", "", "", "", default, default, default, 0f);
                    if (!new ProceduralTemplateController(config, new System.Random(ProceduralController.LayoutSeed(current, 1)))
                        .TryGenerate(current, 1, data, false, 1, out var layout, out reason)) continue;
                    try
                    {
                        var blocks = new ProceduralTemplateGeometryPresenter().Build(layout, config, driver);
                        new ProceduralCakeLinePresenter().Apply(layout, config, blocks, .5f, 2f);
                        new ProceduralNavFallbackPresenter().ValidateTemplate(layout, blocks, .5f, 2f);
                        accepted = true; if (attempt > 0) retried++; break;
                    }
                    catch (InvalidOperationException error) { reason = error.Message; }
                }
                if (!accepted) { fallback++; TestContext.WriteLine("FALLBACK seed=" + seed + " reason=" + reason); }
            }
            TestContext.WriteLine("TEMPLATE_SWEEP theme=" + theme + " seeds=64 rooms=15 fallback=" + fallback + " retried=" + retried);
            Assert.That(fallback / 64d, Is.LessThan(.02d));
        }

        [Test]
        public void TouchingCastleRoomsHaveOneWallAndOneUnblockedDoor()
        {
            var c = Config("Castle"); var catalogue = c.RoomCatalogue.Catalogues[0];
            var template = catalogue.Templates.Single(t => t.Id == "castle_guard_room");
            var first = new ProceduralTemplateRoom { RoomId = 1, Template = template, OpenDoors = new[] { 1 } };
            var second = new ProceduralTemplateRoom { RoomId = 2, Template = template, Offset = new Vector2Int(0, 3), OpenDoors = new[] { 0 } };
            var layout = new ProceduralLayout(); Set(layout, "TemplateCatalogue", catalogue); Set(layout, "TemplateRooms", new[] { first, second });
            Set(layout, "Doors", new[] { new ProceduralDoorPlan(1, 2, new Vector3(3f, 0f, 6f), true) });
            var blocks = new ProceduralTemplateGeometryPresenter().Build(layout, c, Driver());
            foreach (float z in new[] { 5.7f, 6f, 6.3f })
            {
                Assert.That(blocks.Count(b => b.HasCollision && Contains(b, new Vector3(.8f, 1f, z))), Is.EqualTo(1));
                Assert.That(blocks.Any(b => b.HasCollision && Contains(b, new Vector3(3f, 1f, z))), Is.False);
            }
        }
        internal static ProceduralConfig Config(string theme)
        {
            var c = Empty<ProceduralConfig>(); var data = Empty<ProceduralRoomCatalogueData>();
            Field(data, "_catalogues", new[] { Read(theme) }); Field(c, "_roomCatalogue", data);
            Field(c, "_initialRoomCount", 15); Field(c, "_maximumRoomCount", 15); Field(c, "_templatePlacementBudget", 2048);
            Field(c, "_minimumHunterSpawnRooms", 2); Field(c, "_templateExitClearance", 1.6f); Field(c, "_templateExitSpawnDistance", 3.2f);
            Field(c, "_templateExitCakeClearance", 1.5f); Field(c, "_doorWidth", 3.2f); Field(c, "_doorHeight", 2.8f);
            Field(c, "_routeCakeSpacing", 1.5f); Field(c, "_anchorHeight", .05f); Field(c, "_spawnHeight", .1f);
            return c;
        }
        internal static ProceduralDriverConfig Driver()
        {
            var d = Empty<ProceduralDriverConfig>(); Field(d, "_floorThickness", .3f); Field(d, "_ceilingThickness", .3f);
            Field(d, "_wallThickness", .3f); Field(d, "_stairLandingExtension", .4f); Field(d, "_vaultHeight", .9f); return d;
        }
        internal static ProceduralTemplateCatalogue Read(string theme) => ProceduralRoomManifestSetup.Parse(
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Kit/" + theme + "Kit.manifest.json"),
            File.ReadAllText("Assets/Art/Environment/" + theme + "/Rooms/" + theme + "Rooms.manifest.json"));
        internal static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        internal static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        internal static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static bool Contains(ProceduralBlock b, Vector3 p)
        {
            var q = b.Rotation; p = new Quaternion(-q.x, -q.y, -q.z, q.w) * (p - b.Center);
            return Math.Abs(p.x) < b.Size.x * .5f && Math.Abs(p.y) < b.Size.y * .5f && Math.Abs(p.z) < b.Size.z * .5f;
        }
    }
}
