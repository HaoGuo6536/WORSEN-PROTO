// ============================================================================
// LevelInteractableBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds the floor-local interactable registry behind Core's read-only boundary.
//   Immutable object snapshots and a read-only portal map let other systems inspect
//   state without taking ownership or modifying the acoustic graph inputs.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Store stable object identities and edge-keyed closed-door state.
// DEPENDENCIES:
//   - Core interactable values and System collections only.
// USAGE NOTES:
//   Owned by LevelManager and mutated only by its controller. Level owns this
//   registry because it owns the live graph; Procedural only supplies initial data.
//   InRoom returns a frozen, id-sorted snapshot; ClosedDoors is a live read-only view.
// ============================================================================
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    public sealed class LevelInteractableBehaviorState : IReadOnlyInteractableSet
    {
        internal readonly SortedDictionary<int, InteractableState> Items = new SortedDictionary<int, InteractableState>();
        internal readonly Dictionary<int, bool> Portals = new Dictionary<int, bool>();
        public IReadOnlyDictionary<int, bool> ClosedDoors { get; }
        public LevelInteractableBehaviorState() { ClosedDoors = new ReadOnlyDictionary<int, bool>(Portals); }
        public bool TryGet(int id, out InteractableState state) => Items.TryGetValue(id, out state);
        public IReadOnlyList<InteractableState> InRoom(int roomId)
            => System.Array.AsReadOnly(Items.Values.Where(item => item.RoomId == roomId).ToArray());
    }
}
