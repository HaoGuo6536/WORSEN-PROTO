// ============================================================================
// FloorBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Owns counters, selected anchors, scheduled room changes and terminal outcome state.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Hold physical cake counters and live totals independently of trap selection.
//   - Track Passage-only gold and all registered collection objectives.
//   - Hold seeded collapse schedules, pocket activation and hand contact state.
//   - Retain traps, losses, guidance and floor-scoped hooks.
//   - Retain terminal outcome state until reset.
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
        internal readonly List<LevelAnchor> BonusGoldenAnchors = new List<LevelAnchor>();
        internal int TotalCakes;
        internal int TotalGoldenCakes;
        internal int CollapseCakeTarget;
        internal int CollapseCakeCredit;
        public int OptionalGoldenCakeCount { get; internal set; }
        internal IReadOnlyActiveEffects ActiveEffects;
        internal readonly Dictionary<EntityId, BlinderTrapPolicyFact> BlinderPolicies = new Dictionary<EntityId, BlinderTrapPolicyFact>();
        internal int AddedBlinderTraps;
        internal readonly Dictionary<int, LevelAnchor> PuzzleRewards = new Dictionary<int, LevelAnchor>();
        internal readonly HashSet<int> UnlockedPuzzleRewards = new HashSet<int>();
        internal readonly HashSet<int> PassageRewards = new HashSet<int>();
        internal FloorCakeHooks CakeHooks;
        internal double TrapTickElapsed;
        internal double Elapsed;
        internal int CueAnchorId = -1;
        internal int GoldenCueAnchorId = -1;
        internal int CueRoomId;
        internal EntityId CuePlayerId;
        public bool CollapseStarted { get; internal set; }
        public IReadOnlyList<FloorTrapSpawn> Traps { get; }
        internal readonly Dictionary<int, PickupKind> RemainingRewards = new Dictionary<int, PickupKind>();
        internal readonly List<FloorCakeLoss> CakeLosses = new List<FloorCakeLoss>();
        internal readonly Dictionary<int, FloorHandPhase> MutableRoomHandPhases = new Dictionary<int, FloorHandPhase>();
        internal readonly FloorHandBehaviorState Hands = new FloorHandBehaviorState();
        internal bool FasterCollapse;
        internal bool ShuffledCollapse;
        internal bool RouteSafeCollapse;
        internal readonly List<int> PendingCollapseRooms = new List<int>();
        internal double NextShuffledStart;
        public int Round { get; internal set; }
        internal readonly List<LevelAnchor> MutableActiveAnchors = new List<LevelAnchor>();
        internal readonly Dictionary<int, RoomPhase> MutableRoomPhases = new Dictionary<int, RoomPhase>();
        internal readonly HashSet<int> OptionalCrackedRooms = new HashSet<int>();
        internal readonly HashSet<int> CollectedCakes = new HashSet<int>();
        internal readonly HashSet<int> CollectedGoldenCakes = new HashSet<int>();

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