// ============================================================================
// ProceduralGeometryPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts castle rooms and all shared-wall apertures into physical shell blocks.
//   Shared walls are emitted once with both ordinary and optional openings,
//   while tiled floors, complete ceilings and upper routes follow one geometric model.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Calculate collision/render blocks and navigation bounds without engine calls.
//   - Seal every roof and the upper wall transitions between unequal room heights.
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
                int tiles = config.CastleModules ? 3 : 1;
                for (int x = 0; x < tiles; x++)
                for (int z = 0; z < tiles; z++)
                    blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Floor,
                        new Vector3(room.Center.x - room.Size.x * 0.5f + (x + 0.5f) * room.Size.x / tiles,
                            -driver.FloorThickness * 0.5f,
                            room.Center.z - room.Size.z * 0.5f + (z + 0.5f) * room.Size.z / tiles),
                        new Vector3(room.Size.x / tiles, driver.FloorThickness, room.Size.z / tiles)));
                blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Ceiling,
                    new Vector3(room.Center.x, room.Size.y + driver.CeilingThickness * 0.5f, room.Center.z),
                    new Vector3(room.Size.x, driver.CeilingThickness, room.Size.z)));
                for (int side = 0; side < 4; side++) AddWall(blocks, layout, room, side, config, driver);
            }
            blocks.AddRange(new ProceduralRoutePresenter().Build(layout, config, driver));
            if (config.CastleModules) blocks.AddRange(new ProceduralCastlePresenter().Build(layout, config, driver));
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
            var center = new Vector3(room.Center.x, room.Size.y * 0.5f, room.Center.z) +
                (alongX ? Vector3.forward : Vector3.right) * (sign * config.RoomSize * 0.5f);
            var openings = new List<ProceduralDoorPlan>();
            foreach (var door in layout.Doors)
            {
                if (door.FromRoomId != room.Id && door.ToRoomId != room.Id) continue;
                if (door.AlongX == alongX && (alongX ? door.Center.z == center.z : door.Center.x == center.x))
                    openings.Add(door);
            }
            if (openings.Count > 0 && openings[0].ToRoomId == room.Id)
            {
                // The first room owns the apertures and shared wall below its roof.
                // A taller neighbor owns the remaining band so no exterior gap remains.
                var neighbor = layout.Graph.Rooms[openings[0].FromRoomId - 1];
                float extraHeight = room.Size.y - neighbor.Size.y;
                if (extraHeight > 0f)
                    blocks.Add(Wall(room.Id,
                        new Vector3(center.x, neighbor.Size.y + extraHeight * 0.5f, center.z),
                        config.RoomSize, extraHeight, alongX, driver.WallThickness));
                return;
            }
            if (openings.Count == 0)
            {
                blocks.Add(Wall(room.Id, center, config.RoomSize, room.Size.y, alongX, driver.WallThickness));
                return;
            }
            openings.Sort((a, b) => (alongX ? a.Center.x : a.Center.z).CompareTo(alongX ? b.Center.x : b.Center.z));
            float middle = alongX ? center.x : center.z;
            float cursor = middle - config.RoomSize * 0.5f;
            var axis = alongX ? Vector3.right : Vector3.forward;
            foreach (var opening in openings)
            {
                float doorMiddle = alongX ? opening.Center.x : opening.Center.z;
                float width = opening.IsOptional ? 1.8f : config.DoorWidth;
                float gapMin = doorMiddle - width * 0.5f;
                float gapMax = doorMiddle + width * 0.5f;
                if (gapMin < cursor) throw new ArgumentException("Generated wall apertures overlap.");
                if (gapMin > cursor)
                    blocks.Add(Wall(room.Id, center + axis * ((cursor + gapMin) * 0.5f - middle),
                        gapMin - cursor, room.Size.y, alongX, driver.WallThickness));
                float bottom = opening.TraversalKind == TraversalSurfaceKind.Vault ? driver.VaultHeight : 0f;
                float top = opening.TraversalKind == TraversalSurfaceKind.Vault ? driver.WindowTopHeight :
                    opening.TraversalKind == TraversalSurfaceKind.SlideGate ? driver.SlideClearance : config.DoorHeight;
                int surfaceId = 80000 + room.Id * 100 + side * 10 + (opening.IsOptional ? 1 : 0);
                var normal = alongX ? Vector3.forward : Vector3.right;
                var a = opening.Center - normal * driver.LandingOffset;
                var b = opening.Center + normal * driver.LandingOffset;
                if (bottom > 0f)
                    blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Wall,
                        opening.Center + Vector3.up * (bottom * 0.5f),
                        alongX ? new Vector3(width, bottom, driver.WallThickness) : new Vector3(driver.WallThickness, bottom, width),
                        surfaceId, TraversalSurfaceKind.Vault, a, b));
                blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Wall,
                    opening.Center + Vector3.up * ((top + room.Size.y) * 0.5f),
                    alongX ? new Vector3(width, room.Size.y - top, driver.WallThickness) : new Vector3(driver.WallThickness, room.Size.y - top, width),
                    opening.TraversalKind == TraversalSurfaceKind.SlideGate ? surfaceId : 0,
                    opening.TraversalKind == TraversalSurfaceKind.SlideGate ? TraversalSurfaceKind.SlideGate : TraversalSurfaceKind.None, a, b));
                cursor = gapMax;
            }
            float wallMax = middle + config.RoomSize * 0.5f;
            if (cursor < wallMax)
                blocks.Add(Wall(room.Id, center + axis * ((cursor + wallMax) * 0.5f - middle),
                    wallMax - cursor, room.Size.y, alongX, driver.WallThickness));
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
