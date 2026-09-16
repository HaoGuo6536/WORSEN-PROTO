// ============================================================================
// AudioOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run and expedition facts into the Audio presentation service.
//   Presentation owns variation, cadence, deduplication and music aggregation;
//   this adapter only connects existing publishers and passes their values on.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Audio target.
// KEY RESPONSIBILITIES:
//   - Pair run, progression, effects, expedition and UI subscriptions symmetrically.
//   - Preserve legacy cue fallback and feed rich threat layers in every scene.
//   - Reset playback at capture start, then restore room and retained-health facts.
// DEPENDENCIES:
//   - Core payloads; Session Run, Progression, HorrorEffects and Expedition.
//   - Presentation Audio target, ProgressionUI feedback and Environment anchor publishers.
// USAGE NOTES:
//   Persistent on the canonical Audio root; base Run reference is persistent.
//   ConfigureExpansion scopes all optional references, including scene-owned UI;
//   the SceneRoot must call ClearExpansion before scene teardown.
//   Capture restores supplied Environment torch anchors after resetting playback.
//   Optional-room cracks already arrive through
//   the authoritative destruction stream and never receive a duplicate cue here.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Expedition;
using Worsen.Presentation.Audio;
using Worsen.Presentation.ProgressionUI;
using Worsen.Presentation.Environment;

namespace Worsen.Orchestrator
{
    public sealed class AudioOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private AudioManager _audio;
        private ProgressionSessionManager _progression;
        private HorrorEffectsManager _effects;
        private ExpeditionSessionManager _expedition;
        private ProgressionUIManager _ui;
        private EnvironmentManager _environment;

        public void ConfigureExpansion(ProgressionSessionManager progression, HorrorEffectsManager effects,
            ExpeditionSessionManager expedition, ProgressionUIManager ui, EnvironmentManager environment = null)
        {
            OnDisable();
            _progression = progression; _effects = effects; _expedition = expedition; _ui = ui; _environment = environment;
            if (isActiveAndEnabled) OnEnable();
        }
        public void ClearExpansion()
        {
            OnDisable();
            _progression = null; _effects = null; _expedition = null; _ui = null; _environment = null;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_audio == null || _audio.Initialize() != _audio) return;
            _run.CaptureStarted += OnCapture;
            _run.ChaseStarted += OnChase;
            _run.ChaseEnded += OnChaseEnd;
            _run.ProximityPublished += OnProximity;
            _run.HealthChanged += OnHealth;
            _run.PlayerMovementPublished += OnMovement;
            _run.PlayerTraversalPublished += OnTraversal;
            _run.HunterFeedbackPublished += OnHunter;
            _run.PickupCollected += OnPickup;
            _run.CollapseHandPublished += OnHand;
            _run.RoomDestructionPublished += OnDestruction;
            _run.SpeedNormalizedPublished += OnSpeed;
            _run.PlayerDied += OnDeath;
            _run.PhaseChanged += OnPhase;
            _run.RoomPhaseChanged += OnRoom;
            if (_progression != null) _progression.SnapshotChanged += OnSnapshot;
            if (_expedition != null) _expedition.RoomsReady += OnRooms;
            if (_ui != null) _ui.Feedback += OnUiFeedback;
            if (_effects == null) return;
            _effects.FlashlightChanged += OnFlashlight;
            _effects.AfterimageChanged += OnAfterimage;
            _effects.NoiseEmitted += OnEcho;
            _effects.DoorMarked += OnDoorMarked;
        }
        private void OnDisable()
        {
            if (_run != null)
            {
                _run.CaptureStarted -= OnCapture;
                _run.ChaseStarted -= OnChase;
                _run.ChaseEnded -= OnChaseEnd;
                _run.ProximityPublished -= OnProximity;
                _run.HealthChanged -= OnHealth;
                _run.PlayerMovementPublished -= OnMovement;
                _run.PlayerTraversalPublished -= OnTraversal;
                _run.HunterFeedbackPublished -= OnHunter;
                _run.PickupCollected -= OnPickup;
                _run.CollapseHandPublished -= OnHand;
                _run.RoomDestructionPublished -= OnDestruction;
                _run.SpeedNormalizedPublished -= OnSpeed;
                _run.PlayerDied -= OnDeath;
                _run.PhaseChanged -= OnPhase;
                _run.RoomPhaseChanged -= OnRoom;
            }
            if (_progression != null) _progression.SnapshotChanged -= OnSnapshot;
            if (_expedition != null) _expedition.RoomsReady -= OnRooms;
            if (_ui != null) _ui.Feedback -= OnUiFeedback;
            if (_effects == null) return;
            _effects.FlashlightChanged -= OnFlashlight;
            _effects.AfterimageChanged -= OnAfterimage;
            _effects.NoiseEmitted -= OnEcho;
            _effects.DoorMarked -= OnDoorMarked;
        }
        private void OnCapture(RunCaptureMetadata metadata)
        {
            _audio.ResetRun();
            if (_expedition != null)
            {
                _audio.SetRooms(_expedition.PresentationRooms);
                if (_environment != null)
                    foreach (GeneratedRoomSample room in _expedition.PresentationRooms)
                        _audio.SetTorchPositions(room.RoomId, _environment.GetTorchPositions(room.RoomId));
            }
            if (_progression != null) _audio.ObserveProgression(_progression.Snapshot);
            if (_expedition != null && _progression != null)
                _audio.ObserveHealth(_expedition.ActivePlayerId, _progression.Snapshot.Health, _progression.Snapshot.MaxHealth);
        }
        private void OnChase(ChaseFact fact)
        {
            _audio.SetThreat(fact.Hunter.Value, true, 0f);
            if (_expedition == null) _audio.PlayCue(CueId.Detection);
        }
        private void OnChaseEnd(ChaseFact fact)
        {
            _audio.RemoveThreat(fact.Hunter.Value);
            if (_expedition == null && fact.EndReason == ChaseEndReason.Lost) _audio.PlayCue(CueId.Lose);
        }
        private void OnProximity(ProximitySample sample)
        {
            _audio.SetThreat(sample.Hunter.Value, sample.InChase, sample.Closeness);
            if (_expedition == null) _audio.SetProximity(sample.Closeness);
        }
        private void OnHealth(EntityId id, float health, float maximum)
        {
            if (_expedition != null) _audio.ObserveHealth(id, health, maximum);
            else _audio.SetInjury(health, maximum);
        }
        private void OnMovement(PlayerMovementSample sample)
        {
            if (_effects != null) _audio.SetFootstepGain(_effects.FootstepLoudnessMultiplier);
            if (_expedition != null) _audio.ObserveMovement(sample);
            else _audio.SetMovementState(sample.MovementState);
        }
        private void OnTraversal(PlayerTraversalFact fact) { if (_expedition != null) _audio.ObserveTraversal(fact); }
        private void OnHunter(HunterFeedbackEvent fact) { if (_expedition != null) _audio.ObserveHunterFeedback(fact); }
        private void OnPickup(PickupCollectedFact fact, Vector3 position) { if (_expedition != null) _audio.ObservePickup(fact, position); }
        private void OnHand(CollapseHandFact fact) { if (_expedition != null) _audio.ObserveHand(fact); }
        private void OnDestruction(RoomDestructionSample sample) { if (_expedition != null) _audio.ObserveRoom(sample); }
        private void OnRooms(IReadOnlyList<GeneratedRoomSample> rooms) => _audio.SetRooms(rooms);
        private void OnSnapshot(ProgressionSnapshot snapshot) => _audio.ObserveProgression(snapshot);
        private void OnFlashlight(FlashlightSample sample) => _audio.ObserveFlashlight(sample);
        private void OnUiFeedback(CueId cue) => _audio.PlayCue(cue);
        private void OnEcho(NoiseEvent fact) => _audio.PlayCueAt(CueId.Footstep, fact.Position, .25f, fact.Source.Value);
        private void OnDoorMarked(int door, Vector3 position) => _audio.PlayCueAt(CueId.TraversalMiss, position, .25f);
        private void OnAfterimage(FlashlightSample sample, float lifetime) => _audio.ObserveAfterimage(sample, lifetime);
        private void OnSpeed(float speed) => _audio.SetSpeedNormalized(speed);
        private void OnDeath(EntityId id, Vector3 position) { if (_expedition == null) _audio.PlayCue(CueId.Death); }
        private void OnPhase(RunPhase phase) { if (phase == RunPhase.ExitOpen) _audio.PlayCue(CueId.ExitOpen); }
        private void OnRoom(RoomPhaseChangedFact fact)
        { if (_expedition == null && fact.Phase == RoomPhase.Telegraph) _audio.PlayCue(CueId.RoomTelegraph); }
    }
}
