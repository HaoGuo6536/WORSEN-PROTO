// ============================================================================
// HunterSteeringDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains one Hunter's physical steering values between explicit movement ticks.
//   Keeping position, path progress and committed lunge travel here lets a pure
//   Presenter compute motion while the Driver remains the engine boundary.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Store movement pose, velocity, path corners and active corner index.
//   - Store the committed lunge direction and consumed travel budget.
// DEPENDENCIES:
//   - UnityEngine Vector3 value data only; no other project systems.
// USAGE NOTES:
//   Per-instance state; HunterSteeringPresenter.Reset clears every retained value.
//   Driver collision resolution may reconcile Position after applying a displacement.
//   LungeDistanceTravelled counts requested travel so a collision cannot extend a lunge.
//   AlignAfterCorner survives path refreshes until heading aligns with the new leg.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Domain.Hunter
{
    public sealed class HunterSteeringDriverState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Forward = Vector3.forward;
        public Vector3[] Corners = Array.Empty<Vector3>();
        public int CornerIndex;
        public bool AlignAfterCorner;
        public Vector3 LungeDirection;
        public float LungeDistanceTravelled;
        public bool LungeWasActive;
    }
}
