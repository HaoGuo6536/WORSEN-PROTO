// ============================================================================
// RunHunterRosterWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises promoted hunter facts through real Session, Player and Director routes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Check once-only relays, pause, lifecycle pairing and silent-to-hunters Loud Keys.
//   - Check world views, Echo door closure and silent mutation restoration on late hunters.
// DEPENDENCIES:
//   - Core, Domain managers and pure controllers, Expedition/Run/Progression, NUnit.
// USAGE NOTES:
//   Coordinator-only Edit Mode tests; reflection injects state and publisher facts.
//   No persistent scene, asset, navigation, singleton initialization or clock mutation.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunHunterRosterWiringTests
    {
        private readonly List<Object> owned = new List<Object>();
        private readonly List<HunterManager> hunters = new List<HunterManager>();
        private RunSessionManager run;
        private PlayerManager player;
        private PlayerBehaviorState motion;
        private LevelManager level;
        private FloorManager floor;
        private ExpeditionSessionManager expedition;
        private ExpeditionSessionController assembly;
        private ProgressionSessionManager progression;
        [SetUp] public void Setup()
        {
            Assert.That(PlayerRegistry.Items, Is.Empty); Assert.That(HunterRegistry.Items, Is.Empty);
            player = Component<PlayerManager>(); var mover = Config<PlayerMoverDriverConfig>();
            Set(mover, "_hunterBodyLayer", "Ignore Raycast"); Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(Config<PlayerProfile>(), new EntityContext(new EntityId(1), new System.Random(7)));
            motion = (PlayerBehaviorState)player.ReadOnlyState; Register(typeof(PlayerRegistry), "Register", player);
            run = Component<RunSessionManager>(); var state = new RunSessionBehaviorState(7);
            var clock = new RunSessionController(state, new System.Random(7)); clock.StartScene(SceneKey.HorrorRun);
            Set(run, "state", state); Set(run, "controller", clock); run.gameObject.SetActive(true);
            level = Component<LevelManager>();
            level.InitializeGenerated(LevelGraphUtility.Build(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 20f) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero),
                new[] { new InteractableState(101, InteractableKind.Door, 1, Vector3.right, InteractableStateValue.Open) });
            floor = Component<FloorManager>(); Set(floor, "_state", new FloorBehaviorState());
            progression = Component<ProgressionSessionManager>();
            var pc = new ProgressionSessionController(new ProgressionSessionBehaviorState(), Config<ProgressionConfig>(), new System.Random(7));
            pc.StartRun(7); Set(progression, "controller", pc);
            expedition = Component<ExpeditionSessionManager>(); var es = new ExpeditionSessionBehaviorState();
            assembly = new ExpeditionSessionController(es); assembly.Bind(SceneKey.HorrorRun);
            assembly.Queue(new ProgressionGenerationRequest(1, 7, 1, false, new ProgressionEffects(1, 1, 1, 1, 100, 100, 0)));
            assembly.Begin(1);
            Set(expedition, "_state", es); Set(expedition, "_controller", assembly); Set(expedition, "_level", level);
            Set(expedition, "_run", run); Set(expedition, "_floor", floor); Set(expedition, "_progression", progression);
            Set(expedition, "_worldBound", true); Call(expedition, "OnEnable");
            run.BindGameplay(null, null, null);
        }
        [TearDown] public void Cleanup()
        {
            if (run != null) run.DetachGameplay();
            if (expedition != null) { Set(expedition, "_worldBound", false); expedition.ClearScene(); }
            Register(typeof(PlayerRegistry), "Unregister", player);
            foreach (var hunter in hunters) Register(typeof(HunterRegistry), "Unregister", hunter);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear(); hunters.Clear();
        }
        [Test] public void AllCoreRelaysAreOnceOnlyAndPairForLateBindRebindDisableAndDetach()
        {
            var h = Hunter(-1); var ticking = h.gameObject.AddComponent<TickingManager>(); Set(h, "_ticking", ticking);
            run.BindAdditionalHunter(h); run.BindAdditionalHunter(h); run.BindGameplay(null, null, null);
            int facts = 0;
            run.HunterArchetypePublished += _ => facts++; run.HunterHabitPublished += _ => facts++;
            run.HunterMutationPublished += _ => facts++; run.WeaverFactPublished += _ => facts++;
            run.WebHitPublished += _ => facts++; run.TickingSoundPublished += _ => facts++;
            run.TickingGuidancePublished += _ => facts++; run.TickingNoisePublished += _ => facts++;
            void Emit()
            {
                Publish(h, "OnArchetypeFact", new HunterArchetypeFact(h.Id, HunterArchetypeFactKind.ReplayedFootstep, Vector3.zero, 1));
                Publish(h, "OnHabit", new HunterHabitFact(h.Id, HunterHabitKind.TurnToFace, Vector3.zero, 1));
                Publish(h, "OnMutation", new HunterMutationFact(h.Id, "echo", "tell", 1));
                Publish(h, "OnWeaverFact", new WeaverFact(h.Id, WeaverFactKind.WebGlow, 1, Vector3.zero));
                Publish(h, "OnWebHit", new WebHitFact(h.Id, player.Id, 1, 1, .5f, 3f, .5f));
                Publish(ticking, "OnSound", new TickingSoundFact(TickingSound.Tick, Vector3.one, .5f, 1, h.Id));
                Publish(ticking, "OnGuidance", new TickingGuidanceFact(default, false, 1));
                Publish(ticking, "OnNoise", new TickingNoiseFact(h.Id, new NoiseEvent(player.Id, Vector3.zero, 1f, 1)));
            }
            Emit(); Assert.That(facts, Is.EqualTo(8)); Assert.That(motion.WebSpeedMultiplier, Is.EqualTo(.75f));
            run.SetPaused(true); Emit(); Assert.That(facts, Is.EqualTo(8)); run.SetPaused(false);
            run.gameObject.SetActive(false); Emit(); Assert.That(facts, Is.EqualTo(8));
            run.gameObject.SetActive(true); Emit(); Assert.That(facts, Is.EqualTo(16));
            run.DetachGameplay(); Emit(); Assert.That(facts, Is.EqualTo(16));
            foreach (string name in new[] { "OnArchetypeFact", "OnHabit", "OnMutation", "OnWebHit", "OnWeaverFact" })
                Assert.That((Get(h, name) as Delegate)?.GetInvocationList().Length ?? 0, Is.Zero, name);
            foreach (string name in new[] { "OnSound", "OnGuidance", "OnNoise" })
                Assert.That((Get(ticking, name) as Delegate)?.GetInvocationList().Length ?? 0, Is.Zero, name);
        }
        [TestCase(false)] [TestCase(true)] public void LoudKeysNeverReachHuntersWithOrWithoutDirector(bool withDirector)
        {
            var a = Hunter(-1); var b = Hunter(-2); var ticking = a.gameObject.AddComponent<TickingManager>(); Set(a, "_ticking", ticking);
            var director = withDirector ? Component<DirectorManager>() : null; int deliveries = 0;
            if (director != null) director.OnNoiseHintIssued += (_, __) => deliveries++;
            run.BindGameplay(null, null, director);
            var noise = new NoiseEvent(player.Id, Vector3.one * 10000f, 1f, 5);
            a.HearNoise(noise); Assert.That(((IList)Get(a.ReadOnlyState, "HeardNoises")).Count, Is.Zero);
            Publish(ticking, "OnNoise", new TickingNoiseFact(a.Id, noise));
            Publish(ticking, "OnNoise", new TickingNoiseFact(a.Id, noise));
            foreach (var h in new[] { a, b }) Assert.That(((IList)Get(h.ReadOnlyState, "HeardNoises")).Count, Is.Zero);
            Assert.That(deliveries, Is.Zero);
        }
        [Test] public void EveryHunterReceivesViewsEchoClosesDoorAndRespawnRestoresMutationSilently()
        {
            var first = Hunter(-1); run.BindAdditionalHunter(first); Call(expedition, "BindHunterWorld", first);
            AssertViews(first);
            Publish(first, "OnArchetypeFact", new HunterArchetypeFact(first.Id, HunterArchetypeFactKind.ReplayedDoorPassage, Vector3.right, 1, objectId: 101));
            Assert.That(level.Interactables.TryGet(101, out var door), Is.True); Assert.That(door.Value, Is.EqualTo(InteractableStateValue.Inactive));
            int announcements = 0; run.HunterMutationPublished += _ => announcements++;
            var mutation = new HunterMutation(HunterTunable.Acceleration, 17f, "tell");
            Assert.That(first.ApplyMutation(mutation), Is.True);
            var late = Hunter(-2); run.BindAdditionalHunter(late); Call(expedition, "BindHunterWorld", late); Call(expedition, "RestoreMutations", late);
            AssertViews(late); Assert.That(announcements, Is.EqualTo(1)); Assert.That(late.ApplyMutation(mutation), Is.False);
            Assert.That(((IDictionary)Get(late.ReadOnlyState, "Mutations"))[HunterTunable.Acceleration], Is.EqualTo(17f));
        }
        private void AssertViews(HunterManager hunter)
        {
            Assert.That(Get(hunter.ReadOnlyState, "WorldView"), Is.Not.Null);
            object c = Get(hunter, "_controller"); Assert.That(Get(c, "_closedDoors"), Is.SameAs(level.ClosedDoors));
            Assert.That(Get(c, "_interactables"), Is.SameAs(level.Interactables)); Assert.That(Get(c, "_floor"), Is.SameAs(floor.ReadOnlyState));
            Assert.That(Get(c, "_effects"), Is.EqualTo(progression.EffectsSnapshot.ActiveEffects));
        }
        private HunterManager Hunter(int id)
        {
            var h = Component<HunterManager>(); var s = new HunterBehaviorState(); var p = Config<HunterProfile>(); Set(p, "_archetypeKey", "echo");
            var c = new HunterController(s, p, new System.Random(7), motion, level.ReadOnlyState); c.Reset(new EntityId(id), Vector3.right, Vector3.forward);
            Set(h, "_state", s); Set(h, "_controller", c); Set(h, "_profile", p); h.gameObject.SetActive(true);
            hunters.Add(h); Register(typeof(HunterRegistry), "Register", h); assembly.RecordHunter(h.Id); return h;
        }
        [Test] public void ExpansionFactsPairAcrossPauseRebindDisableAndDetach()
        {
            var hunter = Hunter(-1); run.BindAdditionalHunter(hunter);
            AssertRelay<RamFact>(hunter, "OnRamFact", "RamFactPublished");
            AssertRelay<SkipFact>(hunter, "OnSkipFact", "SkipFactPublished");
            AssertRelay<MimicFact>(hunter, "OnMimicFact", "MimicFactPublished");
            AssertRelay<BlinderHitFact>(hunter, "OnBlinderHit", "BlinderHitPublished");
            AssertRelay<BlinderThrowFact>(hunter, "OnBlinderThrow", "BlinderThrowPublished");
            AssertRelay<BlinderSoundFact>(hunter, "OnBlinderSound", "BlinderSoundPublished");
            AssertRelay<BlinderTrapPolicyFact>(hunter, "OnBlinderTrapPolicy", "BlinderTrapPolicyPublished");
            AssertRelay<HeraldScreamFact>(hunter, "OnHeraldScream", "HeraldScreamPublished");
            AssertRelay<HeraldBreathFact>(hunter, "OnHeraldBreath", "HeraldBreathPublished");
            AssertRelay<HeraldDeafenFact>(hunter, "OnHeraldDeafen", "HeraldDeafenPublished");
            AssertRelay<HunterDoorBreakFact>(hunter, "OnDoorBreakCompleted", "HunterDoorBreakPublished");
            AssertRelay<MannequinFact>(hunter, "OnMannequinFact", "MannequinFactPublished");
            AssertRelay<StareFact>(hunter, "OnStareFact", "StareFactPublished");
        }
        private void AssertRelay<T>(HunterManager hunter, string publisher, string relay) where T : struct
        {
            int count = 0; Action<T> observer = _ => count++;
            var output = typeof(RunSessionManager).GetEvent(relay); output.AddEventHandler(run, observer);
            run.BindGameplay(null, null, null); run.BindAdditionalHunter(hunter);
            Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(1), relay);
            run.SetPaused(true); Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(1), relay);
            run.SetPaused(false); run.gameObject.SetActive(false);
            Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(1), relay);
            run.gameObject.SetActive(true); Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(2), relay);
            run.DetachGameplay(); Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(2), relay);
            Assert.That((Get(hunter, publisher) as Delegate)?.GetInvocationList().Length ?? 0, Is.Zero, publisher);
            output.RemoveEventHandler(run, observer);
        }
        [TestCase(NoiseOrigin.World)] [TestCase(NoiseOrigin.Pacification)]
        [TestCase(NoiseOrigin.Presentation)] [TestCase(NoiseOrigin.FalsePositive)]
        public void DisallowedNoiseNeverEntersDirector(NoiseOrigin origin)
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            run.BindGameplay(null, null, director);
            var noise = new NoiseEvent(player.Id, Vector3.zero, 1000f, 1, NoiseSourceKind.Firecracker, origin);
            run.ForwardGameplayNoise(noise); Call(run, "HandleTrapNoise", noise); Call(run, "HandlePickupNoise", noise);
            Assert.That(((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises, Is.Empty);
        }
        [Test] public void UnspecifiedNoiseRequiresTheCommittedPlayerTrapBoundary()
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            run.BindGameplay(null, null, director);
            var noise = new NoiseEvent(player.Id, Vector3.zero, 1f, 1, NoiseSourceKind.Trap);
            var noises = ((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises;
            run.ForwardGameplayNoise(noise); Call(run, "HandlePickupNoise", noise);
            Call(run, "HandleTrapNoise", new NoiseEvent(EntityId.None, Vector3.zero, 1f, 1, NoiseSourceKind.Trap));
            Assert.That(noises, Is.Empty);
            NoiseEvent published = default; run.WorldNoisePublished += value => published = value;
            Call(run, "HandleTrapNoise", noise);
            Assert.That(noises.Count, Is.EqualTo(1));
            Assert.That(published.Origin, Is.EqualTo(NoiseOrigin.PlayerTriggeredCakeTrap));
        }
        [TestCase(NoiseOrigin.PlayerMovement)] [TestCase(NoiseOrigin.Firecracker)] [TestCase(NoiseOrigin.PlayerTriggeredCakeTrap)]
        public void AllowedNoiseEntersDirectorOnce(NoiseOrigin origin)
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            run.BindGameplay(null, null, director);
            run.ForwardGameplayNoise(new NoiseEvent(player.Id, Vector3.zero, 1f, 1, origin: origin));
            Assert.That(((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises.Count, Is.EqualTo(1));
        }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name + " roster wiring test"); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private static FieldInfo Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(object o, string name) => Field(o, name).GetValue(o);
        private static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
        private static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
        private static void Publish(object o, string name, params object[] args) => (Get(o, name) as Delegate)?.DynamicInvoke(args);
        private static void Register(Type type, string name, object actor) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { actor });
    }
}
