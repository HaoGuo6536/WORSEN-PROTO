// ============================================================================
// BlinderProjectileDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains one cosmetic Blinder flight independently of gameplay collision.
//   Hunter identity and serial distinguish simultaneous throws from duplicate hunters.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Store the launch fact, traveled distance and current/trailing positions.
// DEPENDENCIES:
//   Core Blinder facts and Unity value types only.
// USAGE NOTES:
//   Scene-owned by BlinderProjectileDriver; no engine object or authoritative hit state.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public sealed class BlinderProjectileDriverState
    {
        public BlinderThrowFact Fact;
        public Vector3 Position, PreviousPosition;
        public float Traveled;
    }
}
