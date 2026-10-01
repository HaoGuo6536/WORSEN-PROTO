// ============================================================================
// EnvironmentOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes assembled rooms, player position and curse facts to medieval lighting.
//   Keeps map generation and curse rules out of the Environment presentation stack.
//   Exit rays receive the assembled frame and continuous committed opening progress.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Environment target.
// KEY RESPONSIBILITIES:
//   - Forward Expedition theme and room-family facts before RoomsReady constructs dressing.
//   - Pair Horror lighting hooks and resynchronize torch density/Wick after floor dressing resets.
//   - Pair floor, movement and visual-effect subscriptions with scene lifetime.
//   - Route authored boundaries/sockets and synchronize dressing with light/destruction facts.
//   - Publish the exit frame after room dressing exists and forward continuous opening progress.
// DEPENDENCIES:
//   Session Expedition/Run (including FloorFacts)/HorrorEffects; Presentation Environment/Horror; Core values.
//   Domain Level supplies graph/light facts; Procedural supplies boundaries/sockets; Floor supplies door yaw.
// USAGE NOTES:
//   Scene-owned, explicitly configured after canonical services initialize.
//   Environment owns objects and lighting budgets.
//   FloorDisplayChanged carries continuous opening progress; Environment owns ray intensity math.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Environment;
using Worsen.Presentation.Horror;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
using Worsen.Domain.Procedural;
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
        private HorrorManager _horror;
        private ProceduralManager _procedural;
        public void Configure(RunSessionManager run, ExpeditionSessionManager expedition, HorrorEffectsManager effects,
            EnvironmentManager environment, LevelManager level = null, FloorDriverConfig floorVisuals = null, HorrorManager horror = null,
            ProceduralManager procedural = null)
        { OnDisable(); _run=run; _expedition=expedition; _effects=effects; _environment=environment; _level=level; _floorVisuals=floorVisuals; _horror=horror; _procedural=procedural; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            OnDisable();
            if (_run == null || _expedition == null || _effects == null || _environment == null) return;
            _expedition.RoomsReady += OnRooms;
            _expedition.ThemePublished += OnTheme;
            _expedition.RoomThemePublished += OnRoomTheme;
            _expedition.FloorReleased += OnFloorReleased;
            if (_level != null) _level.InteractableChanged += OnInteractable;
            _run.PlayerMovementPublished += OnMovement;
            _run.FloorFacts.RoomDestructionPublished += OnDestruction;
            _run.FloorDisplayChanged += OnFloorDisplay;
            _effects.FlameDimChanged += OnFlame;
            _effects.DoorMarked += OnMark;
            if (_horror != null) _horror.LightingHooksChanged += OnLightingHooks;
            SynchronizeLighting();
        }
        private void OnDisable()
        {
            if (_horror != null) _horror.LightingHooksChanged -= OnLightingHooks;
            if (_expedition != null) { _expedition.RoomsReady -= OnRooms; _expedition.FloorReleased -= OnFloorReleased; }
            if (_expedition != null) { _expedition.ThemePublished -= OnTheme; _expedition.RoomThemePublished -= OnRoomTheme; }
            if (_level != null) _level.InteractableChanged -= OnInteractable;
            if (_run != null) { _run.PlayerMovementPublished -= OnMovement; _run.FloorFacts.RoomDestructionPublished -= OnDestruction; _run.FloorDisplayChanged -= OnFloorDisplay; }
            if (_effects != null) { _effects.FlameDimChanged -= OnFlame; _effects.DoorMarked -= OnMark; }
        }
        private void OnTheme(string theme, string light, string sound, string fog, string hands) => _environment.SetTheme(theme, light);
        private void OnRoomTheme(int room, string theme, string family) => _environment.SetRoomTheme(room, theme, family);
        private void OnRooms(IReadOnlyList<GeneratedRoomSample> rooms)
        {
            _environment.SetRooms(rooms, _procedural != null ? _procedural.RoomBoundary : null,
                _procedural != null ? _procedural.RoomLightSockets : null);
            SynchronizeLighting();
            if (_level != null && rooms != null)
                foreach (var room in rooms) BindLights(room.RoomId);
            if (_level == null || !_level.ReadOnlyState.IsReady || _floorVisuals == null) return;
            LevelGraph graph = _level.ReadOnlyState.Graph;
            _environment.SetExitFrame(graph.ExitRoomId, graph.ExitPosition, Quaternion.Euler(0f, _floorVisuals.ExitDoorYaw, 0f));
            _environment.SetExitProgress(0f);
        }
        private void BindLights(int roomId)
        { foreach (var light in _level.Interactables.InRoom(roomId)) _environment.ApplyLight(light); }
        private void OnInteractable(InteractableState before, InteractableState after) => _environment.ApplyLight(after);
        private void OnDestroy() => OnDisable();
        private void OnLightingHooks(float torches, bool wick)
        { _environment.SetTorchCountMultiplier(torches); _environment.SetLightingHooks(false, wick); }
        private void SynchronizeLighting() => OnLightingHooks(_horror != null ? _horror.TorchCountMultiplier : 1f, _horror != null && _horror.Wick);
        private void OnFloorReleased() { _environment.BeginFloor(); SynchronizeLighting(); }
        private void OnFloorDisplay(FloorDisplaySnapshot snapshot) => _environment.SetExitProgress(snapshot.OpeningProgress);
        private void OnMovement(PlayerMovementSample sample) => _environment.SetObserver(sample.Position);
        private void OnDestruction(RoomDestructionSample sample) => _environment.SetRoomDestruction(sample.RoomId, sample.Progress);
        private void OnFlame(Vector3 position, float radius, float multiplier) => _environment.SetFlameDim(position, radius, multiplier);
        private void OnMark(int id, Vector3 position) => _environment.MarkDoor(id, position);
    }
}
