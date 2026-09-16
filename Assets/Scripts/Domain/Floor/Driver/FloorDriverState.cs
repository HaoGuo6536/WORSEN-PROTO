// ============================================================================
// FloorDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains owned generated objects and lifecycle data for symmetric scene teardown.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Passive engine references only. FloorDriver destroys every owned object and material before clearing them.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public sealed class FloorDriverState
    {
        public GameObject Root;
        public readonly List<CakePickup> Pickups = new List<CakePickup>();
        public readonly Dictionary<int, RoomCollapseVolume> Rooms = new Dictionary<int, RoomCollapseVolume>();
        public readonly List<Material> Materials = new List<Material>();
        public FloorExitVolume Exit;
        public Material CakeMaterial;
        public Material GoldenMaterial;
        public Material BlockerMaterial;
        public Material ExitMaterial;
        public bool Ready;
        public bool Subscribed;
    }
}