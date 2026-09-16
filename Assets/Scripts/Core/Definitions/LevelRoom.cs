// ============================================================================
// LevelRoom.cs
// ============================================================================
// PURPOSE:
//   Describes a stable room and its world-space extent. Downstream systems use
//   the same room identity for path topology and spatial collapse placement.
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
    public readonly struct LevelRoom
    {
        public LevelRoom(int id, Vector3 center, Vector3 size)
        {
            Id = id;
            Center = center;
            Size = size;
        }

        public int Id { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public Bounds Bounds => new Bounds(Center, Size);
    }
}

