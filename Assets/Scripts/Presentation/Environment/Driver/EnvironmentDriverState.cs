// ============================================================================
// EnvironmentDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains references to the current floor's decorative objects and light budget state.
//   The owner can remove one room or reset a floor without retaining destroyed room handles.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Store generated roots, flame outputs, local dimming and owned chalk marks.
// DEPENDENCIES:
//   - Passive Unity references and the wrapped Lumen effect reference only.
// USAGE NOTES:
//   Plain data owned by EnvironmentDriver, reset on every generated floor.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DistantLands.Lumen;

namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentDriverState
    {
        public readonly Dictionary<int, GameObject> Rooms = new Dictionary<int, GameObject>();
        public readonly Dictionary<int, Bounds> RoomBounds = new Dictionary<int, Bounds>();
        public readonly HashSet<int> ConsumedRooms = new HashSet<int>();
        public readonly Dictionary<int, EnvironmentDoorMarkDriverState> DoorMarks = new Dictionary<int, EnvironmentDoorMarkDriverState>();
        public readonly List<EnvironmentFlameDriverState> Flames = new List<EnvironmentFlameDriverState>();
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<bool> Available = new List<bool>();
        public Vector3 Observer;
        public float Elapsed;
        public float UntilRefresh;
        public float Gutter;
        public Vector3 FlameDimPosition;
        public float FlameDimRadius;
        public float FlameDimMultiplier = 1f;
        public Material ChalkMaterial;
        public bool OwnerEnabled = true;
        public int ActiveLumenCount;
        public int ActiveLightCount;
    }

    public sealed class EnvironmentDoorMarkDriverState
    {
        public GameObject Root;
        public int[] RoomIds;
    }

    public sealed class EnvironmentFlameDriverState
    {
        public int RoomId;
        public int Identity;
        public GameObject EffectRoot;
        public LumenEffectPlayer Lumen;
        public bool Moon;
        public float Intensity;
        public float Destruction;
    }
}
