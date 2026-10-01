// ============================================================================
// RunSessionManager.cs
// ============================================================================
//
// PURPOSE:
//   Owns the prototype's single fixed tick and reproducible run seed. A SceneRoot
//   initializes this service explicitly and signals readiness before any tick,
//   so gameplay never depends on which Unity component happened to awaken first.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Session · Run (Session system).
//   Owns the pure Controller and BehaviorState; publishes Core-typed run facts.
//
// KEY RESPONSIBILITIES:
//   - Own the canonical seeded run, timing, randomness, synchronous input, pause and ordered fixed ticks.
//   - Bind gameplay services and pair typed fact-channel inputs with enable/disable.
//   - Publish committed health/shield/floor snapshots for late listeners and retain legacy facts.
//   - Reject protected hit candidates before damage; publish committed combat, pickup, shrine and collapse facts.
//   - Resolve terminal, pending-death/revival and escape outcomes; close capture before terminal notification.
//
// DEPENDENCIES:
//   - Domain Shrine and Session Progression resolve generation-bound shrine activation.
//   - Run Controller, state, and definitions in this system; shared Core types.
//   - Domain Player, Hunter, Chase, Floor and Director Managers receive ordered ticks.
//   - Unity lifecycle and fixed delta time at the Session engine boundary (§8b).
//
// USAGE NOTES:
//   Persistent: this component is installed on its own root GameObject by the
//   setup tool. Initialize returns the canonical instance; duplicate components
//   destroy themselves without changing that instance's seed or subscriptions.
//   A SceneRoot sends HandleSceneReady once for each assembled scene instance,
//   which starts a fresh run using the retained seed. Scene references are scoped
//   to BindGameplay and cleared before scene loading and component destruction.
//   PrepareScene resets shared randomness before factories use it; readiness keeps
//   that same source. Scene publisher bindings are detached on disable/load/teardown.
//   Accepted hits are recorded even before chase confirmation. Terminal outcomes
//   are committed after facts, then capture closes before input/results notification.
//   Recovery uses the processing Run tick, not a queued hit's historical timestamp.
//   Explicit gameplay origins gate hearing before Director or direct delivery.
//   Pacification, pickups, hands and hunter sound facts are presentation-only.
//   Shield-only hits publish Damage=0 and telemetry health loss=0, still count as catches
//   and keep Player's grace/boost. Other hit payloads retain their incoming damage metadata.
//
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Domain.Player;
using Worsen.Domain.Hunter;
using Worsen.Domain.Chase;
using Worsen.Domain.Floor;
using Worsen.Domain.Director;
using Worsen.Domain.Shrine;
using Worsen.Session.Progression;

namespace Worsen.Session.Run
{
    public sealed class RunSessionManager : MonoBehaviour
    {
        private RunSessionBehaviorState state;
        private RunSessionController controller;
        private ChaseManager chase;
        private FloorManager floor;
        private DirectorManager director;
        private ShrineManager shrines;
        private ProgressionSessionManager shrineProgression;
        private EntityId shrinePlayer;
        private int shrineGeneration;
        private Func<float> shrineCollectedFraction;
        private readonly List<PlayerManager> players = new List<PlayerManager>();
        private readonly List<HunterManager> hunters = new List<HunterManager>();
        private readonly List<HunterHit> pendingHits = new List<HunterHit>();

        private RunHunterFactRelayController hunterFacts;
        public RunHunterFactRelayController HunterFacts => hunterFacts ??
            (hunterFacts = new RunHunterFactRelayController(() => IsPaused));
        private RunFloorFactRelayController floorFacts;
        public RunFloorFactRelayController FloorFacts => floorFacts ??
            (floorFacts = new RunFloorFactRelayController(() => IsPaused));
        private RunPlayerFactRelayController playerFacts;
        public RunPlayerFactRelayController PlayerFacts => playerFacts ??
            (playerFacts = new RunPlayerFactRelayController(() => IsPaused));
        private RunWorldFactRelayController worldFacts;
        public RunWorldFactRelayController WorldFacts => worldFacts ??
            (worldFacts = new RunWorldFactRelayController(() => IsPaused));

        public static RunSessionManager Instance { get; private set; }

        public event Action BeforeTick;
        public event Action<bool> PauseChanged;
        public event Action<InputFrame, float, long> TickAdvanced;
        public event Action<RunPhase> PhaseChanged;
        public event Action<PlayerMovementSample> PlayerMovementPublished;
        public event Action<HunterAttackSample> HunterAttackPublished;
        public event Action<HunterArchetypeFact> HunterArchetypePublished;
        public event Action<HunterMutationFact> HunterMutationPublished;
        public event Action<RamFact> RamFactPublished;
        public event Action<SkipFact> SkipFactPublished;
        public event Action<HunterDoorBreakFact> HunterDoorBreakPublished;
        public event Action<MannequinFact> MannequinFactPublished;
        public event Action<HunterHit> HitAccepted;
        public event Action<CollapseHandFact> CollapseHandPublished;
        public event Action<FloorTrapSprungFact> TrapSprung;
        public event Action<PlayerTraversalFact> PlayerTraversalPublished;
        public event Action<InputProbeRecord> PlayerProbeRecorded;
        public event Action<RunCaptureMetadata> CaptureStarted;
        public event Action<long, bool> CaptureEnded;
        public event Action<ChaseFact> ChaseStarted;
        public event Action<ChaseFact> ChaseEnded;
        public event Action<ChaseFact> ChasePhaseChanged;
        public event Action<EntityId, float, float> HealthChanged;
        public void PublishHealthSnapshot()
        {
            foreach (PlayerManager player in players)
                if (player != null && player.ReadOnlyState != null)
                    HealthChanged?.Invoke(player.Id, player.ReadOnlyState.Health, player.ReadOnlyState.MaxHealth);
        }
        public void PublishShieldSnapshot()
        {
            foreach (PlayerManager player in players)
                if (player != null && player.ReadOnlyShieldState != null) PlayerFacts.PublishShield(player.Id, player.ReadOnlyShieldState.Shield);
        }
        public event Action<EntityId, Vector3> PlayerDied;
        public event Action<EntityId, Vector3> PlayerDeathPending;
        public bool CancelDeathForRevival(EntityId player) => controller != null && controller.CancelDeathForRevival(player);
        public void PublishInventory(ConsumableInventorySnapshot inventory)
        { if (controller != null) PlayerFacts.PublishEmptyItemSlots(controller.EmptySlots(inventory.Inventory)); }
        public event Action<FloorDisplaySnapshot> FloorDisplayChanged;
        public void PublishFloorSnapshot() => FloorDisplayChanged?.Invoke(floor != null ? floor.Snapshot() : default);
        public event Action<IntrusionSample> IntrusionPublished;
        public event Action<TelemetrySample> TelemetryPublished;
        public event Action<RunSummary> RunEnded;

        public RunPhase Phase => state == null ? RunPhase.Boot : state.Phase;
        public bool IsPaused => state != null && state.Paused;
        public bool CanPause => state != null && state.SceneIsReady && state.Phase != RunPhase.Boot && state.Phase != RunPhase.Ended && state.PendingEndReason == RunEndReason.Unknown;
        public void SetPaused(bool paused)
        { if (controller != null && controller.SetPaused(paused)) PauseChanged?.Invoke(state.Paused); }
        public void SetSummaryContext(int seed, int depth) => controller?.SetSummaryContext(seed, depth);
        public long Tick => state == null ? 0 : state.Tick;
        public int Seed => state == null ? 0 : state.Seed;
        public double ElapsedSeconds => state == null ? 0 : state.ElapsedSeconds;
        public SceneKey Scene => state == null ? SceneKey.None : state.Scene;
        public System.Random RandomSource => controller == null ? null : controller.RandomSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public RunSessionManager Initialize(int seed)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return Instance;
            }

            Instance = this;
            if (controller == null)
            {
                state = new RunSessionBehaviorState(seed);
                controller = new RunSessionController(state, new System.Random(seed));
            }
            DontDestroyOnLoad(gameObject);
            return this;
        }

        public void ConfigureCapture(string revision, string configHash)
        {
            controller.ConfigureCapture(revision, configHash);
        }

        public void PrepareScene(SceneKey scene)
        {
            if (controller == null) throw new InvalidOperationException("Initialize before scene assembly.");
            CloseCapture(false);
            DetachGameplay();
            controller = new RunSessionController(state, new System.Random(state.Seed));
            controller.StartScene(scene);
            controller.SuspendForSceneLoad();
        }

        public void ReceiveInput(InputFrame frame)
        {
            if (controller != null) controller.ReceiveInput(frame);
        }

        public void PrepareScene(SceneKey scene, int seed)
        {
            if (controller == null) throw new InvalidOperationException("Initialize before scene assembly.");
            CloseCapture(false);
            DetachGameplay();
            string revision = state.SourceRevision;
            string configHash = state.ConfigSnapshotHash;
            state = new RunSessionBehaviorState(seed);
            controller = new RunSessionController(state, new System.Random(seed));
            controller.ConfigureCapture(revision, configHash);
            controller.StartScene(scene);
            controller.SuspendForSceneLoad();
        }

        public void HandleSceneReady(SceneKey scene)
        {
            SetPaused(false);
            if (controller == null)
                throw new InvalidOperationException("Initialize the Run Session before announcing scene readiness.");

            controller.StartScene(scene);
            controller.OpenCapture();
            CaptureStarted?.Invoke(new RunCaptureMetadata(Guid.NewGuid().ToString("N"), state.Seed,
                Time.fixedDeltaTime, state.SourceRevision, state.ConfigSnapshotHash,
                "assembly:PlayerFactory,HunterFactory,Floor;tick:Player,Hunter,Chase,Floor,Director;Director:no-random", 0));
            foreach (PlayerManager player in players)
            {
                HealthChanged?.Invoke(player.Id, player.ReadOnlyState.Health, player.ReadOnlyState.MaxHealth);
                PlayerFacts.PublishShield(player.Id, player.ReadOnlyShieldState.Shield);
            }
            PhaseChanged?.Invoke(state.Phase);
        }

        public void SuspendForSceneLoad()
        {
            CloseCapture(false);
            DetachGameplay();
            if (controller != null) controller.SuspendForSceneLoad();
        }

        public void BindGameplay(ChaseManager chaseManager, FloorManager floorManager, DirectorManager directorManager)
        {
            DetachGameplay();
            chase = chaseManager; floor = floorManager; director = directorManager;
            foreach (PlayerManager player in PlayerRegistry.Items) if (player != null) players.Add(player);
            foreach (HunterManager hunter in HunterRegistry.Items) if (hunter != null) hunters.Add(hunter);
            if (isActiveAndEnabled) OnEnable();
        }

        public void BindShrines(ShrineManager manager, ProgressionSessionManager progression, int generation,
            EntityId player, Func<float> collectedFraction)
        {
            if (shrines != null) shrines.Activated -= HandleShrineActivated;
            if (shrineProgression != null) shrineProgression.ShrineNoiseEmitted -= WorldFacts.HandleShrineNoise;
            shrines = manager; shrineProgression = progression; shrineGeneration = generation;
            shrinePlayer = player; shrineCollectedFraction = collectedFraction;
            if (!isActiveAndEnabled) return;
            if (shrines != null) shrines.Activated += HandleShrineActivated;
            if (shrineProgression != null) shrineProgression.ShrineNoiseEmitted += WorldFacts.HandleShrineNoise;
        }

        public void BindAdditionalHunter(HunterManager hunter)
        {
            if (hunter == null || hunters.Contains(hunter)) return;
            hunters.Add(hunter);
            if (!isActiveAndEnabled) return;
            SubscribeHunter(hunter);
        }

        private void TickShrines(InputFrame frame, float dt, long tick)
        {
            if (IsPaused || shrineProgression == null || state.PendingEndReason != RunEndReason.Unknown) return;
            // Advance clocks first: a newly activated delay starts at this committed tick,
            // and Director drains the resulting delivery-tick noise later in this same tick.
            shrineProgression.TickShrines(shrineGeneration, dt, tick);
            if (shrines != null && PlayerRegistry.TryGet(shrinePlayer, out var player) && player.ReadOnlyState.IsAlive)
                shrines.Sample(player.ReadOnlyState.Position, player.ReadOnlyState.Velocity, frame.Pressed, tick);
        }

        private void HandleShrineActivated(ShrineActivatedFact fact)
        {
            if (!IsPaused && shrineProgression != null && PlayerRegistry.TryGet(shrinePlayer, out var player) && player.ReadOnlyState.IsAlive)
                shrineProgression.ActivateShrine(shrineGeneration, fact, player, shrineCollectedFraction?.Invoke() ?? 0f);
        }

        private void OnEnable()
        {
            UnsubscribeGameplay();
            if (shrines != null) shrines.Activated += HandleShrineActivated;
            if (shrineProgression != null) shrineProgression.ShrineNoiseEmitted += WorldFacts.HandleShrineNoise;
            foreach (PlayerManager player in players)
            {
                player.OnHealthChanged += HandleHealth; player.OnDied += HandleDeath;
                player.OnShieldChanged += PlayerFacts.HandleShield;
                player.OnHeartbeat += HandleHeartbeat;
                player.OnGraceStarted += PlayerFacts.HandleGraceStarted; player.OnGraceEnded += PlayerFacts.HandleGraceEnded;
                player.OnTraversalProgress += PlayerFacts.HandleTraversalProgress; player.OnStumbled += PlayerFacts.HandleStumbled;
            }
            foreach (HunterManager hunter in hunters)
                SubscribeHunter(hunter);
            if (chase != null)
            {
                chase.OnChaseStarted += HandleChaseStarted;
                chase.OnChaseEnded += HandleChaseEnded;
                chase.OnPhaseChanged += HandleChasePhase;
                chase.OnProximityChanged += HandleProximity;
            }
            if (floor != null)
            {
                floor.OnPickupCollected += HandlePickup;
                floor.OnPickupNoise += WorldFacts.HandlePickupNoise;
                floor.OnHandNoise += WorldFacts.HandlePickupNoise;
                floor.OnTrapNoise += HandleTrapNoise;
                floor.OnTrapSprung += HandleTrapSprung;
                floor.OnBlinderHit += HunterFacts.HandleBlinderHit;
                floor.OnGuidanceChanged += FloorFacts.HandleGuidance;
                floor.OnBoundaryContact += HandleBoundaryContact;
                floor.OnCakeLost += FloorFacts.HandleCakeLost;
                floor.OnExitOpened += HandleExitOpened;
                floor.OnRoomPhaseChanged += HandleRoomPhase;
                floor.OnExitReached += HandleExitReached;
                floor.OnLethalContact += HandleLethal;
                floor.OnDisplayChanged += HandleFloorDisplay;
                floor.OnRoomDestruction += FloorFacts.HandleRoomDestruction;
                floor.OnCollapseHand += HandleCollapseHand;
            }
            if (director != null)
            {
                director.OnIntrusion += HandleIntrusion;
                director.OnPressureSampled += HandlePressure;
            }
        }

        private void UnsubscribeGameplay()
        {
            if (shrines != null) shrines.Activated -= HandleShrineActivated;
            if (shrineProgression != null) shrineProgression.ShrineNoiseEmitted -= WorldFacts.HandleShrineNoise;
            foreach (PlayerManager player in players)
                if (player != null)
                {
                    player.OnHealthChanged -= HandleHealth; player.OnDied -= HandleDeath;
                    player.OnShieldChanged -= PlayerFacts.HandleShield;
                    player.OnHeartbeat -= HandleHeartbeat;
                    player.OnGraceStarted -= PlayerFacts.HandleGraceStarted; player.OnGraceEnded -= PlayerFacts.HandleGraceEnded;
                    player.OnTraversalProgress -= PlayerFacts.HandleTraversalProgress; player.OnStumbled -= PlayerFacts.HandleStumbled;
                }
            foreach (HunterManager hunter in hunters) if (hunter != null)
                UnsubscribeHunter(hunter);
            if (chase != null)
            {
                chase.OnChaseStarted -= HandleChaseStarted;
                chase.OnChaseEnded -= HandleChaseEnded;
                chase.OnPhaseChanged -= HandleChasePhase;
                chase.OnProximityChanged -= HandleProximity;
            }
            if (floor != null)
            {
                floor.OnPickupCollected -= HandlePickup;
                floor.OnPickupNoise -= WorldFacts.HandlePickupNoise;
                floor.OnHandNoise -= WorldFacts.HandlePickupNoise;
                floor.OnTrapNoise -= HandleTrapNoise;
                floor.OnTrapSprung -= HandleTrapSprung;
                floor.OnBlinderHit -= HunterFacts.HandleBlinderHit;
                floor.OnGuidanceChanged -= FloorFacts.HandleGuidance;
                floor.OnBoundaryContact -= HandleBoundaryContact;
                floor.OnCakeLost -= FloorFacts.HandleCakeLost;
                floor.OnExitOpened -= HandleExitOpened;
                floor.OnRoomPhaseChanged -= HandleRoomPhase;
                floor.OnExitReached -= HandleExitReached;
                floor.OnLethalContact -= HandleLethal;
                floor.OnDisplayChanged -= HandleFloorDisplay;
                floor.OnRoomDestruction -= FloorFacts.HandleRoomDestruction;
                floor.OnCollapseHand -= HandleCollapseHand;
            }
            if (director != null)
            {
                director.OnIntrusion -= HandleIntrusion;
                director.OnPressureSampled -= HandlePressure;
            }
        }

        private void SubscribeHunter(HunterManager hunter)
        {
            hunter.OnLungeHit += QueueHit; hunter.OnFeedback += HunterFacts.HandleHunterFeedback;
            hunter.OnArchetypeFact += HandleHunterArchetype; hunter.OnHabit += HunterFacts.HandleHunterHabit;
            hunter.OnMutation += HandleHunterMutation; hunter.OnWeaverFact += HunterFacts.HandleWeaver;
            hunter.OnWebHit += HandleWebHit;
            hunter.OnBodyContact += HandleBodyContact;
            hunter.OnRamFact += HandleRam; hunter.OnSkipFact += HandleSkip; hunter.OnMimicFact += HandleMimic;
            hunter.OnBlinderHit += HunterFacts.HandleBlinderHit; hunter.OnBlinderThrow += HunterFacts.HandleBlinderThrow;
            hunter.OnBlinderSound += HunterFacts.HandleBlinderSound; hunter.OnBlinderTrapPolicy += HandleBlinderTrapPolicy;
            hunter.OnHeraldScream += HunterFacts.HandleHeraldScream; hunter.OnHeraldBreath += HunterFacts.HandleHeraldBreath;
            hunter.OnHeraldDeafen += HandleHeraldDeafen; hunter.OnDoorBreakCompleted += HandleDoorBreak;
            hunter.OnMannequinFact += HandleMannequin; hunter.OnStareFact += HunterFacts.HandleStare;
            if (hunter.Ticking == null) return;
            hunter.Ticking.OnSound += HunterFacts.HandleTickingSound; hunter.Ticking.OnGuidance += HunterFacts.HandleTickingGuidance;
            hunter.Ticking.OnNoise += HunterFacts.HandleTickingNoise;
        }
        private void UnsubscribeHunter(HunterManager hunter)
        {
            hunter.OnLungeHit -= QueueHit; hunter.OnFeedback -= HunterFacts.HandleHunterFeedback;
            hunter.OnArchetypeFact -= HandleHunterArchetype; hunter.OnHabit -= HunterFacts.HandleHunterHabit;
            hunter.OnMutation -= HandleHunterMutation; hunter.OnWeaverFact -= HunterFacts.HandleWeaver;
            hunter.OnWebHit -= HandleWebHit;
            hunter.OnBodyContact -= HandleBodyContact;
            hunter.OnRamFact -= HandleRam; hunter.OnSkipFact -= HandleSkip; hunter.OnMimicFact -= HandleMimic;
            hunter.OnBlinderHit -= HunterFacts.HandleBlinderHit; hunter.OnBlinderThrow -= HunterFacts.HandleBlinderThrow;
            hunter.OnBlinderSound -= HunterFacts.HandleBlinderSound; hunter.OnBlinderTrapPolicy -= HandleBlinderTrapPolicy;
            hunter.OnHeraldScream -= HunterFacts.HandleHeraldScream; hunter.OnHeraldBreath -= HunterFacts.HandleHeraldBreath;
            hunter.OnHeraldDeafen -= HandleHeraldDeafen; hunter.OnDoorBreakCompleted -= HandleDoorBreak;
            hunter.OnMannequinFact -= HandleMannequin; hunter.OnStareFact -= HunterFacts.HandleStare;
            if (hunter.Ticking == null) return;
            hunter.Ticking.OnSound -= HunterFacts.HandleTickingSound; hunter.Ticking.OnGuidance -= HunterFacts.HandleTickingGuidance;
            hunter.Ticking.OnNoise -= HunterFacts.HandleTickingNoise;
        }
        private void HandleHunterArchetype(HunterArchetypeFact fact)
        { if (!IsPaused) HunterArchetypePublished?.Invoke(fact); }

        private void HandleHunterMutation(HunterMutationFact fact)
        { if (!IsPaused) HunterMutationPublished?.Invoke(fact); }

        private void HandleWebHit(WebHitFact fact)
        {
            if (IsPaused) return;
            players.Find(player => player != null && player.Id == fact.Player)?.ApplyWebSlow(fact);
            HunterFacts.PublishWebHit(fact);
        }

        private void HandleRam(RamFact fact) { if (!IsPaused) RamFactPublished?.Invoke(fact); }
        private void HandleSkip(SkipFact fact) { if (!IsPaused) SkipFactPublished?.Invoke(fact); }
        private void HandleMimic(MimicFact fact)
        {
            if (IsPaused) return;
            if (fact.Kind == MimicFactKind.BiteStarted)
            { state?.PendingMimicBites.Add(fact); return; }
            floor?.ReceiveMimic(fact);
            players.Find(player => player != null && player.Id == fact.Player)?.ReceiveMimic(fact);
            HunterFacts.PublishMimic(fact);
        }
        private void HandleBlinderTrapPolicy(BlinderTrapPolicyFact fact)
        { if (IsPaused) return; floor?.ReceiveBlinderTrapPolicy(fact); HunterFacts.PublishBlinderTrapPolicy(fact); }
        private void HandleHeraldDeafen(HeraldDeafenFact fact)
        {
            if (IsPaused) return;
            QueueHit(new HunterHit(fact.Hunter, fact.Player, fact.Damage, fact.Tick, fact.Origin,
                ChaseEndReason.Unknown, HitSeverity.Light, HitSource.Scream));
            players.Find(player => player != null && player.Id == fact.Player)?.ReceiveHeraldDeafen(fact);
            HunterFacts.PublishHeraldDeafen(fact);
        }
        private void HandleDoorBreak(HunterDoorBreakFact fact) { if (!IsPaused) HunterDoorBreakPublished?.Invoke(fact); }
        private void HandleMannequin(MannequinFact fact) { if (!IsPaused) MannequinFactPublished?.Invoke(fact); }

        public void ObserveLightExtinguished(int roomId)
        {
            if (IsPaused || roomId <= 0) return;
            float seconds = 0f;
            foreach (var hunter in hunters)
                if (hunter != null) seconds = Math.Max(seconds, hunter.BeginAfterglow(roomId));
            if (seconds > 0f) HunterFacts.PublishAfterglow(roomId, seconds);
        }

        private void OnDisable() { SetPaused(false); UnsubscribeGameplay(); }

        public void DetachGameplay()
        {
            SetPaused(false);
            UnsubscribeGameplay();
            players.Clear(); hunters.Clear(); pendingHits.Clear();
            chase = null; floor = null; director = null;
            BindShrines(null, null, 0, EntityId.None, null);
            if (state != null) { state.FloorDeltaSeconds = 0f; state.PendingMimicBites.Clear(); }
        }

        private void FixedUpdate()
        {
            if (Instance != this || state == null || state.Paused || !state.SceneIsReady || state.Phase == RunPhase.Ended)
                return;
            DrainPendingHits();
            if (FinishIfRequested()) return;

            BeforeTick?.Invoke();
            float deltaTime = Time.fixedDeltaTime;
            if (!controller.TryTick(deltaTime, out InputFrame frame)) return;
            foreach (PlayerManager player in PlayerRegistry.Items)
                player.Tick(frame, deltaTime, state.Tick);
            foreach (HunterManager hunter in HunterRegistry.Items)
            {
                hunter.Tick(deltaTime, state.Tick);
                HunterAttackPublished?.Invoke(hunter.AttackSample);
            }
            if (chase != null) chase.Tick(deltaTime, state.Tick);
            DrainPendingHits();
            if (state.PendingEndReason == RunEndReason.Unknown)
            {
                if (floor != null)
                {
                    state.FloorDeltaSeconds = deltaTime;
                    try { floor.Tick(deltaTime, state.Tick); }
                    finally { state.FloorDeltaSeconds = 0f; }
                }
                TickShrines(frame, deltaTime, state.Tick);
                if (director != null) director.Tick(deltaTime, state.Tick);
            }
            foreach (PlayerManager player in PlayerRegistry.Items)
            {
                PlayerProbeRecorded?.Invoke(player.LastProbeRecord);
                PlayerMovementPublished?.Invoke(player.LastMovementSample);
                PlayerFacts.PublishSpeedNormalized(controller.NormalizeSpeed(player.ReadOnlyState.Velocity, player.ReadOnlyState.MaxDesignSpeed));
                foreach (PlayerTraversalFact fact in player.LastTraversalFacts)
                    PlayerTraversalPublished?.Invoke(fact);
            }
            TickAdvanced?.Invoke(frame, deltaTime, state.Tick);
            FinishIfRequested();
        }


        private void HandleHeartbeat(NoiseEvent noise)
        {
            if (IsPaused || noise.SourceKind != NoiseSourceKind.Heartbeat ||
                noise.Origin != NoiseOrigin.PlayerMovement || !players.Exists(player => player != null &&
                    player.Id == noise.Source && player.ReadOnlyState?.IsAlive == true)) return;
            ForwardGameplayNoise(noise);
        }
        private void HandleBodyContact(EntityId hunter, EntityId player, Vector3 normal)
        { if (!IsPaused) players.Find(value => value != null && value.Id == player)?.TryReboundFromHunter(hunter, normal); }

        private void HandleTrapNoise(NoiseEvent noise)
        {
            if (IsPaused) return;
            // Floor emits this event only after validating a living player's SpringTrap.
            // Stamp legacy payloads at that trusted event boundary, never from audibility.
            if (noise.Origin == NoiseOrigin.Unspecified && noise.Source.IsValid)
                noise = new NoiseEvent(noise.Source, noise.Position, noise.Loudness, noise.Tick,
                    noise.SourceKind, NoiseOrigin.PlayerTriggeredCakeTrap);
            WorldFacts.PublishNoise(noise);
            ForwardGameplayNoise(noise);
        }
        public void ForwardGameplayNoise(NoiseEvent noise)
        {
            if (IsPaused || !HunterHearingUtility.Allows(noise)) return;
            if (director != null) { director.HearNoise(noise); return; }
            foreach (HunterManager hunter in hunters)
                if (hunter != null && hunter.isActiveAndEnabled && hunter.ReadOnlyState?.IsActive == true)
                    hunter.HearNoise(noise);
        }

        private void HandleTrapSprung(FloorTrapSprungFact fact)
        { if (!IsPaused) TrapSprung?.Invoke(fact); }
        private void HandleBoundaryContact(EntityId id, int room, Vector3 acceleration, Vector3 position, long tick)
        {
            if (IsPaused || state == null || state.FloorDeltaSeconds <= 0f) return;
            PlayerManager player = players.Find(value => value != null && value.Id == id);
            player?.ApplyExternalAcceleration(acceleration, state.FloorDeltaSeconds);
        }

        private void HandleCollapseHand(CollapseHandFact fact)
        { if (IsPaused) return; controller.RecordHand(fact); CollapseHandPublished?.Invoke(fact); }
        private void QueueHit(HunterHit hit)
        {
            if (IsPaused) return;
            var player = players.Find(value => value != null && value.Id == hit.Target);
            if (hit.ContactNormal.sqrMagnitude > 0f && player != null && player.TryReboundFromHunter(hit.Hunter, hit.ContactNormal)) return;
            pendingHits.Add(hit);
        }
        private void DrainPendingHits()
        {
            if (IsPaused) return;
            var hits = pendingHits.ToArray();
            pendingHits.Clear();
            foreach (HunterHit hit in hits) ApplyAcceptedHit(hit);
            state.PendingMimicBites.Clear();
        }
        private void ApplyAcceptedHit(HunterHit hit)
        {
            PlayerManager target = players.Find(player => player != null && player.Id == hit.Target);
            if (target == null || !target.ReadOnlyState.IsAlive) return;
            target.AdvanceRecovery(Math.Max(Tick, target.ReadOnlyState.Tick));
            if (target.RevivalDamageImmune || target.RevivalCollisionGraceActive) return;

            float previousHealth = target.ReadOnlyState.Health;
            int chaseId = state.ActiveChaseId;
            bool accepted = hit.IsRam ? target.ApplyRamHit(hit.Damage, hit.HunterPosition, hit.Knockback, hit.Glancing) :
                target.ApplyHit(hit.Damage, hit.HunterPosition, hit.Severity, hit.Source);
            if (!accepted) return;
            foreach (var bite in state.PendingMimicBites.ToArray())
                if (bite.Hunter == hit.Hunter && bite.Player == hit.Target && bite.Tick == hit.Tick)
                {
                    state.PendingMimicBites.Remove(bite);
                    target.ReceiveMimic(bite); HunterFacts.PublishMimic(bite);
                }
            float healthLoss = Mathf.Max(0f, previousHealth - target.ReadOnlyState.Health);
            if (healthLoss == 0f) hit = new HunterHit(hit.Hunter, hit.Target, 0, hit.Tick,
                hit.HunterPosition, hit.Reason, hit.Severity, hit.Source);
            if (!target.ReadOnlyState.IsAlive)
            {
                HunterManager killer = hunters.Find(hunter => hunter != null && hunter.Id == hit.Hunter);
                controller.RecordDeathDetails(hit.Target, hit.Source == HitSource.Hand ? DeathCause.Hand :
                    hit.Source == HitSource.Trap ? DeathCause.Trap : DeathCause.Hunter, killer != null ? killer.ArchetypeKey : string.Empty);
            }
            HitAccepted?.Invoke(hit);
            Emit(TelemetrySampleKind.AcceptedHit, hit.Target, hit.Tick, healthLoss,
                chaseId == 0 ? "pre-confirmation" : "accepted", hit.Reason, chaseId);
            if (chase != null) chase.RecordCatch(hit);
        }
        private void HandleHealth(EntityId player, float health, float maximum)
        { if (!IsPaused) HealthChanged?.Invoke(player, health, maximum); }

        private void HandleDeath(EntityId player, Vector3 killer)
        { if (!IsPaused) controller.RequestEnd(RunEndReason.Died, player, killer); }
        private void HandleChaseStarted(ChaseFact fact)
        {
            if (IsPaused) return;
            controller.RecordChaseStarted(fact);
            players.Find(player => player != null && player.Id == fact.Player)?.ReceiveChase(fact);
            Emit(TelemetrySampleKind.ChaseStarted, fact.Player, fact.Tick, chaseId: fact.ChaseId);
            ChaseStarted?.Invoke(fact);
        }
        private void HandleChaseEnded(ChaseFact fact)
        {
            if (IsPaused) return;
            Emit(TelemetrySampleKind.ChaseEnded, fact.Player, fact.Tick, outcome: fact.EndReason, chaseId: fact.ChaseId);
            controller.RecordChaseEnded(fact);
            players.Find(player => player != null && player.Id == fact.Player)?.ReceiveChase(fact);
            ChaseEnded?.Invoke(fact);
        }
        private void HandleChasePhase(ChaseFact fact)
        { if (IsPaused) return; players.Find(player => player != null && player.Id == fact.Player)?.ReceiveChase(fact); ChasePhaseChanged?.Invoke(fact); }
        private void HandleProximity(ProximitySample sample)
        {
            if (IsPaused) return;
            Emit(TelemetrySampleKind.Proximity, sample.Player, sample.Tick, sample.Distance, chaseId: sample.ChaseId);
            HunterFacts.PublishProximity(sample);
        }
        private void HandlePickup(PickupCollectedFact fact)
        {
            if (IsPaused) return;
            controller.RecordCollection(fact);
            if (PlayerRegistry.TryGet(fact.PlayerId, out var player))
                FloorFacts.PublishPickup(fact, player.ReadOnlyState.Position);
        }
        private void HandleExitOpened(long tick)
        { if (IsPaused) return; controller.Apply(RunEvent.ExitOpened); PhaseChanged?.Invoke(state.Phase); }
        private void HandleRoomPhase(RoomPhaseChangedFact fact)
        {
            if (IsPaused) return;
            if (fact.Phase == RoomPhase.Telegraph && state.Phase == RunPhase.ExitOpen)
            { controller.Apply(RunEvent.CollapseStarted); PhaseChanged?.Invoke(state.Phase); }
            FloorFacts.PublishRoomPhase(fact);
        }
        private void HandleExitReached(ExitReachedFact fact)
        { if (!IsPaused) controller.RequestEnd(RunEndReason.Escaped, fact.PlayerId, Vector3.zero); }
        private void HandleLethal(FloorLethalContactFact fact)
        {
            if (IsPaused) return;
            PlayerManager target = players.Find(player => player != null && player.Id == fact.PlayerId);
            if (target != null && target.ReadOnlyState.IsAlive)
            {
                target.AdvanceRecovery(Math.Max(Tick, target.ReadOnlyState.Tick));
                target.ApplyHit(target.ReadOnlyState.Health, target.ReadOnlyState.Position);
            }
            if (target != null && !target.ReadOnlyState.IsAlive) controller.RecordDeathDetails(fact.PlayerId, DeathCause.Hand);
        }
        private void HandleFloorDisplay(FloorDisplaySnapshot snapshot) { if (!IsPaused) FloorDisplayChanged?.Invoke(snapshot); }
        private void HandleIntrusion(IntrusionSample sample) { if (!IsPaused) IntrusionPublished?.Invoke(sample); }
        private void HandlePressure(DirectorPressureSample sample)
        { if (!IsPaused) Emit(TelemetrySampleKind.Heat, sample.Player, sample.Tick, sample.HeatSeconds); }
        private void Emit(TelemetrySampleKind kind, EntityId player, long tick, float value = 0,
            string detail = "", ChaseEndReason outcome = ChaseEndReason.Unknown, int chaseId = -1)
        {
            if (chaseId < 0) chaseId = state.ActiveChaseId;
            TelemetryPublished?.Invoke(new TelemetrySample(tick, player, chaseId, kind, value, detail,
                state.ActiveChaseId > 0, outcome, controller.NextTelemetryEventId()));
        }
        private bool FinishIfRequested()
        {
            if (state.PendingEndReason == RunEndReason.Died) PlayerDeathPending?.Invoke(state.DeadPlayer, state.KillerPosition);
            if (!controller.TryFinish(out RunSummary summary)) return false;
            foreach (PlayerManager player in players)
                if (player != null) Emit(TelemetrySampleKind.FloorTime, player.Id, state.Tick, (float)state.ElapsedSeconds);
            if (summary.EndReason == RunEndReason.Died) PlayerDied?.Invoke(state.DeadPlayer, state.KillerPosition);
            CloseCapture(true);
            PhaseChanged?.Invoke(state.Phase);
            RunEnded?.Invoke(summary);
            return true;
        }

        private void CloseCapture(bool complete)
        {
            if (controller != null && controller.TryCloseCapture()) CaptureEnded?.Invoke(state.Tick, complete);
        }

        private void OnApplicationQuit() => CloseCapture(false);

        private void OnDestroy()
        {
            DetachGameplay();
            if (Instance == this) CloseCapture(false);
            if (Instance == this) Instance = null;
            BeforeTick = null;
            PauseChanged = null;
            TickAdvanced = null;
            PhaseChanged = null;
            PlayerMovementPublished = null;
            HunterAttackPublished = null;
            HitAccepted = null;
            HunterArchetypePublished = null; HunterMutationPublished = null;
            RamFactPublished = null; SkipFactPublished = null;
            HunterDoorBreakPublished = null; MannequinFactPublished = null;
            CollapseHandPublished = null;
            TrapSprung = null;
            PlayerTraversalPublished = null;
            PlayerProbeRecorded = null;
            CaptureStarted = null;
            CaptureEnded = null;
            ChaseStarted = null; ChaseEnded = null; ChasePhaseChanged = null;
            HealthChanged = null; PlayerDied = null;
            PlayerDeathPending = null;
            FloorDisplayChanged = null; IntrusionPublished = null;
            TelemetryPublished = null; RunEnded = null;
            hunterFacts?.Teardown(); floorFacts?.Teardown();
            playerFacts?.Teardown(); worldFacts?.Teardown();
            controller = null;
            state = null;
        }
    }
}

