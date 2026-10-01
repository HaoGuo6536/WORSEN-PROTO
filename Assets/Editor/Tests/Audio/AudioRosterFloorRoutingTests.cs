// ============================================================================
// AudioRosterFloorRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises collapse source events through Floor and Audio's real playback pool.
//   Separately verifies cake-trap routing against the gate-generated Effects bus.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Audio.
// KEY RESPONSIBILITIES:
//   - Check pause and paired collapse subscriptions reach actual spatial playback.
//   - Check injected mixer assignment and clearing on existing trap sources.
// DEPENDENCIES:
//   Core, Floor, Run, Audio, AudioOrchestrator and native Unity audio objects.
// USAGE NOTES:
//   Native Edit Mode; mixer test requires AudioMixerSetup.Build. No asset writes.
// ============================================================================
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Session.Run;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioRosterFloorRoutingTests
    {
        [Test] public void CollapseCueReachesSpatialSourceOnceAndPairsPauseDisable()
        {
            Assert.That(AudioManager.Instance, Is.Null);
            var root = new GameObject("Collapse audio routing"); root.SetActive(false);
            var audio = root.AddComponent<AudioManager>(); var driver = root.GetComponent<AudioDriver>();
            var run = root.AddComponent<RunSessionManager>(); var route = root.AddComponent<AudioOrchestrator>();
            var floor = root.AddComponent<FloorManager>(); var floorDriver = root.GetComponent<FloorDriver>();
            var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var config = ScriptableObject.CreateInstance<AudioDriverConfig>();
            var floorConfig = ScriptableObject.CreateInstance<FloorConfig>();
            var clip = AudioClip.Create("collapse fixture", 22050, 1, 22050, false);
            try
            {
                Set(bank, "_sounds", new[] { new AudioSoundDefinition { Cue = CueId.RoomTear, Clips = new[] { clip }, Gain = .5f,
                    PitchMinimum = 1f, PitchMaximum = 1f, Priority = 90, MaxConcurrent = 2, Spatial = true, MinimumDistance = 1f, MaximumDistance = 40f } });
                Set(config, "_soundscape", bank); driver.Initialize(config);
                Set(audio, "_driver", driver); Set(audio, "_initialized", true);
                Set(floor, "_driver", floorDriver); Set(floor, "_controller", new FloorController(new FloorBehaviorState(), floorConfig, new System.Random(7)));
                var runState = new RunSessionBehaviorState(7); var clock = new RunSessionController(runState, new System.Random(7));
                clock.StartScene(SceneKey.HorrorRun); Set(run, "state", runState); Set(run, "controller", clock);
                Set(route, "_run", run); Set(route, "_audio", audio); Set(route, "_floor", floor);
                root.SetActive(true); Call(floor, "OnEnable"); Call(route, "OnEnable"); Call(route, "OnEnable");
                driver.SetOwnerEnabled(true);
                var sound = (AudioSoundscapeDriver)Get(driver, "_soundscape"); sound.SetInRun(true);
                var pool = (AudioSoundscapeDriverState)Get(sound, "_state");
                Assert.That(((Delegate)Get(floorDriver, "CollapseCue")).GetInvocationList(), Has.Length.EqualTo(1));
                Assert.That(((Delegate)Get(floor, "CollapseCue")).GetInvocationList(), Has.Length.EqualTo(1));
                Publish(floorDriver, "CollapseCue", CueId.RoomTear, Vector3.right * 3f, 3);
                int index = Array.FindIndex(pool.Voices, v => v.Remaining > 0f);
                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                Assert.That(((AudioSource[])Get(sound, "_voices"))[index].transform.position, Is.EqualTo(Vector3.right * 3f));
                Assert.That(pool.Voices[index].Emitter, Is.EqualTo(3));
                Publish(floorDriver, "CollapseCue", CueId.RoomTear, Vector3.right * 10f, 4);
                Assert.That(pool.Voices.Count(v => v.Remaining > 0f), Is.EqualTo(2), "Rooms have separate admission slots.");
                run.SetPaused(true); Assert.That(pool.Paused, Is.True);
                int voices = pool.Voices.Count(v => v.Remaining > 0f);
                Publish(floorDriver, "CollapseCue", CueId.RoomTear, Vector3.right, 5);
                Assert.That(pool.Voices.Count(v => v.Remaining > 0f), Is.EqualTo(voices));
                Call(route, "OnDisable"); Call(floor, "OnDisable");
                Assert.That(Get(floorDriver, "CollapseCue"), Is.Null); Assert.That(Get(floor, "CollapseCue"), Is.Null);
            }
            finally
            {
                Call(route, "OnDisable"); floor.Teardown(); driver.Teardown(); Object.DestroyImmediate(root);
                Object.DestroyImmediate(bank); Object.DestroyImmediate(config); Object.DestroyImmediate(floorConfig); Object.DestroyImmediate(clip);
                typeof(AudioManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            }
        }
        [Test] public void CakeTrapUsesInjectedEffectsMixerAndReconfigurationClearsIt()
        {
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(Worsen.Editor.Audio.AudioMixerSetup.MixerPath);
            Assert.That(mixer, Is.Not.Null, "Gate prerequisite: AudioMixerSetup.Build.");
            var group = mixer.FindMatchingGroups("Effects").Single(value => value.name == "Effects");
            var owner = new GameObject("Cake trap mixer fixture"); var trap = owner.AddComponent<FloorCakeTrap>();
            var driver = owner.AddComponent<FloorDriver>(); var clip = AudioClip.Create("tick fixture", 2205, 1, 22050, false);
            try
            {
                trap.Configure(1, clip, group); Assert.That(owner.GetComponent<AudioSource>().outputAudioMixerGroup, Is.SameAs(group));
                var state = Get(driver, "_state");
                ((IDictionary)state.GetType().GetField("Traps").GetValue(state)).Add(1, trap);
                driver.ConfigureTrapAudio(null); Assert.That(owner.GetComponent<AudioSource>().outputAudioMixerGroup, Is.Null);
                driver.ConfigureTrapAudio(group); Assert.That(owner.GetComponent<AudioSource>().outputAudioMixerGroup, Is.SameAs(group));
                trap.Configure(1, null, null); Assert.That(owner.GetComponent<AudioSource>().clip, Is.Null);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(clip); }
        }
        private static object Get(object value, string field) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Set(object value, string field, object data) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
        private static void Call(object value, string method) => value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
        private static void Publish(object value, string field, params object[] args) => ((Delegate)Get(value, field))?.DynamicInvoke(args);
    }
}
