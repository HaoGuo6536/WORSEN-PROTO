// ============================================================================
// HunterViewUtility.cs
// ============================================================================
// PURPOSE:
//   Evaluates the actual camera frustum from injected value data, including pitch.
//   The caller supplies occlusion separately; no planar body-heading approximation
//   can accidentally unfreeze a hunter during look-back.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Validate fresh camera evidence and compute projected view-cone membership.
// DEPENDENCIES:
//   - Core camera values and pure Unity vector/quaternion math.
// USAGE NOTES:
//   A missing or stale sample is invalid, never evidence of an unobserved hunter.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    public static class HunterViewUtility
    {
        public static bool Fresh(HunterPlayerView view, long tick) => view.Tick == tick &&
            view.HorizontalFov > 0f && view.HorizontalFov < 180f && view.VerticalFov > 0f && view.VerticalFov < 180f &&
            Finite(view.Origin.sqrMagnitude) && Finite(view.Rotation.x) && Finite(view.Rotation.y) &&
            Finite(view.Rotation.z) && Finite(view.Rotation.w) &&
            Mathf.Abs(view.Rotation.x * view.Rotation.x + view.Rotation.y * view.Rotation.y +
                view.Rotation.z * view.Rotation.z + view.Rotation.w * view.Rotation.w - 1f) < .001f;
        public static bool Contains(HunterPlayerView view, Vector3 point, float halfAngle = 0f)
        {
            Vector3 local = Quaternion.Inverse(view.Rotation) * (point - view.Origin);
            if (local.z <= 0f) return false;
            float horizontal = halfAngle > 0f ? Mathf.Min(halfAngle, view.HorizontalFov * .5f) : view.HorizontalFov * .5f;
            float vertical = halfAngle > 0f ? Mathf.Min(halfAngle, view.VerticalFov * .5f) : view.VerticalFov * .5f;
            return Mathf.Abs(local.x) <= local.z * Mathf.Tan(horizontal * Mathf.Deg2Rad) &&
                Mathf.Abs(local.y) <= local.z * Mathf.Tan(vertical * Mathf.Deg2Rad);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
