// ============================================================================
// LevelMarkerRecord.cs
// ============================================================================
// PURPOSE:
//   Captures one authored marker as immutable data. Drivers report it to their
//   owner so graph assembly never reads transforms or marker components.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Describe stable authored level data across system boundaries.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only; no project layers.
// USAGE NOTES:
//   Immutable shared data; no runtime engine calls or lifecycle ownership.
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public readonly struct LevelMarkerRecord
    {
        public LevelMarkerRecord(int id, LevelMarkerKind kind, int roomId, int targetRoomId,
            Vector3 position, Vector3 size, bool bidirectional = true,
            TraversalAccess access = TraversalAccess.All, CakeAnchorType anchorType = CakeAnchorType.Flow)
        {
            Id = id;
            Kind = kind;
            RoomId = roomId;
            TargetRoomId = targetRoomId;
            Position = position;
            Size = size;
            Bidirectional = bidirectional;
            Access = access;
            AnchorType = anchorType;
        }

        public int Id { get; }
        public LevelMarkerKind Kind { get; }
        public int RoomId { get; }
        public int TargetRoomId { get; }
        public Vector3 Position { get; }
        public Vector3 Size { get; }
        public bool Bidirectional { get; }
        public TraversalAccess Access { get; }
        public CakeAnchorType AnchorType { get; }
    }
}

