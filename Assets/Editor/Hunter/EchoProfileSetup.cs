// ============================================================================
// EchoProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Creates a provisional Echo profile, rules and motor config without new art.
//   Reuses the existing Rusher profile's prefab as a placeholder; repeat runs
//   repair owned references while preserving tuned numbers and asset identities.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Author mirrored Resources assets and explicit provisional brief values.
// DEPENDENCIES:
//   - Hunter configs and UnityEditor asset APIs only.
// USAGE NOTES:
//   Coordinator runs Build in idle Edit Mode after importing scripts and metas.
//   No scenes or prefabs are edited, and only the three owned assets are saved.
//   Exact replay adds zero turn/motion inertia; profile inertia applies to shared
//   motor commands only. Loss is the module's invariant NeverLoses rule.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Echo;
namespace Worsen.Editor.Hunter
{
    public static class EchoProfileSetup
    {
        public const string Directory = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Echo";
        public const string ProfilePath = Directory + "/EchoProfile.asset";
        public const string ConfigPath = Directory + "/EchoConfig.asset";
        public const string MotorPath = Directory + "/EchoMotorDriverConfig.asset";
        public const string PlaceholderProfilePath = "Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/rusher.asset";
        [MenuItem("Worsen/Hunter/Build Echo Profile")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Echo setup requires idle Edit Mode and coordinator admission.");
            var placeholder = AssetDatabase.LoadAssetAtPath<HunterProfile>(PlaceholderProfilePath);
            if (placeholder == null || placeholder.Prefab == null)
                throw new InvalidOperationException("Build the existing horror roster before the Echo placeholder.");
            BuildAssets(Directory, placeholder.Prefab);
        }
        public static HunterProfile BuildAssets(string directory, GameObject placeholder)
        {
            if (placeholder == null || placeholder.GetComponent<HunterManager>() == null)
                throw new ArgumentException("Echo placeholder must contain HunterManager.", nameof(placeholder));
            EnsureFolder(directory);
            string path = directory + "/EchoProfile.asset";
            bool fresh = AssetDatabase.LoadAssetAtPath<HunterProfile>(path) == null;
            HunterProfile profile = Ensure<HunterProfile>(path);
            EchoConfig rules = Ensure<EchoConfig>(directory + "/EchoConfig.asset");
            HunterMotorDriverConfig motor = Ensure<HunterMotorDriverConfig>(directory + "/EchoMotorDriverConfig.asset");
            var so = new SerializedObject(profile);
            so.FindProperty("_archetypeKey").stringValue = "echo";
            so.FindProperty("_prefab").objectReferenceValue = placeholder;
            so.FindProperty("_archetypeRules").objectReferenceValue = rules;
            so.FindProperty("_motorOverride").objectReferenceValue = motor;
            if (fresh)
            {
                so.FindProperty("_acceleration").floatValue = 20f;
                so.FindProperty("_turnRate").floatValue = 240f;
                so.FindProperty("_actionCommitmentSeconds").floatValue = .5f;
                so.FindProperty("_chaseSpeedMultiplier").floatValue = 1f;
                so.FindProperty("_sensorIntervalTicks").intValue = 1;
                so.FindProperty("_lungeWindupSeconds").floatValue = .25f;
                so.FindProperty("_lungeActiveSeconds").floatValue = .3f;
                so.FindProperty("_lungeRecoverySeconds").floatValue = .8f;
                so.FindProperty("_emergenceBias").boolValue = false;
                so.FindProperty("_predictionChance").floatValue = 0f;
                so.FindProperty("_habits").arraySize = 0; // Recording habits must not pause or reroute its trail.
                var pool = so.FindProperty("_mutationPool"); pool.arraySize = 1;
                var mutation = pool.GetArrayElementAtIndex(0);
                mutation.FindPropertyRelative("_tunable").enumValueIndex = (int)HunterTunable.ChaseSpeedMultiplier;
                mutation.FindPropertyRelative("_value").floatValue = 1.1f;
                mutation.FindPropertyRelative("_tellId").stringValue = "echo.quickened-recording";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(rules); AssetDatabase.SaveAssetIfDirty(motor);
            return profile;
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
