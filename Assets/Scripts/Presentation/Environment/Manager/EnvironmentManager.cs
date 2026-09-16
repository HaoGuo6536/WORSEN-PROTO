// ============================================================================
// EnvironmentManager.cs
// ============================================================================
// PURPOSE:
//   Accepts room facts and player position for medieval dressing and Lumen lighting.
//   This narrow command surface keeps procedural layout and game rules out of presentation.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Environment (Service system).
// KEY RESPONSIBILITIES:
//   - Own and initialize the EnvironmentDriver, forwarding scene lifecycle and pushed facts.
//   - Route room batches, threshold chalk and localized flame dimming into the own Driver.
// DEPENDENCIES:
//   - Own presentation stack and Core GeneratedRoomSample; remaining public data is primitive.
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
        public void SetRooms(IReadOnlyList<GeneratedRoomSample> rooms)
        {
            if (_driver == null) return;
            _driver.BeginFloor();
            if (rooms == null) return;
            foreach (GeneratedRoomSample room in rooms)
                _driver.AddRoom(room.RoomId, room.Bounds, room.OpenSky, room.Refuge, room.PortalCenters);
        }
        public void AddRoom(int id, Bounds bounds, bool openSky, bool refuge, Vector3[] portalCenters, Bounds[] reserved = null)
        { if (_driver != null) _driver.AddRoom(id, bounds, openSky, refuge, portalCenters, reserved); }
        public Vector3[] GetTorchPositions(int roomId)
        { return _driver != null ? _driver.GetTorchPositions(roomId) : new Vector3[0]; }
        public void SetObserver(Vector3 position) { if (_driver != null) _driver.SetObserver(position); }
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
