// ============================================================================
// PresentationWiringTests.cs
// ============================================================================
// PURPOSE:
//   Checks camera and horror world routing plus recorded micro-event outcomes.
//   Real Managers/Drivers consume publisher facts without starting gameplay or rendering.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror presentation integration.
// KEY RESPONSIBILITIES:
//   - Verify explicit landing severity, progress/stumble pairing and world replacement.
//   - Require player-open provenance and current tick/seed in the observation journal.
// DEPENDENCIES:
//   - Core, Domain Level, Session Run/Progression/Expedition, presentation and Orchestrators; NUnit.
// USAGE NOTES:
//   Edit Mode only, explicit lifecycle and injected driver state; no scene or shared assets.
//   Telemetry uses an in-memory stream. Singleton cleanup is explicit even on assertion failure.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Horror;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Input;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Horror
{
    public sealed class PresentationWiringTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Action> _cleanup = new List<Action>();

        [SetUp] public void SetUp()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            Assert.That(TelemetryManager.Instance, Is.Null);
        }
        [TearDown] public void TearDown()
        {
            try { for (int i = _cleanup.Count - 1; i >= 0; i--) _cleanup[i](); }
            finally
            {
                for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
                _owned.Clear(); _cleanup.Clear();
                typeof(TelemetryManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
                typeof(RunSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void CameraRoutesLandingSeverityNotDurationAndPairsProgressAndStumble(float severity)
        {
            var run = Component<RunSessionManager>();
            var camera = Component<CameraManager>();
            var driver = camera.gameObject.AddComponent<CameraDriver>();
            var config = Config<CameraDriverConfig>();
            var id = new EntityId(7);
            var state = new CameraDriverState { HasMovement = true, PlayerId = id };
            Set(driver, "_state", state); Set(driver, "_config", config); Set(driver, "_presenter", new CameraFeedbackPresenter());
            Set(camera, "_driver", driver); Set(camera, "_initialized", true);
            var route = Component<CameraOrchestrator>();
            _cleanup.Add(() => Invoke(route, "OnDisable"));
            route.Configure(run, camera); Invoke(route, "OnEnable");
            Assert.That(Subscribers(run, "PlayerTraversalPublished"), Is.EqualTo(1));
            Publish(run, "PlayerTraversalPublished", new PlayerTraversalFact(id, 1, TraversalKind.Land, true, Vector3.down, 99f, severity));
            Assert.That(state.LandingDepth, Is.EqualTo(severity == 1f ? config.HardLandingDip : config.SoftLandingDip));
            state.LandingElapsed = .1f;
            Publish(run, "PlayerTraversalPublished", new PlayerTraversalFact(id, 1, TraversalKind.Land, true, Vector3.down, 99f, severity));
            Assert.That(state.LandingElapsed, Is.EqualTo(.1f), "Duplicate facts cannot restart a landing.");
            Publish(run, "TraversalProgressed", id, 2L, TraversalKind.Vault, .5f, true);
            Assert.That(state.VaultActive, Is.True);
            Assert.That(state.VaultHeight, Is.EqualTo(config.VaultHeight.Evaluate(.5f)));
            Publish(run, "PlayerStumbled", id, 2L, .7f);
            Assert.That(state.StumbleDuration, Is.EqualTo(.7f));
            var replacement = Component<RunSessionManager>();
            route.Configure(replacement, camera); Invoke(route, "OnEnable");
            foreach (string name in new[] { "TraversalProgressed", "PlayerStumbled", "PlayerTraversalPublished" })
            { Assert.That(Subscribers(run, name), Is.Zero); Assert.That(Subscribers(replacement, name), Is.EqualTo(1)); }
            Invoke(route, "OnDisable"); Invoke(route, "OnEnable"); Invoke(route, "OnDestroy");
            foreach (string name in new[] { "TraversalProgressed", "PlayerStumbled", "PlayerTraversalPublished" })
                Assert.That(Subscribers(replacement, name), Is.Zero);
            Assert.That(new PlayerTraversalFact(id, 1, TraversalKind.Land, true, Vector3.down, 10f).Severity, Is.Zero);
        }

        [Test]
        public void FloorsConfigureWorldAndOnlyPlayerDoorOpensBecomeCandidates()
        {
            var run = Component<RunSessionManager>(); var progression = Component<ProgressionSessionManager>();
            var input = Component<InputManager>(); var expedition = Component<ExpeditionSessionManager>();
            var level = Component<LevelManager>(); level.gameObject.SetActive(true);
            var graph = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 20f),
                new LevelRoom(2, Vector3.right * 20f, Vector3.one * 20f) },
                new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            var door = new InteractableState(101, InteractableKind.Door, 1, Vector3.right * 10f, InteractableStateValue.Inactive, 11);
            level.InitializeGenerated(graph, new[] { door });
            var horror = Component<HorrorManager>(); var driver = horror.GetComponent<HorrorDriver>();
            var micro = Component<HorrorMicroEventDriver>();
            var state = (HorrorMicroEventDriverState)Field(micro, "_state").GetValue(micro);
            Set(horror, "_driver", driver); Set(driver, "_state", new HorrorDriverState());
            Set(driver, "_config", Config<HorrorDriverConfig>()); Set(driver, "_presenter", new HorrorPresenter());
            Set(driver, "_micro", micro); Set(driver, "_atmosphere", Component<HorrorAtmosphereDriver>());
            var hud = Component<HUDManager>(); Set(hud, "_initialized", true); hud.gameObject.SetActive(true);
            var route = Component<HorrorOrchestrator>();
            _cleanup.Add(() => { Invoke(route, "OnDisable"); driver.Teardown(); });
            route.Configure(run, progression, input, horror, expedition: expedition, level: level, hud: hud);
            Invoke(route, "OnEnable");
            Assert.That(state.CounterAvailable, Is.True);
            Assert.That(state.World, Is.SameAs(level.Interactables));
            Assert.That(level.OpenDoor(101), Is.True); Assert.That(state.OpenedDoors, Is.Empty);
            Assert.That(level.CloseDoor(101), Is.True);
            Assert.That(level.OpenDoor(101, openedByPlayer: true), Is.True);
            Assert.That(state.OpenedDoors.Count, Is.EqualTo(1));
            Assert.That(level.OpenDoor(101, openedByPlayer: true), Is.False);
            Publish(expedition, "FloorReleased");
            Assert.That(state.World, Is.Null); Assert.That(state.OpenedDoors, Is.Empty);
            level.InitializeGenerated(graph, new[] { door });
            Publish(expedition, "AssemblyReady", default(ProgressionGenerationRequest), Vector3.zero, Quaternion.identity);
            Assert.That(state.World, Is.SameAs(level.Interactables)); Assert.That(state.UnreachableAnchors, Is.Empty);
            bool? applied = null;
            horror.MicroEventOccurred += (kind, target, position, seconds, result) => applied = result;
            route.OnMicroEventSelected(3, 0, Vector3.zero, .7f);
            Assert.That(applied, Is.False, "A connected HUD without a rendered panel cannot report success.");
            var replacement = Component<ExpeditionSessionManager>();
            var otherLevel = Component<LevelManager>();
            route.Configure(run, progression, input, horror, expedition: replacement, level: otherLevel);
            Invoke(route, "OnEnable");
            Assert.That(Subscribers(level, "DoorOpened"), Is.Zero);
            Assert.That(Subscribers(expedition, "AssemblyReady"), Is.Zero);
            Assert.That(Subscribers(expedition, "FloorReleased"), Is.Zero);
            Assert.That(Subscribers(otherLevel, "DoorOpened"), Is.EqualTo(1));
            Assert.That(Subscribers(replacement, "AssemblyReady"), Is.EqualTo(1));
            Invoke(route, "OnDisable"); Invoke(route, "OnEnable"); Invoke(route, "OnDestroy");
            Assert.That(Subscribers(otherLevel, "DoorOpened"), Is.Zero);
            Assert.That(Subscribers(replacement, "AssemblyReady"), Is.Zero);
            Assert.That(Subscribers(replacement, "FloorReleased"), Is.Zero);
            Assert.That(state.CounterAvailable, Is.False); Assert.That(state.World, Is.Null);
        }

        [Test]
        public void MicroEventWritesOneCompleteJournalRowAndPairsAcrossHorrorRebind()
        {
            var run = Component<RunSessionManager>();
            var runState = new RunSessionBehaviorState(731);
            typeof(RunSessionBehaviorState).GetProperty("Tick").SetValue(runState, 42L);
            Set(run, "state", runState); Set(run, "controller", new RunSessionController(runState, new System.Random(731)));
            var telemetry = Component<TelemetryManager>(); var driver = telemetry.GetComponent<TelemetryDriver>();
            var stream = new MemoryStream();
            Set(driver, "_config", Config<TelemetryDriverConfig>()); driver.Initialize(_ => stream);
            Set(telemetry, "_driver", driver); Set(telemetry, "_initialized", true);
            // Prime an existing directory so this fixture never creates an application-data journal.
            driver.RecordObservation(default, TestContext.CurrentContext.WorkDirectory);
            var horror = Component<HorrorManager>(); var route = Component<TelemetryOrchestrator>();
            Set(route, "_run", run); Set(route, "_telemetry", telemetry);
            _cleanup.Add(() => { Invoke(route, "OnDisable"); driver.Teardown(); stream.Dispose(); });
            route.ConfigureHorror(horror); Invoke(route, "OnEnable");
            horror.ReportMicroEvent(3, 101, new Vector3(1.25f, 2f, -3f), .7f, true);
            string text = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(text, Does.Contain(new TelemetryCsvPresenter().MicroEvent(3, 101, new Vector3(1.25f, 2f, -3f), .7f, true, run.Tick, run.Seed)));
            Assert.That(text.Split(new[] { "\"MicroEvent\"" }, StringSplitOptions.None).Length, Is.EqualTo(2));
            var replacement = Component<HorrorManager>();
            route.ConfigureHorror(replacement); Invoke(route, "OnEnable");
            Assert.That(Subscribers(horror, "MicroEventOccurred"), Is.Zero);
            Assert.That(Subscribers(replacement, "MicroEventOccurred"), Is.EqualTo(1));
            Invoke(route, "OnDisable"); Invoke(route, "OnEnable"); Invoke(route, "OnDestroy");
            Assert.That(Subscribers(replacement, "MicroEventOccurred"), Is.Zero);
            long bytes = stream.Length;
            replacement.ReportMicroEvent(1, 9, Vector3.zero, 0f, false);
            Assert.That(stream.Length, Is.EqualTo(bytes));
        }

        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " presentation wiring test");
            owner.SetActive(false); _owned.Add(owner); return owner.AddComponent<T>();
        }
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); _owned.Add(value); return value; }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static int Subscribers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
