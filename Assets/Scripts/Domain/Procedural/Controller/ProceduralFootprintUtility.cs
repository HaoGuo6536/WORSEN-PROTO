// ============================================================================
// ProceduralFootprintUtility.cs
// ============================================================================
// PURPOSE:
//   Resolves exact occupied volumes instead of treating an irregular room's bounds
//   as floor. This keeps shell construction, anchors and spawn checks in agreement
//   and rejects disconnected required content before physical admission.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Resolve template, carved or coarse cells and validate connected required space.
// DEPENDENCIES:
//   - Core graph values and own layout definitions; no engine calls or siblings.
// USAGE NOTES:
//   A room's first cell owns its authored furniture and ramps. Other cells are open
//   hall space. Legacy synthetic layouts without cells retain rectangular volumes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralFootprintUtility
    {
        public static IReadOnlyList<LevelRoom> Volumes(ProceduralLayout layout, LevelRoom room)
        {
            if (layout.UsesTemplates || layout.OrganicRooms.Any(r => r.RoomId == room.Id))
                return room.Cells.Select(c => new LevelRoom(room.Id, c.center, c.size, pocket: room.Pocket)).ToArray();
            var module = layout.Modules?.FirstOrDefault(m => m.RoomId == room.Id) ?? default;
            if (module.Cells == null || module.Cells.Count == 0) return new[] { room };
            return module.Cells.Select(c => new LevelRoom(room.Id,
                new Vector3(layout.Origin.x + c.x * layout.CellSize, room.Center.y, layout.Origin.y + c.y * layout.CellSize),
                new Vector3(layout.CellSize, room.Size.y, layout.CellSize), pocket: room.Pocket)).ToArray();
        }

        public static LevelRoom At(ProceduralLayout layout, Vector3 position)
            => layout.Graph.Rooms.SelectMany(r => Volumes(layout, r)).FirstOrDefault(r => r.Bounds.Contains(position));

        public static void Validate(ProceduralLayout layout)
        {
            var occupied = layout.Modules.SelectMany(m => m.Cells).ToArray();
            if (occupied.Distinct().Count() != occupied.Length || layout.GapCells.Any(occupied.Contains))
                throw new InvalidOperationException("Footprints overlap or occupy a gap.");
            var spawn = At(layout, layout.PlayerSpawnPosition);
            var exit = At(layout, layout.Graph.ExitPosition);
            bool Pocket(int id) => layout.Modules.Any(m => m.RoomId == id && m.PocketId != 0);
            if (spawn.Id == 0 || exit.Id != layout.Graph.ExitRoomId || Pocket(spawn.Id) || Pocket(exit.Id))
                throw new InvalidOperationException("Player spawn or exit is outside the connected footprint.");
            foreach (var actor in new[] { TraversalAccess.Player, TraversalAccess.Hunter })
            {
                var from = LevelGraphUtility.TopologicalDistancesFrom(layout.Graph, spawn.Id, actor);
                var to = LevelGraphUtility.DistancesTo(layout.Graph, exit.Id, actor);
                foreach (var room in layout.Graph.Rooms)
                    if (Pocket(room.Id) ? from[room.Id] >= 0 || to[room.Id] >= 0 : from[room.Id] < 0 || to[room.Id] < 0)
                        throw new InvalidOperationException("Connected rooms and isolated pockets disagree with traversal edges.");
            }
            foreach (var anchor in layout.Graph.Anchors)
                if (Pocket(anchor.RoomId) || layout.OrganicRooms.Any(r => r.RoomId == anchor.RoomId && !ProceduralOrganicUtility.Clear(r, anchor.Position)) ||
                    !Volumes(layout, layout.Graph.Rooms.First(r => r.Id == anchor.RoomId))
                    .Any(r => r.Bounds.Contains(anchor.Position)))
                    throw new InvalidOperationException("Required anchor is in a pocket or outside its footprint: " + anchor.Id);
            foreach (var anchor in layout.PocketAnchors)
                if (!Pocket(anchor.RoomId) || At(layout, anchor.Position).Id != anchor.RoomId)
                    throw new InvalidOperationException("Optional anchor is outside its pocket.");
            foreach (var hunter in layout.HunterSpawnPositions)
                if (At(layout, hunter).Id == 0 || Pocket(At(layout, hunter).Id))
                    throw new InvalidOperationException("Hunter spawn is outside the connected footprint.");
        }
    }
}
