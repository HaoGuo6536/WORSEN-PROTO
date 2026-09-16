// ============================================================================
// ChaseDefinitions.cs
// ============================================================================
// PURPOSE:
//   Returns pursuit changes and proximity as immutable controller results. The Manager translates these facts into the public event stream.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Chase.
// KEY RESPONSIBILITIES:
//   - Describe owned state and expose only read access across system boundaries.
// DEPENDENCIES:
//   - Core shared facts and the owning Chase system only.
// USAGE NOTES:
//   Scene-owned state; no event publication, engine calls, or independent simulation loop.
// ============================================================================
using Worsen.Core;
namespace Worsen.Domain.Chase
{
    public readonly struct ChaseTickResult
    {
        public ChaseTickResult(bool started, bool lost, bool ended, bool phaseChanged, ChaseFact fact, ProximitySample proximity)
        { Started = started; Lost = lost; Ended = ended; PhaseChanged = phaseChanged; Fact = fact; Proximity = proximity; }
        public bool Started { get; }
        public bool Lost { get; }
        public bool Ended { get; }
        public bool PhaseChanged { get; }
        public ChaseFact Fact { get; }
        public ProximitySample Proximity { get; }
    }
}

