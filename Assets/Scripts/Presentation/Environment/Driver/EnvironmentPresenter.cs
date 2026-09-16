// ============================================================================
// EnvironmentPresenter.cs
// ============================================================================
// PURPOSE:
//   Chooses safe wall dressing and bounded local illumination for generated rooms.
//   All placement, range selection and flicker math is deterministic without engine access.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Keep decoration above running lanes and away from door apertures.
//   - Select the nearest effects under fixed budgets and preserve a readable flame minimum.
//   - Compute local flame falloff and bounded chalk crosses with room ownership.
// DEPENDENCIES:
//   - Its own definitions and Unity value math; no other systems.
// USAGE NOTES:
//   Room bounds start at the walking surface, not the structural foundation.
//   Time is explicit and cosmetic variation never consumes the game's random stream.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Presentation.Environment
{
    public static class EnvironmentPresenter
    {
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
            Vector3[] portals, Bounds[] reserved = null)
        {
            var result = new List<EnvironmentSlot>(6);
            EnvironmentSlot[] walls = BuildSlots(roomId, bounds, portals);
            foreach (EnvironmentSlot slot in walls) if (slot.Torch) result.Add(slot);
            if (bounds.size.x < 10f || bounds.size.z < 10f || bounds.size.y < 5.5f)
            { foreach (EnvironmentSlot slot in walls) if (!slot.Torch) result.Add(slot); return result.ToArray(); }
            // Decorative arch stays fully above a standing doorway. Its curved timber
            // and side braces read as a wall-backed facade, never a lower fake doorway.
            if (portals != null && portals.Length > 0)
            {
                Vector3 portal = portals[(roomId & int.MaxValue) % portals.Length];
                Vector3 delta = portal - bounds.center;
                bool xWall = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
                float yaw = xWall ? (delta.x > 0f ? 270f : 90f) : (delta.z > 0f ? 180f : 0f);
                Vector3 position = portal;
                position.y = Mathf.Min(bounds.max.y - 1.15f, bounds.min.y + 4.1f);
                position += xWall ? Vector3.right * (delta.x > 0 ? -.12f : .12f) : Vector3.forward * (delta.z > 0 ? -.12f : .12f);
                result.Add(new EnvironmentSlot(position, yaw, EnvironmentDecorationKind.Arch, new Vector3(4.6f, 2.2f, .42f)));
            }
            // Corner-only columns leave the central/stair spaces and two-metre door
            // approaches untouched. Different corner choices give stable room identity.
            int start = (roomId & int.MaxValue) % 4;
            for (int i = 0; i < 4 && result.Count < (openSky ? 5 : 4); i++)
            {
                int corner = (start + i) % 4;
                Vector3 position = Corner(bounds, corner, .34f);
                position.y = bounds.min.y + 2f;
                if (!ClearsPortals(position, portals, 2.1f)) continue;
                var envelope = new Vector3(.42f, 4f, .42f);
                if (IntersectsReserved(new Bounds(position, envelope), reserved)) continue;
                result.Add(new EnvironmentSlot(position, 0f, EnvironmentDecorationKind.Column, envelope));
            }
            if (!openSky)
                foreach (EnvironmentSlot slot in walls)
                    if (!slot.Torch && result.Count < 5) { result.Add(slot); break; }
            for (int i = 0; i < 4 && result.Count < 6; i++)
            {
                Vector3 position = Corner(bounds, (start + i + 2) % 4, .75f);
                var envelope = new Vector3(.65f, refuge ? 1.1f : 1.5f, .65f);
                position.y = bounds.min.y + envelope.y * .5f;
                if (!ClearsFloorRoutes(position, envelope, bounds, portals, reserved)) continue;
                bool overlaps = false;
                foreach (EnvironmentSlot placed in result)
                    if (placed.Kind == EnvironmentDecorationKind.Column &&
                        IntersectsReserved(new Bounds(position, envelope), new[] { new Bounds(placed.Position, placed.Envelope) })) overlaps = true;
                if (overlaps) continue;
                result.Add(new EnvironmentSlot(position, (roomId % 4) * 90f,
                    refuge ? EnvironmentDecorationKind.MerchantDisplay : EnvironmentDecorationKind.FloorProp, envelope));
                break;
            }
            return result.ToArray();
        }

        private static Vector3 Corner(Bounds bounds, int corner, float inset)
        { return new Vector3(corner % 2 == 0 ? bounds.min.x + inset : bounds.max.x - inset,
            bounds.min.y, corner < 2 ? bounds.min.z + inset : bounds.max.z - inset); }

        public static bool ClearsFloorRoutes(Vector3 position, Vector3 envelope, Bounds room, Vector3[] portals, Bounds[] reserved)
        {
            if (!ClearsPortals(position, portals, 2.4f)) return false;
            // Keep all possible rotated stairs, upper landing approaches and center
            // circulation clear. Only the outermost corner pockets are eligible.
            Vector3 delta = position - room.center;
            float requiredX = room.extents.x - 1.12f + envelope.x * .5f;
            float requiredZ = room.extents.z - 1.12f + envelope.z * .5f;
            if (Mathf.Abs(delta.x) < requiredX || Mathf.Abs(delta.z) < requiredZ) return false;
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

        public static EnvironmentSlot[] BuildSlots(int roomId, Bounds bounds, Vector3[] portals)
        {
            var slots = new List<EnvironmentSlot>(4);
            if (bounds.size.x < 5f || bounds.size.z < 5f || bounds.size.y < 3.4f) return slots.ToArray();
            int torchCount = 0, decorCount = 0;
            int start = (roomId & int.MaxValue) % 8;
            for (int n = 0; n < 8; n++)
            {
                int index = (start + n) % 8;
                int wall = index / 2;
                float offset = index % 2 == 0 ? -0.28f : 0.28f;
                Vector3 position = bounds.center;
                position.y = bounds.min.y + 2.75f;
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
            float scale = 1f;
            if (currentSize.x > 0.001f) scale = Mathf.Min(scale, maximumSize.x / currentSize.x);
            if (currentSize.y > 0.001f) scale = Mathf.Min(scale, maximumSize.y / currentSize.y);
            if (currentSize.z > 0.001f) scale = Mathf.Min(scale, maximumSize.z / currentSize.z);
            return Mathf.Max(0.001f, scale);
        }
    }
}
