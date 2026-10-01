// ============================================================================
// AudioDriverTests.cs
// ============================================================================
//
// PURPOSE:
//   Checks the Audio engine boundary's source ownership and unavailable-clip behavior.
//   Transient sources and clips isolate routing and cleanup without modifying assets.
//   Compatibility bank tests explicitly identify a legacy hunter at each run boundary.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
//
// KEY RESPONSIBILITIES:
//   - Verify pooled fallback source ownership, disabled admission and full teardown.
//   - Verify priority rejection and visible missing-clip diagnostics.
//   - Exercise direct component disable/re-enable with real Play Mode callbacks.
//
// DEPENDENCIES:
//   - Presentation Audio stack, Core CueId, NUnit, UnityEditor and Unity audio APIs.
//
// USAGE NOTES:
//   - Coordinator executes under the Unity lease; no actual audibility claim.
//   - Every temporary source, clip and config is destroyed after the test.
//   - The lifecycle test creates its fixture after EnterPlayMode's domain reload;
//     command-only tests remain in Edit Mode. UnityTearDown restores the editor.
//   - Runtime frame checks temporarily enable runInBackground and restore its prior value.
//
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Presentation.Audio;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class AudioDriverTests
    {
        private GameObject _owner;
        private AudioDriver _driver;
        private AudioDriverConfig _config;
        private AudioClip _clip;
        private bool _priorRunInBackground;
        private bool _backgroundSnapshotTaken;

        private void CreateFixture()
        {
            _owner = new GameObject("Owned audio test");
            _driver = _owner.AddComponent<AudioDriver>();
            _config = ScriptableObject.CreateInstance<AudioDriverConfig>();
            _clip = AudioClip.Create("Temporary audio test", 22050, 1, 22050, false);
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_breathLoop").objectReferenceValue = _clip;
            serialized.FindProperty("_hunterLoop").objectReferenceValue = _clip;
            var cues = serialized.FindProperty("_cues");
            for (int i = 0; i < cues.arraySize; i++)
                cues.GetArrayElementAtIndex(i).FindPropertyRelative("_clip").objectReferenceValue = _clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (_driver != null) _driver.Teardown();
                if (_owner != null) Object.DestroyImmediate(_owner);
                if (_config != null) Object.DestroyImmediate(_config);
                if (_clip != null) Object.DestroyImmediate(_clip);
            }
            finally
            {
                if (_backgroundSnapshotTaken)
                {
                    Application.runInBackground = _priorRunInBackground;
                    _backgroundSnapshotTaken = false;
                }
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SourcesAreOwnedOnceAndTeardownIsRepeatable()
        {
            CreateFixture();
            _driver.Initialize(_config);
            _driver.Initialize(_config);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(31));
            Assert.That(_driver.PlayCue(CueId.Presence), Is.False);
            _driver.SetOwnerEnabled(true);
            _driver.SetWorld(AudioWorldMixPresenterTests.Graph(), null);
            Assert.That(PlayLegacy(CueId.Presence), Is.True);
            Assert.That(System.Array.Exists(_owner.GetComponentsInChildren<AudioSource>(), source => !source.loop && source.clip == _clip && source.volume > 0f), Is.True);
            _driver.SetOwnerEnabled(false);
            Assert.That(_driver.PlayCue(CueId.Death), Is.False);
            Assert.That(_driver.BreathGain, Is.Zero);
            Assert.That(_driver.HunterGain, Is.Zero);
            _driver.Teardown();
            _driver.Teardown();
            Assert.That(_driver.IsInitialized, Is.False);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().Length, Is.Zero);
        }

        [Test]
        public void PriorityAndRunResetReachActualPlaybackAdmission()
        {
            CreateFixture();
            _driver.Initialize(_config);
            _driver.SetOwnerEnabled(true);
            Assert.That(PlayLegacy(CueId.Presence), Is.True);
            Assert.That(PlayLegacy(CueId.Chase), Is.True);
            Assert.That(PlayLegacy(CueId.Presence), Is.False);
            Assert.That(_driver.PlayCue(CueId.Lose), Is.False);
            Assert.That(PlayLegacy(CueId.Chase), Is.False);
            Assert.That(PlayLegacy(CueId.Death), Is.True);
            _driver.ResetRun();
            Assert.That(PlayLegacy(CueId.Presence), Is.True);
        }

        [Test]
        public void MissingClipIsRejectedAndWarnedOncePerRun()
        {
            CreateFixture();
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_cues").GetArrayElementAtIndex(0).FindPropertyRelative("_clip").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _driver.Initialize(_config);
            _driver.SetOwnerEnabled(true);
            LogAssert.Expect(LogType.Warning, "Soundscape cue 'Presence' is not wired to a playable bank.");
            Assert.That(PlayLegacy(CueId.Presence), Is.False);
            Assert.That(PlayLegacy(CueId.Presence), Is.False);
        }

        [UnityTest]
        public IEnumerator DriverDisableDropsStoppedCuePriorityBeforeReenable()
        {
            yield return new EnterPlayMode();
            _priorRunInBackground = Application.runInBackground;
            _backgroundSnapshotTaken = true;
            Application.runInBackground = true;
            // Create references after reload; iterator locals from before EnterPlayMode are not retained.
            CreateFixture();
            Assert.That(Application.isPlaying, Is.True, "This assertion requires real runtime MonoBehaviour callbacks.");
            _driver.Initialize(_config);
            _driver.SetOwnerEnabled(true);
            Assert.That(PlayLegacy(CueId.Chase), Is.True);
            _driver.enabled = false;
            Assert.That(_driver.PlayCue(CueId.Chase), Is.False);
            yield return null;
            Assert.That(System.Array.TrueForAll(_owner.GetComponentsInChildren<AudioSource>(), source => !source.isPlaying), Is.True,
                "OnDisable must stop every owned source before the component is re-enabled.");
            _driver.enabled = true;
            Assert.That(PlayLegacy(CueId.Chase), Is.True);
        }
        private bool PlayLegacy(CueId cue)
        {
            _driver.ObserveHunterFeedback(new HunterFeedbackEvent(new EntityId(7), "rusher", HunterFeedbackKind.LostTarget, Vector3.zero, 1));
            return _driver.PlayCueAt(cue, Vector3.zero, 1f, 7);
        }
    }
}
