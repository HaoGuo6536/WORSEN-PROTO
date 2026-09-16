// ============================================================================
// HunterPrefabGenerator.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the hunter body, manager and driver wiring from deterministic source.
//   It preserves existing archetype tuning and asset identities while restoring
//   the minimal visible capsule hunter needed for the live tag loop.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Ensure mirrored profile/config assets and the owned hunter prefab.
// DEPENDENCIES:
//   - Hunter runtime types and UnityEditor APIs; no shared scene ownership.
// USAGE NOTES:
//   Run in an idle editor under the coordinator's Unity lease. Existing .meta
//   identities and profile values are reused, and unrelated assets are never saved.
//   The explicit-path overload supports isolated asset generation and Editor tests.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
namespace Worsen.Editor.Hunter
{
    public static class HunterPrefabGenerator
    {
        public const string PrefabPath = "Assets/Prefabs/Hunter/Hunter.prefab";
        public const string ProfilePath = "Assets/Resources/ScriptableObjects/Domain/Hunter/HunterProfile.asset";
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Domain/Hunter/HunterMotorDriverConfig.asset";
        [MenuItem("Worsen/Hunter/Build Hunter Assets")]
        public static void BuildHunterAssets() { EnsureAssets(); }
        public static HunterProfile EnsureAssets()
            => EnsureAssets(PrefabPath, ProfilePath, ConfigPath);
        public static HunterProfile EnsureAssets(string prefabPath, string profilePath, string configPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Hunter assets require an idle Edit Mode editor.");
            HunterProfile profile = EnsureAsset<HunterProfile>(profilePath);
            HunterMotorDriverConfig config = EnsureAsset<HunterMotorDriverConfig>(configPath);
            EnsureFolder(prefabPath.Substring(0, prefabPath.LastIndexOf('/')));
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject("Hunter");
            try
            {
                CapsuleCollider capsule = GetOrAdd<CapsuleCollider>(root);
                Rigidbody body = GetOrAdd<Rigidbody>(root);
                HunterDriver driver = GetOrAdd<HunterDriver>(root);
                HunterManager manager = GetOrAdd<HunterManager>(root);
                body.isKinematic = true; body.useGravity = false;
                capsule.height = config.Height; capsule.radius = config.Radius;
                capsule.center = Vector3.up * (config.Height * 0.5f);
                Transform existing = root.transform.Find("Hunter Body");
                GameObject visual = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Hunter Body"; visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.up * (config.Height * 0.5f);
                visual.transform.localScale = new Vector3(config.Radius * 2f, config.Height * 0.5f, config.Radius * 2f);
                Collider visualCollider = visual.GetComponent<Collider>();
                if (visualCollider != null) UnityEngine.Object.DestroyImmediate(visualCollider);
                Wire(driver, "_config", config); Wire(driver, "_capsule", capsule); Wire(driver, "_body", body);
                Wire(manager, "_driver", driver);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null) throw new InvalidOperationException("Hunter prefab could not be saved.");
                Wire(profile, "_prefab", prefab);
                AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(config);
                return profile;
            }
            finally
            { if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
        }
        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }
        private static void Wire(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException(target.name + " lacks " + name);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
