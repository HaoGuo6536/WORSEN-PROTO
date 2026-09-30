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
//   - Forward typed guidance/traps and route boundary acceleration using the Floor tick delta.
//   - Share pickup/hand/trap hearing; environmental sources use Director only when bound.
//   - Pair traversal, stumble and cake-loss relays; route pickup noise to bound active hunters.
//   - Drop gameplay facts raised during pause rather than replaying them on resume.
//   - Own authoritative pause, gate queued damage before ticks, and publish detailed end facts.
//   - Forward hit severity/source, advance recovery without rewinding, and relay Player grace facts.
//   - Relay committed pickup, hand, destruction and hunter sound facts without audio decisions.
//   - Publish committed hunter attack telegraphs and prepare independently seeded generated floors.
//   - Maintain one persistent canonical run and one shared seeded random source.
//   - Request synchronous input publication immediately before each fixed tick.
//   - Hand explicit delta time to the Controller and publish completed tick data.
//   - Consume only Floor's unified escape fact, retaining the early-bail flag.
//
// DEPENDENCIES:
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
//   EntityId.None noises go through Director's acoustic hint path, never both paths.
//   Without Director the legacy direct-hearing fallback is retained. No source is forged.
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

namespace Worsen.Session.Run
{
    public sealed class RunSessionManager : MonoBehaviour
    {
        private RunSessionBehaviorState state;
        private RunSessionController controller;
        private ChaseManager chase;
        private FloorManager floor;
        private DirectorManager director;
        private readonly List<PlayerManager> players = new List<PlayerManager>();
        private readonly List<HunterManager> hunters = new List<HunterManager>();
        private readonly List<HunterHit> pendingHits = new List<HunterHit>();

        public static RunSessionManager Instance { get; private set; }

        public event Action BeforeTick;
        public event Action<bool> PauseChanged;
        public event Action<InputFrame, float, long> TickAdvanced;
        public event Action<RunPhase> PhaseChanged;
        public event Action<PlayerMovementSample> PlayerMovementPublished;
        public event Action<HunterAttackSample> HunterAttackPublished;
        public event Action<HunterFeedbackEvent> HunterFeedbackPublished;
        public event Action<HunterHit> HitAccepted;
        public event Action<GraceWindowFact> OnGraceStarted;
        public event Action<GraceWindowFact> OnGraceEnded;
        public event Action<PickupCollectedFact, Vector3> PickupCollected;
        public event Action<RoomDestructionSample> RoomDestructionPublished;
        public event Action<CollapseHandFact> CollapseHandPublished;
        public event Action<FloorTrapSprungFact> TrapSprung;
        public event Action<IReadOnlyList<GuidanceTarget>> GuidanceChanged;
        public event Action<PlayerTraversalFact> PlayerTraversalPublished;
        public event Action<EntityId, long, TraversalKind, float, bool> TraversalProgressed;
        public event Action<EntityId, long, float> PlayerStumbled;
        public event Action<int, int, PickupKind, long> CakeLost;
        public event Action<InputProbeRecord> PlayerProbeRecorded;
        public event Action<RunCaptureMetadata> CaptureStarted;
        public event Action<long, bool> CaptureEnded;
        public event Action<ChaseFact> ChaseStarted;
        public event Action<ChaseFact> ChaseEnded;
        public event Action<ChaseFact> ChasePhaseChanged;
        public event Action<ProximitySample> ProximityPublished;
        public event Action<EntityId, float, float> HealthChanged;
        public event Action<EntityId, Vector3> PlayerDied;
        public event Action<FloorDisplaySnapshot> FloorDisplayChanged;
        public event Action<RoomPhaseChangedFact> RoomPhaseChanged;
        public event Action<IntrusionSample> IntrusionPublished;
        public event Action<TelemetrySample> TelemetryPublished;
        public event Action<int> EmptyItemSlotsChanged;
        public event Action<float> SpeedNormalizedPublished;
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
                EmptyItemSlotsChanged?.Invoke(controller.EmptySlots(player.ReadOnlyState.Inventory));
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

        private void OnEnable()
        {
            UnsubscribeGameplay();
            foreach (PlayerManager player in players)
            {
                player.OnHealthChanged += HandleHealth; player.OnDied += HandleDeath;
                player.OnGraceStarted += HandleGraceStarted; player.OnGraceEnded += HandleGraceEnded;
                player.OnTraversalProgress += HandleTraversalProgress; player.OnStumbled += HandleStumbled;
            }
            foreach (HunterManager hunter in hunters)
            { hunter.OnLungeHit += QueueHit; hunter.OnFeedback += HandleHunterFeedback; }
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
                floor.OnPickupNoise += HandlePickupNoise;
                floor.OnHandNoise += HandlePickupNoise;
                floor.OnTrapNoise += HandlePickupNoise;
                floor.OnTrapSprung += HandleTrapSprung;
                floor.OnGuidanceChanged += HandleGuidance;
                floor.OnBoundaryContact += HandleBoundaryContact;
                floor.OnCakeLost += HandleCakeLost;
                floor.OnExitOpened += HandleExitOpened;
                floor.OnRoomPhaseChanged += HandleRoomPhase;
                floor.OnEscapeResolved += HandleExitReached;
                floor.OnLethalContact += HandleLethal;
                floor.OnDisplayChanged += HandleFloorDisplay;
                floor.OnRoomDestruction += HandleRoomDestruction;
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
            foreach (PlayerManager player in players)
                if (player != null)
                {
                    player.OnHealthChanged -= HandleHealth; player.OnDied -= HandleDeath;
                    player.OnGraceStarted -= HandleGraceStarted; player.OnGraceEnded -= HandleGraceEnded;
                    player.OnTraversalProgress -= HandleTraversalProgress; player.OnStumbled -= HandleStumbled;
                }
            foreach (HunterManager hunter in hunters) if (hunter != null)
            { hunter.OnLungeHit -= QueueHit; hunter.OnFeedback -= HandleHunterFeedback; }
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
                floor.OnPickupNoise -= HandlePickupNoise;
                floor.OnHandNoise -= HandlePickupNoise;
                floor.OnTrapNoise -= HandlePickupNoise;
                floor.OnTrapSprung -= HandleTrapSprung;
                floor.OnGuidanceChanged -= HandleGuidance;
                floor.OnBoundaryContact -= HandleBoundaryContact;
                floor.OnCakeLost -= HandleCakeLost;
                floor.OnExitOpened -= HandleExitOpened;
                floor.OnRoomPhaseChanged -= HandleRoomPhase;
                floor.OnEscapeResolved -= HandleExitReached;
                floor.OnLethalContact -= HandleLethal;
                floor.OnDisplayChanged -= HandleFloorDisplay;
                floor.OnRoomDestruction -= HandleRoomDestruction;
                floor.OnCollapseHand -= HandleCollapseHand;
            }
            if (director != null)
            {
                director.OnIntrusion -= HandleIntrusion;
                director.OnPressureSampled -= HandlePressure;
            }
        }

        private void OnDisable() { SetPaused(false); UnsubscribeGameplay(); }

        public void DetachGameplay()
        {
            SetPaused(false);
            UnsubscribeGameplay();
            players.Clear(); hunters.Clear(); pendingHits.Clear();
            chase = null; floor = null; director = null;
            if (state != null) state.FloorDeltaSeconds = 0f;
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
                if (director != null) director.Tick(deltaTime, state.Tick);
            }
            foreach (PlayerManager player in PlayerRegistry.Items)
            {
                PlayerProbeRecorded?.Invoke(player.LastProbeRecord);
                PlayerMovementPublished?.Invoke(player.LastMovementSample);
                SpeedNormalizedPublished?.Invoke(controller.NormalizeSpeed(player.ReadOnlyState.Velocity, player.ReadOnlyState.MaxDesignSpeed));
                foreach (PlayerTraversalFact fact in player.LastTraversalFacts)
                    PlayerTraversalPublished?.Invoke(fact);
            }
            TickAdvanced?.Invoke(frame, deltaTime, state.Tick);
            FinishIfRequested();
        }

        private void HandleTraversalProgress(EntityId player, long tick, TraversalKind kind, float progress, bool active)
        { if (!IsPaused) TraversalProgressed?.Invoke(player, tick, kind, progress, active); }
        private void HandleStumbled(EntityId player, long tick, float duration)
        { if (!IsPaused) PlayerStumbled?.Invoke(player, tick, duration); }
        private void HandleCakeLost(int anchorId, int roomId, PickupKind kind, long tick)
        { if (!IsPaused) CakeLost?.Invoke(anchorId, roomId, kind, tick); }
        private void HandlePickupNoise(NoiseEvent noise)
        {
            if (IsPaused) return;
            if (!noise.Source.IsValid && director != null) { director.HearNoise(noise); return; }
            foreach (HunterManager hunter in hunters)
                if (hunter != null && hunter.isActiveAndEnabled && hunter.ReadOnlyState?.IsActive == true)
                    hunter.HearNoise(noise);
        }
        private void HandleTrapSprung(FloorTrapSprungFact fact)
        { if (!IsPaused) TrapSprung?.Invoke(fact); }
        private void HandleGuidance(IReadOnlyList<GuidanceTarget> targets)
        { if (!IsPaused) GuidanceChanged?.Invoke(targets); }
        private void HandleBoundaryContact(EntityId id, int room, Vector3 acceleration, Vector3 position, long tick)
        {
            if (IsPaused || state == null || state.FloorDeltaSeconds <= 0f) return;
            PlayerManager player = players.Find(value => value != null && value.Id == id);
            player?.ApplyExternalAcceleration(acceleration, state.FloorDeltaSeconds);
        }
        private void HandleHunterFeedback(HunterFeedbackEvent fact)
        { if (!IsPaused) HunterFeedbackPublished?.Invoke(fact); }
        private void HandleRoomDestruction(RoomDestructionSample sample)
        { if (!IsPaused) RoomDestructionPublished?.Invoke(sample); }
        private void HandleCollapseHand(CollapseHandFact fact)
        { if (IsPaused) return; controller.RecordHand(fact); CollapseHandPublished?.Invoke(fact); }
        private void QueueHit(HunterHit hit) { if (!IsPaused) pendingHits.Add(hit); }
        private void DrainPendingHits()
        {
            if (IsPaused) return;
            var hits = pendingHits.ToArray();
            pendingHits.Clear();
            foreach (HunterHit hit in hits) ApplyAcceptedHit(hit);
        }
        private void ApplyAcceptedHit(HunterHit hit)
        {
            PlayerManager target = players.Find(player => player != null && player.Id == hit.Target);
            if (target == null || !target.ReadOnlyState.IsAlive) return;
            target.AdvanceRecovery(Math.Max(Tick, target.ReadOnlyState.Tick));
            float previousHealth = target.ReadOnlyState.Health;
            int chaseId = state.ActiveChaseId;
            if (!target.ApplyHit(hit.Damage, hit.HunterPosition, hit.Severity, hit.Source)) return;
            if (target.ReadOnlyState.Health >= previousHealth) return;
            if (!target.ReadOnlyState.IsAlive)
            {
                HunterManager killer = hunters.Find(hunter => hunter != null && hunter.Id == hit.Hunter);
                controller.RecordDeathDetails(hit.Target, hit.Source == HitSource.Hand ? DeathCause.Hand :
                    hit.Source == HitSource.Trap ? DeathCause.Trap : DeathCause.Hunter, killer != null ? killer.ArchetypeKey : string.Empty);
            }
            HitAccepted?.Invoke(hit);
            Emit(TelemetrySampleKind.AcceptedHit, hit.Target, hit.Tick, hit.Damage,
                chaseId == 0 ? "pre-confirmation" : "accepted", hit.Reason, chaseId);
            if (chase != null) chase.RecordCatch(hit);
        }
        private void HandleHealth(EntityId player, float health, float maximum)
        { if (!IsPaused) HealthChanged?.Invoke(player, health, maximum); }
        private void HandleGraceStarted(GraceWindowFact fact) { if (!IsPaused) OnGraceStarted?.Invoke(fact); }
        private void HandleGraceEnded(GraceWindowFact fact) { if (!IsPaused) OnGraceEnded?.Invoke(fact); }
        private void HandleDeath(EntityId player, Vector3 killer)
        { if (!IsPaused) controller.RequestEnd(RunEndReason.Died, player, killer); }
        private void HandleChaseStarted(ChaseFact fact)
        {
            if (IsPaused) return;
            controller.RecordChaseStarted(fact);
            Emit(TelemetrySampleKind.ChaseStarted, fact.Player, fact.Tick, chaseId: fact.ChaseId);
            ChaseStarted?.Invoke(fact);
        }
        private void HandleChaseEnded(ChaseFact fact)
        {
            if (IsPaused) return;
            Emit(TelemetrySampleKind.ChaseEnded, fact.Player, fact.Tick, outcome: fact.EndReason, chaseId: fact.ChaseId);
            controller.RecordChaseEnded(fact);
            ChaseEnded?.Invoke(fact);
        }
        private void HandleChasePhase(ChaseFact fact) { if (!IsPaused) ChasePhaseChanged?.Invoke(fact); }
        private void HandleProximity(ProximitySample sample)
        {
            if (IsPaused) return;
            Emit(TelemetrySampleKind.Proximity, sample.Player, sample.Tick, sample.Distance, chaseId: sample.ChaseId);
            ProximityPublished?.Invoke(sample);
        }
        private void HandlePickup(PickupCollectedFact fact)
        {
            if (IsPaused) return;
            controller.RecordCollection(fact);
            if (PlayerRegistry.TryGet(fact.PlayerId, out var player))
                PickupCollected?.Invoke(fact, player.ReadOnlyState.Position);
        }
        private void HandleExitOpened(long tick)
        { if (IsPaused) return; controller.Apply(RunEvent.ExitOpened); PhaseChanged?.Invoke(state.Phase); }
        private void HandleRoomPhase(RoomPhaseChangedFact fact)
        {
            if (IsPaused) return;
            if (fact.Phase == RoomPhase.Telegraph && state.Phase == RunPhase.ExitOpen)
            { controller.Apply(RunEvent.CollapseStarted); PhaseChanged?.Invoke(state.Phase); }
            RoomPhaseChanged?.Invoke(fact);
        }
        private void HandleExitReached(ExitReachedFact fact, bool bailed)
        { if (!IsPaused) controller.RequestEnd(RunEndReason.Escaped, fact.PlayerId, Vector3.zero, bailed); }
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
            HunterFeedbackPublished = null; HitAccepted = null; PickupCollected = null;
            OnGraceStarted = null; OnGraceEnded = null;
            RoomDestructionPublished = null; CollapseHandPublished = null;
            TrapSprung = null; GuidanceChanged = null;
            PlayerTraversalPublished = null;
            TraversalProgressed = null; PlayerStumbled = null; CakeLost = null;
            PlayerProbeRecorded = null;
            CaptureStarted = null;
            CaptureEnded = null;
            ChaseStarted = null; ChaseEnded = null; ChasePhaseChanged = null;
            ProximityPublished = null; HealthChanged = null; PlayerDied = null;
            FloorDisplayChanged = null; RoomPhaseChanged = null; IntrusionPublished = null;
            TelemetryPublished = null; EmptyItemSlotsChanged = null; SpeedNormalizedPublished = null; RunEnded = null;
            controller = null;
            state = null;
        }
    }
}

