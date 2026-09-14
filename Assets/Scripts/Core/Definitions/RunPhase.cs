// ============================================================================
// RunPhase.cs
// ============================================================================
//
// PURPOSE:
//   Names the public stages of a solo run in a form every layer can understand.
//   Session rules choose transitions while presentation receives only this value,
//   avoiding a dependency on the mutable state or implementation of the run.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared run lifecycle data.
//
// KEY RESPONSIBILITIES:
//   - Provide the run phase payload published by the Session Manager.
//   - Reserve the planned floor-loop phases without implementing their gameplay.
//
// DEPENDENCIES:
//   - None; this enum has no engine or project-layer dependencies.
//
// USAGE NOTES:
//   Boot is the default and does not tick. M0 reaches FirstSweep after a scene
//   readiness hand-off; later milestones supply the exit and collapse facts.
//
// ============================================================================

namespace Worsen.Core
{
    public enum RunPhase
    {
        Boot = 0,
        FirstSweep = 1,
        ExitOpen = 2,
        Collapse = 3,
        Ended = 4
    }
}
