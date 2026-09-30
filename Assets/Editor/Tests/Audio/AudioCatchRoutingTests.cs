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
//   - Inspect the category pool for stings now that legacy cue voices share admission.
//   - Keep lethal health and PlayerDied silent; admit one sting at hold start.
//   - Rearm on capture reset and direct restart, preserving owner readiness guards.
//   - Verify camera replacement, clear and disable pair every subscription.
//   - Route live Chase samples into music and verify floor versus whole-run resets.
// DEPENDENCIES:
//   Core payloads, Session Run, Presentation Audio/Camera, AudioOrchestrator,
//   Domain Chase/Hunter/Player, Session Progression, NUnit and transient Unity objects.
// USAGE NOTES:
//   Edit Mode boundary tests, not audibility or live scene wiring evidence. The
//   Audio Manager's initialized flag is injected to avoid DontDestroyOnLoad in
//   Edit Mode; the Driver uses its actual initialization and normal cue path.
//   Reflection publishes existing events and inspects state; pure gameplay uses explicit clocks.
//   Non-ExecuteAlways callbacks are invoked explicitly in Edit Mode; SetActive alone
//   is not lifecycle evidence. Teardown explicitly releases drivers and singleton seams.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Session.Run;
using Worsen.Session.Progression;
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
        private AudioSoundscapeDriver _soundscape;
        private AudioSoundscapeDriverConfig _musicConfig;
        private ProgressionConfig _progressionConfig;
        private AudioChaseMusicDriverState Music => (AudioChaseMusicDriverState)Field(_soundscape, "_music").GetValue(_soundscape);
        private AudioSoundscapeDriverState Soundscape => (AudioSoundscapeDriverState)Field(_soundscape, "_state").GetValue(_soundscape);
        private AudioDriverState Mix => (AudioDriverState)Field(_driver, "_state").GetValue(_driver);
        private AudioFeedbackDriverState Feedback => (AudioFeedbackDriverState)Field(_driver, "_feedbackState").GetValue(_driver);

        [SetUp]
        public void SetUp()
        {
            Assert.That(RunSessionManager.Instance == null, Is.True, "Requires an isolated Edit Mode fixture.");
            Assert.That(AudioManager.Instance == null, Is.True, "Requires an isolated Edit Mode fixture.");
            _run = Component<RunSessionManager>();
            StaticInstance(typeof(RunSessionManager), _run);
            StaticInstance(typeof(AudioManager), null);
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
            _soundscape = (AudioSoundscapeDriver)Field(_driver, "_soundscape").GetValue(_driver);
            Set(_audio, "_driver", _driver);
            Set(_audio, "_initialized", true);
            _audio.gameObject.SetActive(true);
            Lifecycle(_audio, "OnEnable");
            _route = Component<AudioOrchestrator>();
            Set(_route, "_run", _run);
            Set(_route, "_audio", _audio);
            _route.ConfigureCatch(_camera);
            _route.gameObject.SetActive(true);
            Lifecycle(_route, "OnDisable");
            Lifecycle(_route, "OnEnable");
            Assert.That(Subscribers(_camera, "CatchHoldStarted"), Is.EqualTo(1), "Fixture must establish the real route.");
            Assert.That(Field(_driver, "_ownerEnabled").GetValue(_driver), Is.EqualTo(true));
        }

        [TearDown]
        public void TearDown()
        {
            if (_route != null) { Lifecycle(_route, "OnDisable"); _route.gameObject.SetActive(false); }
            if (_driver != null) _driver.Teardown();
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_config != null) Object.DestroyImmediate(_config);
            if (_clip != null) Object.DestroyImmediate(_clip);
            if (_musicConfig != null) Object.DestroyImmediate(_musicConfig);
            if (_progressionConfig != null)
            { StaticInstance(typeof(ProgressionSessionManager), null); Object.DestroyImmediate(_progressionConfig); }
            StaticInstance(typeof(RunSessionManager), null);
            StaticInstance(typeof(AudioManager), null);
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
            int voice = Array.FindIndex(Soundscape.Voices, entry => entry.Remaining > 0f && entry.Cue == (int)CueId.Death);
            Assert.That(voice, Is.GreaterThanOrEqualTo(0));
            float remaining = Soundscape.Voices[voice].Remaining;
            for (int i = 0; i < 3; i++) Publish(_camera, "CatchHoldStarted", new EntityId(7 + i));
            Assert.That(Array.FindAll(Soundscape.Voices, entry => entry.Remaining > 0f && entry.Cue == (int)CueId.Death).Length, Is.EqualTo(1));
            Assert.That(Soundscape.Voices[voice].Remaining, Is.EqualTo(remaining));
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
            Assert.That(Feedback.HandDeathPlayer.IsValid, Is.False);
            Publish(_camera, "CatchHoldStarted", player);
            Assert.That(Feedback.CatchStingIssued, Is.True);
            Assert.That(Array.Exists(Soundscape.Voices, entry => entry.Remaining > 0f && entry.Cue == (int)CueId.Death), Is.True);
        }

        [Test]
        public void ConfirmedHandDeathOverridesEarlierHunterHitOnlyAtCatchStart()
        {
            var player = new EntityId(7);
            _soundscape.ObserveHit(new HunterHit(new EntityId(4), player, 10, 1, Vector3.one));
            _driver.ObserveHand(new CollapseHandFact(player, 1, CollapseHandEventKind.Consumed, Vector3.zero, 1f, 0f, 2));
            Assert.That(Feedback.HandDeathPlayer, Is.EqualTo(player));
            Assert.That(Feedback.CatchStingIssued, Is.False);
            Assert.That(Array.Exists(Soundscape.Voices, v => v.Remaining > 0 && v.Cue == (int)CueId.Death), Is.False);
            Publish(_camera, "CatchHoldStarted", player);
            Publish(_camera, "CatchHoldStarted", player);
            Assert.That(Array.FindAll(Soundscape.Voices, v => v.Remaining > 0 && v.Cue == (int)CueId.Death).Length, Is.EqualTo(1));
            Assert.That(Feedback.CatchStingIssued, Is.True);
            _audio.ResetRun();
            Assert.That(Feedback.HandDeathPlayer.IsValid, Is.False);
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
            Lifecycle(_route, "OnDisable");
            _route.gameObject.SetActive(false);
            Assert.That(Subscribers(replacement, "CatchHoldStarted"), Is.Zero);
            _route.gameObject.SetActive(true);
            Lifecycle(_route, "OnDisable"); Lifecycle(_route, "OnEnable");
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
            Lifecycle(_audio, "OnDisable");
            _audio.PlayCatchSting(player);
            Assert.That(Feedback.CatchStingIssued, Is.False);
            _audio.enabled = true;
            Lifecycle(_audio, "OnEnable");
            _driver.enabled = false;
            Assert.That(_driver.PlayCatchSting(player), Is.False);
            Assert.That(Feedback.CatchStingIssued, Is.False);
            _driver.enabled = true;
            Assert.That(_driver.PlayCatchSting(player), Is.True);
            Assert.That(_driver.PlayCatchSting(player), Is.False);
        }

        [Test]
        public void LiveLossProximityReachesEarlyFadeWithoutChangingLegacyFeedback()
        {
            InitializeMusic();
            var config = ScriptableObject.CreateInstance<ChaseConfig>();
            try
            {
                var player = new PlayerBehaviorState { Id = new EntityId(7), Health = 100f, Forward = Vector3.forward };
                var hunter = new HunterFixture { PlayerVisible = true, Position = Vector3.back * 4f };
                var controller = new ChaseController(new ChaseBehaviorState(), config, player);
                var hunters = new IReadOnlyHunterState[] { hunter };
                Publish(_run, "ProximityPublished", controller.Tick(hunters, .3f, 1).Proximity);
                TickMusic(10f);
                Assert.That(Music.DangerGain, Is.EqualTo(_musicConfig.ChaseDangerGain));
                hunter.PlayerVisible = false; hunter.Position = Vector3.back * 30f;
                Assert.That(controller.Tick(hunters, config.LossSeconds, 2).Lost, Is.True);
                hunter.Position = Vector3.back * 4f;
                ChaseTickResult end = controller.Tick(hunters, config.LostGraceSeconds, 3);
                Assert.That(end.Ended, Is.True);
                Publish(_run, "ChaseEnded", end.Fact);
                Publish(_run, "ProximityPublished", end.Proximity);
                Assert.That(end.Proximity.Closeness, Is.Zero);
                Assert.That(Mix.Proximity, Is.Zero, "Legacy breathing/feedback still receives gated closeness.");
                Assert.That(Soundscape.Threats[hunter.Id.Value].Closeness, Is.EqualTo(1f));
                Assert.That(Soundscape.Threats[hunter.Id.Value].Chasing, Is.False);
                TickMusic(.1f);
                Assert.That(Music.EarlyDangerFade, Is.True);
                Assert.That(Music.DangerGain, Is.LessThan(_musicConfig.ChaseDangerGain));
                Assert.That(Music.TensionGain, Is.GreaterThan(0f));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void AggregateSnapshotsReplaceObsoleteHuntersAndEmptyOrEndedSnapshotsClearThem()
        {
            InitializeMusic();
            PublishProximity(-1, true); TickMusic();
            PublishProximity(-2, false); TickMusic();
            Assert.That(Soundscape.Threats.ContainsKey(-1), Is.False);
            Assert.That(Soundscape.Threats.Count, Is.EqualTo(1));
            Assert.That(Music.Chasing, Is.False);
            PublishProximity(-2, true);
            Publish(_run, "ChaseEnded", new ChaseFact(1, new EntityId(7), new EntityId(-1), 2, ChasePhase.None));
            Assert.That(Soundscape.Threats, Is.Empty, "Aggregate end must not depend on the original hunter identity.");
            PublishProximity(-2, true);
            Publish(_run, "ProximityPublished", default(ProximitySample)); TickMusic();
            Assert.That(Soundscape.Threats, Is.Empty);
            Assert.That(Music.Chasing, Is.False);
        }

        [Test]
        public void SuccessfulStandaloneEndResetsMusicAndRetainsRuntimeSettings()
        {
            InitializeMusic(); PublishProximity(-1, true); TickMusic();
            Soundscape.RuntimeMusic = .4f;
            Publish(_run, "PhaseChanged", RunPhase.Ended);
            Assert.That(Music.HasContact, Is.False);
            Assert.That(Music.TensionGain + Music.DangerGain + Music.StressGain, Is.Zero);
            Assert.That(Soundscape.Threats, Is.Empty);
            Assert.That(Soundscape.RuntimeMusic, Is.EqualTo(.4f));
            TickMusic(); Assert.That(Music.StartRun, Is.False);
        }

        [Test]
        public void ExpeditionFloorCaptureAndGenerationKeepContactButEndAndSameSeedRestartClearIt()
        {
            InitializeMusic();
            Assert.That(ProgressionSessionManager.Instance == null, Is.True);
            var progression = Component<ProgressionSessionManager>();
            _progressionConfig = ScriptableObject.CreateInstance<ProgressionConfig>();
            var state = new ProgressionSessionBehaviorState();
            Set(progression, "state", state); Set(progression, "config", _progressionConfig);
            Set(progression, "controller", new ProgressionSessionController(state, _progressionConfig, new System.Random(731)));
            StaticInstance(typeof(ProgressionSessionManager), progression);
            _route.ConfigureExpansion(progression, null, null, null);
            progression.StartRun(731); OpenFloor(progression);
            PublishProximity(-1, true); TickMusic();
            float floor = Music.TensionGain;
            object random = Soundscape.CosmeticRandom;
            Publish(_run, "PhaseChanged", RunPhase.Ended);
            Assert.That(progression.CompleteFloor(progression.Snapshot.GenerationId), Is.True);
            OpenFloor(progression);
            Assert.That(Music.HasContact, Is.True, "Generation changes must retain contact.");
            Publish(_run, "CaptureStarted", default(RunCaptureMetadata));
            Assert.That(Music.HasContact, Is.True);
            Assert.That(Music.TensionGain, Is.EqualTo(floor));
            Assert.That(Soundscape.CosmeticRandom, Is.SameAs(random));
            Assert.That(Soundscape.Threats, Is.Empty);
            Assert.That(progression.RestartRun(progression.Snapshot.Revision), Is.False);
            Assert.That(Music.HasContact, Is.True, "Rejected restart must not clear memory.");
            progression.StartRun(731); OpenFloor(progression);
            Assert.That(Music.HasContact, Is.False, "Same-seed committed StartRun is still a full restart.");
            PublishProximity(-1, true); TickMusic();
            Assert.That(progression.EndRun(progression.Snapshot.GenerationId), Is.True);
            Assert.That(Music.HasContact, Is.False);
            Assert.That(Music.TensionGain + Music.DangerGain + Music.StressGain, Is.Zero);
            Assert.That(progression.RestartRun(progression.Snapshot.Revision), Is.True);
            Assert.That(Music.HasContact, Is.False);
            Assert.That(Subscribers(progression, "TransactionCommitted"), Is.EqualTo(1));
            _route.ConfigureExpansion(progression, null, null, null);
            Assert.That(Subscribers(progression, "TransactionCommitted"), Is.EqualTo(1));
            Assert.That(Subscribers(progression, "EffectsSnapshotChanged"), Is.EqualTo(1));
            Assert.That(Subscribers(_run, "OnGraceStarted"), Is.EqualTo(1));
            Assert.That(Subscribers(_run, "FloorDisplayChanged"), Is.EqualTo(1));
            _route.ClearExpansion();
            Assert.That(Subscribers(progression, "TransactionCommitted"), Is.Zero);
            Assert.That(Subscribers(progression, "EffectsSnapshotChanged"), Is.Zero);
        }

        private sealed class HunterFixture : IReadOnlyHunterState
        {
            public EntityId Id => new EntityId(-1);
            public EntityId TargetId => new EntityId(7);
            public bool IsActive => true;
            public bool PlayerVisible { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Velocity => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public Vector3 LastKnownPosition => Vector3.zero;
            public long LastKnownTick => 0;
            public float BeliefConfidence => 0f;
            public long Tick => 0;
        }
        private static void OpenFloor(ProgressionSessionManager progression)
        {
            while (progression.Snapshot.Phase == ProgressionPhase.ChooseThreat || progression.Snapshot.Phase == ProgressionPhase.ChooseCurse)
            {
                ProgressionSnapshot snapshot = progression.Snapshot;
                Assert.That(snapshot.Choices, Is.Not.Empty);
                Assert.That(snapshot.Phase == ProgressionPhase.ChooseThreat
                    ? progression.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision)
                    : progression.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision), Is.True);
            }
            Assert.That(progression.ConfirmFloorReady(progression.Snapshot.GenerationId), Is.True);
        }
        private void InitializeMusic()
        {
            _soundscape.Teardown();
            _musicConfig = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
            var serialized = new SerializedObject(_musicConfig);
            serialized.FindProperty("_earlyDangerFadeProbability").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _soundscape = _audio.gameObject.AddComponent<AudioSoundscapeDriver>();
            _soundscape.Initialize(_musicConfig);
            Set(_driver, "_soundscape", _soundscape);
        }
        private void PublishProximity(int hunter, bool belief) => Publish(_run, "ProximityPublished",
            new ProximitySample(new EntityId(7), new EntityId(hunter), 1, 1, 4f, 0f, false, 1f, belief));
        private void TickMusic(float dt = 1f) => new AudioChaseMusicPresenter().Tick(Music,
            Soundscape.Threats.Values, Soundscape.Alive, dt, 10d, 0d, _musicConfig, Soundscape.CosmeticRandom);
        private static void Lifecycle(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void StaticInstance(Type type, object value) =>
            type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
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
