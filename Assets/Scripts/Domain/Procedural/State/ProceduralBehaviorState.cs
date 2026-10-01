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
//   - Retain retry provenance after teardown so Session can report failed generation.
//   - Keep template exhaustion and the bounded organic recovery stage explicit.
// DEPENDENCIES:
//   - Procedural definitions only; no other gameplay system.
// USAGE NOTES:
//   Scene-owned through its Manager; layout resets on teardown, provenance on Begin.
// ============================================================================
namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralBehaviorState
    {
        public ProceduralLayout Layout { get; internal set; }
        public bool IsReady { get; internal set; }
        public int AttemptIndex { get; internal set; }
        public int AttemptSeed { get; internal set; }
        public int BaseSeed { get; internal set; }
        public int RetryBudget { get; internal set; }
        public bool GenerationSucceeded { get; internal set; }
        public bool UsedFallback { get; internal set; }
        public int FallbackCount { get; internal set; }
        public string TemplateFailureReason { get; internal set; } = string.Empty;
        public string OrganicFallbackReason { get; internal set; } = string.Empty;
        public string GenerationManifest { get; internal set; } = string.Empty;
    }
}
