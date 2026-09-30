// ============================================================================
// FloorHandDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries trigger-backed boundary distance, outward direction and penetration without engine identities.
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
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public enum FloorHandPhase { Idle, Warning, Grabbed, Cooldown }
    public readonly struct FloorHandProbe
    {
        public FloorHandProbe(int roomId, int handId, Vector3 position, float distance, bool available,
            Vector3 outward = default, float penetration = 0f, bool closed = false, Vector3? playerPosition = null)
        { RoomId = roomId; HandId = handId; Position = position; Distance = distance; Available = available;
          Outward = outward; Penetration = penetration; Closed = closed; PlayerPosition = playerPosition; }
        public int RoomId { get; }
        public int HandId { get; }
        public Vector3 Position { get; }
        public float Distance { get; }
        public bool Available { get; }
        public Vector3 Outward { get; }
        public float Penetration { get; }
        public bool Closed { get; }
        public Vector3? PlayerPosition { get; }
    }

    public readonly struct FloorCakeLoss
    {
        public FloorCakeLoss(int anchorId, int roomId, PickupKind kind, long tick)
        { AnchorId = anchorId; RoomId = roomId; Kind = kind; Tick = tick; }
        public int AnchorId { get; }
        public int RoomId { get; }
        public PickupKind Kind { get; }
        public long Tick { get; }
    }
}
