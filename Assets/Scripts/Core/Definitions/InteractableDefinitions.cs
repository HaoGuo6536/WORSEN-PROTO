// ============================================================================
// InteractableDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes shared world objects without passing engine components between systems.
//   Hunters, effects and shrines can inspect the same Level-owned object snapshots.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared World Interactable contracts.
// KEY RESPONSIBILITIES:
//   - Carry object identity, placement and state through a read-only lookup boundary.
// DEPENDENCIES:
//   - System collections and UnityEngine.Vector3 as data only.
// USAGE NOTES:
//   Ids are positive and floor-local; EdgeId -1 means no portal association.
//   Inactive means closed, unlit, intact or unmarked. Open is for doors, Lit for
//   lights, Broken for doors/props/partitions, and Marked for thresholds/shrines
//   (an activated shrine is marked). Owning systems validate kind/state pairings.
//   Set implementations must return immutable snapshots and empty room lists.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Core
{
    /// <summary>The semantic kind of a generated stateful world object.</summary>
    public enum InteractableKind { Door, Light, KnockableProp, Partition, ThresholdMark, Shrine }
    /// <summary>The active state of an interactable, interpreted according to its kind.</summary>
    public enum InteractableStateValue { Inactive, Open, Lit, Broken, Marked }

    /// <summary>An immutable observation of one floor-local world interactable.</summary>
    public readonly struct InteractableState
    {
        public InteractableState(int id, InteractableKind kind, int roomId, Vector3 position,
            InteractableStateValue value, int edgeId = -1)
        { Id = id; Kind = kind; RoomId = roomId; Position = position; Value = value; EdgeId = edgeId; }
        public int Id { get; }
        public InteractableKind Kind { get; }
        public int RoomId { get; }
        public int EdgeId { get; }
        public Vector3 Position { get; }
        public InteractableStateValue Value { get; }
    }

    /// <summary>Read-only identity and room lookup for immutable interactable observations.</summary>
    public interface IReadOnlyInteractableSet
    {
        bool TryGet(int id, out InteractableState state);
        IReadOnlyList<InteractableState> InRoom(int roomId);
    }
}
