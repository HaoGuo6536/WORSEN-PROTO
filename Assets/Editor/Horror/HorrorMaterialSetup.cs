// ============================================================================
// HorrorMaterialSetup.cs
// ============================================================================
// PURPOSE:
//   Builds project-owned URP versions of imported castle surfaces and prefab
//   renderer materials without changing vendor assets or their mesh identities.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10) - Editor - Horror deterministic asset setup.
// KEY RESPONSIBILITIES:
//   - Preserve source texture channels, tiling, opaque/cutout and particle blending.
//   - Save stable source-GUID material copies and prefab renderer overrides.
// DEPENDENCIES:
//   - UnityEditor asset/prefab APIs and Unity URP Lit / Particles Unlit shaders.
// USAGE NOTES:
//   Called by HorrorWorldAssetSetup only while the coordinator holds the Unity
//   lease and the editor is idle. Never changes scenes or imported source assets.
//   AE/Grunge's base masonry maps are preserved; its built-in grunge overlay is
//   intentionally not copied into an incompatible URP shader property layout.
// ============================================================================
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Horror
{
    public static class HorrorMaterialSetup
    {
        private const string MaterialRoot = "Assets/Art/Horror/Environment/Materials/";
        private const string PrefabRoot = "Assets/Prefabs/Horror/Environment/";

        public static Material BuildMaterial(Material source, bool particle = false)
        {
            RequireIdle();
            string identity = SourceIdentity(source);
            Shader shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required URP environment shader is unavailable.");
            string path = MaterialRoot + identity + (particle ? "_Particle.mat" : "_Surface.mat");
            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            Material target = LoadOwned<Material>(path);
            if (target == null)
            {
                EnsureParent(path);
                target = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(target, path);
            }
            // Reset through a clean material so reruns cannot retain obsolete shader keywords.
            var template = new Material(shader);
            try { target.shader = shader; target.CopyPropertiesFromMaterial(template); }
            finally { Object.DestroyImmediate(template); }
            // Unity expects a main asset name to match its filename; paths retain source identity.
            target.name = assetName;
            target.enableInstancing = !particle;

            bool grunge = source.shader != null && source.shader.name == "AE/Grunge";
            CopyTexture(source, target, "_BaseMap", grunge ? "_Base_Color" : "_BaseMap", "_MainTex");
            Color tint = ColorValue(source, Color.white, grunge ? "_Tint" : "_BaseColor", "_Color", "_TintColor");
            float mode = FloatValue(source, 0f, "_Mode");
            bool transparent = particle || FloatValue(source, 0f, "_Surface") > 0.5f || mode >= 2f;
            bool cutout = !transparent && (mode == 1f || FloatValue(source, 0f, "_AlphaClip") > 0.5f || source.IsKeywordEnabled("_ALPHATEST_ON"));
            // The grunge shader stores zero in _Tint.a despite being explicitly opaque.
            if (!transparent && !cutout) tint.a = 1f;
            target.SetColor("_BaseColor", tint);
            if (grunge && source.HasProperty("_Tiling_Main_Texture"))
            {
                Vector4 tiling = source.GetVector("_Tiling_Main_Texture");
                target.SetTextureScale("_BaseMap", new Vector2(tiling.x, tiling.y));
            }

            if (!particle)
            {
                bool normal = CopyTexture(source, target, "_BumpMap", grunge ? "_Normal" : "_BumpMap");
                bool metal = CopyTexture(source, target, "_MetallicGlossMap", grunge ? "_Metallic_Smoothness" : "_MetallicGlossMap");
                bool occlusion = CopyTexture(source, target, "_OcclusionMap", grunge ? "_AmbientOcclusion" : "_OcclusionMap");
                bool emission = CopyTexture(source, target, "_EmissionMap", "_EmissionMap");
                target.SetFloat("_BumpScale", FloatValue(source, 1f, grunge ? "_Normal_Scale" : "_BumpScale"));
                target.SetFloat("_Smoothness", FloatValue(source, 0.3f, "_Smoothness", metal ? "_GlossMapScale" : "_Glossiness"));
                target.SetFloat("_Metallic", FloatValue(source, 0f, "_Metallic"));
                target.SetFloat("_OcclusionStrength", FloatValue(source, 1f, "_OcclusionStrength"));
                target.SetFloat("_WorkflowMode", 1f);
                Color emissionColor = ColorValue(source, Color.black, "_EmissionColor");
                target.SetColor("_EmissionColor", emissionColor);
                Keyword(target, "_NORMALMAP", normal);
                Keyword(target, "_METALLICSPECGLOSSMAP", metal);
                Keyword(target, "_OCCLUSIONMAP", occlusion);
                Keyword(target, "_EMISSION", emission || emissionColor.maxColorComponent > 0f);
            }

            bool additive = transparent && particle && FloatValue(source, (float)BlendMode.OneMinusSrcAlpha, "_DstBlend") == (float)BlendMode.One;
            target.SetFloat("_Surface", transparent ? 1f : 0f);
            target.SetFloat("_Blend", additive ? 2f : 0f);
            target.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            target.SetFloat("_Cutoff", FloatValue(source, 0.5f, "_Cutoff"));
            target.SetInt("_SrcBlend", (int)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            target.SetInt("_DstBlend", (int)(transparent ? (additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha) : BlendMode.Zero));
            if (target.HasProperty("_SrcBlendAlpha")) target.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            if (target.HasProperty("_DstBlendAlpha")) target.SetInt("_DstBlendAlpha", (int)(transparent ? (additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha) : BlendMode.Zero));
            target.SetInt("_ZWrite", transparent ? 0 : 1);
            target.SetInt("_Cull", (int)FloatValue(source, particle ? (float)CullMode.Off : (float)CullMode.Back, "_Cull"));
            target.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            target.SetShaderPassEnabled("ShadowCaster", !transparent);
            Keyword(target, "_SURFACE_TYPE_TRANSPARENT", transparent);
            Keyword(target, "_ALPHATEST_ON", cutout);
            target.renderQueue = (int)(transparent ? RenderQueue.Transparent : cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return target;
        }

        public static GameObject BuildPrefab(GameObject source)
        {
            RequireIdle();
            string identity = SourceIdentity(source);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string path = PrefabRoot + identity + ".prefab";
            LoadOwned<GameObject>(path);
            EnsureParent(path);
            // LoadPrefabContents uses an isolated preview scene, leaving open scenes untouched.
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null)
                            throw new InvalidOperationException("Imported renderer has an empty material slot: " + sourcePath + " / " + renderer.name);
                        materials[i] = BuildMaterial(materials[i], renderer is ParticleSystemRenderer);
                    }
                    renderer.sharedMaterials = materials;
                    if (renderer is ParticleSystemRenderer particles && particles.trailMaterial != null)
                        particles.trailMaterial = BuildMaterial(particles.trailMaterial, true);
                }
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null) throw new InvalidOperationException("URP prefab save failed: " + path);
                return saved;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool CopyTexture(Material source, Material target, string destination, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!source.HasProperty(candidate) || source.GetTexture(candidate) == null) continue;
                target.SetTexture(destination, source.GetTexture(candidate));
                target.SetTextureScale(destination, source.GetTextureScale(candidate));
                target.SetTextureOffset(destination, source.GetTextureOffset(candidate));
                return true;
            }
            return false;
        }

        private static float FloatValue(Material source, float fallback, params string[] properties)
        { foreach (string property in properties) if (source.HasProperty(property)) return source.GetFloat(property); return fallback; }

        private static Color ColorValue(Material source, Color fallback, params string[] properties)
        { foreach (string property in properties) if (source.HasProperty(property)) return source.GetColor(property); return fallback; }

        private static void Keyword(Material material, string keyword, bool enabled)
        { if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword); }

        private static string SourceIdentity(Object source)
        {
            if (source == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("URP conversion requires a persistent imported source asset.");
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (!sourcePath.StartsWith("Assets/External/Environment/The_Modular_Medieval_Castle/", StringComparison.Ordinal))
                throw new InvalidOperationException("URP castle conversion is restricted to the selected imported castle: " + sourcePath);
            return guid + "_" + localId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static T LoadOwned<T>(string path) where T : Object
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && !(asset is T)) throw new InvalidOperationException("Owned URP path contains a different asset type: " + path);
            return asset as T;
        }

        private static void EnsureParent(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length - 1; i++)
            { string next = current + "/" + segments[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]); current = next; }
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("URP material setup requires the coordinator's lease and an idle editor.");
        }
    }
}
