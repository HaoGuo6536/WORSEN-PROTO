// ============================================================================
// LevelBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds the current graph and whether its authoring records are complete.
//   A failed rebuild invalidates this state so downstream systems cannot consume
//   silently invented rooms or pickup positions.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Store graph data owned by LevelController.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Scene-owned through LevelManager. Mutators are not exposed on IReadOnlyLevelState.
// ============================================================================

using Worsen.Core;

namespace Worsen.Domain.Level
{
    public sealed class LevelBehaviorState : IReadOnlyLevelState
    {
        public bool IsReady { get; internal set; }
        public LevelGraph Graph { get; internal set; }
    }
}

