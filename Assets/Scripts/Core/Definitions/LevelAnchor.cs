// ============================================================================
// LevelAnchor.cs
// ============================================================================
// PURPOSE:
//   Records a stable pickup anchor owned by a room. Floor selects from these
//   immutable positions without discovering or retaining Level components.
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
    public readonly struct LevelAnchor
    {
        public LevelAnchor(int id, int roomId, CakeAnchorType type, Vector3 position)
        {
            Id = id;
            RoomId = roomId;
            Type = type;
            Position = position;
        }

        public int Id { get; }
        public int RoomId { get; }
        public CakeAnchorType Type { get; }
        public Vector3 Position { get; }
    }
}

