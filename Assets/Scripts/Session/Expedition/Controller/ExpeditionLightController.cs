// ============================================================================
// ExpeditionLightController.cs
// ============================================================================
// PURPOSE:
//   Resolves shared Mannequin light facts with deterministic budgets and Wick priority.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Apply absolute type-wide lamp budgets, never multiply duplicate requests.
//   - Expire temporary room overrides and retain permanent overrides for this floor.
//   - Restore captured light state after overrides, deferring restoration during Wick.
// DEPENDENCIES:
//   - Core interactable snapshots and injected delta time; no engine calls.
// USAGE NOTES:
//   The Manager applies returned changes to Level and Procedural; recreate per floor.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionLightController
    {
        private readonly ExpeditionLightBehaviorState _state;
        public ExpeditionLightController(ExpeditionLightBehaviorState state) { _state = state ?? throw new ArgumentNullException(nameof(state)); }
        public void Observe(MannequinFact fact)
        {
            if (fact.Kind == MannequinFactKind.LampBudget)
            {
                if (!float.IsNaN(fact.Value) && !float.IsInfinity(fact.Value)) _state.LampBudget = Math.Max(0f, Math.Min(1f, fact.Value));
                return;
            }
            if (fact.Kind != MannequinFactKind.RoomLightOverride || fact.RoomId <= 0 ||
                (!fact.Permanent && (!(fact.Seconds > 0f) || float.IsInfinity(fact.Seconds)))) return;
            if (_state.Rooms.TryGetValue(fact.RoomId, out var current) && (current.Permanent || current.Tick > fact.Tick)) return;
            _state.Rooms[fact.RoomId] = fact;
            _state.Remaining[fact.RoomId] = fact.Seconds;
        }
        public IReadOnlyList<KeyValuePair<int, bool>> Tick(float dt, bool wick, IReadOnlyList<InteractableState> interactables)
        {
            var changes = new List<KeyValuePair<int, bool>>();
            if (interactables == null) return changes;
            if (dt > 0f && !float.IsInfinity(dt))
                foreach (int room in new List<int>(_state.Rooms.Keys))
                {
                    if (_state.Rooms[room].Permanent) continue;
                    _state.Remaining[room] -= dt;
                    if (_state.Remaining[room] > 0f) continue;
                    _state.Rooms.Remove(room); _state.Remaining.Remove(room);
                }
            var lamps = new List<InteractableState>();
            foreach (var item in interactables) if (item.Kind == InteractableKind.Light) lamps.Add(item);
            lamps.Sort((a, b) => a.Id.CompareTo(b.Id));
            int keep = (int)Math.Ceiling(lamps.Count * _state.LampBudget);
            for (int i = 0; i < lamps.Count; i++)
            {
                var lamp = lamps[i];
                bool hasRoom = _state.Rooms.TryGetValue(lamp.RoomId, out var room);
                bool owned = hasRoom || i >= keep;
                bool restoring = _state.Restore.TryGetValue(lamp.Id, out bool original);
                if (!owned && !restoring) continue;
                // Wick already lights the floor. Never capture its forced state as a baseline.
                if (wick && !restoring) continue;
                bool lit = lamp.Value == InteractableStateValue.Lit;
                if (!restoring) { original = lit; _state.Restore[lamp.Id] = original; }
                bool desired = wick || (owned ? i < keep && (hasRoom ? room.Lit : original) : original);
                if (lit != desired) changes.Add(new KeyValuePair<int, bool>(lamp.Id, desired));
                if (!owned && !wick) _state.Restore.Remove(lamp.Id);
            }
            return changes;
        }
    }
}
