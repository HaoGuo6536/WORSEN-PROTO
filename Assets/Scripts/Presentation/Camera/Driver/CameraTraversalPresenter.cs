// ============================================================================
// CameraTraversalPresenter.cs
// ============================================================================
// PURPOSE:
//   Adds bodily traversal feedback without taking over gameplay aim or mouse look.
//   Vault height interpolates committed progress samples on the render clock.
//   Cancellation and impact recovery use explicit elapsed time, independently of aim.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Camera.
// KEY RESPONSIBILITIES:
//   - Reject stale identities, recover smoothly and suppress motion for comfort settings.
// DEPENDENCIES:
//   - Core traversal facts and the Camera config/state; pure value math only.
// USAGE NOTES:
//   Stateless. Tick composes onto a freshly evaluated base pose, never last frame's output.
//   Severity is normalized soft=0/hard=1; the legacy fact has no severity field.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Camera
{
    public static class CameraTraversalPresenter
    {
        public static void Reset(CameraDriverState s)
        {
            s.ProgressTick = s.StumbleTick = -1;
            s.VaultActive = s.VaultCompleting = false;
            s.VaultHeight = s.VaultReturnHeight = s.VaultReturnElapsed = 0f;
            s.PreviousVaultHeight = s.RenderedVaultHeight = s.VaultStepTime = s.VaultStepDuration = 0f;
            s.LandingDepth = s.LandingElapsed = s.StumbleElapsed = s.StumbleDuration = 0f;
        }

        public static void SetProgress(CameraDriverState s, CameraDriverConfig c, EntityId id,
            long tick, TraversalKind kind, float progress, bool active, float fixedTime = 0f, float stepDuration = 0f)
        {
            if (!s.HasMovement || s.PlayerId != id || s.DeathSnapped || tick < s.ProgressTick
                || (tick == s.ProgressTick && active && !s.VaultActive)
                || (kind != TraversalKind.Vault && kind != TraversalKind.Mantle)) return;
            s.ProgressTick = tick;
            if (active || (s.VaultActive && progress >= 1f))
            {
                CameraInterpolationPresenter.SetHeight(s,
                    c.VaultHeight == null ? 0f : Finite(c.VaultHeight.Evaluate(Mathf.Clamp01(Finite(progress)))),
                    fixedTime, stepDuration);
                s.VaultActive = active;
                s.VaultCompleting = !active;
            }
            else if (s.VaultActive)
            {
                s.VaultActive = false;
                s.VaultCompleting = false;
                // Recover from the displayed height, not the unpresented committed endpoint.
                s.VaultReturnHeight = s.RenderedVaultHeight;
                s.VaultReturnElapsed = 0f;
            }
        }

        public static void Land(CameraDriverState s, CameraDriverConfig c, float severity)
        {
            s.LandingDepth = Mathf.Lerp(Mathf.Max(0f, Finite(c.SoftLandingDip)),
                Mathf.Max(0f, Finite(c.HardLandingDip)), Mathf.Clamp01(Finite(severity)));
            s.LandingElapsed = 0f;
        }

        public static void Stumble(CameraDriverState s, EntityId id, long tick, float seconds)
        {
            if (!s.HasMovement || s.PlayerId != id || s.DeathSnapped || tick <= s.StumbleTick) return;
            s.StumbleTick = tick;
            s.StumbleDuration = Mathf.Max(0f, Finite(seconds));
            s.StumbleElapsed = 0f;
        }

        public static void Tick(CameraDriverState s, CameraDriverConfig c, float dt, float renderTime = float.NaN)
        {
            dt = Mathf.Max(0f, Finite(dt));
            if (s.VaultActive || s.VaultCompleting)
            {
                s.RenderedVaultHeight = CameraInterpolationPresenter.Height(s, renderTime);
                if (s.VaultCompleting && CameraInterpolationPresenter.Fraction(renderTime, s.VaultStepTime, s.VaultStepDuration) >= 1f)
                {
                    s.VaultCompleting = false;
                    s.VaultReturnHeight = s.RenderedVaultHeight;
                    s.VaultReturnElapsed = 0f;
                }
            }
            else
            {
                s.VaultReturnElapsed += dt;
                s.RenderedVaultHeight = s.VaultHeight = s.VaultReturnHeight
                    * (1f - Ease(s.VaultReturnElapsed / Mathf.Max(0.001f, Finite(c.VaultReturnSeconds))));
            }
            s.LandingElapsed += dt;
            s.StumbleElapsed += dt;
            // Either disabled preference suppresses both landing and stumble, including mid-envelope.
            if (!s.PunchEnabled || !(s.TiltEnabled ?? c.TiltEnabled))
            { s.LandingDepth = s.StumbleDuration = 0f; return; }
            float landing = Mathf.Clamp01(s.LandingElapsed / Mathf.Max(0.001f, Finite(c.LandingDipSeconds)));
            float dip = s.LandingDepth * Mathf.Sin(landing * Mathf.PI);
            if (landing >= 1f) { s.LandingDepth = 0f; dip = 0f; }
            float envelope = s.StumbleDuration > 0f ? Mathf.Clamp01(1f - s.StumbleElapsed / s.StumbleDuration) : 0f;
            float shake = Mathf.Sin(s.StumbleElapsed * Mathf.Max(0f, Finite(c.StumbleFrequency)) * Mathf.PI * 2f)
                * envelope * Mathf.Clamp01(Finite(c.StumbleStrength)) * Mathf.Clamp01(Finite(c.ShakeIntensity));
            s.Position += Vector3.up * (s.RenderedVaultHeight - dip) + s.AimRotation * Vector3.right * (shake * c.MaximumShakeDisplacement);
            s.Rotation *= Quaternion.Euler(shake * c.MaximumShakeDegrees, 0f, shake * c.MaximumShakeDegrees);
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
    }
}
