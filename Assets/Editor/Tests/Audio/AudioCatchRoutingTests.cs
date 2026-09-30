// ============================================================================
// AudioCatchRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the catch sting through the real Audio Orchestrator, Manager and Driver.
//   Injected publisher facts and transient clips isolate event timing and per-run
//   admission without loading scenes, initializing persistent gameplay or importing assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio integration.
// KEY RESPONSIBILITIES:
//   - Keep lethal health and PlayerDied silent; admit one sting at hold start.
//   - Rearm on capture reset and direct restart, preserving owner readiness guards.
//   - Verify camera replacement, clear and disable pair every subscription.
// DEPENDENCIES:
//   Core payloads, Session Run, Presentation Audio/Camera, AudioOrchestrator,
//   NUnit, UnityEditor transient configuration and UnityEngine audio sources.
// USAGE NOTES:
//   Edit Mode boundary tests, not audibility or live scene wiring evidence. The
//   Audio Manager's initialized flag is injected to avoid DontDestroyOnLoad in
//   Edit Mode; the Driver uses its actual initialization and normal cue path.
//   Reflection publishes existing events and inspects state, never runs gameplay.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Audio
{
    public sealed class AudioCatchRoutingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private RunSessionManager _run;
        private CameraManager _camera;
        private AudioManager _audio;
        private AudioDriver _driver;
        private AudioOrchestrator _route;
        private AudioDriverConfig _config;
        private AudioClip _clip;
        private AudioDriverState Mix => (AudioDriverState)Field(_driver, "_state").GetValue(_driver);
        private AudioFeedbackDriverState Feedback => (AudioFeedbackDriverState)Field(_driver, "_feedbackState").GetValue(_driver);

        [SetUp]
        public void SetUp()
        {
            Assert.That(RunSessionManager.Instance, Is.Null, "Requires an isolated Edit Mode fixture.");
            Assert.That(AudioManager.Instance, Is.Null, "Requires an isolated Edit Mode fixture.");
            _run = Component<RunSessionManager>();
            _camera = Component<CameraManager>();
            _audio = Component<AudioManager>();
            _driver = _audio.GetComponent<AudioDriver>();
            _config = ScriptableObject.CreateInstance<AudioDriverConfig>();
            _clip = AudioClip.Create("Transient catch sting", 22050, 1, 22050, false);
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_breathLoop").objectReferenceValue = _clip;
            serialized.FindProperty("_hunterLoop").objectReferenceValue = _clip;
            var cues = serialized.FindProperty("_cues");
            for (int i = 0; i < cues.arraySize; i++)
                cues.GetArrayElementAtIndex(i).FindPropertyRelative("_clip").objectReferenceValue = _clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _driver.Initialize(_config);
            Set(_audio, "_driver", _driver);
            Set(_audio, "_initialized", true);
            _audio.gameObject.SetActive(true);
            _route = Component<AudioOrchestrator>();
            Set(_route, "_run", _run);
            Set(_route, "_audio", _audio);
            _route.ConfigureCatch(_camera);
            _route.gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_route != null) _route.gameObject.SetActive(false);
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_config != null) Object.DestroyImmediate(_config);
            if (_clip != null) Object.DestroyImmediate(_clip);
        }

        [Test]
        public void DeathAndLethalHealthStaySilentUntilOneHoldStartPerRun()
        {
            var player = new EntityId(7);
            _driver.ObserveHealth(player, 100, 100);
            _driver.ObserveHealth(player, 0, 100);
            Publish(_run, "HealthChanged", player, 0f, 100f);
            Publish(_run, "PlayerDied", player, Vector3.one);
            Assert.That(Feedback.Commands, Is.Empty);
            Assert.That(Feedback.CatchStingIssued, Is.False);
            Publish(_camera, "CatchHoldStarted", default(EntityId));
            Assert.That(Feedback.CatchStingIssued, Is.False);
            Publish(_camera, "CatchHoldStarted", player);
            Assert.That(Feedback.CatchStingIssued, Is.True);
            Assert.That(Mix.ActiveCueKey, Is.EqualTo((int)CueId.Death));
            int voice = Mix.VoiceIndex;
            for (int i = 0; i < 3; i++) Publish(_camera, "CatchHoldStarted", new EntityId(7 + i));
            Assert.That(Mix.VoiceIndex, Is.EqualTo(voice), "Repeated starts must not restart or swap cue voices.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestartAndCaptureResetRearmSting(bool capture)
        {
            var player = new EntityId(7);
            Publish(_camera, "CatchHoldStarted", player);
            AudioFeedbackDriverState before = Feedback;
            if (capture) Publish(_run, "CaptureStarted", default(RunCaptureMetadata));
            else _audio.ResetRun();
            Assert.That(Feedback, Is.Not.SameAs(before));
            Assert.That(Feedback.CatchStingIssued, Is.False);
            Publish(_camera, "CatchHoldStarted", player);
            Assert.That(Feedback.CatchStingIssued, Is.True);
            Assert.That(Mix.ActiveCueKey, Is.EqualTo((int)CueId.Death));
        }

        [Test]
        public void RebindClearAndDisablePairCameraSubscriptions()
        {
            Assert.That(Subscribers(_camera, "CatchHoldStarted"), Is.EqualTo(1));
            _route.ConfigureCatch(_camera);
            Assert.That(Subscribers(_camera, "CatchHoldStarted"), Is.EqualTo(1));
            var replacement = Component<CameraManager>();
            _route.ConfigureCatch(replacement);
            Assert.That(Subscribers(_camera, "CatchHoldStarted"), Is.Zero);
            Assert.That(Subscribers(replacement, "CatchHoldStarted"), Is.EqualTo(1));
            _route.gameObject.SetActive(false);
            Assert.That(Subscribers(replacement, "CatchHoldStarted"), Is.Zero);
            _route.gameObject.SetActive(true);
            Assert.That(Subscribers(replacement, "CatchHoldStarted"), Is.EqualTo(1));
            _route.ClearCatch();
            Assert.That(Subscribers(replacement, "CatchHoldStarted"), Is.Zero);
            Assert.That(Field(_route, "_camera").GetValue(_route), Is.Null);
            Publish(replacement, "CatchHoldStarted", new EntityId(7));
            Assert.That(Feedback.CatchStingIssued, Is.False);
        }

        [Test]
        public void DisabledOrUninitializedOwnerDoesNotConsumeStingAdmission()
        {
            var player = new EntityId(7);
            Set(_audio, "_initialized", false);
            _audio.PlayCatchSting(player);
            Assert.That(Feedback.CatchStingIssued, Is.False);
            Set(_audio, "_initialized", true);
            _audio.enabled = false;
            _audio.PlayCatchSting(player);
            Assert.That(Feedback.CatchStingIssued, Is.False);
            _audio.enabled = true;
            _driver.enabled = false;
            Assert.That(_driver.PlayCatchSting(player), Is.False);
            Assert.That(Feedback.CatchStingIssued, Is.False);
            _driver.enabled = true;
            Assert.That(_driver.PlayCatchSting(player), Is.True);
            Assert.That(_driver.PlayCatchSting(player), Is.False);
        }

        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " catch audio test");
            _objects.Add(owner); owner.SetActive(false);
            return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static int Subscribers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Publish(object target, string name, params object[] args) =>
            (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
