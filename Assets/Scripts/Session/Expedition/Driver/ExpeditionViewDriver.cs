// ============================================================================
// ExpeditionViewDriver.cs
// ============================================================================
// PURPOSE:
//   Samples the rendered gameplay camera for observation-dependent hunters.
// ARCHITECTURAL ROLE:
//   Driver (§7) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Capture actual position, pitch, look-back rotation and lens without guessing from input.
// DEPENDENCIES:
//   - Unity Camera and Core immutable hunter view values; no Presentation references.
// USAGE NOTES:
//   Manager calls once before hunter sensing. An explicit camera wins over MainCamera.
//   Missing/disabled cameras fail closed; no synthetic forward-facing view is supplied.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionViewDriver : MonoBehaviour
    {
        [SerializeField] private Camera _camera = null;
        public bool TrySample(long tick, out HunterPlayerView view)
        {
            Camera camera = _camera != null ? _camera : Camera.main;
            view = default;
            if (camera == null || !camera.isActiveAndEnabled || camera.orthographic) return false;
            view = new HunterPlayerView(camera.transform.position, camera.transform.rotation,
                Camera.VerticalToHorizontalFieldOfView(camera.fieldOfView, camera.aspect), camera.fieldOfView, tick);
            return true;
        }
    }
}
