// ============================================================================
// CakeAnchorType.cs
// ============================================================================
// PURPOSE:
//   Labels an authored pickup opportunity by its traversal cost. Level and Floor
//   share this vocabulary while owning their separate state and behavior.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Describe stable authored level data across system boundaries.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only; no project layers.
// USAGE NOTES:
//   Immutable shared data; no runtime engine calls or lifecycle ownership.
// ============================================================================

namespace Worsen.Core
{
    public enum CakeAnchorType { Flow, Precision, Detour, Risk, Vertical }
}

