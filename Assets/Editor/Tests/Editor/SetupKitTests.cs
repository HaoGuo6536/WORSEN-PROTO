// ============================================================================
// SetupKitTests.cs
// ============================================================================
// PURPOSE:
//   Compares migrated setup wiring with the pre-migration serialization operation
//   on the same in-memory object. This isolates helper parity from importer and
//   prefab instance IDs, and checks diagnostic and folder identity contracts.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Editor setup infrastructure.
// KEY RESPONSIBILITIES:
//   - Compare every migrated single-reference Wire adapter with legacy JSON output.
//   - Check array replacement, checked field diagnostics and path validation.
//   - Verify repeated folder creation preserves GUIDs and existing assets.
// DEPENDENCIES:
//   - Common SetupKit, existing editor adapters, Player/Procedural configs and NUnit.
// USAGE NOTES:
//   Serialization/folder cases require Unity. Only unique test folders and
//   temporary objects are modified; no production setup or scenes are saved.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Editor.Common;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Editor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SetupKitTests
    {
        [TestCase(typeof(Worsen.Editor.Floor.FloorConfigGenerator))]
        [TestCase(typeof(Worsen.Editor.HUD.HUDSetup))]
        [TestCase(typeof(Worsen.Editor.Results.ResultsSetup))]
        [TestCase(typeof(Worsen.Editor.Player.PlayerPrefabGenerator))]
        [TestCase(typeof(Worsen.Editor.Level.TagArenaLevelSetup))]
        [TestCase(typeof(Worsen.Editor.Level.FloorLoopLevelSetup))]
        [TestCase(typeof(Worsen.Editor.Scenes.TagArenaSceneSetup))]
        [TestCase(typeof(Worsen.Editor.Scenes.FloorLoopSceneSetup))]
        [TestCase(typeof(Worsen.Editor.Scenes.HorrorRunSceneSetup))]
        public void MigratedAdapterProducesIdenticalSerializedOutput(Type setup)
        {
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            var prefab = new GameObject("SetupKit parity reference");
            try
            {
                string initial = EditorJsonUtility.ToJson(profile);
                // Frozen legacy operation: the same target instance avoids unstable instance IDs.
                using (var legacy = new SerializedObject(profile))
                {
                    legacy.FindProperty("_prefab").objectReferenceValue = prefab;
                    legacy.ApplyModifiedPropertiesWithoutUndo();
                }
                string expected = EditorJsonUtility.ToJson(profile);
                EditorJsonUtility.FromJsonOverwrite(initial, profile);
                var method = setup.GetMethod("Wire", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { typeof(Object), typeof(string), typeof(Object) }, null);
                Assert.That(method, Is.Not.Null, setup.FullName);
                method.Invoke(null, new object[] { profile, "_prefab", prefab });
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(expected), setup.FullName);
                Assert.That(profile.Prefab, Is.SameAs(prefab));
                method.Invoke(null, new object[] { profile, "_prefab", prefab });
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(expected), "Repeated wiring changed output.");
                method.Invoke(null, new object[] { profile, "_prefab", null });
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(initial), "Optional null semantics changed.");
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void MissingAndWrongPropertyTypesFailWithPathAndTarget()
        {
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            try
            {
                var missing = Assert.Throws<InvalidOperationException>(() => SetupKit.Wire(profile, "_renamedField", (Object)null));
                Assert.That(missing.Message, Does.Contain(nameof(PlayerProfile)).And.Contain("_renamedField"));
                Assert.Throws<InvalidOperationException>(() => SetupKit.Wire(profile, "_sprintSpeed", (Object)null));
                Assert.Throws<InvalidOperationException>(() => SetupKit.Wire(profile, "_prefab", Array.Empty<Object>()));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void AccessibleNestedFieldUsesNameofAndMissingRelativePathFails()
        {
            var catalogue = ScriptableObject.CreateInstance<ProceduralRoomCatalogueData>();
            var reference = new GameObject("Nested reference");
            try
            {
                using (var data = new SerializedObject(catalogue))
                {
                    var entries = data.RequireProperty("_pieces"); entries.arraySize = 1;
                    var entry = entries.GetArrayElementAtIndex(0);
                    entry.RequireRelative(nameof(ProceduralRoomCatalogueData.KitAsset.Prefab)).objectReferenceValue = reference;
                    Assert.Throws<InvalidOperationException>(() => entry.RequireRelative("_missing"));
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            finally { Object.DestroyImmediate(catalogue); Object.DestroyImmediate(reference); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(3)]
        public void ReferenceArrayMatchesLegacyReplacement(int count)
        {
            var owner = new GameObject("Array wiring"); owner.SetActive(false);
            var level = owner.AddComponent<Worsen.Domain.Level.LevelDriver>();
            var marker = owner.AddComponent<Worsen.Domain.Level.LevelMarker>();
            try
            {
                var values = new Worsen.Domain.Level.LevelMarker[count];
                for (int index = 0; index < count; index++) values[index] = marker;
                using (var data = new SerializedObject(level))
                {
                    var field = data.FindProperty("_markers");
                    Assert.That(field, Is.Not.Null);
                    field.arraySize = count;
                    for (int index = 0; index < count; index++) field.GetArrayElementAtIndex(index).objectReferenceValue = marker;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                string expected = EditorJsonUtility.ToJson(level);
                foreach (Type setup in new[] { typeof(Worsen.Editor.Level.TagArenaLevelSetup), typeof(Worsen.Editor.Level.FloorLoopLevelSetup) })
                {
                    SetupKit.Wire(level, "_markers", Array.Empty<Object>());
                    var method = setup.GetMethod("Wire", BindingFlags.NonPublic | BindingFlags.Static, null,
                        new[] { typeof(Object), typeof(string), typeof(Worsen.Domain.Level.LevelMarker[]) }, null);
                    Assert.That(method, Is.Not.Null);
                    method.Invoke(null, new object[] { level, "_markers", values });
                    Assert.That(EditorJsonUtility.ToJson(level), Is.EqualTo(expected), setup.FullName);
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase("")] [TestCase("Packages/X")] [TestCase("Assets/../Outside")]
        [TestCase("Assets//X")] [TestCase("Assets\\X")] [TestCase("C:/Assets/X")]
        public void InvalidPathsFailBeforeAssetDatabaseAccess(string path) => Assert.Throws<ArgumentException>(() => SetupKit.ValidateAssetPath(path));

        [Test]
        public void FolderCreationRetainsIdentityAndSerializedAsset()
        {
            string root = "Assets/SetupKitTest-" + Guid.NewGuid().ToString("N");
            string path = root + "/Nested/Config.asset";
            try
            {
                SetupKit.EnsureParent(path);
                var config = ScriptableObject.CreateInstance<PlayerProfile>();
                AssetDatabase.CreateAsset(config, path);
                string folderGuid = AssetDatabase.AssetPathToGUID(root + "/Nested");
                string assetGuid = AssetDatabase.AssetPathToGUID(path);
                string json = EditorJsonUtility.ToJson(config);
                SetupKit.EnsureParent(path); SetupKit.EnsureFolder(root + "/Nested");
                Assert.That(AssetDatabase.AssetPathToGUID(root + "/Nested"), Is.EqualTo(folderGuid).And.Not.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(assetGuid).And.Not.Empty);
                Assert.That(EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<PlayerProfile>(path)), Is.EqualTo(json));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }
    }
}
