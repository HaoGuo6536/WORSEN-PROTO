// ============================================================================
// AudioSoundscapeDriverTests.cs
// ============================================================================
//
// PURPOSE:
//   Checks the actual AudioSource pool and spatial source configuration using transient fixtures.
//   It verifies stable emitter reuse and complete teardown without claiming audible quality.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
//
// KEY RESPONSIBILITIES:
//   - Verify enemy one-shots stop at death, late feedback is rejected and the player's own death cue survives.
//   - Verify pool ownership, attenuation, pitch and disabled admission.
//   - Verify health-zero cleanup, death cue admission, revival and cue-isolated stops.
//   - Verify the exertion envelope preserves source playback position and bounded pitch until release.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Coordinator runs under the Unity lease; fixtures do not save assets.
//
// ============================================================================

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Audio;
namespace Worsen.Tests.Audio
{
    public sealed class AudioSoundscapeDriverTests
    {
        [Test]
        public void DeathStopsEnemyScreamOneShotsButPreservesWorldAndPlayerDeathCue()
        {
            var owner = new GameObject("Enemy death gate fixture"); var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var clip = AudioClip.Create("Gate fixture", 480000, 1, 48000, false);
            try
            {
                ConfigureFixtureBanks(bank, clip);
                var driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(bank); driver.SetOwnerEnabled(true);
                Assert.That(driver.Play(CueId.EnemyScream, Vector3.zero, 1, 2), Is.True);
                Assert.That(driver.Play(CueId.MistAdvance, Vector3.zero, 1, 3), Is.True);
                Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(2));
                driver.SetAlive(false); Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(1));
                Assert.That(driver.Play(CueId.EnemyScream, Vector3.zero, 1, 4), Is.False);
                Assert.That(driver.Play(CueId.Death, Vector3.zero, 1, 1), Is.True); Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(2));
                driver.SetAlive(true); Assert.That(driver.Play(CueId.EnemyScream, Vector3.zero, 1, 5), Is.True);
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
        [Test]
        public void ExertionEnvelopeReusesPlaybackPositionAndStopsOnReleaseAndDeath()
        {
            var owner = new GameObject("Exertion envelope fixture");
            var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var clip = AudioClip.Create("Exertion fixture", 480000, 1, 48000, false);
            try
            {
                ConfigureFixtureBanks(bank, clip);
                var serialized = new SerializedObject(bank); var entries = serialized.FindProperty("_sounds");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("Cue").intValue != (int)CueId.SprintExertion) continue;
                    entry.FindPropertyRelative("PitchMinimum").floatValue = .98f; entry.FindPropertyRelative("PitchMaximum").floatValue = 1.02f;
                    entry.FindPropertyRelative("GainVariation").floatValue = .06f;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(bank); driver.SetOwnerEnabled(true);
                var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState { HasMovement = true, IsSprinting = true };
                presenter.TickExertion(state, .2f, .8f, 1f, 0);
                AudioFeedbackCommand command = state.Commands[0];
                Assert.That(driver.Play(command.Cue, command.Position, command.Gain, command.Emitter), Is.True);
                AudioSource active = null;
                foreach (AudioSource source in owner.GetComponentsInChildren<AudioSource>()) if (source.clip == clip) active = source;
                Assert.That(active, Is.Not.Null); Assert.That(active.loop, Is.True); Assert.That(active.pitch, Is.InRange(.98f, 1.02f));
                float pitch = active.pitch, gain = active.volume; active.timeSamples = 4800;
                presenter.TickExertion(state, .2f, .8f, 1f, 0); command = state.Commands[0];
                Assert.That(driver.Play(command.Cue, command.Position, command.Gain, command.Emitter), Is.True);
                Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(1)); Assert.That(active.pitch, Is.EqualTo(pitch));
                Assert.That(active.volume, Is.EqualTo(gain * 2f).Within(.000001f)); Assert.That(active.timeSamples, Is.GreaterThanOrEqualTo(4800));
                state.IsSprinting = false; presenter.TickExertion(state, 1f, .8f, 1f, 0);
                Assert.That(state.Commands[0].StopEmitter, Is.True); driver.StopEmitter(state.Commands[0].Emitter);
                Assert.That(CountAssignedSources(owner, clip), Is.Zero);
                state.IsSprinting = true; presenter.TickExertion(state, .2f, .8f, 1f, 0); command = state.Commands[0];
                Assert.That(driver.Play(command.Cue, command.Position, command.Gain, command.Emitter), Is.True);
                driver.SetAlive(false); Assert.That(CountAssignedSources(owner, clip), Is.Zero);
                Assert.That(driver.Play(command.Cue, command.Position, command.Gain, command.Emitter), Is.False);
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
        [Test]
        public void SlideLoopGainChangesOnTheSameSourceWithoutChangingClipOrPitch()
        {
            var owner = new GameObject("Friction modulation test");
            var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var clip = AudioClip.Create("Friction fixture", 48000, 1, 48000, false);
            try
            {
                ConfigureFixtureBanks(bank, clip);
                var driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(bank); driver.SetOwnerEnabled(true);
                Assert.That(driver.Play(CueId.SlideLoop, Vector3.zero, .5f, 7), Is.True);
                AudioSource active = null;
                foreach (AudioSource candidate in owner.GetComponentsInChildren<AudioSource>()) if (candidate.clip == clip) active = candidate;
                Assert.That(active, Is.Not.Null); float gain = active.volume, pitch = active.pitch;
                Assert.That(driver.Play(CueId.SlideLoop, Vector3.right, 1f, 7), Is.True);
                Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(1));
                Assert.That(active.clip, Is.SameAs(clip)); Assert.That(active.pitch, Is.EqualTo(pitch));
                Assert.That(active.transform.position, Is.EqualTo(Vector3.right));
                Assert.That(active.volume, Is.EqualTo(gain * 2f).Within(.000001f));
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
        [Test]
        public void DeathHealthStopsGameplayLoopsPreservesMistAndAllowsDeathCueAndRevival()
        {
            var owner = new GameObject("Death audio test");
            var config = ScriptableObject.CreateInstance<AudioDriverConfig>();
            var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var clip = AudioClip.Create("Loop cleanup fixture", 48000, 1, 48000, false);
            try
            {
                ConfigureFixtureBanks(bank, clip);
                var serialized = new SerializedObject(config); serialized.FindProperty("_soundscape").objectReferenceValue = bank;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var driver = owner.AddComponent<AudioDriver>(); driver.Initialize(config); driver.SetOwnerEnabled(true);
                Assert.That(driver.PlayCueAt(CueId.ProjectileTravel, Vector3.zero, 1, 7), Is.True);
                Assert.That(driver.PlayCueAt(CueId.SlideLoop, Vector3.zero, 1, 8), Is.True);
                Assert.That(driver.PlayCueAt(CueId.PlayerCritical, Vector3.zero, 1, 9), Is.True);
                Assert.That(driver.PlayCueAt(CueId.MistAdvance, Vector3.zero, 1, 10), Is.True);
                driver.SetInjury(0, 100);
                Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(1), "Only room mist survives immediate health-zero cleanup.");
                Assert.That(driver.PlayCueAt(CueId.ProjectileTravel, Vector3.zero, 1, 11), Is.False);
                Assert.That(driver.PlayCueAt(CueId.Death, Vector3.zero, 1, 12), Is.True);
                driver.SetInjury(100, 100);
                Assert.That(driver.PlayCueAt(CueId.ProjectileTravel, Vector3.zero, 1, 13), Is.True);
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(config); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
        [Test]
        public void StoppingAfterimageCueCannotStopDifferentLoopWithSameEmitter()
        {
            var owner = new GameObject("Isolated emitter test");
            var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var clip = AudioClip.Create("Emitter fixture", 48000, 1, 48000, false);
            try
            {
                ConfigureFixtureBanks(bank, clip);
                var driver = owner.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(bank); driver.SetOwnerEnabled(true);
                Assert.That(driver.Play(CueId.ProjectileTravel, Vector3.zero, 1, 7), Is.True);
                Assert.That(driver.Play(CueId.MistAdvance, Vector3.zero, 1, 7), Is.True);
                driver.StopCueEmitter(CueId.MistAdvance, 7);
                Assert.That(CountAssignedSources(owner, clip), Is.EqualTo(1));
                driver.SetAlive(false); Assert.That(CountAssignedSources(owner, clip), Is.Zero);
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
        private static int CountAssignedSources(GameObject owner, AudioClip clip)
        {
            int count = 0; foreach (AudioSource source in owner.GetComponentsInChildren<AudioSource>()) if (source.clip == clip) count++;
            return count;
        }
        private static void ConfigureFixtureBanks(AudioSoundscapeDriverConfig config, AudioClip clip)
        {
            CueId[] cues = { CueId.ProjectileTravel, CueId.SlideLoop, CueId.PlayerCritical, CueId.MistAdvance, CueId.Death, CueId.SprintExertion, CueId.EnemyScream };
            var serialized = new SerializedObject(config); var list = serialized.FindProperty("_sounds"); list.arraySize = cues.Length;
            for (int i = 0; i < cues.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i); item.FindPropertyRelative("Cue").intValue = (int)cues[i];
                var clips = item.FindPropertyRelative("Clips"); clips.arraySize = 1; clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                item.FindPropertyRelative("Gain").floatValue = .5f;
                item.FindPropertyRelative("PitchMinimum").floatValue = 1f; item.FindPropertyRelative("PitchMaximum").floatValue = 1f;
                item.FindPropertyRelative("MaxConcurrent").intValue = 1; item.FindPropertyRelative("Priority").intValue = 80;
                item.FindPropertyRelative("Loop").boolValue = cues[i] != CueId.Death && cues[i] != CueId.EnemyScream;
                item.FindPropertyRelative("Ambience").boolValue = cues[i] == CueId.MistAdvance;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [Test]
        public void FootstepRaycastClassifiesStaticTimberAndIgnoresActorBody()
        {
            var root = new GameObject("Material routing test");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Old Timber Planks";
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            try
            {
                Vector3 point = new Vector3(1234, 10000, 5678);
                floor.transform.position = point + Vector3.down * .15f; floor.transform.localScale = new Vector3(3, .2f, 3);
                actor.transform.position = point + Vector3.up * .2f; actor.transform.localScale = Vector3.one * .1f; actor.AddComponent<Rigidbody>().isKinematic = true;
                Physics.SyncTransforms();
                var driver = root.AddComponent<AudioSoundscapeDriver>(); driver.Initialize(config);
                Assert.That(driver.ResolveFootstep(point + Vector3.up * .35f), Is.EqualTo(CueId.FootstepWood));
                floor.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
                Assert.That(driver.ResolveFootstep(point + Vector3.up * .35f), Is.EqualTo(CueId.Footstep));
                driver.Teardown();
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(floor); Object.DestroyImmediate(actor); Object.DestroyImmediate(config); }
        }
        [Test]
        public void SpatialPoolOwnsBoundedSourcesAndLoopRefreshMovesExistingEmitter()
        {
            var owner = new GameObject("Soundscape test");
            var config = ScriptableObject.CreateInstance<AudioDriverConfig>();
            var bank = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var clip = AudioClip.Create("Temporary test clip", 48000, 1, 48000, false);
            try
            {
                var serialized = new SerializedObject(bank); var list = serialized.FindProperty("_sounds"); list.arraySize = 1;
                var item = list.GetArrayElementAtIndex(0); item.FindPropertyRelative("Cue").intValue = (int)CueId.TorchLoop;
                var clips = item.FindPropertyRelative("Clips"); clips.arraySize = 1; clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                item.FindPropertyRelative("Gain").floatValue = .5f; item.FindPropertyRelative("PitchMinimum").floatValue = .94f; item.FindPropertyRelative("PitchMaximum").floatValue = 1.06f;
                item.FindPropertyRelative("MaxConcurrent").intValue = 3; item.FindPropertyRelative("Spatial").boolValue = true; item.FindPropertyRelative("Loop").boolValue = true;
                item.FindPropertyRelative("MinimumDistance").floatValue = 2; item.FindPropertyRelative("MaximumDistance").floatValue = 12;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var main = new SerializedObject(config); main.FindProperty("_soundscape").objectReferenceValue = bank;
                main.FindProperty("_breathLoop").objectReferenceValue = clip; main.FindProperty("_hunterLoop").objectReferenceValue = clip; main.ApplyModifiedPropertiesWithoutUndo();
                var driver = owner.AddComponent<AudioDriver>(); driver.Initialize(config); driver.SetOwnerEnabled(true);
                Assert.That(owner.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(38));
                Assert.That(driver.PlayCueAt(CueId.TorchLoop, Vector3.right, 1f, 51), Is.True);
                Assert.That(driver.PlayCueAt(CueId.TorchLoop, Vector3.up, 1f, 51), Is.True);
                int found = 0;
                foreach (AudioSource source in owner.GetComponentsInChildren<AudioSource>())
                    if (source.spatialBlend == 1f && source.clip == clip)
                    { found++; Assert.That(source.transform.position, Is.EqualTo(Vector3.up)); Assert.That(source.pitch, Is.InRange(.94f, 1.06f)); Assert.That(source.maxDistance, Is.EqualTo(12)); }
                Assert.That(found, Is.EqualTo(1));
                driver.SetOwnerEnabled(false); Assert.That(driver.PlayCueAt(CueId.TorchLoop, Vector3.zero, 1, 51), Is.False);
                driver.Teardown(); Assert.That(owner.GetComponentsInChildren<AudioSource>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(config); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); }
        }
    }
}
