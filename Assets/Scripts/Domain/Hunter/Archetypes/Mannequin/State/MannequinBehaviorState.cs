// ============================================================================
// MannequinBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Keeps one Mannequin's observation gate and temporary light failure clock.
//   Broken-room memory is floor-local and never leaks into shared config assets.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Retain injected evidence, seeded-check timing, catch admission and light facts.
// DEPENDENCIES:
//   - Core observations and System collections only.
// USAGE NOTES:
//   Reset on spawn/floor reset; only MannequinController mutates this state.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    public sealed class MannequinBehaviorState
    {
        internal HunterPlayerView View;
        internal IReadOnlyHunterWorldView World;
        internal bool Clear, Illuminated, Wick, Hold = true;
        internal bool CatchPublished;
        internal float CheckRemaining, FailureRemaining, Speed = 1f;
        internal int Room, FailureRoom, LampStacks = -1;
        internal long LastTick = -1;
        internal readonly HashSet<int> BrokenRooms = new HashSet<int>();
        internal readonly Queue<MannequinFact> Facts = new Queue<MannequinFact>();
    }
}
