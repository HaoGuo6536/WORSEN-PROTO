// ============================================================================
// HorrorNoiseFact.cs
// ============================================================================
// PURPOSE:
//   Retains explicit producer provenance beside a Core acoustic observation.
//   This additive local payload bridges the missing Core origin contract.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Carry noise and its gameplay/world origin without engine work or routing.
// DEPENDENCIES:
//   Core NoiseEvent only.
// USAGE NOTES:
//   Local to Session. WP-I migrates this origin into Core before cross-layer delivery.
// ============================================================================
using Worsen.Core;
namespace Worsen.Session.HorrorEffects
{
    public enum HorrorNoiseOrigin { World, Firecracker, PlayerTriggeredCakeTrap, Pacification }
    public readonly struct HorrorNoiseFact
    {
        public HorrorNoiseFact(NoiseEvent noise, HorrorNoiseOrigin origin)
        { Noise = noise; Origin = origin; }
        public NoiseEvent Noise { get; }
        public HorrorNoiseOrigin Origin { get; }
    }
}
