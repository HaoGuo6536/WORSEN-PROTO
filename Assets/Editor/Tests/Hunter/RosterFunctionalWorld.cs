// ============================================================================
// RosterFunctionalWorld.cs
// ============================================================================
// PURPOSE:
//   Supplies scripted immutable topology and interactable snapshots to roster tests.
//   It implements the same read-only contracts as Level/Floor without depending on
//   another archetype's fixture, so concurrent Echo changes cannot alter this arena.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test support (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Expose explicit room, door, light and floor snapshots for scripted scenarios.
// DEPENDENCIES:
//   - Core values, Level/Floor read-only contracts and Unity position values only.
// USAGE NOTES:
//   No scene objects or engine calls. Tests replace snapshots, not runtime rules.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
namespace Worsen.Tests.Hunter
{
    internal sealed class RosterFunctionalWorld : IReadOnlyLevelState, IReadOnlyFloorState, IReadOnlyInteractableSet
    {
        public bool IsReady => true;
        public LevelGraph Graph { get; set; }
        public int CakeCount => 0;
        public int RequiredCakeCount => 1;
        public int GoldenCakeCount => 0;
        public ExitState ExitState => ExitState.Open;
        public IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; } = new Dictionary<int, RoomPhase>();
        public IReadOnlyList<LevelAnchor> ActiveCakeAnchors => Array.Empty<LevelAnchor>();
        public Dictionary<int, bool> Doors { get; } = new Dictionary<int, bool>();
        public InteractableState Door { get; set; }
        public IReadOnlyList<InteractableState> Lights { get; set; } = Array.Empty<InteractableState>();
        public bool TryGet(int id, out InteractableState state)
        {
            state = Door; if (Door.Id == id) return true;
            foreach (var light in Lights) if (light.Id == id) { state = light; return true; }
            state = default; return false;
        }
        public IReadOnlyList<InteractableState> InRoom(int room) =>
            Lights.Concat(Door.Id > 0 ? new[] { Door } : Array.Empty<InteractableState>()).Where(i => i.RoomId == room).ToArray();
    }
}
