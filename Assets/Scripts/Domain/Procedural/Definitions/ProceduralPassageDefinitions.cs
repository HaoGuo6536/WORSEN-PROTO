// ============================================================================
// ProceduralPassageDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes a deterministic temporary crossing and the shell pieces it replaces.
//   The plan is internal to Procedural; external facts use only primitive site and
//   room identities, tile positions and existing Core anchors.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Retain ordered tile boxes, aperture substitutions and lined pocket anchors.
// DEPENDENCIES:
//   - Core LevelAnchor and UnityEngine value types only.
// USAGE NOTES:
//   Data only. Tile centers include thickness; published positions are top-face centers.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public readonly struct ProceduralPassageWall
    {
        public ProceduralPassageWall(ProceduralBlock original, IReadOnlyList<ProceduralBlock> pieces)
        { Original = original; Pieces = pieces; }
        public ProceduralBlock Original { get; }
        public IReadOnlyList<ProceduralBlock> Pieces { get; }
    }

    public sealed class ProceduralPassagePlan
    {
        public ProceduralPassagePlan(int siteIndex, int pocketRoomId, Vector3 start, Vector3 end,
            IReadOnlyList<ProceduralBlock> tiles, IReadOnlyList<Vector3> positions, IReadOnlyList<ProceduralPassageWall> walls, IReadOnlyList<LevelAnchor> anchors)
        { SiteIndex = siteIndex; PocketRoomId = pocketRoomId; Start = start; End = end;
            Tiles = tiles; TilePositions = positions; Walls = walls; LinedAnchors = anchors; }
        public int SiteIndex { get; }
        public int PocketRoomId { get; }
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public IReadOnlyList<ProceduralBlock> Tiles { get; }
        public IReadOnlyList<Vector3> TilePositions { get; }
        public IReadOnlyList<ProceduralPassageWall> Walls { get; }
        public IReadOnlyList<LevelAnchor> LinedAnchors { get; }
    }
}
