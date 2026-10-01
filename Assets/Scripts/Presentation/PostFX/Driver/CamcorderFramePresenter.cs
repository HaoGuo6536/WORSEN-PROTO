// ============================================================================
// CamcorderFramePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a constant old-camcorder lens and a degradation-only tape envelope.
//   Explicit delta time makes hits, critical health and intrusion reproducible;
//   neither the shader nor this calculator reads an engine clock.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Keep corner shading and edge softness constant, independent of degradation.
//   - Ease tape jitter/chroma in and out, retaining critical-health pressure.
//   - Bound inputs and reset transient envelopes without changing configuration.
// DEPENDENCIES:
//   Own DriverConfig/DriverState and UnityEngine value math only.
// USAGE NOTES:
//   Stateless; PostFXPresenter supplies health/proximity/intrusion and elapsed time.
//   Distances are source pixels; phase is radians wrapped for long-running sessions.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.PostFX
{
    public static class CamcorderFramePresenter
    {
        public static void Reset(CamcorderFrameDriverState s)
        {
            s.Phase = s.HitWeight = s.Degradation = s.EdgeStart = 0f;
            s.Lens = s.Tape = Vector4.zero;
        }

        public static void Tick(CamcorderFrameDriverState s, PostFXDriverConfig c,
            float injury, float proximity, float intrusion, float dt)
        {
            dt = Mathf.Max(0f, Finite(dt));
            float critical = injury >= Mathf.Clamp01(Finite(c.TapeCriticalInjury)) ? injury : 0f;
            float target = Mathf.Clamp01(Mathf.Max(Finite(critical), Finite(proximity), Finite(intrusion), s.HitWeight));
            float seconds = target > s.Degradation ? c.TapeRiseSeconds : c.TapeRelaxSeconds;
            s.Degradation = Mathf.MoveTowards(s.Degradation, target, dt / Mathf.Max(.001f, Finite(seconds)));
            s.HitWeight = Mathf.MoveTowards(s.HitWeight, 0f, dt / Mathf.Max(.001f, Finite(c.TapeHitSeconds)));
            s.Phase = (float)((s.Phase + (double)dt * Mathf.Max(0f, Finite(c.TapeFrequency)) * 2d * System.Math.PI) % (2d * System.Math.PI));
            s.Lens = new Vector4(Mathf.Clamp01(Finite(c.CamcorderCorners)),
                Mathf.Clamp(Finite(c.CamcorderCornerRadius), .01f, 1f),
                Mathf.Clamp(Finite(c.CamcorderCornerSoftness), .01f, 1f),
                Mathf.Clamp(Finite(c.CamcorderEdgeBlurPixels), 0f, 4f));
            s.EdgeStart = Mathf.Clamp(Finite(c.CamcorderEdgeStart), 0f, .99f);
            s.Tape = new Vector4(Mathf.Clamp(Finite(c.TapeJitterPixels), 0f, 4f) * s.Degradation,
                Mathf.Clamp(Finite(c.TapeChromaPixels), 0f, 4f) * s.Degradation,
                s.Phase, Mathf.Max(1f, Finite(c.TapeLines)));
            if (!c.CamcorderEnabled) { s.Lens = Vector4.zero; s.Tape = Vector4.zero; }
        }
        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
