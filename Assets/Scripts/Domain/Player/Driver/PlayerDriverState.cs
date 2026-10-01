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
//   - Retain grace query filtering, original capsule exclusions and the session warning latch.
//   - Retain resolved poses, interpolation timing and posture.
//   - Hold Driver-rented query buffers across ticks until teardown.
//   - Retain admitted traversal geometry, fixed arc phases and a latched obstruction until reset.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Passive scene-owned data. Initialize resets both poses to the supplied spawn position.
//   A separate Driver-owned session instance holds only the missing-layer warning latch.
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
        public int HunterBodyLayer = -1;
        public bool GraceActive;
        public int OriginalExcludeLayers;
        public bool MissingHunterLayerWarned;
        public RaycastHit[] QueryHits;
        public Collider[] QueryOverlaps;
        public Collider ProbedTraversalCollider, TraversalCollider, IgnoredTraversalCollider;
        public Vector3 ProbedTraversalTarget;
        public float TraversalRisePortion, TraversalTraverseEnd;
        public bool TraversalActive, TraversalObstructed;
    }
}