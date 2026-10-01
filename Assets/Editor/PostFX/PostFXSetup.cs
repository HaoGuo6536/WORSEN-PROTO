// ============================================================================
// PostFXSetup.cs
// ============================================================================
//
// PURPOSE:
//   Constructs one scene-owned PostFX service under the coordinator's chosen parent.
//   It serializes only local system wiring and leaves scene identity and saving to its caller.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Reuse the named child on repeated setup and preserve config assets.
//   - Wire Manager and Driver without starting runtime initialization.
//   - Retain shader-bound camcorder and hunter-only Glimpse features alongside existing features.
//
// DEPENDENCIES:
//   - Worsen.Presentation.PostFX, URP renderer data and UnityEditor serialization APIs.
//
// USAGE NOTES:
//   - Editor-only; coordinator owns scene creation, dirty-scene checks, saving and lease.
//   - Package component construction stays inside the owning Driver.
//
// ============================================================================

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Worsen.Presentation.PostFX;

namespace Worsen.Editor.PostFX
{
    public static class PostFXSetup
    {
        public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        public const string ShaderPath = "Assets/Shaders/CamcorderFrame.shader";
        public const string GlimpseShaderPath = "Assets/Shaders/GlimpseOutline.shader";

        [MenuItem("Worsen/PostFX/Install Camcorder Frame")]
        public static void InstallCamcorderFrame()
        {
            RequireIdle();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (renderer == null || shader == null)
                throw new InvalidOperationException("Camcorder setup requires " + RendererPath + " and " + ShaderPath);
            EnsureCamcorderFeature(renderer, shader);
        }

        public static CamcorderFrameRendererFeature EnsureCamcorderFeature(UniversalRendererData renderer, Shader shader)
        {
            RequireIdle();
            if (renderer == null || shader == null) throw new ArgumentNullException(renderer == null ? nameof(renderer) : nameof(shader));
            if (!AssetDatabase.Contains(renderer)) throw new InvalidOperationException("Persist renderer data before installing its feature.");
            CamcorderFrameRendererFeature frame = null;
            foreach (var feature in renderer.rendererFeatures)
                if (feature is CamcorderFrameRendererFeature candidate)
                {
                    if (frame != null) throw new InvalidOperationException("Renderer contains duplicate camcorder features; resolve ownership before setup.");
                    frame = candidate;
                }
            if (frame == null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(renderer)))
                    if (asset is CamcorderFrameRendererFeature candidate) { frame = candidate; break; }
                if (frame == null)
                {
                    frame = ScriptableObject.CreateInstance<CamcorderFrameRendererFeature>();
                    frame.name = "Worsen Camcorder Frame";
                    AssetDatabase.AddObjectToAsset(frame, renderer);
                }
                renderer.rendererFeatures.Add(frame);
            }
            frame.Initialize(shader);
            frame.SetActive(true);
            EditorUtility.SetDirty(frame);
            // URP SetDirty invalidates rendering only; persist its recovery map explicitly.
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                if (renderer.rendererFeatures[i] != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    renderer.rendererFeatures[i], out string _, out long localId))
                    map.GetArrayElementAtIndex(i).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(frame);
            return frame;
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("PostFX setup requires idle Edit Mode.");
        }

        [MenuItem("Worsen/PostFX/Install Glimpse Outline")]
        public static void InstallGlimpseOutline()
        {
            RequireIdle();
            int layer = LayerMask.NameToLayer("HunterBody");
            if (layer < 0) throw new InvalidOperationException("Glimpse requires the HunterBody layer; run hunter setup first.");
            EnsureGlimpseFeature(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath),
                AssetDatabase.LoadAssetAtPath<Shader>(GlimpseShaderPath), 1 << layer);
        }

        public static GlimpseRendererFeature EnsureGlimpseFeature(UniversalRendererData renderer, Shader shader, LayerMask hunterLayers)
        {
            RequireIdle();
            if (renderer == null || shader == null) throw new ArgumentNullException(renderer == null ? nameof(renderer) : nameof(shader));
            int layer = LayerMask.NameToLayer("HunterBody");
            if (layer < 0 || hunterLayers.value != (1 << layer))
                throw new ArgumentException("Glimpse must use only the HunterBody layer.", nameof(hunterLayers));
            if (!AssetDatabase.Contains(renderer)) throw new InvalidOperationException("Persist renderer data before installing its feature.");
            GlimpseRendererFeature glimpse = null;
            foreach (var feature in renderer.rendererFeatures)
                if (feature is GlimpseRendererFeature candidate)
                {
                    if (glimpse != null) throw new InvalidOperationException("Renderer contains duplicate Glimpse features; resolve ownership before setup.");
                    glimpse = candidate;
                }
            if (glimpse == null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(renderer)))
                    if (asset is GlimpseRendererFeature candidate) { glimpse = candidate; break; }
                if (glimpse == null)
                {
                    glimpse = ScriptableObject.CreateInstance<GlimpseRendererFeature>();
                    glimpse.name = "Worsen Glimpse";
                    AssetDatabase.AddObjectToAsset(glimpse, renderer);
                }
                renderer.rendererFeatures.Add(glimpse);
            }
            glimpse.Initialize(shader, hunterLayers);
            glimpse.SetActive(true);
            EditorUtility.SetDirty(glimpse);
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                if (renderer.rendererFeatures[i] != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    renderer.rendererFeatures[i], out string _, out long localId))
                    map.GetArrayElementAtIndex(i).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty(); EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer); AssetDatabase.SaveAssetIfDirty(glimpse);
            return glimpse;
        }

        public static PostFXManager Create(Transform parent)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before constructing PostFX wiring.");
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            InstallCamcorderFrame();
            InstallGlimpseOutline();
            var child = parent.Find("PostFX Service");
            var owner = child != null ? child.gameObject : new GameObject("PostFX Service");
            owner.transform.SetParent(parent, false);
            var driver = owner.GetComponent<PostFXDriver>();
            if (driver == null) driver = owner.AddComponent<PostFXDriver>();
            driver.ConfigureForSetup();
            var manager = owner.GetComponent<PostFXManager>();
            if (manager == null) manager = owner.AddComponent<PostFXManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_driver").objectReferenceValue = driver;
            serialized.FindProperty("_config").objectReferenceValue = PostFXConfigGenerator.LoadOrCreateConfig();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(manager);
            return manager;
        }
    }
}

