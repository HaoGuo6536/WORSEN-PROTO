// ============================================================================
// EffectCatalogueUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Exercises effect admission without a scene or native Unity allocation.
//   Each gate is isolated so rejecting one requirement cannot mask another.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Check floors, prerequisite chains, hunter kinds, caps and copy lint.
// DEPENDENCIES:
//   - Core effects, Session Progression and NUnit only.
// USAGE NOTES:
//   Pure managed tests; these do not validate owner implementations of effects.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    public sealed class EffectCatalogueUtilityTests
    {
        private static ActiveEffect Active(string id, EffectKind kind = EffectKind.Curse, int count = 1)
            => new ActiveEffect(new EffectId(id), kind, count);

        [Test]
        public void AdmissionRequiresFloorPrerequisiteHunterAndAvailableStack()
        {
            var entry = new EffectCatalogueEntry("curse", EffectKind.Curse, FearAxis.Time, "Curse", "Removes time.",
                floor: 4, cap: 2, prerequisite: "prior", hunters: new[] { "echo", "weaver" });
            var eligible = new ActiveEffects(new[] { Active("prior"), Active("echo", EffectKind.Threat), Active("curse") });
            Assert.That(EffectCatalogueUtility.Eligible(entry, 3, eligible), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 4, eligible), Is.True);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 4, new ActiveEffects(new[] { Active("echo", EffectKind.Threat) })), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 4, new ActiveEffects(new[] { Active("prior") })), Is.False);
            Assert.That(EffectCatalogueUtility.Eligible(entry, 4, new ActiveEffects(new[] { Active("prior"), Active("echo") })), Is.False, "A curse with a hunter id is not a threat.");
            Assert.That(EffectCatalogueUtility.Eligible(entry, 4, new ActiveEffects(new[] { Active("prior"), Active("weaver", EffectKind.Threat) })), Is.True, "Hunter requirements are alternatives.");
            Assert.That(EffectCatalogueUtility.Eligible(entry, 4, new ActiveEffects(new[] { Active("prior"), Active("echo", EffectKind.Threat), Active("curse", count: 2) })), Is.False);
        }

        [Test]
        public void ThreatStackCapNeverLimitsDuplicateSelections()
        {
            var threat = new EffectCatalogueEntry("echo", EffectKind.Threat, FearAxis.Time, "Echo", "Adds an Echo.", cap: 1);
            Assert.That(EffectCatalogueUtility.Eligible(threat, 1,
                new ActiveEffects(new[] { Active("echo", EffectKind.Threat, 100) })), Is.True);
        }

        [TestCase("")] [TestCase("Bad luck awaits.")] [TestCase("Make it worse.")]
        public void CardWithoutMechanicalChangeFailsLint(string copy)
        {
            var entry = new EffectCatalogueEntry("bad", EffectKind.Curse, FearAxis.Stakes, "Bad", copy);
            Assert.That(EffectCatalogueUtility.CopyStatesChange(entry), Is.False);
        }

        [Test]
        public void NumericWorseningHasValidCopyAndRemainsAnAdvisoryWarning()
        {
            var entry = new EffectCatalogueEntry("faster", EffectKind.Curse, FearAxis.Time, "Faster",
                "Increases hunter speed.", numbersOnly: true);
            Assert.That(EffectCatalogueUtility.CopyStatesChange(entry), Is.True);
            Assert.That(entry.OnlyRaisesHunterNumbers, Is.True);
        }
    }
}
