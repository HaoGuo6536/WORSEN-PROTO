// ============================================================================
// NoFloorResultsTests.cs
// ============================================================================
// PURPOSE:
//   Verifies failure presentation and routing separately from ordinary terminal results.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Results.
// KEY RESPONSIBILITIES:
//   - Cover failed generation and independent Expedition fallback evidence.
//   - Check new-seed Retry, title intent, ordinary outcomes and paired subscriptions.
// DEPENDENCIES:
//   NUnit, Core, Session Run/Progression/Expedition, Results and ResultsOrchestrator.
// USAGE NOTES:
//   Inactive Edit Mode objects with injected passive state; no Unity scene loads/file IO.
//   Reflection publishes existing event boundaries; no gameplay services are initialized.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Results;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Session.Expedition;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Results
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class NoFloorResultsTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();
        private ResultsPresenter _presenter;
        private ResultsDriverState _view;
        private ResultsManager _results;
        private ResultsOrchestrator _route;
        private ProgressionSessionManager _progression;
        private ExpeditionSessionManager _expedition;
        private ExpeditionSessionBehaviorState _assembly;
        private int _retries, _titles;
        private bool _fixed;
        [SetUp] public void SetUp()
        {
            Assert.That(RunSessionManager.Instance == null, Is.True, "No foreign Run owner.");
            var run = Component<RunSessionManager>();
            _progression = Component<ProgressionSessionManager>();
            var progressionState = new ProgressionSessionBehaviorState();
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                Set(_progression, "state", progressionState);
                Set(_progression, "controller", new ProgressionSessionController(progressionState, config, new System.Random(73)));
                _expedition = Component<ExpeditionSessionManager>(); _assembly = new ExpeditionSessionBehaviorState();
                Set(_expedition, "_state", _assembly);
                _results = Component<ResultsManager>(); var driver = _results.gameObject.AddComponent<ResultsDriver>();
                _presenter = new ResultsPresenter(); _view = new ResultsDriverState();
                Set(driver, "_state", _view); Set(driver, "_presenter", _presenter);
                Set(_results, "_driver", driver); Set(_results, "_initialized", true);
                _retries = _titles = 0;
                _route = Component<ResultsOrchestrator>();
                _route.ConfigureHorrorRun(run, _results, null, _progression, null,
                    (fixedSeed, seed) => { _retries++; _fixed = fixedSeed; }, _expedition, () => _titles++);
                Invoke(_route, "OnEnable");
            }
            finally { Object.DestroyImmediate(config); }
        }
        [TearDown] public void TearDown()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            foreach (var item in _owned) if (item != null) Object.DestroyImmediate(item);
            _owned.Clear();
        }
        [Test] public void FailureIsImmediateUsesFloorSeedAndRetryMustRequestNewSeed()
        {
            ((Action<ProgressionGenerationRequest>)Get(_progression, "GenerationRequested"))(
                new ProgressionGenerationRequest(18, -731, 4, false, default));
            ((Action<ProgressionSnapshot>)Get(_progression, "SnapshotChanged"))(Snapshot(ProgressionPhase.GenerationFailed));
            Assert.That(_view.Visible && _view.NoFloor, Is.True); Assert.That(_view.HasPendingSummary, Is.False);
            Assert.That(_view.EndReason, Is.EqualTo("The castle would not form. Seed -731."));
            Assert.That(_presenter.SetNextSeed(_view, "123"), Is.False);
            Assert.That(_presenter.TryRestart(_view), Is.True); Assert.That(_presenter.TryReturnToTitle(_view), Is.False);
            ((Action<bool, int>)Get(_results, "RestartWithSeedRequested"))(_view.UseFixedSeed, _view.NextSeed);
            Assert.That(_retries, Is.EqualTo(1)); Assert.That(_fixed, Is.False);
            _results.ShowNoFloor(777); Assert.That(_presenter.TryRestart(_view), Is.False, "Repeated facts cannot rearm navigation.");
        }
        [Test] public void UsedFallbackShowsFailureWithoutFailedPhaseAndTitleRoutesThenUnsubscribes()
        {
            typeof(ExpeditionSessionBehaviorState).GetProperty("UsedFallback").SetValue(_assembly, true);
            ((Action<ProgressionSnapshot>)Get(_progression, "SnapshotChanged"))(Snapshot(ProgressionPhase.Exploring));
            Assert.That(_view.NoFloor && _view.Visible, Is.True);
            ((Action<ProgressionSnapshot>)Get(_progression, "SnapshotChanged"))(Snapshot(ProgressionPhase.ChooseThreat));
            Assert.That(_view.Visible, Is.False, "Stale fallback must not cover the new run's selections.");
            ((Action<ProgressionSnapshot>)Get(_progression, "SnapshotChanged"))(Snapshot(ProgressionPhase.Exploring));
            Assert.That(_presenter.TryReturnToTitle(_view), Is.True);
            ((Action)Get(_results, "ReturnToTitleRequested"))();
            Assert.That(_titles, Is.EqualTo(1)); Assert.That(_view.Visible, Is.False);
            Invoke(_route, "OnDisable");
            Assert.That(Get(_results, "ReturnToTitleRequested"), Is.Null);
            Assert.That(Get(_results, "RestartWithSeedRequested"), Is.Null);
            Assert.That(Get(_progression, "SnapshotChanged"), Is.Null);
            Assert.That(Get(_progression, "GenerationRequested"), Is.Null);
            Assert.That(Get(_expedition, "AssemblyReady"), Is.Null);
        }
        [TestCase(RunEndReason.Died)] [TestCase(RunEndReason.Escaped)]
        public void OrdinaryOutcomesAreNotNoFloorAndCannotReturnViaFailureIntent(RunEndReason reason)
        {
            _view.CatchCompleted = true;
            _presenter.Show(_view, new RunSummary(12, 1, 0, 0, 0, 0, reason));
            Assert.That(_view.Visible, Is.True); Assert.That(_view.NoFloor, Is.False);
            Assert.That(_presenter.TryReturnToTitle(_view), Is.False);
            _presenter.ShowNoFloor(_view, 73);
            _presenter.Show(_view, new RunSummary(12, 1, 0, 0, 0, 0, reason));
            Assert.That(_view.NoFloor, Is.True, "An ordinary summary cannot overwrite failed generation.");
        }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); _owned.Add(go); return go.AddComponent<T>(); }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static ProgressionSnapshot Snapshot(ProgressionPhase phase) => new ProgressionSnapshot(1, 18, 4, 777,
            0, 1, 1, phase, 100, 100, null, null, null, default, "", false, true);
    }
}
