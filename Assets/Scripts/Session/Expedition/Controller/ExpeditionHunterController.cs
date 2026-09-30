// ============================================================================
// ExpeditionHunterController.cs
// ============================================================================
// PURPOSE:
//   Projects Level lighting and revisioned door jams into hunter world evidence.
//   Admits each floor binding and committed Skip route once without engine queries.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Project live room lighting with unknown rooms failing closed.
//   - Scope jam completion to the exact active revision.
//   - Deduplicate floor initialization and route uses without counting failed attempts.
// DEPENDENCIES:
//   - Own BehaviorState, Core immutable contracts and Unity value types only.
// USAGE NOTES:
//   A Manager creates one instance per floor. Bounds come from authored geometry.
//   The Skip module still owns interception placement and player-hit semantics.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionHunterController : IReadOnlyHunterWorldView
    {
        private readonly ExpeditionHunterBehaviorState _state;
        public ExpeditionHunterController(ExpeditionHunterBehaviorState state, long floor, LevelGraph graph, IReadOnlyInteractableSet interactables)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _state.Floor = floor; _state.Graph = graph; _state.Interactables = interactables;
        }
        public IReadOnlyList<HunterDoorJam> JammedDoors => new List<HunterDoorJam>(_state.Jams.Values).AsReadOnly();
        public bool TryGetRoomLit(int roomId, out bool lit)
        {
            lit = false;
            if (_state.Graph == null || _state.Interactables == null) return false;
            foreach (var room in _state.Graph.Rooms)
            {
                if (room.Id != roomId) continue;
                foreach (var item in _state.Interactables.InRoom(roomId))
                    if (item.Kind == InteractableKind.Light && item.Value == InteractableStateValue.Lit) lit = true;
                return true;
            }
            return false;
        }
        public bool BindHunter(EntityId hunter) => hunter.IsValid && _state.BoundHunters.Add(hunter);
        public void SetJam(DoorJamFact fact, Bounds bounds)
        {
            if (!fact.Active) { _state.Jams.Remove(fact.DoorId); return; }
            _state.Jams[fact.DoorId] = new HunterDoorJam(fact.DoorId, ++_state.Revision, bounds, fact.BreakSeconds);
        }
        public bool MatchesBreak(HunterDoorBreakFact fact) => _state.BoundHunters.Contains(fact.Hunter) &&
            _state.Jams.TryGetValue(fact.DoorId, out var jam) && jam.Revision == fact.Revision;
        public void RemoveJam(int doorId) => _state.Jams.Remove(doorId);
        public bool TryRoute(EntityId player, long sequence, int route, SkipRouteKind kind, Vector3 position, int mark, out SkipTraversalUse use)
        {
            use = default;
            var key = (kind, route);
            if (!player.IsValid || _state.Floor <= 0 || route <= 0 || sequence < 0 ||
                (_state.Routes.TryGetValue(key, out long previous) && sequence <= previous)) return false;
            _state.Routes[key] = sequence;
            use = new SkipTraversalUse(_state.Floor, sequence, player, route, kind, position, mark);
            return true;
        }
        public bool TryDoorway(EntityId player, long tick, Vector3 portal, Vector3 arrival, out SkipTraversalUse use)
        {
            use = default;
            if (_state.Graph == null || _state.Interactables == null) return false;
            foreach (var room in _state.Graph.Rooms)
                foreach (var item in _state.Interactables.InRoom(room.Id))
                    if (item.Kind == InteractableKind.ThresholdMark && item.Position.x == portal.x && item.Position.z == portal.z)
                        return TryRoute(player, tick, item.EdgeId, SkipRouteKind.Doorway, arrival, item.Id, out use);
            return false;
        }
    }
}
