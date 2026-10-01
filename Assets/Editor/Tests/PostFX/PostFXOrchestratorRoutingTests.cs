// ============================================================================
// PostFXOrchestratorRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises intrusion admission through the real routing and presentation command path.
//   Explicit publisher facts exercise Horror's real whole-run clock and the injected
//   test seam, while real Progression commands exercise floor and restart boundaries.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX routing integration.
// KEY RESPONSIBILITIES:
//   - Require one clock read and one admission attempt per Director intrusion.
//   - Preserve budget and spacing across floors; reset only on committed run starts.
//   - Verify same-seed restart, rejected restart and paired subscriptions.
//   - Verify tick forwarding, rebind cleanup, suspension and invalid clock commands.
//   - Drive Edit Mode lifecycle explicitly; verify active effects and Blind duration.
// DEPENDENCIES:
//   Core; Domain Floor; Session Run/Progression; Presentation PostFX/Horror/Input;
//   Orchestrators; NUnit, transient configuration and UnityEngine object lifetime.
// USAGE NOTES:
//   Edit Mode boundary tests for the production clock path, not live scene wiring.
//   Horror's state/presenter and inactive atmosphere boundary are injected to avoid
//   global rendering changes. Progression's canonical controller is injected to avoid
//   DontDestroyOnLoad in Edit Mode; public StartRun/RestartRun still publish real facts.
//   Explicit owner lifecycle enables Horror's injected driver; PostFX initializes normally.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Orchestrator;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Input;
using Worsen.Presentation.PostFX;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.PostFX
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class PostFXOrchestratorRoutingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private RunSessionManager _run;
        private ProgressionSessionManager _progression;
        private HorrorManager _horror;
        private HorrorDriver _horrorDriver;
        private HorrorDriverState _horrorState;
        private HorrorDriverConfig _horrorConfig;
        private ProgressionConfig _progressionConfig;
        private PostFXManager _post;
        private PostFXDriver _postDriver;
        private PostFXDriverConfig _postConfig;
        private PostFXOrchestrator _postRoute;
        private HorrorOrchestrator _horrorRoute;
        private InputManager _input;
        private double _wholeRunSeconds;
        private int _clockReads;
        private PostFXDriverState Post => (PostFXDriverState)Field(_postDriver, "_state").GetValue(_postDriver);

        [SetUp]
        public void SetUp()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            _run = Component<RunSessionManager>();
            _progression = Component<ProgressionSessionManager>();
            _progressionConfig = ScriptableObject.CreateInstance<ProgressionConfig>();
            var state = new ProgressionSessionBehaviorState();
            Set(_progression, "state", state);
            Set(_progression, "config", _progressionConfig);
            Set(_progression, "controller", new ProgressionSessionController(state, _progressionConfig, new System.Random(731)));
            typeof(ProgressionSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _progression);
            _horror = Component<HorrorManager>();
            _horrorDriver = _horror.GetComponent<HorrorDriver>();
            _horrorConfig = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            _horrorState = new HorrorDriverState();
            Set(_horror, "_driver", _horrorDriver);
            Set(_horrorDriver, "_state", _horrorState);
            Set(_horrorDriver, "_config", _horrorConfig);
            Set(_horrorDriver, "_presenter", new HorrorPresenter());
            Set(_horrorDriver, "_atmosphere", Component<HorrorAtmosphereDriver>());
            Activate(_horror, true);
            _post = Component<PostFXManager>();
            _postDriver = _post.gameObject.AddComponent<PostFXDriver>();
            _postDriver.ConfigureForSetup();
            _postConfig = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            Set(_post, "_driver", _postDriver); Set(_post, "_config", _postConfig);
            _post.gameObject.SetActive(true);
            _post.Initialize();
            _input = Component<InputManager>();
            _horrorRoute = Component<HorrorOrchestrator>();
            _horrorRoute.Configure(_run, _progression, _input, _horror);
            Activate(_horrorRoute, true);
            _postRoute = Component<PostFXOrchestrator>();
            _postRoute.Configure(_run, _post, horror: _horror, runSeconds: ReadClock, progression: _progression);
            Activate(_postRoute, true);
            _progression.StartRun(731);
            _wholeRunSeconds = 0d; _clockReads = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_postRoute != null) Activate(_postRoute, false);
            if (_horrorRoute != null) Activate(_horrorRoute, false);
            if (_post != null) _post.Teardown();
            if (_horrorDriver != null) _horrorDriver.Teardown();
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            Object.DestroyImmediate(_horrorConfig); Object.DestroyImmediate(_postConfig);
            Object.DestroyImmediate(_progressionConfig);
            // Edit Mode destruction does not run OnDestroy, so clear the session singletons explicitly.
            typeof(RunSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            typeof(ProgressionSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }

        [Test]
        public void EachIntrusionMakesOneAdmissionAndDeniedFeedbackIsSubtle()
        {
            var random = new CountingRandom();
            Set(_horrorDriver, "_startleRandom", random);
            Intrude(5d, true);
            Assert.That(random.Draws, Is.EqualTo(1), "One admission consumes one random draw.");
            Assert.That(_clockReads, Is.EqualTo(1));
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1));
            Intrude(6d, false);
            Assert.That(random.Draws, Is.EqualTo(1), "Spacing rejection must not draw again.");
            Assert.That(_clockReads, Is.EqualTo(2));
            Assert.That(_horrorState.LastIntrusionSeconds, Is.EqualTo(6d));
        }

        [Test]
        public void FloorsDoNotRewindTheInjectedClockOrReplenishTheBudget()
        {
            int first = OpenFloor();
            Intrude(5d, true);
            Assert.That(_progression.CompleteFloor(first), Is.True);
            int second = OpenFloor();
            Assert.That(_run.ElapsedSeconds, Is.Zero, "Run's floor-local clock cannot supply spacing here.");
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1));
            Intrude(5d + _horrorConfig.StartleSpacingSeconds - 1d, false);
            Intrude(5d + _horrorConfig.StartleSpacingSeconds, true);
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(_horrorConfig.StartlesPerRun));
            Assert.That(_progression.CompleteFloor(second), Is.True); // Generates the refuge floor.
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(_horrorConfig.StartlesPerRun));
            Intrude(5d + 2d * _horrorConfig.StartleSpacingSeconds, false);
        }

        [TestCase(731)]
        [TestCase(902)]
        public void InitialStartAndActualRestartResetOnceButGenerationAndRejectedRestartDoNot(int seed)
        {
            _horrorState.StartlesUsed = 9;
            _progression.StartRun(731);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
            int generation = OpenFloor();
            Intrude(5d, true);
            System.Random random = (System.Random)Field(_horrorDriver, "_startleRandom").GetValue(_horrorDriver);
            Assert.That(_progression.RestartRun(_progression.Snapshot.Revision, seed), Is.False);
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1));
            Assert.That(_progression.EndRun(generation), Is.True);
            int revision = _progression.Snapshot.Revision;
            Assert.That(_progression.RestartRun(revision, seed), Is.True);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
            Assert.That(_horrorState.LastIntrusionSeconds, Is.EqualTo(double.NegativeInfinity));
            var restarted = (System.Random)Field(_horrorDriver, "_startleRandom").GetValue(_horrorDriver);
            Assert.That(restarted, Is.Not.SameAs(random));
            Assert.That(restarted.NextDouble(), Is.EqualTo(new System.Random(seed).NextDouble()));
            Intrude(1d, true);
            Assert.That(_progression.RestartRun(revision, seed), Is.False);
            OpenFloor();
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1), "Round-one generation must not reset the new run a second time.");
        }

        [Test]
        public void ReconfigureAndDisablePairSubscriptionsWithoutResettingAdmission()
        {
            Intrude(5d, true);
            _postRoute.Configure(_run, _post, horror: _horror, runSeconds: ReadClock);
            _horrorRoute.Configure(_run, _progression, _input, _horror);
            Assert.That(Subscribers(_run, "IntrusionPublished"), Is.EqualTo(1));
            Assert.That(Subscribers(_progression, "TransactionCommitted"), Is.EqualTo(1));
            Activate(_postRoute, false); Activate(_horrorRoute, false);
            Assert.That(Subscribers(_run, "IntrusionPublished"), Is.Zero);
            Assert.That(Subscribers(_progression, "TransactionCommitted"), Is.Zero);
            Activate(_postRoute, true); Activate(_horrorRoute, true);
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1));
            Intrude(6d, false);
        }

        [Test]
        public void MissingHorrorFailsClosedWithoutConsumingBudgetOrReadingInjectedClock()
        {
            _postRoute.Configure(_run, _post, runSeconds: ReadClock);
            Intrude(5d, false);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
            Assert.That(_clockReads, Is.Zero);
        }

        [Test]
        public void FloorsPreserveTheRealHorrorClockSpacingAndBudgetWithoutAnInjectedClock()
        {
            _postRoute.Configure(_run, _post, horror: _horror);
            var state = new RunSessionBehaviorState(731);
            Set(_run, "state", state);
            Set(_run, "controller", new RunSessionController(state, new System.Random(731)));
            int first = OpenFloor();
            _run.HandleSceneReady(SceneKey.HorrorRun);
            AdvanceGameplayClock(5f);
            Intrude(5d, true);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(5d));
            Assert.That(_run.ElapsedSeconds, Is.EqualTo(5d));
            Assert.That(_progression.CompleteFloor(first), Is.True);
            _run.PrepareScene(SceneKey.HorrorRun, _progression.Snapshot.Seed);
            OpenFloor();
            _run.HandleSceneReady(SceneKey.HorrorRun);
            Assert.That(_run.ElapsedSeconds, Is.Zero);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(5d));
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1));
            AdvanceGameplayClock(_horrorConfig.StartleSpacingSeconds - 1f);
            Intrude(_horror.RunElapsedSeconds, false);
            AdvanceGameplayClock(1f);
            Intrude(_horror.RunElapsedSeconds, true);
            AdvanceGameplayClock(_horrorConfig.StartleSpacingSeconds);
            Intrude(_horror.RunElapsedSeconds, false);
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(_horrorConfig.StartlesPerRun));
            Assert.That(_clockReads, Is.Zero);
            _progression.StartRun(731);
            Assert.That(_horror.RunElapsedSeconds, Is.Zero);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
        }

        [TestCase(731)]
        [TestCase(902)]
        public void CommittedRestartZeroesTheRealClockAndGameplayTicksResume(int seed)
        {
            int generation = OpenFloor();
            Publish(_run, "TickAdvanced", default(InputFrame), 3f, 1L);
            Assert.That(_progression.RestartRun(_progression.Snapshot.Revision, seed), Is.False);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(3d));
            Assert.That(_progression.EndRun(generation), Is.True);
            Assert.That(_progression.RestartRun(_progression.Snapshot.Revision, seed), Is.True);
            Assert.That(_horror.RunElapsedSeconds, Is.Zero);
            OpenFloor();
            Publish(_run, "TickAdvanced", default(InputFrame), 0.5f, 1L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(0.5d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void InvalidInjectedClockDoesNotFallBackToTheValidHorrorClock(double seconds)
        {
            _horror.AdvanceRunClock(5f);
            Intrude(seconds, false);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(5d));
            Assert.That(_clockReads, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitClockTakesPrecedenceOverTheRealHorrorClock()
        {
            Assert.That(_horror.AdvanceRunClock(500f), Is.True);
            Intrude(5d, true);
            Assert.That(_horrorState.LastIntrusionSeconds, Is.EqualTo(5d));
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(500d));
            Assert.That(_clockReads, Is.EqualTo(1));
        }

        [Test]
        public void ReconfigureDisableAndReenableForwardEachTickExactlyOnce()
        {
            Publish(_run, "TickAdvanced", default(InputFrame), 0.25f, 1L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(0.25d));
            _horrorRoute.Configure(_run, _progression, _input, _horror);
            _horrorRoute.Configure(_run, _progression, _input, _horror);
            Assert.That(Subscribers(_run, "TickAdvanced"), Is.EqualTo(1));
            Publish(_run, "TickAdvanced", default(InputFrame), 0.5f, 2L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(0.75d));
            Activate(_horrorRoute, false);
            Assert.That(Subscribers(_run, "TickAdvanced"), Is.Zero);
            Publish(_run, "TickAdvanced", default(InputFrame), 1f, 3L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(0.75d));
            Activate(_horrorRoute, true);
            Assert.That(Subscribers(_run, "TickAdvanced"), Is.EqualTo(1));
            Publish(_run, "TickAdvanced", default(InputFrame), 0.25f, 4L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(1d));
        }

        [Test]
        public void RebindingDetachesTheOldTickPublisher()
        {
            var replacement = Component<RunSessionManager>();
            _horrorRoute.Configure(replacement, _progression, _input, _horror);
            Assert.That(Subscribers(_run, "TickAdvanced"), Is.Zero);
            Assert.That(Subscribers(replacement, "TickAdvanced"), Is.EqualTo(1));
            Publish(_run, "TickAdvanced", default(InputFrame), 9f, 1L);
            Assert.That(_horror.RunElapsedSeconds, Is.Zero);
            Publish(replacement, "TickAdvanced", default(InputFrame), 0.5f, 1L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(0.5d));
            Activate(_horrorRoute, false);
            Assert.That(Subscribers(replacement, "TickAdvanced"), Is.Zero);
        }

        [Test]
        public void SuspendedAndEndedGameplayDoesNotPublishClockTicks()
        {
            var state = new RunSessionBehaviorState(731);
            var controller = new RunSessionController(state, new System.Random(731));
            Set(_run, "state", state); Set(_run, "controller", controller);
            typeof(RunSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _run);
            controller.StartScene(SceneKey.HorrorRun);
            var fixedUpdate = typeof(RunSessionManager).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            fixedUpdate.Invoke(_run, null);
            double elapsed = _horror.RunElapsedSeconds;
            Assert.That(elapsed, Is.GreaterThan(0d));
            Assert.That(elapsed, Is.EqualTo(_run.ElapsedSeconds));
            _run.SuspendForSceneLoad();
            fixedUpdate.Invoke(_run, null);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(elapsed));
            controller.StartScene(SceneKey.HorrorRun);
            Assert.That(_run.ElapsedSeconds, Is.Zero);
            controller.Apply(RunEvent.PlayerDied);
            fixedUpdate.Invoke(_run, null);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(elapsed));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-1f)]
        public void PublicClockCommandRejectsInvalidDeltas(float deltaSeconds)
        {
            Assert.That(_horror.AdvanceRunClock(2f), Is.True);
            Assert.That(_horror.AdvanceRunClock(deltaSeconds), Is.False);
            Assert.That(_horrorDriver.AdvanceRunClock(deltaSeconds), Is.False);
            Publish(_run, "TickAdvanced", default(InputFrame), deltaSeconds, 1L);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(2d));
        }

        [Test]
        public void DisabledOwnersRejectClockCommandsAndUninitializedHorrorFailsClosed()
        {
            Assert.That(_horror.AdvanceRunClock(2f), Is.True);
            _horror.enabled = false;
            Invoke(_horror, "OnDisable");
            Assert.That(_horror.AdvanceRunClock(1f), Is.False);
            Assert.That(_horrorDriver.AdvanceRunClock(1f), Is.False);
            Assert.That(_horror.RunElapsedSeconds, Is.EqualTo(2d));
            _horror.enabled = true;
            Invoke(_horror, "OnEnable");
            _horrorDriver.enabled = false;
            Assert.That(_horror.AdvanceRunClock(1f), Is.False);
            _horrorDriver.enabled = true;
            var uninitialized = Component<HorrorManager>();
            Assert.That(double.IsNaN(uninitialized.RunElapsedSeconds), Is.True);
            Assert.That(uninitialized.AdvanceRunClock(1f), Is.False);
            _postRoute.Configure(_run, _post, horror: uninitialized);
            Intrude(5d, false);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
            Assert.That(_clockReads, Is.Zero);
        }

        [Test]
        public void EffectsSynchronizeOnConnectChangeCaptureResetAndRestart()
        {
            Assert.That(Post.ActiveEffects, Is.Not.Null);
            IReadOnlyActiveEffects view = new ActiveEffects(new[] { new ActiveEffect(new EffectId("blinded"), EffectKind.Curse, 1) });
            Publish(_progression, "EffectsSnapshotChanged", _progression.Snapshot, view);
            Assert.That(Post.ActiveEffects, Is.SameAs(view));
            Assert.That(_horrorState.ActiveEffects, Is.SameAs(view));
            Post.ActiveEffects = null;
            Publish(_run, "CaptureStarted", default(RunCaptureMetadata));
            Assert.That(Post.ActiveEffects, Is.Not.Null);
            int generation = OpenFloor();
            Assert.That(_progression.EndRun(generation), Is.True);
            Assert.That(_progression.RestartRun(_progression.Snapshot.Revision), Is.True);
            Assert.That(Post.ActiveEffects, Is.Not.Null);
            Assert.That(Post.ActiveEffects.Has(new EffectId("blinded")), Is.False);
            Assert.That(_horrorState.ActiveEffects, Is.EquivalentTo(Post.ActiveEffects));
            _postRoute.Configure(_run, _post, progression: _progression);
            Assert.That(Subscribers(_progression, "EffectsSnapshotChanged"), Is.EqualTo(2));
            Activate(_postRoute, false); Activate(_horrorRoute, false);
            Assert.That(Subscribers(_progression, "EffectsSnapshotChanged"), Is.Zero);
            Invoke(_postRoute, "OnDestroy"); Invoke(_horrorRoute, "OnDestroy");
            Assert.That(Subscribers(_progression, "EffectsSnapshotChanged"), Is.Zero);
            var replacement = Component<ProgressionSessionManager>();
            _postRoute.Configure(_run, _post, progression: replacement);
            _horrorRoute.Configure(_run, replacement, _input, _horror);
            Activate(_postRoute, true); Activate(_horrorRoute, true);
            Assert.That(Subscribers(_progression, "EffectsSnapshotChanged"), Is.Zero);
            Assert.That(Subscribers(replacement, "EffectsSnapshotChanged"), Is.EqualTo(2));
            Activate(_postRoute, false); Activate(_horrorRoute, false);
            Assert.That(Subscribers(replacement, "EffectsSnapshotChanged"), Is.Zero);
        }

        [TestCase(FloorTrapKind.Blind, 2.5f)]
        [TestCase(FloorTrapKind.Slow, 0f)]
        [TestCase(FloorTrapKind.Announce, 0f)]
        public void BlindTrapUsesConfigAndPairsAcrossRebind(FloorTrapKind kind, float expected)
        {
            Publish(_run, "TrapSprung", new FloorTrapSprungFact(1, kind, 1, Vector3.zero, 1, new EntityId(7)));
            Assert.That(Post.BlindnessRemaining, Is.EqualTo(expected));
            Set(_postConfig, "_blindTrapSeconds", 4f);
            Publish(_run, "TrapSprung", new FloorTrapSprungFact(2, FloorTrapKind.Blind, 1, Vector3.zero, 2, new EntityId(7)));
            Assert.That(Post.BlindnessRemaining, Is.EqualTo(4f));
            var replacement = Component<RunSessionManager>();
            _postRoute.Configure(replacement, _post);
            Assert.That(Subscribers(_run, "TrapSprung"), Is.Zero);
            Assert.That(Subscribers(replacement, "TrapSprung"), Is.EqualTo(1));
            Activate(_postRoute, false);
            Assert.That(Subscribers(replacement, "TrapSprung"), Is.Zero);
        }

        private static void Invoke(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Activate(MonoBehaviour target, bool active)
        {
            Invoke(target, "OnDisable");
            target.gameObject.SetActive(active);
            if (active) { Invoke(target, "OnDisable"); Invoke(target, "OnEnable"); }
        }
        private int OpenFloor()
        {
            while (_progression.Snapshot.Phase == ProgressionPhase.ChooseThreat || _progression.Snapshot.Phase == ProgressionPhase.ChooseCurse)
            {
                ProgressionSnapshot snapshot = _progression.Snapshot;
                Assert.That(snapshot.Choices, Is.Not.Empty);
                bool accepted = snapshot.Phase == ProgressionPhase.ChooseThreat
                    ? _progression.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision)
                    : _progression.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision);
                Assert.That(accepted, Is.True);
            }
            int generation = _progression.Snapshot.GenerationId;
            Assert.That(_progression.ConfirmFloorReady(generation), Is.True);
            return generation;
        }
        private void Intrude(double seconds, bool admitted)
        {
            _wholeRunSeconds = seconds; _post.ResetEffects();
            Publish(_run, "IntrusionPublished", new IntrusionSample(new EntityId(7), 1, 2f));
            Assert.That(Post.IntrusionRemaining, Is.EqualTo(admitted ? 2f : 0f));
            Assert.That(Post.SubtleIntrusionRemaining, Is.EqualTo(admitted ? 0f : 2f));
        }
        private void AdvanceGameplayClock(float deltaSeconds)
        {
            var controller = (RunSessionController)Field(_run, "controller").GetValue(_run);
            Assert.That(controller.TryTick(deltaSeconds, out InputFrame frame), Is.True);
            Publish(_run, "TickAdvanced", frame, deltaSeconds, _run.Tick);
        }
        private double ReadClock() { _clockReads++; return _wholeRunSeconds; }
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " look routing test");
            _objects.Add(owner); owner.SetActive(false); return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static int Subscribers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
        private sealed class CountingRandom : System.Random
        {
            public int Draws;
            public override double NextDouble() { Draws++; return 0d; }
        }
    }
}
