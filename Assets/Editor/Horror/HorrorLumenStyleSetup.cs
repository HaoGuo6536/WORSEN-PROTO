// ============================================================================
// HorrorLumenStyleSetup.cs
// ============================================================================
// PURPOSE:
//   Author the project's soft, organic Lumen 2 torch lighting without modifying
//   the imported Lantern profile, shaders or materials.
// ARCHITECTURAL ROLE:
//   Editor tooling (§10) · Editor · Horror.
// KEY RESPONSIBILITIES:
//   - Preserve owned profile/prefab identities across deterministic setup runs.
//   - Use Lumen fake-light falloff and Voronoi fluctuation for broad soft pools.
// DEPENDENCIES:
//   UnityEditor, UnityEngine, DistantLands.Lumen public authoring API.
// USAGE NOTES:
//   Called by HorrorWorldAssetSetup in an idle Editor under its Unity lease.
//   Receiver materials remain unchanged; fake light needs scene-color/depth input.
// ============================================================================
using System;
using DistantLands.Lumen;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Horror
{
    public static class HorrorLumenStyleSetup
    {
        public const string ProfilePath = "Assets/Resources/ScriptableObjects/Presentation/Environment/SoftTorchLumenProfile.asset";
        public const string PrefabPath = "Assets/Prefabs/Horror/Environment/SoftTorchLumen.prefab";

        public static GameObject EnsureSoftTorch(GameObject importedLantern) => EnsureProfile(importedLantern, "SoftTorch");
        public static GameObject EnsureMoon(GameObject importedLantern) => EnsureProfile(importedLantern, "Moon");

        public static GameObject EnsureFlashlight() => EnsureProfile(ImportedLantern(), "Flashlight");
        public static GameObject EnsureNearFill() => EnsureProfile(ImportedLantern(), "NearFill");
        public static GameObject EnsureRoomWarning() => EnsureProfile(ImportedLantern(), "RoomWarning");
        public static GameObject EnsureExitGlow() => EnsureProfile(ImportedLantern(), "ExitGlow");

        private static GameObject ImportedLantern() => AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.distantlands.lumen/Content/Prefabs/Lantern Effect.prefab");

        private static GameObject EnsureProfile(GameObject importedLantern, string kind)
        {
            bool moon = kind == "Moon";
            bool torch = kind == "SoftTorch";
            bool flashlight = kind == "Flashlight";
            string profilePath = "Assets/Resources/ScriptableObjects/Presentation/Environment/" + kind + "LumenProfile.asset";
            string prefabPath = "Assets/Prefabs/Horror/Environment/" + kind + "Lumen.prefab";
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Lumen style setup requires an idle Editor and the caller's Unity lease.");
            LumenEffectPlayer source = importedLantern != null ? importedLantern.GetComponentInChildren<LumenEffectPlayer>(true) : null;
            if (source == null || source.profile == null)
                throw new InvalidOperationException("Imported Lumen Lantern requires a profile.");
            EnsureFolder(profilePath.Substring(0, profilePath.LastIndexOf('/')));
            EnsureFolder(prefabPath.Substring(0, prefabPath.LastIndexOf('/')));
            Object previousProfile = AssetDatabase.LoadMainAssetAtPath(profilePath);
            if (previousProfile != null && !(previousProfile is LumenEffectProfile))
                throw new InvalidOperationException("Soft torch profile path contains another asset type.");
            LumenEffectProfile profile = previousProfile as LumenEffectProfile;
            LumenEffectProfile authored = Object.Instantiate(source.profile);
            try
            {
                authored.name = kind + " Lumen Profile";
                if (!torch) authored.layers.RemoveAll(layer => !(layer is LumenLightLayer));
                authored.autoAssignSun = false;
                bool hasFakeLight = false;
                foreach (LumenEffectLayer layer in authored.layers)
                {
                    if (!(layer is LumenLightLayer light)) continue;
                    hasFakeLight = true;
                    light.active = true;
                    // Vendor shader uses half-range as radial falloff, not Unity Light.range.
                    light.range = moon ? 22f : torch ? 11f : 2f;
                    light.isSpotlight = moon || flashlight;
                    light.minSpotlightAngle = flashlight ? 14f : 45f;
                    light.maxSpotlightAngle = flashlight ? 27.5f : 65f;
                    if (!torch) light.color = moon ? new Color(0.34f, 0.52f, 0.8f) : Color.white;
                    light.repeat = false;
                    light.position = Vector3.zero;
                    light.rotation = Vector3.zero;
                    light.smoothness = flashlight ? 1.2f : torch || moon ? 3f : kind == "ExitGlow" ? 2f : 1.5f;
                    light.brightness = 1f;
                    light.intensity = torch || moon ? 1f : 0.2f;
                    light.posterize = false;
                    light.normalFade = false;
                    light.fluctuation = torch || flashlight;
                    light.fluctuationSpeed = flashlight ? 0.4f : 0.25f;
                    light.fluctuationScale = flashlight ? 1f : 1.6f;
                    light.fluctuationAmount = flashlight ? 0.08f : 0.25f;
                }
                if (!hasFakeLight) throw new InvalidOperationException("Imported Lantern has no fake-light layer.");
                if (profile == null)
                {
                    profile = authored;
                    AssetDatabase.CreateAsset(profile, profilePath);
                    authored = null;
                }
                else EditorUtility.CopySerialized(authored, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            finally { if (authored != null) Object.DestroyImmediate(authored); }

            Object previousPrefab = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (previousPrefab != null && !(previousPrefab is GameObject))
                throw new InvalidOperationException("Soft torch prefab path contains another asset type.");
            bool existing = previousPrefab != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(kind + " Lumen");
            try
            {
                root.SetActive(false);
                LumenEffectPlayer player = root.GetComponent<LumenEffectPlayer>();
                if (player == null) player = root.AddComponent<LumenEffectPlayer>();
                player.profile = profile;
                player.autoAssignSun = false;
                player.useLumenSunScript = false;
                player.updateFrequency = LumenEffectPlayer.UpdateFrequency.ViaScripting;
                player.initializationBehavior = LumenEffectPlayer.InitializationBehavior.Immediate;
                player.deinitializationBehavior = LumenEffectPlayer.DeinitializationBehavior.Immediate;
                player.scale = torch ? 0.7f : 1f; player.range = 1f; player.brightness = torch || moon ? 0.95f : 1f;
                // Save authoring state only. Vendor-generated meshes are runtime-owned.
                player.ClearEffect();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success) throw new InvalidOperationException("Failed to save soft torch Lumen prefab.");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
