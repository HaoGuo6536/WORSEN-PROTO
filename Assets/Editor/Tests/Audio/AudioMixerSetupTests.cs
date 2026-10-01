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
//   - Diagnose direct native Edit Mode readback independently of preference routing.
// DEPENDENCIES:
//   - Audio editor setup, Presentation Audio, NUnit and Unity asset/audio APIs.
// USAGE NOTES:
//   Coordinator holds the Unity lease. Cleanup deletes only the unique fixture asset.
//   Unity's AudioMixer API documents exposed values and snapshot ownership, not an
//   Edit Mode readback guarantee. Request assertions verify calls and acceptance;
//   the direct native probe reports readback without claiming audible runtime gain.
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
            AudioDriver driver = null;
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
                driver = owner.AddComponent<AudioDriver>(); driver.Initialize(ownerConfig); driver.SetOwnerEnabled(true);
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
                Assert.That(request.MasterAccepted && request.MusicAccepted && request.EffectsAccepted, Is.True);
                Assert.That(soundscape.EffectsSourceGain, Is.EqualTo(ownerConfig.MasterGain));
                foreach (var source in owner.GetComponentsInChildren<AudioSource>())
                {
                    Assert.That(source.outputAudioMixerGroup, Is.Not.Null);
                    Assert.That(source.outputAudioMixerGroup.audioMixer, Is.SameAs(mixer));
                }
                // An independent SetFloat call distinguishes native readback from a missing driver call.
                Assert.That(mixer.GetFloat("MasterVolume", out float before), Is.True);
                float probe = volume.Decibels(.25f);
                Assert.That(mixer.SetFloat("MasterVolume", probe), Is.True);
                Assert.That(mixer.GetFloat("MasterVolume", out float after), Is.True);
                TestContext.WriteLine("Edit Mode direct MasterVolume probe: requested={0}, before={1}, after={2}, reflected={3}",
                    probe, before, after, Mathf.Abs(after - probe) <= .001f);
                driver.ApplySettings(settings);
                Assert.That(soundscape.EffectsSourceGain, Is.EqualTo(ownerConfig.MasterGain), "Repeated preferences must not compound source gain.");
                Assert.That(mixer.GetFloat("MusicVolume", out float music), Is.True);
                Assert.That(mixer.GetFloat("EffectsVolume", out float effects), Is.True);
                if (Mathf.Abs(after - probe) <= .001f)
                {
                    Assert.That(mixer.GetFloat("MasterVolume", out float master), Is.True);
                    Assert.That(master, Is.EqualTo(request.Master).Within(.001f));
                    Assert.That(music, Is.EqualTo(request.Music).Within(.001f));
                    Assert.That(effects, Is.EqualTo(request.Effects).Within(.001f));
                }
            }
            finally { if (driver != null) driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(ownerConfig); Object.DestroyImmediate(config); AssetDatabase.DeleteAsset(path); }
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
