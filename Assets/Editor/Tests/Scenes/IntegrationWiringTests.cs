// ============================================================================
// IntegrationWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises permitted scene routing boundaries without loading or saving scenes.
//   Injected receiver/getter seams explicitly represent APIs pending other owners;
//   they do not stand in for an end-to-end HorrorRun acceptance run.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Scenes.
// KEY RESPONSIBILITIES:
//   - Check held selection, pause/death/floor clearing and output-camera forwarding.
//   - Check actual-visibility and heartbeat getter forwarding plus disable cleanup.
//   - Check authored-furniture callback and Blinder launch/hit/tick/reset pairing.
// DEPENDENCIES:
//   Core, Session, Presentation, Orchestrators, NUnit and reflection.
// USAGE NOTES:
//   Coordinator-run native Edit Mode; no assets, scene loads or persistent services.
//   Native Drivers teardown explicitly even when lifecycle is invoked manually.
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
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Camera;
using Worsen.Presentation.HeldItem;
using Worsen.Presentation.HUD;
using Worsen.Presentation.ProgressionUI;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Input;
using Worsen.Presentation.Environment;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Scenes
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class IntegrationWiringTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }
        [Test] public void HeldSelectionSeedsAndSuppressesThroughPauseCatchAndFloorRelease()
        {
            var progression = Make<ProgressionSessionManager>(); var run = Make<RunSessionManager>();
            var expedition = Make<ExpeditionSessionManager>(); var held = Make<HeldItemManager>();
            var driver = held.gameObject.AddComponent<HeldItemDriver>(); var visual = new HeldItemDriverState();
            Set(driver, "_state", visual); Set(held, "_driver", driver);
            var camera = Make<CameraManager>(); var cameraDriver = camera.gameObject.AddComponent<CameraDriver>();
            var cameraState = new CameraDriverState(); Set(cameraDriver, "_state", cameraState); Set(camera, "_driver", cameraDriver);
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var state = new ProgressionSessionBehaviorState();
            var controller = new ProgressionSessionController(state, config, new System.Random(7));
            Set(progression, "state", state); Set(progression, "controller", controller); Set(progression, "config", config);
            Property(state, "Health", 100f); Property(state, "MaximumHealth", 100f); Property(state, "Phase", ProgressionPhase.Exploring);
            var shop = state.GetType().GetProperty("Shop", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            var inventory = (List<ProgressionInventorySlot>)shop.GetType().GetProperty("Inventory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shop);
            inventory.Add(new ProgressionInventorySlot("gauze", "Gauze", 1));
            var runState = new RunSessionBehaviorState(7); Property(runState, "Phase", RunPhase.FirstSweep); Set(run, "state", runState);
            var route = Make<HeldItemOrchestrator>();
            try
            {
                route.Configure(progression, run, expedition, held, camera); Call(route, "OnEnable"); Call(route, "OnEnable");
                Assert.That(visual.SelectedId, Is.EqualTo("gauze")); Assert.That(visual.Suppressed, Is.False);
                Assert.That(((Delegate)Read(progression, "ConsumablesChanged")).GetInvocationList(), Has.Length.EqualTo(1));
                Publish(progression, "ConsumablesChanged", new ConsumableInventorySnapshot(new[] { new ProgressionInventorySlot("firecracker", "Firecracker", 1) }, new[] { 2 }, 0));
                Assert.That(visual.SelectedId, Is.EqualTo("firecracker"));
                Property(runState, "Paused", true); Call(route, "Update"); Assert.That(visual.Suppressed, Is.True);
                Property(runState, "Paused", false); Call(route, "Update"); Assert.That(visual.Suppressed, Is.False);
                cameraState.DeathSnapped = true; Call(route, "Update"); Assert.That(visual.Suppressed, Is.True);
                cameraState.DeathSnapped = false; Call(route, "Update"); Assert.That(visual.Suppressed, Is.False);
                foreach (ProgressionPhase phase in Enum.GetValues(typeof(ProgressionPhase)))
                {
                    Property(state, "Phase", phase); Call(route, "Update");
                    Assert.That(visual.Suppressed, Is.EqualTo(phase != ProgressionPhase.Exploring), phase.ToString());
                }
                Publish(expedition, "FloorReleased"); Assert.That(visual.SelectedId, Is.Empty); Assert.That(visual.Suppressed, Is.True);
                Call(route, "OnDisable"); Assert.That(Read(progression, "ConsumablesChanged"), Is.Null);
                Assert.That(Read(run, "PlayerDeathPending"), Is.Null); Assert.That(Read(expedition, "FloorReleased"), Is.Null);
            }
            finally { Call(route, "OnDisable"); held.Teardown(); cameraDriver.Teardown(); Object.DestroyImmediate(config); }
        }
        [Test] public void OutputCameraAndDeathLatchForwardWithoutSceneSearch()
        {
            var manager = Make<CameraManager>(); var driver = manager.gameObject.AddComponent<CameraDriver>();
            var camera = Make<UnityEngine.Camera>(); var state = new CameraDriverState();
            try
            {
                Assert.That(manager.OutputCamera, Is.Null);
                Set(manager, "_driver", driver); Set(driver, "_outputCamera", camera); Set(driver, "_state", state);
                Assert.That(driver.OutputCamera, Is.SameAs(camera)); Assert.That(manager.OutputCamera, Is.SameAs(camera));
                state.DeathSnapped = true; Assert.That(manager.IsDeathPresentationActive, Is.True);
                state.DeathSnapped = false; Assert.That(manager.IsDeathPresentationActive, Is.False);
            }
            finally { driver.Teardown(); }
        }
        [Test] public void ModalGetterNotProgressionPhaseControlsSuppressionAndDisableClearsIt()
        {
            var ui = Make<ProgressionUIManager>(); Set(ui, "_initialized", true); ui.gameObject.SetActive(true);
            var hud = Make<HUDManager>(); var driver = hud.gameObject.AddComponent<HUDDriver>();
            var state = new HUDDriverState(); Set(driver, "_state", state); Set(driver, "_presenter", new HUDPresenter());
            Set(hud, "_driver", driver); Set(hud, "_initialized", true);
            var route = Make<ProgressionUIOrchestrator>(); bool visible = true;
            try
            {
                hud.SetHealth(35f, 100f);
                route.Configure(Make<ProgressionSessionManager>(), ui, hud: hud, modalVisible: () => visible);
                Call(route, "OnEnable"); Assert.That(state.ModalOpen, Is.True);
                Assert.That(state.HealthText, Is.EqualTo("35 / 100"));
                visible = false; Call(route, "LateUpdate"); Assert.That(state.ModalOpen, Is.False);
                visible = true; Call(route, "LateUpdate"); Assert.That(state.ModalOpen, Is.True);
                ui.gameObject.SetActive(false); Call(route, "LateUpdate"); Assert.That(state.ModalOpen, Is.False);
                ui.gameObject.SetActive(true); Call(route, "LateUpdate"); Call(route, "OnDisable");
                Assert.That(state.ModalOpen, Is.False); Assert.That(state.HealthText, Is.EqualTo("35 / 100"));
            }
            finally { Call(route, "OnDisable"); ui.Teardown(); driver.Teardown(); }
        }
        [Test] public void HeartbeatUsesInjectedEnvelopeAndClearsOnRebindAndDisable()
        {
            var post = Make<PostFXManager>(); var driver = post.gameObject.AddComponent<PostFXDriver>();
            var state = new PostFXDriverState(); Set(driver, "_state", state); Set(driver, "_presenter", new PostFXPresenter());
            Set(post, "_driver", driver); Set(post, "_initialized", true);
            var route = Make<PostFXOrchestrator>(); float envelope = .75f;
            try
            {
                route.Configure(Make<RunSessionManager>(), post, heartbeatEnvelope: () => envelope);
                Call(route, "LateUpdate"); Assert.That(state.HeartbeatEnvelope, Is.EqualTo(.75f));
                envelope = 0f; Call(route, "LateUpdate"); Assert.That(state.HeartbeatEnvelope, Is.Zero);
                envelope = 1f; Call(route, "LateUpdate"); Call(route, "OnDisable"); Assert.That(state.HeartbeatEnvelope, Is.Zero);
                route.Configure(null, post); Call(route, "LateUpdate"); Assert.That(state.HeartbeatEnvelope, Is.Zero);
            }
            finally { Call(route, "OnDisable"); post.Teardown(); }
        }
        [Test] public void FurnishedRoomCallbackReachesEnvironmentThroughFourthArgument()
        {
            var environment = Make<EnvironmentManager>(); var driver = environment.GetComponent<EnvironmentDriver>(); Set(environment, "_driver", driver);
            var expedition = Make<ExpeditionSessionManager>(); var route = Make<EnvironmentOrchestrator>(); var observed = new List<int>();
            try
            {
                route.Configure(Make<RunSessionManager>(), expedition, Make<HorrorEffectsManager>(), environment,
                    authoredFurniture: id => { observed.Add(id); return true; }); Call(route, "OnEnable");
                Publish(expedition, "RoomsReady", (object)new[] { new GeneratedRoomSample(7, new Bounds(Vector3.zero, Vector3.one), false, false, Array.Empty<Vector3>()) });
                Assert.That(observed, Is.EqualTo(new[] { 7 }));
                Call(route, "OnDisable"); Assert.That(Read(expedition, "RoomsReady"), Is.Null);
            }
            finally { Call(route, "OnDisable"); driver.Teardown(); }
        }
        [Test] public void BlinderCallbacksPairLaunchHitTickAndFloorClearWithoutDriverBypass()
        {
            var run = Make<RunSessionManager>(); var expedition = Make<ExpeditionSessionManager>();
            var horror = Make<HorrorManager>(); var route = Make<HorrorOrchestrator>();
            int launches = 0, hits = 0, clears = 0; float seconds = 0f;
            try
            {
                route.Configure(run, Make<ProgressionSessionManager>(), Make<InputManager>(), horror, expedition: expedition);
                route.ConfigureProjectilePresentation(_ => launches++, _ => hits++, dt => seconds += dt, () => clears++);
                Call(route, "OnEnable"); Call(route, "OnEnable");
                Publish(run.HunterFacts, "BlinderThrowPublished", default(BlinderThrowFact));
                Publish(run.HunterFacts, "BlinderHitPublished", default(BlinderHitFact));
                Publish(run, "TickAdvanced", default(InputFrame), .25f, 1L);
                Assert.That(launches, Is.EqualTo(1)); Assert.That(hits, Is.EqualTo(1)); Assert.That(seconds, Is.EqualTo(.25f));
                int before = clears; Publish(expedition, "FloorReleased"); Assert.That(clears, Is.EqualTo(before + 1));
                Call(route, "OnDisable"); Assert.That(Read(run.HunterFacts, "BlinderThrowPublished"), Is.Null);
                Assert.That(Read(run.HunterFacts, "BlinderHitPublished"), Is.Null); Assert.That(Read(run, "TickAdvanced"), Is.Null);
            }
            finally { Call(route, "OnDisable"); horror.GetComponent<HorrorDriver>().Teardown(); }
        }
        private T Make<T>() where T : Component
        { var go = new GameObject(typeof(T).Name + " integration test"); go.SetActive(false); _objects.Add(go); return go.AddComponent<T>(); }
        private static object Read(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Property(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string name, params object[] args) => ((Delegate)Read(target, name))?.DynamicInvoke(args);
    }
}
