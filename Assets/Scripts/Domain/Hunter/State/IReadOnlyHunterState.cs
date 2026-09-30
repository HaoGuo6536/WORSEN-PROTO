// ============================================================================
// IReadOnlyHunterState.cs
// ============================================================================
// PURPOSE:
//   Exposes committed hunter pose and belief to later Domain systems. Callers receive observations without permission to change a hunter.
//   An additive pursuit view carries the archetype loss rule and deliberate withdrawal.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
//   - Keep legacy observation providers compatible while real hunters expose pursuit policy.
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
    public interface IReadOnlyHunterPursuitState : IReadOnlyHunterState
    {
        float LossSeconds { get; }
        float LossDistance { get; }
        bool PursuitSuppressed { get; }
    }
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

