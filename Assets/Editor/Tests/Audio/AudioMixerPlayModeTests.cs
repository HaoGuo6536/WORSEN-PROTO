// ============================================================================
// AudioMixerPlayModeTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that the assigned project mixer accepts runtime preference writes.
//   Native readback and unity source preference gain prove the accepted path that
//   Edit Mode cannot establish, without claiming human listening acceptance.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Require accepted Master/Music/Effects writes and matching native readback.
//   - Check assigned routing and no repeated preference attenuation at sources.
//   - Restore mixer values and release all transient fixture objects before exiting play.
// DEPENDENCIES:
//   - Presentation Audio, Core settings, AudioMixerSetup's asset path and Unity Test Framework.
// USAGE NOTES:
//   Coordinator runs this single-entry Play Mode fixture under the Unity lease.
//   Configs, clip and sources are created after domain reload; no assets are saved.
//   Designer gains are explicitly unity to isolate preference attenuation.
//   Runtime mixer values and background policy are restored in finally; UnityTearDown
//   exits Play Mode even on assertion failure. Entry can cost about 100 seconds.
// ============================================================================
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Editor.Audio;
using Worsen.Presentation.Audio;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class AudioMixerPlayModeTests
    {
        [UnityTest]
        public IEnumerator PreferencesAreAcceptedReadBackAndUseUnitySourceGainInPlayMode()
        {
            yield return new EnterPlayMode();
            yield return VerifyRuntimePreferences();
        }

        private static IEnumerator VerifyRuntimePreferences()
        {
            Assert.That(Application.isPlaying, Is.True);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixerSetup.MixerPath);
            Assert.That(mixer, Is.Not.Null, "Run the coordinator's mixer setup before this acceptance test.");
            var parameters = new[] { "MasterVolume", "MusicVolume", "EffectsVolume" };
            var previous = new float[parameters.Length];
            int captured = 0;
            bool background = Application.runInBackground;
            GameObject owner = null;
            AudioDriver driver = null;
            AudioDriverConfig ownerConfig = null;
            AudioSoundscapeDriverConfig config = null;
            AudioClip clip = null;
            try
            {
                Application.runInBackground = true;
                // Run after startup callbacks, not in Awake/OnEnable where SetFloat is unsafe.
                yield return null;
                for (int i = 0; i < parameters.Length; i++)
                {
                    Assert.That(mixer.GetFloat(parameters[i], out previous[i]), Is.True, parameters[i]);
                    captured++;
                }
                config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
                ownerConfig = ScriptableObject.CreateInstance<AudioDriverConfig>();
                clip = AudioClip.Create("Runtime mixer fixture", 480, 1, 48000, false);
                var data = new SerializedObject(config);
                data.FindProperty("_mixer").objectReferenceValue = mixer;
                data.FindProperty("_musicGroup").objectReferenceValue = mixer.FindMatchingGroups("Music").Single();
                data.FindProperty("_effectsGroup").objectReferenceValue = mixer.FindMatchingGroups("Effects").Single();
                data.FindProperty("_ambienceGroup").objectReferenceValue = mixer.FindMatchingGroups("Effects").Single();
                data.FindProperty("_masterParameter").stringValue = parameters[0];
                data.FindProperty("_musicParameter").stringValue = parameters[1];
                data.FindProperty("_effectsParameter").stringValue = parameters[2];
                data.FindProperty("_musicGain").floatValue = 1f;
                data.FindProperty("_runGain").floatValue = 1f;
                data.FindProperty("_runIntro").objectReferenceValue = clip;
                data.ApplyModifiedPropertiesWithoutUndo();
                data = new SerializedObject(ownerConfig);
                data.FindProperty("_soundscape").objectReferenceValue = config;
                data.FindProperty("_masterGain").floatValue = 1f;
                data.ApplyModifiedPropertiesWithoutUndo();
                owner = new GameObject("Runtime mixer preference fixture");
                driver = owner.AddComponent<AudioDriver>();
                driver.Initialize(ownerConfig);
                driver.SetOwnerEnabled(true);
                var soundscape = owner.GetComponentInChildren<AudioSoundscapeDriver>();
                var music = owner.GetComponentsInChildren<AudioSource>().Single(s => s.name == "Run Intro");
                Assert.That(music.outputAudioMixerGroup, Is.SameAs(config.MusicGroup));
                foreach (var source in owner.GetComponentsInChildren<AudioSource>())
                {
                    Assert.That(source.outputAudioMixerGroup, Is.Not.Null);
                    Assert.That(source.outputAudioMixerGroup.audioMixer, Is.SameAs(mixer));
                }
                var volume = new AudioVolumePresenter();
                foreach (var settings in new[]
                {
                    new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, .5f, .25f, .75f),
                    new PlayerSettingsRecord(1, 1f, false, 90f, true, true, true, .25f, .75f, .5f)
                })
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    driver.ApplySettings(settings);
                    Assert.That(soundscape.MixerVolumeRequest.HasValue, Is.True);
                    var request = soundscape.MixerVolumeRequest.Value;
                    Assert.That(request.Mixer, Is.SameAs(mixer));
                    Assert.That(request.MasterAccepted, Is.True, "MasterVolume SetFloat rejected in Play Mode.");
                    Assert.That(request.MusicAccepted, Is.True, "MusicVolume SetFloat rejected in Play Mode.");
                    Assert.That(request.EffectsAccepted, Is.True, "EffectsVolume SetFloat rejected in Play Mode.");
                    var expected = new[] { volume.Decibels(settings.MasterVolume), volume.Decibels(settings.MusicVolume), volume.Decibels(settings.EffectsVolume) };
                    Assert.That(request.Master, Is.EqualTo(expected[0]).Within(.001f));
                    Assert.That(request.Music, Is.EqualTo(expected[1]).Within(.001f));
                    Assert.That(request.Effects, Is.EqualTo(expected[2]).Within(.001f));
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Assert.That(mixer.GetFloat(parameters[i], out float actual), Is.True, parameters[i]);
                        Assert.That(actual, Is.EqualTo(expected[i]).Within(.001f), parameters[i]);
                    }
                    Assert.That(soundscape.EffectsSourceGain, Is.EqualTo(1f).Within(.000001f));
                    Assert.That(music.volume, Is.EqualTo(1f).Within(.000001f), "The mixer owns preference attenuation, including repeated writes.");
                }
            }
            finally
            {
                try
                {
                    if (driver != null) driver.Teardown();
                    if (owner != null) Object.DestroyImmediate(owner);
                    if (ownerConfig != null) Object.DestroyImmediate(ownerConfig);
                    if (config != null) Object.DestroyImmediate(config);
                    if (clip != null) Object.DestroyImmediate(clip);
                }
                finally
                {
                    for (int i = 0; i < captured; i++) mixer.SetFloat(parameters[i], previous[i]);
                    Application.runInBackground = background;
                }
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreEditor()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
