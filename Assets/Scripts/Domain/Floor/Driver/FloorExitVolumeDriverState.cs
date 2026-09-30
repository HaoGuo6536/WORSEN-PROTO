// ============================================================================
// FloorExitVolumeDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the trigger reference for the legacy exit volume.
//   Contact admission stays with FloorManager; no timed overlap state is retained.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Hold the owned trigger reference without engine calls.
// DEPENDENCIES:
//   - Passive Unity collider references only.
// USAGE NOTES:
//   Scene-owned by FloorExitVolume; replaced on reconfiguration.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public sealed class FloorExitVolumeDriverState
    {
        public BoxCollider Trigger;

    }
}
