// ============================================================================
// FloorExitDoorDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains passive opening progress and per-collider doorway crossing observations.
//   The doorway distinguishes collecting the last cake from deliberately leaving.
//   Explicit timing and crossing observations keep the transition reproducible.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Retain the native Lumen threshold glow alongside moving door references.
//   - Keep visible door movement and physical passage in agreement.
//   - Prevent a stationary overlap from becoming an accidental floor transition.
// DEPENDENCIES:
//   - Core shared values and Floor-owned visual configuration only.
// USAGE NOTES:
//   Scene-owned through FloorDriver. Session supplies elapsed time; no Update loop.
//   No global settings. Reinitialization clears crossing and opening state.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Floor
{
    public sealed class FloorExitDoorDriverState
    {
        public bool Opening;
        public bool FullyOpen;
        public float Elapsed;
        public float LastClock;
        public Transform LeftHinge;
        public Transform RightHinge;
        public BoxCollider Threshold;
        public FloorLumenGlow Glow;
        public readonly Dictionary<int, FloorExitCrossingDriverState> Contacts = new Dictionary<int, FloorExitCrossingDriverState>();
    }
    public sealed class FloorExitCrossingDriverState
    {
        public int EntrySide;
        public bool Completed;
    }
}
