// ============================================================================
// RunFloorEffectWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Floor event subscriptions through real Run, Player and hearing controllers.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Verify in-flight Floor delta during grace, typed relays and active hunter fan-out.
//   - Verify environmental Director routing and replacement/disable/teardown pairing.
// DEPENDENCIES:
//   Core, Domain Player/Floor/Hunter/Director/Chase/Level, Run, NUnit and Unity.
// USAGE NOTES:
//   Edit Mode boundary tests; reflection injects controllers and publisher events.
//   No scene, asset, navigation, Unity clock setting or Library mutation occurs.
//   The fixture claims the canonical Run identity without persistent initialization,
//   and explicitly restores it and resets actor registries on teardown.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunFloorEffectWiringTests
    {
        private readonly List<Object> owned = new List<Object>();
        private readonly List<HunterManager> hunters = new List<HunterManager>();
        private RunSessionManager run;
        private FloorManager floor;
        private PlayerManager player;
        private PlayerBehaviorState motion;
        private LevelView level;
        private static readonly string[] Events = { "OnBoundaryContact", "OnHandNoise", "OnTrapNoise", "OnTrapSprung", "OnGuidanceChanged" };
        [SetUp]
        public void Setup()
        {
            ResetRegistries();
            Assert.That(PlayerRegistry.Items, Is.Empty); Assert.That(HunterRegistry.Items, Is.Empty);
            Assert.That(RunSessionManager.Instance, Is.Null);
            player = Component<PlayerManager>();
            var profile = Config<PlayerProfile>(); var mover = Config<PlayerMoverDriverConfig>();
            Set(mover, "_hunterBodyLayer", "Ignore Raycast"); Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(profile, new EntityContext(new EntityId(1), new System.Random(7)));
            motion = (PlayerBehaviorState)player.ReadOnlyState; Register(typeof(PlayerRegistry), "Register", player);
            player.gameObject.SetActive(true);
            run = Component<RunSessionManager>(); var state = new RunSessionBehaviorState(7);
            var clock = new RunSessionController(state, new System.Random(7)); clock.StartScene(SceneKey.HorrorRun);
            Set(run, "state", state); Set(run, "controller", clock); run.gameObject.SetActive(true);
            typeof(RunSessionManager).GetProperty("Instance").SetValue(null, run);
            level = new LevelView(); floor = Component<FloorManager>();
            var fs = new FloorBehaviorState(); var fc = Config<FloorConfig>(); Set(fc, "_useRoomCakeDensity", false);
            var logic = new FloorController(fs, fc, new System.Random(7)); logic.Initialize(level.Graph, new[] { motion }, 1);
            Set(fs, "CueElapsed", 0d);
            Set(floor, "_state", fs); Set(floor, "_config", fc); Set(floor, "_controller", logic);
            Set(floor, "_driver", floor.GetComponent<FloorDriver>());
            Set(floor, "_hands", new FloorHandController((FloorHandBehaviorState)Get(fs, "Hands"), fc));
            run.BindGameplay(null, floor, null);
        }
        [TearDown]
        public void Cleanup()
        {
            if (run != null) run.DetachGameplay();
            if (ReferenceEquals(RunSessionManager.Instance, run))
                typeof(RunSessionManager).GetProperty("Instance").SetValue(null, null);
            if (player != null) Register(typeof(PlayerRegistry), "Unregister", player);
            foreach (var h in hunters) Register(typeof(HunterRegistry), "Unregister", h);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear(); hunters.Clear();
            ResetRegistries();
        }
        [Test]
        public void CounterSnapshotReplaysFloorTotalsBeforeAndAfterPhysicalPickups()
        {
            var snapshots = new List<FloorDisplaySnapshot>(); run.FloorDisplayChanged += snapshots.Add;
            run.PublishFloorSnapshot();
            Assert.That(snapshots.Count, Is.EqualTo(1));
            Assert.That(snapshots[0].Collected, Is.Zero);
            Assert.That(snapshots[0].TotalCakes, Is.EqualTo(1));
            Assert.That(snapshots[0].TotalGoldenCakes, Is.EqualTo(1));
            var logic = (FloorController)Get(floor, "_controller");
            Assert.That(logic.Collect(player.Id, 11, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(logic.Collect(player.Id, 11, PickupKind.GoldenCake, 2, out _), Is.True);
            run.PublishFloorSnapshot();
            Assert.That(snapshots[1].Collected, Is.EqualTo(1)); Assert.That(snapshots[1].Golden, Is.EqualTo(1));
            Assert.That(snapshots[1].TotalCakes, Is.EqualTo(1)); Assert.That(snapshots[1].TotalGoldenCakes, Is.EqualTo(1));
            run.DetachGameplay(); run.PublishFloorSnapshot();
            Assert.That(snapshots[2].TotalCakes, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void BoundaryUsesExactlyTheFloorTickDeltaEvenDuringGrace(bool grace)
        {
            if (grace) Assert.That(player.ApplyHit(1f, Vector3.back), Is.True);
            int contacts = 0;
            floor.OnRoomDestruction += sample => {
                if (sample.RoomId == 1) { contacts++; Publish(floor, "OnBoundaryContact", player.Id, 1, Vector3.right * 4f, Vector3.zero, run.Tick); }
            };
            float floorDelta = Time.fixedDeltaTime;
            long previousTick = run.Tick;
            Call(run, "FixedUpdate");
            Assert.That(run.Tick, Is.EqualTo(previousTick + 1), "The canonical Run must execute its tick.");
            Assert.That(contacts, Is.EqualTo(1));
            Assert.That(motion.GraceActive, Is.EqualTo(grace));
            Assert.That(motion.PendingExternalVelocity.x, Is.EqualTo(4f * floorDelta).Within(.00001f));
            Publish(floor, "OnBoundaryContact", player.Id, 1, Vector3.right * 4f, Vector3.zero, run.Tick);
            Assert.That(motion.PendingExternalVelocity.x, Is.EqualTo(4f * floorDelta).Within(.00001f), "No stale delta outside the Floor tick.");
        }
        [Test]
        public void TypedRelaysAndSubscriptionsPairAcrossRebindDisableAndTeardown()
        {
            int traps = 0, guidance = 0;
            var trap = new FloorTrapSprungFact(11, FloorTrapKind.Slow, 1, Vector3.zero, 1, player.Id);
            var targets = new[] { new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.forward),
                new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.right, Vector3.right) };
            run.TrapSprung += value => { Assert.That(value, Is.EqualTo(trap)); traps++; };
            run.GuidanceChanged += value => { Assert.That(value, Is.SameAs(targets)); guidance++; };
            run.BindGameplay(null, floor, null); AssertBindings(floor, 1);
            Publish(floor, "OnTrapSprung", trap); Publish(floor, "OnGuidanceChanged", (object)targets);
            Assert.That(traps, Is.EqualTo(1)); Assert.That(guidance, Is.EqualTo(1));
            run.SetPaused(true); Publish(floor, "OnTrapSprung", trap); Publish(floor, "OnGuidanceChanged", (object)targets);
            Assert.That(traps, Is.EqualTo(1)); Assert.That(guidance, Is.EqualTo(1)); run.SetPaused(false);
            var replacement = Component<FloorManager>(); run.BindGameplay(null, replacement, null);
            AssertBindings(floor, 0); AssertBindings(replacement, 1);
            Call(run, "OnDisable"); AssertBindings(replacement, 0);
            Call(run, "OnEnable"); AssertBindings(replacement, 1);
            run.DetachGameplay(); AssertBindings(replacement, 0);
            run.BindGameplay(null, floor, null); Call(run, "OnDestroy"); AssertBindings(floor, 0);
        }
        [Test]
        public void HandAndTrapNoiseReachEachActiveBoundHunterOnceAndEnvironmentalNoiseUsesDirector()
        {
            for (int i = 0; i < 4; i++) AddHunter(-i - 1);
            hunters[2].enabled = false;
            typeof(HunterBehaviorState).GetProperty("IsActive").SetValue(hunters[3].ReadOnlyState, false);
            run.BindGameplay(null, floor, null);
            var hand = new NoiseEvent(player.Id, Vector3.zero, .8f, 0, NoiseSourceKind.Other);
            var trap = new NoiseEvent(player.Id, Vector3.zero, 1f, 0, NoiseSourceKind.Trap);
            Publish(floor, "OnHandNoise", hand); Publish(floor, "OnTrapNoise", trap);
            for (int i = 0; i < hunters.Count; i++) Assert.That(Heard(hunters[i]).Count, Is.EqualTo(i < 2 ? 2 : 0));
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(7), new ChaseBehaviorState(), floor.ReadOnlyState);
            director.SetLevelView(level); run.BindGameplay(null, floor, director);
            var environmental = new NoiseEvent(EntityId.None, Vector3.zero, 1f, 0, NoiseSourceKind.Trap);
            Publish(floor, "OnTrapNoise", environmental);
            var ds = (DirectorBehaviorState)Get(Get(director, "_controller"), "_state");
            Assert.That(ds.Noises, Is.EqualTo(new[] { environmental }));
            Assert.That(Heard(hunters[0]).Count, Is.EqualTo(2), "No simultaneous direct hearing path.");
            typeof(HunterBehaviorState).GetProperty("IsActive").SetValue(hunters[2].ReadOnlyState, false);
            int deliveries = 0; director.OnNoiseHintIssued += (_, noise) => { Assert.That(noise, Is.EqualTo(environmental)); deliveries++; };
            director.Tick(.02f, 0); director.Tick(.02f, 1);
            Assert.That(ds.Noises, Is.Empty); Assert.That(deliveries, Is.EqualTo(2));
            Assert.That(Heard(hunters[0]).Count, Is.EqualTo(3)); Assert.That(Heard(hunters[1]).Count, Is.EqualTo(3));
        }
        private static void ResetRegistries()
        {
            typeof(PlayerRegistry).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            typeof(HunterRegistry).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        private void AddHunter(int id)
        {
            var h = Component<HunterManager>(); var s = new HunterBehaviorState(); var p = Config<HunterProfile>();
            var c = new HunterController(s, p, new System.Random(7), motion, level); c.Reset(new EntityId(id), Vector3.right, Vector3.forward);
            Set(h, "_state", s); Set(h, "_controller", c); Set(h, "_profile", p); h.gameObject.SetActive(true);
            hunters.Add(h); Register(typeof(HunterRegistry), "Register", h);
        }
        private static IList Heard(HunterManager hunter) => (IList)Get(hunter.ReadOnlyState, "HeardNoises");
        private void AssertBindings(FloorManager publisher, int count)
        { foreach (string name in Events) Assert.That((Get(publisher, name) as Delegate)?.GetInvocationList().Count(d => ReferenceEquals(d.Target, run)) ?? 0, Is.EqualTo(count), name); }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name + " floor wiring test"); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var c = ScriptableObject.CreateInstance<T>(); owned.Add(c); return c; }
        private static FieldInfo Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(object o, string name) => Field(o, name).GetValue(o);
        private static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
        private static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
        private static void Publish(object o, string name, params object[] args) => (Get(o, name) as Delegate)?.DynamicInvoke(args);
        private static void Register(Type type, string name, object actor) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { actor });
        private sealed class LevelView : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = LevelGraphUtility.Build(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 20f) },
                Array.Empty<LevelEdge>(), new[] { new LevelAnchor(11, 1, CakeAnchorType.Flow, Vector3.right) }, 1, Vector3.right * 2f);
        }
    }
}
