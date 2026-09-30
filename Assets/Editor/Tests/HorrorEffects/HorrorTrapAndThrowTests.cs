// ============================================================================
// HorrorTrapAndThrowTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Session-owned hand throws and trap slow lifetimes at the Floor boundary.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Check accepted, duplicate, lethal, grace and ward outcomes; pair hazard subscriptions.
//   - Check independent slow/grab composition, expiry and floor reset.
// DEPENDENCIES:
//   Core, Player/Floor, HorrorEffects/Progression, NUnit and transient Unity objects.
// USAGE NOTES:
//   Edit Mode. Reflection supplies controllers and facts, never fabricated Player events.
//   The isolated ward fixture restores the canonical Progression reference on teardown.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.HorrorEffects
{
    public sealed class HorrorTrapAndThrowTests
    {
        private readonly List<Object> owned = new List<Object>();
        private HorrorEffectsManager effects;
        private PlayerManager player;
        private PlayerBehaviorState motion;
        private FloorManager floor;
        private FloorHandController hands;
        private FloorHandBehaviorState handState;
        private FloorConfig floorConfig;
        private FloorBehaviorState floorState;
        [SetUp]
        public void Setup()
        {
            Assert.That(PlayerRegistry.Items, Is.Empty); Assert.That(ProgressionSessionManager.Instance, Is.Null);
            player = Component<PlayerManager>(); var mover = Config<PlayerMoverDriverConfig>();
            Set(mover, "_hunterBodyLayer", "Ignore Raycast"); Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(Config<PlayerProfile>(), new EntityContext(new EntityId(1), new System.Random(7)));
            motion = (PlayerBehaviorState)player.ReadOnlyState;
            Registry("Register"); player.gameObject.SetActive(true);
            floor = Component<FloorManager>(); floorState = new FloorBehaviorState();
            handState = new FloorHandBehaviorState(); floorConfig = Config<FloorConfig>(); hands = new FloorHandController(handState, floorConfig);
            Set(floor, "_state", floorState); Set(floor, "_hands", hands); Set(floor, "_driver", floor.GetComponent<FloorDriver>());
            effects = Component<HorrorEffectsManager>();
            Set(effects, "controller", new HorrorEffectsController(new HorrorEffectsBehaviorState(), Config<HorrorEffectsConfig>()));
            effects.gameObject.SetActive(true); effects.BeginFloor(1, default); effects.ConfigureHazards(null, floor);
        }
        [TearDown]
        public void Cleanup()
        {
            if (effects != null) { effects.ClearHazards(); effects.Suspend(); }
            if (player != null) Registry("Unregister");
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, null);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
        [TestCase(false, 10f, true)] [TestCase(true, 10f, false)] [TestCase(false, 100f, false)]
        public void ThrowIsAppliedOnceOnlyForAcceptedSurvivingHit(bool grace, float damage, bool thrown)
        {
            if (grace) player.ApplyHit(1f, Vector3.back);
            var fact = new CollapseHandFact(player.Id, 1, CollapseHandEventKind.Hit, Vector3.zero, 1f, damage, 1,
                throwVelocity: Vector3.right * 8f);
            Publish(floor, "OnCollapseHand", fact); Publish(floor, "OnCollapseHand", fact);
            Assert.That(motion.PendingExternalVelocity, Is.EqualTo(thrown ? Vector3.right * 8f : Vector3.zero));
            Assert.That(motion.Health, Is.EqualTo(grace ? 99f : 100f - damage));
        }
        [Test]
        public void WaxWardCancelsRealGrabBeforeAnyDamageOrThrow()
        {
            var progression = Component<ProgressionSessionManager>(); var ps = new ProgressionSessionBehaviorState();
            var pc = new ProgressionSessionController(ps, Config<ProgressionConfig>(), new System.Random(7));
            Property(ps, "GenerationId", 1); Property(ps, "Phase", ProgressionPhase.Exploring); Property(ps, "WaxWardCharges", 1);
            Set(progression, "state", ps); Set(progression, "controller", pc);
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, progression);
            effects.ConfigureHazards(progression, floor);
            var probe = new FloorHandProbe(1, 0, Vector3.zero, 0f, true, Vector3.right);
            Step(probe, 0f, 1); Step(probe, floorConfig.HandWarningDuration, 2);
            Assert.That(ps.WaxWardCharges, Is.Zero);
            Assert.That(handState.Contacts[player.Id].Phase, Is.EqualTo(FloorHandPhase.Cooldown));
            Step(probe, floorConfig.HandEscapeGrace, 3);
            Assert.That(motion.Health, Is.EqualTo(100f)); Assert.That(motion.PendingExternalVelocity, Is.EqualTo(Vector3.zero));
        }
        [Test]
        public void SlowComposesWithGrabExpiresAndClearsAcrossHazardLifetimes()
        {
            Publish(floor, "OnCollapseHand", new CollapseHandFact(player.Id, 1, CollapseHandEventKind.Grabbed, Vector3.zero, .5f, 0f, 1));
            var trap = new FloorTrapSprungFact(11, FloorTrapKind.Slow, 1, Vector3.zero, 1, player.Id);
            Publish(floor, "OnTrapSprung", trap);
            Assert.That(motion.GrabSpeedMultiplier, Is.EqualTo(.5f)); Assert.That(motion.TrapSpeedMultiplier, Is.EqualTo(.6f));
            effects.Tick(1f, 1); Publish(floor, "OnTrapSprung", trap); effects.Tick(1f, 2);
            Assert.That(motion.TrapSpeedMultiplier, Is.EqualTo(1f), "Duplicate trap facts cannot restart the deadline.");
            Assert.That(motion.GrabSpeedMultiplier, Is.EqualTo(.5f));
            Publish(floor, "OnTrapSprung", new FloorTrapSprungFact(12, FloorTrapKind.Slow, 1, Vector3.zero, 2, player.Id));
            effects.BeginFloor(2, default); Assert.That(motion.TrapSpeedMultiplier, Is.EqualTo(1f));
            effects.ConfigureHazards(null, floor); effects.ConfigureHazards(null, floor);
            Assert.That(((Delegate)Get(floor, "OnTrapSprung")).GetInvocationList().Length, Is.EqualTo(1));
            Call(effects, "OnDisable"); Assert.That(Get(floor, "OnTrapSprung"), Is.Null); Assert.That(Get(floor, "OnCollapseHand"), Is.Null);
            effects.BeginFloor(3, default); Call(effects, "OnEnable");
            effects.ClearHazards(); Assert.That(Get(floor, "OnTrapSprung"), Is.Null);
        }
        private void Step(FloorHandProbe probe, float dt, long tick)
        { Set(floorState, "Tick", tick); if (hands.Tick(player.Id, true, probe, dt, tick, out var fact)) Publish(floor, "OnCollapseHand", fact); }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name + " trap test"); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject { var c = ScriptableObject.CreateInstance<T>(); owned.Add(c); return c; }
        private void Registry(string method) => typeof(PlayerRegistry).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
        private static FieldInfo Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(object o, string n) => Field(o, n).GetValue(o);
        private static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
        private static void Property(object o, string n, object v) => o.GetType().GetProperty(n).SetValue(o, v);
        private static void Call(object o, string n) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
        private static void Publish(object o, string n, params object[] args) => (Get(o, n) as Delegate)?.DynamicInvoke(args);
    }
}
