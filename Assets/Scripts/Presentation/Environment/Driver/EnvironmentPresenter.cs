// ============================================================================
// EnvironmentPresenter.cs
// ============================================================================
// PURPOSE:
//   Chooses safe wall dressing and bounded local illumination for generated rooms.
//   All placement, range selection and flicker math is deterministic without engine access.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Budget eligible lights with protected exit priority and destruction gating.
//   - Admit dressing on occupied geometry, including supplied curved shell boundaries.
//   - Preserve exact authored lights and mirrored low-ceiling corridor sockets.
//   - Compute bounded flicker, curse falloff and room-owned chalk placement.
//   - Admit wall-side furniture without fake structural arches or corner clutter.
// DEPENDENCIES:
//   - Its own definitions/state, Core interactable snapshots and Unity value math.
// USAGE NOTES:
//   Room bounds start at the walking surface, not the structural foundation.
//   Time is explicit and cosmetic variation never consumes the game's random stream.
//   Exit lights retain ordinary range/cap culling; priority prevents collapse-density starvation.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Environment
{
    public static class EnvironmentPresenter
    {
        public static bool ApplyLight(EnvironmentDriverState state, InteractableState light)
        {
            if (light.Kind != InteractableKind.Light) return false;
            for (int i = 0; i < state.Flames.Count; i++)
            {
                var flame = state.Flames[i];
                if (flame.Moon || IsExitRoomLight(state, flame) || flame.RoomId != light.RoomId || !flame.SocketPosition.Equals(light.Position)) continue;
                flame.Lit = light.Value == InteractableStateValue.Lit;
                state.Available[i] = flame.Lit && flame.Destruction < 1f;
                return true;
            }
            return false;
        }

        public static float LampBrightness(float elapsed, int identity, float gutter, float destruction,
            bool wick, bool darkerFloors, float darkerMultiplier)
            => FlameBrightness(elapsed, identity, wick ? 0f : gutter, destruction)
                * (darkerFloors ? Mathf.Clamp01(float.IsNaN(darkerMultiplier) ? 1f : darkerMultiplier) : 1f);

        public static float LocalFlameMultiplier(Vector3 flamePosition, Vector3 position, float radius, float multiplier)
        {
            if (!(radius > 0f) || float.IsInfinity(radius) || float.IsNaN(multiplier) || multiplier >= 1f) return 1f;
            float distance = (flamePosition - position).magnitude;
            if (float.IsNaN(distance) || distance >= radius) return 1f;
            float fade = Mathf.Clamp01((radius - distance) / (radius * .2f));
            return Mathf.Lerp(1f, Mathf.Clamp(multiplier, .3f, 1f), fade);
        }

        public static float CombinedFlameGutter(float globalGutter, Vector3 flamePosition, Vector3 dimPosition,
            float dimRadius, float dimMultiplier)
        {
            float local = LocalFlameMultiplier(flamePosition, dimPosition, dimRadius, dimMultiplier);
            return Mathf.Max(Mathf.Clamp01(globalGutter), (1f - local) / .7f);
        }

        public static Vector3[][] ChalkCross(Vector3 position)
        {
            Vector3 center = position + Vector3.up * .035f;
            return new[]
            {
                new[] { center + new Vector3(-.19f, 0f, -.14f), center + new Vector3(.18f, 0f, .15f) },
                new[] { center + new Vector3(-.14f, 0f, .19f), center + new Vector3(.15f, 0f, -.18f) }
            };
        }

        public static int[] ChalkRooms(Vector3 position, IReadOnlyDictionary<int, Bounds> rooms)
        {
            var owners = new List<int>(2);
            foreach (var pair in rooms)
            {
                Bounds room = pair.Value;
                if (Mathf.Abs(position.y - room.min.y) > .5f) continue;
                if (position.x >= room.min.x - .15f && position.x <= room.max.x + .15f &&
                    position.z >= room.min.z - .15f && position.z <= room.max.z + .15f) owners.Add(pair.Key);
            }
            owners.Sort(); return owners.ToArray();
        }

        public static EnvironmentSlot[] BuildDressing(int roomId, Bounds bounds, bool openSky, bool refuge,
            Vector3[] portals, Bounds[] reserved = null, IReadOnlyList<Bounds> cells = null,
            IReadOnlyList<Vector3> boundary = null, IReadOnlyList<Vector3> lightSockets = null,
            bool authoredFurniture = false, Vector3 floorEnvelope = default, Vector3 wallEnvelope = default)
        {
            if (authoredFurniture)
            {
                var lights = new List<EnvironmentSlot>();
                if (lightSockets != null)
                    foreach (var point in lightSockets) lights.Add(new EnvironmentSlot(point, 0f, true));
                else
                    foreach (var slot in BuildSlots(roomId, bounds, portals, cells)) if (slot.Torch) lights.Add(slot);
                return lights.ToArray();
            }
            if (floorEnvelope.Equals(default(Vector3))) floorEnvelope = EnvironmentDriverConfig.DefaultFloorEnvelope;
            if (wallEnvelope.Equals(default(Vector3))) wallEnvelope = EnvironmentDriverConfig.DefaultWallEnvelope;
            if (boundary != null || lightSockets != null)
            {
                var admitted = new List<EnvironmentSlot>();
                foreach (var slot in BuildDressing(roomId, bounds, openSky, refuge, portals, reserved, cells,
                    floorEnvelope: floorEnvelope, wallEnvelope: wallEnvelope))
                    if ((!slot.Torch || lightSockets == null) && FitsBoundary(slot, boundary)) admitted.Add(slot);
                if (lightSockets != null)
                    foreach (var point in lightSockets) admitted.Add(new EnvironmentSlot(point, 0f, true));
                return admitted.ToArray();
            }
            if (cells != null && cells.Count > 1)
            {
                var dressing = new List<EnvironmentSlot>();
                foreach (var cell in cells)
                    foreach (var slot in BuildDressing(roomId, cell, openSky, refuge, portals, reserved,
                        floorEnvelope: floorEnvelope, wallEnvelope: wallEnvelope))
                        if (FitsCell(slot, cell, cells)) dressing.Add(slot);
                return dressing.ToArray();
            }
            if (cells != null && cells.Count == 1) bounds = cells[0];
            var result = new List<EnvironmentSlot>(4);
            EnvironmentSlot[] walls = BuildSlots(roomId, bounds, portals);
            foreach (EnvironmentSlot slot in walls) if (slot.Torch) result.Add(slot);
            // One useful wall-side furnishing, not a random corner pot. Never add
            // structural arches or columns: those belong to the authored shell.
            EnvironmentSlot? furniture = null;
            foreach (var wall in walls)
            {
                if (wall.Torch || floorEnvelope.y > bounds.size.y) continue;
                var inward = EnvironmentPlacementPresenter.RotateYaw(Vector3.forward, wall.Yaw);
                Vector3 position = wall.Position + inward * (floorEnvelope.z * .5f - .3f);
                position.y = bounds.min.y + floorEnvelope.y * .5f;
                Vector3 worldSize = EnvironmentPlacementPresenter.RotateBounds(new Bounds(Vector3.zero, floorEnvelope), wall.Yaw).size;
                if (!ClearsFloorRoutes(position, worldSize, bounds, portals, reserved)) continue;
                furniture = new EnvironmentSlot(position, wall.Yaw,
                    refuge ? EnvironmentDecorationKind.MerchantDisplay : EnvironmentDecorationKind.FloorProp, floorEnvelope);
                result.Add(furniture.Value); break;
            }
            if (!openSky && wallEnvelope.y <= bounds.size.y)
                foreach (var wall in walls)
                {
                    if (wall.Torch || (furniture.HasValue && furniture.Value.Yaw == wall.Yaw)) continue;
                    var inward = EnvironmentPlacementPresenter.RotateYaw(Vector3.forward, wall.Yaw);
                    Vector3 position = wall.Position + inward * (wallEnvelope.z * .5f - .3f);
                    position.y = Mathf.Clamp(position.y, bounds.min.y + wallEnvelope.y * .5f, bounds.max.y - wallEnvelope.y * .5f);
                    var slot = new EnvironmentSlot(position, wall.Yaw, EnvironmentDecorationKind.Banner, wallEnvelope);
                    Vector3 worldSize = EnvironmentPlacementPresenter.RotateBounds(new Bounds(Vector3.zero, wallEnvelope), wall.Yaw).size;
                    if (!IntersectsReserved(new Bounds(position, worldSize), reserved)) { result.Add(slot); break; }
                }
            return result.ToArray();
        }

        public static bool ClearsFloorRoutes(Vector3 position, Vector3 envelope, Bounds room, Vector3[] portals, Bounds[] reserved)
        {
            if (!ClearsPortals(position, portals, 2.4f)) return false;
            // The complete world-axis footprint stays inside a perimeter band;
            // either wall suffices, rather than forcing both axes into a corner.
            Vector3 delta = position - room.center;
            if (Mathf.Abs(delta.x) + envelope.x * .5f > room.extents.x + .001f ||
                Mathf.Abs(delta.z) + envelope.z * .5f > room.extents.z + .001f) return false;
            float requiredX = room.extents.x - envelope.x * .5f;
            float requiredZ = room.extents.z - envelope.z * .5f;
            if (Mathf.Abs(delta.x) < requiredX - .001f && Mathf.Abs(delta.z) < requiredZ - .001f) return false;
            return !IntersectsReserved(new Bounds(position, envelope + Vector3.one * .12f), reserved);
        }

        private static bool IntersectsReserved(Bounds target, Bounds[] reserved)
        {
            if (reserved == null) return false;
            foreach (Bounds forbidden in reserved)
                if (target.min.x < forbidden.max.x && target.max.x > forbidden.min.x &&
                    target.min.y < forbidden.max.y && target.max.y > forbidden.min.y &&
                    target.min.z < forbidden.max.z && target.max.z > forbidden.min.z) return true;
            return false;
        }

        public static EnvironmentSlot[] BuildSlots(int roomId, Bounds bounds, Vector3[] portals, IReadOnlyList<Bounds> cells = null)
        {
            if (cells != null && cells.Count > 1)
            {
                var lighting = new List<EnvironmentSlot>();
                foreach (var cell in cells)
                    foreach (var slot in BuildSlots(roomId, cell, portals))
                        if (FitsCell(slot, cell, cells)) lighting.Add(slot);
                return lighting.ToArray();
            }
            if (cells != null && cells.Count == 1) bounds = cells[0];
            var slots = new List<EnvironmentSlot>(4);
            if (bounds.size.x < 4f || bounds.size.z < 4f || bounds.size.y < 3.2f) return slots.ToArray();
            int torchCount = 0, decorCount = 0;
            int start = (roomId & int.MaxValue) % 8;
            for (int n = 0; n < 8; n++)
            {
                int index = (start + n) % 8;
                int wall = index / 2;
                float offset = index % 2 == 0 ? -0.28f : 0.28f;
                Vector3 position = bounds.center;
                position.y = bounds.min.y + Mathf.Min(2.75f, bounds.size.y - .6f);
                float yaw;
                if (wall == 0 || wall == 2)
                {
                    position.x += bounds.size.x * offset;
                    position.z = wall == 0 ? bounds.min.z + 0.3f : bounds.max.z - 0.3f;
                    yaw = wall == 0 ? 0f : 180f;
                }
                else
                {
                    position.z += bounds.size.z * offset;
                    position.x = wall == 1 ? bounds.max.x - 0.3f : bounds.min.x + 0.3f;
                    yaw = wall == 1 ? 270f : 90f;
                }
                if (!ClearsPortals(position, portals, 2.1f)) continue;
                bool torch = n % 2 == 0 && torchCount < 2;
                if (!torch && decorCount >= 2) { if (torchCount >= 2) continue; torch = true; }
                slots.Add(new EnvironmentSlot(position, yaw, torch));
                if (torch) torchCount++; else decorCount++;
                if (torchCount == 2 && decorCount == 2) break;
            }
            return slots.ToArray();
        }

        public static bool FitsBoundary(EnvironmentSlot slot, IReadOnlyList<Vector3> polygon)
        {
            if (polygon == null) return true;
            if (polygon.Count < 3) return false;
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                var point = slot.Position + EnvironmentPlacementPresenter.RotateYaw(
                    new Vector3(x * slot.Envelope.x * .5f, 0f, z * slot.Envelope.z * .5f), slot.Yaw);
                bool inside = false;
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                {
                    // Contact with the shell is valid: wall-backed envelopes have
                    // rear corners exactly on the boundary, not strictly inside it.
                    Vector3 edge = polygon[i] - polygon[j]; edge.y = 0f;
                    Vector3 delta = point - polygon[j]; delta.y = 0f;
                    if (edge.sqrMagnitude > .000001f)
                    {
                        float along = Mathf.Clamp01(Vector3.Dot(delta, edge) / edge.sqrMagnitude);
                        if ((delta - edge * along).sqrMagnitude < .00000001f) { inside = true; break; }
                    }
                    if ((polygon[i].z > point.z) != (polygon[j].z > point.z) && point.x <
                        (polygon[j].x - polygon[i].x) * (point.z - polygon[i].z) / (polygon[j].z - polygon[i].z) + polygon[i].x) inside = !inside;
                }
                if (!inside) return false;
            }
            return true;
        }

        private static bool FitsCell(EnvironmentSlot slot, Bounds cell, IReadOnlyList<Bounds> cells)
        {
            bool rotated = Mathf.Abs(Mathf.Sin(slot.Yaw * Mathf.Deg2Rad)) > .5f;
            Vector3 half = (rotated ? new Vector3(slot.Envelope.z, slot.Envelope.y, slot.Envelope.x) : slot.Envelope) * .5f;
            Vector3 min = slot.Position - half, max = slot.Position + half;
            if (min.x < cell.min.x || max.x > cell.max.x || min.z < cell.min.z || max.z > cell.max.z) return false;
            int wall = slot.Yaw == 0f ? 0 : slot.Yaw == 270f ? 1 : slot.Yaw == 180f ? 2 : 3;
            return ExposedWall(cell, cells, wall, min, max);
        }

        private static bool ExposedWall(Bounds cell, IReadOnlyList<Bounds> cells, int wall, Vector3 min, Vector3 max)
        {
            bool x = wall == 1 || wall == 3, positive = wall == 1 || wall == 2;
            float plane = x ? (positive ? cell.max.x : cell.min.x) : (positive ? cell.max.z : cell.min.z);
            foreach (var other in cells)
            {
                if (other.Equals(cell) || other.min.y >= max.y || other.max.y <= min.y) continue;
                float low = x ? other.min.x : other.min.z, high = x ? other.max.x : other.max.z;
                bool across = positive ? low <= plane && high > plane : low < plane && high >= plane;
                bool overlap = x ? other.min.z < max.z && other.max.z > min.z : other.min.x < max.x && other.max.x > min.x;
                if (across && overlap) return false;
            }
            return true;
        }

        public static bool ClearsPortals(Vector3 point, Vector3[] portals, float clearance)
        {
            if (portals == null) return true;
            float square = clearance * clearance;
            for (int i = 0; i < portals.Length; i++)
            {
                Vector3 delta = point - portals[i]; delta.y = 0f;
                if (delta.sqrMagnitude < square) return false;
            }
            return true;
        }

        public static bool IsExitRoomLight(EnvironmentDriverState state, EnvironmentFlameDriverState flame)
            => flame.Exit || (state.ExitLightIndex >= 0 && state.ExitLightIndex < state.Flames.Count &&
                flame.RoomId == state.Flames[state.ExitLightIndex].RoomId);

        public static int[] BudgetedLights(EnvironmentDriverState state, int maximum, float distance)
        {
            int[] eligible = Nearest(state.Observer, state.Positions, state.Available, state.Flames.Count, distance);
            int torches = 0;
            foreach (int i in eligible) if (!state.Flames[i].Moon && !IsExitRoomLight(state, state.Flames[i])) torches++;
            float multiplier = float.IsNaN(state.TorchCountMultiplier) || float.IsInfinity(state.TorchCountMultiplier)
                ? 1f : Mathf.Clamp01(state.TorchCountMultiplier);
            int budget = Mathf.FloorToInt(Mathf.Min(Mathf.Max(0, maximum), torches) * multiplier);
            var visible = new List<int>();
            foreach (int i in eligible)
                if (visible.Count < maximum && IsExitRoomLight(state, state.Flames[i])) visible.Add(i);
            foreach (int i in eligible)
            {
                if (visible.Count >= maximum) break;
                if (IsExitRoomLight(state, state.Flames[i])) continue;
                if (!state.Flames[i].Moon && budget-- <= 0) continue;
                visible.Add(i);
            }
            return visible.ToArray();
        }

        public static int[] Nearest(Vector3 observer, IList<Vector3> positions, IList<bool> available, int maximum, float distance)
        {
            var indices = new List<int>();
            float square = Mathf.Max(0f, distance) * Mathf.Max(0f, distance);
            for (int i = 0; i < positions.Count; i++)
                if (available[i] && (positions[i] - observer).sqrMagnitude <= square) indices.Add(i);
            indices.Sort((a, b) =>
            {
                int order = (positions[a] - observer).sqrMagnitude.CompareTo((positions[b] - observer).sqrMagnitude);
                return order == 0 ? a.CompareTo(b) : order;
            });
            int count = Mathf.Clamp(maximum, 0, indices.Count);
            if (count < indices.Count) indices.RemoveRange(count, indices.Count - count);
            return indices.ToArray();
        }

        public static float FlameBrightness(float elapsed, int identity, float gutter, float destruction)
        {
            float phase = (identity & 255) * 0.618f;
            float flutter = 0.9f + 0.065f * Mathf.Sin(elapsed * 8.1f + phase) + 0.035f * Mathf.Sin(elapsed * 13.7f + phase * 2f);
            return flutter * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(gutter)) * (1f - Mathf.Clamp01(destruction));
        }

        public static float FitScale(Vector3 currentSize, Vector3 maximumSize, float yaw = 0f)
        {
            if (Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.5f)
                maximumSize = new Vector3(maximumSize.z, maximumSize.y, maximumSize.x);
            return EnvironmentPlacementPresenter.FitScale(currentSize, maximumSize,
                EnvironmentDriverConfig.DefaultMinimumPropScale, EnvironmentDriverConfig.DefaultMaximumPropScale);
        }
    }
}
