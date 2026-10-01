// ============================================================================
// MannequinBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Keeps one Mannequin's camera gate, shrine override and catch admission.
//   Per-life facts and curse scaling never leak into shared configuration assets.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Retain injected observation, tick identity, catch admission and silence facts.
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
        internal bool Clear, Wick, Hold = true;
        internal bool CatchPublished;
        internal float Speed = 1f;
        internal long LastTick = -1;
        internal readonly Queue<MannequinFact> Facts = new Queue<MannequinFact>();
    }
}
