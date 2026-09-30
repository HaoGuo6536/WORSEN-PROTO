// ============================================================================
// LevelInteractableController.cs
// ============================================================================
// PURPOSE:
//   Validates and changes the Level-owned floor interactable registry using Core
//   snapshots. Door changes update the same edge-keyed portal state consumed by
//   AcousticOcclusionUtility, without mutating immutable movement topology.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Reject jammed opens before any mutation; breaking clears the jam atomically.
//   - Validate initial object kinds, values, identities and room/edge references.
//   - Apply typed, idempotent commands and return committed before/after facts.
// DEPENDENCIES:
//   - Core graph/interactable contracts and own BehaviorState; no Domain siblings.
// USAGE NOTES:
//   No time or randomness. H1 has no Knocked value: Marked on KnockableProp means
//   displaced; Inactive means upright. Broken is terminal. Commands return false
//   for wrong kinds, absent identities and no-ops; only Managers publish facts.
//   ClosedDoors supplements LevelGraph; it does not delete edges or close parallel
//   windows, which remain valid open sound paths under the shared hearing model.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    public sealed class LevelInteractableController
    {
        private readonly LevelInteractableBehaviorState _state;
        public LevelInteractableController(LevelInteractableBehaviorState state)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        public void Load(LevelGraph graph, IReadOnlyList<InteractableState> items)
        {
            Clear();
            if (graph == null || items == null) throw new ArgumentNullException();
            var ids = new HashSet<int>(); var doorEdges = new HashSet<int>();
            foreach (var item in items)
            {
                if (item.Id <= 0 || !ids.Add(item.Id) || !Enum.IsDefined(typeof(InteractableKind), item.Kind) ||
                    !ValidValue(item.Kind, item.Value) || !Finite(item.Position) ||
                    !graph.Rooms.Any(room => room.Id == item.RoomId && room.Bounds.Contains(item.Position)))
                    throw new ArgumentException("Invalid interactable identity, state or placement.");
                if (item.EdgeId != -1 && !graph.Edges.Any(edge => edge.Id == item.EdgeId &&
                    (edge.FromRoomId == item.RoomId || edge.ToRoomId == item.RoomId)))
                    throw new ArgumentException("Interactable edge must border its room.");
                if (item.Kind == InteractableKind.Door && (item.EdgeId == -1 || !doorEdges.Add(item.EdgeId)))
                    throw new ArgumentException("Each physical door needs one unique graph edge.");
            }
            foreach (var item in items)
            {
                _state.Items.Add(item.Id, item);
                if (item.Kind == InteractableKind.Door) _state.Portals.Add(item.EdgeId, item.Value == InteractableStateValue.Inactive);
            }
        }

        public bool Change(int id, InteractableKind kind, InteractableStateValue value,
            out InteractableState before, out InteractableState after)
        {
            after = default;
            if (!_state.TryGet(id, out before) || before.Kind != kind || !ValidValue(kind, value) ||
                before.Value == value || before.Value == InteractableStateValue.Broken ||
                (kind == InteractableKind.Door && value == InteractableStateValue.Open && _state.JammedDoors.Contains(id))) return false;
            after = new InteractableState(before.Id, before.Kind, before.RoomId, before.Position, value, before.EdgeId);
            _state.Items[id] = after;
            if (value == InteractableStateValue.Broken) _state.JammedDoors.Remove(id);
            if (kind == InteractableKind.Door) _state.Portals[after.EdgeId] = value == InteractableStateValue.Inactive;
            return true;
        }

        public void SetDoorJammed(int id, bool active)
        {
            if (!active) { _state.JammedDoors.Remove(id); return; }
            if (_state.TryGet(id, out var door) && door.Kind == InteractableKind.Door && door.Value == InteractableStateValue.Inactive)
                _state.JammedDoors.Add(id);
        }
        public void ClearDoorJams() => _state.JammedDoors.Clear();
        public void Clear() { _state.Items.Clear(); _state.Portals.Clear(); ClearDoorJams(); }

        private static bool ValidValue(InteractableKind kind, InteractableStateValue value)
        {
            if (value == InteractableStateValue.Inactive) return true;
            switch (kind)
            {
                case InteractableKind.Door: return value == InteractableStateValue.Open || value == InteractableStateValue.Broken;
                case InteractableKind.Light: return value == InteractableStateValue.Lit;
                case InteractableKind.KnockableProp: return value == InteractableStateValue.Marked || value == InteractableStateValue.Broken;
                case InteractableKind.Partition: return value == InteractableStateValue.Broken;
                case InteractableKind.ThresholdMark: return value == InteractableStateValue.Marked;
                default: return false;
            }
        }
        private static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsInfinity(value.x) ||
            float.IsNaN(value.y) || float.IsInfinity(value.y) || float.IsNaN(value.z) || float.IsInfinity(value.z));
    }
}
