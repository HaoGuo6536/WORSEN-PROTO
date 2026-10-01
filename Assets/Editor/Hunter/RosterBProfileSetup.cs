// ============================================================================
// RosterBProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Builds Ram, Skip and Mimic profiles using a persistent Hunter placeholder prefab.
//   Repeated setup repairs missing references without overwriting designer tuning
//   or replacing a body already bound by the roster visual setup.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Author four-part briefs, provisional depth gates and mirrored rule assets.
//   - Supply a Mimic-only cake motor and configure its saved body collision.
// DEPENDENCIES:
//   - Hunter configs, Core habit kinds and UnityEditor asset APIs only.
// USAGE NOTES:
//   Coordinator runs under its import lease in idle Edit Mode. No scenes, prefabs,
//   catalogue or roster selections are modified; registration is a separate hand-off.
//   Ram waits at thresholds and stamps before rushing; Skip marks reused routes;
//   Mimic stays perfectly still as a cake and remains spent after one bite.
//   Visual setup applies the cake box to its own loaded prefab, never the placeholder.
//   Win presentation uses the normal accepted-catch path and archetype Won facts.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ram;
using Worsen.Domain.Hunter.Archetypes.Skip;
using Worsen.Domain.Hunter.Archetypes.Mimic;
namespace Worsen.Editor.Hunter
{
    public static class RosterBProfileSetup
    {
        public const string Root = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/";
        public static Vector3 MimicCollisionSize => new Vector3(.266f, .289f, .352f);
        [MenuItem("Worsen/Hunter/Build Ram Profile")] public static void BuildRam() { BuildAssets("Ram", Placeholder()); }
        [MenuItem("Worsen/Hunter/Build Skip Profile")] public static void BuildSkip() { BuildAssets("Skip", Placeholder()); }
        [MenuItem("Worsen/Hunter/Build Mimic Profile")] public static void BuildMimic() { BuildAssets("Mimic", Placeholder()); }
        public static HunterProfile BuildAssets(string name, GameObject placeholder, string root = Root)
        {
            RequireIdle();
            if (name != "Ram" && name != "Skip" && name != "Mimic") throw new ArgumentException("Unknown roster B archetype.");
            if (placeholder == null || !EditorUtility.IsPersistent(placeholder) || !PrefabUtility.IsPartOfPrefabAsset(placeholder) ||
                placeholder.GetComponent<HunterManager>() == null) throw new ArgumentException("Placeholder requires a persistent prefab asset with HunterManager.", nameof(placeholder));
            string directory = root.TrimEnd('/') + "/" + name; EnsureFolder(directory);
            string path = directory + "/" + name + "Profile.asset";
            bool fresh = AssetDatabase.LoadAssetAtPath<HunterProfile>(path) == null;
            var profile = Ensure<HunterProfile>(path);
            HunterArchetypeConfig rules = name == "Ram" ? (HunterArchetypeConfig)Ensure<RamConfig>(directory + "/RamConfig.asset") :
                name == "Skip" ? (HunterArchetypeConfig)Ensure<SkipConfig>(directory + "/SkipConfig.asset") : Ensure<MimicConfig>(directory + "/MimicConfig.asset");
            var so = new SerializedObject(profile);
            so.FindProperty("_archetypeKey").stringValue = name.ToLowerInvariant();
            if (profile.Prefab == null) so.FindProperty("_prefab").objectReferenceValue = placeholder;
            so.FindProperty("_archetypeRules").objectReferenceValue = rules;
            if (name == "Mimic")
            {
                string motorPath = directory + "/MimicMotorDriverConfig.asset";
                bool newMotor = AssetDatabase.LoadAssetAtPath<HunterMotorDriverConfig>(motorPath) == null;
                var motor = Ensure<HunterMotorDriverConfig>(motorPath);
                var data = new SerializedObject(motor);
                // The motor still sweeps a capsule. Keep that unused stationary probe
                // inside the cake's height/width; the physical solid is the exact box.
                data.FindProperty("_radius").floatValue = MimicCollisionSize.x * .5f;
                data.FindProperty("_height").floatValue = MimicCollisionSize.y;
                if (newMotor)
                {
                    data.FindProperty("_skinWidth").floatValue = .005f;
                    data.FindProperty("_eyeHeight").floatValue = MimicCollisionSize.y * .5f;
                    data.FindProperty("_gravity").floatValue = 0f;
                    data.FindProperty("_stepHeight").floatValue = 0f;
                }
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(motor);
                so.FindProperty("_motorOverride").objectReferenceValue = motor;
            }
            if (fresh)
            {
                bool ram = name == "Ram", skip = name == "Skip";
                so.FindProperty("_minimumDepth").intValue = ram ? 4 : skip ? 6 : 5;
                so.FindProperty("_acceleration").floatValue = ram ? 8f : skip ? 4f : 0f;
                so.FindProperty("_turnRate").floatValue = ram || skip ? 90f : 0f;
                so.FindProperty("_actionCommitmentSeconds").floatValue = ram ? 1f : skip ? .6f : 1.2f;
                so.FindProperty("_chaseSpeedMultiplier").floatValue = ram ? 1.05f : skip ? .2f : 0f;
                so.FindProperty("_lossSeconds").floatValue = ram ? 2.5f : 0f;
                so.FindProperty("_lossDistance").floatValue = ram ? 14f : 0f;
                so.FindProperty("_patrolSpeed").floatValue = ram ? 2.5f : skip ? 1.5f : 0f;
                so.FindProperty("_investigateSpeed").floatValue = ram ? 2.5f : skip ? 1.5f : 0f;
                so.FindProperty("_sensorIntervalTicks").intValue = 1;
                so.FindProperty("_emergenceBias").boolValue = ram;
                so.FindProperty("_screamOnDetection").boolValue = false;
                // Silent annoyances must not inherit an audible cake-reaction habit.
                var habits = so.FindProperty("_habits"); habits.arraySize = ram ? 3 : skip ? 1 : 0;
                for (int i = 0; i < habits.arraySize; i++)
                {
                    var habit = habits.GetArrayElementAtIndex(i);
                    habit.FindPropertyRelative("_kind").enumValueIndex = i;
                    habit.FindPropertyRelative("_enabled").boolValue = true;
                    habit.FindPropertyRelative("_pauseSeconds").floatValue = .4f;
                    habit.FindPropertyRelative("_radius").floatValue = 15f;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(rules); return profile;
        }
        private static GameObject Placeholder()
        {
            RequireIdle(); var profile = AssetDatabase.LoadAssetAtPath<HunterProfile>(EchoProfileSetup.PlaceholderProfilePath);
            if (profile == null || profile.Prefab == null) throw new InvalidOperationException("Build the existing horror roster first.");
            return profile.Prefab;
        }
        public static void ConfigureMimicCollision(GameObject root, Vector3 center)
        {
            if (root == null || EditorUtility.IsPersistent(root))
                throw new ArgumentException("Mimic collision requires isolated loaded prefab contents.", nameof(root));
            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null) throw new InvalidOperationException("Mimic motor requires its capsule component.");
            var box = root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();
            box.size = MimicCollisionSize; box.center = center; box.enabled = true; box.isTrigger = false;
            box.sharedMaterial = capsule.sharedMaterial; box.contactOffset = capsule.contactOffset;
            box.includeLayers = capsule.includeLayers; box.excludeLayers = capsule.excludeLayers;
            box.layerOverridePriority = capsule.layerOverridePriority;
            // HunterDriver requires this component, but it must never remain a solid pillar.
            capsule.radius = MimicCollisionSize.x * .5f; capsule.height = MimicCollisionSize.y;
            capsule.direction = 1; capsule.center = center; capsule.enabled = false;
        }
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Roster B setup requires idle Edit Mode and coordinator admission.");
        }
        private static T Ensure<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Asset type conflict: " + path);
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
