// ============================================================================
// IEntityHandle.cs
// ============================================================================
//
// PURPOSE:
//   Lets a live engine object announce which entity it is. When a physics
//   callback or a raycast hands a Driver a Collider, somebody has to turn that
//   engine object back into the EntityId that the game rules understand. This
//   interface is that bridge: the root Manager of every Entity system (§1b)
//   implements it, so a single GetComponentInParent<IEntityHandle>() call on
//   any child collider, mesh, or trigger finds the entity it belongs to.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared by every system.
//   It is implemented by Entity Managers, consumed by Managers doing identity
//   resolution (§1) and by entity Registries (§8) that store and hand back live
//   instances. Nothing in the logic stack ever holds one — Controllers see
//   EntityId only.
//
// KEY RESPONSIBILITIES:
//   - Expose the EntityId of the entity that owns this GameObject hierarchy.
//
// DEPENDENCIES:
//   - EntityId (Core, same folder).
//
// USAGE NOTES:
//   - Keep this interface behavior-free (§5). It is a nameplate, not an API
//     surface for commanding an entity; commands go through the owning
//     Manager, which is the only thing allowed to drive that entity stack.
//   - Implementations are MonoBehaviours living in a system Manager/ folder,
//     so this interface must stay free of any engine dependency itself.
//
// ============================================================================

namespace Worsen.Core
{
    /// <summary>Implemented by an Entity system root Manager (§1b) to report its identity.</summary>
    public interface IEntityHandle
    {
        /// <summary>The identity assigned by the Factory when this instance was spawned.</summary>
        EntityId Id { get; }
    }
}
