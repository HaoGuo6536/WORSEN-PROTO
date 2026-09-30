// ============================================================================
// ProceduralPassageDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the lifetime of one single-use Passage crossing independently of other
//   crossings on the floor. Injected elapsed time and the next tile index make
//   collapse order reproducible even when a frame spans several tile deadlines.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Hold the immutable plan, elapsed seconds and owned tile object references.
// DEPENDENCIES:
//   - Own plan and passive UnityEngine references only.
// USAGE NOTES:
//   No engine operations or event publication; the owning Driver applies transitions.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralPassageDriverState
    {
        public ProceduralPassagePlan Plan;
        public double Elapsed;
        public int CollapsedCount;
        public readonly List<GameObject> Tiles = new List<GameObject>();
    }
}
