// ============================================================================
// ShrineSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the shrine asset dependencies after scripts have been imported by Unity.
//   Existing identities and designer values survive repeated setup; runtime assembly
//   loads these mirrored assets when no serialized Expedition override is supplied.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Shrine.
// KEY RESPONSIBILITIES:
//   - Ensure placement, appearance, progression and live spawn configs without scene edits.
// DEPENDENCIES:
//   - Domain Shrine, Session Expedition/Progression, existing catalogue path and UnityEditor.
// USAGE NOTES:
//   Coordinator invokes in idle Edit Mode under its lease. Never overwrites authored assets.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Shrine;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Editor.Progression;
namespace Worsen.Editor.Shrine
{
    public static class ShrineSetup
    {
        [MenuItem("Worsen/Shrine/Ensure Shrine Assets")]
        public static void EnsureShrineAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Shrine setup requires idle Edit Mode.");
            Ensure<ShrineConfig>("Domain/Shrine/ShrineConfig");
            Ensure<ShrineDriverConfig>("Domain/Shrine/ShrineDriverConfig");
            Ensure<ExpeditionSpawnDriverConfig>("Session/Expedition/ExpeditionSpawnDriverConfig");
            var rules = Ensure<ShrineProgressionConfig>("Session/Progression/ShrineProgressionConfig");
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(EffectCatalogueSetup.ProgressionPath);
            if (progression == null) throw new InvalidOperationException("Create ProgressionConfig before binding shrine rules.");
            if (progression.ShrineConfig != null) return;
            var serialized = new SerializedObject(progression);
            serialized.FindProperty("_shrineConfig").objectReferenceValue = rules;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(progression);
        }
        private static T Ensure<T>(string relative) where T : ScriptableObject
        {
            string path = "Assets/Resources/ScriptableObjects/" + relative + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Wrong asset type at " + path);
            Folder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssetIfDirty(asset); return asset;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash);
            Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
