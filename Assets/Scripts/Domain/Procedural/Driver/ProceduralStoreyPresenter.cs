// ============================================================================
// ProceduralStoreyPresenter.cs
// ============================================================================
// PURPOSE:
//   Builds upper-floor slabs with actual openings rather than painting holes over
//   collision. A continuous hunter ramp and untagged two-stage ledges reach the
//   upper floor independently; four drop silhouettes return to supported ground.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Reserve stair/climb wells, partition the upper slab and build physical drops.
//   - Mark staging and collapsed ramps non-walkable while retaining their collision.
//   - Reject unsupported route endpoints and blocked landing volumes before admission.
// DEPENDENCIES:
//   - Own layout/config values and Core traversal definitions; no engine calls.
// USAGE NOTES:
//   Fixed sockets describe the 8m square module, inset inside a minimum 12m cell.
//   The 2m perimeter remains clear for existing doors, window arcs and lower anchors.
//   Both storeys keep the original room id, so destruction moves the whole building.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralStoreyPresenter
    {
        public IReadOnlyList<ProceduralBlock> Build(ProceduralLayout layout, ProceduralConfig config, ProceduralDriverConfig driver)
        {
            var blocks = new List<ProceduralBlock>();
            foreach (var s in layout.Storeys)
            {
                float h = s.Height, t = driver.FloorThickness;
                if (t <= 0f || t > h - 2f || driver.StairLandingExtension <= 0f || driver.StairLandingExtension > 1f)
                    throw new ArgumentException("Storey floor thickness/landing extension obstructs standing circulation.");
                bool hole = s.Drop == ProceduralVerticalKind.FloorHole || s.Drop == ProceduralVerticalKind.Shaft;
                // Exact cut boundaries; no overlapping slab fills a well or drop opening.
                float[] xs = { -4f, -2f, -0.8f, 0.8f, 2f, 4f }, zs = { -4f, -1f, 0f, 1.2f, 2.6f, 4f };
                for (int x = 1; x < xs.Length; x++)
                for (int z = 1; z < zs.Length; z++)
                {
                    float cx = (xs[x - 1] + xs[x]) * 0.5f, cz = (zs[z - 1] + zs[z]) * 0.5f;
                    if (cx < -2f && cz < 1.2f || cx > 2f && cz < 0f ||
                        Mathf.Abs(cx) < 0.8f && (hole ? cz > 1.2f && cz < 2.6f : cz < -1f)) continue;
                    Box(ProceduralSurfaceKind.Floor, new Vector3(cx, h - t * 0.5f, cz),
                        new Vector3(xs[x] - xs[x - 1], t, zs[z] - zs[z - 1]));
                }
                var ramp = layout.VerticalRoutes.Single(r => r.RoomId == s.RoomId && r.Kind == ProceduralVerticalKind.Ramp);
                Ramp(ramp.Points[0], ramp.Points[1], 2f, ProceduralBlockRole.StairRamp);
                float extension = driver.StairLandingExtension;
                foreach (bool upper in new[] { false, true })
                    Box(ProceduralSurfaceKind.Floor, new Vector3(-3f, (upper ? h : 0f) - t * 0.5f,
                        (upper ? 1.2f : -3.6f) + extension * (upper ? 0.5f : -0.5f)),
                        new Vector3(2f, t, extension), ProceduralBlockRole.StairLanding);
                // The ramp is also visible; the invisible collision role retains existing semantics.
                var physicalRamp = blocks[blocks.Count - 3];
                blocks.Add(new ProceduralBlock(s.RoomId, physicalRamp.Kind, physicalRamp.Center, physicalRamp.Size,
                    role: ProceduralBlockRole.VisualOnly, rotation: physicalRamp.Rotation));
                Box(ProceduralSurfaceKind.Floor, new Vector3(3f, h * 0.25f, -1f),
                    new Vector3(2f, h * 0.5f, 2f), ProceduralBlockRole.PlayerOnly);
                // Give the final ledge a chest-height untagged face, not only a thin slab lip.
                Box(ProceduralSurfaceKind.Wall, new Vector3(3f, h * 0.75f, 0f), new Vector3(2f, h * 0.5f, 0.1f));
                if (config.BaseReboundSupported)
                    Box(ProceduralSurfaceKind.Wall, new Vector3(1.75f, h * 0.5f, -1.1f), new Vector3(0.3f, h, 1.8f),
                        id: 95000 + s.RoomId * 10, traversal: TraversalSurfaceKind.Rebound);
                if (s.Drop == ProceduralVerticalKind.Shaft)
                    foreach (float x in new[] { -0.95f, 0.95f })
                        Box(ProceduralSurfaceKind.Wall, new Vector3(x, (h - t) * 0.5f, 1.9f), new Vector3(0.3f, h - t, 1.4f));
                if (s.Drop == ProceduralVerticalKind.Balcony)
                {
                    var drop = layout.VerticalRoutes.Single(r => r.RoomId == s.RoomId && r.Kind == s.Drop);
                    Box(ProceduralSurfaceKind.Wall, new Vector3(0f, h + driver.VaultHeight * 0.5f, -1f),
                        new Vector3(2f, driver.VaultHeight, driver.WallThickness), id: drop.Id,
                        traversal: TraversalSurfaceKind.Vault, a: drop.Points[0], b: drop.Points[1]);
                }
                if (s.Drop == ProceduralVerticalKind.CollapsedRamp)
                    Ramp(s.Origin + new Vector3(0f, h, -1f), s.Origin + new Vector3(0f, h * 0.5f, -2.5f),
                        2f, ProceduralBlockRole.PlayerOnly);

                void Box(ProceduralSurfaceKind kind, Vector3 local, Vector3 size,
                    ProceduralBlockRole role = ProceduralBlockRole.Solid, int id = 0,
                    TraversalSurfaceKind traversal = TraversalSurfaceKind.None, Vector3 a = default, Vector3 b = default)
                    => blocks.Add(new ProceduralBlock(s.RoomId, kind, s.Origin + local, size, id, traversal, a, b, role));
                void Ramp(Vector3 a, Vector3 b, float width, ProceduralBlockRole role)
                {
                    var rotation = Quaternion.LookRotation(b - a, Vector3.up);
                    blocks.Add(new ProceduralBlock(s.RoomId, ProceduralSurfaceKind.Floor,
                        (a + b) * 0.5f - rotation * Vector3.up * (t * 0.5f), new Vector3(width, t, Vector3.Distance(a, b)),
                        endpointA: a, endpointB: b, role: role, rotation: rotation));
                }
            }
            return blocks.AsReadOnly();
        }

        public void ValidateLandings(ProceduralLayout layout, IReadOnlyList<ProceduralBlock> blocks)
        {
            foreach (var route in layout.VerticalRoutes)
            foreach (var point in route.Kind == ProceduralVerticalKind.LedgeClimb || route.Kind == ProceduralVerticalKind.ReboundClimb ?
                route.Points : new[] { route.Points[0], route.Points.Last() })
            {
                if (!blocks.Any(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Floor && Contains(b, point - Vector3.up * 0.01f)))
                    throw new InvalidOperationException("Vertical route has no physical landing: " + route.Id);
                // Upright capsule clearance sampled conservatively as a box at each endpoint.
                var body = new Bounds(point + Vector3.up * 1.01f, new Vector3(0.6f, 2f, 0.6f));
                foreach (var block in blocks.Where(b => b.HasCollision && b.Rotation == Quaternion.identity))
                    if (new Bounds(block.Center, block.Size).Intersects(body))
                        throw new InvalidOperationException("Vertical route landing is obstructed: " + route.Id);
            }
        }
        private static bool Contains(ProceduralBlock b, Vector3 p)
            => new Bounds(Vector3.zero, b.Size).Contains(Quaternion.Inverse(b.Rotation) * (p - b.Center));
    }
}
