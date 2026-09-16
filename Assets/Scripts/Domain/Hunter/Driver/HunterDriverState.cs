// ============================================================================
// HunterDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores passive transient data for the owning Hunter engine boundary.
//   Handles, collections and movement or visual bookkeeping belong to one life.
//   The owning Driver initializes and clears this data during entity reuse.
// ARCHITECTURAL ROLE:
//   DriverState (section 7c) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
//   - Cache a bounded, physically verified corner-arc prediction until the next path refresh.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Passive data only; no simulation or engine operations.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterDriverState
    {
        public readonly HunterSteeringDriverState Steering = new HunterSteeringDriverState();
        public readonly HunterSteeringDriverState CornerPreview = new HunterSteeringDriverState();
        public readonly RaycastHit[] CornerCastHits = new RaycastHit[32];
        public readonly Collider[] CornerOverlaps = new Collider[16];
        public bool ClearCornerArc;
        public readonly List<Collider> Contacts = new List<Collider>();
        public readonly List<Bounds> UnavailableRooms = new List<Bounds>();
        public NavMeshPath Path;
        public Vector3 RepathEntry;
        public Vector3 RepathExit;
        public bool RepathActive;
        public bool RepathReverse;
        public Vector3 LastTarget;
        public float PathCooldown;
        public float VerticalSpeed;
        public bool PathAvailable;
    }
}
