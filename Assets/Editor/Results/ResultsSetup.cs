// ============================================================================
// ResultsSetup.cs
// ============================================================================
//
// PURPOSE:
//   Rebuilds the Results service's local UI Toolkit and configuration wiring.
//   The coordinator selects the scene parent and owns scene saving, so this tool
//   can run repeatedly without replacing existing designer tuning or asset identities.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Results.
//
// KEY RESPONSIBILITIES:
//   - Create missing mirrored config and panel assets; require versioned UI sources.
//   - Reuse one named service child and serialize its owned component references.
//
// DEPENDENCIES:
//   - Worsen.Presentation.Results; UnityEditor and Unity UI Toolkit asset APIs.
//
// USAGE NOTES:
//   - Editor-only. Caller owns the exclusive Unity lease, idle/dirty-scene checks and saving.
//   - Create does not initialize runtime state or load, close, or save any scene.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Presentation.Results;

namespace Worsen.Editor.Results
{
    public static class ResultsSetup
    {
        private const string UiRoot = "Assets/Resources/UI/Presentation/Results/";
        private const string ConfigPath = "Assets/Resources/ScriptableObjects/Presentation/Results/ResultsDriverConfig.asset";

        [MenuItem("Worsen/Results/Restore UI Assets")]
        public static void RestoreAssets()
        {
            RequireEditMode();
            var config = LoadOrCreateConfig();
            LoadOrCreatePanel();
            RequireAsset<VisualTreeAsset>(UiRoot + "Results.uxml");
            AssetDatabase.SaveAssetIfDirty(config);
        }

        public static ResultsManager Create(Transform parent)
        {
            RequireEditMode();
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var config = LoadOrCreateConfig();
            var panel = LoadOrCreatePanel();
            var tree = RequireAsset<VisualTreeAsset>(UiRoot + "Results.uxml");
            var child = parent.Find("Results Service");
            var owner = child != null ? child.gameObject : new GameObject("Results Service");
            owner.transform.SetParent(parent, false);
            var document = owner.GetComponent<UIDocument>();
            if (document == null) document = owner.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            var driver = owner.GetComponent<ResultsDriver>();
            if (driver == null) driver = owner.AddComponent<ResultsDriver>();
            var manager = owner.GetComponent<ResultsManager>();
            if (manager == null) manager = owner.AddComponent<ResultsManager>();
            Wire(driver, "_document", document);
            Wire(driver, "_visualTree", tree);
            Wire(driver, "_panelSettings", panel);
            Wire(manager, "_config", config);
            Wire(manager, "_driver", driver);
            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(manager);
            return manager;
        }

        public static ResultsDriverConfig LoadOrCreateConfig()
        {
            RequireEditMode();
            return EnsureAsset<ResultsDriverConfig>(ConfigPath);
        }

        private static PanelSettings LoadOrCreatePanel()
        {
            string path = UiRoot + "ResultsPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = EnsureAsset<PanelSettings>(path);
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.sortingOrder = 200;
                EditorUtility.SetDirty(panel);
            }
            var theme = RequireAsset<ThemeStyleSheet>(UiRoot + "ResultsTheme.tss");
            if (panel.themeStyleSheet != theme)
            {
                panel.themeStyleSheet = theme;
                EditorUtility.SetDirty(panel);
            }
            AssetDatabase.SaveAssetIfDirty(panel);
            return panel;
        }

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required UI source missing: " + path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before restoring Results wiring.");
        }
    }
}
