// ============================================================================
// EffectCatalogueConfigTests.cs
// ============================================================================
// PURPOSE:
//   Checks the approved catalogue inventory and the actual authored card copy.
//   Serialized assets are checked when present as well as fresh config defaults.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Require all approved general curses, upgrades, consumables and first hunter curses.
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
            EffectCatalogueUtility.Validate(catalogue, new[] { "watcher", "rusher", "lurker", "hexer", "thorncaller" });
            AssertIds(catalogue, EffectKind.Curse, "no-look-back silent-presence hidden-count darker-floors random-spawn shuffled-collapse nothing thin-skin spent-pockets greedy-door short-grace faster-collapse slow-mend no-regen rough-start short-burst heavy-legs", true);
            AssertIds(catalogue, EffectKind.Upgrade, "stored-momentum soft-landing quiet-slide thick-skin wax-heart low-profile steady-hand second-bounce sweet-tooth glimpse latch echo-boots exit-sense blind-faith loud-heart gilded-greed longer-slide higher-jump sticky-fingers bigger-pockets cat-eyes field-kit lucky-reroll bargain-hunter keen-ears trail-reader sure-footing golden-sense stone-nerves web-cutter marked-doors afterglow spare-key mirror-skin ear-plugs bail-bond extra-life golden-touch shop-reroll loyalty-card interest refund extra-pedestal business-license speed-boost quick-start air-control fast-hands long-boost");
            AssertIds(catalogue, EffectKind.Consumable, "firecracker gauze smelling-salts wax-ward doorstop oil-flask glass-vial adrenaline");
            Assert.That(EffectCatalogueUtility.Find(catalogue, "wagered-haul"), Is.Null);
            Assert.That(EffectCatalogueUtility.Find(catalogue, "no-regen").PrerequisiteEffectId, Is.EqualTo("slow-mend"));
            Assert.That(EffectCatalogueUtility.Find(catalogue, "heavy-legs").PrerequisiteEffectId, Is.EqualTo("short-burst"));
            foreach (var hunter in new[] { "echo", "weaver", "ticking" })
            {
                var curses = catalogue.Entries.Where(e => e.Kind == EffectKind.Curse && e.RequiredHunterIds.Contains(hunter)).ToArray();
                Assert.That(curses.Length, Is.EqualTo(hunter == "echo" ? 3 : 4));
                Assert.That(curses.All(e => e.Id.StartsWith(hunter + "-", StringComparison.Ordinal)), Is.True);
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
