// ============================================================================
// EnvironmentOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes assembled rooms, player position and curse facts to medieval lighting.
//   Keeps map generation and curse rules out of the Environment presentation stack.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Environment target.
// KEY RESPONSIBILITIES:
//   - Pair floor, movement and visual-effect subscriptions with scene lifetime.
//   - Keep decorations and local lighting synchronized with room destruction.
// DEPENDENCIES:
//   Session Expedition/Run/HorrorEffects; Presentation Environment; Core values.
// USAGE NOTES:
//   Scene-owned, explicitly configured after canonical services initialize.
//   Environment owns objects and lighting budgets.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Environment;
namespace Worsen.Orchestrator
{
    public sealed class EnvironmentOrchestrator : MonoBehaviour
    {
        private RunSessionManager _run;
        private ExpeditionSessionManager _expedition;
        private HorrorEffectsManager _effects;
        private EnvironmentManager _environment;
        public void Configure(RunSessionManager run, ExpeditionSessionManager expedition, HorrorEffectsManager effects,
            EnvironmentManager environment)
        { OnDisable(); _run=run; _expedition=expedition; _effects=effects; _environment=environment; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            if (_run == null || _expedition == null || _effects == null || _environment == null) return;
            _expedition.RoomsReady += OnRooms;
            _run.PlayerMovementPublished += OnMovement;
            _run.RoomDestructionPublished += OnDestruction;
            _effects.FlameDimChanged += OnFlame;
            _effects.DoorMarked += OnMark;
        }
        private void OnDisable()
        {
            if (_expedition != null) _expedition.RoomsReady -= OnRooms;
            if (_run != null) { _run.PlayerMovementPublished -= OnMovement; _run.RoomDestructionPublished -= OnDestruction; }
            if (_effects != null) { _effects.FlameDimChanged -= OnFlame; _effects.DoorMarked -= OnMark; }
        }
        private void OnRooms(IReadOnlyList<GeneratedRoomSample> rooms) => _environment.SetRooms(rooms);
        private void OnMovement(PlayerMovementSample sample) => _environment.SetObserver(sample.Position);
        private void OnDestruction(RoomDestructionSample sample) => _environment.SetRoomDestruction(sample.RoomId, sample.Progress);
        private void OnFlame(Vector3 position, float radius, float multiplier) => _environment.SetFlameDim(position, radius, multiplier);
        private void OnMark(int id, Vector3 position) => _environment.MarkDoor(id, position);
    }
}
