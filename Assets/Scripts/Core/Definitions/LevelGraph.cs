// ============================================================================
// LevelGraph.cs
// ============================================================================
// PURPOSE:
//   Carries a frozen snapshot of room topology, anchors and the exit. Defensive
//   copies prevent a caller from mutating graph data after consumers receive it.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Describe stable authored level data across system boundaries.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only; no project layers.
// USAGE NOTES:
//   Construct through LevelGraphUtility.Build for validation and stable ordering.
//   A graph is immutable after construction; the Level owner publishes a new
//   snapshot when the set of active markers changes.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Core
{
    public sealed class LevelGraph
    {
        public LevelGraph(IReadOnlyList<LevelRoom> rooms, IReadOnlyList<LevelEdge> edges,
            IReadOnlyList<LevelAnchor> anchors, int exitRoomId, Vector3 exitPosition)
        {
            Rooms = Array.AsReadOnly((rooms ?? throw new ArgumentNullException(nameof(rooms))).ToArray());
            Edges = Array.AsReadOnly((edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
            Anchors = Array.AsReadOnly((anchors ?? throw new ArgumentNullException(nameof(anchors))).ToArray());
            ExitRoomId = exitRoomId;
            ExitPosition = exitPosition;
        }

        public IReadOnlyList<LevelRoom> Rooms { get; }
        public IReadOnlyList<LevelEdge> Edges { get; }
        public IReadOnlyList<LevelAnchor> Anchors { get; }
        public int ExitRoomId { get; }
        public Vector3 ExitPosition { get; }
    }
}

