// ============================================================================
// FloorBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Owns counters, selected anchors, scheduled room changes and per-player exit holds.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Retain seeded pending collapse priorities and the next safely admitted room deadline.
//   - Retain trap identities, default-off cake hooks, typed guidance and separate collapse readiness.
//   - Support staged cracks, tearing, mist advance and escapable hand contacts.
//   - Retain elapsed locked-exit contact until cancellation or a terminal outcome.
//   - Retain dormant pocket identities and explicit activation deadlines on the floor clock.
//   - Retain optional rewards, queued cake losses, collapse hooks and read-only room hand phases.
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
    public sealed class FloorBehaviorState : IReadOnlyFloorCollapseState
    {
        internal readonly List<LevelAnchor> SelectedAnchors = new List<LevelAnchor>();
        internal readonly List<LevelAnchor> SpawnedAnchors = new List<LevelAnchor>();
        internal readonly List<FloorTrapSpawn> MutableTraps = new List<FloorTrapSpawn>();
        internal readonly HashSet<int> SprungTraps = new HashSet<int>();
        internal readonly List<LevelAnchor> GoldenAnchors = new List<LevelAnchor>();
        internal FloorCakeHooks CakeHooks;
        internal double TrapTickElapsed;
        internal double Elapsed;
        internal int CueAnchorId = -1;
        public bool CollapseStarted { get; internal set; }
        public IReadOnlyList<FloorTrapSpawn> Traps { get; }
        internal readonly Dictionary<int, PickupKind> RemainingRewards = new Dictionary<int, PickupKind>();
        internal readonly List<FloorCakeLoss> CakeLosses = new List<FloorCakeLoss>();
        internal readonly Dictionary<int, FloorHandPhase> MutableRoomHandPhases = new Dictionary<int, FloorHandPhase>();
        internal readonly FloorHandBehaviorState Hands = new FloorHandBehaviorState();
        internal bool FasterCollapse;
        internal bool ShuffledCollapse;
        internal readonly List<int> PendingCollapseRooms = new List<int>();
        internal double NextShuffledStart;
        public int Round { get; internal set; }
        internal readonly List<LevelAnchor> MutableActiveAnchors = new List<LevelAnchor>();
        internal readonly Dictionary<int, RoomPhase> MutableRoomPhases = new Dictionary<int, RoomPhase>();
        internal readonly HashSet<int> OptionalCrackedRooms = new HashSet<int>();
        internal readonly HashSet<int> CollectedCakes = new HashSet<int>();
        internal readonly HashSet<int> CollectedGoldenCakes = new HashSet<int>();
        internal readonly Dictionary<EntityId, double> ExitHolds = new Dictionary<EntityId, double>();
        internal readonly List<FloorScheduledTransition> Schedule = new List<FloorScheduledTransition>();
        internal IReadOnlyList<IReadOnlyPlayerState> Players = Array.Empty<IReadOnlyPlayerState>();
        internal LevelGraph Graph;
        internal double CollapseElapsed;
        internal double CueElapsed;
        internal int NextTransition;
        internal readonly Dictionary<int, double> CollapseStarts = new Dictionary<int, double>();
        internal readonly HashSet<int> PocketRooms = new HashSet<int>();
        internal readonly Dictionary<int, double> PocketStarts = new Dictionary<int, double>();
        internal bool Ended;
        internal long Tick;
        internal FloorDisplaySnapshot Display;
        public bool IsReady { get; internal set; }
        public int CakeCount { get; internal set; }
        public int RequiredCakeCount { get; internal set; }
        public int GoldenCakeCount { get; internal set; }
        public ExitState ExitState { get; internal set; }
        public IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; }
        public IReadOnlyDictionary<int, FloorHandPhase> RoomHandPhases { get; }
        public IReadOnlyList<LevelAnchor> ActiveCakeAnchors { get; }

        public FloorBehaviorState()
        {
            Traps = MutableTraps.AsReadOnly();
            RoomPhases = new ReadOnlyDictionary<int, RoomPhase>(MutableRoomPhases);
            RoomHandPhases = new ReadOnlyDictionary<int, FloorHandPhase>(MutableRoomHandPhases);
            ActiveCakeAnchors = MutableActiveAnchors.AsReadOnly();
        }
    }
}