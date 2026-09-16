// ============================================================================
// HunterBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one hunter's identity, observations, plan and attack bookkeeping.
//   Per-run speed scaling is instance data, leaving shared archetypes unchanged.
//   Reset replaces all fields so a reused entity cannot inherit contacts or hints.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Hunter system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterBehaviorState : IReadOnlyHunterState
    {
        public EntityId Id { get; internal set; }
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
        public float PhaseSeconds { get; internal set; }
        public Vector3 LungeDirection { get; internal set; }
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
        internal bool HasPatrolTarget;
        internal float SearchSeconds;
        internal ulong PlannedFacts = ulong.MaxValue;
        internal HunterAction Action;
        internal bool ActionFailed;
        internal int ReplanCount;
        internal int LastRoom;
        internal bool LoopDetected;
        internal readonly List<int> RecentRooms = new List<int>();
    }
}
