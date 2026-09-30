// ============================================================================
// HorrorRunFogWiringTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the targeted HorrorRun fog setup is repeatable without replacing
//   authored references. The real installer must retain every unrelated feature.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Scenes.
// KEY RESPONSIBILITIES:
//   - Check one manager, route and renderer feature after repeated restoration.
//   - Preserve custom config tuning, component identities and distance-fog features.
// DEPENDENCIES:
//   HorrorRunSceneSetup, FogSpikeSetup, Presentation Fog, UnityEditor and NUnit.
//   Renderer wiring is inspected through serialized fields, not a new URP reference.
// USAGE NOTES:
//   Coordinator-only Edit Mode check under the Unity lease. Uses a disposable
//   additive scene, but the real installer creates/saves its documented config
//   assets and PC_Renderer feature. No scene or unrelated asset is saved.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Editor.Fog;
using Worsen.Editor.Scenes;
using Worsen.Orchestrator;
using Worsen.Presentation.Fog;

namespace Worsen.Tests.Scenes
{
    public sealed class HorrorRunFogWiringTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RestoreTwicePreservesReferencesAndOneRendererFeature(bool authored)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            FogDriverConfig custom = null;
            try
            {
                SceneManager.SetActiveScene(scene);
                var owner = new GameObject("Fog wiring test"); owner.SetActive(false);
                var root = owner.AddComponent<HorrorRunSceneRoot>();
                var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(FogSpikeSetup.RendererPath);
                Assert.That(renderer, Is.Not.Null);
                var otherFeatures = Features(renderer).Where(value => !IsFog(value)).ToArray();
                if (authored)
                {
                    custom = ScriptableObject.CreateInstance<FogDriverConfig>();
                    var fog = owner.AddComponent<FogManager>();
                    var route = owner.AddComponent<FogOrchestrator>();
                    HorrorRunSceneSetup.Wire(root, "_fog", fog);
                    HorrorRunSceneSetup.Wire(root, "_fogConfig", custom);
                    HorrorRunSceneSetup.Wire(root, "_fogRoute", route);
                    HorrorRunSceneSetup.Wire(fog, "_config", custom);
                }
                HorrorRunSceneSetup.RestoreFog(root);
                var manager = Reference<FogManager>(root, "_fog");
                var orchestrator = Reference<FogOrchestrator>(root, "_fogRoute");
                var config = Reference<FogDriverConfig>(root, "_fogConfig");
                var driver = Reference<FogDriver>(manager, "_driver");
                Assert.That(config, Is.SameAs(authored ? custom : FogSpikeSetup.Config));
                string tuning = JsonUtility.ToJson(config);
                string configGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(FogSpikeSetup.Config));
                var feature = Features(renderer).Single(IsFog);
                HorrorRunSceneSetup.RestoreFog(root);
                Assert.That(Reference<FogManager>(root, "_fog"), Is.SameAs(manager));
                Assert.That(Reference<FogOrchestrator>(root, "_fogRoute"), Is.SameAs(orchestrator));
                Assert.That(Reference<FogDriverConfig>(root, "_fogConfig"), Is.SameAs(config));
                Assert.That(Reference<FogDriverConfig>(manager, "_config"), Is.SameAs(config));
                Assert.That(Reference<FogDriver>(manager, "_driver"), Is.SameAs(driver));
                Assert.That(driver, Is.Not.Null);
                Assert.That(JsonUtility.ToJson(config), Is.EqualTo(tuning));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(FogSpikeSetup.Config)), Is.EqualTo(configGuid));
                Assert.That(scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<FogManager>(true)).Count(), Is.EqualTo(1));
                Assert.That(scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<FogOrchestrator>(true)).Count(), Is.EqualTo(1));
                Assert.That(Features(renderer).Single(IsFog), Is.SameAs(feature));
                Assert.That(new SerializedObject(feature).FindProperty("m_Active").boolValue, Is.True);
                Assert.That(Features(renderer).Where(value => !IsFog(value)), Is.EqualTo(otherFeatures));
                // Lost root references must reuse existing inactive scene components, not duplicate them.
                HorrorRunSceneSetup.Wire(root, "_fog", null); HorrorRunSceneSetup.Wire(root, "_fogRoute", null);
                HorrorRunSceneSetup.RestoreFog(root);
                Assert.That(Reference<FogManager>(root, "_fog"), Is.SameAs(manager));
                Assert.That(Reference<FogOrchestrator>(root, "_fogRoute"), Is.SameAs(orchestrator));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (custom != null) Object.DestroyImmediate(custom);
            }
        }
        private static T Reference<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
        private static bool IsFog(Object value) => value != null && value.GetType().FullName == "Worsen.Presentation.Fog.FogRendererFeature";
        private static Object[] Features(Object renderer)
        {
            var property = new SerializedObject(renderer).FindProperty("m_RendererFeatures");
            return Enumerable.Range(0, property.arraySize).Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        }
    }
}
