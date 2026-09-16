// ============================================================================
// FloorHandDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries sampled hand proximity and obstruction results without engine identities.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public enum FloorHandPhase { Idle, Warning, Grabbed, Cooldown }
    public readonly struct FloorHandProbe
    {
        public FloorHandProbe(int roomId, int handId, Vector3 position, float distance, bool available)
        { RoomId = roomId; HandId = handId; Position = position; Distance = distance; Available = available; }
        public int RoomId { get; }
        public int HandId { get; }
        public Vector3 Position { get; }
        public float Distance { get; }
        public bool Available { get; }
    }
}
