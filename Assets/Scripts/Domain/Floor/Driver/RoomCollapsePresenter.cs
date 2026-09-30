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
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public sealed class RoomCollapsePresenter
    {
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
