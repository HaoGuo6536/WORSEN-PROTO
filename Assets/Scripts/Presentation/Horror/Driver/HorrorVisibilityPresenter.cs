// ============================================================================
// HorrorVisibilityPresenter.cs
// ============================================================================
// PURPOSE:
//   Keeps ordinary darkness and atmospheric haze above a readable colour floor.
//   Old serialized black values cannot erase nearby silhouettes or turn haze into
//   a black wall; deliberate PostFX blindness remains independently owned.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Sanitize and floor ambient, haze and supplementary fill outputs.
// DEPENDENCIES:
//   - Unity value math only; all tuning is supplied by HorrorDriverConfig.
// USAGE NOTES:
//   Pure and stateless. This is a rendering floor, not gameplay illumination.
//   It cannot override a later full-screen blackout; PostFX has separate ownership.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.Horror
{
    public static class HorrorVisibilityPresenter
    {
        public static Color AtLeast(Color value, Color floor) => new Color(
            Mathf.Max(Safe(value.r), Safe(floor.r)), Mathf.Max(Safe(value.g), Safe(floor.g)),
            Mathf.Max(Safe(value.b), Safe(floor.b)), 1f);
        public static float Fill(float intensity, float offMultiplier, bool flashlight, float minimum)
            => Mathf.Max(Safe(minimum), Safe(intensity) * (flashlight ? 1f : Mathf.Clamp01(Safe(offMultiplier))));
        private static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
