// ============================================================================
// EnvironmentManager.cs
// ============================================================================
// PURPOSE:
//   Accepts room facts and player position for medieval dressing and Lumen lighting.
//   This narrow command surface keeps procedural layout and game rules out of presentation.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Environment (Service system).
// KEY RESPONSIBILITIES:
//   - Receive theme tags, template ownership, light sockets and boundaries before dressing.
//   - Own the Driver lifecycle and forward primitive/Core facts without Domain references.
//   - Route room batches, threshold chalk and local dimming into the owned Driver.
//   - Expose exit-frame, lamp, fog and rim commands for upward routing.
//   - Preserve Level light state, footprint cells and effect-density budgets.
// DEPENDENCIES:
//   - Own presentation stack and Core GeneratedRoomSample/InteractableState; remaining public data is primitive.
// USAGE NOTES:
//   Scene-owned service. Initialize before AddRoom; BeginFloor removes preceding floor dressing.
//   Global fog belongs to Horror and torch audio is routed through the central soundscape.
// ============================================================================
using UnityEngine;
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Presentation.Environment
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnvironmentDriver))]
    public sealed class EnvironmentManager : MonoBehaviour
    {
        [SerializeField] private EnvironmentDriverConfig _config;
        [SerializeField] private EnvironmentDriver _driver;
        public bool IsReady => _driver != null && _driver.IsReady;
        public int RoomCount => _driver != null ? _driver.RoomCount : 0;
        public int ActiveLumenCount => _driver != null ? _driver.ActiveLumenCount : 0;
        public int ActiveLightCount => _driver != null ? _driver.ActiveLightCount : 0;
        public int DoorMarkCount => _driver != null ? _driver.DoorMarkCount : 0;
        private void Awake() { if (_driver == null) _driver = GetComponent<EnvironmentDriver>(); }
        public EnvironmentManager Initialize(EnvironmentDriverConfig config)
        {
            if (_driver == null) _driver = GetComponent<EnvironmentDriver>();
            if (config != null) _config = config;
            _driver.Initialize(_config); _driver.SetOwnerEnabled(isActiveAndEnabled); return this;
        }
        public void BeginFloor() { if (_driver != null) _driver.BeginFloor(); }
        public void SetTheme(string theme, string lightSource) => _driver?.SetTheme(theme, lightSource);
        public void SetRoomTheme(int room, string theme, string family) => _driver?.SetRoomTheme(room, theme, family);
        public void SetRooms(IReadOnlyList<GeneratedRoomSample> rooms,
            System.Func<int, IReadOnlyList<Vector3>> boundary = null, System.Func<int, IReadOnlyList<Vector3>> lightSockets = null,
            System.Func<int, bool> authoredFurniture = null)
        {
            if (_driver == null) return;
            _driver.BeginFloor(clearTheme: false);
            if (rooms == null) return;
            foreach (GeneratedRoomSample room in rooms)
                _driver.AddRoom(room.RoomId, room.Bounds, room.OpenSky, room.Refuge, room.PortalCenters, cells: room.Cells,
                    boundary: boundary?.Invoke(room.RoomId), lightSockets: lightSockets?.Invoke(room.RoomId),
                    authoredFurniture: authoredFurniture?.Invoke(room.RoomId) ?? false);
        }
        public void AddRoom(int id, Bounds bounds, bool openSky, bool refuge, Vector3[] portalCenters, Bounds[] reserved = null,
            IReadOnlyList<Bounds> cells = null, IReadOnlyList<Vector3> boundary = null, IReadOnlyList<Vector3> lightSockets = null,
            bool authoredFurniture = false)
        { if (_driver != null) _driver.AddRoom(id, bounds, openSky, refuge, portalCenters, reserved, cells, boundary, lightSockets, authoredFurniture); }
        public Vector3[] GetTorchPositions(int roomId)
        { return _driver != null ? _driver.GetTorchPositions(roomId) : new Vector3[0]; }
        public void SetObserver(Vector3 position) { if (_driver != null) _driver.SetObserver(position); }
        public void SetLightingHooks(bool darkerFloors, bool wick)
        { if (_driver != null) _driver.SetLightingHooks(darkerFloors, wick); }
        public void SetTorchCountMultiplier(float multiplier) { if (_driver != null) _driver.SetTorchCountMultiplier(multiplier); }
        public void ApplyLight(InteractableState light) { if (_driver != null) _driver.ApplyLight(light); }
        public void SetExitFrame(int roomId, Vector3 position, Quaternion rotation)
        { if (_driver != null) _driver.SetExitFrame(roomId, position, rotation); }
        public void SetExitProgress(float progress) { if (_driver != null) _driver.SetExitProgress(progress); }
        public float FogBoundaryGlow(float density) => _driver != null ? _driver.FogBoundaryGlow(density) : 0f;
        public Color FogBoundaryColor => _driver != null ? _driver.FogBoundaryColor : Color.black;
        public float HunterRim(bool lookBack) => _driver != null ? _driver.HunterRim(lookBack) : 0f;
        public void SetFlameGutter(float amount) { if (_driver != null) _driver.SetFlameGutter(amount); }
        public void SetFlameDim(Vector3 position, float radius, float multiplier)
        { if (_driver != null) _driver.SetFlameDim(position, radius, multiplier); }
        public void MarkDoor(int doorId, Vector3 position) { if (_driver != null) _driver.MarkDoor(doorId, position); }
        public void SetRoomConsumed(int roomId) { if (_driver != null) _driver.SetRoomConsumed(roomId); }
        public void SetRoomDestruction(int roomId, float severity) { if (_driver != null) _driver.SetRoomDestruction(roomId, severity); }
        private void Update() { if (_driver != null) _driver.Tick(Time.deltaTime); }
        private void OnEnable() { if (_driver != null) _driver.SetOwnerEnabled(true); }
        private void OnDisable() { if (_driver != null) _driver.SetOwnerEnabled(false); }
        private void OnDestroy() { if (_driver != null) _driver.Teardown(); }
    }
}
