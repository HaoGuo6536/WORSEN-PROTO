// ============================================================================
// AudioMixerSetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks repeatable mixer authoring and real runtime preference routing.
//   A temporary mixer is created only when the coordinator runs this Edit Mode fixture.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Preserve asset/group identity and exposed parameters on repeated setup.
//   - Verify mixer gains replace, rather than multiply, source preference fallbacks.
// DEPENDENCIES:
//   - Audio editor setup, Presentation Audio, NUnit and Unity asset/audio APIs.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Cleanup deletes only the unique fixture asset.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Audio;
using Worsen.Editor.Audio;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Audio
{
    public sealed class AudioMixerSetupTests
    {
        [Test] public void SetupIsIdempotentAndPreferencesUseAssignedMixerWithoutDoubleAttenuation()
        {
            string path = "Assets/Editor/Tests/Audio/MixerFixture-" + Guid.NewGuid().ToString("N") + ".mixer";
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var ownerConfig = ScriptableObject.CreateInstance<AudioDriverConfig>();
            var owner = new GameObject("Mixer preference fixture");
            try
            {
                var mixer = AudioMixerSetup.Configure(path, config);
                string identity = AssetDatabase.AssetPathToGUID(path);
                var groups = mixer.FindMatchingGroups("").Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray();
                Assert.That(groups.Length, Is.EqualTo(3));
                Assert.That(AudioMixerSetup.Configure(path, config), Is.SameAs(mixer));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(identity));
                CollectionAssert.AreEqual(groups, mixer.FindMatchingGroups("").Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray());
                var serialized = new SerializedObject(ownerConfig); serialized.FindProperty("_soundscape").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
                var driver = owner.AddComponent<AudioDriver>(); driver.Initialize(ownerConfig); driver.SetOwnerEnabled(true);
                var settings = new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, .5f, .25f, .75f);
                driver.ApplySettings(settings);
                var volume = new AudioVolumePresenter();
                Assert.That(mixer.GetFloat("MasterVolume", out float master), Is.True); Assert.That(master, Is.EqualTo(volume.Decibels(.5f)).Within(.001f));
                Assert.That(mixer.GetFloat("MusicVolume", out float music), Is.True); Assert.That(music, Is.EqualTo(volume.Decibels(.25f)).Within(.001f));
                Assert.That(mixer.GetFloat("EffectsVolume", out float effects), Is.True); Assert.That(effects, Is.EqualTo(volume.Decibels(.75f)).Within(.001f));
                Assert.That(owner.GetComponentInChildren<AudioSoundscapeDriver>().EffectsSourceGain, Is.EqualTo(ownerConfig.MasterGain));
                foreach (var source in owner.GetComponentsInChildren<AudioSource>()) Assert.That(source.outputAudioMixerGroup, Is.Not.Null);
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(ownerConfig); Object.DestroyImmediate(config); AssetDatabase.DeleteAsset(path); }
        }
        [Test] public void MissingMixerKeepsSourceFallbackAndZeroIsSilence()
        {
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>(); var owner = new GameObject("Fallback volume fixture");
            try
            {
                var driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(config);
                driver.ApplySettings(new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, .5f, .25f, .75f), .8f);
                Assert.That(driver.EffectsSourceGain, Is.EqualTo(.3f).Within(.000001f));
                Assert.That(new AudioVolumePresenter().Decibels(0f), Is.EqualTo(-80f)); driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
    }
}
