// ============================================================================
// TickingProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Builds reusable clockwork and key placeholders plus the Ticking profile.
//   Repeat runs preserve asset identities and designer tuning while repairing
//   owned config references. No scene or existing roster is changed.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Author mirrored Resources configs, the four-part brief and one threshold habit.
// DEPENDENCIES:
//   - Hunter configs, UnityEditor asset APIs and primitive placeholder geometry.
// USAGE NOTES:
//   Coordinator-only idle Edit Mode operation, not an automatic import hook.
//   Resources profile: ScriptableObjects/Domain/Hunter/Archetypes/Ticking/TickingProfile.
//   Other assets and prefab paths are the constants below. Existing prefabs are reused.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ticking;
namespace Worsen.Editor.Hunter
{
    public static class TickingProfileSetup
    {
        public const string Directory = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Ticking";
        public const string ProfilePath = Directory + "/TickingProfile.asset";
        public const string ConfigPath = Directory + "/TickingConfig.asset";
        public const string DriverPath = Directory + "/TickingDriverConfig.asset";
        public const string MotorPath = Directory + "/TickingMotorDriverConfig.asset";
        public const string PrefabDirectory = "Assets/Resources/Prefabs/Domain/Hunter/Archetypes/Ticking";
        public const string HunterPrefabPath = PrefabDirectory + "/TickingPlaceholder.prefab";
        public const string KeyPrefabPath = PrefabDirectory + "/TickingKey.prefab";
        [MenuItem("Worsen/Hunter/Build Ticking Profile")]
        public static void Build()
        {
            RequireIdle(); EnsureFolder(PrefabDirectory);
            BuildAssets(Directory, EnsurePlaceholder(HunterPrefabPath, false), EnsurePlaceholder(KeyPrefabPath, true));
        }
        public static HunterProfile BuildAssets(string directory, GameObject hunterPrefab, GameObject keyPrefab)
        {
            RequireIdle();
            if (hunterPrefab == null || hunterPrefab.GetComponent<HunterManager>() == null || keyPrefab == null)
                throw new ArgumentException("Ticking setup requires a HunterManager placeholder and a key placeholder.");
            EnsureFolder(directory);
            bool fresh = AssetDatabase.LoadAssetAtPath<HunterProfile>(directory + "/TickingProfile.asset") == null;
            var profile = Ensure<HunterProfile>(directory + "/TickingProfile.asset");
            var rules = Ensure<TickingConfig>(directory + "/TickingConfig.asset");
            var driver = Ensure<TickingDriverConfig>(directory + "/TickingDriverConfig.asset");
            var motor = Ensure<HunterMotorDriverConfig>(directory + "/TickingMotorDriverConfig.asset");
            var so = new SerializedObject(profile);
            so.FindProperty("_archetypeKey").stringValue = "ticking";
            so.FindProperty("_prefab").objectReferenceValue = hunterPrefab;
            so.FindProperty("_archetypeRules").objectReferenceValue = rules;
            so.FindProperty("_motorOverride").objectReferenceValue = motor;
            if (fresh)
            {
                // Four tunables: inertia, commitment, speed ratio, time+distance loss.
                so.FindProperty("_acceleration").floatValue = 20f;
                so.FindProperty("_turnRate").floatValue = 240f;
                so.FindProperty("_actionCommitmentSeconds").floatValue = .5f;
                so.FindProperty("_chaseSpeedMultiplier").floatValue = 1.12f;
                so.FindProperty("_lossSeconds").floatValue = 2.5f;
                so.FindProperty("_lossDistance").floatValue = 14f;
                so.FindProperty("_emergenceBias").boolValue = false;
                var habits = so.FindProperty("_habits"); habits.arraySize = 1;
                var habit = habits.GetArrayElementAtIndex(0);
                habit.FindPropertyRelative("_kind").enumValueIndex = (int)HunterHabitKind.ThresholdPause;
                habit.FindPropertyRelative("_enabled").boolValue = true;
                habit.FindPropertyRelative("_pauseSeconds").floatValue = .4f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var ruleObject = new SerializedObject(rules); ruleObject.FindProperty("_driverConfig").objectReferenceValue = driver;
            ruleObject.ApplyModifiedPropertiesWithoutUndo();
            var driverObject = new SerializedObject(driver); driverObject.FindProperty("_keyPrefab").objectReferenceValue = keyPrefab;
            driverObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(rules);
            AssetDatabase.SaveAssetIfDirty(driver); AssetDatabase.SaveAssetIfDirty(motor); return profile;
        }
        private static GameObject EnsurePlaceholder(string path, bool key)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject(key ? "Ticking Key" : "Ticking Clockwork");
            try
            {
                if (key)
                {
                    Part(root, PrimitiveType.Sphere, "Bow", new Vector3(0, .55f, 0), new Vector3(.3f, .3f, .12f));
                    Part(root, PrimitiveType.Cube, "Stem", new Vector3(0, .3f, 0), new Vector3(.08f, .4f, .08f));
                    Part(root, PrimitiveType.Cube, "Tooth", new Vector3(.08f, .18f, 0), new Vector3(.2f, .08f, .08f));
                }
                else
                {
                    root.AddComponent<HunterManager>();
                    var capsule = root.GetComponent<CapsuleCollider>(); capsule.radius = .4f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
                    Part(root, PrimitiveType.Sphere, "Clock", new Vector3(0, 1f, 0), new Vector3(.9f, 1.2f, .45f));
                    Part(root, PrimitiveType.Cube, "Winder", new Vector3(0, 1.65f, 0), new Vector3(.6f, .12f, .12f));
                    Part(root, PrimitiveType.Cube, "Left leg", new Vector3(-.22f, .25f, 0), new Vector3(.12f, .5f, .12f));
                    Part(root, PrimitiveType.Cube, "Right leg", new Vector3(.22f, .25f, 0), new Vector3(.12f, .5f, .12f));
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Part(GameObject root, PrimitiveType shape, string name, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(shape); part.name = name; part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Ticking setup requires idle Edit Mode and coordinator admission.");
        }
        private static T Ensure<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            { string next = parent + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]); parent = next; }
        }
    }
}
