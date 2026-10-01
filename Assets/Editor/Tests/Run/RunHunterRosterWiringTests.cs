// ============================================================================
// RunHunterRosterWiringTests.cs
// ============================================================================
// PURPOSE:
//   Exercises promoted hunter facts through real Session, Player and Director routes.
//   Valid two-room door topology keeps world validation active in every wiring case.
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
//   Pair non-ExecuteAlways Run lifecycle callbacks explicitly in Edit Mode; Level
//   remains active so its public mutation gate is exercised rather than bypassed.
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
            level.gameObject.SetActive(true);
            level.InitializeGenerated(LevelGraphUtility.Build(new[] {
                new LevelRoom(1, Vector3.zero, Vector3.one * 20f),
                new LevelRoom(2, Vector3.right * 20f, Vector3.one * 20f) },
                new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero),
                new[] { new InteractableState(101, InteractableKind.Door, 1, Vector3.right * 10f, InteractableStateValue.Open, 11) });
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
            var h = Hunter(-1); var ticking = h.gameObject.AddComponent<TickingManager>(); Set(h, "_module", ticking);
            run.BindAdditionalHunter(h); run.BindAdditionalHunter(h); run.BindGameplay(null, null, null);
            int facts = 0;
            run.HunterArchetypePublished += _ => facts++; run.HunterFacts.HunterHabitPublished += _ => facts++;
            run.HunterMutationPublished += _ => facts++; run.HunterFacts.WeaverFactPublished += _ => facts++;
            run.HunterFacts.WebHitPublished += _ => facts++; run.HunterFacts.TickingSoundPublished += _ => facts++;
            run.HunterFacts.TickingGuidancePublished += _ => facts++; run.HunterFacts.TickingNoisePublished += _ => facts++;
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
            run.gameObject.SetActive(false); Call(run, "OnDisable"); Emit(); Assert.That(facts, Is.EqualTo(8));
            run.gameObject.SetActive(true); Call(run, "OnEnable"); Emit(); Assert.That(facts, Is.EqualTo(16));
            run.DetachGameplay(); Emit(); Assert.That(facts, Is.EqualTo(16));
            foreach (string name in new[] { "OnArchetypeFact", "OnHabit", "OnMutation", "OnWebHit", "OnWeaverFact" })
                Assert.That((Get(h, name) as Delegate)?.GetInvocationList().Length ?? 0, Is.Zero, name);
            foreach (string name in new[] { "OnSound", "OnGuidance", "OnNoise" })
                Assert.That((Get(ticking, name) as Delegate)?.GetInvocationList().Length ?? 0, Is.Zero, name);
        }
        [TestCase(false)] [TestCase(true)] public void LoudKeysNeverReachHuntersWithOrWithoutDirector(bool withDirector)
        {
            var a = Hunter(-1); var b = Hunter(-2); var ticking = a.gameObject.AddComponent<TickingManager>(); Set(a, "_module", ticking);
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
        [Test]
        public void PurgatoryMutationReachesEveryMatchingInstanceWithOneAnnouncementAndSilentRestoration()
        {
            var a = Hunter(-1); var b = Hunter(-2);
            run.BindAdditionalHunter(a); run.BindAdditionalHunter(b);
            int announcements = 0; run.HunterMutationPublished += _ => announcements++;
            var mutation = new HunterMutation(HunterTunable.Acceleration, 17f, "tell");
            Call(expedition, "ApplyTypeMutation", a, mutation);
            Assert.That(a.HasMutation(mutation) && b.HasMutation(mutation), Is.True);
            Assert.That(announcements, Is.EqualTo(1));
            var late = Hunter(-3); run.BindAdditionalHunter(late); Call(expedition, "RestoreMutations", late);
            Assert.That(late.HasMutation(mutation), Is.True); Assert.That(announcements, Is.EqualTo(1));
        }

        [Test]
        public void ChaseDeliveryArmsBodyReboundBeforeDamageAdmission()
        {
            var h = Hunter(-1); run.BindAdditionalHunter(h);
            Set(player, "_controller", new PlayerController(motion, Config<PlayerProfile>(), new System.Random(1), Config<PlayerEffectConfig>()));
            motion.AppliedEffects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("second-bounce"), EffectKind.Upgrade, 1) });
            motion.MovementState = MovementState.Air; motion.Velocity = Vector3.forward * 8f; motion.ReboundJumpRemaining = .1f;
            Call(run, "HandleChaseStarted", new ChaseFact(1, player.Id, h.Id, 1, ChasePhase.Confirmed));
            int rebounds = 0; player.OnTraversal += fact => { if (fact.Kind == TraversalKind.Rebound) rebounds++; };
            Publish(h, "OnLungeHit", new HunterHit(h.Id, player.Id, 30, 1, h.ReadOnlyState.Position, contactNormal: Vector3.back));
            Assert.That(rebounds, Is.EqualTo(1)); Assert.That(motion.Health, Is.EqualTo(100f));
            Assert.That((IList)Get(run, "pendingHits"), Is.Empty);
            Assert.That(motion.Velocity.z, Is.LessThan(0));
        }

        [Test]
        public void HeartbeatSubscriptionIsExplicitAndPairedAcrossRebindAndDetach()
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            run.BindGameplay(null, null, director); run.BindGameplay(null, null, director);
            var noises = ((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises;
            Publish(player, "OnHeartbeat", new NoiseEvent(player.Id, Vector3.zero, 1f, 1, NoiseSourceKind.Other, NoiseOrigin.PlayerMovement));
            Assert.That(noises, Is.Empty);
            Publish(player, "OnHeartbeat", new NoiseEvent(player.Id, Vector3.zero, 1f, 1, NoiseSourceKind.Heartbeat, NoiseOrigin.PlayerMovement));
            Assert.That(noises.Count, Is.EqualTo(1));
            Assert.That(((Delegate)Get(player, "OnHeartbeat")).GetInvocationList().Length, Is.EqualTo(1));
            run.DetachGameplay(); Assert.That(Get(player, "OnHeartbeat"), Is.Null);
        }

        [Test]
        public void AfterglowGivesNoMannequinSafetyWindowAndPublishesNone()
        {
            // Owner rule 2026-09-30: the Mannequin moves whenever unseen, lit or dark, so an
            // extinguished light opens no refuge for any bound Mannequin, even with Afterglow.
            var config = Config<Worsen.Domain.Hunter.Archetypes.Mannequin.MannequinConfig>();
            var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("afterglow"), EffectKind.Upgrade, 1) });
            foreach (int id in new[] { -1, -2 })
            {
                var h = Hunter(id);
                var rules = new Worsen.Domain.Hunter.Archetypes.Mannequin.MannequinController(config, new System.Random(1));
                rules.SetEffects(effects);
                // The archetype now reaches HunterManager as a registered module (PLAN-027 hunter plug-ins).
                var module = h.gameObject.AddComponent<Worsen.Domain.Hunter.Archetypes.Mannequin.MannequinModuleManager>();
                module.InitializeModule(rules, null, null, null, null, null, null);
                Set(h, "_module", module); run.BindAdditionalHunter(h);
            }
            int windows = 0; run.HunterFacts.AfterglowWindowPublished += (room, seconds) => windows++;
            Assert.That(config.AfterglowSeconds, Is.Zero);
            run.ObserveLightExtinguished(1); Assert.That(windows, Is.Zero, "No Mannequin refuge window may be published.");
            Assert.That(hunters.Count, Is.EqualTo(2));
            foreach (var h in hunters)
            {
                Assert.That(Get(h, "_module"), Is.InstanceOf<Worsen.Domain.Hunter.Archetypes.Mannequin.MannequinModuleManager>());
                Assert.That(h.BeginAfterglow(1), Is.Zero, "Afterglow must not hold a Mannequin in a darkened room.");
            }
            run.SetPaused(true); run.ObserveLightExtinguished(2); Assert.That(windows, Is.Zero);
        }

        [Test]
        public void ConfirmedSprintDoorCrossingClosesTheActualDoorThroughLatch()
        {
            var rooms = new[] {
                new LevelRoom(1, new Vector3(0, 3, 0), new Vector3(12, 6, 12)),
                new LevelRoom(2, new Vector3(12, 3, 0), new Vector3(12, 6, 12)) };
            level.InitializeGenerated(new LevelGraph(rooms, new[] { new LevelEdge(1, 1, 2, true) },
                Array.Empty<LevelAnchor>(), 2, Vector3.right * 12),
                new[] { new InteractableState(101, InteractableKind.Door, 1, Vector3.right * 6, InteractableStateValue.Open, 1) });
            assembly.RecordRooms(new[] {
                new GeneratedRoomSample(1, rooms[0].Bounds, false, false, new[] { Vector3.right * 6 }),
                new GeneratedRoomSample(2, rooms[1].Bounds, false, false, new[] { Vector3.right * 6 }) });
            assembly.RecordPlayer(player.Id); assembly.Ready();
            Set(player, "_controller", new PlayerController(motion, Config<PlayerProfile>(), new System.Random(1), Config<PlayerEffectConfig>()));
            motion.AppliedEffects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("latch"), EffectKind.Upgrade, 1) });
            motion.IsSprinting = true;
            PlayerMovementSample Sample(float x, long tick) => new PlayerMovementSample(player.Id, tick,
                new Vector3(x, 1, 0), Vector3.right * 8, Vector3.right, 0, Vector2.zero, false, MovementState.Ground, 0);
            Call(expedition, "HandleMovement", Sample(5.5f, 1));
            Assert.That(level.Interactables.TryGet(101, out var door), Is.True); Assert.That(door.Value, Is.EqualTo(InteractableStateValue.Open));
            Call(expedition, "HandleMovement", Sample(6.5f, 2));
            level.Interactables.TryGet(101, out door); Assert.That(door.Value, Is.EqualTo(InteractableStateValue.Inactive));
            Assert.That(player.TryLatchDoor(2, 101), Is.False, "One latch per entered room.");
        }

        [TestCase(NoiseOrigin.Pacification, false)] [TestCase(NoiseOrigin.World, false)]
        [TestCase(NoiseOrigin.Presentation, false)] [TestCase(NoiseOrigin.FalsePositive, false)]
        [TestCase(NoiseOrigin.PlayerMovement, true)] [TestCase(NoiseOrigin.Firecracker, true)]
        [TestCase(NoiseOrigin.PlayerTriggeredCakeTrap, true)]
        public void DirectorAndEffectsIngressRejectNonGameplayNoiseBeforeHunters(NoiseOrigin origin, bool allowed)
        {
            var h = Hunter(-1);
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            director.SetLevelView(level.ReadOnlyState);
            var noise = new NoiseEvent(player.Id, Vector3.zero, 1f, 0, NoiseSourceKind.Firecracker, origin);
            var noises = ((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises;
            director.HearNoise(noise); Assert.That(noises.Count, Is.EqualTo(allowed ? 1 : 0));
            int delivered = 0; director.OnNoiseHintIssued += (_, fact) => { Assert.That(fact.Origin, Is.EqualTo(origin)); delivered++; };
            director.HearFloorWideNoise(noise);
            Assert.That(delivered, Is.EqualTo(allowed ? 1 : 0));
            var effects = Component<Worsen.Session.HorrorEffects.HorrorEffectsManager>(); Set(effects, "director", director);
            Call(effects, "DeliverNoise", noise);
            Assert.That(noises.Count, Is.EqualTo(allowed ? 1 : 0));
            h.HearNoise(noise);
            Assert.That(((IList)Get(h.ReadOnlyState, "HeardNoises")).Count, Is.EqualTo(allowed ? 1 : 0));
        }

        [TestCase(false)] [TestCase(true)]
        public void MimicBiteHoldRequiresSameTickAcceptedDamage(bool protectedByGrace)
        {
            var h = Hunter(-1); run.BindAdditionalHunter(h);
            motion.GraceActive = protectedByGrace;
            var bite = new MimicFact(h.Id, player.Id, MimicFactKind.BiteStarted, 0, Vector3.zero, 2f);
            int published = 0; run.HunterFacts.MimicFactPublished += fact => { if (fact.Kind == MimicFactKind.BiteStarted) published++; };
            Publish(h, "OnLungeHit", new HunterHit(h.Id, player.Id, 10, 0, Vector3.zero, severity: HitSeverity.Light, source: HitSource.Trap));
            Publish(h, "OnMimicFact", new MimicFact(h.Id, player.Id, MimicFactKind.PoseRemoved, 0, Vector3.zero));
            Publish(h, "OnMimicFact", bite);
            Assert.That(published, Is.Zero);
            // Keep a live grace deadline when AdvanceRecovery runs.
            if (protectedByGrace) motion.GraceWindow = new GraceWindowFact(player.Id, 0, 100, HitSeverity.Light);
            Call(run, "DrainPendingHits");
            Assert.That(((IDictionary)Get(motion, "MimicHolds")).Contains(h.Id), Is.EqualTo(!protectedByGrace));
            Assert.That(published, Is.EqualTo(protectedByGrace ? 0 : 1));
        }

        [Test]
        public void MimicGuidanceReceivesRunFactsButNeedsFaithlessArrowAndUnsubscribes()
        {
            var h = Hunter(-1);
            level.InitializeGenerated(new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 20) },
                Array.Empty<LevelEdge>(), new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, Vector3.forward * 5) }, 1, Vector3.zero));
            var config = Config<FloorConfig>(); Set(config, "_useRoomCakeDensity", false);
            Set(floor.GetComponent<FloorDriver>(), "_config", Config<FloorDriverConfig>());
            floor.Initialize(config, level.ReadOnlyState, new[] { motion }, new System.Random(1), requiredCakeCount: 1);
            var guidance = (FloorGuidanceController)Get(floor, "_guidance"); run.BindGameplay(null, floor, null);
            var normal = new[] { new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.forward * 5) };
            void Send(long tick)
            {
                Publish(h, "OnMimicFact", new MimicFact(h.Id, player.Id, MimicFactKind.Pose, tick, Vector3.right * 5));
                Publish(h, "OnMimicFact", new MimicFact(h.Id, player.Id, MimicFactKind.FaithlessWindow, tick, Vector3.right * 5, 3f));
            }
            Send(0); Assert.That(guidance.Apply(normal, player.Id, Vector3.zero)[0].EntityId, Is.EqualTo(EntityId.None));
            guidance.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(FloorGuidanceController.FaithlessArrow, EffectKind.Curse, 1) }));
            Send(1); Assert.That(guidance.Apply(normal, player.Id, Vector3.zero)[0].EntityId, Is.EqualTo(h.Id));
            run.DetachGameplay(); Assert.That(Get(h, "OnMimicFact"), Is.Null);
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
            var channel = RunFactRelayTestUtility.Publisher(run, relay);
            var output = channel.GetType().GetEvent(relay); output.AddEventHandler(channel, observer);
            run.BindGameplay(null, null, null); run.BindAdditionalHunter(hunter);
            Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(1), relay);
            run.SetPaused(true); Assert.That(run.IsPaused, Is.True);
            Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(1), relay + " while paused");
            run.SetPaused(false); run.gameObject.SetActive(false); Call(run, "OnDisable");
            Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(1), relay);
            run.gameObject.SetActive(true); Call(run, "OnEnable"); Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(2), relay);
            run.DetachGameplay(); Publish(hunter, publisher, default(T)); Assert.That(count, Is.EqualTo(2), relay);
            Assert.That((Get(hunter, publisher) as Delegate)?.GetInvocationList().Length ?? 0, Is.Zero, publisher);
            output.RemoveEventHandler(channel, observer);
        }
        [TestCase(NoiseOrigin.World)] [TestCase(NoiseOrigin.Pacification)]
        [TestCase(NoiseOrigin.Presentation)] [TestCase(NoiseOrigin.FalsePositive)]
        public void DisallowedNoiseNeverEntersDirector(NoiseOrigin origin)
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            run.BindGameplay(null, null, director);
            var noise = new NoiseEvent(player.Id, Vector3.zero, 1000f, 1, NoiseSourceKind.Firecracker, origin);
            run.ForwardGameplayNoise(noise); Call(run, "HandleTrapNoise", noise); Call(run.WorldFacts, "HandlePickupNoise", noise);
            Assert.That(((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises, Is.Empty);
        }
        [Test] public void UnspecifiedNoiseRequiresTheCommittedPlayerTrapBoundary()
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(1), new Worsen.Domain.Chase.ChaseBehaviorState(), new FloorBehaviorState());
            run.BindGameplay(null, null, director);
            var noise = new NoiseEvent(player.Id, Vector3.zero, 1f, 1, NoiseSourceKind.Trap);
            var noises = ((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises;
            run.ForwardGameplayNoise(noise); Call(run.WorldFacts, "HandlePickupNoise", noise);
            Call(run, "HandleTrapNoise", new NoiseEvent(EntityId.None, Vector3.zero, 1f, 1, NoiseSourceKind.Trap));
            Assert.That(noises, Is.Empty);
            NoiseEvent published = default; run.WorldFacts.WorldNoisePublished += value => published = value;
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
        { var value = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<T>(); owned.Add(value); return value; }
        private static FieldInfo Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static object Get(object o, string name) => Field(o, name).GetValue(o);
        private static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
        private static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
        private static void Publish(object o, string name, params object[] args) => (Get(o, name) as Delegate)?.DynamicInvoke(args);
        private static void Register(Type type, string name, object actor) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { actor });
    }
}
