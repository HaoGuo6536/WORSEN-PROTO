// ============================================================================
// ShrineBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds floor-local placements and which shrines have already been touched.
//   Rebuilding the floor replaces this data without retaining player or scene objects.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Retain the placement order and single-use identities for the Controller.
// DEPENDENCIES:
//   - Core shrine values and System collections only.
// USAGE NOTES:
//   Private to the Shrine system; external consumers receive copied placements.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Domain.Shrine
{
    public sealed class ShrineBehaviorState
    {
        internal readonly List<ShrinePlacement> Placements = new List<ShrinePlacement>();
        internal readonly HashSet<int> Used = new HashSet<int>();
    }
}
