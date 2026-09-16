// ============================================================================
// GeneratedRoomSample.cs
// ============================================================================
// PURPOSE:
//   Describes one assembled room for lighting, sound and safe optional effects.
//   It transfers geometry facts without exposing procedural implementation types.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Generated environment shared contracts.
// KEY RESPONSIBILITIES:
//   - Carry room bounds, doorway positions and environmental identity.
//   - Identify optional rooms without allowing presentation to decide safe routes.
// DEPENDENCIES:
//   - UnityEngine value types only.
// USAGE NOTES:
//   Generator owns construction; consumers must not mutate the supplied portal array.
//   Bounds contain the full vertical room interior; portal positions are world metres.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{
    public readonly struct GeneratedRoomSample
    {
        public GeneratedRoomSample(int roomId, Bounds bounds, bool openSky, bool refuge,
            Vector3[] portalCenters, bool optionalRoom = false)
        {
            RoomId = roomId; Bounds = bounds; OpenSky = openSky; Refuge = refuge;
            PortalCenters = portalCenters; OptionalRoom = optionalRoom;
        }
        public int RoomId { get; }
        public Bounds Bounds { get; }
        public bool OpenSky { get; }
        public bool Refuge { get; }
        public Vector3[] PortalCenters { get; }
        public bool OptionalRoom { get; }
    }
}
