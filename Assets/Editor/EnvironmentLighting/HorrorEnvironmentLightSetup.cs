// ============================================================================
// HorrorEnvironmentLightSetup.cs
// ============================================================================
// PURPOSE:
//   Authors a local-light template with a bounded URP shadow request.
//   Six point-light faces need explicit resolution to coexist with camera lights.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10) - Editor - Environment deterministic asset setup.
// KEY RESPONSIBILITIES:
//   - Preserve the owned prefab identity and assign its URP Low shadow tier.
//   - Wire the supplied persistent Environment config without global quality edits.
// DEPENDENCIES:
//   - UnityEditor, URP additional light authoring and Environment DriverConfig.
// USAGE NOTES:
//   Called by HorrorWorldAssetSetup under the coordinator's exclusive Unity lease.
//   Saves only its owned prefab and the supplied Environment config asset.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Worsen.Presentation.Environment;

namespace Worsen.Editor.EnvironmentLighting
{
    public static class HorrorEnvironmentLightSetup
    {
        public const string TemplatePath = "Assets/Prefabs/Horror/Environment/LocalLightTemplate.prefab";

        public static Light EnsureTemplate(EnvironmentDriverConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Local light setup requires an idle Editor and the caller's Unity lease.");
            if (config == null || !AssetDatabase.GetAssetPath(config).StartsWith("Assets/Resources/ScriptableObjects/Presentation/Environment/", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected a project-owned persistent Environment config.");
            var configData = new SerializedObject(config);
            SerializedProperty templateField = configData.FindProperty("_lightTemplate");
            if (templateField == null) throw new InvalidOperationException("Environment config has no light template field.");
            EnsureFolder("Assets/Prefabs/Horror/Environment");
            UnityEngine.Object previous = AssetDatabase.LoadMainAssetAtPath(TemplatePath);
            if (previous != null && !(previous is GameObject))
                throw new InvalidOperationException("Local light template path contains another asset type.");
            bool existing = previous != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(TemplatePath) : new GameObject("Local Light Template");
            try
            {
                Light light = root.GetComponent<Light>();
                if (light == null)
                {
                    light = root.AddComponent<Light>();
                    light.type = LightType.Point; light.shadows = LightShadows.None;
                    light.enabled = false; light.intensity = 0f;
                }
                UniversalAdditionalLightData additional = root.GetComponent<UniversalAdditionalLightData>();
                if (additional == null) additional = root.AddComponent<UniversalAdditionalLightData>();
                var serialized = new SerializedObject(additional);
                SerializedProperty tier = serialized.FindProperty("m_AdditionalLightsShadowResolutionTier");
                if (tier == null) throw new InvalidOperationException("Installed URP has no serialized shadow resolution tier.");
                tier.intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TemplatePath, out bool success);
                if (!success) throw new InvalidOperationException("Failed to save local light template.");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            Light saved = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath).GetComponent<Light>();
            templateField.objectReferenceValue = saved;
            configData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            return saved;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
