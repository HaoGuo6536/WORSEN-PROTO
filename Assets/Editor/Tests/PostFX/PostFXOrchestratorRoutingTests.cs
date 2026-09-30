// ============================================================================
// PostFXOrchestratorRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises intrusion admission through the real routing and presentation command path.
//   Explicit publisher facts and a supplied whole-run clock isolate the budget from
//   floor-local Run resets, while real Progression commands exercise restart identity.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX routing integration.
// KEY RESPONSIBILITIES:
//   - Require one clock read and one admission attempt per Director intrusion.
//   - Preserve budget and spacing across floors; reset only on committed run starts.
//   - Verify same-seed restart, rejected restart and paired subscriptions.
// DEPENDENCIES:
//   Core; Session Run/Progression; Presentation PostFX/Horror/Input; Orchestrators;
//   NUnit, transient configuration and UnityEngine object lifetime.
// USAGE NOTES:
//   Edit Mode boundary tests, not live scene wiring or a production whole-run clock test.
//   Horror's state/presenter and inactive atmosphere boundary are injected to avoid
//   global rendering changes. Progression's canonical controller is injected to avoid
//   DontDestroyOnLoad in Edit Mode; public StartRun/RestartRun still publish real facts.
//   The coordinator must wire a Horror-owned clock that survives ResetRound.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
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
            _horror.gameObject.SetActive(true);
            _post = Component<PostFXManager>();
            _postDriver = _post.gameObject.AddComponent<PostFXDriver>();
            _postDriver.ConfigureForSetup();
            _postConfig = ScriptableObject.CreateInstance<PostFXDriverConfig>();
            Set(_post, "_driver", _postDriver); Set(_post, "_config", _postConfig);
            _post.gameObject.SetActive(true);
            _input = Component<InputManager>();
            _horrorRoute = Component<HorrorOrchestrator>();
            _horrorRoute.Configure(_run, _progression, _input, _horror);
            _horrorRoute.gameObject.SetActive(true);
            _postRoute = Component<PostFXOrchestrator>();
            _postRoute.Configure(_run, _post, horror: _horror, runSeconds: ReadClock);
            _postRoute.gameObject.SetActive(true);
            _progression.StartRun(731);
            _wholeRunSeconds = 0d; _clockReads = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_postRoute != null) _postRoute.gameObject.SetActive(false);
            if (_horrorRoute != null) _horrorRoute.gameObject.SetActive(false);
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            Object.DestroyImmediate(_horrorConfig); Object.DestroyImmediate(_postConfig);
            Object.DestroyImmediate(_progressionConfig);
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
            _postRoute.gameObject.SetActive(false); _horrorRoute.gameObject.SetActive(false);
            Assert.That(Subscribers(_run, "IntrusionPublished"), Is.Zero);
            Assert.That(Subscribers(_progression, "TransactionCommitted"), Is.Zero);
            _postRoute.gameObject.SetActive(true); _horrorRoute.gameObject.SetActive(true);
            Assert.That(_horrorState.StartlesUsed, Is.EqualTo(1));
            Intrude(6d, false);
        }

        [Test]
        public void MissingWholeRunClockFailsClosedWithoutConsumingBudget()
        {
            _postRoute.Configure(_run, _post, horror: _horror);
            Intrude(5d, false);
            Assert.That(_horrorState.StartlesUsed, Is.Zero);
            Assert.That(_clockReads, Is.Zero);
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
