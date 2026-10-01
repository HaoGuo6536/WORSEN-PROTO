// ============================================================================
// FogOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes assembled rooms and authoritative collapse facts to the fog service.
//   The graph is taken at RoomsReady, after Expedition has initialized Level.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Fog target.
// KEY RESPONSIBILITIES:
//   - Route theme optics before room construction and clear them on floor release.
//   - Pair room and destruction subscriptions and reset on floor replacement.
//   - Forward monotonic whole-room consumption, not resetting per-phase progress.
// DEPENDENCIES:
//   - Session Expedition publishes rooms; Domain Level supplies the Core graph;
//     Domain Floor publishes destruction; Presentation Fog receives Core values.
// USAGE NOTES:
//   Scene-owned. Configure before the first floor assembly. No cached floor data
//   or inferred collapse order. The scene root owner registers this component.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using Worsen.Presentation.Fog;
using Worsen.Session.Expedition;

namespace Worsen.Orchestrator
{
    public sealed class FogOrchestrator : MonoBehaviour
    {
        private ExpeditionSessionManager _expedition;
        private LevelManager _level;
        private FloorManager _floor;
        private FogManager _fog;
        public void Configure(ExpeditionSessionManager expedition, LevelManager level, FloorManager floor, FogManager fog)
        { OnDisable(); _expedition = expedition; _level = level; _floor = floor; _fog = fog; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            if (_expedition == null || _level == null || _floor == null || _fog == null) return;
            _expedition.ThemePublished -= OnTheme;
            _expedition.FloorReleased -= OnFloorReleased;
            _expedition.ThemePublished += OnTheme;
            _expedition.FloorReleased += OnFloorReleased;
            _expedition.RoomsReady -= OnRooms;
            _floor.OnRoomDestruction -= OnDestruction;
            _expedition.RoomsReady += OnRooms;
            _floor.OnRoomDestruction += OnDestruction;
        }
        private void OnDisable()
        {
            if (_expedition != null) _expedition.RoomsReady -= OnRooms;
            if (_expedition != null) { _expedition.ThemePublished -= OnTheme; _expedition.FloorReleased -= OnFloorReleased; }
            if (_floor != null) _floor.OnRoomDestruction -= OnDestruction;
            if (_fog != null) _fog.ResetFloor();
        }
        private void OnTheme(string theme, string light, string sound, string fog, string hands) => _fog.SetLook(fog);
        private void OnFloorReleased() => _fog.ResetFloor();
        private void OnDestroy() => OnDisable();
        private void OnRooms(IReadOnlyList<GeneratedRoomSample> rooms) => _fog.SetRooms(rooms, _level.ReadOnlyState.Graph);
        private void OnDestruction(RoomDestructionSample sample) => _fog.SetRoomProgress(sample.RoomId,
            Mathf.Max(_fog.RoomProgress(sample.RoomId), FloorCollapseFrontUtility.Consumption(sample.Phase, sample.Progress)));
    }
}
