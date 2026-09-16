// ============================================================================
// AudioChaseMusicPlaybackTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the run sequence on real Unity AudioSources with short generated clips.
//   Separate saved-asset assertions bind this transport to the six user-selected imports.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Observe intro-to-loop playback, multi-hunter continuity and one ending.
//   - Verify early loss, reacquisition, death and reset cancel scheduled sources.
// DEPENDENCIES:
//   - AudioSoundscapeDriver, configuration, Unity Test Framework and NUnit.
// USAGE NOTES:
//   Runs under the Unity lease. Fixture clips are transient and never saved.
//   Source timing is playback evidence, not a claim of human listening approval.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Presentation.Audio;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Audio
{
    public sealed class AudioChaseMusicPlaybackTests
    {
        private GameObject owner;
        private AudioSoundscapeDriverConfig config;
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private AudioSoundscapeDriver driver;
        [Test] public void ProductionConfigUsesAllSixRequestedImportsWithoutOldMusic()
        {
            var saved = AssetDatabase.LoadAssetAtPath<AudioSoundscapeDriverConfig>(Worsen.Editor.Audio.HorrorAudioSetup.ConfigPath);
            Assert.That(saved, Is.Not.Null);
            string[] names = { "Amb_Deep_impacts", "Amb_Deep_impacts_stress", "Amb_claustrophobia", "Amb_Run_1", "Amb_Run_2", "Amb_Run_End" };
            AudioClip[] actual = { saved.TensionStem, saved.ChaseStem, saved.DangerStem, saved.RunIntro, saved.RunLoop, saved.RunEnd };
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(actual[i], Is.Not.Null); Assert.That(actual[i].name, Is.EqualTo(names[i]));
                Assert.That(actual[i].samples, Is.GreaterThan(0));
                Assert.That(AssetDatabase.GetAssetPath(actual[i]), Does.StartWith("Assets/External/Audio/Horror Elements/Ambient/"));
            }
        }
        [UnityTest] public IEnumerator IntroThenLoopSurvivesAnotherHunterAndEndingPlaysExactlyOnce()
        {
            yield return new EnterPlayMode(); Setup();
            driver.SetThreat(1, true, .9f);
            yield return WaitFor(() => Source("Run Intro").timeSamples > 0, "Intro audible timeline");
            yield return WaitFor(() => Source("Run Loop").timeSamples > 0, "Scheduled loop timeline");
            Assert.That(Source("Run Intro").isPlaying, Is.False);
            Assert.That(driver.DangerGain * config.MusicGain, Is.EqualTo(Source("Danger").volume).Within(.000001f));
            Assert.That(driver.ChaseGain * config.MusicGain, Is.EqualTo(Source("Chase").volume).Within(.000001f));
            driver.SetThreat(2, true, .2f); driver.RemoveThreat(1);
            yield return Delay(.3f);
            Assert.That(Source("Run Loop").isPlaying, Is.True); Assert.That(Source("Run Intro").isPlaying, Is.False);
            driver.RemoveThreat(2);
            yield return WaitFor(() => Source("Run End").timeSamples > 0, "Single ending");
            Assert.That(Source("Run Loop").isPlaying, Is.False); Assert.That(Source("Run End").loop, Is.False);
            yield return Delay(.4f); Assert.That(Source("Run End").isPlaying, Is.False);
            yield return Delay(.25f); Assert.That(Source("Run End").isPlaying, Is.False);
        }
        [UnityTest] public IEnumerator EarlyLossReacquisitionDeathAndResetCancelQueuedLoopAndOutro()
        {
            yield return new EnterPlayMode(); Setup();
            driver.SetThreat(1, true, 1f);
            yield return WaitFor(() => Source("Run Intro").timeSamples > 0, "Early intro");
            driver.RemoveThreat(1);
            yield return Delay(.8f);
            Assert.That(Source("Run Loop").isPlaying, Is.False); Assert.That(Source("Run End").isPlaying, Is.False);
            driver.SetThreat(2, true, 1f);
            yield return WaitFor(() => Source("Run Intro").timeSamples > 0, "Fresh intro");
            driver.SetAlive(false); yield return Delay(.7f);
            Assert.That(RunSources().Any(s => s.isPlaying), Is.False, "Death must cancel scheduled playback and not emit an ending.");
            driver.ResetRun(); yield return Delay(.2f);
            Assert.That(RunSources().Any(s => s.isPlaying), Is.False);
            driver.SetThreat(3, true, 1f); yield return WaitFor(() => Source("Run Intro").timeSamples > 0, "Reset intro");
            driver.SetOwnerEnabled(false); yield return Delay(.7f);
            Assert.That(RunSources().Any(s => s.isPlaying), Is.False);
            driver.SetOwnerEnabled(true); yield return Delay(.2f);
            Assert.That(RunSources().Any(s => s.isPlaying), Is.False, "Enable after owner reset must not retain a hunter.");
        }
        private void Setup()
        {
            owner = new GameObject("Chase sequence fixture"); config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            string[] fields = { "_tensionStem", "_chaseStem", "_dangerStem", "_runIntro", "_runLoop", "_runEnd" };
            int[] lengths = { 6000, 9000, 12000, 24000, 12000, 7200 };
            var data = new SerializedObject(config);
            for (int i = 0; i < fields.Length; i++)
            {
                AudioClip clip = AudioClip.Create("Sequence fixture " + i, lengths[i], 1, 48000, false);
                var samples = new float[lengths[i]];
                for (int j = 0; j < samples.Length; j++) samples[j] = .01f * Mathf.Sin(j * .025f);
                clip.SetData(samples, 0); clips.Add(clip); data.FindProperty(fields[i]).objectReferenceValue = clip;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(config); driver.SetOwnerEnabled(true);
            Assert.That(Source("Danger").clip, Is.SameAs(config.DangerStem), "Independent duration must not disable Claustrophobia.");
        }
        private AudioSource Source(string label) => owner.GetComponentsInChildren<AudioSource>().Single(s => s.name == label);
        private IEnumerable<AudioSource> RunSources() => owner.GetComponentsInChildren<AudioSource>().Where(s => s.name.StartsWith("Run "));
        private IEnumerator WaitFor(Func<bool> condition, string context)
        {
            double until = Time.realtimeSinceStartupAsDouble + 4;
            while (!condition() && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(condition(), Is.True, context);
        }
        private IEnumerator Delay(float seconds)
        { double until = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < until) yield return null; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (driver != null) driver.Teardown(); if (owner != null) Object.DestroyImmediate(owner);
            if (config != null) Object.DestroyImmediate(config);
            foreach (AudioClip clip in clips) if (clip != null) Object.DestroyImmediate(clip); clips.Clear();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
