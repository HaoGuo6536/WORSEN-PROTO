// ============================================================================
// BlinderHeraldProfileSetup.cs
// ============================================================================
// PURPOSE:
//   Builds Blinder and Herald profiles using the existing Hunter placeholder.
//   Only owned config references are repaired on repeat runs; designer tuning
//   and asset identities are retained. No prefab, scene or roster is rewritten.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Author four-part briefs, three shared habits, mutation tells and depth gates.
//   - Bind an independent ground-only sweep config for the Blinder.
// DEPENDENCIES:
//   - Hunter config types, existing Echo placeholder path and UnityEditor APIs.
// USAGE NOTES:
//   Coordinator runs after import in idle Edit Mode. Blinder pauses at thresholds,
//   turns on loss and reacts to cakes; its floor-trap evidence is Floor-owned.
//   Herald shares those habits but breathes before a stationary radius scream.
//   Catch: Blinder holds eye contact with a hiss; Herald holds its mouth open,
//   reusing the fixed attack scream as the death sting through catch routing.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Herald;
using Worsen.Domain.Hunter.Archetypes.Weaver;
namespace Worsen.Editor.Hunter
{
    public static class BlinderHeraldProfileSetup
    {
        public const string BlinderDirectory = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Blinder";
        public const string HeraldDirectory = "Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Herald";
        public const string BlinderProfilePath = BlinderDirectory + "/BlinderProfile.asset";
        public const string HeraldProfilePath = HeraldDirectory + "/HeraldProfile.asset";
        [MenuItem("Worsen/Hunter/Build Blinder Profile")]
        public static void BuildBlinder() => BuildAssets(BlinderDirectory, Placeholder(), false);
        [MenuItem("Worsen/Hunter/Build Herald Profile")]
        public static void BuildHerald() => BuildAssets(HeraldDirectory, Placeholder(), true);
        private static GameObject Placeholder()
        {
            RequireIdle();
            var profile = AssetDatabase.LoadAssetAtPath<HunterProfile>(EchoProfileSetup.PlaceholderProfilePath);
            if (profile == null || profile.Prefab == null) throw new InvalidOperationException("Build the existing horror roster first.");
            return profile.Prefab;
        }
        public static HunterProfile BuildAssets(string directory, GameObject placeholder, bool herald)
        {
            RequireIdle();
            if (placeholder == null || placeholder.GetComponent<HunterManager>() == null) throw new ArgumentException("Placeholder requires HunterManager.");
            EnsureFolder(directory);
            string name = herald ? "Herald" : "Blinder", key = herald ? "herald" : "blinder";
            string path = directory + "/" + name + "Profile.asset";
            bool fresh = AssetDatabase.LoadAssetAtPath<HunterProfile>(path) == null;
            HunterProfile profile = Ensure<HunterProfile>(path);
            HunterArchetypeConfig rules = herald ? (HunterArchetypeConfig)Ensure<HeraldConfig>(directory + "/HeraldConfig.asset") :
                Ensure<BlinderConfig>(directory + "/BlinderConfig.asset");
            var motor = Ensure<HunterMotorDriverConfig>(directory + "/" + name + "MotorDriverConfig.asset");
            if (!herald)
            {
                var sweep = Ensure<WeaverDriverConfig>(directory + "/BlinderSweepDriverConfig.asset");
                var ruleObject = new SerializedObject(rules); ruleObject.FindProperty("_sweepConfig").objectReferenceValue = sweep;
                ruleObject.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(sweep);
            }
            var so = new SerializedObject(profile);
            so.FindProperty("_archetypeKey").stringValue = key;
            so.FindProperty("_prefab").objectReferenceValue = placeholder;
            so.FindProperty("_archetypeRules").objectReferenceValue = rules;
            so.FindProperty("_motorOverride").objectReferenceValue = motor;
            if (fresh)
            {
                so.FindProperty("_minimumDepth").intValue = herald ? 6 : 5;
                so.FindProperty("_acceleration").floatValue = herald ? 14f : 16f;
                so.FindProperty("_turnRate").floatValue = 200f;
                so.FindProperty("_actionCommitmentSeconds").floatValue = .6f;
                so.FindProperty("_chaseSpeedMultiplier").floatValue = herald ? 1.02f : 1.05f;
                so.FindProperty("_lossSeconds").floatValue = 2.5f;
                so.FindProperty("_lossDistance").floatValue = 14f;
                so.FindProperty("_lungeDistance").floatValue = 1.2f;
                so.FindProperty("_sensorIntervalTicks").intValue = 1;
                so.FindProperty("_screamOnDetection").boolValue = false;
                var pool = so.FindProperty("_mutationPool"); pool.arraySize = 1;
                var mutation = pool.GetArrayElementAtIndex(0);
                mutation.FindPropertyRelative("_tunable").enumValueIndex = (int)HunterTunable.ChaseSpeedMultiplier;
                mutation.FindPropertyRelative("_value").floatValue = 1.15f;
                mutation.FindPropertyRelative("_tellId").stringValue = key + "-quickened-approach";
                // Three existing habits: threshold pause, turn-to-face, cake reaction.
                // Ordinary motor mask 1 prevents Weaver-only partition traversal.
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (ScriptableObject asset in new ScriptableObject[] { profile, rules, motor }) AssetDatabase.SaveAssetIfDirty(asset);
            return profile;
        }
        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Blinder/Herald setup requires idle Edit Mode and coordinator admission.");
        }
        private static T Ensure<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Wrong asset type at " + path);
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
