// ============================================================================
// RunInterfaceRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises authoritative pause and HorrorRun terminal Results with real routers.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Results/Menu routing.
// KEY RESPONSIBILITIES:
//   - Verify pause acknowledgement/restoration, catch release, history and seed restart.
//   - Reject per-floor Results and duplicate restart after progression advances.
// DEPENDENCIES:
//   NUnit, Core, Session Run/Progression/Settings, Menu/Results and Orchestrators.
// USAGE NOTES:
//   Inactive Edit Mode objects bypass UI creation and persistent initialization.
//   Reflection injects state and publishes existing event boundaries; no file IO.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Session.Settings;
using Worsen.Presentation.Menu;
using Worsen.Presentation.Results;
using Worsen.Tests.Settings;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Results
{
    public sealed class RunInterfaceRoutingTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private RunSessionManager _run;
        private ProgressionSessionManager _progression;
        private SettingsManager _settings;
        private MenuManager _menu;
        private MenuOrchestrator _menuRoute;
        private ResultsManager _results;
        private ResultsOrchestrator _resultsRoute;
        private ResultsDriverState _view;
        private float _timeScale;
        [SetUp] public void SetUp()
        {
            _timeScale = Time.timeScale;
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            Assert.That(RunSessionManager.Instance, Is.Null);
            _run = Component<RunSessionManager>(); var runState = new RunSessionBehaviorState(73);
            var clock = new RunSessionController(runState, new System.Random(73)); clock.StartScene(SceneKey.HorrorRun);
            Set(_run, "state", runState); Set(_run, "controller", clock);
            _progression = Component<ProgressionSessionManager>(); var config = Config<ProgressionConfig>();
            var state = new ProgressionSessionBehaviorState(); Set(_progression, "state", state); Set(_progression, "config", config);
            Set(_progression, "controller", new ProgressionSessionController(state, config, new System.Random(73)));
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, _progression);
            _settings = Component<SettingsManager>(); var defaults = Config<SettingsConfig>(); var settingsState = new SettingsBehaviorState();
            Set(_settings, "_state", settingsState); Set(_settings, "_controller", new SettingsController(settingsState, defaults.Defaults));
            Set(_settings, "_driver", _settings.gameObject.AddComponent<FakeSettingsFileDriver>());
            _menu = Component<MenuManager>(); Set(_menu, "_driver", _menu.gameObject.AddComponent<MenuDriver>()); Set(_menu, "_initialized", true); Invoke(_menu, "OnEnable");
            _menuRoute = Component<MenuOrchestrator>(); _menuRoute.Configure(_menu, _settings, null, _progression, run: _run); Invoke(_menuRoute, "OnEnable");
            _results = Component<ResultsManager>(); var driver = _results.gameObject.AddComponent<ResultsDriver>();
            _view = new ResultsDriverState(); Set(driver, "_state", _view); Set(driver, "_presenter", new ResultsPresenter());
            Set(_results, "_driver", driver); Set(_results, "_initialized", true);
            var root = Component<HorrorRunSceneRoot>(); Set(root, "_progression", _progression);
            var restart = (Action<bool, int>)Delegate.CreateDelegate(typeof(Action<bool, int>), root, Method(root, "RestartFromResults"));
            _resultsRoute = Component<ResultsOrchestrator>(); _resultsRoute.ConfigureHorrorRun(_run, _results, null, _progression, _settings, restart);
            Invoke(_resultsRoute, "OnEnable");
            _progression.StartRun(73); var snapshot = _progression.Snapshot;
            Assert.That(_progression.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision), Is.True); snapshot = _progression.Snapshot;
            Assert.That(_progression.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision), Is.True);
            Assert.That(_progression.ConfirmFloorReady(_progression.Snapshot.GenerationId), Is.True);
        }
        [TearDown] public void TearDown()
        {
            Invoke(_menuRoute, "OnDisable"); Invoke(_resultsRoute, "OnDisable"); Invoke(_menu, "OnDisable");
            foreach (Object item in _owned) if (item is GameObject) Object.DestroyImmediate(item);
            foreach (Object item in _owned) if (item != null) Object.DestroyImmediate(item);
            _owned.Clear(); Time.timeScale = _timeScale;
        }
        [Test]
        public void PauseIntentAcknowledgesRunAndDisableRestoresTimeScale()
        {
            Time.timeScale = .75f; _menu.TogglePause();
            Assert.That(_run.IsPaused, Is.True); Assert.That(Time.timeScale, Is.Zero);
            _menu.TogglePause(); Assert.That(_run.IsPaused, Is.False); Assert.That(Time.timeScale, Is.EqualTo(.75f));
            _menu.TogglePause(); Invoke(_menuRoute, "OnDisable");
            Assert.That(_run.IsPaused, Is.False); Assert.That(Time.timeScale, Is.EqualTo(.75f));
            Assert.That(Get(_run, "PauseChanged"), Is.Null);
        }
        [Test]
        public void FloorEscapeStaysHiddenButDeathReleasesResultsAndRoutesFixedSeedOnce()
        {
            var ended = (Action<RunSummary>)Get(_run, "RunEnded");
            ended(new RunSummary(10, 1, 0, 0, 0, 0, RunEndReason.Escaped, 73, SceneKey.HorrorRun));
            Assert.That(_view.Visible || _view.HasPendingSummary, Is.False);
            var player = new EntityId(1); ((Action<EntityId, Vector3>)Get(_run, "PlayerDied"))(player, Vector3.zero);
            Assert.That(_progression.EndRun(_progression.Snapshot.GenerationId), Is.True);
            ended(new RunSummary(12, 1, 0, 0, 0, 0, RunEndReason.Died, 73, SceneKey.HorrorRun, DeathCause.Hand, "", 1, -1, 1));
            Assert.That(_view.HasPendingSummary, Is.True); Assert.That(_view.Visible, Is.False);
            _results.EndCatch(player); Assert.That(_view.Visible, Is.True); Assert.That(_view.Cause, Is.EqualTo("Hand"));
            Assert.That(_view.BestDepth, Is.EqualTo("1")); Assert.That(_settings.History.LifetimeRuns, Is.EqualTo(1));
            var presenter = new ResultsPresenter(); Assert.That(presenter.SetNextSeed(_view, "-731"), Is.True);
            Assert.That(presenter.TryRestart(_view), Is.True);
            var restart = (Action<bool, int>)Get(_results, "RestartWithSeedRequested");
            restart(_view.UseFixedSeed, _view.NextSeed);
            Assert.That(_progression.Snapshot.Seed, Is.EqualTo(-731)); Assert.That(_view.Visible, Is.False);
            int revision = _progression.Snapshot.Revision; restart(true, 99);
            Assert.That(_progression.Snapshot.Revision, Is.EqualTo(revision)); Assert.That(_progression.Snapshot.Seed, Is.EqualTo(-731));
            Invoke(_resultsRoute, "OnDisable"); Assert.That(Get(_results, "RestartWithSeedRequested"), Is.Null);
        }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); _owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); _owned.Add(value); return value; }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static MethodInfo Method(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Invoke(object target, string method) => Method(target, method).Invoke(target, null);
    }
}
