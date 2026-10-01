// ============================================================================
// ObservedHunterProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Builds Mannequin and Stare rule/profile assets using an existing placeholder.
//   Repeat execution repairs only owned references and preserves tuning and asset
//   identities; no scenes, prefabs, roster lists or catalogue entries are changed.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Author four-part briefs, depth gates, habits and learnable mutation tells.
// DEPENDENCIES:
//   - Hunter configs, Core rule identities and UnityEditor asset APIs.
// USAGE NOTES:
//   Coordinator runs the menu in admitted idle Edit Mode after importing scripts.
//   Mannequin moves whenever unseen, lit or dark, and freezes silently in view;
//   it wins silently in the shared catch. Wick still freezes it.
//   Stare holds eye contact, returns on cadence and wins with stare.catch.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Hunter.Archetypes.Stare;
namespace Worsen.Editor.Hunter
{
    public static class ObservedHunterProfileSetup
    {
        public const string Root = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/";
        [MenuItem("Worsen/Hunter/Build Mannequin Profile")]
        public static void BuildMannequin() => BuildAssets(Root + "Mannequin", Placeholder(), false);
        [MenuItem("Worsen/Hunter/Build Stare Profile")]
        public static void BuildStare() => BuildAssets(Root + "Stare", Placeholder(), true);
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Observed-hunter setup requires idle Edit Mode and coordinator admission.");
        }
        private static GameObject Placeholder()
        {
            RequireIdle();
            var source = AssetDatabase.LoadAssetAtPath<HunterProfile>(EchoProfileSetup.PlaceholderProfilePath);
            if (source == null || source.Prefab == null) throw new InvalidOperationException("Build the existing horror placeholder first.");
            return source.Prefab;
        }
        public static HunterProfile BuildAssets(string directory, GameObject placeholder, bool stare)
        {
            RequireIdle();
            if (placeholder == null || placeholder.GetComponent<HunterManager>() == null) throw new ArgumentException("A HunterManager placeholder is required.");
            string[] parts = directory.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            { string next = parent + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]); parent = next; }
            string name = stare ? "Stare" : "Mannequin"; string path = directory + "/" + name + "Profile.asset";
            bool fresh = AssetDatabase.LoadAssetAtPath<HunterProfile>(path) == null;
            var profile = Ensure<HunterProfile>(path);
            HunterArchetypeConfig config = stare ? (HunterArchetypeConfig)Ensure<StareConfig>(directory + "/StareConfig.asset") :
                Ensure<MannequinConfig>(directory + "/MannequinConfig.asset");
            var motor = Ensure<HunterMotorDriverConfig>(directory + "/" + name + "MotorDriverConfig.asset");
            var so = new SerializedObject(profile);
            so.FindProperty("_archetypeKey").stringValue = stare ? "stare" : "mannequin";
            so.FindProperty("_prefab").objectReferenceValue = placeholder;
            so.FindProperty("_archetypeRules").objectReferenceValue = config;
            so.FindProperty("_motorOverride").objectReferenceValue = motor;
            if (fresh)
            {
                so.FindProperty("_minimumDepth").intValue = stare ? 6 : 4;
                so.FindProperty("_acceleration").floatValue = stare ? 20f : 32f;
                so.FindProperty("_turnRate").floatValue = stare ? 240f : 540f;
                so.FindProperty("_actionCommitmentSeconds").floatValue = stare ? .5f : .35f;
                so.FindProperty("_chaseSpeedMultiplier").floatValue = stare ? 1.12f : 1.05f;
                so.FindProperty("_lossSeconds").floatValue = 2.5f;
                so.FindProperty("_lossDistance").floatValue = 14f;
                so.FindProperty("_sensorIntervalTicks").intValue = 1;
                so.FindProperty("_emergenceBias").boolValue = false;
                so.FindProperty("_predictionChance").floatValue = 0f;
                so.FindProperty("_screamOnDetection").boolValue = false;
                var habits = so.FindProperty("_habits"); habits.arraySize = 1;
                var habit = habits.GetArrayElementAtIndex(0);
                habit.FindPropertyRelative("_kind").enumValueIndex = (int)HunterHabitKind.ThresholdPause;
                habit.FindPropertyRelative("_enabled").boolValue = true;
                habit.FindPropertyRelative("_pauseSeconds").floatValue = .4f;
                var pool = so.FindProperty("_mutationPool"); pool.arraySize = 1;
                var mutation = pool.GetArrayElementAtIndex(0);
                mutation.FindPropertyRelative("_tunable").enumValueIndex = (int)HunterTunable.ChaseSpeedMultiplier;
                mutation.FindPropertyRelative("_value").floatValue = stare ? 1.2f : 1.15f;
                mutation.FindPropertyRelative("_tellId").stringValue = stare ? "stare.quickened-gaze" : "mannequin.long-step";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(profile); AssetDatabase.SaveAssetIfDirty(config); AssetDatabase.SaveAssetIfDirty(motor);
            return profile;
        }
        private static T Ensure<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Wrong asset type at " + path);
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
    }
}
