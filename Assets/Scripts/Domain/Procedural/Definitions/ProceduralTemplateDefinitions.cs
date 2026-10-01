// ============================================================================
// ProceduralTemplateDefinitions.cs
// ============================================================================
// PURPOSE:
//   Stores imported room catalogues independently of editor JSON and scene objects.
//   Placed rooms retain their authored identity, sockets and transform so generation
//   and rendering consume the same source rather than inventing a second shell.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Carry serializable theme, kit, room, anchor and piece data.
//   - Record placed template origins and the door sockets actually opened.
//   - Preserve explicit traversal kinds, collision and room-local paired endpoints.
//   - Retain furnished reservations, singular shrine sockets and authored puzzle lanes.
// DEPENDENCIES:
//   - Unity value types and System serialization only.
// USAGE NOTES:
//   Public fields are import DTOs, not tunables or runtime ScriptableObject setters.
//   Coordinates use south-west cell corners; Module is the fixed two-metre contract.
//   Span counts boundary cells, not the frame aperture. SubcellOffset is in metres
//   so odd-centred room sockets can mate with even-centred hallway sockets.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    [Serializable]
    public sealed class ProceduralTemplateCatalogue
    {
        public string Theme;
        public float Module = 2f;
        public float WallHeight;
        public ProceduralKitPiece[] Kit = Array.Empty<ProceduralKitPiece>();
        public ProceduralRoomTemplate[] Templates = Array.Empty<ProceduralRoomTemplate>();
    }

    [Serializable]
    public sealed class ProceduralKitPiece
    {
        public string Id, File, Kind;
        public Vector3 Size;
        public TraversalSurfaceKind TraversalKind;
        public bool Collision = true;
    }

    [Serializable]
    public sealed class ProceduralRoomTemplate
    {
        public string Id, Kind, SizeClass, Shape, Gimmick;
        public float Height, Weight;
        public int MinRound;
        public int FurnishingVersion;
        public ProceduralShrineSocket[] ShrineSockets = Array.Empty<ProceduralShrineSocket>();
        public ProceduralPuzzleSocket PuzzleSockets;
        public ProceduralTemplateGap PassageGap;
        public ProceduralTemplateReservation[] ReservedAreas = Array.Empty<ProceduralTemplateReservation>();
        public ProceduralTemplateTransition Transition;
        public Vector2Int[] Footprint = Array.Empty<Vector2Int>();
        public ProceduralTemplateDoor[] Doors = Array.Empty<ProceduralTemplateDoor>();
        public Vector3[] Cake = Array.Empty<Vector3>(), GoldenCake = Array.Empty<Vector3>(),
            Light = Array.Empty<Vector3>(), HunterSpawn = Array.Empty<Vector3>();
        public ProceduralTemplatePiece[] Pieces = Array.Empty<ProceduralTemplatePiece>();
    }

    [Serializable]
    public sealed class ProceduralTemplateDoor
    {
        public Vector2Int Cell;
        public string Side;
        public int Span = 1;
        public ProceduralTemplatePiece[] ClosedWith = Array.Empty<ProceduralTemplatePiece>();
    }

    [Serializable]
    public sealed class ProceduralTemplatePiece
    {
        public string Id;
        public Vector3 Position;
        public float RotY;
        public TraversalSurfaceKind TraversalKind;
        public bool Collision = true;
        public bool HasEndpoints;
        public Vector3 EndpointA, EndpointB;
    }

    [Serializable]
    public sealed class ProceduralShrineSocket
    {
        public string Id;
        public Vector3 Position, Facing, ModelEnvelope, InteractionCenter, InteractionSize;
    }

    [Serializable]
    public sealed class ProceduralPuzzleSocket
    {
        public Vector3 Origin, Axis, Reward;
        public float LaneLength, LaneWidth, CageHeight, PanelThickness, Clearance;
        public Vector3[] Steps = Array.Empty<Vector3>();
        public string[] SupportedKinds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ProceduralTemplateGap
    {
        public string Side, PocketTemplateId;
        public Vector2Int EdgeCell, PocketOffset;
        public Vector3 Edge, Landing;
        public float Width, GapLength;
        public int PocketTurns;
        public bool SealedUntilActivated, OptionalOnly;
    }

    [Serializable]
    public sealed class ProceduralTemplateReservation
    {
        public string Id;
        public Vector3 Center, Size;
    }

    [Serializable]
    public sealed class ProceduralTemplateTransition
    {
        public string Purpose;
        public string[] CompatibleThemes = Array.Empty<string>();
        public ProceduralTransitionDoor[] Doors = Array.Empty<ProceduralTransitionDoor>();
    }

    [Serializable]
    public sealed class ProceduralTransitionDoor
    {
        public int Index;
        public float ClearWidth, ClearHeight, FloorY;
        public string Seam;
    }

    public sealed class ProceduralTemplateRoom
    {
        public int RoomId;
        public int PocketId;
        public ProceduralRoomTemplate Template;
        public ProceduralTemplateCatalogue Catalogue;
        public ProceduralThemeData Theme;
        public ProceduralTemplateRoom PassagePocket;
        public Vector2Int Offset;
        public Vector2Int SubcellOffset;
        public int Turns;
        public int MaximumEntrances = int.MaxValue;
        public int[] OpenDoors = Array.Empty<int>();
    }
}
