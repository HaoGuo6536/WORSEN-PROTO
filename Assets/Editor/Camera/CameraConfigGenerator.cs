// ============================================================================
// CameraConfigGenerator.cs
// ============================================================================
//
// PURPOSE:
//   Creates the mirrored Camera designer config when it is missing.
//   Existing assets are reused to preserve their identity and tuning across deterministic rebuilds.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Camera.
//
// KEY RESPONSIBILITIES:
//   - Create only the owning system's missing config asset.
//   - Save only that config asset from the standalone menu action.
//   - Retain a shader-bound hand material as a config subasset for player builds.
//   - Leave scene saving, imports and test lease admission to the caller.
//
// DEPENDENCIES:
//   - Worsen.Presentation.Camera config type; UnityEditor asset APIs.
//
// USAGE NOTES:
//   - Editor-only; caller must hold the repository Unity lease before invocation.
//   - No runtime effects or scene changes are started by this generator.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Camera;

namespace Worsen.Editor.Camera
{
    public static class CameraConfigGenerator
    {
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Presentation/Camera/CameraDriverConfig.asset";

        [MenuItem("Worsen/Camera/Create Config")]
        public static void CreateConfig()
        {
            var config = LoadOrCreateConfig();
            AssetDatabase.SaveAssetIfDirty(config);
        }

        public static CameraDriverConfig LoadOrCreateConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating Camera assets.");
            var existing = AssetDatabase.LoadAssetAtPath<CameraDriverConfig>(ConfigPath);
            if (existing != null) { EnsureHandMaterial(existing); return existing; }
            var folder = "Assets";
            foreach (var part in new[] { "Resources", "ScriptableObjects", "Presentation", "Camera" })
            {
                var next = folder + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, part);
                folder = next;
            }
            var config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            EnsureHandMaterial(config);
            return config;
        }

        public static Material EnsureHandMaterial(CameraDriverConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Hand material setup requires idle Edit Mode.");
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!AssetDatabase.Contains(config)) throw new InvalidOperationException("Persist Camera config before assigning its hand material.");
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty("_handMaterial");
            if (property.objectReferenceValue is Material retained && AssetDatabase.Contains(retained)) return retained;
            Material material = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(config)))
                if (asset is Material candidate && candidate.name == "Hand Catch Material") { material = candidate; break; }
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is required for the retained hand material.");
                material = new Material(shader) { name = "Hand Catch Material" };
                material.SetColor("_BaseColor", new Color(0.12f, 0.10f, 0.09f, 1f));
                AssetDatabase.AddObjectToAsset(material, config);
            }
            property.objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(config);
            return material;
        }
    }
}
