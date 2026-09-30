// ============================================================================
// FloorDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains owned generated objects and lifecycle data for symmetric scene teardown.
//   It also retains target-local guidance history so a failed refresh cannot lose
//   a good direction or borrow the direction of another candidate cake.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Retain generated cake visuals, trap contacts and the owned tick clip for teardown.
//   - Retain the owned native Lumen fallback-exit effect for lifecycle routing.
//   - Retain last good directions and fallback/held flags keyed by target anchor id.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor-owned sub-driver references and UnityEngine value types only.
// USAGE NOTES:
//   Passive data only. FloorDriver destroys owned objects and clears guidance at teardown.
//   Anchor id zero is the exit; flags describe each target's latest query.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public sealed class FloorDriverState
    {
        public GameObject Root;
        public readonly Dictionary<int, LevelAnchor> Anchors = new Dictionary<int, LevelAnchor>();
        public readonly List<CakePickup> Pickups = new List<CakePickup>();
        public readonly Dictionary<int, FloorCakeTrap> Traps = new Dictionary<int, FloorCakeTrap>();
        public readonly List<FloorCakeVisual> CakeVisuals = new List<FloorCakeVisual>();
        public AudioClip TrapTickClip;
        public readonly Dictionary<int, RoomCollapseVolume> Rooms = new Dictionary<int, RoomCollapseVolume>();
        public readonly List<Material> Materials = new List<Material>();
        public FloorExitVolume Exit;
        public FloorExitDoor ExitDoor;
        public FloorLumenGlow ExitGlow;
        public Material CakeMaterial;
        public Material GoldenMaterial;
        public Material BlockerMaterial;
        public Material ExitMaterial;
        public bool Ready;
        public bool Subscribed;
        public readonly Dictionary<int, Vector3> LastGoodDirections = new Dictionary<int, Vector3>();
        public readonly HashSet<int> FallbackDirections = new HashSet<int>();
        public readonly HashSet<int> HeldDirections = new HashSet<int>();
    }
}
