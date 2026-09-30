// ============================================================================
// CatchPresentationRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises catch routing through the actual Orchestrators and presentation Managers.
//   Publisher events are injected at their boundary so both lethal paths and missing
//   camera completion can be checked without loading a scene or advancing gameplay.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Camera integration.
// KEY RESPONSIBILITIES:
//   - Require death gates before the terminal snapshot, for hunter and hand deaths.
//   - Release both views only at the matching hold end; preserve immediate escapes.
//   - Verify missing-camera timeouts, capture reset and subscription teardown.
// DEPENDENCIES:
//   Core payloads, Run/Progression/SceneFlow, Results/ProgressionUI/Camera,
//   Orchestrators, NUnit and UnityEngine transient objects.
// USAGE NOTES:
//   Edit Mode boundary test, not physical death or live rendering evidence. Inactive
//   objects and injected DriverState avoid assets, persistent service initialization
//   and native camera work. Reflection invokes lifecycle and publisher delegates only.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using Worsen.Presentation.ProgressionUI;
using Worsen.Presentation.Results;
using Worsen.Session.Progression;
using Worsen.Session.Run;

using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Camera
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class CatchPresentationRoutingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private RunSessionManager _run;
        private ProgressionSessionManager _progression;
        private CameraManager _camera;
        private ProgressionUIOrchestrator _route;
        private ProgressionUIDriverState _uiState;
        private ResultsDriverState _resultsState;
        private ResultsOrchestrator _resultsRoute;

        [SetUp]
        public void SetUp()
        {
            Assert.That(RunSessionManager.Instance, Is.Null, "Requires an isolated Edit Mode fixture.");
            _run = Component<RunSessionManager>();
            _progression = Component<ProgressionSessionManager>();
            _camera = Component<CameraManager>();
            var ui = Component<ProgressionUIManager>();
            var uiDriver = ui.gameObject.AddComponent<ProgressionUIDriver>();
            _uiState = new ProgressionUIDriverState();
            Set(uiDriver, "_state", _uiState);
            Set(uiDriver, "_presenter", new ProgressionUIPresenter());
            Set(ui, "_driver", uiDriver);
            _route = Component<ProgressionUIOrchestrator>();
            _route.Configure(_progression, ui, _run, _camera);
            Invoke(_route, "OnEnable");

            var results = Component<ResultsManager>();
            var resultsDriver = results.gameObject.AddComponent<ResultsDriver>();
            _resultsState = new ResultsDriverState();
            Set(resultsDriver, "_state", _resultsState);
            Set(resultsDriver, "_presenter", new ResultsPresenter());
            Set(results, "_driver", resultsDriver);
            Set(results, "_initialized", true);
            _resultsRoute = Component<ResultsOrchestrator>();
            Set(_resultsRoute, "_run", _run);
            Set(_resultsRoute, "_results", results);
            // OnEnable initializes SceneFlow, so bind only its event routes below.
            // Results route handlers are exercised through real publisher subscriptions.
            BindResults("RunEnded", "OnRunEnded");
            BindResults("PlayerDied", "OnDeath");
            BindResults("CaptureStarted", "OnCapture");
            Set(_resultsRoute, "_camera", _camera);
            _camera.CatchHoldEnded += (Action<EntityId>)Delegate.CreateDelegate(typeof(Action<EntityId>),
                _resultsRoute, Method(_resultsRoute, "OnCatchEnded"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            if (_resultsRoute != null) Invoke(_resultsRoute, "OnDisable");
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothDeathPathsArmBeforeSnapshotAndCutOnlyAtHoldEnd(bool hand)
        {
            Publish(_progression, "SnapshotChanged", Snapshot(1, ProgressionPhase.Exploring));
            var player = new EntityId(7);
            if (hand) Publish(_run, "CollapseHandPublished", new CollapseHandFact(player, 1,
                CollapseHandEventKind.Consumed, Vector3.zero, 1f, 100f, 1));
            Publish(_run, "PlayerDied", player, Vector3.one);
            Assert.That(_uiState.TerminalDeferred, Is.True, "Gate must precede the terminal snapshot.");
            Publish(_run, "RunEnded", Summary(RunEndReason.Died));
            Publish(_progression, "SnapshotChanged", Snapshot(2, ProgressionPhase.Ended));
            Assert.That(_uiState.ModalVisible || _resultsState.Visible, Is.False);
            Assert.That(_resultsState.HasPendingSummary, Is.True);
            Publish(_camera, "CatchHoldStarted", player);
            Assert.That(_uiState.ModalVisible || _resultsState.Visible, Is.False);
            Publish(_camera, "CatchHoldEnded", new EntityId(8));
            Assert.That(_uiState.ModalVisible || _resultsState.Visible, Is.False);
            Publish(_camera, "CatchHoldEnded", player);
            Assert.That(_uiState.ModalVisible && _resultsState.Visible, Is.True);
            Assert.That(_uiState.CatchFallbackFired || _resultsState.CatchFallbackFired, Is.False);
        }

        [Test]
        public void EscapeDoesNotWaitForCamera()
        {
            Publish(_run, "RunEnded", Summary(RunEndReason.Escaped));
            Publish(_progression, "SnapshotChanged", Snapshot(1, ProgressionPhase.Shop));
            Assert.That(_uiState.ModalVisible && _resultsState.Visible, Is.True);
            Assert.That(_uiState.TerminalDeferred || _resultsState.HasPendingSummary, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CaptureResetAndRouteTeardownDiscardPendingDeath(bool teardown)
        {
            Publish(_progression, "SnapshotChanged", Snapshot(1, ProgressionPhase.Exploring));
            Publish(_run, "PlayerDied", new EntityId(7), Vector3.one);
            Publish(_run, "RunEnded", Summary(RunEndReason.Died));
            Publish(_progression, "SnapshotChanged", Snapshot(2, ProgressionPhase.Ended));
            if (teardown) { Invoke(_route, "OnDisable"); Invoke(_resultsRoute, "OnDisable"); }
            else Publish(_run, "CaptureStarted", default(RunCaptureMetadata));
            Assert.That(_uiState.HasDeferredTerminal || _resultsState.HasPendingSummary, Is.False);
            Publish(_camera, "CatchHoldEnded", new EntityId(7));
            Assert.That(_uiState.ModalVisible || _resultsState.Visible, Is.False);
            if (teardown)
            {
                Assert.That(Field(_camera, "CatchHoldEnded").GetValue(_camera), Is.Null);
                Publish(_run, "PlayerDied", new EntityId(7), Vector3.one);
                Assert.That(_uiState.TerminalDeferred, Is.False);
            }
        }

        [Test]
        public void CameraDisappearingCannotLeaveEitherTerminalPendingForever()
        {
            Publish(_progression, "SnapshotChanged", Snapshot(1, ProgressionPhase.Exploring));
            Publish(_run, "PlayerDied", new EntityId(7), Vector3.one);
            Publish(_run, "RunEnded", Summary(RunEndReason.Died));
            Publish(_progression, "SnapshotChanged", Snapshot(2, ProgressionPhase.Ended));
            Object.DestroyImmediate(_camera.gameObject);
            var ui = new ProgressionUIPresenter(); var results = new ResultsPresenter();
            Assert.That(ui.Tick(_uiState, 2f), Is.False);
            Assert.That(results.Tick(_resultsState, 2f), Is.False);
            Assert.That(ui.Tick(_uiState, .1f), Is.True);
            Assert.That(results.Tick(_resultsState, .1f), Is.True);
            Assert.That(_uiState.ModalVisible && _resultsState.Visible, Is.True);
            Assert.That(_uiState.CatchFallbackFired && _resultsState.CatchFallbackFired, Is.True);
        }

        private void BindResults(string eventName, string handler)
        {
            var info = typeof(RunSessionManager).GetEvent(eventName);
            info.AddEventHandler(_run, Delegate.CreateDelegate(info.EventHandlerType, _resultsRoute,
                Method(_resultsRoute, handler)));
        }
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " catch test");
            _objects.Add(owner); owner.SetActive(false);
            return owner.AddComponent<T>();
        }
        private static RunSummary Summary(RunEndReason reason) => new RunSummary(10, 1, 0, 1, 0, 2, reason);
        private static ProgressionSnapshot Snapshot(int revision, ProgressionPhase phase) =>
            new ProgressionSnapshot(revision, 1, 1, 123, 0, 0, 0, phase, 0, 100,
                null, null, null, default, "", phase == ProgressionPhase.Shop, phase == ProgressionPhase.Ended);
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo Method(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name) => Method(target, name).Invoke(target, null);
        private static void Publish(object target, string name, params object[] args) =>
            (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
