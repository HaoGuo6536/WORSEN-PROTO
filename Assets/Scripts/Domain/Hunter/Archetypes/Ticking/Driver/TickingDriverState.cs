// ============================================================================
// TickingDriverState.cs
// ============================================================================
// PURPOSE:
//   Holds the one world key owned by a Ticking driver and its contact serial.
//   Destruction and contact forwarding remain engine work in the driver.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Hunter Ticking.
// KEY RESPONSIBILITIES:
//   - Retain transient engine references without operating on them.
//   - Hold Driver-rented physics buffers and the configured obstacle mask.
// DEPENDENCIES:
//   - UnityEngine references as passive data only.
// USAGE NOTES:
//   Cleared on teardown; never shared between duplicates or stored on a config.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public sealed class TickingDriverState
    {
        public GameObject Key;
        public RaycastHit[] QueryHits;
        public Collider[] QueryOverlaps;
        public int ObstacleMask;
        public int Serial;
        public UnityEngine.AI.NavMeshPath Path;
    }
}
