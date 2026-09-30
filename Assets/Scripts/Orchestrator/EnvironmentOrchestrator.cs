// ============================================================================
// EnvironmentOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes assembled rooms, player position and curse facts to medieval lighting.
//   Keeps map generation and curse rules out of the Environment presentation stack.
//   Exit rays receive the assembled frame and committed locked/open display state.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Environment target.
// KEY RESPONSIBILITIES:
//   - Pair floor, movement and visual-effect subscriptions with scene lifetime.
//   - Keep decorations and local lighting synchronized with room destruction.
//   - Publish the exit frame after room dressing exists and forward binary opening progress.
// DEPENDENCIES:
//   Session Expedition/Run/HorrorEffects; Presentation Environment; Core values.
//   Domain Level supplies the current graph; FloorDriverConfig supplies the authored door yaw.
// USAGE NOTES:
//   Scene-owned, explicitly configured after canonical services initialize.
//   Environment owns objects and lighting budgets.
//   Floor does not yet publish continuous door progress: Locked maps to 0, Open to 1.
//   The Environment presenter owns easing; replace this mapping when Floor publishes that fact.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Environment;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
namespace Worsen.Orchestrator
{
    public sealed class EnvironmentOrchestrator : MonoBehaviour
    {
        private RunSessionManager _run;
        private ExpeditionSessionManager _expedition;
        private HorrorEffectsManager _effects;
        private EnvironmentManager _environment;
        private LevelManager _level;
        private FloorDriverConfig _floorVisuals;
        public void Configure(RunSessionManager run, ExpeditionSessionManager expedition, HorrorEffectsManager effects,
            EnvironmentManager environment, LevelManager level = null, FloorDriverConfig floorVisuals = null)
        { OnDisable(); _run=run; _expedition=expedition; _effects=effects; _environment=environment; _level=level; _floorVisuals=floorVisuals; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            if (_run == null || _expedition == null || _effects == null || _environment == null) return;
            _expedition.RoomsReady += OnRooms;
            _run.PlayerMovementPublished += OnMovement;
            _run.RoomDestructionPublished += OnDestruction;
            _run.FloorDisplayChanged += OnFloorDisplay;
            _effects.FlameDimChanged += OnFlame;
            _effects.DoorMarked += OnMark;
        }
        private void OnDisable()
        {
            if (_expedition != null) _expedition.RoomsReady -= OnRooms;
            if (_run != null) { _run.PlayerMovementPublished -= OnMovement; _run.RoomDestructionPublished -= OnDestruction; _run.FloorDisplayChanged -= OnFloorDisplay; }
            if (_effects != null) { _effects.FlameDimChanged -= OnFlame; _effects.DoorMarked -= OnMark; }
        }
        private void OnRooms(IReadOnlyList<GeneratedRoomSample> rooms)
        {
            _environment.SetRooms(rooms);
            if (_level == null || !_level.ReadOnlyState.IsReady || _floorVisuals == null) return;
            LevelGraph graph = _level.ReadOnlyState.Graph;
            _environment.SetExitFrame(graph.ExitRoomId, graph.ExitPosition, Quaternion.Euler(0f, _floorVisuals.ExitDoorYaw, 0f));
            _environment.SetExitProgress(0f);
        }
        private void OnFloorDisplay(FloorDisplaySnapshot snapshot) => _environment.SetExitProgress(snapshot.Exit == ExitState.Open ? 1f : 0f);
        private void OnMovement(PlayerMovementSample sample) => _environment.SetObserver(sample.Position);
        private void OnDestruction(RoomDestructionSample sample) => _environment.SetRoomDestruction(sample.RoomId, sample.Progress);
        private void OnFlame(Vector3 position, float radius, float multiplier) => _environment.SetFlameDim(position, radius, multiplier);
        private void OnMark(int id, Vector3 position) => _environment.MarkDoor(id, position);
    }
}
