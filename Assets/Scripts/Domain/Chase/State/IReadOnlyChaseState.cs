// ============================================================================
// IReadOnlyChaseState.cs
// ============================================================================
// PURPOSE:
//   Exposes the aggregate pursuit identity and feedback intensity. Consumers can inspect a chase without changing timers or creating measurement events.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Chase system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
using Worsen.Core;
namespace Worsen.Domain.Chase
{
    public interface IReadOnlyChaseState
    {
        ChasePhase Phase { get; }
        int ChaseId { get; }
        EntityId PlayerId { get; }
        EntityId HunterId { get; }
        long StartTick { get; }
        float Closeness { get; }
        bool HasActiveChase { get; }
    }
}

