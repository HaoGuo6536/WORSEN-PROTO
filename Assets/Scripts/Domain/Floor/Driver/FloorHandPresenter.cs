// ============================================================================
// FloorHandPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes continuously searching hands from owner-supplied time and observations.
//   Bounded translation and aim strain toward nearby players without changing hazards.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Preserve castle and unknown looks; scale provisional hospital glove silhouettes.
//   - Produce deterministic per-hand flail, reach direction and finger flex.
// DEPENDENCIES:
//   - Primitive look tags and injected FloorDriverConfig values only.
// USAGE NOTES:
//   Pure and stateless. RoomCollapseVolume applies this only to non-colliding art.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public static class FloorHandPresenter
    {
        public static float VisualScale(string look, float baseScale, float glovedMultiplier)
            => baseScale * (look == "gloved-shadow-hands" ? glovedMultiplier : 1f);

        public static (Vector3 Offset, Vector3 Direction, float Grip) Pose(Vector3 origin, Vector3? target,
            float elapsed, int index, float amplitude, float rate, float reachRange, float reachDistance)
        {
            float time = Finite(elapsed) ? elapsed : 0f;
            float phase = index * 2.399963f;
            float cycle = time * Safe(rate) * (1f + (index % 5) * 0.07f) + phase;
            var wave = new Vector3(Mathf.Sin(cycle), 0.5f + 0.5f * Mathf.Sin(cycle * 1.31f + phase),
                Mathf.Cos(cycle * 0.83f + phase));
            Vector3 flail = Vector3.ClampMagnitude(wave, 1f) * Safe(amplitude);
            Vector3 delta = target.HasValue ? target.Value - origin : Vector3.zero;
            float distance = delta.magnitude;
            float weight = target.HasValue && Safe(reachRange) > 0f && Finite(distance)
                ? Mathf.Clamp01(1f - distance / reachRange) : 0f;
            Vector3 toward = distance > 0.0001f && Finite(distance) ? delta / distance : Vector3.up;
            // Reach ramps continuously at the range boundary; flailing never stops when straining.
            Vector3 offset = flail * (1f - 0.65f * weight) + toward * (Safe(reachDistance) * weight);
            Vector3 direction = Vector3.Lerp(new Vector3(wave.x * 0.7f, 1f, wave.z * 0.7f), toward, weight).normalized;
            return (offset, direction, Mathf.Lerp(15f, 65f, 0.5f + 0.5f * Mathf.Sin(cycle * 1.7f)));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Safe(float value) => Finite(value) ? Mathf.Max(0f, value) : 0f;
    }
}
