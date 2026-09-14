// ============================================================================
// RunDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Names the facts understood by the pure run transition rules. Keeping the
//   vocabulary outside the Manager makes transition tests independent of Unity
//   and leaves future gameplay systems free to report facts through the Manager.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · Run.
//
// KEY RESPONSIBILITIES:
//   - Describe the facts that advance or finish a solo run.
//   - Keep internal flow inputs separate from the public Core RunPhase payload.
//
// DEPENDENCIES:
//   - None; these are system-local enum values.
//
// USAGE NOTES:
//   M0 supplies SceneReady. The remaining values define the approved transition
//   skeleton and receive gameplay producers in the later floor-loop milestones.
//
// ============================================================================

namespace Worsen.Session.Run
{
    public enum RunEvent
    {
        SceneReady,
        ExitOpened,
        CollapseStarted,
        PlayerDied,
        ExitReached
    }
}
