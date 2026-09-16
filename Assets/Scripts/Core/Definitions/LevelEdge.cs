// ============================================================================
// LevelEdge.cs
// ============================================================================
// PURPOSE:
//   Describes an authored route between rooms, including its direction and actor
//   restrictions. Its stable identity survives deterministic arena rebuilds.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Describe stable authored level data across system boundaries.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only; no project layers.
// USAGE NOTES:
//   Immutable shared data; no runtime engine calls or lifecycle ownership.
// ============================================================================

namespace Worsen.Core
{
    public readonly struct LevelEdge
    {
        public LevelEdge(int id, int fromRoomId, int toRoomId, bool bidirectional,
            TraversalAccess access = TraversalAccess.All)
        {
            Id = id;
            FromRoomId = fromRoomId;
            ToRoomId = toRoomId;
            Bidirectional = bidirectional;
            Access = access;
        }

        public int Id { get; }
        public int FromRoomId { get; }
        public int ToRoomId { get; }
        public bool Bidirectional { get; }
        public TraversalAccess Access { get; }
    }
}

