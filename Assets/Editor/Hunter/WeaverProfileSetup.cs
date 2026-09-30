// ============================================================================
// WeaverProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Creates the Weaver profile and three configs at stable Resources paths.
//   Reuses the existing Hunter placeholder without changing its prefab, and
//   repairs owned references while preserving designer tuning on repeat runs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Author the four-part brief, shared habits and area-3 navigation opt-in.
// DEPENDENCIES:
//   - Hunter configs and UnityEditor asset APIs only.
// USAGE NOTES:
//   Coordinator runs Build in idle Edit Mode after import. No scene, prefab or
//   ProjectSettings edits; only the four named assets are saved. Area 3 must be
//   reserved for PLAN-026 partition links; ordinary hunters retain mask 1.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Weaver;
namespace Worsen.Editor.Hunter
{
    public static class WeaverProfileSetup
    {
        public const string Directory = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Weaver";
        public const string ProfilePath = Directory + "/WeaverProfile.asset";
        public const string ConfigPath = Directory + "/WeaverConfig.asset";
        public const string DriverPath = Directory + "/WeaverDriverConfig.asset";
        public const string MotorPath = Directory + "/WeaverMotorDriverConfig.asset";
        [MenuItem("Worsen/Hunter/Build Weaver Profile")]
        public static void Build()
        {
            RequireIdle();
            var placeholder = AssetDatabase.LoadAssetAtPath<HunterProfile>(EchoProfileSetup.PlaceholderProfilePath);
            if (placeholder == null || placeholder.Prefab == null) throw new InvalidOperationException("Build the existing horror roster first.");
            BuildAssets(Directory, placeholder.Prefab);
        }
        public static HunterProfile BuildAssets(string directory, GameObject placeholder)
        {
            RequireIdle();
            if (placeholder == null || placeholder.GetComponent<HunterManager>() == null) throw new ArgumentException("Placeholder requires HunterManager.");
            EnsureFolder(directory);
            bool fresh = AssetDatabase.LoadAssetAtPath<HunterProfile>(directory + "/WeaverProfile.asset") == null;
            HunterProfile profile = Ensure<HunterProfile>(directory + "/WeaverProfile.asset");
            WeaverConfig rules = Ensure<WeaverConfig>(directory + "/WeaverConfig.asset");
            WeaverDriverConfig driver = Ensure<WeaverDriverConfig>(directory + "/WeaverDriverConfig.asset");
            HunterMotorDriverConfig motor = Ensure<HunterMotorDriverConfig>(directory + "/WeaverMotorDriverConfig.asset");
            var so = new SerializedObject(profile);
            so.FindProperty("_archetypeKey").stringValue = "weaver";
            so.FindProperty("_prefab").objectReferenceValue = placeholder;
            so.FindProperty("_archetypeRules").objectReferenceValue = rules;
            so.FindProperty("_motorOverride").objectReferenceValue = motor;
            // Shared attacks are close catches only; web attacks live in the module.
            so.FindProperty("_attackStyle").enumValueIndex = (int)HunterAttackStyle.Lunge;
            if (fresh)
            {
                so.FindProperty("_acceleration").floatValue = 16f;
                so.FindProperty("_turnRate").floatValue = 200f;
                so.FindProperty("_actionCommitmentSeconds").floatValue = .6f;
                so.FindProperty("_chaseSpeedMultiplier").floatValue = 1.05f;
                so.FindProperty("_lossSeconds").floatValue = 2.5f;
                so.FindProperty("_lossDistance").floatValue = 14f;
                so.FindProperty("_lungeDistance").floatValue = 1.2f;
                so.FindProperty("_sensorIntervalTicks").intValue = 1;
                // Keep threshold pause, turn-to-face and cake reaction defaults.
                var pool = so.FindProperty("_mutationPool"); pool.arraySize = 1;
                var mutation = pool.GetArrayElementAtIndex(0);
                mutation.FindPropertyRelative("_tunable").enumValueIndex = (int)HunterTunable.ChaseSpeedMultiplier;
                mutation.FindPropertyRelative("_value").floatValue = 1.15f;
                mutation.FindPropertyRelative("_tellId").stringValue = "weaver-quickened-skitter";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var motorSo = new SerializedObject(motor);
            motorSo.FindProperty("_navigationAreaMask").intValue = 1 | (1 << 3);
            motorSo.ApplyModifiedPropertiesWithoutUndo();
            var rulesSo = new SerializedObject(rules); rulesSo.FindProperty("_driverConfig").objectReferenceValue = driver;
            rulesSo.ApplyModifiedPropertiesWithoutUndo();
            foreach (ScriptableObject asset in new ScriptableObject[] { profile, rules, driver, motor }) AssetDatabase.SaveAssetIfDirty(asset);
            return profile;
        }
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Weaver setup requires idle Edit Mode and coordinator admission.");
        }
        private static T Ensure<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
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
