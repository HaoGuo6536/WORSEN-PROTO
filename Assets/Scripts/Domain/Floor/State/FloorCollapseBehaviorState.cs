// ============================================================================
// FloorCollapseBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains reusable escape-route working storage for a single Floor controller.
//   Rebuilding connectivity in these containers avoids allocating graph maps on
//   every tick while still observing newly closed rooms and moving players.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Hold adjacency, distance, queue and occupancy scratch collections.
//   - Hold the current protected-room result until the next calculation.
// DEPENDENCIES:
//   - Core level data only; no engine operations or foreign mutable state.
// USAGE NOTES:
//   Controller-owned scratch, never shared across concurrent calculations.
//   The utility clears working contents on each call; capacity is retained.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public sealed class FloorCollapseBehaviorState
    {
        internal LevelGraph Graph;
        internal readonly HashSet<int> ProtectedRooms = new HashSet<int>();
        internal readonly Dictionary<int, List<int>> Next = new Dictionary<int, List<int>>();
        internal readonly Dictionary<int, List<int>> Previous = new Dictionary<int, List<int>>();
        internal readonly Dictionary<int, int> Distance = new Dictionary<int, int>();
        internal readonly Queue<int> Queue = new Queue<int>();
        internal readonly List<int> Occupied = new List<int>();
    }
}
