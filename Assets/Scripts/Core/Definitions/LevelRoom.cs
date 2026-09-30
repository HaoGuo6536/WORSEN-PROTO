// ============================================================================
// LevelRoom.cs
// ============================================================================
// PURPOSE:
//   Describes a stable room and its world-space extent. Downstream systems use
//   the same room identity for path topology and spatial collapse placement.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Describe stable authored level data across system boundaries.
//   - Preserve rectangular bounds while exposing copied footprint cells and pocket identity.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only; no project layers.
// USAGE NOTES:
//   Immutable shared data; no runtime engine calls or lifecycle ownership.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Core
{
    public readonly struct LevelRoom
    {
        public LevelRoom(int id, Vector3 center, Vector3 size, Bounds[] cells = null, bool pocket = false)
        {
            Id = id;
            Center = center;
            Size = size;
            Cells = Array.AsReadOnly(cells == null || cells.Length == 0
                ? new[] { new Bounds(center, size) } : (Bounds[])cells.Clone());
            Pocket = pocket;
        }

        public int Id { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public Bounds Bounds => new Bounds(Center, Size);
        public IReadOnlyList<Bounds> Cells { get; }
        public bool Pocket { get; }

        public bool ContainsXZ(Vector3 point)
        {
            if (Cells == null) return false;
            foreach (var cell in Cells)
                if (point.x >= cell.min.x && point.x <= cell.max.x &&
                    point.z >= cell.min.z && point.z <= cell.max.z) return true;
            return false;
        }
    }
}

