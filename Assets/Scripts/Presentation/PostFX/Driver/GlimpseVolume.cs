// ============================================================================
// GlimpseVolume.cs
// ============================================================================
// PURPOSE:
//   Carries a brief look-back silhouette reveal into the camera-local render stack.
//   Neutral defaults keep other cameras and ordinary look-back views unchanged.
// ARCHITECTURAL ROLE:
//   Driver (§7a, engine volume adapter) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Carry the owned outline strength, width and tint to the renderer.
// DEPENDENCIES:
//   Unity rendering volume APIs only.
// USAGE NOTES:
//   Profile-owned; PostFXDriver creates and destroys this runtime component.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Worsen.Presentation.PostFX
{
    [Serializable, VolumeComponentMenu("Worsen/Glimpse")]
    public sealed class GlimpseVolume : VolumeComponent
    {
        public ClampedFloatParameter Strength = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter Width = new ClampedFloatParameter(0f, 0f, 0.1f);
        public ColorParameter Tint = new ColorParameter(Color.clear);
    }
}
