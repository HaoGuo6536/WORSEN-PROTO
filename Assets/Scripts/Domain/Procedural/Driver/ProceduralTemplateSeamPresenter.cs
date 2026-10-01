// ============================================================================
// ProceduralTemplateSeamPresenter.cs
// ============================================================================
// PURPOSE:
//   Resolves touching template shells into one physical shared wall. Authored
//   exterior offsets are valid for isolated rooms but cannot become a second
//   wall inside a neighbour; only the touching intervals are replaced.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Find shared metre-cell boundaries without rounding authored placements.
//   - Clip duplicate straight walls and retain unshared collision intervals.
//   - Emit one two-sided primitive seam with the actual connected door apertures.
// DEPENDENCIES:
//   - Own template/layout definitions and pure coordinate utility.
// USAGE NOTES:
//   Curved walls and furniture are never removed. The placement admission utility
//   reserves their physical envelopes. A cut mesh is replaced, never stretched.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralTemplateSeamPresenter
    {
        public List<ProceduralBlock> Build(ProceduralLayout layout, IReadOnlyList<ProceduralBlock> source, ProceduralConfig config)
        {
            var result = source.ToList();
            var shared = new List<ProceduralBlock>();
            var owners = new Dictionary<Vector2Int, int>();
            foreach (var room in layout.TemplateRooms)
                foreach (var cell in ProceduralTemplateUtility.OccupiedCells(room)) owners.Add(cell, room.RoomId);
            var kits = layout.TemplateRooms.ToDictionary(r => r.RoomId, r => (r.Catalogue ?? layout.TemplateCatalogue).Kit.ToDictionary(p => p.Id));
            var seams = new List<(int a, int b, bool alongX, float plane, float low)>();
            foreach (var pair in owners.OrderBy(p => p.Key.x).ThenBy(p => p.Key.y))
            foreach (var direction in new[] { Vector2Int.right, Vector2Int.up })
            {
                if (!owners.TryGetValue(pair.Key + direction, out int other) || other == pair.Value) continue;
                bool alongX = direction.y != 0;
                seams.Add((Math.Min(pair.Value, other), Math.Max(pair.Value, other), alongX,
                    alongX ? pair.Key.y + 1f + layout.Origin.y : pair.Key.x + 1f + layout.Origin.x,
                    alongX ? pair.Key.x + layout.Origin.x : pair.Key.y + layout.Origin.y));
            }
            // Merge runs before clipping so door frames are not reintroduced as
            // solid boxes one subcell at a time.
            foreach (var group in seams.GroupBy(s => (s.a, s.b, s.alongX, s.plane)))
            {
                var sorted = group.Select(s => s.low).OrderBy(v => v).ToArray();
                int start = 0;
                for (int end = 1; end <= sorted.Length; end++)
                {
                    if (end < sorted.Length && Math.Abs(sorted[end] - sorted[end - 1] - 1f) < .001f) continue;
                    Resolve(group.Key.a, group.Key.b, group.Key.alongX, group.Key.plane, sorted[start], sorted[end - 1] + 1f);
                    start = end;
                }
            }
            result.AddRange(shared);
            return result;

            void Resolve(int a, int b, bool alongX, float plane, float low, float high)
            {
                int axis = alongX ? 0 : 2, across = alongX ? 2 : 0;
                float thickness = 0f;
                var next = new List<ProceduralBlock>();
                foreach (var block in result)
                {
                    bool straight = block.Kind == ProceduralSurfaceKind.Wall && (block.RoomId == a || block.RoomId == b) &&
                        (block.PieceId == null || kits[block.RoomId].TryGetValue(block.PieceId, out var piece) &&
                         (piece.Kind == "wall" || piece.Kind == "door" || piece.Kind == "window") && piece.Id != "wall_round_tangent_r4");
                    var tangent = block.Rotation * Vector3.right;
                    if (!straight || Math.Abs(tangent[axis]) < .999f ||
                        Math.Abs(block.Center[across] - plane) > block.Size.z * .5f + .001f ||
                        block.Center[axis] + block.Size.x * .5f <= low + .001f || block.Center[axis] - block.Size.x * .5f >= high - .001f)
                    { next.Add(block); continue; }
                    thickness = Math.Max(thickness, block.Size.z);
                    if (block.Role == ProceduralBlockRole.KitVisual) continue;
                    float min = block.Center[axis] - block.Size.x * .5f, max = block.Center[axis] + block.Size.x * .5f;
                    Part(min, Math.Min(max, low)); Part(Math.Max(min, high), max);
                    void Part(float from, float to)
                    {
                        if (to - from <= .001f) return;
                        var center = block.Center; center[axis] = (from + to) * .5f;
                        next.Add(new ProceduralBlock(block.RoomId, block.Kind, center,
                            new Vector3(to - from, block.Size.y, block.Size.z), rotation: block.Rotation));
                    }
                }
                result = next;
                if (thickness == 0f) return; // Raster adjacency alone is not a curved-wall seam.
                float height = Math.Max(layout.TemplateRooms.Single(r => r.RoomId == a).Template.Height,
                    layout.TemplateRooms.Single(r => r.RoomId == b).Template.Height);
                if (layout.Graph != null) height = Math.Max(height, layout.Graph.Rooms.Where(r => r.Id == a || r.Id == b).Max(r => r.Bounds.max.y));
                var openings = (layout.Doors ?? Array.Empty<ProceduralDoorPlan>()).Where(d => !d.IsOptional &&
                    ((d.FromRoomId == a && d.ToRoomId == b) || (d.FromRoomId == b && d.ToRoomId == a)) &&
                    d.AlongX == alongX && Math.Abs(d.Center[across] - plane) < .001f)
                    .Select(d => d.Center[axis]).OrderBy(v => v).ToArray();
                float cursor = low;
                foreach (float door in openings)
                {
                    float from = Math.Max(cursor, door - config.DoorWidth * .5f), to = Math.Min(high, door + config.DoorWidth * .5f);
                    if (to <= from) continue;
                    Wall(cursor, from, 0f); Wall(from, to, config.DoorHeight); cursor = to;
                }
                Wall(cursor, high, 0f);
                void Wall(float from, float to, float bottom)
                {
                    if (to - from <= .001f || height <= bottom) return;
                    var center = Vector3.up * ((height + bottom) * .5f); center[axis] = (from + to) * .5f; center[across] = plane;
                    shared.Add(new ProceduralBlock(a, ProceduralSurfaceKind.Wall, center,
                        alongX ? new Vector3(to - from, height - bottom, thickness) : new Vector3(thickness, height - bottom, to - from)));
                }
            }
        }
    }
}
