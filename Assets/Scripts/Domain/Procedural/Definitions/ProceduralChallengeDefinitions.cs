// ============================================================================
// ProceduralChallengeDefinitions.cs
// ============================================================================
// PURPOSE:
//   Records local puzzle cages and threshold staging without adding required
//   graph edges. External consumers receive primitive facts from the Manager,
//   keeping Domain-specific construction types out of Presentation assemblies.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Carry immutable challenge identity, optional reward and freeze doorway data.
// DEPENDENCIES:
//   - Core anchor and Unity value types only.
// USAGE NOTES:
//   Puzzle reward anchors never enter LevelGraph.Anchors. DoorIndex is the stable
//   index in layout.Doors; BehindRoomId is a collapse preference, not a timer.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public enum ProceduralPuzzleKind { OrderedPlates, DimmingPath, MovingDoor, TimedVaults }
    public readonly struct ProceduralPuzzlePlan
    {
        public ProceduralPuzzlePlan(int id, int roomId, ProceduralPuzzleKind kind, Vector3 origin, bool alongX, LevelAnchor reward, bool reversed = false)
        { Id = id; RoomId = roomId; Kind = kind; Origin = origin; AlongX = alongX; Reward = reward; Reversed = reversed; }
        public bool Reversed { get; }
        public int Id { get; }
        public int RoomId { get; }
        public ProceduralPuzzleKind Kind { get; }
        public Vector3 Origin { get; }
        public bool AlongX { get; }
        public LevelAnchor Reward { get; }
    }
    public readonly struct ProceduralFreezePlan
    {
        public ProceduralFreezePlan(int doorIndex, int roomId, int behindRoomId, int anchorId, Vector3 hunter)
        { DoorIndex = doorIndex; RoomId = roomId; BehindRoomId = behindRoomId; AnchorId = anchorId; Hunter = hunter; }
        public int DoorIndex { get; }
        public int RoomId { get; }
        public int BehindRoomId { get; }
        public int AnchorId { get; }
        public Vector3 Hunter { get; }
    }
}
