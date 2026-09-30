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
    public sealed class ExpeditionFloorHookTests
    {
        [TestCase("sweet-tooth")] [TestCase("blind-faith")] [TestCase("golden-sense")]
        [TestCase("greedy-door")] [TestCase("faster-collapse")] [TestCase("shuffled-collapse")] [TestCase("wax-heart")]
        public void EveryMappedIdExistsAndSetsOnlyItsOwnHook(string id)
        {
            var catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try
            {
                var entry = catalogue.Entries.Single(e => e.Id == id);
                var view = new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), entry.Kind, 1) });
                var cakes = ExpeditionFloorEffectUtility.CakeHooks(view);
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
        public void EmptyViewsAreNeutralAndAssemblySuppliesRoundAndHooks()
        {
            Assert.That(ExpeditionFloorEffectUtility.FasterCollapse(null), Is.False);
            Assert.That(ExpeditionFloorEffectUtility.ShuffledCollapse(default(ActiveEffects)), Is.False);
            Assert.That(ExpeditionFloorEffectUtility.WaxHeart(null), Is.False);
            var c = ExpeditionFloorEffectUtility.CakeHooks(null);
            Assert.That(c.SweetTooth || c.BlindFaith || c.GoldenSense || c.GreedyDoor || c.MoreTraps || c.SilentTraps, Is.False);
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            Assert.That(source, Does.Contain("round: request.Round"));
            Assert.That(source, Does.Contain("cakeHooks: ExpeditionFloorEffectUtility.CakeHooks(activeEffects)"));
            Assert.That(source, Does.Contain("waxHeart: ExpeditionFloorEffectUtility.WaxHeart(activeEffects)"));
        }
    }
}
