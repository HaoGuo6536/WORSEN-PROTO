// ============================================================================
// IReadOnlyHunterState.cs
// ============================================================================
// PURPOSE:
//   Exposes committed hunter pose and belief to later Domain systems. Callers receive observations without permission to change a hunter.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Hunter system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public interface IReadOnlyHunterState
    {
        EntityId Id { get; }
        EntityId TargetId { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Vector3 Forward { get; }
        bool PlayerVisible { get; }
        Vector3 LastKnownPosition { get; }
        long LastKnownTick { get; }
        float BeliefConfidence { get; }
        long Tick { get; }
        bool IsActive { get; }
    }
}

