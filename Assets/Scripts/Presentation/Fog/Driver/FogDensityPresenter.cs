// ============================================================================
// FogDensityPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a bounded, floor-weighted density field from published room facts.
//   Diffusion is a compact one-hop portal kernel, never a wall-crossing box blur.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Build room voxel ownership once and rebuild only changed rooms and neighbors.
//   - Blend matching portal mouths, floor tendrils and room-local collapse density.
// DEPENDENCIES:
//   - Core GeneratedRoomSample/LevelGraph; own DriverState and DriverConfig.
// USAGE NOTES:
//   Pure and deterministic. No time or random source is needed. Graph traversal
//   direction restricts actors, not fog. Only shared axis-aligned wall openings
//   are resolved; unmatched edges are counted, not guessed. A one-voxel empty
//   guard beside solid walls prevents trilinear sampling leaking through them.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Fog
{
    public static class FogDensityPresenter
    {
        public static FogDriverState Build(IReadOnlyList<GeneratedRoomSample> rooms, LevelGraph graph, FogDriverConfig config)
        {
            var state = new FogDriverState();
            if (rooms == null || rooms.Count == 0) return state;
            if (graph == null || config == null) throw new ArgumentNullException(graph == null ? nameof(graph) : nameof(config));
            state.Bounds = rooms[0].Bounds;
            foreach (GeneratedRoomSample sample in rooms)
            {
                Vector3 size = sample.Bounds.size;
                if (!Finite(size.x) || !Finite(size.y) || !Finite(size.z) || size.x <= 0 || size.y <= 0 || size.z <= 0 ||
                    !Finite(sample.Bounds.center.x) || !Finite(sample.Bounds.center.y) || !Finite(sample.Bounds.center.z))
                    throw new ArgumentException("Fog rooms require finite positive bounds.");
                state.Rooms.Add(sample.RoomId, new FogRoomDriverState { Sample = sample });
                state.Bounds.Encapsulate(sample.Bounds);
            }
            Vector3 extent = state.Bounds.size;
            Vector3Int cap = config.GridCap;
            float horizontal = Mathf.Max(config.HorizontalVoxel, Mathf.Max(extent.x / cap.x, extent.z / cap.z));
            float vertical = Mathf.Max(config.VerticalVoxel, extent.y / cap.y);
            state.Size = new Vector3Int(Cells(extent.x, horizontal, cap.x), Cells(extent.y, vertical, cap.y), Cells(extent.z, horizontal, cap.z));
            state.Voxel = new Vector3(extent.x / state.Size.x, extent.y / state.Size.y, extent.z / state.Size.z);
            state.Density = new byte[state.Size.x * state.Size.y * state.Size.z];
            // Stable ID ordering makes boundary ownership independent of input ordering.
            var ids = new List<int>(state.Rooms.Keys); ids.Sort();
            for (int i = 0; i < state.Density.Length; i++)
            {
                Vector3 point = Position(state, i);
                foreach (int id in ids)
                    if (state.Rooms[id].Sample.Bounds.Contains(point)) { state.Rooms[id].Voxels.Add(i); break; }
            }
            foreach (LevelEdge edge in graph.Edges)
            {
                if (!state.Rooms.TryGetValue(edge.FromRoomId, out var a) || !state.Rooms.TryGetValue(edge.ToRoomId, out var b)) continue;
                int matches = 0;
                foreach (Vector3 pa in a.Sample.PortalCenters ?? Array.Empty<Vector3>())
                    foreach (Vector3 pb in b.Sample.PortalCenters ?? Array.Empty<Vector3>())
                    {
                        if ((pa - pb).sqrMagnitude > config.PortalMatchTolerance * config.PortalMatchTolerance) continue;
                        Vector3 center = (pa + pb) * .5f;
                        if (!SharedFace(a.Sample.Bounds, b.Sample.Bounds, center, config.PortalMatchTolerance, out int axis, out int side)) continue;
                        AddPortal(a, b.Sample.RoomId, center, axis, side);
                        AddPortal(b, a.Sample.RoomId, center, axis, -side);
                        matches++;
                    }
                if (matches == 0) state.UnmatchedEdges++;
            }
            state.UploadPending = true;
            return state;
        }

        public static bool SetProgress(FogDriverState state, int id, float progress, FogDriverConfig config)
        {
            if (!Finite(progress) || !state.Rooms.TryGetValue(id, out var room)) return false;
            progress = Mathf.Clamp01(progress);
            if (progress == room.Progress || (progress != 0f && progress != 1f && Mathf.Abs(progress - room.Progress) <= config.ProgressEpsilon)) return false;
            room.Progress = progress;
            state.DirtyRooms.Add(id);
            foreach (var portal in room.Portals) state.DirtyRooms.Add(portal.Neighbor);
            return true;
        }

        public static void Rebuild(FogDriverState state, FogDriverConfig config, bool full = false)
        {
            if (full) foreach (int id in state.Rooms.Keys) state.DirtyRooms.Add(id);
            state.LastRebuiltRooms = state.DirtyRooms.Count;
            foreach (int id in state.DirtyRooms)
                foreach (int index in state.Rooms[id].Voxels)
                    state.Density[index] = (byte)Mathf.RoundToInt(255f * Evaluate(state, id, Position(state, index), config));
            if (state.DirtyRooms.Count > 0) state.UploadPending = true;
            state.DirtyRooms.Clear();
        }

        public static float Evaluate(FogDriverState state, int id, Vector3 point, FogDriverConfig config)
        {
            if (!state.Rooms.TryGetValue(id, out var room) || !room.Sample.Bounds.Contains(point)) return 0f;
            Bounds bounds = room.Sample.Bounds;
            float height = point.y - bounds.min.y;
            float vertical = Mathf.Pow(Mathf.Clamp01(1f - height / bounds.size.y), config.VerticalFalloff);
            float wave = .5f + .5f * Mathf.Sin(point.x * (2f * Mathf.PI / config.TendrilWavelength) + Mathf.Sin(point.z * (2f * Mathf.PI / config.TendrilWavelength)));
            float body = room.Progress * vertical * (1f - config.TendrilStrength * (1f - room.Progress) * wave);
            float density = body;
            foreach (var portal in room.Portals)
            {
                float neighbor = state.Rooms[portal.Neighbor].Progress;
                float reach = config.LeakDistance * (1f + room.Progress * config.LeakGrowth);
                float distance = Mathf.Abs(point[portal.Axis] - portal.Center[portal.Axis]);
                float kernel = Smooth(1f - distance / reach);
                float profile = Mouth(point, portal, bounds, config);
                // Both sides have the same value at the opening. Own progress expands
                // the leak inward, but leaked density never seeds another graph hop.
                density = Mathf.Max(density, Mathf.Max(room.Progress, neighbor) * profile * kernel);
            }
            float wall = 1f;
            for (int axis = 0; axis <= 2; axis += 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    float distance = side < 0 ? point[axis] - bounds.min[axis] : bounds.max[axis] - point[axis];
                    float opening = 0f;
                    foreach (var portal in room.Portals)
                        if (portal.Axis == axis && portal.Side == side) opening = Mathf.Max(opening, Mouth(point, portal, bounds, config));
                    float guard = Smooth((distance - state.Voxel[axis]) / state.Voxel[axis]);
                    wall = Mathf.Min(wall, Mathf.Max(guard, opening));
                }
            // No ceiling attachment even when the authored opening reaches the roof.
            float ceiling = Mathf.Clamp01((bounds.max.y - point.y) / state.Voxel.y);
            return Mathf.Clamp01(density * wall * ceiling);
        }

        public static Vector3 Position(FogDriverState state, int index)
        {
            int x = index % state.Size.x;
            int y = index / state.Size.x % state.Size.y;
            int z = index / (state.Size.x * state.Size.y);
            return state.Bounds.min + Vector3.Scale(new Vector3(x + .5f, y + .5f, z + .5f), state.Voxel);
        }

        private static float Mouth(Vector3 point, FogPortalDriverState portal, Bounds bounds, FogDriverConfig config)
        {
            float height = point.y - Mathf.Max(bounds.min.y, portal.Center.y);
            float maximum = Mathf.Min(config.PortalHeight, bounds.max.y - Mathf.Max(bounds.min.y, portal.Center.y));
            if (height < 0f || height >= maximum || maximum <= 0f) return 0f;
            float across = (point[2 - portal.Axis] - portal.Center[2 - portal.Axis]) / (config.PortalWidth * .5f);
            float arch = Mathf.Max(0f, (height - maximum * .5f) / (maximum * .5f));
            return Smooth((1f - across * across - arch * arch) / config.MouthSoftness);
        }

        private static bool SharedFace(Bounds a, Bounds b, Vector3 point, float tolerance, out int axis, out int side)
        {
            for (int candidate = 0; candidate <= 2; candidate += 2)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float face = sign < 0 ? a.min[candidate] : a.max[candidate];
                    float other = sign < 0 ? b.max[candidate] : b.min[candidate];
                    Bounds expandedA = a, expandedB = b;
                    expandedA.Expand(tolerance * 2f); expandedB.Expand(tolerance * 2f);
                    if (Mathf.Abs(face - other) <= tolerance && Mathf.Abs(point[candidate] - face) <= tolerance && expandedA.Contains(point) && expandedB.Contains(point))
                    { axis = candidate; side = sign; return true; }
                }
            axis = 0; side = 0; return false;
        }

        private static void AddPortal(FogRoomDriverState room, int neighbor, Vector3 center, int axis, int side)
        {
            foreach (var existing in room.Portals)
                if (existing.Neighbor == neighbor && existing.Center == center) return;
            room.Portals.Add(new FogPortalDriverState { Neighbor = neighbor, Center = center, Axis = axis, Side = side });
        }
        private static int Cells(float extent, float voxel, int cap) => Mathf.Clamp(Mathf.CeilToInt(extent / voxel), 1, cap);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
    }
}
