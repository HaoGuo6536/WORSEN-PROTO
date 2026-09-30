// ============================================================================
// EnvironmentOrchestratorRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises assembled-room and floor-display routing into the real Environment driver.
//   Transient geometry facts verify the exit frame is created after its room and
//   receives locked/open progress without requiring a generated scene or live renderer.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · CastleEnvironment routing integration.
// KEY RESPONSIBILITIES:
//   - Require graph exit identity, world position and authored Floor door rotation.
//   - Route Locked to zero and Open to one, resetting progress on floor replacement.
//   - Pair subscriptions across reconfiguration and disable.
// DEPENDENCIES:
//   Core; Domain Level/Floor config; Session Run/Expedition/HorrorEffects;
//   Presentation Environment; EnvironmentOrchestrator; NUnit and UnityEngine.
// USAGE NOTES:
//   Edit Mode boundary fixture. Reflection publishes existing facts and injects a
//   transient driver config without asset lookup; rooms and exit rays use real commands.
//   Vendor rendering stays off in Edit Mode. Continuous opening and live wiring remain
//   coordinator checks; no scene, prefab, shared config or render settings are changed.
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
            _route.gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_route != null) _route.gameObject.SetActive(false);
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
            Display(ExitState.Locked); Assert.That(Exit.OpeningProgress, Is.Zero);
            Display(ExitState.Open); Assert.That(Exit.OpeningProgress, Is.EqualTo(1f));
            Rooms(); Assert.That(Exit.OpeningProgress, Is.Zero);
        }

        [Test]
        public void ReconfigureAndDisablePairFloorDisplayAndRoomSubscriptions()
        {
            _route.Configure(_run, _expedition, _effects, _environment, _level, _floorVisuals);
            Assert.That(Subscribers(_expedition, "RoomsReady"), Is.EqualTo(1));
            Assert.That(Subscribers(_run, "FloorDisplayChanged"), Is.EqualTo(1));
            Rooms(); _route.gameObject.SetActive(false);
            Assert.That(Subscribers(_expedition, "RoomsReady"), Is.Zero);
            Assert.That(Subscribers(_run, "FloorDisplayChanged"), Is.Zero);
            Display(ExitState.Open); Assert.That(Exit.OpeningProgress, Is.Zero);
            _route.gameObject.SetActive(true);
            Display(ExitState.Open); Assert.That(Exit.OpeningProgress, Is.EqualTo(1f));
        }

        private void Rooms() => Publish(_expedition, "RoomsReady", (object)new[] {
            new GeneratedRoomSample(_room.Id, _room.Bounds, false, false, Array.Empty<Vector3>()) });
        private void Display(ExitState exit) => Publish(_run, "FloorDisplayChanged", new FloorDisplaySnapshot(0, 1, 0, exit, false, Vector3.zero));
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " environment routing test");
            _objects.Add(owner); owner.SetActive(false); return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static int Subscribers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
