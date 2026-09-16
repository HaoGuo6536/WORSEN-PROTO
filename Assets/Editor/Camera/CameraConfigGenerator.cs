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
            if (existing != null) return existing;
            var folder = "Assets";
            foreach (var part in new[] { "Resources", "ScriptableObjects", "Presentation", "Camera" })
            {
                var next = folder + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, part);
                folder = next;
            }
            var config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }
    }
}
