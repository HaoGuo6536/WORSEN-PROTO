// ============================================================================
// ProceduralRoomBudgetTests.cs
// ============================================================================
// PURPOSE:
//   Separates connected-floor growth from the independently sampled pocket budget.
//   A floor can gain required rooms without gaining total rooms when optional
//   pockets disappear, as at the batch-15 combat-to-shop transition.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check the production budget and footprint growth across rounds one to eight.
//   - Reproduce equal total counts with strictly growing connected counts.
//   - Require full budgets on template, missing-catalogue and exhausted-search paths.
//   - Preserve footprint, pocket isolation, template identity and graph reachability.
// DEPENDENCIES:
//   - Domain.Procedural, Core graph contracts, NUnit and existing template test data.
// USAGE NOTES:
//   Budget/footprint cases invoke production private methods with managed-only
//   config data, avoiding ScriptableObject construction in the headless tier.
//   Full-generation cases use temporary configs and synthetic catalogues for all
//   four theme contracts, not imported art. Native-backed Unity math may require
//   the coordinator's Edit Mode run; these tests do not claim a navigation bake.
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
    public sealed class ProceduralRoomBudgetTests
    {
        private static readonly int[] ExpectedBudgets = { 7, 9, 11, 13, 15, 15, 15, 15 };

        [TestCase(1, 7)] [TestCase(2, 9)] [TestCase(3, 11)] [TestCase(4, 13)]
        [TestCase(5, 15)] [TestCase(6, 15)] [TestCase(7, 15)] [TestCase(8, 15)]
        [TestCase(int.MaxValue, 15)]
        public void ConnectedRoomBudgetGrowsAndCaps(int round, int expected)
        {
            var config = ManagedGrowthConfig();
            Assert.That(Budget(Controller(config, 17, round), round), Is.EqualTo(expected));
        }

        [TestCase(7, 0, 15)] [TestCase(7, 3, 12)] [TestCase(7, int.MaxValue, 15)]
        public void ConfiguredGrowthHonorsMaximumWithoutOverflow(int initial, int growth, int maximum)
        {
            var config = ManagedGrowthConfig();
            Set(config, "_initialRoomCount", initial); Set(config, "_roomsPerRound", growth);
            Set(config, "_maximumRoomCount", maximum);
            int previous = 0;
            foreach (int round in Enumerable.Range(1, 8).Concat(new[] { int.MaxValue }))
            {
                int count = Budget(Controller(config, 17, round), round);
                Assert.That(count, Is.EqualTo((int)Math.Min(maximum, initial + (long)(round - 1) * growth)));
                Assert.That(count, Is.InRange(previous, maximum)); previous = count;
            }
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void OrganicFootprintsMeetEveryRoundBudgetWithSeparatePockets(bool castle, bool pockets)
        {
            var config = ManagedGrowthConfig();
            Set(config, "_castleModules", castle); Set(config, "_gapProbability", pockets ? 1f : 0f);
            Set(config, "_pocketProbability", pockets ? 1f : 0f);
            for (int seed = 0; seed < 32; seed++)
            {
                int previous = 0;
                for (int round = 1; round <= 8; round++)
                {
                    int optional = pockets && round >= config.GapStartRound ? config.PocketRoomCount : 0;
                    var controller = Controller(config, seed, round);
                    int budget = Budget(controller, round);
                    var footprints = Grow(controller, budget, round);
                    int connected = footprints.Count - optional;
                    Assert.That(connected, Is.EqualTo(ExpectedBudgets[round - 1]), "seed=" + seed + ", round=" + round);
                    Assert.That(connected, Is.InRange(previous, config.MaximumRoomCount));
                    Assert.That(footprints.SelectMany(c => c).Distinct().Count(), Is.EqualTo(footprints.Sum(c => c.Count)));
                    previous = connected;
                }
            }
        }

        [Test]
        public void DefaultPocketSamplingCanHideGrowthAtShopRoundThree()
        {
            var config = ManagedGrowthConfig();
            // Find a reproducible example using the real seeded footprint generator,
            // not a fabricated graph. The failing scene's random seed was not supplied.
            for (int seed = 0; seed < 128; seed++)
            {
                var combat = Controller(config, seed, 2); var shop = Controller(config, seed, 3);
                int combatBudget = Budget(combat, 2), shopBudget = Budget(shop, 3);
                var combatRooms = Grow(combat, combatBudget, 2); var shopRooms = Grow(shop, shopBudget, 3);
                if (combatRooms.Count != 11 || shopRooms.Count != 11) continue;
                Assert.That(combatRooms.Count - combatBudget, Is.EqualTo(2));
                Assert.That(shopRooms.Count - shopBudget, Is.Zero);
                Assert.That(shopBudget, Is.GreaterThan(combatBudget));
                TestContext.WriteLine("Pocket regression seed=" + seed + ": round 2=9+2=11; shop round 3=11+0=11.");
                return;
            }
            Assert.Fail("The declared seed sample must exercise the equal-total, growing-connected regression.");
        }

        [Test]
        public void GeneratedCountsMeetBudgetsAcrossAllThemesAndPaths(
            [Values("castle", "hospital", "school", "basement")] string theme,
            [Values("templates", "missing", "budget")] string path,
            [Values(false, true)] bool pockets,
            [Values(false, true)] bool shop)
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var themes = ScriptableObject.CreateInstance<ProceduralThemeConfig>();
            var data = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            var challenges = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            var organic = ScriptableObject.CreateInstance<ProceduralOrganicConfig>();
            try
            {
                var selected = new[] { themes.Castle, themes.Hospital, themes.School, themes.Basement }.Single(t => t.Id == theme);
                // Pin one genuine theme entry so each case exercises all eight rounds.
                Set(themes, "_castle", selected); Set(themes, "_hospitalEnabled", false);
                Set(themes, "_schoolEnabled", false); Set(themes, "_basementEnabled", false);
                Set(config, "_themes", themes); Set(config, "_challenges", challenges); Set(config, "_organic", organic);
                var catalogue = Catalogue(selected);
                Set(data, "_catalogues", new[] { catalogue });
                Set(config, "_roomCatalogue", path == "missing" ? null : data);
                if (path == "budget") Set(config, "_templatePlacementBudget", 1);
                Set(config, "_gapProbability", pockets ? 1f : 0f); Set(config, "_pocketProbability", pockets ? 1f : 0f);
                int previous = 0;
                for (int round = 1; round <= 8; round++)
                {
                    var layout = Controller(config, 17, round).Generate(17, round, shop, requiredHunterCount: shop ? 0 : 1);
                    Assert.That(layout.ThemeId, Is.EqualTo(theme));
                    Assert.That(layout.UsesTemplates, Is.EqualTo(path == "templates"), layout.TemplateFallbackReason);
                    if (path == "missing") Assert.That(layout.TemplateFallbackReason, Does.StartWith("missing-catalogue:"));
                    if (path == "budget") Assert.That(layout.TemplateFallbackReason, Does.Contain("placement budget exhausted"));
                    int connected = layout.Graph.Rooms.Count(room => !room.Pocket);
                    int optional = pockets && round >= config.GapStartRound ? config.PocketRoomCount : 0;
                    Assert.That(connected, Is.EqualTo(ExpectedBudgets[round - 1]), "round=" + round);
                    Assert.That(connected, Is.InRange(previous, config.MaximumRoomCount));
                    Assert.That(layout.Graph.Rooms.Count(room => room.Pocket), Is.EqualTo(optional));
                    Assert.That(layout.Graph.Rooms.Count, Is.EqualTo(connected + optional));
                    Assert.DoesNotThrow(() => ProceduralFootprintUtility.Validate(layout));
                    foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
                    {
                        var distances = LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, actor);
                        foreach (var room in layout.Graph.Rooms)
                            Assert.That(distances[room.Id] >= 0, Is.EqualTo(!room.Pocket));
                    }
                    if (layout.UsesTemplates)
                    {
                        Assert.That(layout.TemplateRooms.Count, Is.EqualTo(layout.Graph.Rooms.Count));
                        Assert.That(layout.TemplateRooms.All(r => catalogue.Templates.Contains(r.Template)), Is.True);
                        var halls = layout.TemplateRooms.Where(r => r.Template.SizeClass == "hall").ToArray();
                        Assert.That(halls.Select(r => r.Template.Id).Distinct().Count(), Is.EqualTo(halls.Length));
                    }
                    previous = connected;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(themes);
                UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(challenges);
                UnityEngine.Object.DestroyImmediate(organic);
            }
        }

        private static ProceduralTemplateCatalogue Catalogue(ProceduralThemeData theme)
        {
            var catalogue = ProceduralTemplateTestData.Catalogue();
            catalogue.Theme = theme.Id; catalogue.WallHeight = theme.WallHeight;
            foreach (var piece in catalogue.Kit)
            {
                piece.File = char.ToUpperInvariant(theme.Id[0]) + theme.Id.Substring(1) + "_" + piece.Id + ".fbx";
                piece.Size = new Vector3(piece.Size.x, theme.WallHeight, piece.Size.z);
            }
            foreach (var room in catalogue.Templates)
            {
                room.Id = theme.Id + room.Id.Substring("castle".Length);
                room.Height = theme.WallHeight;
            }
            ProceduralTemplateValidationUtility.Validate(catalogue);
            return catalogue;
        }

        private static ProceduralConfig ManagedGrowthConfig()
        {
            // Only the data read by RoomCount/GrowCells; no engine object is created.
            var config = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
            Set(config, "_initialRoomCount", 7); Set(config, "_roomsPerRound", 2); Set(config, "_maximumRoomCount", 15);
            Set(config, "_castleModules", true); Set(config, "_multiCellStartRound", 1);
            Set(config, "_oneCellWeight", .55f); Set(config, "_twoCellWeight", .3f); Set(config, "_threeCellWeight", .15f);
            Set(config, "_lShapeWeight", .4f); Set(config, "_gapStartRound", 2); Set(config, "_gapProbability", .35f);
            Set(config, "_maximumGapCells", 3); Set(config, "_pocketProbability", .5f); Set(config, "_pocketRoomCount", 2);
            return config;
        }
        private static ProceduralController Controller(ProceduralConfig config, int seed, int round)
            => new ProceduralController(new ProceduralBehaviorState(), config, new System.Random(ProceduralController.LayoutSeed(seed, round)));
        private static int Budget(ProceduralController controller, int round)
            => (int)Method("RoomCount").Invoke(controller, new object[] { round });
        private static List<List<Vector2Int>> Grow(ProceduralController controller, int count, int round)
            => (List<List<Vector2Int>>)Method("GrowCells").Invoke(controller, new object[] { count, round, null });
        private static MethodInfo Method(string name)
            => typeof(ProceduralController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
