// ============================================================================
// ProceduralBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds the current generated layout and its admission state. Geometry and
//   navigation must both pass validation before the Manager exposes readiness.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Retain generated data independently of engine objects and subscriptions.
// DEPENDENCIES:
//   - Procedural definitions only; no other gameplay system.
// USAGE NOTES:
//   Scene-owned through its Manager; reset on every generation and teardown.
// ============================================================================
namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralBehaviorState
    {
        public ProceduralLayout Layout { get; internal set; }
        public bool IsReady { get; internal set; }
    }
}
