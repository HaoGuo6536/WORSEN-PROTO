// ============================================================================
// ProgressionRosterUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks approved first-admission gates without Unity allocation or lifetime data.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Cover every approved gate and reject legacy/unknown identities.
// DEPENDENCIES:
//   NUnit, Core effects and Progression pure rules only.
// USAGE NOTES:
//   Managed pure tests; serialized assets and actual hunter spawning are separate gates.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Session.Progression;
namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProgressionRosterUtilityTests
    {
        [TestCase("echo", 1)] [TestCase("weaver", 1)] [TestCase("ticking", 1)]
        [TestCase("ram", 4)] [TestCase("mannequin", 4)]
        [TestCase("mimic", 5)] [TestCase("blinder", 5)]
        [TestCase("skip", 6)] [TestCase("herald", 6)] [TestCase("stare", 6)]
        public void ExactGateAndDuplicateAdmission(string id, int gate)
        {
            Assert.That(ProgressionRosterUtility.FirstRound(id), Is.EqualTo(gate));
            Assert.That(ProgressionRosterUtility.Admits(id, gate - 1), Is.False);
            Assert.That(ProgressionRosterUtility.Admits(id, gate), Is.True);
            var row = new EffectCatalogueEntry(id, EffectKind.Threat, FearAxis.Time, id, "Adds a hunter.");
            var active = new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), EffectKind.Threat, 100) });
            Assert.That(EffectCatalogueUtility.Eligible(row, gate - 1, active), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(row, gate, active), Is.True);
            Assert.That(ProgressionRosterUtility.Admits(id, int.MaxValue), Is.True);
        }
        [TestCase("watcher")] [TestCase("rusher")] [TestCase("lurker")]
        [TestCase("hexer")] [TestCase("thorncaller")] [TestCase("unknown")]
        public void LegacyAndUnknownNeverEnterRuns(string id)
        {
            Assert.That(ProgressionRosterUtility.Admits(id, int.MaxValue), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(new EffectCatalogueEntry(id, EffectKind.Threat,
                FearAxis.Time, id, "Adds a hunter."), int.MaxValue, default(ActiveEffects)), Is.False);
        }
        [TestCase("watcher-long-memory")] [TestCase("echo-debt")] [TestCase("thin-skin")]
        [TestCase("bail-bond")] [TestCase("field-dressing")]
        [TestCase("mannequin-fewer-lamps")] [TestCase("mannequin-broken-lights")]
        [TestCase("afterglow")] [TestCase("blind-faith")] [TestCase("greedy-door")]
        public void RetiredRowsRemainIneligibleEvenInStaleCatalogues(string id)
        {
            Assert.That(ProgressionRosterUtility.Retired(id), Is.True);
            Assert.That(EffectCatalogueUtility.Eligible(new EffectCatalogueEntry(id, EffectKind.Curse,
                FearAxis.Time, id, "Removes safety."), 100, default(ActiveEffects)), Is.False);
        }
        [Test]
        public void OldEchoMultipliersCannotChangeOrdinaryReplay()
        {
            var rule = new ShrineEchoRule(ShrineKind.Pacification, 2, 2, .5f);
            Assert.That(rule.Magnitude, Is.EqualTo(1));
            Assert.That(rule.CostMultiplier, Is.EqualTo(1));
            Assert.That(rule.DelayMultiplier, Is.EqualTo(1f));
        }
    }
}
