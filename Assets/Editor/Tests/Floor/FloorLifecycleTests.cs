// ============================================================================
// FloorLifecycleTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the real Floor Manager, Driver and generated objects in Edit Mode.
//   Contact callbacks and repeated initialization verify ownership and event wiring
//   that pure Controller tests cannot establish.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Check collection and exit contacts traverse the generated Driver stack once.
//   - Check teardown and reinitialization release prior objects and subscriptions.
//   - Prevent terminal callbacks from publishing an old run's display after restart.
// DEPENDENCIES:
//   - Domain Floor components, read-only Level/Player interfaces and Core values.
//   - UnityEngine creates temporary test objects; NUnit and reflection inspect them.
// USAGE NOTES:
//   Unity Edit Mode engine tests only; exclude this fixture from standalone managed
//   runners. The coordinator must hold the Unity lease while executing these tests.
//   Every object and config is temporary and destroyed in finally. No assets or
//   scenes are saved, and no navigation bake or physics simulation is performed.
//   Calling the actual trigger method verifies callback routing, not physical contact
//   detection; a missing navigation mesh may produce an unavailable cue.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorLifecycleTests
    {
        [Test]
        public void GeneratedContactsPublishOnceAndTeardownReleasesAllOwnedObjects()
        {
            LifecycleFixture fixture = null;
            try
            {
                fixture = new LifecycleFixture();
                var pickupEvents = 0;
                var openedEvents = 0;
                var exitEvents = 0;
                fixture.Manager.OnPickupCollected += _ => pickupEvents++;
                fixture.Manager.OnExitOpened += _ => openedEvents++;
                fixture.Manager.OnExitReached += _ => exitEvents++;
                fixture.Initialize();

                Assert.That(fixture.Manager.ReadOnlyState.IsReady, Is.True);
                Assert.That(fixture.Driver.OwnedPickupCount, Is.EqualTo(1));
                Assert.That(fixture.Driver.OwnedRoomCount, Is.EqualTo(1));
                Assert.That(fixture.Root.GetComponentsInChildren<RoomCollapseVolume>(true), Has.Length.EqualTo(1));
                var generated = fixture.Root.transform.Find("Generated Floor Runtime").gameObject;
                var cake = fixture.Root.GetComponentsInChildren<CakePickup>(true).Single();
                InvokeTrigger(cake, "OnTriggerEnter", fixture.ContactCollider);
                InvokeTrigger(cake, "OnTriggerStay", fixture.ContactCollider);

                Assert.That(pickupEvents, Is.EqualTo(1));
                Assert.That(openedEvents, Is.EqualTo(1));
                Assert.That(fixture.Manager.ReadOnlyState.CakeCount, Is.EqualTo(1));
                Assert.That(fixture.Manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                Assert.That(cake.gameObject.activeSelf, Is.False);
                Assert.That(fixture.Driver.OwnedPickupCount, Is.EqualTo(2));
                var golden = fixture.Root.GetComponentsInChildren<CakePickup>(true).Single(pickup => pickup.Kind == PickupKind.GoldenCake);
                Assert.That(golden.gameObject.activeSelf, Is.True);
                var exit = fixture.Root.GetComponentInChildren<FloorExitVolume>(true);
                InvokeTrigger(exit, "OnTriggerEnter", fixture.ContactCollider);
                InvokeTrigger(exit, "OnTriggerEnter", fixture.ContactCollider);
                Assert.That(exitEvents, Is.EqualTo(1));

                fixture.Manager.Teardown();

                Assert.That(fixture.Manager.ReadOnlyState, Is.Null);
                Assert.That(fixture.Driver.OwnedPickupCount, Is.Zero);
                Assert.That(fixture.Driver.OwnedRoomCount, Is.Zero);
                Assert.That(fixture.Root.transform.childCount, Is.Zero);
                Assert.That(generated == null, Is.True);
                Assert.That(SubscriberCount(fixture.Driver, "PickupContact", fixture.Manager), Is.Zero);
            }
            finally { fixture?.Dispose(); }
        }

        [Test]
        public void ReinitializeReplacesPriorRootAndStartsFreshWithExactlyOneSubscriptionPerRoute()
        {
            LifecycleFixture fixture = null;
            try
            {
                fixture = new LifecycleFixture();
                var pickupEvents = 0;
                var openedEvents = 0;
                fixture.Manager.OnPickupCollected += _ => pickupEvents++;
                fixture.Manager.OnExitOpened += _ => openedEvents++;
                fixture.Initialize();
                var oldState = fixture.Manager.ReadOnlyState;
                var oldRoot = fixture.Root.transform.Find("Generated Floor Runtime").gameObject;
                var oldCake = fixture.Root.GetComponentsInChildren<CakePickup>(true).Single();
                InvokeTrigger(oldCake, "OnTriggerEnter", fixture.ContactCollider);
                Assert.That(pickupEvents, Is.EqualTo(1));
                Assert.That(openedEvents, Is.EqualTo(1));

                fixture.Initialize();

                Assert.That(oldRoot == null, Is.True);
                Assert.That(oldCake == null, Is.True);
                Assert.That(oldState.IsReady, Is.False);
                Assert.That(fixture.Manager.ReadOnlyState, Is.Not.SameAs(oldState));
                Assert.That(fixture.Manager.ReadOnlyState.IsReady, Is.True);
                Assert.That(fixture.Manager.ReadOnlyState.CakeCount, Is.Zero);
                Assert.That(fixture.Manager.ReadOnlyState.GoldenCakeCount, Is.Zero);
                Assert.That(fixture.Manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
                Assert.That(fixture.Root.transform.childCount, Is.EqualTo(1));
                Assert.That(fixture.Driver.OwnedPickupCount, Is.EqualTo(1));
                Assert.That(fixture.Driver.OwnedRoomCount, Is.EqualTo(1));
                var cake = fixture.Root.GetComponentsInChildren<CakePickup>(true).Single();
                var room = fixture.Root.GetComponentInChildren<RoomCollapseVolume>(true);
                var exit = fixture.Root.GetComponentInChildren<FloorExitVolume>(true);
                Assert.That(SubscriberCount(fixture.Driver, "PickupContact", fixture.Manager), Is.EqualTo(1));

                Assert.That(SubscriberCount(fixture.Driver, "ExitContact", fixture.Manager), Is.EqualTo(1));
                Assert.That(SubscriberCount(cake, "Contact", fixture.Driver), Is.EqualTo(1));
                Assert.That(room.GetComponentsInChildren<Collider>(true).All(value => !value.enabled), Is.True);
                Assert.That(SubscriberCount(exit, "Contact", fixture.Driver), Is.EqualTo(1));

                InvokeTrigger(cake, "OnTriggerEnter", fixture.ContactCollider);

                Assert.That(pickupEvents, Is.EqualTo(2));
                Assert.That(openedEvents, Is.EqualTo(2));
                Assert.That(fixture.Manager.ReadOnlyState.CakeCount, Is.EqualTo(1));
            }
            finally { fixture?.Dispose(); }
        }

        [TestCase("pickup")]
        [TestCase("room")]
        [TestCase("exit")]
        public void SynchronousEventSubscriberCanTeardownOwnerWithoutResumingTheOldRun(string boundary)
        {
            LifecycleFixture fixture = null;
            try
            {
                fixture = new LifecycleFixture();
                fixture.Initialize();
                var teardownEvents = 0;
                Action teardown = () => { teardownEvents++; fixture.Manager.Teardown(); };
                Action operation;
                if (boundary == "pickup")
                {
                    fixture.Manager.OnPickupCollected += _ => teardown();
                    operation = () => fixture.Manager.Collect(new EntityId(1), 101, PickupKind.Cake);
                }
                else
                {
                    fixture.Manager.Collect(new EntityId(1), 101, PickupKind.Cake);
                    if (boundary == "room")
                    {
                        fixture.Manager.OnRoomPhaseChanged += fact =>
                        {
                            if (fact.Phase == RoomPhase.Closed) teardown();
                        };
                        operation = () => fixture.Manager.Tick(14f, 2);
                    }
                    else
                    {
                        fixture.Manager.OnExitReached += _ => teardown();
                        operation = () => fixture.Manager.ContactExit(new EntityId(1));
                    }
                }

                Assert.DoesNotThrow(() => operation());

                Assert.That(teardownEvents, Is.EqualTo(1));
                Assert.That(fixture.Manager.ReadOnlyState, Is.Null);
                Assert.That(fixture.Driver.OwnedPickupCount, Is.Zero);
                Assert.That(fixture.Driver.OwnedRoomCount, Is.Zero);
                Assert.That(fixture.Root.transform.childCount, Is.Zero);
            }
            finally { fixture?.Dispose(); }
        }

        [Test]
        public void NullRandomInitializationRollsBackPriorLifeAndAllowsAValidRetry()
        {
            LifecycleFixture fixture = null;
            try
            {
                fixture = new LifecycleFixture();
                fixture.Initialize();
                var priorState = fixture.Manager.ReadOnlyState;

                Assert.Throws<ArgumentNullException>(() => fixture.Initialize(null));

                Assert.That(priorState.IsReady, Is.False);
                Assert.That(fixture.Manager.ReadOnlyState, Is.Null);
                Assert.That(fixture.Driver.OwnedPickupCount, Is.Zero);
                Assert.That(fixture.Driver.OwnedRoomCount, Is.Zero);
                Assert.That(fixture.Root.transform.childCount, Is.Zero);
                fixture.Initialize();
                Assert.That(fixture.Manager.ReadOnlyState.IsReady, Is.True);
                Assert.That(fixture.Root.transform.childCount, Is.EqualTo(1));
                Assert.That(fixture.Driver.OwnedPickupCount, Is.EqualTo(1));
                Assert.That(SubscriberCount(fixture.Driver, "PickupContact", fixture.Manager), Is.EqualTo(1));
            }
            finally { fixture?.Dispose(); }
        }

        [TestCase("exit")]
        [TestCase("hand")]
        public void TerminalSubscriberReinitializeKeepsTheFinalDisplayOnTheFreshRun(string terminal)
        {
            LifecycleFixture fixture = null;
            try
            {
                fixture = new LifecycleFixture();
                var displays = new List<FloorDisplaySnapshot>();
                var restarts = 0;
                fixture.Manager.OnDisplayChanged += snapshot => displays.Add(snapshot);
                fixture.Initialize();
                fixture.Manager.Collect(new EntityId(1), 101, PickupKind.Cake);
                Assert.That(fixture.Manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Open));
                Action restart = () => { restarts++; fixture.Initialize(); };
                if (terminal == "exit")
                {
                    fixture.Manager.OnExitReached += _ => restart();

                    Assert.DoesNotThrow(() => fixture.Manager.ContactExit(new EntityId(1)));
                }
                else
                {
                    fixture.Manager.Tick(14f, 2);
                    fixture.Manager.OnCollapseHand += fact => { if (fact.Kind == CollapseHandEventKind.Escaped) restart(); };
                    Assert.DoesNotThrow(() => fixture.Manager.CancelCollapseGrab(new EntityId(1)));
                }

                Assert.That(restarts, Is.EqualTo(1));
                Assert.That(fixture.Manager.ReadOnlyState.IsReady, Is.True);
                Assert.That(fixture.Manager.ReadOnlyState.ExitState, Is.EqualTo(ExitState.Locked));
                Assert.That(fixture.Manager.ReadOnlyState.CakeCount, Is.Zero);
                Assert.That(displays, Is.Not.Empty);
                Assert.That(displays.Last().Exit, Is.EqualTo(ExitState.Locked));
                Assert.That(displays.Last().Collected, Is.Zero);
                Assert.That(displays.Last().Golden, Is.Zero);
                Assert.That(fixture.Root.transform.childCount, Is.EqualTo(1));
            }
            finally { fixture?.Dispose(); }
        }

        private static void InvokeTrigger(Component component, string callback, Collider other)
        {
            var method = component.GetType().GetMethod(callback, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, component.GetType().Name + " must expose its Unity contact callback.");
            method.Invoke(component, new object[] { other });
        }

        private static int SubscriberCount(object publisher, string eventName, object subscriber)
        {
            var field = publisher.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected an instance event backing field for " + eventName);
            var callbacks = field.GetValue(publisher) as Delegate;
            return callbacks == null ? 0 : callbacks.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, subscriber));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class LifecycleFixture : IDisposable
        {
            public GameObject Root { get; private set; }
            public FloorManager Manager { get; private set; }
            public FloorDriver Driver { get; private set; }
            public Collider ContactCollider { get; private set; }
            private GameObject _actor;
            private FloorConfig _config;
            private FloorDriverConfig _driverConfig;

            public LifecycleFixture()
            {
                try
                {
                    Assert.That(Application.isPlaying, Is.False, "Run Floor lifecycle tests in Edit Mode.");
                    _config = ScriptableObject.CreateInstance<FloorConfig>();
                    _driverConfig = ScriptableObject.CreateInstance<FloorDriverConfig>();
                    SetField(_config, "_requiredCakeCount", 1);
                    Root = new GameObject("Floor lifecycle test owner");
                    Root.SetActive(false);
                    Driver = Root.AddComponent<FloorDriver>();
                    Manager = Root.AddComponent<FloorManager>();
                    SetField(Driver, "_config", _driverConfig);
                    SetField(Manager, "_driver", Driver);
                    Root.SetActive(true);
                    _actor = new GameObject("Floor lifecycle test player handle");
                    _actor.AddComponent<FloorLifecycleEntityHandle>();
                    ContactCollider = _actor.AddComponent<BoxCollider>();
                }
                catch { Dispose(); throw; }
            }

            public void Initialize() => Initialize(new System.Random(1337));

            public void Initialize(System.Random random)
            {
                Manager.Initialize(_config, new LevelFixture(), new IReadOnlyPlayerState[] { new PlayerFixture() }, random);
            }

            public void Dispose()
            {
                try { if (Manager != null) Manager.Teardown(); }
                finally
                {
                    if (Root != null) Object.DestroyImmediate(Root);
                    if (_actor != null) Object.DestroyImmediate(_actor);
                    if (_config != null) Object.DestroyImmediate(_config);
                    if (_driverConfig != null) Object.DestroyImmediate(_driverConfig);
                }
            }
        }

        private sealed class LevelFixture : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = LevelGraphUtility.Build(
                new[] { new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 8f)) },
                new LevelEdge[0], new[] { new LevelAnchor(101, 1, CakeAnchorType.Flow, new Vector3(1f, 0f, 0f)) },
                1, new Vector3(2f, 0f, 0f));
        }

        private sealed class PlayerFixture : IReadOnlyPlayerState
        {
            public EntityId Id => new EntityId(1);
            public Vector3 Position => Vector3.zero;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public float HeadingDegrees => 0f;
            public float SprintSpeed => 8f;
            public float MaxDesignSpeed => 12f;
            public float Health => 100f;
            public float MaxHealth => 100f;
            public bool IsAlive => true;
            public bool LookBack => false;
            public MovementState MovementState => Worsen.Core.MovementState.Ground;
            public long Tick => 0;
            public IReadOnlyList<NoiseEvent> RecentNoises => Array.Empty<NoiseEvent>();
            public InventorySnapshot Inventory => default;
        }
    }

    public sealed class FloorLifecycleEntityHandle : MonoBehaviour, IEntityHandle
    {
        public EntityId Id => new EntityId(1);
    }
}

