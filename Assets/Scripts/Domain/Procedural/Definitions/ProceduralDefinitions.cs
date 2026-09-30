// ============================================================================
// ProceduralDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries generated room cells, traversal apertures and castle shell blocks between the
//   procedural logic and engine boundary. Shared consumers receive only the
//   existing immutable Core LevelGraph and spawn value types from the Manager.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Describe one reproducible layout and its physical construction commands.
//   - Separate stepped visuals from rotated ramp and flat landing collision boxes.
//   - Retain the effective spawn policy and immutable world-object construction plans.
//   - Record exact occupied cells, optional pocket anchors and Passage placement sites.
//   - Retain storeys, directed vertical routes and permissioned navigation link plans.
// DEPENDENCIES:
//   - Core LevelGraph and UnityEngine value types only.
// USAGE NOTES:
//   Data only; these system-local snapshots contain no engine object references.
//   Block Size is local to Rotation. Ordinary blocks default to visible solid boxes;
//   stair ramp endpoints describe the centerline of the walkable top face.
//   CollisionOnly seals a view window without creating a traversable aperture.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public readonly struct ProceduralDoorPlan
    {
        public ProceduralDoorPlan(int fromRoomId, int toRoomId, Vector3 center, bool alongX,
            TraversalSurfaceKind traversalKind = TraversalSurfaceKind.None)
        { FromRoomId = fromRoomId; ToRoomId = toRoomId; Center = center; AlongX = alongX; TraversalKind = traversalKind; }
        public TraversalSurfaceKind TraversalKind { get; }
        public bool IsOptional => TraversalKind != TraversalSurfaceKind.None;
        public int FromRoomId { get; }
        public int ToRoomId { get; }
        public Vector3 Center { get; }
        public bool AlongX { get; }
    }

    public sealed class ProceduralLayout
    {
        public int Seed { get; internal set; }
        public int RoundIndex { get; internal set; }
        public LevelGraph Graph { get; internal set; }
        public IReadOnlyList<Vector2Int> Cells { get; internal set; }
        public float CellSize { get; internal set; }
        public Vector2 Origin { get; internal set; }
        public IReadOnlyList<Vector2Int> GapCells { get; internal set; } = System.Array.Empty<Vector2Int>();
        public IReadOnlyList<LevelAnchor> PocketAnchors { get; internal set; } = System.Array.Empty<LevelAnchor>();
        public IReadOnlyList<ProceduralGapSite> GapSites { get; internal set; } = System.Array.Empty<ProceduralGapSite>();
        public IReadOnlyList<ProceduralStoreyPlan> Storeys { get; internal set; } = System.Array.Empty<ProceduralStoreyPlan>();
        public IReadOnlyList<ProceduralVerticalRoute> VerticalRoutes { get; internal set; } = System.Array.Empty<ProceduralVerticalRoute>();
        public IReadOnlyList<ProceduralDoorPlan> Doors { get; internal set; }
        public IReadOnlyList<ProceduralRoomModule> Modules { get; internal set; }
        public Vector3 PlayerSpawnPosition { get; internal set; }
        public Quaternion PlayerSpawnRotation { get; internal set; }
        public IReadOnlyList<Vector3> HunterSpawnPositions { get; internal set; }
        public int MinimumHunterSpawnRooms { get; internal set; }
        public string SpawnValidationReport { get; internal set; }
        public IReadOnlyList<ProceduralInteractablePlan> Interactables { get; internal set; }
            = System.Array.Empty<ProceduralInteractablePlan>();
        public string Manifest { get; internal set; }
        public string InteractableManifest { get; internal set; } = string.Empty;
        public IReadOnlyList<GeneratedRoomSample> PresentationRooms { get; internal set; }
    }

    public enum ProceduralSurfaceKind { Floor, Wall, Ceiling }
    public readonly struct ProceduralInteractablePlan
    {
        public ProceduralInteractablePlan(InteractableState state, Vector3 size, int surfaceId = 0)
        { State = state; Size = size; SurfaceId = surfaceId; }
        public InteractableState State { get; }
        public Vector3 Size { get; }
        public int SurfaceId { get; }
    }

    public enum ProceduralBlockRole { Solid, VisualOnly, StairRamp, StairLanding, CollisionOnly, PlayerOnly }
    public enum ProceduralVerticalKind { Ramp, LedgeClimb, ReboundClimb, FloorHole, Shaft, Balcony, CollapsedRamp }

    public readonly struct ProceduralStoreyPlan
    {
        public ProceduralStoreyPlan(int roomId, Vector3 origin, float height, ProceduralVerticalKind drop)
        { RoomId = roomId; Origin = origin; Height = height; Drop = drop; }
        public int RoomId { get; }
        public int UpperRegionId => 10000 + RoomId;
        public Vector3 Origin { get; }
        public float Height { get; }
        public ProceduralVerticalKind Drop { get; }
    }

    public readonly struct ProceduralVerticalRoute
    {
        public ProceduralVerticalRoute(int id, int roomId, ProceduralVerticalKind kind, int fromRegion, int toRegion,
            bool bidirectional, TraversalAccess access, IReadOnlyList<Vector3> points)
        { Id = id; RoomId = roomId; Kind = kind; FromRegion = fromRegion; ToRegion = toRegion;
            Bidirectional = bidirectional; Access = access; Points = points; }
        public int Id { get; }
        public int RoomId { get; }
        public ProceduralVerticalKind Kind { get; }
        public int FromRegion { get; }
        public int ToRegion { get; }
        public bool Bidirectional { get; }
        public TraversalAccess Access { get; }
        public IReadOnlyList<Vector3> Points { get; }
    }

    public readonly struct ProceduralNavigationLink
    {
        public ProceduralNavigationLink(Vector3 start, Vector3 end, int area)
        { Start = start; End = end; Area = area; }
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public int Area { get; }
    }
    public enum ProceduralModuleKind
    {
        VaultPartition, WindowPartition, SlidePartition,
        TorchGallery, OpenStairHall, SplitLevelLibrary, BrokenCloister, BrokenGallery, MerchantRefuge, ExitHub
    }

    public readonly struct ProceduralRoomModule
    {
        public ProceduralRoomModule(int roomId, ProceduralModuleKind kind, bool alongX,
            IReadOnlyList<Vector2Int> cells = null, int pocketId = 0)
        { RoomId = roomId; Kind = kind; AlongX = alongX; Cells = cells; PocketId = pocketId; }
        public int RoomId { get; }
        public ProceduralModuleKind Kind { get; }
        public bool AlongX { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
        public int PocketId { get; }
    }

    public readonly struct ProceduralGapSite
    {
        public ProceduralGapSite(int roomId, int pocketId, Vector3 edge, Vector3 landing)
        { RoomId = roomId; PocketId = pocketId; Edge = edge; Landing = landing; }
        public int RoomId { get; }
        public int PocketId { get; }
        public Vector3 Edge { get; }
        public Vector3 Landing { get; }
    }

    public readonly struct ProceduralBlock
    {
        public ProceduralBlock(int roomId, ProceduralSurfaceKind kind, Vector3 center, Vector3 size,
            int surfaceId = 0, TraversalSurfaceKind traversalKind = TraversalSurfaceKind.None,
            Vector3 endpointA = default, Vector3 endpointB = default,
            ProceduralBlockRole role = ProceduralBlockRole.Solid, Quaternion? rotation = null)
        { RoomId = roomId; Kind = kind; Center = center; Size = size; SurfaceId = surfaceId;
            TraversalKind = traversalKind; EndpointA = endpointA; EndpointB = endpointB;
            Role = role; Rotation = rotation ?? Quaternion.identity; }
        public int RoomId { get; }
        public ProceduralSurfaceKind Kind { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public ProceduralBlockRole Role { get; }
        public Quaternion Rotation { get; }
        public bool HasCollision => Role != ProceduralBlockRole.VisualOnly;
        public bool HasRenderer => Role == ProceduralBlockRole.Solid || Role == ProceduralBlockRole.VisualOnly || Role == ProceduralBlockRole.PlayerOnly;
        public int SurfaceId { get; }
        public TraversalSurfaceKind TraversalKind { get; }
        public Vector3 EndpointA { get; }
        public Vector3 EndpointB { get; }
    }
}
