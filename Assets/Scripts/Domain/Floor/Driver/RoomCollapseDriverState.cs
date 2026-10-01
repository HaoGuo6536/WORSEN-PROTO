// ============================================================================
// RoomCollapseDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains a fixed room-local visual pool, boundary trigger contacts and stage playback state.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Retain cosmetic hand look, player observations and grip independently of hazards.
//   - Retain the room-owned native Lumen warning effect.
//   - Retain cell-local triggers, advancing fog meshes and inward-only Closed seals.
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Hold pooled query buffers until the owning volume is destroyed.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System.Collections.Generic;
using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class RoomCollapseDriverState
    {
        public Bounds Bounds;
        public LevelRoom Room;
        public RaycastHit[] QueryHits;
        public Collider[] QueryOverlaps;
        public readonly List<BoxCollider> Boundaries = new List<BoxCollider>();
        public readonly List<BoxCollider> ClosedWalls = new List<BoxCollider>();
        public readonly List<Transform> FogCells = new List<Transform>();
        public Material FogMaterial;
        public Vector3 FrontDirection = Vector3.right;
        public float Consumption;
        public float FrontExponent = 1f;
        public readonly List<Bounds> HandBounds = new List<Bounds>();
        public int RoomId;
        public RoomPhase Phase;
        public float Progress;
        public float Elapsed;
        public bool OptionalCracks;
        public string HandLook;
        public Vector3? PlayerTarget;
        public float GripWeight;
        public readonly List<SkinnedMeshRenderer[]> HandSkins = new List<SkinnedMeshRenderer[]>();
        public FloorLumenGlow Warning;

        public float BoundaryReach;
        public Func<Collider, EntityId> ResolveIdentity;
        public readonly Dictionary<Collider, EntityId> Contacts = new Dictionary<Collider, EntityId>();
        public readonly List<Transform> Hands = new List<Transform>();
        public readonly List<Vector3> HandPositions = new List<Vector3>();

        public readonly List<LineRenderer> Cracks = new List<LineRenderer>();

    }
}
