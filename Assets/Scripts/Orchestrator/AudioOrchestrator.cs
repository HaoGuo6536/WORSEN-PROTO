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
//   - Route roster, progression tells, world acoustics and committed transactions.
//   - Pair Session, camera, UI and scoped deliberation subscriptions symmetrically.
//   - Route Blinder muffling, Herald deafness, cleansing and pause to Audio.
//   - Preserve legacy cues and replace aggregate threat facts without accumulation.
//   - Reset floor playback while retaining contact only within an expedition.
// DEPENDENCIES:
//   - Domain Hunter registry supplies only scoped deliberation publisher references.
//   - Domain Level supplies Core graph and closed-door snapshots; Floor supplies trap positions.
//   - Core payloads; Session Run, Progression, HorrorEffects and Expedition.
//   - Presentation Audio target, Camera catch, ProgressionUI feedback and Environment anchor publishers.
// USAGE NOTES:
//   Persistent on the canonical Audio root; base Run reference is persistent.
//   ConfigureExpansion scopes all optional references, including scene-owned UI;
//   the SceneRoot must call ClearExpansion before scene teardown.
//   ConfigureCatch/ClearCatch separately scope the scene camera; disable unhooks it.
//   Capture restores supplied Environment torch anchors after resetting playback.
//   Run.Ended is floor-local in Expedition. Progression StartRun/Ended transactions
//   reset contact memory; ordinary floor captures and generation changes do not.
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
using Worsen.Presentation.Camera;
using Worsen.Presentation.ProgressionUI;
using Worsen.Presentation.Environment;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using HunterArchetypeFact = Worsen.Core.HunterArchetypeFact;
using HunterHabitFact = Worsen.Core.HunterHabitFact;

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
        private CameraManager _camera;
        private LevelManager _level;
        private readonly List<HunterManager> _deliberationPublishers = new List<HunterManager>();

        public void ConfigureCatch(CameraManager camera)
        {
            OnDisable(); _camera = camera;
            if (isActiveAndEnabled) OnEnable();
        }
        public void ClearCatch() => ConfigureCatch(null);

        public void ConfigureExpansion(ProgressionSessionManager progression, HorrorEffectsManager effects,
            ExpeditionSessionManager expedition, ProgressionUIManager ui, EnvironmentManager environment = null, LevelManager level = null)
        {
            OnDisable();
            _progression = progression; _effects = effects; _expedition = expedition; _ui = ui; _environment = environment;
            _level = level;
            if (isActiveAndEnabled) OnEnable();
        }
        public void ClearExpansion()
        {
            OnDisable();
            _progression = null; _effects = null; _expedition = null; _ui = null; _environment = null;
            _level = null;
            if (_audio != null) { _audio.SetWorld(null, null); _audio.SetActiveEffects(null); _audio.SetTheme(null); }
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            OnDisable();
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_audio == null || _audio.Initialize() != _audio) return;
            _run.CaptureStarted += OnCapture;
            _run.PauseChanged += OnPause;
            _audio.SetPaused(_run.IsPaused);
            _run.ChaseStarted += OnChase;
            _run.ChaseEnded += OnChaseEnd;
            _run.ProximityPublished += OnProximity;
            _run.HealthChanged += OnHealth;
            _run.PlayerMovementPublished += OnMovement;
            _run.PlayerTraversalPublished += OnTraversal;
            _run.HunterFeedbackPublished += OnHunter;
            _run.HunterArchetypePublished += OnArchetype;
            _run.HunterHabitPublished += OnHabit;
            _run.WeaverFactPublished += OnWeaver;
            _run.TickingSoundPublished += OnTicking;
            _run.BlinderHitPublished += OnBlinderHit;
            _run.HeraldDeafenPublished += OnHeraldDeafen;
            _run.HitAccepted += OnHit;
            _run.BeforeTick += RefreshHunters;
            RefreshHunters();
            _run.PickupCollected += OnPickup;
            _run.CollapseHandPublished += OnHand;
            _run.RoomDestructionPublished += OnDestruction;
            _run.SpeedNormalizedPublished += OnSpeed;
            _run.PhaseChanged += OnPhase;
            _run.RoomPhaseChanged += OnRoom;
            _run.FloorDisplayChanged += OnFloorDisplay;
            _run.OnGraceStarted += OnGrace;
            _run.TrapSprung += OnTrap;
            if (_level != null) { _level.ReadinessChanged += OnLevelReady; _level.InteractableChanged += OnInteractable; }
            if (_camera != null) _camera.CatchHoldStarted += OnCatchStarted;
            if (_progression != null)
            { _progression.SnapshotChanged += OnSnapshot; _progression.TransactionCommitted += OnTransaction; _progression.EffectsSnapshotChanged += OnEffectsSnapshot; _progression.ProgressionEventCommitted += OnProgressionEvent; }
            RefreshViews();
            if (_progression != null) OnSnapshot(_progression.Snapshot);
            if (_expedition != null) { _expedition.RoomsReady += OnRooms; _expedition.ThemePublished += OnTheme; _expedition.RoomThemePublished += OnRoomTheme; _expedition.FloorReleased += OnFloorReleased; }
            if (_ui != null) _ui.Feedback += OnUiFeedback;
            if (_effects == null) return;
            _effects.FlashlightChanged += OnFlashlight;
            _effects.AfterimageChanged += OnAfterimage;
            _effects.NoiseEmitted += OnEcho;
            _effects.DoorMarked += OnDoorMarked;
            _effects.SensesCleansed += OnSensesCleansed;
        }
        private void OnDisable()
        {
            ClearHunters();
            if (_run != null)
            {
                _run.CaptureStarted -= OnCapture;
                _run.PauseChanged -= OnPause;
                _run.ChaseStarted -= OnChase;
                _run.ChaseEnded -= OnChaseEnd;
                _run.ProximityPublished -= OnProximity;
                _run.HealthChanged -= OnHealth;
                _run.PlayerMovementPublished -= OnMovement;
                _run.PlayerTraversalPublished -= OnTraversal;
                _run.HunterFeedbackPublished -= OnHunter;
                _run.HunterArchetypePublished -= OnArchetype;
                _run.HunterHabitPublished -= OnHabit;
                _run.WeaverFactPublished -= OnWeaver;
                _run.TickingSoundPublished -= OnTicking;
                _run.BlinderHitPublished -= OnBlinderHit;
                _run.HeraldDeafenPublished -= OnHeraldDeafen;
                _run.HitAccepted -= OnHit;
                _run.BeforeTick -= RefreshHunters;
                _run.PickupCollected -= OnPickup;
                _run.CollapseHandPublished -= OnHand;
                _run.RoomDestructionPublished -= OnDestruction;
                _run.SpeedNormalizedPublished -= OnSpeed;
                _run.PhaseChanged -= OnPhase;
                _run.RoomPhaseChanged -= OnRoom;
                _run.FloorDisplayChanged -= OnFloorDisplay;
                _run.OnGraceStarted -= OnGrace;
                _run.TrapSprung -= OnTrap;
            }
            if (_level != null) { _level.ReadinessChanged -= OnLevelReady; _level.InteractableChanged -= OnInteractable; }
            if (_camera != null) _camera.CatchHoldStarted -= OnCatchStarted;
            if (_progression != null)
            { _progression.SnapshotChanged -= OnSnapshot; _progression.TransactionCommitted -= OnTransaction; _progression.EffectsSnapshotChanged -= OnEffectsSnapshot; _progression.ProgressionEventCommitted -= OnProgressionEvent; }
            if (_expedition != null) { _expedition.RoomsReady -= OnRooms; _expedition.ThemePublished -= OnTheme; _expedition.RoomThemePublished -= OnRoomTheme; _expedition.FloorReleased -= OnFloorReleased; }
            if (_ui != null) _ui.Feedback -= OnUiFeedback;
            if (_effects == null) return;
            _effects.FlashlightChanged -= OnFlashlight;
            _effects.AfterimageChanged -= OnAfterimage;
            _effects.NoiseEmitted -= OnEcho;
            _effects.DoorMarked -= OnDoorMarked;
            _effects.SensesCleansed -= OnSensesCleansed;
        }
        private void OnCapture(RunCaptureMetadata metadata)
        {
            _audio.ResetRun(_progression != null, true);
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
            RefreshViews();
            _audio.SetInRun(true);
        }
        private void OnChase(ChaseFact fact)
        {
            if (_expedition == null) _audio.PlayCue(CueId.Detection);
        }
        private void OnChaseEnd(ChaseFact fact)
        {
            _audio.ObserveProximity(default);
            if (_expedition == null && fact.EndReason == ChaseEndReason.Lost) _audio.PlayCue(CueId.Lose);
        }
        private void OnProximity(ProximitySample sample)
        {
            _audio.ObserveProximity(sample);
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
        private void OnHunter(HunterFeedbackEvent fact) => _audio.ObserveHunterFeedback(fact);
        private void OnArchetype(HunterArchetypeFact fact) => _audio.ObserveArchetype(fact);
        private void OnHabit(HunterHabitFact fact) => _audio.ObserveHabit(fact);
        private void OnWeaver(WeaverFact fact) => _audio.ObserveWeaver(fact);
        private void OnTicking(TickingSoundFact fact) => _audio.ObserveTicking(fact);
        private void OnBlinderHit(BlinderHitFact fact) { if (fact.MuffledDark) OnMuffledDark(fact.Duration); }
        private void OnHeraldDeafen(HeraldDeafenFact fact) => OnDeafening(fact.Duration);
        private void OnHit(HunterHit fact) => _audio.ObserveHit(fact);
        private void OnDeliberation(EntityId hunter, Vector3 position, long tick)
        { if (_run != null && !_run.IsPaused) _audio.ObserveDeliberation(hunter, position, tick); }
        private void OnProgressionEvent(ProgressionEventFact fact) => _audio.ObserveProgressionEvent(fact);
        private void OnTheme(string theme, string light, string sound, string fog, string hands) => _audio.SetTheme(sound);
        private void OnRoomTheme(int room, string theme, string family) => _audio.SetRoomTheme(room, theme, family);
        private void OnSensesCleansed(SensoryCleanseFact fact) => _audio.ClearSenses();
        private void OnFloorReleased() { ClearHunters(); _audio.ResetRun(true); }
        private void RefreshHunters()
        {
            ClearHunters();
            foreach (var hunter in HunterRegistry.Items)
                if (hunter != null) { hunter.OnDeliberation += OnDeliberation; _deliberationPublishers.Add(hunter); }
        }
        private void ClearHunters()
        {
            foreach (var hunter in _deliberationPublishers) if (hunter != null) hunter.OnDeliberation -= OnDeliberation;
            _deliberationPublishers.Clear();
        }
        private void OnPickup(PickupCollectedFact fact, Vector3 position) { if (_expedition != null) _audio.ObservePickup(fact, position); }
        private void OnHand(CollapseHandFact fact) { if (_expedition != null) _audio.ObserveHand(fact); }
        private void OnDestruction(RoomDestructionSample sample) { if (_expedition != null) _audio.ObserveRoom(sample); }
        private void OnRooms(IReadOnlyList<GeneratedRoomSample> rooms) => _audio.SetRooms(rooms);
        private void OnSnapshot(ProgressionSnapshot snapshot) { _audio.ObserveProgression(snapshot); RefreshViews(); }
        private void OnTransaction(ProgressionSnapshot previous, ProgressionSnapshot current, string operation, string choiceId)
        {
            if (operation == nameof(ProgressionSessionManager.StartRun) || current.Phase == ProgressionPhase.Ended)
                _audio.ResetRun();
            _audio.ObserveTransaction(previous, current, operation);
        }
        private void OnFlashlight(FlashlightSample sample) => _audio.ObserveFlashlight(sample);
        private void OnUiFeedback(CueId cue) => _audio.PlayCue(cue);
        private void OnEcho(NoiseEvent fact) => _audio.PlayCueAt(CueId.Footstep, fact.Position, .25f, fact.Source.Value);
        private void OnDoorMarked(int door, Vector3 position) => _audio.PlayCueAt(CueId.TraversalMiss, position, .25f);
        private void OnAfterimage(FlashlightSample sample, float lifetime) => _audio.ObserveAfterimage(sample, lifetime);
        private void OnSpeed(float speed) => _audio.SetSpeedNormalized(speed);
        private void OnCatchStarted(EntityId player) => _audio.PlayCatchSting(player);
        private void OnPause(bool paused) => _audio.SetPaused(paused);
        private void OnPhase(RunPhase phase)
        {
            // ExitOpen is a logical unlock, not the physical door's opening animation.
            if (phase == RunPhase.Ended) _audio.ResetRun(_progression != null && _progression.Snapshot.Phase != ProgressionPhase.Ended);
        }
        private void OnRoom(RoomPhaseChangedFact fact)
        { if (_expedition == null && fact.Phase == RoomPhase.Telegraph) _audio.PlayCue(CueId.RoomTelegraph); }
        private void RefreshViews()
        {
            _audio.SetWorld(_level != null && _level.ReadOnlyState.IsReady ? _level.ReadOnlyState.Graph : null, _level != null ? _level.ClosedDoors : null);
            _audio.SetActiveEffects(_progression != null ? _progression.EffectsSnapshot.ActiveEffects : null);
        }
        private void OnLevelReady(bool ready) => RefreshViews();
        private void OnInteractable(InteractableState before, InteractableState after) => RefreshViews();
        private void OnEffectsSnapshot(ProgressionSnapshot snapshot, IReadOnlyActiveEffects effects) => _audio.SetActiveEffects(effects);
        private void OnGrace(GraceWindowFact fact) => _audio.ObserveGrace(fact);
        private void OnTrap(FloorTrapSprungFact fact)
        { if (fact.Kind == FloorTrapKind.Announce) _audio.PlayCueAt(CueId.SpikeErupt, fact.Position, 1f, fact.TrapId); }
        private void OnFloorDisplay(FloorDisplaySnapshot snapshot)
        {
            if (_level != null && _level.ReadOnlyState.IsReady) _audio.ObserveExit(snapshot, _level.ReadOnlyState.Graph.ExitPosition);
        }
        public void OnDeafening(float seconds) => _audio.SetDeafening(seconds);
        public void OnMuffledDark(float seconds) => _audio.SetMuffledDark(seconds);
    }
}
