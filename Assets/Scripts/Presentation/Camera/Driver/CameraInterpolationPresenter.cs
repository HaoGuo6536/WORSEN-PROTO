// ============================================================================
// CameraInterpolationPresenter.cs
// ============================================================================
// PURPOSE:
//   Buffers committed camera positions and traversal heights for render-time sampling.
//   It uses the Player body's previous-to-current convention without extrapolation,
//   so a fast render loop does not introduce either tick stepping or extra latency.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Camera.
// KEY RESPONSIBILITIES:
//   - Compute clamped fractions from injected fixed/render clocks.
//   - Snap first samples, invalid clocks and configured teleport-sized changes.
//   - Sample independent movement and traversal buffers regardless of event order.
// DEPENDENCIES:
//   CameraDriverState and pure UnityEngine value math only.
// USAGE NOTES:
//   Stateless. The caller resets identity/lifecycle discontinuities before sampling.
//   Untimed callers use immediate samples; runtime Drivers always supply clocks.
//   Gameplay aim and look deltas are deliberately not interpolated here.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Camera
{
    public static class CameraInterpolationPresenter
    {
        public static bool SetPosition(CameraDriverState s, Vector3 eye, float fixedTime,
            float stepDuration, float snapDistance)
        {
            float threshold = Finite(snapDistance) ? Mathf.Max(0f, snapDistance) : 0f;
            bool discontinuity = !s.HasMovement || fixedTime < s.MovementStepTime
                || (eye - s.EyePosition).sqrMagnitude > threshold * threshold;
            bool snap = discontinuity || !Finite(fixedTime) || !Finite(stepDuration) || stepDuration <= 0f;
            s.PreviousEyePosition = snap ? eye : s.EyePosition;
            s.EyePosition = eye;
            s.MovementStepTime = Finite(fixedTime) ? fixedTime : 0f;
            s.MovementStepDuration = snap ? 0f : stepDuration;
            return discontinuity;
        }

        public static Vector3 Position(CameraDriverState s, float renderTime)
            => Vector3.Lerp(s.PreviousEyePosition, s.EyePosition,
                Fraction(renderTime, s.MovementStepTime, s.MovementStepDuration));

        public static void SetHeight(CameraDriverState s, float height, float fixedTime, float stepDuration)
        {
            s.PreviousVaultHeight = s.VaultActive || s.VaultCompleting ? s.VaultHeight : s.RenderedVaultHeight;
            s.VaultHeight = height;
            s.VaultStepTime = fixedTime;
            s.VaultStepDuration = stepDuration;
        }

        public static float Height(CameraDriverState s, float renderTime)
            => Mathf.Lerp(s.PreviousVaultHeight, s.VaultHeight,
                Fraction(renderTime, s.VaultStepTime, s.VaultStepDuration));

        public static float Fraction(float renderTime, float fixedTime, float stepDuration)
            => !Finite(renderTime) || !Finite(fixedTime) || !Finite(stepDuration) || stepDuration <= 0f
                ? 1f : Mathf.Clamp01((renderTime - fixedTime) / stepDuration);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
