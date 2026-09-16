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
//   - Relay committed pickup, hand, destruction and hunter sound facts without audio decisions.
//   - Publish committed hunter attack telegraphs and prepare independently seeded generated floors.
//   - Maintain one persistent canonical run and one shared seeded random source.
//   - Request synchronous input publication immediately before each fixed tick.
//   - Hand explicit delta time to the Controller and publish completed tick data.
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
        public event Action<InputFrame, float, long> TickAdvanced;
        public event Action<RunPhase> PhaseChanged;
        public event Action<PlayerMovementSample> PlayerMovementPublished;
        public event Action<HunterAttackSample> HunterAttackPublished;
        public event Action<HunterFeedbackEvent> HunterFeedbackPublished;
        public event Action<HunterHit> HitAccepted;
        public event Action<PickupCollectedFact, Vector3> PickupCollected;
        public event Action<RoomDestructionSample> RoomDestructionPublished;
        public event Action<CollapseHandFact> CollapseHandPublished;
        public event Action<PlayerTraversalFact> PlayerTraversalPublished;
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
            { player.OnHealthChanged += HandleHealth; player.OnDied += HandleDeath; }
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
                floor.OnExitOpened += HandleExitOpened;
                floor.OnRoomPhaseChanged += HandleRoomPhase;
                floor.OnExitReached += HandleExitReached;
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
                if (player != null) { player.OnHealthChanged -= HandleHealth; player.OnDied -= HandleDeath; }
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
                floor.OnExitOpened -= HandleExitOpened;
                floor.OnRoomPhaseChanged -= HandleRoomPhase;
                floor.OnExitReached -= HandleExitReached;
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

        private void OnDisable() => UnsubscribeGameplay();

        public void DetachGameplay()
        {
            UnsubscribeGameplay();
            players.Clear(); hunters.Clear(); pendingHits.Clear();
            chase = null; floor = null; director = null;
        }

        private void FixedUpdate()
        {
            if (Instance != this || state == null || !state.SceneIsReady || state.Phase == RunPhase.Ended)
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
                if (floor != null) floor.Tick(deltaTime, state.Tick);
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

        private void HandleHunterFeedback(HunterFeedbackEvent fact) => HunterFeedbackPublished?.Invoke(fact);
        private void HandleRoomDestruction(RoomDestructionSample sample) => RoomDestructionPublished?.Invoke(sample);
        private void HandleCollapseHand(CollapseHandFact fact) => CollapseHandPublished?.Invoke(fact);
        private void QueueHit(HunterHit hit) => pendingHits.Add(hit);
        private void DrainPendingHits()
        {
            var hits = pendingHits.ToArray();
            pendingHits.Clear();
            foreach (HunterHit hit in hits) ApplyAcceptedHit(hit);
        }
        private void ApplyAcceptedHit(HunterHit hit)
        {
            PlayerManager target = players.Find(player => player != null && player.Id == hit.Target);
            if (target == null || !target.ReadOnlyState.IsAlive) return;
            float previousHealth = target.ReadOnlyState.Health;
            int chaseId = state.ActiveChaseId;
            target.ApplyHit(hit.Damage, hit.HunterPosition);
            if (target.ReadOnlyState.Health >= previousHealth) return;
            HitAccepted?.Invoke(hit);
            Emit(TelemetrySampleKind.AcceptedHit, hit.Target, hit.Tick, hit.Damage,
                chaseId == 0 ? "pre-confirmation" : "accepted", hit.Reason, chaseId);
            if (chase != null) chase.RecordCatch(hit);
        }
        private void HandleHealth(EntityId player, float health, float maximum) => HealthChanged?.Invoke(player, health, maximum);
        private void HandleDeath(EntityId player, Vector3 killer) => controller.RequestEnd(RunEndReason.Died, player, killer);
        private void HandleChaseStarted(ChaseFact fact)
        {
            controller.RecordChaseStarted(fact);
            Emit(TelemetrySampleKind.ChaseStarted, fact.Player, fact.Tick, chaseId: fact.ChaseId);
            ChaseStarted?.Invoke(fact);
        }
        private void HandleChaseEnded(ChaseFact fact)
        {
            Emit(TelemetrySampleKind.ChaseEnded, fact.Player, fact.Tick, outcome: fact.EndReason, chaseId: fact.ChaseId);
            controller.RecordChaseEnded(fact);
            ChaseEnded?.Invoke(fact);
        }
        private void HandleChasePhase(ChaseFact fact) => ChasePhaseChanged?.Invoke(fact);
        private void HandleProximity(ProximitySample sample)
        {
            Emit(TelemetrySampleKind.Proximity, sample.Player, sample.Tick, sample.Distance, chaseId: sample.ChaseId);
            ProximityPublished?.Invoke(sample);
        }
        private void HandlePickup(PickupCollectedFact fact)
        {
            controller.RecordCollection(fact);
            if (PlayerRegistry.TryGet(fact.PlayerId, out var player))
                PickupCollected?.Invoke(fact, player.ReadOnlyState.Position);
        }
        private void HandleExitOpened(long tick)
        { controller.Apply(RunEvent.ExitOpened); PhaseChanged?.Invoke(state.Phase); }
        private void HandleRoomPhase(RoomPhaseChangedFact fact)
        {
            if (fact.Phase == RoomPhase.Telegraph && state.Phase == RunPhase.ExitOpen)
            { controller.Apply(RunEvent.CollapseStarted); PhaseChanged?.Invoke(state.Phase); }
            RoomPhaseChanged?.Invoke(fact);
        }
        private void HandleExitReached(ExitReachedFact fact) => controller.RequestEnd(RunEndReason.Escaped, fact.PlayerId, Vector3.zero);
        private void HandleLethal(FloorLethalContactFact fact)
        {
            PlayerManager target = players.Find(player => player != null && player.Id == fact.PlayerId);
            if (target != null && target.ReadOnlyState.IsAlive)
                target.ApplyHit(target.ReadOnlyState.Health, target.ReadOnlyState.Position);
        }
        private void HandleFloorDisplay(FloorDisplaySnapshot snapshot) => FloorDisplayChanged?.Invoke(snapshot);
        private void HandleIntrusion(IntrusionSample sample) => IntrusionPublished?.Invoke(sample);
        private void HandlePressure(DirectorPressureSample sample) => Emit(TelemetrySampleKind.Heat, sample.Player, sample.Tick, sample.HeatSeconds);
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
            TickAdvanced = null;
            PhaseChanged = null;
            PlayerMovementPublished = null;
            HunterAttackPublished = null;
            HunterFeedbackPublished = null; HitAccepted = null; PickupCollected = null;
            RoomDestructionPublished = null; CollapseHandPublished = null;
            PlayerTraversalPublished = null;
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

