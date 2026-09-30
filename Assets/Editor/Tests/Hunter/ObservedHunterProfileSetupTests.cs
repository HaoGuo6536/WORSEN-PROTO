// ============================================================================
// ObservedHunterProfileSetupTests.cs
// ============================================================================
// PURPOSE:
//   Verifies deterministic setup for the two camera-dependent hunter profiles.
//   Rebuilding references must not erase designer tuning or replace asset ids.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Check rule registration, placeholder reuse, gates and idempotent authoring.
// DEPENDENCIES:
//   - Hunter setup/configs, UnityEditor asset APIs and NUnit.
// USAGE NOTES:
//   Coordinator runs in Edit Mode; temporary test assets are always removed.
// ============================================================================
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
using Worsen.Editor.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ObservedHunterProfileSetupTests
    {
        [TestCase(false, "mannequin", 4)]
        [TestCase(true, "stare", 6)]
        public void RebuildPreservesTuningAndIdentity(bool stare, string key, int depth)
        {
            string directory = "Assets/ObservedHunterSetupTest-" + System.Guid.NewGuid().ToString("N");
            var placeholder = new GameObject("Observed hunter test placeholder");
            try
            {
                placeholder.AddComponent<HunterManager>();
                AssetDatabase.CreateFolder("Assets", directory.Substring("Assets/".Length));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(placeholder, directory + "/Placeholder.prefab");
                HunterProfile first = ObservedHunterProfileSetup.BuildAssets(directory, prefab, stare);
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first));
                var so = new SerializedObject(first); so.FindProperty("_chaseSpeedMultiplier").floatValue = .91f; so.ApplyModifiedPropertiesWithoutUndo();
                HunterProfile second = ObservedHunterProfileSetup.BuildAssets(directory, prefab, stare);
                Assert.That(second, Is.SameAs(first)); Assert.That(second.ArchetypeKey, Is.EqualTo(key));
                Assert.That(second.MinimumDepth, Is.EqualTo(depth)); Assert.That(second.ArchetypeRules, Is.Not.Null);
                Assert.That(second.ChaseSpeedMultiplier, Is.EqualTo(.91f)); Assert.That(second.Habits.Count, Is.GreaterThan(0));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second)), Is.EqualTo(guid));
            }
            finally { Object.DestroyImmediate(placeholder); AssetDatabase.DeleteAsset(directory); }
        }
    }
}
