// ============================================================================
// FogDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores the field snapshot and the Driver's transient engine resources.
//   Room voxel buckets let progress updates avoid scanning the whole floor.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Retain accepted progress, portal links, dirty rooms and upload telemetry.
// DEPENDENCIES:
//   - Core room samples; UnityEngine values and passive texture references.
// USAGE NOTES:
//   Scene-owned through FogDriver. Data only; ResetFloor replaces this snapshot.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Fog
{
    public sealed class FogDriverState
    {
        public readonly Dictionary<int, FogRoomDriverState> Rooms = new Dictionary<int, FogRoomDriverState>();
        public readonly HashSet<int> DirtyRooms = new HashSet<int>();
        public Bounds Bounds;
        public Vector3Int Size;
        public Vector3 Voxel;
        public byte[] Density = Array.Empty<byte>();
        public int UnmatchedEdges;
        public int LastRebuiltRooms;
        public bool UploadPending;
        public bool Enabled = true;
        public Texture3D Texture;
        public double LastUploadMilliseconds;
        public int UploadRevision;
    }

    public sealed class FogRoomDriverState
    {
        public GeneratedRoomSample Sample;
        public float Progress;
        public readonly List<int> Voxels = new List<int>();
        public readonly List<FogPortalDriverState> Portals = new List<FogPortalDriverState>();
    }

    public sealed class FogPortalDriverState
    {
        public int Neighbor;
        public Vector3 Center;
        public int Axis;
        public int Side;
    }
}
