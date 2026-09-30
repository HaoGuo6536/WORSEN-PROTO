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
//   - Copy footprint cells so presentation cannot fill a missing room notch.
//   - Identify optional rooms without allowing presentation to decide safe routes.
// DEPENDENCIES:
//   - UnityEngine value types and System collections only.
// USAGE NOTES:
//   Generator owns construction; consumers must not mutate the supplied portal array.
//   Bounds contain the full vertical room interior; portal positions are world metres.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Core
{
    public readonly struct GeneratedRoomSample
    {
        public GeneratedRoomSample(int roomId, Bounds bounds, bool openSky, bool refuge,
            Vector3[] portalCenters, bool optionalRoom = false, Bounds[] cells = null)
        {
            RoomId = roomId; Bounds = bounds; OpenSky = openSky; Refuge = refuge;
            PortalCenters = portalCenters; OptionalRoom = optionalRoom;
            Cells = Array.AsReadOnly(cells == null || cells.Length == 0
                ? new[] { bounds } : (Bounds[])cells.Clone());
        }
        public int RoomId { get; }
        public Bounds Bounds { get; }
        public bool OpenSky { get; }
        public bool Refuge { get; }
        public Vector3[] PortalCenters { get; }
        public bool OptionalRoom { get; }
        public IReadOnlyList<Bounds> Cells { get; }
    }
}
