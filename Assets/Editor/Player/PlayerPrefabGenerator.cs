// ============================================================================
// PlayerPrefabGenerator.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the Player prefab and mirrored archetype/config wiring without replacing tuning values.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Run only under the coordinator Unity lease. Reuses asset GUIDs and existing prefab objects; no shared scene or project settings are changed.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Player;

namespace Worsen.Editor.Player
{
    public static class PlayerPrefabGenerator
    {
        public const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
        public const string ProfilePath = "Assets/Resources/ScriptableObjects/Domain/Player/PlayerProfile.asset";
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Domain/Player/PlayerMoverDriverConfig.asset";

        [MenuItem("Worsen/Player/Build Player Assets")]
        public static void BuildPlayerAssets() { EnsureAssets(); }

        public static PlayerProfile EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Player assets require an idle Edit Mode editor.");
            PlayerProfile profile = EnsureAsset<PlayerProfile>(ProfilePath);
            PlayerMoverDriverConfig config = EnsureAsset<PlayerMoverDriverConfig>(ConfigPath);
            EnsureFolder("Assets/Prefabs/Player");
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Player");
            try
            {
                PlayerDriver driver = GetOrAdd<PlayerDriver>(root);
                PlayerManager manager = GetOrAdd<PlayerManager>(root);
                CapsuleCollider capsule = GetOrAdd<CapsuleCollider>(root);
                Rigidbody body = GetOrAdd<Rigidbody>(root);
                body.isKinematic = true;
                body.useGravity = false;
                capsule.height = config.Height;
                capsule.radius = config.Radius;
                capsule.center = Vector3.up * config.Height * 0.5f;
                GameObject visuals = Child(root.transform, "Player Visuals");
                PlayerLimbStandIn limbs = GetOrAdd<PlayerLimbStandIn>(visuals);
                Wire(limbs, "_leftHand", Limb(visuals.transform, "Left Hand", new Vector3(0.13f, 0.22f, 0.13f)));
                Wire(limbs, "_rightHand", Limb(visuals.transform, "Right Hand", new Vector3(0.13f, 0.22f, 0.13f)));
                Wire(limbs, "_leftFoot", Limb(visuals.transform, "Left Foot", new Vector3(0.14f, 0.14f, 0.3f)));
                Wire(limbs, "_rightFoot", Limb(visuals.transform, "Right Foot", new Vector3(0.14f, 0.14f, 0.3f)));
                Wire(driver, "_config", config);
                Wire(driver, "_capsule", capsule);
                Wire(driver, "_body", body);
                Wire(driver, "_visualRoot", visuals.transform);
                Wire(driver, "_limbs", limbs);
                Wire(manager, "_driver", driver);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Player prefab could not be saved.");
                Wire(profile, "_prefab", prefab);
                AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.SaveAssetIfDirty(config);
                return profile;
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string parent = parts[0];
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
        private static GameObject Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
        private static GameObject Limb(Transform parent, string name, Vector3 scale)
        {
            Transform existing = parent.Find(name);
            GameObject limb = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            limb.name = name;
            limb.transform.SetParent(parent, false);
            limb.transform.localScale = scale;
            Collider collider = limb.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            limb.SetActive(false);
            return limb;
        }
        private static void Wire(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException(target.name + " lacks " + name);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}