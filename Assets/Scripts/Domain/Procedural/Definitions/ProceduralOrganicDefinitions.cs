// ============================================================================
// ProceduralOrganicDefinitions.cs
// ============================================================================
// PURPOSE:
//   Records the two-metre tiles owned by each refined room reservation.
//   Coarse cells remain available for gap and storey planning, while these tiles
//   define the actual shell and preserve the existing room as the collapse unit.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Carry room shape, ordered floor tiles and optional circular enclosure axes.
// DEPENDENCIES:
//   - Unity value types and standard collections only.
// USAGE NOTES:
//   Tile coordinates name bottom-left corners relative to the layout origin.
//   Hallways are ordinary graph rooms with their own stable room identity.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public enum ProceduralRoomShape { Rectangle, Hallway, LShape, TShape, Round }

    public readonly struct ProceduralOrganicRoom
    {
        public ProceduralOrganicRoom(int roomId, ProceduralRoomShape shape, IReadOnlyList<Vector2Int> tiles,
            Vector3 roundCenter = default, Vector3 roundFacing = default)
        { RoomId = roomId; Shape = shape; Tiles = tiles; RoundCenter = roundCenter; RoundFacing = roundFacing; }
        public int RoomId { get; }
        public ProceduralRoomShape Shape { get; }
        public IReadOnlyList<Vector2Int> Tiles { get; }
        public Vector3 RoundCenter { get; }
        public Vector3 RoundFacing { get; }
    }
}
