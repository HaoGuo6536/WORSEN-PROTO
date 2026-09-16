// ============================================================================
// HunterDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains physical probe and path refresh bookkeeping for one hunter body.
//   Keeping this transient data separate makes teardown and reuse predictable.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Store the path cooldown, gravity velocity and deferred contact collection.
//   - Retain validation-only endpoints while crossing a navigation gap.
// DEPENDENCIES:
//   - UnityEngine references are passive data; no game system dependencies.
// USAGE NOTES:
//   Scene-owned, replaced by Initialize and cleared by Teardown.
//   Gap endpoints survive invalidation until the body leaves the crossing. They
//   never authorize movement without fresh local crossing and target-path validation.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterDriverState
    {
        public readonly HunterSteeringDriverState Steering = new HunterSteeringDriverState();
        public readonly List<Collider> Contacts = new List<Collider>();
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
