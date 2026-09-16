// ============================================================================
// AudioWorldDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Stores the bounded torch assignments and room-specific ambience cooldowns.
//   All retained locations are supplied room values, with no cached scene-owned objects.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Keep ambient emitters bounded and tied to supplied room geometry.
//   - Preserve transient presentation ownership and deterministic verification.
//
// DEPENDENCIES:
//   - Core room geometry values and the owning Audio presentation system.
//
// USAGE NOTES:
//   Owned by AudioSoundscapeDriver; replacing this state resets every floor emitter and cooldown.
//
// ============================================================================

using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Presentation.Audio
{
    public sealed class AudioWorldDriverState
    {
        public readonly Dictionary<int, GeneratedRoomSample> Rooms = new Dictionary<int, GeneratedRoomSample>();
        public readonly Dictionary<int, AudioTorchAnchor[]> RoomTorches = new Dictionary<int, AudioTorchAnchor[]>();
        public readonly HashSet<int> ConsumedRooms = new HashSet<int>();
        public readonly List<AudioTorchAnchor> Nearest = new List<AudioTorchAnchor>();
        public readonly Dictionary<int, float> AccentDue = new Dictionary<int, float>();
        public AudioTorchSlot[] Slots = new AudioTorchSlot[4];
        public int NextAnchor;
        public int LastTorchClip = -1;
        public System.Random Random = new System.Random(41417);
        public float Time;
        public float NextGlobalAccent = 4f;
        public CueId LastAccent = CueId.ChainCreak;
    }
}
