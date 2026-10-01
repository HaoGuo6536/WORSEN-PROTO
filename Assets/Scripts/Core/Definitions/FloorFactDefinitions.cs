// ============================================================================
// FloorFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries a committed closed-room contact entry across the Floor boundary.
//   The velocity is an impulse, not acceleration; continued contact produces no
//   additional fact until the same player leaves and re-enters that room's front.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Floor contact facts.
// KEY RESPONSIBILITIES:
//   - Preserve player, room, contact position, velocity and simulation tick.
// DEPENDENCIES:
//   Core entity identity and UnityEngine value types only.
// USAGE NOTES:
//   Run relays only during the current Floor tick. Horror routing delivers the
//   impulse to Player; grab slow, damage and accepted-hit throw remain separate.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public readonly struct FloorBoundaryImpulseFact
    {
        public FloorBoundaryImpulseFact(EntityId player, int room, Vector3 velocity, Vector3 position, long tick)
        { Player = player; Room = room; Velocity = velocity; Position = position; Tick = tick; }
        public EntityId Player { get; }
        public int Room { get; }
        public Vector3 Velocity { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
    }
}
