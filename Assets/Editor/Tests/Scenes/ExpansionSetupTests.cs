// ============================================================================
// ExpansionSetupTests.cs
// ============================================================================
// PURPOSE:
//   Verifies title-first build ordering and persisted expansion profile repair.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Scenes · setup verification.
// KEY RESPONSIBILITIES:
//   - Preserve explicit test scenes and make title-scene promotion idempotent.
//   - Preserve expansion profile GUIDs and designer tuning on repeated setup.
// DEPENDENCIES:
//   - Scene/Hunter editor setup, UnityEditor, Hunter data and NUnit.
// USAGE NOTES:
//   Profile cases require Unity and use only disposable assets. Ordering does not save settings.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
using Worsen.Editor.Hunter;
using Worsen.Editor.Scenes;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Scenes
{
    public sealed class ExpansionSetupTests
    {
        [Test] public void TitleFirstPreservesExplicitTestScenesAndIsIdempotent()
        {
            var existing = new[] { new EditorBuildSettingsScene("Assets/Scenes/TagArena.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false),
                new EditorBuildSettingsScene(HorrorRunSceneSetup.ScenePath, false),
                new EditorBuildSettingsScene("Assets/Scenes/FloorLoop.unity", true) };
            var once = HorrorRunSceneSetup.OrderedBuildScenes(existing);
            var twice = HorrorRunSceneSetup.OrderedBuildScenes(once);
            Assert.That(once[0].path, Is.EqualTo(HorrorRunSceneSetup.ScenePath)); Assert.That(once[0].enabled, Is.True);
            Assert.That(once.Skip(1), Is.EqualTo(new[] { existing[0], existing[1], existing[3] }));
            Assert.That(twice.Select(x => (x.path, x.enabled)), Is.EqualTo(once.Select(x => (x.path, x.enabled))));
        }
        [Test] public void PlayableRosterHasExactlyTheTenOwnerSelectedKeys()
        {
            Assert.That(ExpansionHunterProfileSetup.Names, Is.EquivalentTo(new[]
                { "Echo", "Weaver", "Ticking", "Ram", "Skip", "Mimic", "Blinder", "Herald", "Mannequin", "Stare" }));
        }
        [TestCase("Ram")] [TestCase("Skip")] [TestCase("Mimic")]
        [TestCase("Blinder")] [TestCase("Herald")] [TestCase("Mannequin")] [TestCase("Stare")]
        public void ExpansionProfilesPersistAndPreserveTuning(string name)
        {
            string directory = "Assets/WpIProfileTest-" + Guid.NewGuid().ToString("N");
            var actor = new GameObject("Temporary expansion actor"); actor.SetActive(false);
            try
            {
                actor.AddComponent<HunterManager>();
                AssetDatabase.CreateFolder("Assets", directory.Substring("Assets/".Length));
                var prefab = PrefabUtility.SaveAsPrefabAsset(actor, directory + "/Actor.prefab");
                HunterProfile Build() => name == "Ram" || name == "Skip" || name == "Mimic" ? RosterBProfileSetup.BuildAssets(name, prefab, directory) :
                    name == "Blinder" || name == "Herald" ? BlinderHeraldProfileSetup.BuildAssets(directory + "/" + name, prefab, name == "Herald") :
                    ObservedHunterProfileSetup.BuildAssets(directory + "/" + name, prefab, name == "Stare");
                var first = Build(); string path = AssetDatabase.GetAssetPath(first); string guid = AssetDatabase.AssetPathToGUID(path);
                var data = new SerializedObject(first); data.FindProperty("_chaseSpeedMultiplier").floatValue = .91f; data.ApplyModifiedPropertiesWithoutUndo();
                var second = Build();
                Assert.That(second, Is.SameAs(first)); Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(second.ChaseSpeedMultiplier, Is.EqualTo(.91f)); Assert.That(second.ArchetypeKey, Is.EqualTo(name.ToLowerInvariant()));
                Assert.That(AssetDatabase.Contains(second.ArchetypeRules), Is.True); Assert.That(second.Prefab, Is.SameAs(prefab));
            }
            finally { Object.DestroyImmediate(actor); AssetDatabase.DeleteAsset(directory); }
        }
    }
}
