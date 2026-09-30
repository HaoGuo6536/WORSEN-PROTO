// ============================================================================
// ShopSetup.cs
// ============================================================================
// PURPOSE:
//   Creates and binds the shop's designer asset after Unity imports its scripts.
//   Existing catalogue and economy tuning are preserved on repeated setup runs.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Ensure catalogue and mirrored shop assets, filling only absent bindings.
// DEPENDENCIES:
//   - Progression config, ShopConfig, existing catalogue setup and UnityEditor.
// USAGE NOTES:
//   Coordinator only, in idle Edit Mode under its lease. No scene/prefab edits.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using Worsen.Session.Progression;
using Worsen.Session.Progression.Shop;

namespace Worsen.Editor.Progression
{
    public static class ShopSetup
    {
        public const string Folder = "Assets/Resources/ScriptableObjects/Session/Progression/Shop";
        public const string AssetPath = Folder + "/ShopConfig.asset";
        [MenuItem("Worsen/Progression/Ensure Shop")]
        public static void EnsureShop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Shop setup requires idle Edit Mode.");
            EffectCatalogueSetup.EnsureEffectCatalogue();
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(EffectCatalogueSetup.ProgressionPath);
            if (progression.ShopConfig != null) return;
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources/ScriptableObjects/Session/Progression", "Shop");
            var shop = AssetDatabase.LoadAssetAtPath<ShopConfig>(AssetPath);
            if (shop == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null)
                    throw new InvalidOperationException("Shop path contains a different asset type.");
                shop = ScriptableObject.CreateInstance<ShopConfig>();
                AssetDatabase.CreateAsset(shop, AssetPath);
                AssetDatabase.SaveAssetIfDirty(shop);
            }
            var serialized = new SerializedObject(progression);
            serialized.FindProperty("_shopConfig").objectReferenceValue = shop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(progression);
        }
    }
}
