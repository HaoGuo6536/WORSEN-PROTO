// ============================================================================
// ProceduralDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries generated room cells, door openings and shell blocks between the
//   procedural logic and engine boundary. Shared consumers receive only the
//   existing immutable Core LevelGraph and spawn value types from the Manager.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Describe one reproducible layout and its physical construction commands.
// DEPENDENCIES:
//   - Core LevelGraph and UnityEngine value types only.
// USAGE NOTES:
//   Data only; these system-local snapshots contain no engine object references.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public readonly struct ProceduralDoorPlan
    {
        public ProceduralDoorPlan(int fromRoomId, int toRoomId, Vector3 center, bool alongX)
        { FromRoomId = fromRoomId; ToRoomId = toRoomId; Center = center; AlongX = alongX; }
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
        public IReadOnlyList<ProceduralDoorPlan> Doors { get; internal set; }
        public IReadOnlyList<ProceduralRoomModule> Modules { get; internal set; }
        public Vector3 PlayerSpawnPosition { get; internal set; }
        public Quaternion PlayerSpawnRotation { get; internal set; }
        public IReadOnlyList<Vector3> HunterSpawnPositions { get; internal set; }
        public string Manifest { get; internal set; }
    }

    public enum ProceduralSurfaceKind { Floor, Wall, Ceiling }
    public enum ProceduralModuleKind { VaultPartition, WindowPartition, SlidePartition }

    public readonly struct ProceduralRoomModule
    {
        public ProceduralRoomModule(int roomId, ProceduralModuleKind kind, bool alongX)
        { RoomId = roomId; Kind = kind; AlongX = alongX; }
        public int RoomId { get; }
        public ProceduralModuleKind Kind { get; }
        public bool AlongX { get; }
    }

    public readonly struct ProceduralBlock
    {
        public ProceduralBlock(int roomId, ProceduralSurfaceKind kind, Vector3 center, Vector3 size,
            int surfaceId = 0, TraversalSurfaceKind traversalKind = TraversalSurfaceKind.None,
            Vector3 endpointA = default, Vector3 endpointB = default)
        { RoomId = roomId; Kind = kind; Center = center; Size = size; SurfaceId = surfaceId;
            TraversalKind = traversalKind; EndpointA = endpointA; EndpointB = endpointB; }
        public int RoomId { get; }
        public ProceduralSurfaceKind Kind { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public int SurfaceId { get; }
        public TraversalSurfaceKind TraversalKind { get; }
        public Vector3 EndpointA { get; }
        public Vector3 EndpointB { get; }
    }
}
