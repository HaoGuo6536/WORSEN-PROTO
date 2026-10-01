// ============================================================================
// ProceduralNavFallbackIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises recovery through the real Manager with template-only obstructions.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Require a physical template rejection to recover into an admitted organic map.
//   - Verify seed/round provenance, failure reasons and no stale template geometry.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural, synthetic manifest fixture and Unity objects.
// USAGE NOTES:
//   Coordinator-only native Edit Mode test; the offline runner reports environment.
//   No assets, scenes, agent settings or catalogue files are changed.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralNavFallbackIntegrationTests
    {
        [Test, Timeout(300000)]
        public void Batch18SeedAndRoundAdmitWithTheWiredContentConfig()
        {
            var source = Resources.Load<ProceduralConfig>("ScriptableObjects/Domain/Procedural/ProceduralConfig");
            Assert.That(source, Is.Not.Null);
            var config = UnityEngine.Object.Instantiate(source);
            var driver = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<ProceduralDriverConfig>();
            var owner = new GameObject("Batch 18 seed replay");
            try
            {
                Set(config, "_origin", new Vector2(20000f, 20000f));
                var manager = owner.AddComponent<ProceduralManager>();
                manager.Initialize(config, driver, 133745427, 2);
                Assert.That(manager.IsReady, Is.True, manager.LayoutManifest);
                Assert.That(manager.GenerationSucceeded, Is.True); Assert.That(manager.UsedFallback, Is.False);
                Assert.That(manager.Graph.Rooms.Count(r => !r.Pocket), Is.EqualTo(9));
                TestContext.WriteLine(manager.LayoutManifest);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(driver);
            }
        }

        [Test, Timeout(300000)]
        public void ForcedTemplateNavigationFailureProducesAdmittedOrganicFloorNotNoFloor()
        {
            var config = ScriptableObject.CreateInstance<ProceduralConfig>();
            var organic = ScriptableObject.CreateInstance<ProceduralOrganicConfig>();
            var challenges = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            var data = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            var driver = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<ProceduralDriverConfig>();
            var owner = new GameObject("Template fallback admission fixture");
            try
            {
                var catalogue = ProceduralTemplateTestData.Catalogue();
                catalogue.Kit = catalogue.Kit.Concat(new[] { new ProceduralKitPiece {
                    Id = "test_obstruction", File = "Castle_test_obstruction.fbx", Kind = "prop", Size = Vector3.one } }).ToArray();
                // Room hubs remain clear. Every attached hallway contains an authored
                // prop at its required cake socket: graph connectivity alone cannot admit it.
                foreach (var room in catalogue.Templates.Where(t => t.Kind == "hallway"))
                    room.Pieces = room.Pieces.Concat(new[] { new ProceduralTemplatePiece {
                        Id = "test_obstruction", Position = room.Cake[0] } }).ToArray();
                Set(data, "_catalogues", new[] { catalogue }); Set(config, "_roomCatalogue", data);
                Set(config, "_organic", organic); Set(config, "_challenges", challenges);
                Set(config, "_gapProbability", 0f); Set(config, "_origin", new Vector2(10000f, 10000f));
                const int seed = 7, round = 1;
                for (int attempt = 0; attempt <= config.GenerationRetries; attempt++)
                {
                    int next = unchecked(seed + attempt * ProceduralGenerationController.SeedStride);
                    var candidate = new ProceduralController(new ProceduralBehaviorState(), config,
                        new System.Random(ProceduralController.LayoutSeed(next, round))).Generate(next, round);
                    Assert.That(candidate.UsesTemplates, Is.True, candidate.TemplateFallbackReason);
                    var blocks = new ProceduralGeometryPresenter().Build(candidate, config, driver);
                    Assert.That(Assert.Throws<InvalidOperationException>(() => new ProceduralNavFallbackPresenter()
                        .ValidateTemplate(candidate, blocks, .3f, 1.8f)).Message, Does.Contain("cake anchor="));
                }
                var manager = owner.AddComponent<ProceduralManager>();
                manager.Initialize(config, driver, seed, round);
                Assert.That(manager.IsReady, Is.True); Assert.That(manager.GenerationSucceeded, Is.True);
                Assert.That(manager.UsedFallback, Is.False, "Session/UI interprets UsedFallback as no playable floor.");
                Assert.That(manager.Graph, Is.Not.Null); Assert.That(manager.ValidatedHunterSpawnCapacity, Is.GreaterThanOrEqualTo(1));
                Assert.That(manager.LayoutManifest, Does.Contain("stage=organic"));
                Assert.That(manager.LayoutManifest, Does.Contain("template-attempts-exhausted"));
                Assert.That(manager.LayoutManifest, Does.Contain("fallback=Organic|generationSucceeded=true"));
                Assert.That(manager.LayoutManifest, Does.Not.Contain("NoFloor"));
                Assert.That(manager.LayoutManifest, Does.Contain("Organic"));
                Assert.That(owner.GetComponentsInChildren<Transform>().Count(t => t.name == "Generated Castle Rooms - Round 1"), Is.EqualTo(1));
                var state = (ProceduralBehaviorState)typeof(ProceduralManager).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                Assert.That(state.Layout.UsesTemplates, Is.False); Assert.That(state.Layout.OrganicRooms, Is.Not.Empty);
                Assert.That(state.Layout.RoundIndex, Is.EqualTo(round));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                foreach (var value in new UnityEngine.Object[] { config, organic, challenges, data, driver }) UnityEngine.Object.DestroyImmediate(value);
            }
        }
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
