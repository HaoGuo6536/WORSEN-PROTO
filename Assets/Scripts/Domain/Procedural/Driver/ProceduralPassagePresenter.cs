// ============================================================================
// ProceduralPassagePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a straight, supported crossing from an admitted shrine to the first
//   pocket beyond a declared gap. Both sealed wall faces receive an aperture and
//   the temporary tiles fall in site-outward order using explicitly supplied time.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Resolve destination identity without jumping over intervening occupied cells.
//   - Tile the full gap and subtract an aperture from collision and visual wall pieces.
//   - Produce ordered collapse transitions and deterministic falling offsets.
// DEPENDENCIES:
//   - Own layout, DriverConfig and DriverState; Core graph and anchor values only.
// USAGE NOTES:
//   No engine calls or random draws. Only cardinal, ground-level crossings are admitted.
//   Door height and floor thickness reuse existing tunables; cuts preserve wall roles.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralPassagePresenter
    {
        public int Destination(ProceduralLayout layout, int sourceRoom, Vector3 position, Vector3 facing)
            => Corridor(layout, sourceRoom, position, facing, out _, out _, out int destination) ? destination : 0;

        private static bool Corridor(ProceduralLayout layout, int sourceRoom, Vector3 position, Vector3 facing,
            out Vector3 edge, out Vector3 landing, out int destination)
        {
            edge = landing = default; destination = 0;
            if (layout?.Graph == null || !(layout.CellSize > 0f) || !Finite(position.x) || !Finite(position.y) ||
                !Finite(position.z) || Mathf.Abs(position.y) > .001f || Mathf.Abs(facing.y) > .001f ||
                !((Mathf.Abs(facing.x) == 1f && facing.z == 0f) || (Mathf.Abs(facing.z) == 1f && facing.x == 0f))) return false;
            var room = layout.Graph.Rooms.FirstOrDefault(r => r.Id == sourceRoom);
            if (room.Id == 0 || room.Pocket) return false;
            var cell = ProceduralFootprintUtility.Volumes(layout, room).FirstOrDefault(r => r.ContainsXZ(position));
            if (cell.Id == 0) return false;
            bool x = facing.x != 0f;
            float distance = x ? (facing.x > 0f ? cell.Bounds.max.x - position.x : position.x - cell.Bounds.min.x) :
                (facing.z > 0f ? cell.Bounds.max.z - position.z : position.z - cell.Bounds.min.z);
            edge = position + facing * distance;
            var center = new Vector3(cell.Center.x, 0f, cell.Center.z);
            int gaps = 0;
            for (int step = 1; step <= layout.GapCells.Count + 1; step++)
            {
                var next = center + facing * (layout.CellSize * step);
                var grid = new Vector2Int(Mathf.RoundToInt((next.x - layout.Origin.x) / layout.CellSize),
                    Mathf.RoundToInt((next.z - layout.Origin.y) / layout.CellSize));
                if (layout.GapCells.Contains(grid)) { gaps++; continue; }
                if (gaps == 0) return false;
                foreach (var target in layout.Graph.Rooms)
                foreach (var volume in ProceduralFootprintUtility.Volumes(layout, target))
                    if (volume.ContainsXZ(next))
                    {
                        if (!target.Pocket) return false;
                        landing = edge + facing * (layout.CellSize * gaps);
                        destination = target.Id; return true;
                    }
                return false;
            }
            return false;
        }

        public ProceduralPassagePlan Build(ProceduralLayout layout, int siteIndex, ProceduralConfig config,
            ProceduralDriverConfig driver, IReadOnlyList<ProceduralBlock> blocks)
        {
            foreach (float value in new[] { driver.PassageTileLength, driver.PassageWidth, driver.PassageTileInterval,
                driver.PassageFallAcceleration, driver.PassageFallDuration })
                if (!Finite(value) || value <= 0f) throw new ArgumentException("Invalid Passage dimensions or timing.");
            if (!Finite(driver.PassageFirstTileDelay) || driver.PassageFirstTileDelay < 0f ||
                driver.PassageWidth >= layout.CellSize || driver.PassageWidth <= driver.NavSampleRadius * 2f)
                throw new ArgumentException("Passage width must fit a cell and navigation clearance.");
            if (siteIndex < 0 || siteIndex >= layout.ShrineSites.Count) throw new ArgumentOutOfRangeException(nameof(siteIndex));
            var site = layout.ShrineSites[siteIndex];
            if (!site.GapEdge || !Corridor(layout, site.RoomId, site.Position, site.Facing, out var edge, out var landing, out int destination) ||
                destination != site.DestinationPocketRoomId) throw new ArgumentException("Passage site has no identified pocket across its gap.");
            var sourceCell = ProceduralFootprintUtility.Volumes(layout, layout.Graph.Rooms.Single(r => r.Id == site.RoomId))
                .Single(r => r.ContainsXZ(site.Position));
            var targetCell = ProceduralFootprintUtility.Volumes(layout, layout.Graph.Rooms.Single(r => r.Id == destination))
                .Single(r => r.ContainsXZ(landing + site.Facing * .01f));
            bool x = site.Facing.x != 0f;
            float tangent = x ? site.Position.z : site.Position.x;
            foreach (var cell in new[] { sourceCell, targetCell })
                if (tangent - driver.PassageWidth * .5f <= (x ? cell.Bounds.min.z : cell.Bounds.min.x) ||
                    tangent + driver.PassageWidth * .5f >= (x ? cell.Bounds.max.z : cell.Bounds.max.x))
                    throw new ArgumentException("Passage aperture crosses a cell corner.");
            var walls = new List<ProceduralPassageWall>();
            foreach (var face in new[] { edge, landing })
            {
                int roomId = face == edge ? site.RoomId : destination;
                var aperture = new Bounds(face + Vector3.up * (config.DoorHeight * .5f), x ?
                    new Vector3(driver.WallThickness * 2f, config.DoorHeight, driver.PassageWidth) :
                    new Vector3(driver.PassageWidth, config.DoorHeight, driver.WallThickness * 2f));
                foreach (var block in blocks.Where(b => b.RoomId == roomId && b.Kind == ProceduralSurfaceKind.Wall && b.Rotation == Quaternion.identity))
                    if (Overlaps(new Bounds(block.Center, block.Size), aperture))
                        walls.Add(new ProceduralPassageWall(block, Subtract(block, aperture)));
            }
            if (!walls.Any(w => w.Original.RoomId == site.RoomId && w.Original.HasCollision) ||
                !walls.Any(w => w.Original.RoomId == destination && w.Original.HasCollision))
                throw new ArgumentException("Passage must open both sealed wall faces.");
            var start = edge - site.Facing * driver.WallThickness;
            var finish = landing + site.Facing * driver.WallThickness;
            float length = Vector3.Distance(start, finish);
            int count = Mathf.CeilToInt(length / driver.PassageTileLength);
            var tiles = new List<ProceduralBlock>(count);
            for (int i = 0; i < count; i++)
            {
                float from = i * driver.PassageTileLength, to = Mathf.Min(length, from + driver.PassageTileLength);
                tiles.Add(new ProceduralBlock(site.RoomId, ProceduralSurfaceKind.Floor,
                    start + site.Facing * ((from + to) * .5f) - Vector3.up * (driver.FloorThickness * .5f),
                    x ? new Vector3(to - from, driver.FloorThickness, driver.PassageWidth) :
                        new Vector3(driver.PassageWidth, driver.FloorThickness, to - from)));
            }
            int pocketId = layout.Modules.Single(m => m.RoomId == destination).PocketId;
            var rooms = layout.Modules.Where(m => m.PocketId == pocketId).Select(m => m.RoomId).ToArray();
            return new ProceduralPassagePlan(siteIndex, destination, site.Position,
                landing + site.Facing * (driver.NavSampleRadius + driver.WallThickness), tiles.AsReadOnly(),
                Array.AsReadOnly(tiles.Select(t => t.Center + Vector3.up * (driver.FloorThickness * .5f)).ToArray()), walls.AsReadOnly(),
                Array.AsReadOnly(layout.PocketAnchors.Where(a => rooms.Contains(a.RoomId)).OrderBy(a => a.Id).ToArray()));
        }

        public IReadOnlyList<int> Advance(ProceduralPassageDriverState state, float deltaTime, ProceduralDriverConfig config)
        {
            if (!Finite(deltaTime) || deltaTime < 0f) throw new ArgumentException("Passage time must be finite and nonnegative.");
            state.Elapsed += deltaTime;
            var fallen = new List<int>();
            while (state.CollapsedCount < state.Plan.Tiles.Count && state.Elapsed + .000001 >=
                config.PassageFirstTileDelay + (double)state.CollapsedCount * config.PassageTileInterval)
                fallen.Add(state.CollapsedCount++);
            return fallen.AsReadOnly();
        }

        public Vector3 FallOffset(double elapsed, int tileIndex, ProceduralDriverConfig config)
        {
            double time = Math.Max(0d, elapsed - config.PassageFirstTileDelay - (double)tileIndex * config.PassageTileInterval);
            return Vector3.down * (float)(.5d * config.PassageFallAcceleration * time * time);
        }

        private static IReadOnlyList<ProceduralBlock> Subtract(ProceduralBlock block, Bounds cut)
        {
            var bounds = new Bounds(block.Center, block.Size);
            var min = Vector3.Max(bounds.min, cut.min); var max = Vector3.Min(bounds.max, cut.max);
            var result = new List<ProceduralBlock>();
            Add(bounds.min, new Vector3(min.x, bounds.max.y, bounds.max.z));
            Add(new Vector3(max.x, bounds.min.y, bounds.min.z), bounds.max);
            Add(new Vector3(min.x, bounds.min.y, bounds.min.z), new Vector3(max.x, min.y, bounds.max.z));
            Add(new Vector3(min.x, max.y, bounds.min.z), new Vector3(max.x, bounds.max.y, bounds.max.z));
            Add(new Vector3(min.x, min.y, bounds.min.z), new Vector3(max.x, max.y, min.z));
            Add(new Vector3(min.x, min.y, max.z), new Vector3(max.x, max.y, bounds.max.z));
            return result.AsReadOnly();
            void Add(Vector3 a, Vector3 b)
            {
                var size = b - a;
                if (size.x > .00001f && size.y > .00001f && size.z > .00001f)
                    result.Add(new ProceduralBlock(block.RoomId, block.Kind, (a + b) * .5f, size, role: block.Role));
            }
        }
        private static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x &&
            a.min.y < b.max.y && a.max.y > b.min.y && a.min.z < b.max.z && a.max.z > b.min.z;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
