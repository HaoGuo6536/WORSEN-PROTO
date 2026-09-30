// ============================================================================
// ProceduralCastlePresenter.cs
// ============================================================================
// PURPOSE:
//   Gives generated castle rooms distinct playable silhouettes and elevations.
//   Ordinary stairs reach every raised cake while low routes and rebound faces
//   provide chase choices without requiring a purchase or an enemy jump ability.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Build bounded stairs, upper galleries, open cloister piers and ceiling ribs.
//   - Preserve stepped visuals over one continuous ramp and two flush landing colliders.
//   - Keep perimeter door circulation and lower bailout routes unobstructed.
//   - Support interior slide lintels with grounded end piers instead of floating panels.
//   - Keep authored furniture in the primary cell; extension cells remain open halls.
// DEPENDENCIES:
//   - Core traversal value types and this system's immutable layout/config data.
// USAGE NOTES:
//   Pure and stateless. Ramp box rotation and local size describe its top face
//   exactly; only collision-enabled boxes are baked by the Driver. Existing visual
//   treads and traversal faces retain their placement without consuming randomness.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralCastlePresenter
    {
        public IReadOnlyList<ProceduralBlock> Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            if (layout?.Graph == null || config == null || driver == null) throw new ArgumentNullException();
            var blocks = new List<ProceduralBlock>();
            foreach (var module in layout.Modules)
            {
                var room = ProceduralFootprintUtility.Volumes(layout, layout.Graph.Rooms[module.RoomId - 1])[0];
                if (module.Kind == ProceduralModuleKind.OpenStairHall || module.Kind == ProceduralModuleKind.SplitLevelLibrary ||
                    module.Kind == ProceduralModuleKind.BrokenGallery)
                {
                    // Five objectives on the upper gallery force actual elevation navigation.
                    // Keep the entire perimeter vault capsule arc clear, not only its landing feet.
                    Add(blocks, room, module, ProceduralSurfaceKind.Floor,
                        new Vector3(0f, config.UpperDeckHeight - driver.FloorThickness * 0.5f, 2.6f),
                        new Vector3(8.6f, driver.FloorThickness, 2.8f));
                    Stairs(blocks, room, module, config, driver, -3f);
                    if (module.Kind == ProceduralModuleKind.BrokenGallery) Stairs(blocks, room, module, config, driver, 3f);
                    else
                    {
                        // A face beside the stair lets the base rebound ability skip several steps.
                        Add(blocks, room, module, ProceduralSurfaceKind.Wall,
                            new Vector3(-4f, 2f, -1.1f), new Vector3(0.25f, 4f, 3.8f),
                            60000 + room.Id * 10, TraversalSurfaceKind.Rebound);
                    }
                    if (module.Kind == ProceduralModuleKind.SplitLevelLibrary)
                    {
                        Add(blocks, room, module, ProceduralSurfaceKind.Floor,
                            new Vector3(3.45f, config.UpperDeckHeight - driver.FloorThickness * 0.5f, -0.5f),
                            new Vector3(1.7f, driver.FloorThickness, 3.4f));
                        Add(blocks, room, module, ProceduralSurfaceKind.Wall,
                            new Vector3(2.45f, 1.05f, -0.5f), new Vector3(0.3f, 2.1f, 2.8f),
                            60000 + room.Id * 10 + 1, TraversalSurfaceKind.Rebound);
                    }
                }
                else if (module.Kind == ProceduralModuleKind.TorchGallery)
                {
                    // Grounded end piers carry the lintel while keeping its central slide gap.
                    foreach (float x in new[] { -2.7f, 2.7f })
                        Add(blocks, room, module, ProceduralSurfaceKind.Wall,
                            new Vector3(x, driver.SlideClearance * 0.5f, 0f),
                            new Vector3(0.6f, driver.SlideClearance, 0.6f));
                    Add(blocks, room, module, ProceduralSurfaceKind.Wall,
                        new Vector3(0f, (driver.SlideClearance + 4f) * 0.5f, 0f),
                        new Vector3(6f, 4f - driver.SlideClearance, 0.6f),
                        60000 + room.Id * 10, TraversalSurfaceKind.SlideGate,
                        Point(room, module, new Vector3(0f, 0f, -driver.LandingOffset)),
                        Point(room, module, new Vector3(0f, 0f, driver.LandingOffset)));
                }
                else if (module.Kind == ProceduralModuleKind.BrokenCloister)
                {
                    foreach (float x in new[] { -3.4f, 3.4f })
                    foreach (float z in new[] { -0.4f, 0.4f })
                        Add(blocks, room, module, ProceduralSurfaceKind.Wall,
                            new Vector3(x, 2.3f, z), new Vector3(0.6f, 4.6f, 0.6f),
                            60000 + room.Id * 10 + (x > 0f ? 2 : 0) + (z > 0f ? 1 : 0), TraversalSurfaceKind.Rebound);
                }
                // Vaulted ribs give even safe rooms depth while preserving all standing lanes.
                if (module.Kind != ProceduralModuleKind.BrokenCloister && module.Kind != ProceduralModuleKind.BrokenGallery)
                    foreach (float offset in new[] { -4f, 0f, 4f })
                        Add(blocks, room, module, ProceduralSurfaceKind.Ceiling,
                            new Vector3(offset, room.Size.y - 0.35f, 0f), new Vector3(0.4f, 0.7f, room.Size.z));
            }
            return blocks.AsReadOnly();
        }

        private static void Stairs(List<ProceduralBlock> blocks, LevelRoom room, ProceduralRoomModule module,
            ProceduralConfig config, ProceduralDriverConfig driver, float x)
        {
            if (float.IsNaN(driver.StairLandingExtension) || float.IsInfinity(driver.StairLandingExtension) ||
                driver.StairLandingExtension <= 0f || driver.StairLandingExtension >=
                    config.RoomSize * 0.5f - 3.6f - driver.WallThickness * 0.5f - driver.NavSampleRadius ||
                float.IsNaN(driver.FloorThickness) || float.IsInfinity(driver.FloorThickness) || driver.FloorThickness <= 0f)
                throw new ArgumentException("Stair landings need finite positive dimensions within the perimeter walking lane.");
            int count = Mathf.CeilToInt(config.UpperDeckHeight / 0.2f);
            float tread = 4.8f / count;
            for (int index = 0; index < count; index++)
            {
                float top = (index + 1f) * config.UpperDeckHeight / count;
                Add(blocks, room, module, ProceduralSurfaceKind.Floor,
                    new Vector3(x, top * 0.5f, -3.6f + (index + 0.5f) * tread),
                    new Vector3(2f, top, tread), role: ProceduralBlockRole.VisualOnly);
            }
            var start = Point(room, module, new Vector3(x, 0f, -3.6f));
            var end = Point(room, module, new Vector3(x, config.UpperDeckHeight, 1.2f));
            var rotation = Quaternion.LookRotation(end - start, Vector3.up);
            // Offset along the face normal so thickness never raises the walkable surface.
            blocks.Add(new ProceduralBlock(room.Id, ProceduralSurfaceKind.Floor,
                (start + end) * 0.5f - rotation * Vector3.up * (driver.FloorThickness * 0.5f),
                new Vector3(2f, driver.FloorThickness, Vector3.Distance(start, end)),
                endpointA: start, endpointB: end, role: ProceduralBlockRole.StairRamp, rotation: rotation));
            float extension = driver.StairLandingExtension;
            Add(blocks, room, module, ProceduralSurfaceKind.Floor,
                new Vector3(x, -driver.FloorThickness * 0.5f, -3.6f - extension * 0.5f),
                new Vector3(2f, driver.FloorThickness, extension), role: ProceduralBlockRole.StairLanding);
            Add(blocks, room, module, ProceduralSurfaceKind.Floor,
                new Vector3(x, config.UpperDeckHeight - driver.FloorThickness * 0.5f, 1.2f + extension * 0.5f),
                new Vector3(2f, driver.FloorThickness, extension), role: ProceduralBlockRole.StairLanding);
        }
        private static Vector3 Point(LevelRoom room, ProceduralRoomModule module, Vector3 local)
            => new Vector3(room.Center.x, 0f, room.Center.z) + (module.AlongX ? local : new Vector3(local.z, local.y, local.x));
        private static void Add(List<ProceduralBlock> blocks, LevelRoom room, ProceduralRoomModule module,
            ProceduralSurfaceKind kind, Vector3 local, Vector3 size, int surfaceId = 0,
            TraversalSurfaceKind traversal = TraversalSurfaceKind.None, Vector3 endpointA = default, Vector3 endpointB = default,
            ProceduralBlockRole role = ProceduralBlockRole.Solid)
            => blocks.Add(new ProceduralBlock(room.Id, kind, Point(room, module, local),
                module.AlongX ? size : new Vector3(size.z, size.y, size.x), surfaceId, traversal, endpointA, endpointB, role));
    }
}
