// ============================================================================
// DirectorConfigGenerator.cs
// ============================================================================
// PURPOSE:
//   Creates the Director pacing asset in its canonical mirrored location.
//   Deterministic scene setup can request this asset repeatedly while preserving
//   the designer's existing timing values and the asset's serialized identity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Director.
// KEY RESPONSIBILITIES:
//   - Create missing folders and the one Director config asset.
//   - Reuse existing assets without replacing their tuning.
// DEPENDENCIES:
//   - Domain Director configuration and UnityEditor asset APIs.
// USAGE NOTES:
//   Editor-only. Caller must own the Unity lease; this tool does not save scenes.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Director;

namespace Worsen.Editor.Director
{
    public static class DirectorConfigGenerator
    {
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Domain/Director/DirectorConfig.asset";

        [MenuItem("Worsen/Director/Create Config")]
        public static void CreateConfig()
        {
            var config = LoadOrCreateConfig();
            AssetDatabase.SaveAssetIfDirty(config);
        }

        public static DirectorConfig LoadOrCreateConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating Director assets.");
            var existing = AssetDatabase.LoadAssetAtPath<DirectorConfig>(ConfigPath);
            if (existing != null) return existing;
            var folder = "Assets";
            foreach (var part in new[] { "Resources", "ScriptableObjects", "Domain", "Director" })
            {
                var next = folder + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, part);
                folder = next;
            }
            var config = ScriptableObject.CreateInstance<DirectorConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }
    }
}
