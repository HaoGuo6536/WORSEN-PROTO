// ============================================================================
// PlayerDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains resolved physics poses and interpolation timing for one Player Driver.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Passive scene-owned data. Initialize resets both poses to the supplied spawn position.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Player
{
    public sealed class PlayerDriverState
    {
        public Vector3 PreviousPosition;
        public Vector3 Position;
        public Vector3 Velocity;
        public float PreviousHeading;
        public float Heading;
        public float Height;
        public float LastStepTime;
        public float LastStepDuration;
        public bool Grounded;
        public bool Ready;
    }
}