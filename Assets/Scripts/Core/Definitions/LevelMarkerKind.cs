// ============================================================================
// LevelMarkerKind.cs
// ============================================================================
// PURPOSE:
//   Names the authored records captured by the Level engine boundary. Keeping
//   this type in Core makes marker lifecycle facts safe event payloads.
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
    public enum LevelMarkerKind
    {
        Room, RoomLink, VaultSurface, SlideGate, ReboundSurface, OneWayDrop,
        LosBreak, CakeAnchor, HunterLink, ExitMarker
    }
}

