// ============================================================================
// CameraHandCatchRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises confirmed hand death through Camera and Results routing using the
//   actual Managers and pure catch clock. Native LateUpdate is replaced only at
//   its event-publication boundary; this does not claim rendered or audible output.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Camera integration.
// KEY RESPONSIBILITIES:
//   - Route consumption into hand timing and release results only after the grab.
//   - Check one sting admission through the existing Audio feedback contract.
//   - Pair Camera subscriptions across reconfiguration and explicit teardown.
// DEPENDENCIES:
//   Core, Run/Progression, Camera/Results/Audio, Orchestrators, NUnit and reflection.
// USAGE NOTES:
//   Inactive transient objects; no scene assets, audio device or native camera tick.
//   AudioCatchRoutingTests separately covers the real route to CueId.Death.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Results;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Camera
{
    public sealed class CameraHandCatchRoutingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private CameraDriverConfig _config;
        private CameraDriverState _state;
        private CameraFeedbackPresenter _presenter;
        private CameraDriver _driver;
        private CameraManager _camera;
        private CameraOrchestrator _route;
        private ResultsOrchestrator _resultsRoute;
        private ResultsDriverState _results;
        private RunSessionManager _run;
        private readonly EntityId _player = new EntityId(7);

        [SetUp] public void Setup()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            _run = Component<RunSessionManager>();
            _config = ScriptableObject.CreateInstance<CameraDriverConfig>();
            _state = new CameraDriverState(); _presenter = new CameraFeedbackPresenter();
            _camera = Component<CameraManager>();
            _driver = _camera.gameObject.AddComponent<CameraDriver>();
            Set(_driver, "_state", _state); Set(_driver, "_presenter", _presenter); Set(_driver, "_config", _config);
            Set(_camera, "_driver", _driver); Set(_camera, "_initialized", true);
            Invoke(_camera, "OnEnable");
            _route = Component<CameraOrchestrator>();
            _route.Configure(_run, _camera); Invoke(_route, "OnEnable");
            var results = Component<ResultsManager>();
            var resultsDriver = results.gameObject.AddComponent<ResultsDriver>();
            _results = new ResultsDriverState();
            Set(resultsDriver, "_state", _results); Set(resultsDriver, "_presenter", new ResultsPresenter());
            Set(results, "_driver", resultsDriver); Set(results, "_initialized", true);
            _resultsRoute = Component<ResultsOrchestrator>();
            _resultsRoute.ConfigureHorrorRun(_run, results, _camera, Component<ProgressionSessionManager>(), null, null);
            Invoke(_resultsRoute, "OnEnable");
            Publish(_run, "PlayerMovementPublished", new PlayerMovementSample(_player, 1,
                Vector3.zero, Vector3.zero, Vector3.up, 0f, Vector2.zero, false, MovementState.Ground, 0f));
            _presenter.Tick(_state, _config, 0f, 1f);
        }
        [TearDown] public void Cleanup()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            if (_resultsRoute != null) Invoke(_resultsRoute, "OnDisable");
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Object.DestroyImmediate(_config);
        }
        [Test] public void ConfirmedHandDeathApproachesGrabsStingsOnceAndReleasesActualResultsGate()
        {
            int starts = 0, ends = 0, stings = 0;
            var feedback = new AudioFeedbackPresenter(); var sound = new AudioFeedbackDriverState();
            _camera.CatchHoldStarted += player => { starts++; if (feedback.TryCatchSting(sound, player)) stings++; };
            _camera.CatchHoldEnded += player => { Assert.That(player, Is.EqualTo(_player)); ends++; };
            Publish(_run, "CollapseHandPublished", new CollapseHandFact(_player, 1,
                CollapseHandEventKind.Grabbed, Vector3.forward, 1f, 0f, 1));
            Assert.That(_state.Consumed, Is.False);
            Publish(_run, "CollapseHandPublished", new CollapseHandFact(_player, 2,
                CollapseHandEventKind.Consumed, Vector3.forward, 1f, 100f, 1));
            Publish(_run, "PlayerDied", _player, Vector3.right);
            Publish(_run, "RunEnded", new RunSummary(10, 1, 0, 1, 0, 2, RunEndReason.Died));
            Assert.That(_state.Consumed && _results.HasPendingSummary, Is.True);
            Advance(_config.HandApproachSeconds / 2f);
            Assert.That(stings, Is.Zero); Assert.That(_results.Visible, Is.False);
            Advance(_config.HandApproachSeconds / 2f);
            Assert.That(stings, Is.EqualTo(1)); Assert.That(_results.Visible, Is.False);
            Advance(_config.HandGrabSeconds / 2f);
            Assert.That(_state.HandGrip, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(_results.Visible, Is.False);
            Advance(_config.HandGrabSeconds / 2f); Advance(10f);
            Assert.That(_results.Visible, Is.True); Assert.That(_results.CatchFallbackFired, Is.False);
            Assert.That(starts, Is.EqualTo(1)); Assert.That(ends, Is.EqualTo(1)); Assert.That(stings, Is.EqualTo(1));
        }
        [Test] public void ReconfigureAndManagerTeardownPairDriverAndRunSubscriptions()
        {
            _route.Configure(_run, _camera); Invoke(_route, "OnEnable"); Invoke(_camera, "OnEnable");
            Assert.That(Count(_run, "CollapseHandPublished"), Is.EqualTo(1));
            Assert.That(Count(_driver, "CatchHoldStarted"), Is.EqualTo(1));
            Assert.That(Count(_driver, "CatchHoldEnded"), Is.EqualTo(1));
            var replacement = Component<RunSessionManager>();
            _route.Configure(replacement, _camera); Invoke(_route, "OnEnable");
            Assert.That(Count(_run, "CollapseHandPublished"), Is.Zero);
            Assert.That(Count(replacement, "CollapseHandPublished"), Is.EqualTo(1));
            Invoke(_route, "OnDisable");
            Assert.That(Count(replacement, "CollapseHandPublished"), Is.Zero);
            _camera.Teardown(); _camera.Teardown();
            Assert.That(Count(_driver, "CatchHoldStarted"), Is.Zero);
            Assert.That(Count(_driver, "CatchHoldEnded"), Is.Zero);
        }
        private void Advance(float dt)
        {
            bool start = _state.CatchHoldStarted, end = _state.CatchHoldEnded;
            _presenter.Tick(_state, _config, dt, 1f);
            // Same edge boundary as CameraDriver.LateUpdate; native rendering is not exercised.
            if (!start && _state.CatchHoldStarted) Publish(_driver, "CatchHoldStarted", _state.PlayerId);
            if (!end && _state.CatchHoldEnded) Publish(_driver, "CatchHoldEnded", _state.PlayerId);
        }
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " hand test"); _objects.Add(owner);
            owner.SetActive(false); return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private static void Publish(object target, string name, params object[] values) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(values);
        private static int Count(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
    }
}
