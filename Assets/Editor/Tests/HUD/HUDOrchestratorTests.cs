// ============================================================================
// HUDOrchestratorTests.cs
// ============================================================================
// PURPOSE:
//   Verifies authoritative floor-count routing without rendering a document.
//   The real Managers and Drivers retain routed values in their presentation state.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · HUD routing.
// KEY RESPONSIBILITIES:
//   - Cover golden and occupied-slot updates, capture reset and subscription cleanup.
//   - Require the documented Hidden Count fallback at a fresh expedition boundary.
// DEPENDENCIES:
//   NUnit, Core, Run/Progression, HUD/ProgressionUI and their Orchestrators.
// USAGE NOTES:
//   Edit Mode boundary tests. Reflection supplies state and publisher facts only;
//   no scene loads, persistent services, asset writes or native UI rendering occur.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.HUD;
using Worsen.Presentation.ProgressionUI;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Object = UnityEngine.Object;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDOrchestratorTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private HUDDriverConfig _config;
        private RunSessionManager _run;
        private HUDOrchestrator _route;
        private HUDDriverState _state;
        [SetUp]
        public void SetUp()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            _config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            _run = Component<RunSessionManager>();
            var hud = Component<HUDManager>();
            var driver = hud.gameObject.AddComponent<HUDDriver>();
            _state = new HUDDriverState();
            Set(driver, "_state", _state); Set(driver, "_presenter", new HUDPresenter()); Set(driver, "_config", _config);
            Set(hud, "_driver", driver); Set(hud, "_initialized", true);
            _route = Component<HUDOrchestrator>();
            Set(_route, "_run", _run); Set(_route, "_hud", hud);
            Invoke(_route, "OnEnable");
        }
        [TearDown]
        public void TearDown()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Object.DestroyImmediate(_config);
        }
        [Test]
        public void GoldenUsesFloorSnapshotAndCaptureResetsItBeforeFirstPickup()
        {
            Publish(_run, "FloorDisplayChanged", new FloorDisplaySnapshot(3, 5, 7, ExitState.Locked, false, Vector3.zero));
            Assert.That(_state.GoldenText, Is.EqualTo("Golden: 7"));
            Publish(_run, "EmptyItemSlotsChanged", 1);
            Assert.That(_state.DisplayedSlots, Is.EqualTo(1));
            Publish(_run, "CaptureStarted", default(RunCaptureMetadata));
            Assert.That(_state.GoldenText, Is.EqualTo("Golden: 0"));
            Assert.That(_state.DisplayedSlots, Is.Zero);
            Publish(_run, "FloorDisplayChanged", new FloorDisplaySnapshot(1, 5, 2, ExitState.Locked, false, Vector3.zero));
            Assert.That(_state.GoldenText, Is.EqualTo("Golden: 2"));
        }
        [Test]
        public void RepeatedEnableAndDisableLeaveNoDuplicateOrStaleSubscribers()
        {
            Invoke(_route, "OnEnable");
            Assert.That(((Delegate)Field(_run, "FloorDisplayChanged").GetValue(_run)).GetInvocationList().Length, Is.EqualTo(1));
            Invoke(_route, "OnDisable");
            foreach (string name in new[] { "FloorDisplayChanged", "PlayerMovementPublished", "EmptyItemSlotsChanged", "ChaseStarted", "ChaseEnded", "CaptureStarted" })
                Assert.That(Field(_run, name).GetValue(_run), Is.Null, name);
        }
        [Test]
        public void FreshRunClearsHiddenCountAndFallbackSnapshotsKeepItFalse()
        {
            var progression = Component<ProgressionSessionManager>();
            var ui = Component<ProgressionUIManager>();
            var driver = ui.gameObject.AddComponent<ProgressionUIDriver>();
            var state = new ProgressionUIDriverState();
            Set(driver, "_state", state); Set(driver, "_presenter", new ProgressionUIPresenter());
            Set(ui, "_driver", driver);
            var route = Component<ProgressionUIOrchestrator>();
            route.Configure(progression, ui);
            Invoke(route, "OnEnable");
            try
            {
                ui.SetHiddenCount(true);
                Publish(progression, "TransactionCommitted", default(ProgressionSnapshot), default(ProgressionSnapshot), nameof(ProgressionSessionManager.StartRun), "");
                Assert.That(state.HideActiveHunters, Is.False);
                ui.SetHiddenCount(true);
                Publish(progression, "SnapshotChanged", new ProgressionSnapshot(1, 1, 1, 731, 0, 0, 0,
                    ProgressionPhase.ChooseThreat, 100, 100, null, null, null, default, "", false, false));
                Assert.That(state.HideActiveHunters, Is.False);
            }
            finally { Invoke(route, "OnDisable"); }
            Assert.That(Field(progression, "TransactionCommitted").GetValue(progression), Is.Null);
        }
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " count test");
            owner.SetActive(false); _objects.Add(owner); return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
