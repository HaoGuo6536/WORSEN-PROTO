// ============================================================================
// AudioMixerSetupTests.cs
// ============================================================================
// PURPOSE:
//   Checks repeatable mixer authoring and the real preference driver's mixer requests.
//   A temporary mixer is created only when the coordinator runs this Edit Mode fixture.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Preserve asset/group identity and exposed parameters on repeated setup.
//   - Verify mixer gains replace, rather than multiply, source preference fallbacks.
//   - Check rejected writes use each source preference once, including repeat requests.
// DEPENDENCIES:
//   - Audio editor setup, Presentation Audio, NUnit and Unity asset/audio APIs.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Cleanup deletes only the unique fixture asset.
//   Edit Mode may reject exposed parameter writes. Assertions inspect requests and
//   select fallback expectations from each native result, not mixer readback.
//   AudioMixerPlayModeTests separately requires accepted writes and native readback.
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioMixerSetupTests
    {
        [Test] public void SetupIsIdempotentAndPreferencesUseAssignedMixerWithoutDoubleAttenuation()
        {
            string path = "Assets/Editor/Tests/Audio/MixerFixture-" + Guid.NewGuid().ToString("N") + ".mixer";
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var ownerConfig = ScriptableObject.CreateInstance<AudioDriverConfig>();
            var owner = new GameObject("Mixer preference fixture");
            var clip = AudioClip.Create("Mixer routing fixture", 480, 1, 48000, false);
            AudioDriver driver = null;
            try
            {
                var mixer = AudioMixerSetup.Configure(path, config);
                string identity = AssetDatabase.AssetPathToGUID(path);
                var groups = mixer.FindMatchingGroups("").Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray();
                Assert.That(groups.Length, Is.EqualTo(4));
                Assert.That(config.AmbienceGroup.name, Is.EqualTo("Ambience"));
                Assert.That(config.AmbienceGroup, Is.Not.SameAs(config.EffectsGroup));
                CollectionAssert.AreEquivalent(new[] { config.EffectsGroup, config.AmbienceGroup }, mixer.FindMatchingGroups("Effects"));
                var exposed = new SerializedObject(mixer).FindProperty("m_ExposedParameters");
                Assert.That(exposed.arraySize, Is.EqualTo(4));
                var parameterNames = Enumerable.Range(0, exposed.arraySize)
                    .Select(i => exposed.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue).ToArray();
                CollectionAssert.Contains(parameterNames, "AmbienceVolume");
                Assert.That(AudioMixerSetup.Configure(path, config), Is.SameAs(mixer));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(identity));
                CollectionAssert.AreEqual(groups, mixer.FindMatchingGroups("").Select(g => g.GetInstanceID()).OrderBy(id => id).ToArray());
                var soundscapeConfig = new SerializedObject(config);
                soundscapeConfig.FindProperty("_runIntro").objectReferenceValue = clip;
                soundscapeConfig.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(ownerConfig); serialized.FindProperty("_soundscape").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
                driver = owner.AddComponent<AudioDriver>(); driver.Initialize(ownerConfig); driver.SetOwnerEnabled(true);
                Assert.That(driver.EffectsGroup, Is.SameAs(config.EffectsGroup));
                foreach (var ambience in owner.GetComponentsInChildren<AudioSource>().Where(s => s.name == "Interior" || s.name == "Exterior"))
                    Assert.That(ambience.outputAudioMixerGroup, Is.SameAs(config.AmbienceGroup));
                var settings = new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, .5f, .25f, .75f);
                driver.ApplySettings(settings);
                var volume = new AudioVolumePresenter();
                var soundscape = owner.GetComponentInChildren<AudioSoundscapeDriver>();
                Assert.That(soundscape.MixerVolumeRequest.HasValue, Is.True);
                var request = soundscape.MixerVolumeRequest.Value;
                Assert.That(request.Mixer, Is.SameAs(mixer));
                Assert.That(config.MasterParameter, Is.EqualTo("MasterVolume"));
                Assert.That(config.MusicParameter, Is.EqualTo("MusicVolume"));
                Assert.That(config.EffectsParameter, Is.EqualTo("EffectsVolume"));
                Assert.That(request.Master, Is.EqualTo(volume.Decibels(.5f)).Within(.001f));
                Assert.That(request.Music, Is.EqualTo(volume.Decibels(.25f)).Within(.001f));
                Assert.That(request.Effects, Is.EqualTo(volume.Decibels(.75f)).Within(.001f));
                foreach (var source in owner.GetComponentsInChildren<AudioSource>())
                {
                    Assert.That(source.outputAudioMixerGroup, Is.Not.Null);
                    Assert.That(source.outputAudioMixerGroup.audioMixer, Is.SameAs(mixer));
                }
                var musicSource = owner.GetComponentsInChildren<AudioSource>().Single(s => s.name == "Run Intro");
                Assert.That(musicSource.outputAudioMixerGroup, Is.SameAs(config.MusicGroup));
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    request = soundscape.MixerVolumeRequest.Value;
                    float masterGain = ownerConfig.MasterGain * (request.MasterAccepted ? 1f : settings.MasterVolume);
                    float effectsGain = masterGain * (request.EffectsAccepted ? 1f : settings.EffectsVolume);
                    float musicGain = masterGain * (request.MusicAccepted ? 1f : settings.MusicVolume);
                    Assert.That(soundscape.EffectsSourceGain, Is.EqualTo(effectsGain).Within(.000001f),
                        "Rejected preferences apply once at the source; accepted preferences do not apply there.");
                    Assert.That(musicSource.volume, Is.EqualTo(musicGain * config.MusicGain * config.RunGain).Within(.000001f),
                        "Repeated preferences must replace, not compound, music fallback.");
                    if (repeat == 0) driver.ApplySettings(settings);
                }
            }
            finally { if (driver != null) driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(ownerConfig); Object.DestroyImmediate(config); Object.DestroyImmediate(clip); AssetDatabase.DeleteAsset(path); }
        }
        [Test] public void MissingMixerKeepsSourceFallbackAndZeroIsSilence()
        {
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>(); var owner = new GameObject("Fallback volume fixture");
            AudioSoundscapeDriver driver = null;
            try
            {
                driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(config);
                driver.ApplySettings(new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, .5f, .25f, .75f), .8f);
                Assert.That(driver.EffectsSourceGain, Is.EqualTo(.3f).Within(.000001f));
                Assert.That(driver.MixerVolumeRequest.HasValue, Is.False);
                Assert.That(new AudioVolumePresenter().Decibels(0f), Is.EqualTo(-80f));
            }
            finally { if (driver != null) driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(config); }
        }
        [TestCase(0f, -80f)] [TestCase(.5f, -6.02060032f)] [TestCase(1f, 0f)]
        public void VolumePreferenceUsesExactlyOneAttenuationPath(float preference, float decibels)
        {
            var volume = new AudioVolumePresenter();
            Assert.That(volume.Decibels(preference), Is.EqualTo(decibels).Within(.001f));
            Assert.That(volume.SourceGain(preference, true), Is.EqualTo(1f));
            Assert.That(volume.SourceGain(preference, false), Is.EqualTo(preference));
        }
    }
}
