// ============================================================================
// HunterBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Hunter life, beliefs, light reactions and committed attack identities.
//   Per-instance curses and cooldowns stay outside shared profile assets.
//   Reset removes contacts, light traces and unavailable rooms from the previous life.
//   Intent memory includes observed cake ids, fixed search legs and pending sound decisions.
// ARCHITECTURAL ROLE:
//   BehaviorState (section 3) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Hold per-life state, separate from shared configuration and foreign systems, preserving observable sensing and committed attacks.
//   - Retain the per-archetype pursuit policy and bounded commitment, deliberation and walking search-leg budgets.
//   - Retain run overrides across Controller.Reset while clearing transient habit and catch state.
//   - Retain the spawn request's duplicate index independently of entity identity.
//   - Store reaction timers, injected world and camera evidence, and once-only door facts.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Owned by HunterController; passive data only and no event publication.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterBehaviorState : IReadOnlyHunterPursuitState
    {
        public EntityId Id { get; internal set; }
        public int DuplicateIndex { get; internal set; }
        public EntityId TargetId { get; internal set; }
        public Vector3 Position { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public Vector3 Forward { get; internal set; }
        public bool PlayerVisible { get; internal set; }
        public Vector3 LastKnownPosition { get; internal set; }
        public long LastKnownTick { get; internal set; }
        public float BeliefConfidence { get; internal set; }
        public long Tick { get; internal set; }
        public bool IsActive { get; internal set; }
        public float RunSpeedMultiplier { get; internal set; } = 1f;
        public HunterLungePhase LungePhase { get; internal set; }
        public HunterAction CurrentAction => Action;
        public float LossSeconds { get; internal set; }
        public float LossDistance { get; internal set; }
        public bool PursuitSuppressed { get; internal set; }
        public HunterGoal CurrentGoal { get; internal set; }
        public bool IsDeliberating => DeliberationRemaining > 0f;
        public int LastPickupRoom { get; internal set; }
        public bool CatchActive { get; internal set; }
        internal bool ChaseActive, LossHabitObserved;
        internal float StunRemaining, SlipRemaining, DoorBreakRemaining;
        internal bool ReactionHeld, WickActive, DoorBreakPublished, PlayerViewClear;
        internal int BreakingDoor;
        internal long DoorRevision;
        internal IReadOnlyHunterWorldView WorldView;
        internal HunterPlayerView PlayerView;
        internal readonly Queue<HunterDoorBreakFact> DoorBreakFacts = new Queue<HunterDoorBreakFact>();
        internal float ThresholdPauseRemaining;
        internal readonly Queue<HunterHabitFact> HabitFacts = new Queue<HunterHabitFact>();
        internal readonly Dictionary<HunterTunable, float> Mutations = new Dictionary<HunterTunable, float>();
        internal readonly Dictionary<int, Vector3> CakePositions = new Dictionary<int, Vector3>();
        internal float CommitmentRemaining, DeliberationRemaining, RetreatRemaining;
        internal Vector3 DeliberationTarget;
        internal bool DeliberationFactPending, PendingNoiseDecision;
        internal NoiseEvent PendingNoise;
        internal float PendingNoiseLoudness;
        internal readonly List<NoiseEvent> HeardNoises = new List<NoiseEvent>();
        internal readonly Dictionary<int, int> CakeRooms = new Dictionary<int, int>();
        internal readonly List<Vector3> SearchRoute = new List<Vector3>();
        internal int SearchIndex, ObservedPlayerRoom, PreviousPlayerRoom;
        internal Vector3 ObservedPlayerVelocity;
        internal bool SearchActive;
        internal Vector3 CakeTarget, ExitTarget;
        internal bool CakeAvailable, ExitAvailable;
        internal readonly List<int> PredictionRoute = new List<int>();
        internal int PredictionIndex;
        internal bool Predict;
        public float PhaseSeconds { get; internal set; }
        public Vector3 LungeDirection { get; internal set; }
        public FlashlightSample Flashlight { get; internal set; }
        public bool LightObserved { get; internal set; }
        public bool DirectlyIlluminated { get; internal set; }
        public Vector3 LastLightPosition { get; internal set; }
        public float LightMemoryRemaining { get; internal set; }
        internal float LightExposure;
        internal float LightReactionRemaining;
        internal float LightReactionCooldown;
        internal Vector3 LightReactionTarget;
        internal float StepDistance;
        internal float FootstepCooldown;
        internal float ScreamCooldown;
        internal readonly Queue<HunterFeedbackKind> Feedback = new Queue<HunterFeedbackKind>();
        internal FlashlightSample Afterimage;
        internal float AfterimageRemaining;
        internal readonly HashSet<int> UnavailableRoomIds = new HashSet<int>();
        internal readonly List<Bounds> UnavailableRooms = new List<Bounds>();
        internal ProgressionTraits Traits;
        internal readonly HashSet<int> FiredRangedAttacks = new HashSet<int>();
        internal readonly HashSet<int> AcceptedRangedAttacks = new HashSet<int>();
        internal Vector3 AttackTarget;
        public int AttackSerial { get; internal set; }
        internal bool AttackBecameActive;
        internal bool LungeHitAccepted;
        internal bool SensorInitialized;
        internal bool PlayerHeard;
        internal bool HasHint;
        internal float BeliefInitialConfidence;
        internal long BeliefReferenceTick;
        internal float BeliefAgeAtReference;
        internal long LastNoiseTick = -1;
        internal float DeltaTime;
        internal Vector3 NavigationTarget;
        public Vector3 CurrentTarget => NavigationTarget;
        internal bool HasPatrolTarget;
        internal float SearchSeconds;
        internal float SearchLegBudget;
        internal ulong PlannedFacts = ulong.MaxValue;
        internal HunterAction Action;
        internal bool ActionFailed;
        internal int ReplanCount;
        internal int LastRoom;
        internal bool LoopDetected;
        internal readonly List<int> RecentRooms = new List<int>();
    }
}
