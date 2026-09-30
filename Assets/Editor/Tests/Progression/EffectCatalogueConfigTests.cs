// ============================================================================
// EffectCatalogueConfigTests.cs
// ============================================================================
// PURPOSE:
//   Checks the approved catalogue inventory and the actual authored card copy.
//   Serialized assets are checked when present as well as fresh config defaults.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Include More Shrines in the exact approved upgrade inventory.
//   - Require owner-approved inventory and exclude retired Thin Skin and Bail Bond.
//   - Fail malformed data/copy; warn without failing on hunter-number-only changes.
// DEPENDENCIES:
//   - Core, Session Progression, NUnit and UnityEditor asset access.
// USAGE NOTES:
//   Edit Mode; no writes. Nothing??? is the spec's explicit hidden-copy exception.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EffectCatalogueConfigTests
    {
        [Test]
        public void DefaultsContainValidCompleteCatalogue()
        {
            var defaults = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try { Check(defaults); }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }

        [Test]
        public void AuthoredAssetContainsValidCompleteCatalogue()
        {
            var asset = AssetDatabase.LoadAssetAtPath<EffectCatalogueConfig>(
                "Assets/Resources/ScriptableObjects/Session/Progression/EffectCatalogueConfig.asset");
            Assert.That(asset, Is.Not.Null, "Run Worsen/Progression/Ensure Effect Catalogue before integration tests.");
            Check(asset);
        }

        private static void Check(EffectCatalogueConfig catalogue)
        {
            EffectCatalogueUtility.Validate(catalogue, Array.Empty<string>());
            Assert.That(catalogue.Entries.Any(entry => ProgressionRosterUtility.Retired(entry.Id)), Is.False);
            foreach (var threat in catalogue.Entries.Where(entry => entry.Kind == EffectKind.Threat))
                Assert.That(threat.AvailabilityRound, Is.EqualTo(ProgressionRosterUtility.FirstRound(threat.Id)), threat.Id);
            AssertIds(catalogue, EffectKind.Curse, "no-look-back silent-presence hidden-count darker-floors random-spawn shuffled-collapse nothing spent-pockets greedy-door short-grace faster-collapse slow-mend no-regen rough-start short-burst heavy-legs", true);
            AssertIds(catalogue, EffectKind.Upgrade, "stored-momentum soft-landing quiet-slide thick-skin wax-heart low-profile steady-hand second-bounce sweet-tooth glimpse latch echo-boots exit-sense blind-faith loud-heart gilded-greed longer-slide higher-jump sticky-fingers bigger-pockets cat-eyes field-kit lucky-reroll bargain-hunter keen-ears trail-reader sure-footing golden-sense stone-nerves web-cutter marked-doors afterglow spare-key mirror-skin ear-plugs extra-life golden-touch shop-reroll loyalty-card interest refund extra-pedestal more-shrines business-license speed-boost quick-start air-control fast-hands long-boost");
            AssertIds(catalogue, EffectKind.Consumable, "firecracker gauze smelling-salts wax-ward doorstop oil-flask glass-vial adrenaline");
            Assert.That(EffectCatalogueUtility.Find(catalogue, "wagered-haul"), Is.Null);
            Assert.That(EffectCatalogueUtility.Find(catalogue, "thin-skin"), Is.Null);
            Assert.That(EffectCatalogueUtility.Find(catalogue, "bail-bond"), Is.Null);
            Assert.That(EffectCatalogueUtility.Find(catalogue, "no-regen").AvailabilityRound, Is.EqualTo(12));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "hidden-count").CardCopy, Is.EqualTo("Hides the cake counter during a floor."));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "extra-life").CardCopy, Is.EqualTo(
                "Once per run, a catch revives you where you fell, with a brief collision grace and temporary damage immunity."));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "faster-collapse").CardCopy, Does.Contain("15% more golden cakes"));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "mimic-faithless-arrow").RequiredHunterIds, Is.EqualTo(new[] { "mimic" }));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "no-regen").PrerequisiteEffectId, Is.EqualTo("slow-mend"));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "heavy-legs").PrerequisiteEffectId, Is.EqualTo("short-burst"));
            foreach (var hunter in new[] { "echo", "weaver", "ticking" })
            {
                var curses = catalogue.Entries.Where(e => e.Kind == EffectKind.Curse && e.RequiredHunterIds.Contains(hunter)).ToArray();
                Assert.That(curses.Length, Is.EqualTo(hunter == "echo" ? 3 : 4));
                Assert.That(curses.All(e => e.Id.StartsWith(hunter + "-", StringComparison.Ordinal)), Is.True);
            }
            foreach (var hunter in new[] { "mannequin", "stare", "ram", "mimic", "skip", "blinder", "herald" })
            {
                var curses = catalogue.Entries.Where(e => e.Kind == EffectKind.Curse && e.RequiredHunterIds.Contains(hunter)).ToArray();
                Assert.That(curses.Length, Is.EqualTo(hunter == "herald" ? 5 : 4), hunter);
                foreach (var curse in curses)
                {
                    Assert.That(EffectCatalogueUtility.Eligible(curse, 100, default(ActiveEffects)), Is.False);
                    var held = new ActiveEffects(new[] { new ActiveEffect(new EffectId(hunter), EffectKind.Threat, 3),
                        new ActiveEffect(new EffectId(curse.Id), EffectKind.Curse, curse.StackCap) });
                    Assert.That(EffectCatalogueUtility.Eligible(curse, 100, held), Is.False, curse.Id);
                }
            }
            foreach (var entry in catalogue.Entries)
            {
                Assert.That(EffectCatalogueUtility.CopyStatesChange(entry), Is.True, entry.Id + ": card must state what is removed or changed.");
                if (entry.OnlyRaisesHunterNumbers)
                    Debug.LogWarning("Catalogue advisory: " + entry.Id + " only raises hunter numbers; prefer a removed safety or changed rule.");
            }
        }

        [Test]
        public void ValidationRejectsDuplicateIdsKindsAxesCapsAndBrokenPrerequisites()
        {
            var catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            var valid = new EffectCatalogueEntry("valid", EffectKind.Curse, FearAxis.Time, "Valid", "Removes time.");
            try
            {
                foreach (var entries in new[] {
                    new[] { valid, valid },
                    new[] { new EffectCatalogueEntry("bad", (EffectKind)99, FearAxis.Time, "Bad", "Removes time.") },
                    new[] { new EffectCatalogueEntry("bad", EffectKind.Curse, FearAxis.None, "Bad", "Removes time.") },
                    new[] { new EffectCatalogueEntry("bad", EffectKind.Curse, FearAxis.Time, "Bad", "Removes time.", cap: 0) },
                    new[] { new EffectCatalogueEntry("bad", EffectKind.Curse, FearAxis.Time, "Bad", "Removes time.", prerequisite: "missing") },
                    new[] { new EffectCatalogueEntry("bad", EffectKind.Curse, FearAxis.Time, "Bad", "Removes time.", prerequisite: "bad") } })
                {
                    typeof(EffectCatalogueConfig).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalogue, entries);
                    Assert.Throws<ArgumentException>(() => EffectCatalogueUtility.Validate(catalogue, Array.Empty<string>()));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(catalogue); }
        }

        private static void AssertIds(EffectCatalogueConfig catalogue, EffectKind kind, string ids, bool general = false)
        {
            Assert.That(catalogue.Entries.Where(e => e.Kind == kind && (!general || e.RequiredHunterIds.Count == 0)).Select(e => e.Id),
                Is.EquivalentTo(ids.Split(' ')));
        }
    }
}
