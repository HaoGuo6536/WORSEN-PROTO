// ============================================================================
// EntityId.cs
// ============================================================================
//
// PURPOSE:
//   Identifies one spawned entity instance with a plain integer value instead
//   of an engine reference. The architecture forbids game-rule code (§2) from
//   ever seeing a GameObject or Transform, so something has to answer the
//   question "which enemy took the damage?" in a form a pure C# class can hold,
//   compare, and store. That is this type: a small immutable value that survives
//   scene loads, serializes into a save file, and can be handed to a Controller
//   in a unit test that has no scene loaded at all.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared by every system.
//   EntityId is the only entity identity permitted to cross a Controller
//   boundary. A Factory (§1c) mints one per spawned instance and registers it;
//   a Manager (§1) resolves an engine Collider or GameObject into one before
//   anything reaches the logic stack; a Registry (§8) maps it back to the live
//   IEntityHandle when the presentation side needs the real object again.
//
// KEY RESPONSIBILITIES:
//   - Wrap an opaque integer identity in a distinct, type-safe struct.
//   - Provide value equality, hashing, and a readable ToString for logs.
//   - Provide the sentinel None for "no entity" (the default value).
//
// DEPENDENCIES:
//   - None. Core references nothing (§9).
//
// USAGE NOTES:
//   - Id 0 is reserved for None; a valid entity always has a non-zero value.
//     Check IsValid rather than comparing against default by hand.
//   - This type does NOT allocate ids. Minting fresh ids is the owning
//     Factory's job (§1c), so the counter lives with the Factory that owns the
//     entity pool and there is no mutable static state in Core.
//   - For persistence (§3) serialize Value, never the struct's layout.
//
// ============================================================================

using System;

namespace Worsen.Core
{
    /// <summary>Opaque, immutable identity for one spawned entity instance.</summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        /// <summary>The "no entity" sentinel. Equal to <c>default(EntityId)</c>.</summary>
        public static readonly EntityId None = default;

        private readonly int _value;

        public EntityId(int value)
        {
            _value = value;
        }

        /// <summary>The raw identity. Persist this, not the struct itself.</summary>
        public int Value => _value;

        /// <summary>False for <see cref="None"/> and for any default-constructed id.</summary>
        public bool IsValid => _value != 0;

        public bool Equals(EntityId other) => _value == other._value;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => _value;

        public override string ToString() => IsValid ? $"Entity#{_value}" : "Entity#none";

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
}
