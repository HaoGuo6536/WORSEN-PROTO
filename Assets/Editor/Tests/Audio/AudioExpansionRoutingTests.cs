// ============================================================================
// AudioExpansionRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Drives Run expansion events through the actual Audio manager and owned sources.
//   Subscription counts detect double delivery even when receiver deduplication hides it.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Audio.
// KEY RESPONSIBILITIES:
//   - Pair every expansion sound and sensory subscription on rebind/disable.
//   - Preserve raw durations, source identity, pitch and Mannequin catch selection.
// DEPENDENCIES:
//   Core, Run, Audio, Camera, Orchestrators, NUnit and transient Unity audio objects.
// USAGE NOTES:
//   Native Edit Mode fixture. No assets, scenes or AI stimuli are created.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioExpansionRoutingTests
    {
        private readonly List<Object> owned = new List<Object>();
        private RunSessionManager run;
        private AudioManager audio;
        private AudioSoundscapeDriver sound;
        private AudioOrchestrator route;
        private AudioSoundscapeDriverConfig config;
        private AudioClip clip, snap;
        private static readonly string[] Channels = { "RamFactPublished", "MimicFactPublished", "BlinderSoundPublished",
            "BlinderHitPublished", "HeraldScreamPublished", "HeraldBreathPublished", "HeraldDeafenPublished",
            "MannequinFactPublished", "StareFactPublished" };
        private AudioSoundscapeDriverState Pool => Get<AudioSoundscapeDriverState>(sound, "_state");
        [SetUp] public void Setup()
        {
            Assert.That(AudioManager.Instance, Is.Null); Assert.That(RunSessionManager.Instance, Is.Null);
            config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>(); owned.Add(config);
            clip = AudioClip.Create("expansion", 22050, 1, 22050, false); owned.Add(clip);
            snap = AudioClip.Create("snap", 2205, 1, 22050, false); owned.Add(snap);
            Set(config, "_rosterBindings", new[] {
                new AudioRosterBinding("herald-test", CueId.Detection) { Clip = clip },
                new AudioRosterBinding("mannequin.death", CueId.Death) { Clip = snap, OverrideGain = true, Gain = .3f },
                new AudioRosterBinding("hunter.death", CueId.Death) { Clip = clip } });
            sound = Make<AudioSoundscapeDriver>(); sound.gameObject.SetActive(true);
            sound.Initialize(config); sound.SetOwnerEnabled(true); sound.SetInRun(true);
            audio = Make<AudioManager>(); var driver = audio.GetComponent<AudioDriver>();
            Set(audio, "_initialized", true); Set(audio, "_driver", driver);
            audio.gameObject.SetActive(true); Set(driver, "_soundscape", sound);
            run = Make<RunSessionManager>(); route = Make<AudioOrchestrator>();
            Set(route, "_run", run); Set(route, "_audio", audio); Call(route, "OnEnable");
        }
        [TearDown] public void Cleanup()
        {
            if (route != null) Call(route, "OnDisable");
            if (audio != null) Set(audio.GetComponent<AudioDriver>(), "_soundscape", null);
            if (sound != null) sound.Teardown();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            typeof(AudioManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
        [Test] public void ExpansionChannelsPairExactlyOnceAcrossEnableAndPublisherReplacement()
        {
            Call(route, "OnEnable"); Call(route, "OnEnable");
            foreach (string name in Channels) Assert.That(Get<Delegate>(run, name).GetInvocationList().Length, Is.EqualTo(1), name);
            Call(route, "OnDisable");
            foreach (string name in Channels) Assert.That(Get<Delegate>(run, name), Is.Null, name);
            var next = Make<RunSessionManager>(); Set(route, "_run", next); Call(route, "OnEnable");
            foreach (string name in Channels)
            { Assert.That(Get<Delegate>(run, name), Is.Null); Assert.That(Get<Delegate>(next, name).GetInvocationList().Length, Is.EqualTo(1)); }
        }
        [Test] public void RawDurationsReachTypedReceiversOnceWithoutLegacyRefresh()
        {
            audio.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("ear-plugs"), EffectKind.Upgrade, 1),
                new ActiveEffect(new EffectId("mirror-skin"), EffectKind.Upgrade, 1) }));
            var hit = new BlinderHitFact(new EntityId(7), new EntityId(1), 1, 1, 6f, true);
            var deafen = new HeraldDeafenFact(new EntityId(8), new EntityId(1), 1, Vector3.zero, 5f, 1, 4f, false);
            Publish(run, "BlinderHitPublished", hit); Publish(run, "HeraldDeafenPublished", deafen);
            Assert.That(Pool.WorldMix.MuffledRemaining, Is.EqualTo(3f)); Assert.That(Pool.WorldMix.DeafenedRemaining, Is.EqualTo(2f));
            sound.TickEmbodiment(0f, .5f);
            Publish(run, "BlinderHitPublished", hit); Publish(run, "HeraldDeafenPublished", deafen);
            Assert.That(Pool.WorldMix.MuffledRemaining, Is.EqualTo(2.5f)); Assert.That(Pool.WorldMix.DeafenedRemaining, Is.EqualTo(1.5f));
        }
        [Test] public void HeraldKeepsAcousticOriginPitchAndHunterIdentity()
        {
            var hunter = new EntityId(7); var origin = new Vector3(2f, 3f, 4f);
            var fact = new HeraldScreamFact(hunter, HeraldSound.Discovery, "herald-test", .94f,
                new NoiseEvent(hunter, origin, 1f, 10), new NoiseEvent(hunter, Vector3.zero, 1f, 10), 1, false);
            Publish(run, "HeraldScreamPublished", fact); Publish(run, "HeraldScreamPublished", fact);
            int index = Array.FindIndex(Pool.Voices, v => v.Remaining > 0f);
            Assert.That(Pool.Voices.Count(v => v.Remaining > 0f), Is.EqualTo(1));
            var source = Get<AudioSource[]>(sound, "_voices")[index];
            Assert.That(source.transform.position, Is.EqualTo(origin)); Assert.That(source.pitch, Is.EqualTo(.94f));
            Assert.That(Pool.Voices[index].Emitter, Is.EqualTo(7));
        }
        [Test] public void MannequinRelaySelectsSnapInsteadOfSharedLoudSting()
        {
            var hunter = new EntityId(7);
            Publish(run, "MannequinFactPublished", new MannequinFact(hunter, MannequinFactKind.SilentSoundSet, 1));
            Publish(run, "HitAccepted", new HunterHit(hunter, new EntityId(1), 100, 2, Vector3.zero));
            Assert.That(sound.PlayDeath(), Is.True);
            int index = Array.FindIndex(Pool.Voices, v => v.Remaining > 0f);
            Assert.That(Get<AudioSource[]>(sound, "_voices")[index].clip, Is.SameAs(snap));
            Assert.That(Pool.VoiceGains[index], Is.EqualTo(.3f));
        }
        private T Make<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string channel, params object[] args) => Get<Delegate>(target, channel)?.DynamicInvoke(args);
    }
}
