// ============================================================================
// HeraldBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Keeps Herald attack timing and alternating chase calls per spawned entity.
//   Queued immutable facts are drained by HunterManager, not dispatched by state.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Retain injected context, once-per-tick guards and pending facts.
// DEPENDENCIES:
//   - Hunter context and Core Herald values only.
// USAGE NOTES:
//   No engine operations. Reset clears pending hits before a reused life starts.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Herald
{
    public sealed class HeraldBehaviorState
    {
        public HunterArchetypeContext Context;
        public long LastTick = -1;
        public bool WasVisible, Warning, ChaseTwo;
        public float WarningRemaining, Cooldown, ChaseRemaining;
        public readonly Queue<HeraldScreamFact> Screams = new Queue<HeraldScreamFact>();
        public readonly Queue<HeraldBreathFact> Breaths = new Queue<HeraldBreathFact>();
        public readonly Queue<HeraldDeafenFact> Hits = new Queue<HeraldDeafenFact>();
    }
}
