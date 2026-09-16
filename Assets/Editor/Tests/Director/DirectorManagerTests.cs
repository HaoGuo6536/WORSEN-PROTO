// ============================================================================
// DirectorManagerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Director lifecycle boundaries using temporary real Unity components.
//   A synchronous listener may replace or tear down the scene service, so the old
//   tick must stop publishing and failed initialization must preserve its old wiring.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Director.
// KEY RESPONSIBILITIES:
//   - Exercise atomic initialization and event-triggered teardown/reinitialization.
//   - Restore only the temporary Player registry entry created by each test.
// DEPENDENCIES:
//   - Core facts and Director; Player registry fixture; typed Chase/Floor views.
//   - UnityEngine components, NUnit, and reflection for isolated fixture injection.
// USAGE NOTES:
//   Actual Edit Mode lifecycle tests, excluded from standalone managed evidence.
//   No scene/assets are saved and no Player movement or physics tick is run.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using Worsen.Domain.Player;
using Worsen.Domain.Chase;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Director
{
    public sealed class DirectorManagerTests
    {
        private static readonly EntityId PlayerId = new EntityId(2147483501);

        [Test]
        public void FailedReinitializeKeepsPriorControllerAndTypedViewsTogether()
        {
            using (var fixture = new ManagerFixture())
            {
                var oldController = Get(fixture.Manager, "_controller");
                var oldChase = Get(fixture.Manager, "_chase");
                var oldFloor = Get(fixture.Manager, "_floor");
                var invalid = ScriptableObject.CreateInstance<DirectorConfig>();
                try
                {
                    Set(invalid, "_hintConfidence", float.NaN);
                    Assert.Throws<ArgumentException>(() => fixture.Manager.Initialize(invalid, new System.Random(8), new ChaseFixture(), new FloorFixture()));
                    Assert.That(Get(fixture.Manager, "_controller"), Is.SameAs(oldController));
                    Assert.That(Get(fixture.Manager, "_chase"), Is.SameAs(oldChase));
                    Assert.That(Get(fixture.Manager, "_floor"), Is.SameAs(oldFloor));
                }
                finally { Object.DestroyImmediate(invalid); }
            }
        }

        [Test]
        public void IntrusionListenerTeardownSuppressesRemainingPressureFromOldTick()
        {
            using (var fixture = new ManagerFixture())
            {
                int intrusions = 0, pressure = 0;
                fixture.Manager.OnIntrusion += sample =>
                {
                    if (sample.Player != PlayerId) return;
                    intrusions++;
                    fixture.Manager.Teardown();
                };
                fixture.Manager.OnPressureSampled += sample => { if (sample.Player == PlayerId) pressure++; };
                Assert.DoesNotThrow(() => fixture.Manager.Tick(3.5f, 0));
                Assert.That(intrusions, Is.EqualTo(1));
                Assert.That(pressure, Is.Zero);
                Assert.That(Get(fixture.Manager, "_controller"), Is.Null);
            }
        }

        [Test]
        public void PressureListenerReinitializeStopsOldBatchAndNewClockStartsFresh()
        {
            using (var fixture = new ManagerFixture())
            {
                int pressure = 0;
                fixture.Manager.OnPressureSampled += sample =>
                {
                    if (sample.Player != PlayerId) return;
                    pressure++;
                    if (pressure == 1) fixture.Initialize();
                };
                fixture.Manager.Tick(1f, 0);
                Assert.That(pressure, Is.EqualTo(1));
                Assert.DoesNotThrow(() => fixture.Manager.Tick(0.5f, 0));
                Assert.That(pressure, Is.EqualTo(2));
            }
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Register(PlayerManager player, string method) => typeof(PlayerRegistry).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });

        private sealed class ManagerFixture : IDisposable
        {
            private GameObject _root;
            private GameObject _playerObject;
            private PlayerManager _player;
            private DirectorConfig _config;
            public DirectorManager Manager { get; private set; }
            public ManagerFixture()
            {
                try
                {
                    Assert.That(Application.isPlaying, Is.False);
                    _config = ScriptableObject.CreateInstance<DirectorConfig>();
                    _root = new GameObject("Director lifecycle test");
                    Manager = _root.AddComponent<DirectorManager>();
                    _playerObject = new GameObject("Director lifecycle test player");
                    _player = _playerObject.AddComponent<PlayerManager>();
                    Set(_player, "_state", new PlayerBehaviorState { Id = PlayerId, Position = Vector3.zero, Velocity = Vector3.zero, Health = 100f });
                    Register(_player, "Register");
                    Initialize();
                }
                catch { Dispose(); throw; }
            }
            public void Initialize() => Manager.Initialize(_config, new System.Random(77), new ChaseFixture(), new FloorFixture());
            public void Dispose()
            {
                if (_player != null) Register(_player, "Unregister");
                if (Manager != null) Manager.Teardown();
                if (_root != null) Object.DestroyImmediate(_root);
                if (_playerObject != null) Object.DestroyImmediate(_playerObject);
                if (_config != null) Object.DestroyImmediate(_config);
            }
        }

        private sealed class ChaseFixture : IReadOnlyChaseState
        {
            public ChasePhase Phase => default;
            public int ChaseId => 0;
            public EntityId PlayerId => DirectorManagerTests.PlayerId;
            public EntityId HunterId => EntityId.None;
            public long StartTick => 0;
            public float Closeness => 0f;
            public bool HasActiveChase => false;
        }

        private sealed class FloorFixture : IReadOnlyFloorState
        {
            public bool IsReady => true;
            public int CakeCount => 0;
            public int RequiredCakeCount => 10;
            public int GoldenCakeCount => 0;
            public ExitState ExitState => Worsen.Core.ExitState.Locked;
            public IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; } = new Dictionary<int, RoomPhase>();
            public IReadOnlyList<LevelAnchor> ActiveCakeAnchors => Array.Empty<LevelAnchor>();
        }
    }
}
