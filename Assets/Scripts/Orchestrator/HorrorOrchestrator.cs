// ============================================================================
// HorrorOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects physical attack facts, authoritative flashlight state and retained curses to the
//   horror presentation system. This keeps enemy decisions and progression rules
//   out of the camera lights, fog, spatial sound and warning visuals.
//   Completed gameplay ticks advance Horror's clock across every generated floor.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Horror presentation target.
// KEY RESPONSIBILITIES:
//   - Route collapse, Weaver, attack, flashlight and authoritative Afterglow facts.
//   - Pair revival catches and return their completion to Session.
//   - Bind world/HUD micro-events and report their presentation outcomes.
//   - Synchronize effects and clear floor-local visuals at lifecycle boundaries.
//   - Advance the whole-run clock and reset its budget only on committed StartRun.
// DEPENDENCIES:
//   - Domain Level commits reopenable door closure; Core views describe injected safe candidates.
//   - Session Run/Progression/HorrorEffects/Expedition, Presentation Horror/Input/Camera/HUD and Core.
//   Authoritative aim is sampled before the Session tick; cosmetic shake never changes it.
// USAGE NOTES:
//   Scene-owned. Configure is called once canonical services are initialized.
//   All subscriptions pair OnEnable/OnDisable; no gameplay state is retained.
//   HorrorEffects consumes input through the Expedition tick. Without that service,
//   this route does not provide an independent flashlight toggle fallback.
//   Progression publishes StartRun transactions before generation (and before initial choices).
//   Generation requests reset only round cues; generation identities and seeds do not identify a run.
//   TickAdvanced is emitted only for accepted gameplay ticks; suspended or ended runs emit none.
//   No certified unreachable anchors exist yet; silhouettes fail closed. Door visibility uses
//   the entire owning room conservatively until exact door bounds are published by Level.
//   Run's typed HunterFacts channel supplies authoritative Afterglow windows.
//   Safety is never inferred here; subscriptions use the same channel on teardown.
// ============================================================================
using UnityEngine;
using System.Collections.Generic;
using Worsen.Domain.Level;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Input;
using Worsen.Presentation.Camera;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Session.Expedition;
using Worsen.Presentation.HUD;
namespace Worsen.Orchestrator
{
    public sealed class HorrorOrchestrator : MonoBehaviour
    {
        private RunSessionManager _run;
        private ProgressionSessionManager _progression;
        private InputManager _input;
        private HorrorManager _horror;
        private HorrorEffectsManager _effects;
        private CameraManager _camera;
        private LevelManager _level;
        private ExpeditionSessionManager _expedition;
        private HUDManager _hud;
        public void ConfigureMicroEvents(LevelManager level, IReadOnlyList<Vector3> unreachableAnchors)
        {
            if (_level != level)
            { OnDisable(); _level = level; if (isActiveAndEnabled) OnEnable(); }
            _effects?.ConfigureConsumableWorld(level);
            _horror.SetMicroEventWorld(level != null ? level.Interactables : null, unreachableAnchors);
        }
        public void OnPlayerOpenedDoor(int id, Bounds bounds) => _horror.ObservePlayerOpenedDoor(id, bounds);
        public void OnActiveEffectsChanged(IReadOnlyActiveEffects effects) => _horror.SetActiveEffects(effects);
        public void Configure(RunSessionManager run, ProgressionSessionManager progression, InputManager input, HorrorManager horror, HorrorEffectsManager effects = null, CameraManager camera = null,
            ExpeditionSessionManager expedition = null, LevelManager level = null, HUDManager hud = null)
        {
            OnDisable(); _run = run; _progression = progression; _input = input; _horror = horror;
            _effects = effects; _camera = camera; _expedition = expedition; _level = level; _hud = hud;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            OnDisable();
            if (_run == null || _progression == null || _input == null || _horror == null) return;
            _run.HunterAttackPublished += OnAttack;
            PairAfterglow(true);
            _run.HunterFacts.WeaverFactPublished += OnWeaver;
            _run.PlayerDeathPending += OnDeathPending;
            if (_camera != null) _camera.CatchHoldEnded += OnRevivalCatchEnded;
            _run.TickAdvanced += OnTickAdvanced;
            _run.FloorFacts.RoomDestructionPublished += OnRoomDestruction;
            _run.ChaseStarted += OnChaseStarted;
            _run.ChaseEnded += OnChaseEnded;
            _run.HunterFacts.ProximityPublished += OnMicroEventProximity;
            _horror.MicroEventSelected += OnMicroEventSelected;
            if (_effects != null) { _effects.FlashlightChanged += OnLight; _effects.AfterimageChanged += OnAfterimage; _run.PlayerMovementPublished += OnMovement; }
            _progression.GenerationRequested += OnGeneration;
            _progression.SnapshotChanged += OnSnapshot;
            _progression.TransactionCommitted += OnTransaction;
            _progression.EffectsSnapshotChanged += OnEffectsSnapshot;
            if (_expedition != null) { _expedition.AssemblyReady += OnAssemblyReady; _expedition.FloorReleased += OnFloorReleased; _expedition.RoomsReady += OnCollapseRooms; }
            if (_level != null) { _level.DoorOpened += OnDoorOpened; _level.InteractableChanged += OnLightChanged; }
            _horror.SetCounterAvailable(_hud != null && _hud.isActiveAndEnabled);
            OnActiveEffectsChanged(_progression.EffectsSnapshot.ActiveEffects);
            if (_level != null && _level.ReadOnlyState.IsReady) ConfigureMicroEvents(_level, System.Array.Empty<Vector3>());
        }
        private void OnDisable()
        {
            if (_expedition != null) { _expedition.AssemblyReady -= OnAssemblyReady; _expedition.FloorReleased -= OnFloorReleased; _expedition.RoomsReady -= OnCollapseRooms; }
            if (_level != null) { _level.DoorOpened -= OnDoorOpened; _level.InteractableChanged -= OnLightChanged; }
            PairAfterglow(false);
            if (_horror != null) { _horror.SetCounterAvailable(false); _horror.ResetRound(); }
            if (_run != null) _run.HunterAttackPublished -= OnAttack;
            if (_run != null) _run.HunterFacts.WeaverFactPublished -= OnWeaver;
            if (_run != null) _run.PlayerDeathPending -= OnDeathPending;
            if (_camera != null) _camera.CatchHoldEnded -= OnRevivalCatchEnded;
            if (_run != null) _run.TickAdvanced -= OnTickAdvanced;
            if (_run != null) _run.FloorFacts.RoomDestructionPublished -= OnRoomDestruction;
            if (_run != null) { _run.ChaseStarted -= OnChaseStarted; _run.ChaseEnded -= OnChaseEnded; }
            if (_run != null) _run.HunterFacts.ProximityPublished -= OnMicroEventProximity;
            if (_horror != null) _horror.InvalidateMicroEventChase();
            if (_horror != null) _horror.MicroEventSelected -= OnMicroEventSelected;
            if (_run != null) _run.PlayerMovementPublished -= OnMovement;
            if (_effects != null) { _effects.FlashlightChanged -= OnLight; _effects.AfterimageChanged -= OnAfterimage; }
            if (_progression != null)
            { _progression.GenerationRequested -= OnGeneration; _progression.SnapshotChanged -= OnSnapshot; _progression.TransactionCommitted -= OnTransaction; _progression.EffectsSnapshotChanged -= OnEffectsSnapshot; }
        }
        private void OnDestroy() => OnDisable();
        private void OnEffectsSnapshot(ProgressionSnapshot snapshot, IReadOnlyActiveEffects effects) => OnActiveEffectsChanged(effects);
        private void OnAssemblyReady(ProgressionGenerationRequest request, Vector3 position, Quaternion rotation)
            => ConfigureMicroEvents(_level, System.Array.Empty<Vector3>());
        private void OnFloorReleased() => _horror.ResetRound();
        private void OnCollapseRooms(IReadOnlyList<GeneratedRoomSample> rooms) => _horror.SetCollapseRooms(rooms,
            _level != null && _level.ReadOnlyState.IsReady ? _level.ReadOnlyState.Graph.ExitRoomId : (int?)null);
        private void OnRoomDestruction(RoomDestructionSample sample) => _horror.ObserveCollapse(sample);
        private void OnDoorOpened(InteractableState door, bool openedByPlayer)
        {
            if (!openedByPlayer || _level == null || !_level.ReadOnlyState.IsReady) return;
            foreach (var room in _level.ReadOnlyState.Graph.Rooms)
                if (room.Id == door.RoomId) { OnPlayerOpenedDoor(door.Id, room.Bounds); return; }
        }
        private void OnLight(FlashlightSample sample) => _horror.SetFlashlight(sample);
        private void PairAfterglow(bool subscribe)
        {
            if (_run == null) return;
            if (subscribe) _run.HunterFacts.AfterglowWindowPublished += OnAfterglowWindow;
            else _run.HunterFacts.AfterglowWindowPublished -= OnAfterglowWindow;
        }
        private void OnAfterglowWindow(int roomId, float seconds)
        {
            if (_level == null || _horror == null) return;
            foreach (var light in _level.Interactables.InRoom(roomId))
                if (light.Kind == InteractableKind.Light && light.Value == InteractableStateValue.Broken)
                    _horror.SetAfterglow(light, seconds);
        }
        private void OnLightChanged(InteractableState before, InteractableState after)
        {
            if (before.Kind == InteractableKind.Light && (after.Kind != InteractableKind.Light || after.Value != InteractableStateValue.Broken))
                _horror.SetAfterglow(before, 0f);
        }
        private void OnDeathPending(EntityId player, Vector3 killer)
        {
            if (_camera == null || !_camera.IsReady || _effects == null || !_effects.TryBeginRevival(player)) return;
            _run.CancelDeathForRevival(player);
            _camera.PlayDeathSnap(killer);
        }
        private void OnRevivalCatchEnded(EntityId player)
        { if (_effects != null && _effects.CompleteRevival(player)) _camera.ResetView(); }
        private void OnAfterimage(FlashlightSample sample, float seconds) => _horror.SetAfterimage(sample, seconds);
        private void OnMovement(PlayerMovementSample sample)
        {
            if (_camera != null) _effects.ObserveAim(new FlashlightSample(sample.Id, sample.Tick, true,
                _camera.AimPosition, _camera.AimRotation * Vector3.forward, 18f, 52f));
        }
        private void OnAttack(HunterAttackSample sample) => _horror.SetAttack(sample);
        private void OnWeaver(WeaverFact fact) => _horror.ObserveWeaver(fact);
        private void OnChaseStarted(ChaseFact fact) => _horror.SetMicroEventChase(fact.ChaseId, true);
        private void OnChaseEnded(ChaseFact fact) => _horror.SetMicroEventChase(fact.ChaseId, false);
        private void OnMicroEventProximity(ProximitySample sample) => _horror.ObserveMicroEventProximity(sample);
        public void OnMicroEventSelected(int kind, int target, Vector3 position, float seconds)
        {
            bool applied = kind == 1 ? _level != null && _level.CloseDoor(target)
                : kind == 2 ? _horror.ShowMicroSilhouette(position, seconds)
                : kind == 3 && _hud != null && _hud.TryShowPhantomCake(seconds);
            _horror.ReportMicroEvent(kind, target, position, seconds, applied);
        }
        private void OnTickAdvanced(InputFrame frame, float deltaSeconds, long tick)
        { _horror.SetCounterAvailable(_hud != null && _hud.isActiveAndEnabled); _horror.AdvanceRunClock(deltaSeconds); }
        private void OnTransaction(ProgressionSnapshot previous, ProgressionSnapshot current, ProgressionOperation operation, string choiceId)
        {
            if (operation != ProgressionOperation.StartRun) return;
            _horror.ResetRun(current.Seed);
            _effects?.ResetRun();
            OnActiveEffectsChanged(_progression.EffectsSnapshot.ActiveEffects);
        }
        private void OnGeneration(ProgressionGenerationRequest request)
        { _horror.ResetRound(); _horror.SetEffects(request.Effects.FogDensityMultiplier, request.Effects.FlashlightRangeMultiplier); OnActiveEffectsChanged(_progression.EffectsSnapshot.ActiveEffects); }
        private void OnSnapshot(ProgressionSnapshot snapshot) => _horror.SetEffects(snapshot.Effects.FogDensityMultiplier, snapshot.Effects.FlashlightRangeMultiplier);
    }
}
