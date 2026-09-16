// ============================================================================
// ProceduralGeometryPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts room and doorway data into axis-aligned shell blocks. A shared wall
//   is emitted once with one matched opening, preventing the neighboring room
//   from sealing the doorway or introducing a gap in the enclosed shell.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Calculate collision/render blocks and navigation bounds without engine calls.
// DEPENDENCIES:
//   - Core room values and Procedural layout/configuration only.
// USAGE NOTES:
//   Pure, stateless calculations. Floors tile exactly at room boundaries; ceilings
//   and walls are tagged separately so navigation never treats their tops as floors.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralGeometryPresenter
    {
        public IReadOnlyList<ProceduralBlock> Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            if (layout?.Graph == null || config == null || driver == null) throw new ArgumentNullException();
            Validate(config, driver);
            var blocks = new List<ProceduralBlock>();
            foreach (var room in layout.Graph.Rooms)
            {
                blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Floor,
                    new Vector3(room.Center.x, -driver.FloorThickness * 0.5f, room.Center.z),
                    new Vector3(room.Size.x, driver.FloorThickness, room.Size.z)));
                blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Ceiling,
                    new Vector3(room.Center.x, config.RoomHeight + driver.CeilingThickness * 0.5f, room.Center.z),
                    new Vector3(room.Size.x, driver.CeilingThickness, room.Size.z)));
                for (int side = 0; side < 4; side++) AddWall(blocks, layout, room, side, config, driver);
            }
            blocks.AddRange(new ProceduralRoutePresenter().Build(layout, config, driver));
            return blocks.AsReadOnly();
        }

        public Bounds NavigationBounds(IReadOnlyList<ProceduralBlock> blocks, float padding)
        {
            if (blocks == null || blocks.Count == 0 || !Finite(padding) || padding < 0f)
                throw new ArgumentException("Navigation bounds need nonempty geometry and finite nonnegative padding.");
            var bounds = new Bounds(blocks[0].Center, blocks[0].Size);
            foreach (var block in blocks) bounds.Encapsulate(new Bounds(block.Center, block.Size));
            bounds.Expand(padding * 2f);
            return bounds;
        }

        private static void AddWall(List<ProceduralBlock> blocks, ProceduralLayout layout, LevelRoom room,
            int side, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            bool alongX = side == 0 || side == 2;
            float sign = side < 2 ? 1f : -1f;
            var center = new Vector3(room.Center.x, config.RoomHeight * 0.5f, room.Center.z) +
                (alongX ? Vector3.forward : Vector3.right) * (sign * config.RoomSize * 0.5f);
            ProceduralDoorPlan? opening = null;
            foreach (var door in layout.Doors)
            {
                if (door.FromRoomId != room.Id && door.ToRoomId != room.Id) continue;
                if (door.AlongX != alongX) continue;
                if ((alongX ? door.Center.z == center.z : door.Center.x == center.x)) { opening = door; break; }
            }
            if (opening.HasValue && opening.Value.ToRoomId == room.Id) return;
            if (!opening.HasValue)
            {
                blocks.Add(Wall(room.Id, center, config.RoomSize, config.RoomHeight, alongX, driver.WallThickness));
                return;
            }
            var doorCenter = opening.Value.Center;
            float middle = alongX ? center.x : center.z;
            float doorMiddle = alongX ? doorCenter.x : doorCenter.z;
            float wallMin = middle - config.RoomSize * 0.5f;
            float wallMax = middle + config.RoomSize * 0.5f;
            float gapMin = doorMiddle - config.DoorWidth * 0.5f;
            float gapMax = doorMiddle + config.DoorWidth * 0.5f;
            var axis = alongX ? Vector3.right : Vector3.forward;
            blocks.Add(Wall(room.Id, center + axis * ((wallMin + gapMin) * 0.5f - middle),
                gapMin - wallMin, config.RoomHeight, alongX, driver.WallThickness));
            blocks.Add(Wall(room.Id, center + axis * ((wallMax + gapMax) * 0.5f - middle),
                wallMax - gapMax, config.RoomHeight, alongX, driver.WallThickness));
            var lintelCenter = doorCenter + Vector3.up * ((config.DoorHeight + config.RoomHeight) * 0.5f);
            blocks.Add(Wall(room.Id, lintelCenter, config.DoorWidth,
                config.RoomHeight - config.DoorHeight, alongX, driver.WallThickness));
        }

        private static ProceduralBlock Wall(int roomId, Vector3 center, float length, float height, bool alongX, float thickness)
        {
            var size = alongX ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length);
            return new ProceduralBlock(roomId, ProceduralSurfaceKind.Wall, center, size);
        }

        private static void Validate(ProceduralConfig config, ProceduralDriverConfig driver)
        {
            Positive(driver.WallThickness); Positive(driver.FloorThickness); Positive(driver.CeilingThickness);
            Positive(driver.NavSampleRadius); Positive(driver.NavVoxelSize); Positive(driver.NavBoundsPadding);
            if (driver.GeometryLayer < 0 || driver.GeometryLayer > 31) throw new ArgumentException("Invalid geometry layer.");
            float usableHalf = (config.RoomSize - driver.WallThickness) * 0.5f - driver.NavSampleRadius;
            if ((CakeSpan(config) >= usableHalf) || config.CakeLineOffset >= usableHalf || config.SpawnSideOffset >= usableHalf ||
                driver.WallThickness >= config.DoorWidth || driver.NavVoxelSize >= config.DoorWidth * 0.5f)
                throw new ArgumentException("Shell thickness or navigation clearance would obstruct the cake lines or doors.");
        }
        private static float CakeSpan(ProceduralConfig config) => (config.CakesPerLine - 1) * config.CakeSpacing * 0.5f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Positive(float value)
        { if (!Finite(value) || value <= 0f) throw new ArgumentException("Geometry and navigation dimensions must be finite and positive."); }
    }
}
