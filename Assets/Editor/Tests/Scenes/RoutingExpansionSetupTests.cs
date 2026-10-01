// ============================================================================
// RoutingExpansionSetupTests.cs
// ============================================================================
// PURPOSE:
//   Protects wave-two renderer, mixer and title build-order setup contracts.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Scenes.
// KEY RESPONSIBILITIES:
//   - Retain existing features and one shader-bound hunter-only Glimpse subasset.
//   - Bind both soundscape mixer groups and preserve title-first test-scene ordering.
//   - Repair missing mixer views without replacing groups or valid authored views.
// DEPENDENCIES:
//   Editor setup, Presentation Audio, UnityEditor, NUnit; URP via serialized seams.
// USAGE NOTES:
//   Coordinator-run Edit Mode; unique temporary assets are removed in finally blocks.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Editor.Audio;
using Worsen.Editor.PostFX;
using Worsen.Editor.Scenes;
using Worsen.Presentation.Audio;
namespace Worsen.Tests.Scenes
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RoutingExpansionSetupTests
    {
        [Test] public void GlimpseSetupPreservesCamcorderAndRetainsShaderAndHunterOnlyMask()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/Tests/Scenes/TemporaryGlimpseRenderer.asset");
            var renderer = ScriptableObject.CreateInstance(Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime", true));
            AssetDatabase.CreateAsset(renderer, path);
            try
            {
                var frame = typeof(PostFXSetup).GetMethod("EnsureCamcorderFeature").Invoke(null,
                    new object[] { renderer, AssetDatabase.LoadAssetAtPath<Shader>(PostFXSetup.ShaderPath) });
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(PostFXSetup.GlimpseShaderPath);
                int layer = LayerMask.NameToLayer("HunterBody"); Assert.That(layer, Is.GreaterThanOrEqualTo(0));
                var setup = typeof(PostFXSetup).GetMethod("EnsureGlimpseFeature");
                object[] args = { renderer, shader, (LayerMask)(1 << layer) };
                var first = (UnityEngine.Object)setup.Invoke(null, args);
                Assert.That(setup.Invoke(null, args), Is.SameAs(first));
                var data = new SerializedObject(renderer); var features = data.FindProperty("m_RendererFeatures");
                Assert.That(features.arraySize, Is.EqualTo(2));
                Assert.That(features.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(frame));
                Assert.That(features.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(first));
                var feature = new SerializedObject(first);
                Assert.That(feature.FindProperty("_shader").objectReferenceValue, Is.EqualTo(shader));
                Assert.That(feature.FindProperty("_hunterLayers").intValue, Is.EqualTo(1 << layer));
                Assert.That(AssetDatabase.IsSubAsset(first), Is.True);
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Count(a => a.GetType() == first.GetType()), Is.EqualTo(1));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string _, out long id), Is.True);
                Assert.That(data.FindProperty("m_RendererFeatureMap").GetArrayElementAtIndex(1).longValue, Is.EqualTo(id));
                args[2] = (LayerMask)(-1);
                Assert.That(() => setup.Invoke(null, args), Throws.TypeOf<System.Reflection.TargetInvocationException>()
                    .With.InnerException.TypeOf<ArgumentException>());
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
        [Test] public void MixerSetupRepairsBothNullBindingsIdempotently()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/Tests/Scenes/TemporaryRouting.mixer");
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            try
            {
                var mixer = AudioMixerSetup.Configure(path, config);
                AssertCurrentView(mixer, 3);
                var data = new SerializedObject(config);
                var effects = data.FindProperty("_effectsGroup").objectReferenceValue;
                var music = data.FindProperty("_musicGroup").objectReferenceValue;
                Assert.That(effects, Is.Not.Null); Assert.That(music, Is.Not.Null); Assert.That(effects, Is.Not.SameAs(music));
                data.FindProperty("_effectsGroup").objectReferenceValue = null;
                data.FindProperty("_musicGroup").objectReferenceValue = null; data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(AudioMixerSetup.Configure(path, config), Is.SameAs(mixer)); data.Update();
                Assert.That(data.FindProperty("_effectsGroup").objectReferenceValue, Is.SameAs(effects));
                Assert.That(data.FindProperty("_musicGroup").objectReferenceValue, Is.SameAs(music));
                AssertCurrentView(mixer, 3);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); AssetDatabase.DeleteAsset(path); }
        }
        [TestCase(false)] [TestCase(true)]
        public void MixerSetupRepairsMissingOrInvalidViewWithoutReplacingGroups(bool missingView)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/Tests/Scenes/TemporaryRoutingView.mixer");
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            try
            {
                var mixer = AudioMixerSetup.Configure(path, config);
                string guid = AssetDatabase.AssetPathToGUID(path);
                var groups = mixer.FindMatchingGroups("").Select(group => group.GetInstanceID()).OrderBy(id => id).ToArray();
                var viewsProperty = MixerProperty(mixer, "views");
                var views = (Array)viewsProperty.GetValue(mixer);
                object authored = views.GetValue(0);
                authored.GetType().GetField("name").SetValue(authored, "Authored view");
                views.SetValue(authored, 0); viewsProperty.SetValue(mixer, views);
                if (missingView) viewsProperty.SetValue(mixer, Array.CreateInstance(views.GetType().GetElementType(), 0));
                else
                {
                    // Unity validates the index on assignment and logs this error; corrupting it is the point.
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Invalid view index"));
                    MixerProperty(mixer, "currentViewIndex").SetValue(mixer, views.Length);
                }
                Assert.That(AudioMixerSetup.Configure(path, config), Is.SameAs(mixer));
                AssertCurrentView(mixer, 3);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(mixer.FindMatchingGroups("").Select(group => group.GetInstanceID()).OrderBy(id => id), Is.EqualTo(groups));
                if (!missingView)
                {
                    var repaired = (Array)viewsProperty.GetValue(mixer);
                    Assert.That(repaired.Length, Is.EqualTo(views.Length));
                    var retained = repaired.GetValue(0);
                    Assert.That(retained.GetType().GetField("name").GetValue(retained), Is.EqualTo("Authored view"));
                    Assert.That((Array)retained.GetType().GetField("guids").GetValue(retained),
                        Is.EqualTo((Array)authored.GetType().GetField("guids").GetValue(authored)),
                        "Repair must not overwrite an authored view's group identities.");
                }
                Assert.That(AudioMixerSetup.Configure(path, config), Is.SameAs(mixer));
                AssertCurrentView(mixer, 3);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); AssetDatabase.DeleteAsset(path); }
        }
        private static PropertyInfo MixerProperty(UnityEngine.Audio.AudioMixer mixer, string name)
            => mixer.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static void AssertCurrentView(UnityEngine.Audio.AudioMixer mixer, int groupCount)
        {
            var views = (Array)MixerProperty(mixer, "views").GetValue(mixer);
            int index = (int)MixerProperty(mixer, "currentViewIndex").GetValue(mixer);
            Assert.That(index, Is.InRange(0, views.Length - 1));
            var view = views.GetValue(index);
            var guids = (Array)view.GetType().GetField("guids").GetValue(view);
            Assert.That(guids.Length, Is.EqualTo(groupCount));
            Assert.That(guids.Cast<object>().Distinct().Count(), Is.EqualTo(groupCount));
        }
        [Test] public void DuplicateTitleEntriesCollapseWithoutDroppingOrEnablingTestScenes()
        {
            var scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/TagArena.unity", false),
                new EditorBuildSettingsScene(HorrorRunSceneSetup.ScenePath, false),
                new EditorBuildSettingsScene("Assets/Scenes/FloorLoop.unity", true),
                new EditorBuildSettingsScene(HorrorRunSceneSetup.ScenePath, true) };
            var ordered = HorrorRunSceneSetup.OrderedBuildScenes(scenes);
            Assert.That(ordered.Select(x => x.path), Is.EqualTo(new[] {
                HorrorRunSceneSetup.ScenePath, scenes[0].path, scenes[2].path }));
            Assert.That(ordered.Select(x => x.enabled), Is.EqualTo(new[] { true, false, true }));
            Assert.That(HorrorRunSceneSetup.OrderedBuildScenes(ordered).Select(x => (x.path, x.enabled)),
                Is.EqualTo(ordered.Select(x => (x.path, x.enabled))));
        }
    }
}
