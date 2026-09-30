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
//   - Retain the cosmetic hand look independently of hazard geometry and phase.
//   - Retain the room-owned native Lumen warning effect.
//   - Retain cell-local triggers and hand bounds without filling the room's bounding rectangle.
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
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
        public readonly List<BoxCollider> Boundaries = new List<BoxCollider>();
        public readonly List<Bounds> HandBounds = new List<Bounds>();
        public int RoomId;
        public RoomPhase Phase;
        public float Progress;
        public float Elapsed;
        public bool OptionalCracks;
        public string HandLook;
        public FloorLumenGlow Warning;

        public float BoundaryReach;
        public Func<Collider, EntityId> ResolveIdentity;
        public readonly Dictionary<Collider, EntityId> Contacts = new Dictionary<Collider, EntityId>();
        public readonly List<Transform> Hands = new List<Transform>();
        public readonly List<Vector3> HandPositions = new List<Vector3>();
        public readonly List<ParticleSystem> Mist = new List<ParticleSystem>();
        public readonly List<LineRenderer> Cracks = new List<LineRenderer>();
        public readonly List<Material> OwnedMaterials = new List<Material>();
        public readonly List<UnityEngine.Object> OwnedResources = new List<UnityEngine.Object>();
    }
}
