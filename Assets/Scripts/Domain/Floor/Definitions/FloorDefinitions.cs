// ============================================================================
// FloorDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries internal path candidates and scheduled transitions between Floor components.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   System-local immutable records. Public cross-system facts live in Core.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public readonly struct FloorPathCandidate
    {
        public FloorPathCandidate(int anchorId, float length, Vector3 direction)
        { AnchorId = anchorId; Length = length; Direction = direction; }
        public int AnchorId { get; }
        public float Length { get; }
        public Vector3 Direction { get; }
    }

    public readonly struct FloorScheduledTransition
    {
        public FloorScheduledTransition(int roomId, RoomPhase phase, double at)
        { RoomId = roomId; Phase = phase; At = at; }
        public int RoomId { get; }
        public RoomPhase Phase { get; }
        public double At { get; }
    }
}