// ============================================================================
// AudioRosterPlaybackTests.cs
// ============================================================================
// PURPOSE:
//   Exercises owned roster sources, effect readers and setup failure atomicity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Verify alternates, gain overrides, exact tells, warning retention and budgets.
//   - Verify the Mannequin snap and dedicated Herald source/pitch behavior.
//   - Verify Ear Plugs/Mirror Skin once-only timing without suppressing protected cues.
// DEPENDENCIES:
//   - Core, Audio, Audio editor setup, NUnit and transient Unity objects.
// USAGE NOTES:
//   Coordinator-only native Edit Mode fixture; does not save assets or scenes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Presentation.Audio;
using Worsen.Editor.Audio;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioRosterPlaybackTests
    {
        private readonly List<Object> owned = new List<Object>();
        private AudioSoundscapeDriver driver;
        private AudioSoundscapeDriverConfig config;
        private AudioClip first, second, snap;
        private AudioSoundscapeDriverState Pool => Read<AudioSoundscapeDriverState>(driver, "_state");
        private AudioSource[] Sources => Read<AudioSource[]>(driver, "_voices");
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>(); owned.Add(config);
            first = Clip("primary", 1); second = Clip("alternate", 1); snap = Clip("snap", 1);
            Set(config, "_rosterBindings", new[] {
                new AudioRosterBinding("echo.presence", CueId.Presence) { Clip = first, Alternates = new[] { second }, Gain = .4f, OverrideGain = true },
                new AudioRosterBinding("sb_mangled_scream_02", CueId.EnemyWindup) { Clip = first, Gain = .5f, OverrideGain = true },
                new AudioRosterBinding("mannequin.death", CueId.Death) { Clip = snap, Gain = .3f, OverrideGain = true },
                new AudioRosterBinding("hunter.death", CueId.Death) { Clip = first }
            });
            var go = new GameObject("Roster playback fixture"); owned.Add(go); driver = go.AddComponent<AudioSoundscapeDriver>();
            driver.Initialize(config); driver.SetOwnerEnabled(true); driver.SetInRun(true);
            driver.SetWorld(AudioWorldMixPresenterTests.Graph(), null);
        }
        [TearDown] public void Cleanup()
        {
            if (driver != null) driver.Teardown();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
        [Test] public void RosterAlternatesNeverRepeatAndNeverAllocateExtraSources()
        {
            var hunter = new EntityId(7); AudioClip previous = null;
            for (int tick = 1; tick <= 20; tick++)
            {
                driver.ObserveHunter(new HunterFeedbackEvent(hunter, "echo", HunterFeedbackKind.LightReaction, Vector3.zero, tick));
                int voice = Array.FindIndex(Pool.Voices, v => v.Remaining > 0f);
                Assert.That(voice, Is.GreaterThanOrEqualTo(0)); Assert.That(Sources[voice].clip, Is.Not.SameAs(previous));
                previous = Sources[voice].clip;
                Assert.That(Sources[voice].pitch, Is.InRange(.97f, 1.03f));
                Assert.That(Pool.VoiceGains[voice], Is.InRange(.38f, .42f));
                Assert.That(Pool.Voices.Count(v => v.Remaining > 0f), Is.EqualTo(1));
                driver.StopEmitter(7);
            }
            Assert.That(driver.OwnedVoiceCount, Is.EqualTo(config.VoiceCount));
        }
        [Test] public void HeraldAttackKeepsSourceAndPitchDespiteAlternatePlayerClue()
        {
            var hunter = new EntityId(7); var origin = Vector3.zero;
            var fact = new HeraldScreamFact(hunter, HeraldSound.Attack, "sb_mangled_scream_02", .8f,
                new NoiseEvent(hunter, origin, 1, 10), new NoiseEvent(hunter, Vector3.right * 8, 1, 10), 1, false);
            driver.ObserveHerald(fact); driver.ObserveHerald(fact);
            int index = Array.FindIndex(Pool.Voices, v => v.Remaining > 0f);
            Assert.That(Pool.Voices.Count(v => v.Remaining > 0f), Is.EqualTo(1));
            Assert.That(Sources[index].transform.position, Is.EqualTo(origin)); Assert.That(Sources[index].pitch, Is.EqualTo(1f));
            Assert.That(Pool.Voices[index].Catalogue.Protected, Is.True); Assert.That(Pool.VoiceGains[index], Is.EqualTo(.5f));
        }
        [Test] public void MannequinCatchPlaysQuietSnapAndNeverGenericFallback()
        {
            var hunter = new EntityId(7);
            driver.ObserveMannequin(new MannequinFact(hunter, MannequinFactKind.SilentSoundSet, 1));
            driver.ObserveHit(new HunterHit(hunter, new EntityId(1), 100, 2, default));
            Assert.That(driver.PlayDeath(), Is.True);
            int index = Array.FindIndex(Pool.Voices, v => v.Remaining > 0f);
            Assert.That(Sources[index].clip, Is.SameAs(snap)); Assert.That(Pool.VoiceGains[index], Is.EqualTo(.3f));
            driver.StopEmitter(7);
            Set(config, "_rosterBindings", new[] { new AudioRosterBinding("hunter.death", CueId.Death) { Clip = first } });
            LogAssert.Expect(LogType.Warning, "Roster cue 'mannequin.death' has no clip; silent placeholder.");
            Assert.That(driver.PlayDeath(), Is.False); Assert.That(driver.PlayDeath(), Is.False);
        }
        [Test] public void RawHunterEffectsApplyUpgradesOncePreserveProtectionAndClear()
        {
            var hunter = new EntityId(7); var player = new EntityId(1);
            driver.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("ear-plugs"), EffectKind.Upgrade, 1),
                new ActiveEffect(new EffectId("mirror-skin"), EffectKind.Upgrade, 1) }));
            var deafen = new HeraldDeafenFact(hunter, player, 1, default, 5, 1, 4, false);
            var hit = new BlinderHitFact(hunter, player, 1, 1, 6, true);
            driver.ObserveHeraldDeafen(deafen); driver.ObserveBlinderHit(hit);
            Assert.That(Pool.WorldMix.DeafenedRemaining, Is.EqualTo(2f)); Assert.That(Pool.WorldMix.MuffledRemaining, Is.EqualTo(3f));
            driver.TickEmbodiment(0, .5f); driver.ObserveHeraldDeafen(deafen); driver.ObserveBlinderHit(hit);
            Assert.That(Pool.WorldMix.DeafenedRemaining, Is.EqualTo(1.5f)); Assert.That(Pool.WorldMix.MuffledRemaining, Is.EqualTo(2.5f));
            var mix = new AudioWorldMixPresenter();
            Assert.That(mix.Mask(Pool.WorldMix, true, config), Is.EqualTo(1f)); Assert.That(mix.Cutoff(Pool.WorldMix, true, config), Is.EqualTo(22000f));
            driver.ClearSenses(); Assert.That(Pool.WorldMix.DeafenedRemaining + Pool.WorldMix.MuffledRemaining, Is.Zero);
            driver.SetActiveEffects(null);
            driver.ObserveHeraldDeafen(new HeraldDeafenFact(hunter, player, 2, default, 5, 1, 4, false));
            driver.ObserveBlinderHit(new BlinderHitFact(hunter, player, 2, 2, 6, false));
            Assert.That(Pool.WorldMix.DeafenedRemaining, Is.EqualTo(4f)); Assert.That(Pool.WorldMix.MuffledRemaining, Is.Zero);
        }
        [Test] public void MissingSetupAssetFailsBeforeChangingAnyBinding()
        {
            string before = JsonUtility.ToJson(config);
            string text = "| clip:a | Assets/External/WP-A-missing-asset.wav | 1 | -3 | -20 | fixture |\n" +
                "| cue:echo.presence | Presence | 0.5 | a | - | fixture |\n";
            Assert.Throws<FileNotFoundException>(() => HunterRosterAudioSetup.Configure(config, text));
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(before));
        }
        private AudioClip Clip(string name, int seconds)
        { var clip = AudioClip.Create(name, seconds * 22050, 1, 22050, false); owned.Add(clip); return clip; }
        private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
