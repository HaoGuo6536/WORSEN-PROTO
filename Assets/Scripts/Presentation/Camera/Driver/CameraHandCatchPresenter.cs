// ============================================================================
// CameraHandCatchPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a hand emerging slowly from fog before snapping onto a held camera.
//   The existing catch start/end facts become the grab sting and hard-cut boundary,
//   keeping presentation timing separate from the already committed gameplay death.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Camera.
// KEY RESPONSIBILITIES:
//   - Hold the captured camera pose and advance independent approach/grab clocks.
//   - Compute hand distance, fog reveal and finger closure from supplied time.
//   - Preserve one presented approach endpoint before beginning the fast grab.
// DEPENDENCIES:
//   Own CameraDriverState/Config and pure UnityEngine value math only.
// USAGE NOTES:
//   Stateless; called only for confirmed hand catches. Hunter timing is untouched.
//   CatchHoldStarted is the single sting edge; CatchHoldEnded releases results.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.Camera
{
    public static class CameraHandCatchPresenter
    {
        public static void Begin(CameraDriverState s, CameraDriverConfig c)
        {
            s.CatchTargetPosition = s.CatchStartPosition;
            s.CatchStartRotation = s.DeathRotation;
            s.CatchApproachDuration = Mathf.Max(0f, Finite(c.HandApproachSeconds));
            s.CatchHoldDuration = Mathf.Max(0f, Finite(c.HandGrabSeconds));
            s.CatchElapsed = s.CatchHoldElapsed = 0f;
            s.CatchHoldStarted = s.CatchHoldEnded = false;
            s.HandReveal = s.HandGrip = 0f;
            s.HandDistance = Mathf.Max(FaceDistance(c), Finite(c.HandStartDistance));
        }
        public static void Tick(CameraDriverState s, CameraDriverConfig c, float dt)
        {
            dt = Mathf.Max(0f, Finite(dt));
            if (!s.Consumed || !s.DeathSnapped) return;
            float face = FaceDistance(c);
            float reach = Mathf.Max(face, Finite(c.HandReachDistance));
            float start = Mathf.Max(reach, Finite(c.HandStartDistance));
            if (!s.CatchHoldStarted)
            {
                s.CatchElapsed = Mathf.Min(s.CatchApproachDuration, s.CatchElapsed + dt);
                float t = s.CatchApproachDuration > 0f ? s.CatchElapsed / s.CatchApproachDuration : 1f;
                s.HandReveal = t * t * (3f - 2f * t);
                s.HandDistance = Mathf.Lerp(start, reach, s.HandReveal);
                if (t >= 1f) s.CatchHoldStarted = true;
            }
            else
            {
                s.CatchHoldElapsed = Mathf.Min(s.CatchHoldDuration, s.CatchHoldElapsed + dt);
                float t = s.CatchHoldDuration > 0f ? s.CatchHoldElapsed / s.CatchHoldDuration : 1f;
                s.HandGrip = t * t;
                s.HandDistance = Mathf.Lerp(reach, face, s.HandGrip);
                s.CatchHoldEnded = t >= 1f;
            }
            s.Position = s.CatchStartPosition;
            s.AimRotation = s.Rotation = s.DeathRotation;
            s.Roll = 0f;
        }
        public static Color Tint(Color fog, Color hand, float reveal) => Color.Lerp(fog, hand, Mathf.Clamp01(Finite(reveal)));
        public static Quaternion FingerRotation(float grip, float degrees) => Quaternion.Euler(-Mathf.Clamp(Finite(degrees), 0f, 120f) * Mathf.Clamp01(Finite(grip)), 0f, 0f);
        private static float FaceDistance(CameraDriverConfig c) => Mathf.Max(.01f, Finite(c.NearClip), Finite(c.HandFaceDistance));
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
