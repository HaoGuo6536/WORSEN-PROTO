// ============================================================================
// FloorBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Owns counters, selected anchors, scheduled room changes and terminal-contact bookkeeping.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Scene-owned via FloorManager. No engine operations or events; a new initialization clears every collection.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class FloorBehaviorState : IReadOnlyFloorState
    {
        internal readonly List<LevelAnchor> SelectedAnchors = new List<LevelAnchor>();
        internal readonly List<LevelAnchor> MutableActiveAnchors = new List<LevelAnchor>();
        internal readonly Dictionary<int, RoomPhase> MutableRoomPhases = new Dictionary<int, RoomPhase>();
        internal readonly HashSet<int> CollectedCakes = new HashSet<int>();
        internal readonly HashSet<int> CollectedGoldenCakes = new HashSet<int>();
        internal readonly List<FloorScheduledTransition> Schedule = new List<FloorScheduledTransition>();
        internal IReadOnlyList<IReadOnlyPlayerState> Players = Array.Empty<IReadOnlyPlayerState>();
        internal LevelGraph Graph;
        internal double CollapseElapsed;
        internal double CueElapsed;
        internal int NextTransition;
        internal bool Ended;
        internal long Tick;
        internal FloorDisplaySnapshot Display;
        public bool IsReady { get; internal set; }
        public int CakeCount { get; internal set; }
        public int RequiredCakeCount { get; internal set; }
        public int GoldenCakeCount { get; internal set; }
        public ExitState ExitState { get; internal set; }
        public IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; }
        public IReadOnlyList<LevelAnchor> ActiveCakeAnchors { get; }

        public FloorBehaviorState()
        {
            RoomPhases = new ReadOnlyDictionary<int, RoomPhase>(MutableRoomPhases);
            ActiveCakeAnchors = MutableActiveAnchors.AsReadOnly();
        }
    }
}