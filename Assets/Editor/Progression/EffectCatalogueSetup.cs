// ============================================================================
// EffectCatalogueSetup.cs
// ============================================================================
// PURPOSE:
//   Creates the approved effect catalogue after Unity has generated script metadata.
//   It binds the existing Progression config without replacing its legacy content
//   or overwriting designer tuning on subsequent runs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Idempotently create the mirrored catalogue and fill only an absent binding.
// DEPENDENCIES:
//   - Session Progression config and UnityEditor asset operations.
// USAGE NOTES:
//   Coordinator only in idle Edit Mode under the Unity lease. No scene or prefab edits.
//   Does not register new hunter factories or migrate the shop's live offer subset.
// ============================================================================
using System;
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
            if (progression.EffectCatalogue != null) return;
            var serialized = new SerializedObject(progression);
            serialized.FindProperty("_effectCatalogue").objectReferenceValue = catalogue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(progression);
        }
    }
}
