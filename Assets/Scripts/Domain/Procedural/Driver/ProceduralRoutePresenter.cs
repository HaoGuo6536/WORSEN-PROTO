// ============================================================================
// ProceduralRoutePresenter.cs
// ============================================================================
// PURPOSE:
//   Places deliberate traversal shortcuts through a full-height sight-blocking
//   partition. Each shortcut joins two generous cake lanes while an ordinary
//   walking route around either partition end remains available to both actors.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Calculate vault, window and slide module geometry with explicit landings.
//   - Mark partition corners for rebounds and export neutral traversal records.
// DEPENDENCIES:
//   - Core traversal interfaces/records and Procedural room/configuration values.
// USAGE NOTES:
//   Pure and stateless. A shortcut never replaces the mandatory walking route.
//   Native navigation separately verifies that Hunter must take the longer path;
//   no HunterRouteGate layer is used because that layer lets Hunter ignore walls.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralRoutePresenter
    {
        public IReadOnlyList<ProceduralBlock> Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            if (layout?.Modules == null || config == null || driver == null) throw new ArgumentNullException();
            Validate(config, driver);
            var blocks = new List<ProceduralBlock>();
            foreach (var module in layout.Modules)
            {
                var room = layout.Graph.Rooms[module.RoomId - 1];
                var center = new Vector3(room.Center.x, 0f, room.Center.z);
                var along = module.AlongX ? Vector3.right : Vector3.forward;
                var across = module.AlongX ? Vector3.forward : Vector3.right;
                float legLength = (driver.PartitionLength - driver.ShortcutWidth) * 0.5f;
                float legOffset = (driver.PartitionLength + driver.ShortcutWidth) * 0.25f;
                for (int side = 0; side < 2; side++)
                    blocks.Add(Block(module, center + along * ((side == 0 ? -1f : 1f) * legOffset) + Vector3.up * (config.RoomHeight * 0.5f),
                        legLength, config.RoomHeight, driver.PartitionThickness,
                        50000 + room.Id * 10 + side, TraversalSurfaceKind.Rebound));
                var endpointA = center - across * driver.LandingOffset;
                var endpointB = center + across * driver.LandingOffset;
                if (module.Kind == ProceduralModuleKind.SlidePartition)
                {
                    float height = config.RoomHeight - driver.SlideClearance;
                    blocks.Add(Block(module, center + Vector3.up * ((config.RoomHeight + driver.SlideClearance) * 0.5f),
                        driver.ShortcutWidth, height, driver.PartitionThickness,
                        50000 + room.Id * 10 + 2, TraversalSurfaceKind.SlideGate, endpointA, endpointB));
                }
                else
                {
                    blocks.Add(Block(module, center + Vector3.up * (driver.VaultHeight * 0.5f),
                        driver.ShortcutWidth, driver.VaultHeight, driver.PartitionThickness,
                        50000 + room.Id * 10 + 2, TraversalSurfaceKind.Vault, endpointA, endpointB));
                    if (module.Kind == ProceduralModuleKind.WindowPartition)
                        blocks.Add(Block(module, center + Vector3.up * ((config.RoomHeight + driver.WindowTopHeight) * 0.5f),
                            driver.ShortcutWidth, config.RoomHeight - driver.WindowTopHeight, driver.PartitionThickness));
                }
            }
            return blocks.AsReadOnly();
        }

        public IReadOnlyList<LevelMarkerRecord> DescribeMarkers(IReadOnlyList<ProceduralBlock> blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            var markers = new List<LevelMarkerRecord>();
            foreach (var block in blocks)
            {
                if (block.SurfaceId == 0) continue;
                var kind = block.TraversalKind == TraversalSurfaceKind.Vault ? LevelMarkerKind.VaultSurface :
                    block.TraversalKind == TraversalSurfaceKind.SlideGate ? LevelMarkerKind.SlideGate : LevelMarkerKind.ReboundSurface;
                markers.Add(new LevelMarkerRecord(block.SurfaceId, kind, block.RoomId, block.RoomId,
                    block.Center, block.Size, true, TraversalAccess.Player));
            }
            return markers.AsReadOnly();
        }

        private static ProceduralBlock Block(ProceduralRoomModule module, Vector3 center, float length, float height, float thickness,
            int id = 0, TraversalSurfaceKind kind = TraversalSurfaceKind.None, Vector3 endpointA = default, Vector3 endpointB = default)
            => new ProceduralBlock(module.RoomId, ProceduralSurfaceKind.Wall, center,
                module.AlongX ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length), id, kind, endpointA, endpointB);

        private static void Validate(ProceduralConfig config, ProceduralDriverConfig driver)
        {
            foreach (float value in new[] { driver.PartitionLength, driver.PartitionThickness, driver.ShortcutWidth,
                driver.VaultHeight, driver.WindowTopHeight, driver.SlideClearance, driver.LandingOffset })
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new ArgumentException("Route module dimensions must be finite and positive.");
            if (driver.ShortcutWidth >= driver.PartitionLength ||
                driver.PartitionLength + driver.WallThickness + driver.NavSampleRadius * 2f >= config.RoomSize ||
                driver.PartitionThickness * 0.5f + driver.NavSampleRadius >= config.CakeLineOffset ||
                driver.LandingOffset <= driver.PartitionThickness * 0.5f || driver.LandingOffset >= config.CakeLineOffset ||
                driver.VaultHeight >= driver.WindowTopHeight || driver.WindowTopHeight >= config.RoomHeight ||
                driver.SlideClearance >= config.RoomHeight)
                throw new ArgumentException("Route modules must preserve both cake lanes, end bypasses and clear landings.");
        }
    }
}
