// ============================================================================
// MovementProbe.cs
// ============================================================================
//
// PURPOSE:
//   Carries the results of one movement physics probe into pure movement rules.
//   A Driver gathers these values before a tick so the Controller can make the
//   same decisions from a recorded probe without accessing a Unity scene.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared movement data.
//   This value crosses the Driver, Manager, and Controller boundaries as data.
//
// KEY RESPONSIBILITIES:
//   - Describe ground contact and the nearest relevant wall.
//   - Describe a checked vault candidate and its available clearance.
//   - Carry a stable wall identity without exposing a Collider or GameObject.
//
// DEPENDENCIES:
//   - UnityEngine.Vector3 value types only; no project-layer dependencies.
//
// USAGE NOTES:
//   A default probe reports no contacts. Distances are metres and angles are
//   degrees. WallId is a recorded probe identity; zero means no identified wall.
//   The Driver resolves marker components before setting VaultCandidate, so
//   this Core type never depends on a Domain marker enum.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public readonly struct MovementProbe
    {
        public MovementProbe(bool grounded, Vector3 groundNormal,
            bool wallDetected = false, float wallDistance = 0f,
            Vector3 wallNormal = default, float wallAngleDegrees = 0f,
            int wallId = 0, bool vaultCandidate = false,
            float vaultHeight = 0f, float vaultClearance = 0f,
            Vector3 vaultTarget = default)
        {
            Grounded = grounded;
            GroundNormal = groundNormal;
            WallDetected = wallDetected;
            WallDistance = wallDistance;
            WallNormal = wallNormal;
            WallAngleDegrees = wallAngleDegrees;
            WallId = wallId;
            VaultCandidate = vaultCandidate;
            VaultHeight = vaultHeight;
            VaultClearance = vaultClearance;
            VaultTarget = vaultTarget;
        }

        public bool Grounded { get; }
        public Vector3 GroundNormal { get; }
        public bool WallDetected { get; }
        public float WallDistance { get; }
        public Vector3 WallNormal { get; }
        public float WallAngleDegrees { get; }
        public int WallId { get; }
        public bool VaultCandidate { get; }
        public float VaultHeight { get; }
        public float VaultClearance { get; }
        public Vector3 VaultTarget { get; }
    }
}
