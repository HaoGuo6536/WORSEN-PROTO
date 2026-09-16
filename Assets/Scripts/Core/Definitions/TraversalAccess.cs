// ============================================================================
// TraversalAccess.cs
// ============================================================================
// PURPOSE:
//   Identifies which actor can use an authored route. Graph consumers can apply
//   the same access policy as navigation and collision without seeing a Driver.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Describe stable authored level data across system boundaries.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only; no project layers.
// USAGE NOTES:
//   Immutable shared data; no runtime engine calls or lifecycle ownership.
// ============================================================================

using System;

namespace Worsen.Core
{
    [Flags]
    public enum TraversalAccess
    {
        None = 0,
        Player = 1,
        Hunter = 2,
        All = Player | Hunter
    }
}

