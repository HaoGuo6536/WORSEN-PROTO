// ============================================================================
// CamcorderFrameDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the camcorder's tape envelope independently of other image effects.
//   Packed outputs are plain values for the volume adapter, not live engine objects.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Hold injected-time phase, transient hit weight and smoothed degradation.
//   - Carry constant lens and degradation-only tape parameters.
// DEPENDENCIES:
//   UnityEngine value types only.
// USAGE NOTES:
//   Scene-owned through PostFXDriver; reset at capture and disable boundaries.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.PostFX
{
    public sealed class CamcorderFrameDriverState
    {
        public float Phase, HitWeight, Degradation, EdgeStart;
        public Vector4 Lens, Tape;
    }
}
