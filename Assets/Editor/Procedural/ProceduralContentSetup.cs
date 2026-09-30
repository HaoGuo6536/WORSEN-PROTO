// ============================================================================
// ProceduralContentSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds the config references that opt a selected ProceduralConfig into wave
//   three content. Existing assets and tunings are reused, not overwritten, so
//   coordinator import does not require hand-authored metadata or scene edits.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Procedural.
// KEY RESPONSIBILITIES:
//   - Create missing mirrored theme/challenge assets and wire the selected config.
// DEPENDENCIES:
//   - UnityEditor and Domain.Procedural only.
// USAGE NOTES:
//   Coordinator-only under the Unity publication lease. Refuses play/import/compile;
//   changes no scenes and never auto-runs. Hospital remains provisional and disableable.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Editor.Procedural
{
    public static class ProceduralContentSetup
    {
        [MenuItem("Worsen/Procedural/Wire wave 3c content to selected config")]
        public static void WireSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Content setup requires an idle editor and the coordinator's publication lease.");
            var selected = Selection.activeObject as ProceduralConfig;
            if (selected == null || !AssetDatabase.Contains(selected)) throw new InvalidOperationException("Select an existing ProceduralConfig asset.");
            const string folder = "Assets/Resources/ScriptableObjects/Domain/Procedural";
            EnsureFolder(folder);
            var themes = LoadOrCreate<ProceduralThemeConfig>(folder + "/ProceduralThemeConfig.asset");
            var challenges = LoadOrCreate<ProceduralChallengeConfig>(folder + "/ProceduralChallengeConfig.asset");
            var settings = new SerializedObject(selected);
            if (settings.FindProperty("_themes").objectReferenceValue == null) settings.FindProperty("_themes").objectReferenceValue = themes;
            if (settings.FindProperty("_challenges").objectReferenceValue == null) settings.FindProperty("_challenges").objectReferenceValue = challenges;
            settings.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(selected);
        }
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Wrong asset type at " + path);
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path);
            return value;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); string parent = path.Substring(0, split);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
