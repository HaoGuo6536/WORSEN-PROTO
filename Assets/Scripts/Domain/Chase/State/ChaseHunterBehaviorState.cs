// ============================================================================
// ChaseHunterBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores the continuous sight and loss time for one hunter. The Chase Controller updates these values using only explicit simulation steps.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Chase system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
namespace Worsen.Domain.Chase
{
    public sealed class ChaseHunterBehaviorState
    {
        internal double SightSeconds;
        internal double NoSightSeconds;
        internal bool Participating;
        internal long SeenTick;
    }
}
