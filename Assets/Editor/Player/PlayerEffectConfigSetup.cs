// ============================================================================
// PlayerEffectConfigSetup.cs
// ============================================================================
// PURPOSE:
//   Creates the mirrored Player effect config after Unity generates script metadata.
//   Re-running this tool preserves the existing asset identity and designer tuning;
//   it never edits scenes, prefabs or unrelated assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Create missing folders and the default PlayerEffectConfig asset idempotently.
// DEPENDENCIES:
//   - Player config and UnityEditor asset operations.
// USAGE NOTES:
//   Coordinator only, in an idle editor under its Unity lease. The Player Driver
//   resolves this resource when no serialized config override is assigned.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Player;

namespace Worsen.Editor.Player
{
    public static class PlayerEffectConfigSetup
    {
        public const string AssetPath = "Assets/Resources/ScriptableObjects/Domain/Player/PlayerEffectConfig.asset";

        [MenuItem("Worsen/Player/Ensure Effect Config")]
        public static void EnsureEffectConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Player effect config requires an idle Edit Mode editor.");
            if (AssetDatabase.LoadAssetAtPath<PlayerEffectConfig>(AssetPath) != null) return;
            if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
                throw new InvalidOperationException("Player effect config path contains a different asset type.");
            string[] parts = AssetPath.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length - 1; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
            var config = ScriptableObject.CreateInstance<PlayerEffectConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssetIfDirty(config);
        }
    }
}
