// ============================================================================
// EnvironmentOrchestratorRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises assembled-room and floor-display routing into the real Environment driver.
//   Transient geometry facts verify the exit frame is created after its room and
//   receives continuous opening and Level light state without a generated scene or live renderer.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · CastleEnvironment routing integration.
// KEY RESPONSIBILITIES:
//   - Require graph exit identity, world position and authored Floor door rotation.
//   - Route partial opening and bail poses even while Locked, resetting on floor release.
//   - Verify initial and changed torch state reaches the real Driver through Level events.
//   - Pair subscriptions across reconfiguration and disable.
// DEPENDENCIES:
//   Core; Domain Level/Floor config; Session Run/Expedition/HorrorEffects;
//   Presentation Environment; EnvironmentOrchestrator; NUnit and UnityEngine.
// USAGE NOTES:
//   Edit Mode boundary fixture. Reflection publishes existing facts and injects a
//   transient driver config without asset lookup; rooms and exit rays use real commands.
//   Vendor rendering stays off in Edit Mode. Live rendering and scene wiring remain
//   coordinator checks; no scene, prefab, shared config or render settings are changed.
//   Router lifecycle is invoked explicitly; SetActive alone is not an Edit Mode callback.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.Environment;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Run;
using Object = UnityEngine.Object;

namespace Worsen.Tests.CastleEnvironment
{
    public sealed class EnvironmentOrchestratorRoutingTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private RunSessionManager _run;
        private ExpeditionSessionManager _expedition;
        private HorrorEffectsManager _effects;
        private LevelManager _level;
        private EnvironmentManager _environment;
        private EnvironmentDriver _driver;
        private EnvironmentOrchestrator _route;
        private EnvironmentDriverConfig _config;
        private FloorDriverConfig _floorVisuals;
        private readonly LevelRoom _room = new LevelRoom(7, new Vector3(10f, 3f, 20f), new Vector3(12f, 6f, 12f));
        private EnvironmentDriverState State => (EnvironmentDriverState)Field(_driver, "_state").GetValue(_driver);
        private EnvironmentFlameDriverState Exit => State.Flames[State.ExitLightIndex];

        [SetUp]
        public void SetUp()
        {
            _run = Component<RunSessionManager>(); _expedition = Component<ExpeditionSessionManager>();
            _effects = Component<HorrorEffectsManager>(); _level = Component<LevelManager>();
            _level.InitializeGenerated(new LevelGraph(new[] { _room }, Array.Empty<LevelEdge>(),
                Array.Empty<LevelAnchor>(), _room.Id, new Vector3(10f, 0f, 24f)));
            _environment = Component<EnvironmentManager>(); _driver = _environment.GetComponent<EnvironmentDriver>();
            _config = ScriptableObject.CreateInstance<EnvironmentDriverConfig>();
            _floorVisuals = ScriptableObject.CreateInstance<FloorDriverConfig>();
            Set(_driver, "_config", _config); Set(_environment, "_driver", _driver);
            Set(_floorVisuals, "_exitDoorYaw", 73f);
            _route = Component<EnvironmentOrchestrator>();
            _route.Configure(_run, _expedition, _effects, _environment, _level, _floorVisuals);
            Invoke(_route, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Object.DestroyImmediate(_config); Object.DestroyImmediate(_floorVisuals);
        }

        [Test]
        public void RoomsCreateExitFrameBeforeLockedThenOpenProgressAndNewFloorResetsIt()
        {
            Assert.That(State.ExitLightIndex, Is.EqualTo(-1));
            Rooms();
            Assert.That(_environment.RoomCount, Is.EqualTo(1));
            Assert.That(Exit.RoomId, Is.EqualTo(_room.Id));
            Assert.That(Exit.EffectRoot.transform.parent, Is.EqualTo(State.Rooms[_room.Id].transform));
            Assert.That(Exit.EffectRoot.transform.position, Is.EqualTo(_level.ReadOnlyState.Graph.ExitPosition));
            Assert.That(Quaternion.Angle(Exit.EffectRoot.transform.rotation,
                Quaternion.Euler(0f, _floorVisuals.ExitDoorYaw, 0f)), Is.LessThan(0.001f));
            Display(ExitState.Locked, 0f); Assert.That(Exit.OpeningProgress, Is.Zero);
            Display(ExitState.Locked, .37f); Assert.That(Exit.OpeningProgress, Is.EqualTo(.37f));
            Display(ExitState.Locked, .2f); Assert.That(Exit.OpeningProgress, Is.EqualTo(.2f), "Bail pose is not binary exit state.");
            Display(ExitState.Open, 1f); Assert.That(Exit.OpeningProgress, Is.EqualTo(1f));
            Rooms(); Assert.That(Exit.OpeningProgress, Is.Zero);
        }

        [Test]
        public void ReconfigureAndDisablePairFloorDisplayAndRoomSubscriptions()
        {
            _route.Configure(_run, _expedition, _effects, _environment, _level, _floorVisuals);
            Invoke(_route, "OnEnable");
            Assert.That(Subscribers(_expedition, "RoomsReady"), Is.EqualTo(1));
            Assert.That(Subscribers(_run, "FloorDisplayChanged"), Is.EqualTo(1));
            Assert.That(Subscribers(_level, "InteractableChanged"), Is.EqualTo(1));
            Assert.That(Subscribers(_expedition, "FloorReleased"), Is.EqualTo(1));
            Rooms(); Invoke(_route, "OnDisable");
            Assert.That(Subscribers(_expedition, "RoomsReady"), Is.Zero);
            Assert.That(Subscribers(_run, "FloorDisplayChanged"), Is.Zero);
            Assert.That(Subscribers(_level, "InteractableChanged"), Is.Zero);
            Assert.That(Subscribers(_expedition, "FloorReleased"), Is.Zero);
            Display(ExitState.Open, 1f); Assert.That(Exit.OpeningProgress, Is.Zero);
            Invoke(_route, "OnEnable");
            Display(ExitState.Open, 1f); Assert.That(Exit.OpeningProgress, Is.EqualTo(1f));
        }

        [Test]
        public void TorchFollowsInitialAndChangedLevelStateAndClearsOnRelease()
        {
            _level.gameObject.SetActive(true);
            Vector3 socket = Array.Find(EnvironmentPresenter.BuildSlots(_room.Id, _room.Bounds, null), s => s.Torch).Position;
            var light = new InteractableState(42, InteractableKind.Light, _room.Id, socket, InteractableStateValue.Inactive);
            _level.InitializeGenerated(_level.ReadOnlyState.Graph, new[] { light });
            _environment.SetObserver(socket);
            Rooms();
            var flame = State.Flames.Find(f => !f.Moon && !f.Exit && f.SocketPosition.Equals(socket));
            Assert.That(flame, Is.Not.Null); Assert.That(flame.Lit, Is.False);
            Assert.That(flame.EffectRoot.activeSelf, Is.False);
            Assert.That(_level.SetLit(42, true), Is.True);
            Assert.That(flame.Lit, Is.True); Assert.That(flame.EffectRoot.activeSelf, Is.True);
            _environment.SetLightingHooks(true, true);
            Assert.That(_level.SetLit(42, false), Is.True);
            _environment.SetRoomDestruction(_room.Id, .5f); _driver.Tick(0f);
            Assert.That(flame.EffectRoot.activeSelf, Is.False, "Wick and destruction updates cannot relight an unlit socket.");
            Publish(_expedition, "FloorReleased");
            Assert.That(State.Flames, Is.Empty); Assert.That(State.ExitLightIndex, Is.EqualTo(-1));
            Assert.That(_environment.RoomCount, Is.Zero);
        }

        private void Rooms() => Publish(_expedition, "RoomsReady", (object)new[] {
            new GeneratedRoomSample(_room.Id, _room.Bounds, false, false, Array.Empty<Vector3>()) });
        private void Display(ExitState exit, float progress) => Publish(_run, "FloorDisplayChanged", new FloorDisplaySnapshot(0, 1, 0, exit, false, Vector3.zero, progress));
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " environment routing test");
            _objects.Add(owner); owner.SetActive(false); return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static int Subscribers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
