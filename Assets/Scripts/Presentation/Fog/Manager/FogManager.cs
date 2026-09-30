// ============================================================================
// FogManager.cs
// ============================================================================
// PURPOSE:
//   Accepts published floor geometry and collapse progress for fog presentation.
//   It owns the Driver lifetime without importing any Domain implementation.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Fog (Service system).
// KEY RESPONSIBILITIES:
//   - Forward theme look tags without changing room progress or gameplay rules.
//   - Forward field commands and coalesce rendering work through the own Driver.
// DEPENDENCIES:
//   - Core room samples/graph and own FogDriver/DriverConfig.
// USAGE NOTES:
//   Scene-owned. Initialize before SetRooms. Disable clears the floor; the assembly
//   owner must republish rooms after re-enabling. SetEnabled preserves CPU progress.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Fog
{
    [DisallowMultipleComponent, RequireComponent(typeof(FogDriver))]
    public sealed class FogManager : MonoBehaviour
    {
        [SerializeField] private FogDriverConfig _config;
        [SerializeField] private FogDriver _driver;
        public int RoomCount => _driver == null ? 0 : _driver.RoomCount;
        public int UploadRevision => _driver == null ? 0 : _driver.UploadRevision;
        public double LastUploadMilliseconds => _driver == null ? 0 : _driver.LastUploadMilliseconds;
        public float RoomProgress(int id) => _driver == null ? 0f : _driver.Progress(id);
        public FogManager Initialize(FogDriverConfig config)
        {
            if (_driver == null) _driver = GetComponent<FogDriver>();
            if (config != null) _config = config;
            _driver.Initialize(_config);
            return this;
        }
        public void SetRooms(IReadOnlyList<GeneratedRoomSample> rooms, LevelGraph graph) => _driver?.SetRooms(rooms, graph);
        public void SetRoomProgress(int roomId, float progress) => _driver?.SetRoomProgress(roomId, progress);
        public void SetLook(string look) => _driver?.SetLook(look);
        public void ResetFloor() => _driver?.ResetFloor();
        public void SetEnabled(bool value) => _driver?.SetEnabled(value);
        private void Awake() { if (_driver == null) _driver = GetComponent<FogDriver>(); }
        private void OnEnable() { if (_config != null) Initialize(_config); }
        private void LateUpdate() => _driver?.Flush();
        private void OnDisable() => _driver?.ResetFloor();
        private void OnDestroy() => _driver?.ResetFloor();
    }
}
