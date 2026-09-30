// ============================================================================
// EffectCatalogueSetup.cs
// ============================================================================
// PURPOSE:
//   Creates the approved effect catalogue after Unity has generated script metadata.
//   It binds the existing Progression config without replacing its legacy content
//   while migrating catalogue rows by identity to the approved code defaults.
//   Retired ids are removed; unknown designer rows are retained.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Reconcile known rows, remove retired/duplicate ids and append missing defaults.
//   - Idempotently create the mirrored catalogue and fill only an absent binding.
// DEPENDENCIES:
//   - Session Progression config and UnityEditor asset operations.
// USAGE NOTES:
//   Coordinator only in idle Edit Mode under the Unity lease. No scene or prefab edits.
//   Does not register new hunter factories or migrate the shop's live offer subset.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Worsen.Session.Progression;

namespace Worsen.Editor.Progression
{
    public static class EffectCatalogueSetup
    {
        public const string AssetPath = "Assets/Resources/ScriptableObjects/Session/Progression/EffectCatalogueConfig.asset";
        public const string ProgressionPath = "Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset";
        [MenuItem("Worsen/Progression/Ensure Effect Catalogue")]
        public static void EnsureEffectCatalogue()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Effect catalogue setup requires idle Edit Mode.");
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ProgressionPath);
            if (progression == null) throw new InvalidOperationException("Create the Progression config first: " + ProgressionPath);
            var catalogue = AssetDatabase.LoadAssetAtPath<EffectCatalogueConfig>(AssetPath);
            if (catalogue == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
                    throw new InvalidOperationException("Catalogue path contains a different asset type.");
                catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
                AssetDatabase.CreateAsset(catalogue, AssetPath);
                AssetDatabase.SaveAssetIfDirty(catalogue);
            }
            AppendMissingEntries(progression.EffectCatalogue != null ? progression.EffectCatalogue : catalogue);
            if (progression.EffectCatalogue != null) return;
            var serialized = new SerializedObject(progression);
            serialized.FindProperty("_effectCatalogue").objectReferenceValue = catalogue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(progression);
        }

        public static void AppendMissingEntries(EffectCatalogueConfig catalogue)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Catalogue migration requires idle Edit Mode.");
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            var defaults = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try
            {
                var serialized = new SerializedObject(catalogue);
                var entries = serialized.FindProperty("_entries");
                var ids = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < entries.arraySize;)
                {
                    string id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue;
                    if (id == "thin-skin" || id == "bail-bond" || ids.ContainsKey(id))
                    { entries.DeleteArrayElementAtIndex(i); continue; }
                    ids.Add(id, i++);
                }
                foreach (var entry in defaults.Entries)
                {
                    if (!ids.TryGetValue(entry.Id, out int index))
                    {
                        index = entries.arraySize;
                        entries.arraySize = index + 1;
                        ids.Add(entry.Id, index);
                    }
                    var row = entries.GetArrayElementAtIndex(index);
                    row.FindPropertyRelative("_id").stringValue = entry.Id;
                    row.FindPropertyRelative("_kind").intValue = (int)entry.Kind;
                    row.FindPropertyRelative("_axis").intValue = (int)entry.Axis;
                    row.FindPropertyRelative("_title").stringValue = entry.Title;
                    row.FindPropertyRelative("_cardCopy").stringValue = entry.CardCopy;
                    row.FindPropertyRelative("_availabilityRound").intValue = entry.AvailabilityRound;
                    row.FindPropertyRelative("_stackCap").intValue = entry.StackCap;
                    row.FindPropertyRelative("_price").intValue = entry.Price;
                    row.FindPropertyRelative("_value").intValue = entry.Value;
                    row.FindPropertyRelative("_prerequisiteEffectId").stringValue = entry.PrerequisiteEffectId ?? "";
                    row.FindPropertyRelative("_onlyRaisesHunterNumbers").boolValue = entry.OnlyRaisesHunterNumbers;
                    row.FindPropertyRelative("_changeStatement").stringValue = entry.ChangeStatement;
                    var hunters = row.FindPropertyRelative("_requiredHunterIds");
                    hunters.arraySize = entry.RequiredHunterIds.Count;
                    for (int i = 0; i < hunters.arraySize; i++) hunters.GetArrayElementAtIndex(i).stringValue = entry.RequiredHunterIds[i];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (AssetDatabase.Contains(catalogue)) AssetDatabase.SaveAssetIfDirty(catalogue);
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }
    }
}
