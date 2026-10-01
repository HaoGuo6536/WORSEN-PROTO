// ============================================================================
// FogOrchestratorTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the actual publisher-to-fog routing boundary without running a floor.
//   Inactive objects keep texture allocation and persistent session startup out of
//   the fixture, following the existing catch presentation routing test pattern.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Orchestrator · Fog.
// KEY RESPONSIBILITIES:
//   - Verify monotonic phase consumption, new-floor reset and paired subscriptions.
// DEPENDENCIES:
//   - Core, Domain Floor/Level, Session Expedition, Fog and FogOrchestrator; NUnit.
// USAGE NOTES:
//   Edit Mode boundary evidence only. Reflection invokes lifecycle and publisher
//   delegates; no native rendering, scene save or actual gameplay tick is performed.
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
using Worsen.Presentation.Fog;
using Worsen.Session.Expedition;

namespace Worsen.Tests.Fog
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FogOrchestratorTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private FogDriverConfig _config;
        private FogManager _fog;
        private FogOrchestrator _route;
        private ExpeditionSessionManager _expedition;
        private FloorManager _floor;
        private LevelManager _level;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<FogDriverConfig>();
            _fog = Component<FogManager>(); _fog.Initialize(_config);
            _expedition = Component<ExpeditionSessionManager>(); _floor = Component<FloorManager>(); _level = Component<LevelManager>();
            _route = Component<FogOrchestrator>(); _route.Configure(_expedition, _level, _floor, _fog);
            Invoke(_route, "OnEnable");
        }
        [TearDown] public void TearDown()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            for (int i = _objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); UnityEngine.Object.DestroyImmediate(_config);
        }
        [Test] public void RoutesProgressAndNewFloorResetsReusedRoomIds()
        {
            Assemble(1);
            Publish(_floor, "OnRoomDestruction", new RoomDestructionSample(1, RoomPhase.Encroaching, .5f));
            Assert.That(_fog.RoomCount, Is.EqualTo(1)); Assert.That(_fog.RoomProgress(1), Is.EqualTo(.56f).Within(.0001f));
            Publish(_floor, "OnRoomDestruction", new RoomDestructionSample(1, RoomPhase.Tearing, .5f));
            Assert.That(_fog.RoomProgress(1), Is.EqualTo(.56f).Within(.0001f), "Stale phase facts cannot undo consumption.");
            Publish(_floor, "OnRoomDestruction", new RoomDestructionSample(1, RoomPhase.Closed, 0f));
            Assert.That(_fog.RoomProgress(1), Is.EqualTo(1f));
            Assemble(1); Assert.That(_fog.RoomProgress(1), Is.Zero);
            Assemble(5);
            Publish(_floor, "OnRoomDestruction", new RoomDestructionSample(1, default, 1f));
            Assert.That(_fog.RoomCount, Is.EqualTo(1)); Assert.That(_fog.RoomProgress(5), Is.Zero);
        }
        [Test] public void ReconfigureDoesNotDuplicateSubscriptionsAndDisableUnpairsThem()
        {
            Invoke(_route, "OnEnable");
            Assert.That(Handlers(_floor, "OnRoomDestruction"), Is.EqualTo(1));
            Assert.That(Handlers(_expedition, "RoomsReady"), Is.EqualTo(1));
            _route.Configure(_expedition, _level, _floor, _fog); Invoke(_route, "OnEnable");
            Assert.That(Handlers(_floor, "OnRoomDestruction"), Is.EqualTo(1));
            Assemble(1); Invoke(_route, "OnDisable");
            Assert.That(Handlers(_floor, "OnRoomDestruction"), Is.Zero);
            Assert.That(Handlers(_expedition, "RoomsReady"), Is.Zero);
            Assert.That(_fog.RoomCount, Is.Zero);
            Publish(_floor, "OnRoomDestruction", new RoomDestructionSample(1, default, 1f));
            Assert.That(_fog.RoomCount, Is.Zero);
        }
        private void Assemble(int id)
        {
            var room = new LevelRoom(id, new Vector3(0, 2, 0), new Vector3(12, 4, 12));
            _level.InitializeGenerated(new LevelGraph(new[] { room }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), id, Vector3.zero));
            Publish(_expedition, "RoomsReady", (object)new[] { new GeneratedRoomSample(id, room.Bounds, false, false, Array.Empty<Vector3>()) });
        }
        private T Component<T>() where T : Component
        { var item = new GameObject(typeof(T).Name + " fog test"); item.SetActive(false); _objects.Add(item); return item.AddComponent<T>(); }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static int Handlers(object target, string name) => (Field(target, name).GetValue(target) as Delegate)?.GetInvocationList().Length ?? 0;
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Publish(object target, string name, params object[] args) => (Field(target, name).GetValue(target) as Delegate)?.DynamicInvoke(args);
    }
}
