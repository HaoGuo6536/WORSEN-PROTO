// ============================================================================
// RunPauseSummaryTests.cs
// ============================================================================
// PURPOSE:
//   Exercises pause admission and detailed outcomes at the real Run tick boundary.
//   Explicit clocks and transient actors avoid scene loading and persistent services.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Verify pre-pause damage waits, paused contacts drop, input clears and clocks exclude pause.
//   - Verify accepted hunter/trap deaths, lethal hands and escape summary fields.
// DEPENDENCIES:
//   NUnit, Core, Domain Player/Hunter and Session Run.
// USAGE NOTES:
//   Coordinator runs in Edit Mode. Reflection installs state and invokes engine
//   boundaries, not substitute gameplay calculations. No assets or files are saved.
// ============================================================================
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Hunter;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunPauseSummaryTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private RunSessionManager _run;
        private RunSessionController _clock;
        private PlayerManager _player;
        private readonly EntityId _hunterId = new EntityId(-1);
        [SetUp]
        public void SetUp()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            Assert.That(PlayerRegistry.Items, Is.Empty);
            _run = Component<RunSessionManager>();
            var state = new RunSessionBehaviorState(73);
            _clock = new RunSessionController(state, new System.Random(73));
            _clock.StartScene(SceneKey.HorrorRun);
            Set(_run, "state", state); Set(_run, "controller", _clock);
            typeof(RunSessionManager).GetProperty("Instance").SetValue(null, _run);
            _player = Component<PlayerManager>();
            var mover = Config<PlayerMoverDriverConfig>(); Set(mover, "_hunterBodyLayer", "Ignore Raycast");
            Set(_player.GetComponent<PlayerDriver>(), "_config", mover);
            _player.Initialize(Config<PlayerProfile>(), new EntityContext(new EntityId(1), new System.Random(73)));
            ((List<PlayerManager>)Get(_run, "players")).Add(_player);
            var hunter = Component<HunterManager>();
            var profile = Config<HunterProfile>(); Set(profile, "_archetypeKey", "summary-hunter");
            var hunterState = new HunterBehaviorState();
            typeof(HunterBehaviorState).GetProperty("Id").SetValue(hunterState, _hunterId);
            Set(hunter, "_state", hunterState); Set(hunter, "_profile", profile);
            ((List<HunterManager>)Get(_run, "hunters")).Add(hunter);
            Invoke(_run, "OnEnable");
        }
        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_run != null) _run.DetachGameplay();
                for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
                _owned.Clear();
            }
            finally { Worsen.Tests.Menu.PauseFixtureCleanup.Restore(); }
        }
        [Test]
        public void PauseGatesQueuedDamageBeforeInputAndTickAndResumeDiscardsBufferedControls()
        {
            int before = 0; _run.BeforeTick += () => before++;
            var input = new InputFrame(Vector2.up, Vector2.one, InputButtons.Sprint, InputButtons.Jump, InputButtons.None);
            _run.ReceiveInput(input);
            Invoke(_run, "QueueHit", new HunterHit(_hunterId, _player.Id, 10, 0, Vector3.right));
            _run.SetPaused(true);
            Invoke(_run, "QueueHit", new HunterHit(_hunterId, _player.Id, 100, 0, Vector3.left));
            _run.ReceiveInput(input); Invoke(_run, "FixedUpdate");
            Assert.That(before, Is.Zero); Assert.That(_run.Tick, Is.Zero);
            Assert.That(_run.ElapsedSeconds, Is.Zero); Assert.That(_player.ReadOnlyState.Health, Is.EqualTo(100));
            Assert.That(((List<HunterHit>)Get(_run, "pendingHits")).Count, Is.EqualTo(1));
            _run.SetPaused(false);
            Assert.That(_clock.TryTick(.5f, out var frame), Is.True);
            Assert.That(frame.Move, Is.EqualTo(Vector2.zero)); Assert.That(frame.LookDelta, Is.EqualTo(Vector2.zero));
            Assert.That(frame.Held | frame.Pressed | frame.Released, Is.EqualTo(InputButtons.None));
            Invoke(_run, "FixedUpdate");
            Assert.That(before, Is.EqualTo(1)); Assert.That(_player.ReadOnlyState.Health, Is.EqualTo(90));
            Assert.That((List<HunterHit>)Get(_run, "pendingHits"), Is.Empty);
        }
        [Test]
        public void SceneReadinessAndTeardownResetPauseWithoutClosingCaptureOnPause()
        {
            _clock.OpenCapture(); _run.SetPaused(true);
            Assert.That(_clock.TryTick(100, out _), Is.False);
            Assert.That(_clock.TryCloseCapture(), Is.True, "Pause must not interrupt capture.");
            _run.HandleSceneReady(SceneKey.HorrorRun);
            Assert.That(_run.IsPaused, Is.False); Assert.That(_run.ElapsedSeconds, Is.Zero);
            _run.SetPaused(true); _run.SuspendForSceneLoad();
            Assert.That(_run.IsPaused, Is.False); Assert.That(_run.CanPause, Is.False);
            _run.SetPaused(true); Assert.That(_run.IsPaused, Is.False);
        }
        [TestCase(HitSource.Lunge, DeathCause.Hunter)]
        [TestCase(HitSource.Trap, DeathCause.Trap)]
        public void AcceptedLethalHitSuppliesCauseAndArchetype(HitSource source, DeathCause cause)
        {
            RunSummary summary = default; _run.RunEnded += value => summary = value;
            _run.SetSummaryContext(731, 4);
            Invoke(_run, "QueueHit", new HunterHit(_hunterId, _player.Id, 100, 0, Vector3.right, source: source));
            Invoke(_run, "FixedUpdate");
            Assert.That(summary.EndReason, Is.EqualTo(RunEndReason.Died));
            Assert.That(summary.DeathCause, Is.EqualTo(cause)); Assert.That(summary.KillerArchetypeId, Is.EqualTo("summary-hunter"));
            Assert.That(summary.DepthReached, Is.EqualTo(4)); Assert.That(summary.Seed, Is.EqualTo(731));
        }
        [Test]
        public void LethalHandSuppliesHandCauseAndNoInventedArchetype()
        {
            Invoke(_run, "HandleLethal", new FloorLethalContactFact(_player.Id, 1, 0));
            Assert.That(_clock.TryFinish(out var summary), Is.True);
            Assert.That(summary.DeathCause, Is.EqualTo(DeathCause.Hand)); Assert.That(summary.KillerArchetypeId, Is.Empty);
        }
        [Test]
        public void EscapeSummaryCountsCommittedEscapesAndExcludesPausedExitTime()
        {
            _clock.TryTick(2, out _); Invoke(_run, "HandleExitOpened", _run.Tick);
            _clock.TryTick(1, out _); _run.SetPaused(true);
            Assert.That(_clock.TryTick(100, out _), Is.False);
            _run.SetPaused(false); _clock.TryTick(3, out _);
            Invoke(_run, "HandleCollapseHand", new CollapseHandFact(_player.Id, 1, CollapseHandEventKind.Escaped, Vector3.zero, 1, 0, _run.Tick));
            Invoke(_run, "HandleCollapseHand", new CollapseHandFact(_player.Id, 1, CollapseHandEventKind.Hit, Vector3.zero, 1, 0, _run.Tick));
            _run.SetSummaryContext(73, 5);
            _clock.RequestEnd(RunEndReason.Escaped, _player.Id, Vector3.zero);
            Assert.That(_clock.TryFinish(out var summary), Is.True);
            Assert.That(summary.ElapsedSeconds, Is.EqualTo(6)); Assert.That(summary.SecondsFromExitOpenToEscape, Is.EqualTo(4));
            Assert.That(summary.GrabsEscaped, Is.EqualTo(1)); Assert.That(summary.DepthReached, Is.EqualTo(5));
            Assert.That(summary.DeathCause, Is.EqualTo(DeathCause.None));
        }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); _owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var config = ScriptableObject.CreateInstance<T>(); _owned.Add(config); return config; }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
