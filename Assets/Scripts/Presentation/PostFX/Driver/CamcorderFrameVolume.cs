// ============================================================================
// CamcorderFrameVolume.cs
// ============================================================================
// PURPOSE:
//   Bridges the owned PostFX profile into URP's per-camera volume stack.
//   Disabled defaults prevent cameras outside the owner's volume mask from inheriting
//   a frame; all designer tuning remains in PostFXDriverConfig.
// ARCHITECTURAL ROLE:
//   Driver (§7a, engine volume adapter) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Expose packed per-camera renderer parameters with neutral engine defaults.
// DEPENDENCIES:
//   Unity rendering core volume APIs only.
// USAGE NOTES:
//   Profile-owned VolumeComponent, not a MonoBehaviour. PostFXDriver writes and
//   destroys the runtime component; the renderer reads the resolved camera stack.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Worsen.Presentation.PostFX
{
    [Serializable, VolumeComponentMenu("Worsen/Old Camcorder")]
    public sealed class CamcorderFrameVolume : VolumeComponent
    {
        public BoolParameter Enabled = new BoolParameter(false);
        public Vector4Parameter Lens = new Vector4Parameter(Vector4.zero);
        public Vector4Parameter Tape = new Vector4Parameter(Vector4.zero);
        public FloatParameter EdgeStart = new FloatParameter(0f);
    }
}
