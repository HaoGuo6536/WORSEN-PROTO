// ============================================================================
// RoomCollapsePresenter.cs
// ============================================================================
// PURPOSE:
//   Calculates clipped fog advance, five-finger reveal, and deterministic surface fissures.
//   Boundary planes and cake reach are independent of visual hand activation.
//   Collapsing rooms use a boundary shell; only consumed rooms include their full interior.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Probe exposed cell edges only; exclude notches and internal cell seams.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public sealed class RoomCollapsePresenter
    {
        public FloorHandProbe BoundaryProbe(LevelRoom room, RoomPhase phase, Vector3 feet, float reach, int preferredFace = -1)
        {
            if (room.Cells.Count == 1) return BoundaryProbe(room.Cells[0], room.Id, phase, feet, reach, preferredFace);
            if (phase != RoomPhase.Tearing && phase != RoomPhase.Encroaching && phase != RoomPhase.Closed) return default;
            bool inside = room.ContainsXZ(feet);
            var bounds = room.Bounds;
            if (!inside && feet.x >= bounds.min.x && feet.x <= bounds.max.x &&
                feet.z >= bounds.min.z && feet.z <= bounds.max.z) return default;
            FloorHandProbe closest = default;
            float nearest = float.PositiveInfinity;
            for (int cellIndex = 0; cellIndex < room.Cells.Count; cellIndex++)
            {
                var cell = room.Cells[cellIndex];
                if (feet.y < cell.min.y || feet.y >= cell.max.y) continue;
                for (int face = 0; face < 4; face++)
                {
                    int identity = cellIndex * 4 + face;
                    if (preferredFace >= 0 && identity != preferredFace) continue;
                    bool xFace = face < 2, positive = face % 2 == 1;
                    float plane = xFace ? (positive ? cell.max.x : cell.min.x) : (positive ? cell.max.z : cell.min.z);
                    var spans = new List<Vector2> { new Vector2(xFace ? cell.min.z : cell.min.x, xFace ? cell.max.z : cell.max.x) };
                    for (int otherIndex = 0; otherIndex < room.Cells.Count; otherIndex++)
                    {
                        if (otherIndex == cellIndex) continue;
                        var other = room.Cells[otherIndex];
                        if (feet.y < other.min.y || feet.y >= other.max.y) continue;
                        float min = xFace ? other.min.x : other.min.z, max = xFace ? other.max.x : other.max.z;
                        if (!(positive ? min <= plane && max > plane : min < plane && max >= plane)) continue;
                        float low = xFace ? other.min.z : other.min.x, high = xFace ? other.max.z : other.max.x;
                        for (int s = spans.Count - 1; s >= 0; s--)
                        {
                            var span = spans[s];
                            if (low >= span.y || high <= span.x) continue;
                            spans.RemoveAt(s);
                            if (low > span.x) spans.Add(new Vector2(span.x, low));
                            if (high < span.y) spans.Add(new Vector2(high, span.y));
                        }
                    }
                    foreach (var span in spans)
                    {
                        var point = xFace ? new Vector3(plane, feet.y, Mathf.Clamp(feet.z, span.x, span.y)) :
                            new Vector3(Mathf.Clamp(feet.x, span.x, span.y), feet.y, plane);
                        float distance = Vector3.Distance(feet, point);
                        if (distance >= nearest || ((!inside || phase != RoomPhase.Closed) && distance > reach)) continue;
                        var outward = xFace ? (positive ? Vector3.right : Vector3.left) : (positive ? Vector3.forward : Vector3.back);
                        closest = new FloorHandProbe(room.Id, identity, point, inside ? 0f : distance, true,
                            outward, inside ? distance : 0f, phase == RoomPhase.Closed, feet);
                        nearest = distance;
                    }
                }
            }
            return closest;
        }

        public FloorHandProbe BoundaryProbe(Bounds bounds, int roomId, RoomPhase phase, Vector3 feet,
            float reach, int preferredFace = -1)
        {
            if (phase != RoomPhase.Tearing && phase != RoomPhase.Encroaching && phase != RoomPhase.Closed) return default;
            if (feet.y < bounds.min.y || feet.y >= bounds.max.y ||
                feet.x < bounds.min.x - reach || feet.x > bounds.max.x + reach ||
                feet.z < bounds.min.z - reach || feet.z > bounds.max.z + reach) return default;
            float[] depths = { feet.x - bounds.min.x, bounds.max.x - feet.x,
                feet.z - bounds.min.z, bounds.max.z - feet.z };
            Vector3[] normals = { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
            int face = preferredFace >= 0 && preferredFace < 4 ? preferredFace : 0;
            if (preferredFace < 0)
                for (int i = 1; i < depths.Length; i++) if (depths[i] < depths[face]) face = i;
            if (phase != RoomPhase.Closed && depths[face] > reach) return default;
            var point = feet + normals[face] * depths[face];
            point.x = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
            point.z = Mathf.Clamp(point.z, bounds.min.z, bounds.max.z);
            float distance = Mathf.Max(0f, -depths[face]);
            return new FloorHandProbe(roomId, face, point, distance, distance <= reach,
                normals[face], Mathf.Max(0f, depths[face]), phase == RoomPhase.Closed, feet);
        }
        public float Pulse(RoomDestructionSample sample) => sample.PulseRate > 0f
            ? 0.5f + 0.5f * Mathf.Sin(sample.PulsePhase * Mathf.PI * 2f) : 0f;
        public Vector3 CakeReach(Vector3 origin, Vector3 cake, RoomPhase phase, float progress)
            => Vector3.Lerp(origin, cake, MistProgress(phase, progress));

        public float GripWeight(CollapseHandEventKind kind)
        {
            if (kind == CollapseHandEventKind.Grabbed || kind == CollapseHandEventKind.Consumed) return 100f;
            if (kind == CollapseHandEventKind.Warning) return 30f;
            return 0f;
        }
        public float MistProgress(RoomPhase phase, float progress)
        {
            if (phase == RoomPhase.Closed) return 1f;
            if (phase == RoomPhase.Encroaching) return Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(progress));
            if (phase == RoomPhase.Tearing) return 0.12f * Mathf.Clamp01(progress);
            return 0f;
        }
        public bool Contains(Bounds bounds, Vector3 feet, float inset)
        {
            return feet.x >= bounds.min.x + inset && feet.x <= bounds.max.x - inset &&
                feet.z >= bounds.min.z + inset && feet.z <= bounds.max.z - inset &&
                feet.y >= bounds.min.y - 0.25f && feet.y < bounds.max.y;
        }
        public float InwardFraction(Bounds bounds, Vector3 point)
        {
            float x = Mathf.Min(point.x - bounds.min.x, bounds.max.x - point.x) / Mathf.Max(0.01f, bounds.extents.x);
            float z = Mathf.Min(point.z - bounds.min.z, bounds.max.z - point.z) / Mathf.Max(0.01f, bounds.extents.z);
            return Mathf.Clamp01(Mathf.Min(x, z));
        }
        public Vector3 GridPoint(Bounds bounds, int index, int width, float inset)
        {
            width = Mathf.Max(1, width);
            float x = (index % width + 0.5f) / width;
            float z = (index / width + 0.5f) / width;
            return new Vector3(Mathf.Lerp(bounds.min.x + inset, bounds.max.x - inset, x), bounds.min.y,
                Mathf.Lerp(bounds.min.z + inset, bounds.max.z - inset, z));
        }
        public float HandReveal(Bounds bounds, Vector3 hand, float mist)
        {
            if (mist <= 0f) return 0f;
            return Mathf.Clamp01((mist - InwardFraction(bounds, hand) + 0.2f) * 5f);
        }
        public Vector3 HandScale(float reveal, float elapsed, int index, float scale)
        {
            float sway = Mathf.Sin(elapsed * 1.6f + index * 2.1f) * 0.055f;
            return new Vector3(scale, Mathf.Max(0.01f, reveal * scale * (1f + sway)), scale);
        }
        public Vector3[] CrackPath(Vector3 center, Vector3 tangent, Vector3 bitangent, float length, int seed)
        {
            var points = new Vector3[9];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / 8f - 0.5f;
                float bend = Mathf.Sin((i + seed) * 2.7f) * length * 0.035f;
                points[i] = center + tangent * (t * length) + bitangent * bend;
            }
            return points;
        }
    }
}
