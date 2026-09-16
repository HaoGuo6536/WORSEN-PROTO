// ============================================================================
// IReadOnlyPlayerState.cs
// ============================================================================
// PURPOSE:
//   Allows dependent Domain systems to read player facts without mutating them.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Player read-only state contract.
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Lives beside PlayerBehaviorState. EntityContext remains Core-only; inject this interface separately.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    public interface IReadOnlyPlayerState
    {
        EntityId Id { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Vector3 Forward { get; }
        float HeadingDegrees { get; }
        float SprintSpeed { get; }
        float MaxDesignSpeed { get; }
        float Health { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        bool LookBack { get; }
        MovementState MovementState { get; }
        long Tick { get; }
        IReadOnlyList<NoiseEvent> RecentNoises { get; }
        InventorySnapshot Inventory { get; }
    }
}