// ============================================================================
// HorrorArtSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds project materials, the cake-slice prefab and imported dither-fog
//   renderer integration from explicit asset paths. Only selectively exported
//   project models are used; vendor assets remain in the External folder.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Scenes horror art wiring.
// KEY RESPONSIBILITIES:
//   - Create repeatable cake material/prefab and dedicated fog volume wiring.
// DEPENDENCIES:
//   - UnityEditor, URP and imported FronkonGames DitherFog engine APIs.
// USAGE NOTES:
//   Editor-only, called under the Unity lease. Saves only its owned assets and
//   the selected renderer feature; never invokes a global SaveAssets.
// ============================================================================
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FronkonGames.Weird.DitherFog;
namespace Worsen.Editor.Scenes
{
    public static class HorrorArtSetup
    {
        public const string ArtPath = "Assets/Art/Horror/Cake/";
        public static Material EnsureAttackMaterial()
        {
            string path = ArtPath + "AttackCue.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("Particle unlit shader is required for vertex-colored attack cues.");
            material = new Material(shader) { name = "Attack Cue" };
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        public static GameObject EnsureCake()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath + "WORSEN_CakePickup.fbx");
            if (mesh == null) throw new InvalidOperationException("Import the selected Blender cake slice before building HorrorRun.");
            foreach (string name in new[] { "CakePalette.png", "CakeEmission.png" })
            {
                var importer = AssetImporter.GetAtPath(ArtPath + name) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Cake palette texture is missing.");
                importer.sRGBTexture = true; importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            string materialPath = ArtPath + "CakeSlice.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "CakePalette.png"));
            material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "CakeEmission.png"));
            material.SetColor("_BaseColor", Color.white); material.SetColor("_EmissionColor", Color.white * 0.5f);
            material.SetFloat("_Smoothness", 0.42f); material.EnableKeyword("_EMISSION"); material.enableInstancing = true;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(mesh);
            try
            {
                instance.name = "Cake Slice — One Eighth";
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(instance, ArtPath + "CakeSlice.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        public static Volume BuildFog(Transform parent)
        {
            const string rendererPath = "Assets/Settings/PC_Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null) throw new InvalidOperationException("PC renderer is missing.");
            if (!renderer.rendererFeatures.OfType<DitherFog>().Any())
            {
                var feature = ScriptableObject.CreateInstance<DitherFog>();
                feature.name = "Horror Dither Fog"; feature.Create();
                AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
                renderer.SetDirty(); EditorUtility.SetDirty(renderer); AssetDatabase.SaveAssetIfDirty(renderer);
            }
            var profile = HorrorRunSceneSetup.Ensure<VolumeProfile>("Assets/Resources/ScriptableObjects/Presentation/Horror/HorrorFog.asset");
            if (!profile.TryGet<DitherFogVolume>(out var fog))
            {
                fog = profile.Add<DitherFogVolume>(true);
                AssetDatabase.AddObjectToAsset(fog, profile);
            }
            fog.intensity.Override(1f); fog.curvedFog.Override(false); fog.fogStart.Override(0.45f);
            fog.fogCurveStart.Override(0.008f); fog.fogCurveEnd.Override(0.024f);
            fog.fogColor.Override(new Color(0.012f, 0.018f, 0.02f)); fog.fogOpacity.Override(1f);
            fog.ditherScale.Override(1);
            EditorUtility.SetDirty(fog); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            var owner = new GameObject("Horror Dither Fog"); owner.transform.SetParent(parent, false);
            var volume = owner.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10f; volume.sharedProfile = profile;
            return volume;
        }
    }
}
