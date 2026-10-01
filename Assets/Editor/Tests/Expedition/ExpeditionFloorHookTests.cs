// ============================================================================
// ExpeditionFloorHookTests.cs
// ============================================================================
// PURPOSE:
//   Verifies exact catalogue ids and real-round Floor hook mapping.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition.
// KEY RESPONSIBILITIES:
//   - Check each registered hook independently and keep missing Blinder hooks neutral.
//   - Check assembly forwards the request round rather than Floor's default round one.
// DEPENDENCIES:
//   Core, Floor, Progression catalogue, Expedition utility, NUnit and Unity test assets.
// USAGE NOTES:
//   Edit Mode. Source check covers the assembly call without generation or navigation.
// ============================================================================
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;

namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionFloorHookTests
    {
        [TestCase("sweet-tooth")] [TestCase("golden-sense")]
        [TestCase("faster-collapse")] [TestCase("shuffled-collapse")] [TestCase("wax-heart")] [TestCase("hidden-count")]
        public void EveryMappedIdExistsAndSetsOnlyItsOwnHook(string id)
        {
            var catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try
            {
                var entry = catalogue.Entries.Single(e => e.Id == id);
                var view = new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), entry.Kind, 1) });
                var cakes = ExpeditionFloorEffectUtility.CakeHooks(view, 1.15f);
                Assert.That(cakes.HiddenCount, Is.EqualTo(id == "hidden-count"));
                Assert.That(cakes.GoldenCakeMultiplier, Is.EqualTo(id == "faster-collapse" ? 1.15f : 1f));
                Assert.That(cakes.SweetTooth, Is.EqualTo(id == "sweet-tooth"));
                Assert.That(cakes.BlindFaith, Is.EqualTo(id == "blind-faith"));
                Assert.That(cakes.GoldenSense, Is.EqualTo(id == "golden-sense"));
                Assert.That(cakes.GreedyDoor, Is.EqualTo(id == "greedy-door"));
                Assert.That(ExpeditionFloorEffectUtility.FasterCollapse(view), Is.EqualTo(id == "faster-collapse"));
                Assert.That(ExpeditionFloorEffectUtility.ShuffledCollapse(view), Is.EqualTo(id == "shuffled-collapse"));
                Assert.That(ExpeditionFloorEffectUtility.WaxHeart(view), Is.EqualTo(id == "wax-heart"));
                Assert.That(cakes.MoreTraps || cakes.SilentTraps, Is.False);
            }
            finally { Object.DestroyImmediate(catalogue); }
        }
        [Test]
        public void NothingAddsOneSeededRosterDrawPerStack()
        {
            var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("nothing"), EffectKind.Curse, 5) });
            var a = ExpeditionFloorEffectUtility.ExtraHunters(effects, new[] { "B", "A", "A" }, 123, 7);
            Assert.That(a.Count, Is.EqualTo(5));
            Assert.That(a.All(x => x == "A" || x == "B"), Is.True);
            Assert.That(a, Is.EqualTo(ExpeditionFloorEffectUtility.ExtraHunters(effects, new[] { "A", "B" }, 123, 7)));
            Assert.That(ExpeditionFloorEffectUtility.ExtraHunters(null, null, 123, 7), Is.Empty);
            Assert.Throws<System.InvalidOperationException>(() => ExpeditionFloorEffectUtility.ExtraHunters(effects, null, 123, 7));
        }
        [Test]
        public void EmptyViewsAreNeutralAndAssemblySuppliesRoundAndHooks()
        {
            Assert.That(ExpeditionFloorEffectUtility.FasterCollapse(null), Is.False);
            Assert.That(ExpeditionFloorEffectUtility.ShuffledCollapse(default(ActiveEffects)), Is.False);
            Assert.That(ExpeditionFloorEffectUtility.WaxHeart(null), Is.False);
            var c = ExpeditionFloorEffectUtility.CakeHooks(null);
            Assert.That(c.SweetTooth || c.BlindFaith || c.GoldenSense || c.GreedyDoor || c.MoreTraps || c.SilentTraps, Is.False);
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            Assert.That(source, Does.Contain("round: request.Round"));
            Assert.That(source, Does.Contain("cakeHooks: ExpeditionFloorEffectUtility.CakeHooks(activeEffects, _progression.FasterCollapseGoldenCakeMultiplier)"));
            Assert.That(source, Does.Contain("waxHeart: ExpeditionFloorEffectUtility.WaxHeart(activeEffects)"));
        }
        [TestCase(1.15f)] [TestCase(1.4f)]
        public void ProgressionMultiplierAppliesOnlyToActiveFasterCollapse(float multiplier)
        {
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var owner = new GameObject("Multiplier bridge"); owner.SetActive(false);
            var manager = owner.AddComponent<ProgressionSessionManager>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                Assert.That(ProgressionSessionManager.Instance, Is.Null);
                typeof(ProgressionSessionManager).GetField("<Instance>k__BackingField",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, manager);
                typeof(ProgressionConfig).GetField("_fasterCollapseGoldenCakeMultiplier", flags).SetValue(config, multiplier);
                typeof(ProgressionSessionManager).GetField("config", flags).SetValue(manager, config);
                typeof(ProgressionSessionManager).GetField("controller", flags).SetValue(manager,
                    new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(7)));
                Assert.That(manager.FasterCollapseGoldenCakeMultiplier, Is.EqualTo(multiplier));
                var active = new ActiveEffects(new[] { new ActiveEffect(new EffectId("faster-collapse"), EffectKind.Curse, 1) });
                Assert.That(ExpeditionFloorEffectUtility.CakeHooks(active, manager.FasterCollapseGoldenCakeMultiplier).GoldenCakeMultiplier,
                    Is.EqualTo(multiplier));
                Assert.That(ExpeditionFloorEffectUtility.CakeHooks(null, manager.FasterCollapseGoldenCakeMultiplier).GoldenCakeMultiplier,
                    Is.EqualTo(1f));
            }
            finally
            {
                if (ProgressionSessionManager.Instance == manager)
                    typeof(ProgressionSessionManager).GetField("<Instance>k__BackingField",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, null);
                Object.DestroyImmediate(owner); Object.DestroyImmediate(config);
            }
        }
    }
}
