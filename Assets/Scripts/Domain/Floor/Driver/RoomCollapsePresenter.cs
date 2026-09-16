// ============================================================================
// RoomCollapsePresenter.cs
// ============================================================================
// PURPOSE:
//   Calculates clipped fog advance, five-finger reveal, and deterministic surface fissures.
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
