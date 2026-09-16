// ============================================================================
// FloorConfigGenerator.cs
// ============================================================================
// PURPOSE:
//   Rebuilds Floor configuration and its scene-owned Manager/Driver prefab deterministically.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Coordinator invokes under the exclusive Unity lease. Reuses config values and asset identities, touches only Floor-owned assets or the passed generated Floor child; never saves shared scenes.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Floor;

namespace Worsen.Editor.Floor
{
    public static class FloorConfigGenerator
    {
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Domain/Floor/FloorConfig.asset";
        public const string DriverConfigPath = "Assets/Resources/ScriptableObjects/Domain/Floor/FloorDriverConfig.asset";
        public const string PrefabPath = "Assets/Prefabs/Floor/Floor.prefab";
        public const string GeneratedRootName = "Generated Floor System";

        [MenuItem("Worsen/Floor/Build Floor Assets")]
        public static void BuildFloorAssets() => EnsureAssets();

        public static FloorConfig EnsureAssets()
        {
            RequireEditor();
            var config = EnsureAsset<FloorConfig>(ConfigPath);
            var driverConfig = EnsureAsset<FloorDriverConfig>(DriverConfigPath);
            EnsureFolder("Assets/Prefabs/Floor");
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Floor");
            try
            {
                var driver = root.GetComponent<FloorDriver>();
                if (driver == null) driver = root.AddComponent<FloorDriver>();
                var manager = root.GetComponent<FloorManager>();
                if (manager == null) manager = root.AddComponent<FloorManager>();
                Wire(driver, "_config", driverConfig); Wire(manager, "_driver", driver); Wire(manager, "_config", config);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Floor prefab save failed.");
                AssetDatabase.SaveAssetIfDirty(config); AssetDatabase.SaveAssetIfDirty(driverConfig);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            return config;
        }

        public static FloorManager Build(Transform parent)
        {
            RequireEditor();
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            EnsureAssets();
            var existing = parent.Find(GeneratedRootName);
            if (existing != null)
            {
                if (existing.GetComponent<FloorManager>() == null) throw new InvalidOperationException("Unrelated object occupies the generated Floor name.");
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            root.name = GeneratedRootName;
            return root.GetComponent<FloorManager>();
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
        private static void Wire(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing Floor serialized field: " + name);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void RequireEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Floor generation requires an idle Edit Mode editor.");
        }
    }
}
