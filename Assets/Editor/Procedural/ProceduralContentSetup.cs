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
//   - Wire theme, challenge, organic fallback and validated room catalogue assets.
// DEPENDENCIES:
//   - Common SetupKit creates asset folders while retaining existing identities.
//   - UnityEditor and Domain.Procedural only.
// USAGE NOTES:
//   Coordinator-only under the Unity publication lease. Refuses play/import/compile;
//   changes no scenes and never auto-runs. Existing designer overrides are preserved.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Editor.Procedural
{
    public static class ProceduralContentSetup
    {
        [MenuItem("Worsen/Procedural/Wire wave 3c content to selected config")]
        public static void WireSelected()
            => Configure(Selection.activeObject as ProceduralConfig);

        public static void Configure(ProceduralConfig selected)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Content setup requires an idle editor and the coordinator's publication lease.");

            if (selected == null || !AssetDatabase.Contains(selected)) throw new InvalidOperationException("Select an existing ProceduralConfig asset.");
            const string folder = "Assets/Resources/ScriptableObjects/Domain/Procedural";
            EnsureFolder(folder);
            var themes = LoadOrCreate<ProceduralThemeConfig>(folder + "/ProceduralThemeConfig.asset");
            var challenges = LoadOrCreate<ProceduralChallengeConfig>(folder + "/ProceduralChallengeConfig.asset");
            var organic = LoadOrCreate<ProceduralOrganicConfig>(folder + "/ProceduralOrganicConfig.asset");
            var imported = new List<ProceduralTemplateCatalogue>();
            foreach (string theme in new[] { "Castle", "Hospital", "School", "Basement" })
            {
                string root = "Assets/Art/Environment/" + theme;
                string kit = root + "/Kit/" + theme + "Kit.manifest.json";
                string rooms = root + "/Rooms/" + theme + "Rooms.manifest.json";
                if (!File.Exists(kit) || !File.Exists(rooms))
                { Debug.LogWarning(theme + " catalogue absent: generation will record organic fallback."); continue; }
                try { imported.Add(ProceduralRoomManifestSetup.Parse(File.ReadAllText(kit), File.ReadAllText(rooms))); }
                catch (ArgumentException error) { Debug.LogWarning(theme + " catalogue rejected; organic fallback: " + error.Message); }
            }
            var catalogue = LoadOrCreate<ProceduralRoomCatalogueData>(folder + "/ProceduralRoomCatalogueData.asset");
            ProceduralRoomManifestSetup.Publish(catalogue, imported.ToArray());
            ProceduralKitAssetSetup.Build(catalogue, selected);
            var settings = new SerializedObject(selected);
            if (settings.FindProperty("_themes").objectReferenceValue == null) settings.FindProperty("_themes").objectReferenceValue = themes;
            if (settings.FindProperty("_challenges").objectReferenceValue == null) settings.FindProperty("_challenges").objectReferenceValue = challenges;
            if (settings.FindProperty("_organic").objectReferenceValue == null) settings.FindProperty("_organic").objectReferenceValue = organic;
            settings.FindProperty("_roomCatalogue").objectReferenceValue = catalogue;
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
            => Worsen.Editor.Common.SetupKit.EnsureFolder(path);
    }
}
