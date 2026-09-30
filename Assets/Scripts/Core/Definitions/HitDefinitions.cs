// ============================================================================
// HitDefinitions.cs
// ============================================================================
// PURPOSE:
//   Separates hit recovery classification from damage and chase bookkeeping.
//   Player can later publish grace intervals without exposing mutable health state.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Hit contracts.
// KEY RESPONSIBILITIES:
//   - Name hit severity, source and death cause; describe a committed grace window.
// DEPENDENCIES:
//   - Core EntityId and System only.
// USAGE NOTES:
//   Grace covers [StartTick, EndTick); equal endpoints mean no grace. Player owns
//   duration, validation of the player identity and publication, not this payload.
// ============================================================================
using System;

namespace Worsen.Core
{
    /// <summary>The recovery class of an accepted hit, independent of damage amount.</summary>
    public enum HitSeverity { Light, Heavy }
    /// <summary>The physical or environmental origin of an accepted hit.</summary>
    public enum HitSource { Lunge, Hand, Projectile, Scream, Trap, Other }
    /// <summary>The reported cause of a run-ending death; None also means not reported.</summary>
    public enum DeathCause { None, Hunter, Hand, Trap, Other }

    /// <summary>A Player-owned committed grace interval measured in run ticks.</summary>
    public readonly struct GraceWindowFact
    {
        public GraceWindowFact(EntityId playerId, long startTick, long endTick, HitSeverity severity)
        {
            if (startTick < 0) throw new ArgumentOutOfRangeException(nameof(startTick));
            if (endTick < startTick) throw new ArgumentOutOfRangeException(nameof(endTick));
            PlayerId = playerId; StartTick = startTick; EndTick = endTick; Severity = severity;
        }
        public EntityId PlayerId { get; }
        public long StartTick { get; }
        public long EndTick { get; }
        public HitSeverity Severity { get; }
    }
}
