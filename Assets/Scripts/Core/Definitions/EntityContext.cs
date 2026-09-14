// ============================================================================
// EntityContext.cs
// ============================================================================
//
// PURPOSE:
//   Carries everything a freshly spawned entity needs to start running, in one
//   parameter. Entity systems (§1b) are forbidden from hunting for their
//   dependencies: no Find, no scene lookups, no reaching for a singleton to
//   discover the world they woke up in. Instead the Factory pushes this context
//   into the entity Manager at spawn time, which keeps entities testable, keeps
//   pooled instances from inheriting a previous life, and keeps the dependency
//   direction in the layer graph honest (§9).
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared by every Entity system.
//   Built by a Factory (§1c) during Spawn and passed to the entity root
//   Manager Initialize(archetype, ctx). The Manager unpacks it and hands the
//   pieces to its Controller; the context itself does not survive as state.
//
// KEY RESPONSIBILITIES:
//   - Carry the EntityId the Factory just minted for this instance.
//   - Carry the deterministic randomness source the entity logic must use, so
//     no Controller ever reaches for UnityEngine.Random (§2).
//
// DEPENDENCIES:
//   - EntityId (Core, same folder).
//   - System.Random only. Core references nothing else (§9).
//
// USAGE NOTES:
//   - THIS TYPE IS EXPECTED TO GROW. As systems appear, add the read-only state
//     views (IReadOnly[System]State, §2c) and service handles that entities
//     legitimately need, and pass them in here rather than letting an entity
//     look them up. Keep every added member Core-typed or an interface declared
//     in Core; an engine object or a Domain type in this struct would break the
//     layer graph for everyone who consumes it.
//   - Random is shared, not owned: it comes from the Factory so that an entire
//     run is reproducible from one seed. Do not replace it per entity.
//   - Pooled entities get a fresh context on every reuse; nothing about a prior
//     life may leak through (§1c).
//
// ============================================================================

using System;

namespace Worsen.Core
{
    /// <summary>The dependency bundle a Factory pushes into an entity at spawn (§1b, §1c).</summary>
    public readonly struct EntityContext
    {
        public EntityContext(EntityId id, Random random)
        {
            Id = id;
            Random = random;
        }

        /// <summary>The identity this instance was just given.</summary>
        public EntityId Id { get; }

        /// <summary>Seeded randomness for this entity logic. Never UnityEngine.Random (§2).</summary>
        public Random Random { get; }
    }
}
