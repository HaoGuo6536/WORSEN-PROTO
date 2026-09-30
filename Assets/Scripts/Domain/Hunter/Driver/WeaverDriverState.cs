// ============================================================================
// WeaverDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores per-life physical web and ceiling-offset handles. Originals are kept
//   so teardown can restore the reused placeholder without changing its asset.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter shared swept-shot presentation stack.
// KEY RESPONSIBILITIES:
//   - Retain body/visual baselines and bounded projectile/nest records.
// DEPENDENCIES:
//   - UnityEngine passive references and parent-owned sweep values only.
// USAGE NOTES:
//   No engine operations or events; owned exclusively by WeaverWebDriver.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class WeaverDriverState
    {
        public CapsuleCollider Capsule;
        public Vector3 CapsuleCenter;
        public readonly List<Transform> Children = new List<Transform>();
        public readonly List<Vector3> ChildPositions = new List<Vector3>();
        public float Offset;
        public readonly List<WeaverWebDriverState> Webs = new List<WeaverWebDriverState>();
    }
    public sealed class WeaverWebDriverState
    {
        public int Serial;
        public Vector3 Position, Direction;
        public float Radius, Speed, Remaining;
        public bool Nest;
    }
}
