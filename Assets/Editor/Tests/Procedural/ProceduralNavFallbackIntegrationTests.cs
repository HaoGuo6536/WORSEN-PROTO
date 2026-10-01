// ============================================================================
// ProceduralNavFallbackIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises recovery through the real Manager with template-only obstructions.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Require preflight and later native path failures to recover into an organic map.
//   - Verify seed/round provenance, failure reasons and no stale template geometry.
// DEPENDENCIES:
//   - NUnit, Domain.Procedural, synthetic manifest fixture and Unity objects.
// USAGE NOTES:
//   Coordinator-only native Edit Mode test; the offline runner reports environment.
//   No assets, scenes, agent settings or catalogue files are changed. Owners are
//   torn down explicitly; Edit Mode does not call OnDestroy on runtime components.
//   Owner playtest 2026-09-30: lines avoid obsolete cake markers. Failure injection
//   therefore obstructs required hunter sockets, which must not move with the cakes.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
                Release(owner);
                UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(driver);
            }
        }

        [TestCase(false), TestCase(true), Timeout(300000)]
        public void ForcedTemplateNavigationFailureProducesAdmittedOrganicFloorNotNoFloor(bool nativeFailure)
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
                    Id = "test_obstruction", File = "Castle_test_obstruction.fbx", Kind = "prop",
                    Size = nativeFailure ? new Vector3(2.1f, 3f, .1f) : Vector3.one } }).ToArray();
                // Every retained hunter socket is blocked or isolated; the hub
                // selector remains free to choose clear player/exit positions.
                foreach (var room in catalogue.Templates)
                {
                    room.HunterSpawn = new[] { room.HunterSpawn[0] };
                    room.Pieces = room.Pieces.Concat(nativeFailure ? new[] {
                        // Seal the hunter corner without touching its standing
                        // envelope or doorway sweeps. Only native paths fail.
                        new ProceduralTemplatePiece { Id = "test_obstruction", Position = new Vector3(1f, 0f, 2f) },
                        new ProceduralTemplatePiece { Id = "test_obstruction", Position = new Vector3(2f, 0f, 1f), RotY = 90f }
                    } : new[] { new ProceduralTemplatePiece { Id = "test_obstruction", Position = room.HunterSpawn[0] } }).ToArray();
                }
                Set(data, "_catalogues", new[] { catalogue }); Set(config, "_roomCatalogue", data);
                Set(config, "_organic", organic); Set(config, "_challenges", challenges);
                Set(config, "_gapProbability", 0f); Set(config, "_origin", new Vector2(10000f, 10000f));
                Set(config, "_knockablePropsPerRoom", 0);
                const int seed = 7, round = 1;
                for (int attempt = 0; attempt <= config.GenerationRetries; attempt++)
                {
                    int next = unchecked(seed + attempt * ProceduralGenerationController.SeedStride);
                    var candidate = new ProceduralController(new ProceduralBehaviorState(), config,
                        new System.Random(ProceduralController.LayoutSeed(next, round))).Generate(next, round);
                    Assert.That(candidate.UsesTemplates, Is.True, candidate.TemplateFallbackReason);
                    var blocks = new ProceduralGeometryPresenter().Build(candidate, config, driver);
                    var settings = NavMesh.GetSettingsByID(driver.NavMeshAgentTypeId);
                    new ProceduralCakeLinePresenter().Apply(candidate, config, blocks, settings.agentRadius, settings.agentHeight);
                    if (nativeFailure)
                    {
                        Assert.DoesNotThrow(() => new ProceduralNavFallbackPresenter()
                            .ValidateTemplate(candidate, blocks, settings.agentRadius, settings.agentHeight));
                        var existing = owner.GetComponent<ProceduralDriver>();
                        var boundary = existing != null ? existing : owner.AddComponent<ProceduralDriver>();
                        var failure = Assert.Throws<InvalidOperationException>(() => boundary.Build(candidate, config, driver));
                        Assert.That(failure.Message, Does.Contain("Generated navigation cannot reach required"));
                        Assert.That(boundary.IsReady, Is.False); Assert.That(boundary.OwnedBlockCount, Is.Zero);
                    }
                    else Assert.That(Assert.Throws<InvalidOperationException>(() => new ProceduralNavFallbackPresenter()
                        .ValidateTemplate(candidate, blocks, settings.agentRadius, settings.agentHeight)).Message, Does.Contain("hunter spawn="));
                }
                var manager = owner.AddComponent<ProceduralManager>();
                manager.Initialize(config, driver, seed, round);
                Assert.That(manager.IsReady, Is.True); Assert.That(manager.GenerationSucceeded, Is.True);
                Assert.That(manager.UsedFallback, Is.False, "Session/UI interprets UsedFallback as no playable floor.");
                Assert.That(manager.Graph, Is.Not.Null); Assert.That(manager.ValidatedHunterSpawnCapacity, Is.GreaterThanOrEqualTo(1));
                Assert.That(manager.LayoutManifest, Does.Contain("stage=organic"));
                Assert.That(manager.LayoutManifest, Does.Contain("template-attempts-exhausted"));
                if (nativeFailure) Assert.That(Uri.UnescapeDataString(manager.LayoutManifest), Does.Contain("Generated navigation cannot reach required"));
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
                Release(owner);
                foreach (var value in new UnityEngine.Object[] { config, organic, challenges, data, driver }) UnityEngine.Object.DestroyImmediate(value);
            }
        }
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        // Edit Mode never sends OnDestroy to these components: destroying the owner alone
        // leaves its baked NavMesh registered for later fixtures that share this origin.
        private static void Release(GameObject owner)
        {
            foreach (var manager in owner.GetComponents<ProceduralManager>()) manager.Teardown();
            foreach (var driver in owner.GetComponents<ProceduralDriver>()) driver.Teardown();
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }
}
