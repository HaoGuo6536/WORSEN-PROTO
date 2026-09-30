// ============================================================================
// HunterObservationDefinitions.cs
// ============================================================================
// PURPOSE:
//   Adds optional view-dependent rules without changing existing archetype hooks.
//   Modules receive camera and room evidence and return motion/audio/loss policy.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Keep optional observation behavior additive for other roster workers.
// DEPENDENCIES:
//   - Core world and camera values only.
// USAGE NOTES:
//   Hold blocks every motion path, including lunges and recording replay.
// ============================================================================
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    public interface IHunterObservationRules
    {
        void Observe(HunterPlayerView view, bool clear, bool illuminated, IReadOnlyHunterWorldView world, bool wick);
        bool Hold { get; }
        bool Silent { get; }
        float SpeedMultiplier { get; }
        float LossMultiplier { get; }
    }
}
