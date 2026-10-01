// ============================================================================
// EffectCatalogueSetupTests.cs
// ============================================================================
// PURPOSE:
//   Verifies deterministic migration of obsolete catalogue rows to code defaults.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Progression.
// KEY RESPONSIBILITIES:
//   - Cover changed, retired, missing and duplicate ids plus repeat-run stability.
// DEPENDENCIES:
//   Core, Progression config/setup, NUnit and UnityEditor serialization.
// USAGE NOTES:
//   Coordinator-run Edit Mode; transient configs only, no authored asset writes.
// ============================================================================
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Editor.Progression;
using Worsen.Session.Progression;
namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EffectCatalogueSetupTests
    {
        [Test]
        public void RunRosterMigrationIsIdempotentAndLeavesCadenceUntouched()
        {
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                typeof(ProgressionConfig).GetField("_threats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config,
                    new[] { new ProgressionEntryConfig("watcher", "Old", "Old") });
                int cadence = config.ShopInterval;
                EffectCatalogueSetup.MigrateRunRoster(config);
                Assert.That(config.Threats.Count, Is.EqualTo(10));
                Assert.That(config.Threats.Any(entry => ProgressionRosterUtility.Retired(entry.Id)), Is.False);
                Assert.That(config.Curses.All(entry => entry.Traits == ProgressionTraits.None), Is.True);
                string first = EditorJsonUtility.ToJson(config);
                EffectCatalogueSetup.MigrateRunRoster(config);
                Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(first));
                Assert.That(config.ShopInterval, Is.EqualTo(cadence));
            }
            finally { Object.DestroyImmediate(config); }
        }
        [Test]
        public void MigrationReconcilesByIdRemovesRetiredAddsMissingAndIsIdempotent()
        {
            var catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            var defaults = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try
            {
                var old = new EffectCatalogueEntry("no-regen", EffectKind.Curse, FearAxis.Time, "Old", "Old", floor: 1);
                var custom = new EffectCatalogueEntry("owner-custom", EffectKind.Upgrade, FearAxis.Agency, "Custom", "Adds a test rule.");
                typeof(EffectCatalogueConfig).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalogue,
                    new[] { old, new EffectCatalogueEntry("thin-skin", EffectKind.Curse, FearAxis.Stakes, "Old", "Old"),
                        new EffectCatalogueEntry("bail-bond", EffectKind.Upgrade, FearAxis.Stakes, "Old", "Old"),
                        new EffectCatalogueEntry("watcher", EffectKind.Threat, FearAxis.Time, "Old", "Old"),
                        new EffectCatalogueEntry("rusher-long-stride", EffectKind.Curse, FearAxis.Time, "Old", "Old"),
                        new EffectCatalogueEntry("mannequin-broken-lights", EffectKind.Curse, FearAxis.Information, "Old", "Old"), old, custom });
                EffectCatalogueSetup.AppendMissingEntries(catalogue);
                Assert.That(catalogue.Entries.Select(e => e.Id).Distinct().Count(), Is.EqualTo(catalogue.Entries.Count));
                Assert.That(catalogue.Entries.Any(e => ProgressionRosterUtility.Retired(e.Id)), Is.False);
                Assert.That(catalogue.Entries.Single(e => e.Id == "owner-custom").CardCopy, Is.EqualTo(custom.CardCopy));
                foreach (var expected in defaults.Entries)
                {
                    var actual = catalogue.Entries.Single(e => e.Id == expected.Id);
                    Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)), expected.Id);
                }
                string once = EditorJsonUtility.ToJson(catalogue);
                EffectCatalogueSetup.AppendMissingEntries(catalogue);
                Assert.That(EditorJsonUtility.ToJson(catalogue), Is.EqualTo(once));
            }
            finally { Object.DestroyImmediate(catalogue); Object.DestroyImmediate(defaults); }
        }
    }
}
