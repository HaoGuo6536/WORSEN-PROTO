// ============================================================================
// SkipBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores one Skip's floor-local route counts and pending arrival commitment.
//   Duplicate hunters share inputs but never share cooldowns or queues.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter Skip.
// KEY RESPONSIBILITIES:
//   - Retain deduplicated route uses, elapsed time and immutable fact queues.
// DEPENDENCIES:
//   - Hunter context and Core route/fact values only.
// USAGE NOTES:
//   BeginFloor clears every floor-local observation; state has no events or logic.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Skip
{
    public sealed class SkipBehaviorState
    {
        public HunterArchetypeContext Context;
        public long Floor = -1, LastTick = -1;
        public float Elapsed;
        public bool Pending;
        public SkipTraversalUse Candidate, WalkRoute;
        public readonly Dictionary<int, int> Counts = new Dictionary<int, int>();
        public readonly Dictionary<int, SkipTraversalUse> Routes = new Dictionary<int, SkipTraversalUse>();
        public readonly Queue<SkipFact> Facts = new Queue<SkipFact>();
    }
}
