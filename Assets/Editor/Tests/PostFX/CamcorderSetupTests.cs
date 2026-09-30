// ============================================================================
// CamcorderSetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks retained renderer and camera material setup without touching production assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX/Camera integration.
// KEY RESPONSIBILITIES:
//   - Preserve existing renderer features and stable camcorder subasset identity.
//   - Retain the hand material and shader as a build-reachable config reference.
// DEPENDENCIES:
//   PostFX/Camera setup, URP, UnityEditor and NUnit.
// USAGE NOTES:
//   Coordinator-run Edit Mode under the Unity lease; deletes only owned temporary assets.
// ============================================================================
using System.Linq;
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using Worsen.Editor.PostFX;
using Worsen.Editor.Camera;

using Worsen.Presentation.Camera;
namespace Worsen.Tests.PostFX
{
    public sealed class CamcorderSetupTests
    {
        [Test]
        public void RendererSetupPreservesFeaturesAndRetainsOneShaderBoundFrame()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/Tests/PostFX/TemporaryCamcorderRenderer.asset");
            // Tests deliberately have no URP assembly dependency. Inspect its serialized seam.
            var renderer = ScriptableObject.CreateInstance(Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime", true));
            AssetDatabase.CreateAsset(renderer, path);
            try
            {
                var existing = ScriptableObject.CreateInstance(Type.GetType(
                    "UnityEngine.Rendering.Universal.FullScreenPassRendererFeature, Unity.RenderPipelines.Universal.Runtime", true));
                existing.name = "Existing feature"; AssetDatabase.AddObjectToAsset(existing, renderer);
                var data = new SerializedObject(renderer);
                var features = data.FindProperty("m_RendererFeatures");
                features.arraySize = 1; features.GetArrayElementAtIndex(0).objectReferenceValue = existing;
                data.ApplyModifiedPropertiesWithoutUndo();
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(PostFXSetup.ShaderPath);
                Assert.That(shader, Is.Not.Null);
                var setup = typeof(PostFXSetup).GetMethod("EnsureCamcorderFeature");
                var first = (UnityEngine.Object)setup.Invoke(null, new object[] { renderer, shader });
                var second = (UnityEngine.Object)setup.Invoke(null, new object[] { renderer, shader });
                Assert.That(second, Is.SameAs(first));
                data.Update(); features = data.FindProperty("m_RendererFeatures");
                Assert.That(features.arraySize, Is.EqualTo(2));
                Assert.That(features.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(existing));
                Assert.That(features.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(first));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string _, out long localId), Is.True);
                var map = data.FindProperty("m_RendererFeatureMap");
                Assert.That(map.arraySize, Is.EqualTo(2));
                Assert.That(map.GetArrayElementAtIndex(1).longValue, Is.EqualTo(localId));
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Count(a => a.GetType() == first.GetType()), Is.EqualTo(1));
                Assert.That(new SerializedObject(first).FindProperty("_shader").objectReferenceValue, Is.EqualTo(shader));
                Assert.That(AssetDatabase.IsSubAsset(first), Is.True);
                Assert.That(new SerializedObject(first).FindProperty("m_Active").boolValue, Is.True);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
        [Test]
        public void CameraSetupRetainsBuildSafeHandMaterialAcrossRepeatedRuns()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/Tests/PostFX/TemporaryHandConfig.asset");
            var config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            AssetDatabase.CreateAsset(config, path);
            try
            {
                var first = CameraConfigGenerator.EnsureHandMaterial(config);
                Assert.That(CameraConfigGenerator.EnsureHandMaterial(config), Is.SameAs(first));
                Assert.That(AssetDatabase.IsSubAsset(first), Is.True);
                Assert.That(first.shader, Is.Not.Null);
                Assert.That(new SerializedObject(config).FindProperty("_handMaterial").objectReferenceValue, Is.EqualTo(first));
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Count(), Is.EqualTo(1));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
