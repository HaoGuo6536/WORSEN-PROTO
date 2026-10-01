// ============================================================================
// PlayerPrefabGenerator.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the Player prefab and mirrored archetype/config wiring without replacing tuning values.
//   Generated blocky arms replace the optional capsule hands when their FBX exists.
//   Missing art retains hidden capsules; rebuilding replaces limb children instead
//   of accumulating meshes, bones or colliders.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Preserve existing Player tuning and rebuild idempotent limb references.
//   - Split the imported Hold rest pose into independent skinned arm roots.
// DEPENDENCIES:
//   - Common SetupKit owns checked serialized wiring and asset-folder creation.
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
//   - BlockyCharacterSetup supplies the configured Generic first-person model.
// USAGE NOTES:
//   Run only under the coordinator Unity lease. Reuses asset GUIDs and existing prefab objects; no shared scene or project settings are changed.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using System.Linq;
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
            GameObject arms = BlockyCharacterSetup.LoadArmsIfPresent();
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
                PlayerLimbStandIn limbs = RebuildLimbs(visuals, arms);
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

        public static PlayerLimbStandIn RebuildLimbs(GameObject visuals, GameObject arms)
        {
            foreach (Transform child in visuals.transform.Cast<Transform>().ToArray())
                if (new[] { "Left Hand", "Right Hand", "Left Foot", "Right Foot", "Blocky Arms" }.Contains(child.name))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            PlayerLimbStandIn limbs = GetOrAdd<PlayerLimbStandIn>(visuals);
            GameObject left, right;
            if (arms == null)
            {
                left = Limb(visuals.transform, "Left Hand", new Vector3(0.13f, 0.22f, 0.13f));
                right = Limb(visuals.transform, "Right Hand", new Vector3(0.13f, 0.22f, 0.13f));
            }
            else
            {
                // Keep the exported skin; no BakeMesh/static approximation. The player
                // uses the authored Hold rest pose, not an Animator that could overwrite
                // camera placement. Original Hold/Sway clip paths remain on the source FBX.
                GameObject model = UnityEngine.Object.Instantiate(arms, visuals.transform, false);
                model.name = "Blocky Arms";
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
                    UnityEngine.Object.DestroyImmediate(animator);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);
                left = ArmRoot(model, visuals.transform, "Left");
                right = ArmRoot(model, visuals.transform, "Right");
                UnityEngine.Object.DestroyImmediate(model);
            }
            Wire(limbs, "_leftHand", left);
            Wire(limbs, "_rightHand", right);
            Wire(limbs, "_leftFoot", Limb(visuals.transform, "Left Foot", new Vector3(0.14f, 0.14f, 0.3f)));
            Wire(limbs, "_rightFoot", Limb(visuals.transform, "Right Foot", new Vector3(0.14f, 0.14f, 0.3f)));
            var serialized = new SerializedObject(limbs);
            serialized.FindProperty("_showHands").boolValue = arms != null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return limbs;
        }

        private static GameObject ArmRoot(GameObject model, Transform parent, string side)
        {
            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            Transform shoulder = bones.Single(t => t.name == side + "Shoulder");
            Transform hand = bones.Single(t => t.name == side + "Hand");
            SkinnedMeshRenderer renderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(r => r.name == side + "Arm");
            GameObject root = new GameObject(side + " Hand");
            root.transform.SetParent(parent, false);
            root.transform.position = hand.position;
            shoulder.SetParent(root.transform, true);
            renderer.transform.SetParent(root.transform, true);
            // FBX can include zero-weight foreign bones. Keep those slots valid after
            // removing the common Root; weighted slots retain their original transforms.
            renderer.bones = renderer.bones.Select(b => b != null && b.IsChildOf(shoulder) ? b : shoulder).ToArray();
            renderer.rootBone = shoulder;
            renderer.updateWhenOffscreen = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.SetActive(false);
            return root;
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
            => Worsen.Editor.Common.SetupKit.EnsureFolder(path);
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
            => Worsen.Editor.Common.SetupKit.Wire(target, name, value);
    }
}