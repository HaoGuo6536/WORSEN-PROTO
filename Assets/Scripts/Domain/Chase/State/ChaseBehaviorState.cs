// ============================================================================
// ChaseBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds a single player's aggregate chase and independent hunter timers. Separate source records prevent alternating sensors from producing false confirmation or loss.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Chase system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Domain.Chase
{
    public sealed class ChaseBehaviorState : IReadOnlyChaseState
    {
        public ChasePhase Phase { get; internal set; }
        public int ChaseId { get; internal set; }
        public EntityId PlayerId { get; internal set; }
        public EntityId HunterId { get; internal set; }
        public long StartTick { get; internal set; }
        public float Closeness { get; internal set; }
        public bool HasActiveChase => Phase != ChasePhase.None;
        internal int NextChaseId;
        internal double GraceSeconds;
        internal long LastTick = -1;
        internal long LastCatchTick = -1;
        internal readonly Dictionary<EntityId, ChaseHunterBehaviorState> Hunters = new Dictionary<EntityId, ChaseHunterBehaviorState>();
    }
}
