// ============================================================================
// BlinderHeraldProfileSetupTests.cs
// ============================================================================
// PURPOSE:
//   Verifies profile setup preserves identities and tuning while repairing bindings.
//   Temporary assets use a unique owned folder and an existing-shape placeholder;
//   production scenes, prefabs and roster settings are never saved by the fixture.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Check both four-part briefs, gates, habit sets and independent module configs.
// DEPENDENCIES:
//   - Hunter setup/config types, UnityEditor asset APIs and NUnit.
// USAGE NOTES:
//   Coordinator-only Edit Mode test; deletes only its own temporary asset folder.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Herald;
using Worsen.Editor.Hunter;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class BlinderHeraldProfileSetupTests
    {
        [TestCase(false)] [TestCase(true)]
        public void RebuildRepairsReferencesWithoutReplacingAssetsOrDesignerTuning(bool herald)
        {
            string folder = "Assets/Resources/ScriptableObjects/Domain/Hunter/Plan017Fixture" + Guid.NewGuid().ToString("N");
            var root = new GameObject("PLAN017 placeholder fixture"); root.SetActive(false); root.AddComponent<HunterManager>();
            try
            {
                var first = BlinderHeraldProfileSetup.BuildAssets(folder, root, herald);
                string path = AssetDatabase.GetAssetPath(first), guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(first.ArchetypeKey, Is.EqualTo(herald ? "herald" : "blinder"));
                Assert.That(first.MinimumDepth, Is.EqualTo(herald ? 6 : 5));
                Assert.That(first.Acceleration, Is.EqualTo(herald ? 14f : 16f)); Assert.That(first.TurnRate, Is.EqualTo(200f));
                Assert.That(first.ActionCommitmentSeconds, Is.EqualTo(.6f));
                Assert.That(first.ChaseSpeedMultiplier, Is.EqualTo(herald ? 1.02f : 1.05f));
                Assert.That(first.LossSeconds, Is.EqualTo(2.5f)); Assert.That(first.LossDistance, Is.EqualTo(14f));
                Assert.That(first.Habits.Count, Is.EqualTo(3)); Assert.That(first.MutationPool.Count, Is.EqualTo(1));
                Assert.That(first.MotorOverride.NavigationAreaMask, Is.EqualTo(1));
                var rules = first.ArchetypeRules;
                if (herald) Assert.That(rules, Is.TypeOf<HeraldConfig>());
                else Assert.That(((BlinderConfig)rules).SweepConfig, Is.Not.Null);
                var so = new SerializedObject(first); so.FindProperty("_acceleration").floatValue = 17f;
                so.FindProperty("_minimumDepth").intValue = 9; so.FindProperty("_archetypeRules").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(first);
                var second = BlinderHeraldProfileSetup.BuildAssets(folder, root, herald);
                Assert.That(second, Is.SameAs(first)); Assert.That(second.ArchetypeRules, Is.SameAs(rules));
                Assert.That(second.Acceleration, Is.EqualTo(17f)); Assert.That(second.MinimumDepth, Is.EqualTo(9));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }
    }
}
