// ============================================================================
// HunterStallDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains a sliding history of measured navigation progress for one hunter life.
//   Observation state is separate from steering and can never command movement.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Store elapsed intervals, signed progress and the episode publication latch.
// DEPENDENCIES:
//   - System collections and UnityEngine value data only.
// USAGE NOTES:
//   Passive, scene-owned data; recreated with HunterDriverState on initialization.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterStallDriverState
    {
        internal readonly LinkedList<(double Seconds, double Progress)> Samples = new LinkedList<(double, double)>();
        internal double Elapsed;
        internal double WindowSeconds;
        internal double WindowProgress;
        internal bool Reported;
        internal Vector3 PreviousPosition;
        internal Vector3[] PreviousCorners;
    }
}
