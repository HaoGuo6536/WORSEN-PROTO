// ============================================================================
// AudioWorldDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Carries torch anchors and the four physical voice assignments as value data.
//   No scene object or playback operation crosses into the pure selection calculations.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Keep ambient emitters bounded and tied to supplied room geometry.
//   - Preserve transient presentation ownership and deterministic verification.
//
// DEPENDENCIES:
//   - Core room geometry values and the owning Audio presentation system.
//
// USAGE NOTES:
//   AudioWorldPresenter changes these values; the owning soundscape Driver applies them.
//
// ============================================================================

using UnityEngine;
namespace Worsen.Presentation.Audio
{
    public struct AudioTorchAnchor
    {
        public int Id;
        public int Room;
        public Vector3 Position;
        public float DistanceSquared;
        public bool AcrossPortal;
    }
    public struct AudioTorchSlot
    {
        public int Id;
        public Vector3 Position;
        public float Gain;
        public float Pitch;
        public int Clip;
        public bool Changed;
        public bool AcrossPortal;
    }
}
