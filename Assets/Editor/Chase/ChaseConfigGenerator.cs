// ============================================================================
// ChaseConfigGenerator.cs
// ============================================================================
// PURPOSE:
//   Recreates missing chase tuning at its canonical mirrored asset path.
//   Existing assets and authored values are preserved on repeated setup runs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Chase.
// KEY RESPONSIBILITIES:
//   - Create the ChaseConfig asset required by scene assembly.
// DEPENDENCIES:
//   - Common SetupKit creates asset folders while retaining existing identities.
//   - Chase config schema and UnityEditor asset APIs only.
// USAGE NOTES:
//   Run in an idle editor under the coordinator's Unity lease; no scene mutations.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Chase;
namespace Worsen.Editor.Chase
{
    public static class ChaseConfigGenerator
    {
        public const string ConfigPath = "Assets/Resources/ScriptableObjects/Domain/Chase/ChaseConfig.asset";
        [MenuItem("Worsen/Chase/Build Chase Config")]
        public static void BuildChaseConfig() { EnsureAssets(); }
        public static ChaseConfig EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Chase config setup requires an idle Edit Mode editor.");
            ChaseConfig config = AssetDatabase.LoadAssetAtPath<ChaseConfig>(ConfigPath);
            if (config != null) return config;
            Worsen.Editor.Common.SetupKit.EnsureParent(ConfigPath);
            config = ScriptableObject.CreateInstance<ChaseConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssetIfDirty(config); return config;
        }
    }
}
