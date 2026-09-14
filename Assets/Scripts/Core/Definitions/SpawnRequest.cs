// ============================================================================
// SpawnRequest.cs
// ============================================================================
//
// PURPOSE:
//   Describes an entity that ought to come into existence, without creating it.
//   Controllers (§2) decide *that* something spawns and *what* spawns, but they
//   are pure C# and may not touch the engine, so they cannot call Instantiate.
//   They return one of these instead: a plain data record naming the archetype
//   and where to put it. The owning Manager hands it to the Factory (§1c),
//   which is the only script allowed to actually instantiate a prefab.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared by every system.
//   This is the hand-off DTO between the logic stack and entity creation:
//   Controller produces it, Manager routes it, Factory consumes it and returns
//   the resulting EntityId. Because it is a Core type it can cross any layer
//   boundary, including as an event payload (§9).
//
// KEY RESPONSIBILITIES:
//   - Name the archetype to spawn, by a stable designer-facing key.
//   - Carry the desired world placement as plain value types.
//   - Record which entity, if any, requested the spawn (projectile owner,
//     summoner, spawner), so attribution survives the round trip.
//
// DEPENDENCIES:
//   - EntityId (Core, same folder).
//   - UnityEngine value types (Vector3, Quaternion) only, which are pure-safe
//     and explicitly permitted outside Drivers (§7).
//
// USAGE NOTES:
//   - ArchetypeKey is a key, not a prefab reference. Resolving it to a Content
//     SO and its prefab is the Factory job (§1c); putting a prefab reference
//     here would drag an engine object into the logic stack.
//   - Owner is EntityId.None for an unowned spawn. Check Owner.IsValid rather
//     than testing for null.
//   - This is a request, not a permission. "Can the player afford this summon?"
//     is a Controller decision made before the request is ever built (§1c).
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    /// <summary>A Controller request that the Factory bring one entity into existence (§1c).</summary>
    public readonly struct SpawnRequest
    {
        public SpawnRequest(string archetypeKey, Vector3 position, Quaternion rotation, EntityId owner = default)
        {
            ArchetypeKey = archetypeKey;
            Position = position;
            Rotation = rotation;
            Owner = owner;
        }

        /// <summary>Designer-facing key the Factory resolves to an archetype Content SO (§4b).</summary>
        public string ArchetypeKey { get; }

        /// <summary>World position to spawn at.</summary>
        public Vector3 Position { get; }

        /// <summary>World rotation to spawn with.</summary>
        public Quaternion Rotation { get; }

        /// <summary>The entity that caused this spawn, or <see cref="EntityId.None"/> if unowned.</summary>
        public EntityId Owner { get; }
    }
}
