// ============================================================================
// AudioDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies pure mixer decisions to Unity Audio sources on the persistent service.
//   An optional pooled soundscape adds spatial cues and synchronized music.
//   All cues, including legacy configs, use the same category-limited soundscape.
//   Dedicated bodily sources render pursuit breathing and proximity/grace heartbeat.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Audio.
//   AudioManager alone commands this engine boundary.
//
// KEY RESPONSIBILITIES:
//   - Route committed world/roster facts into budgeted soundscape playback.
//   - Retain confirmed hand death and admit one correctly identified sting at catch start.
//   - Apply settings, pause, health and floor/run resets without config writes.
//   - Render movement, breathing and grace/proximity heartbeat from supplied observations.
//   - Own and release bodily sources and the pooled soundscape.
//
// DEPENDENCIES:
//   - Core CueId and MovementState; Unity Audio only, with no gameplay queries.
//
// USAGE NOTES:
//   - Persistent tier; all sources are on owned child objects, never scene targets.
//   - Own DriverConfig: AudioDriverConfig; owns AudioListener.pause only during routed pause,
//     restoring the previous value on resume, reset, disable and teardown.
//   - Mixer parameters carry runtime preferences when assigned; source gains are the fallback.
//   - Presentation Update uses unscaled time; Session remains the gameplay tick owner.
//   - Teardown destroys all created sources; disable stops playback immediately.
//   - ResetRun replaces feedback state, rearming the sting on restart and capture reset.
//   - Generation changes clear floor-local soundscape state, not expedition contact memory.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioDriver : MonoBehaviour
    {
        private const string ConfigPath = "ScriptableObjects/Presentation/Audio/AudioDriverConfig";
        private AudioDriverConfig _config;
        private AudioDriverState _state;
        private AudioMixPresenter _presenter;
        private GameObject _sourceRoot;
        private AudioSource _breath;
        private AudioSource _heartbeat;
        private bool _ownerEnabled;
        private AudioSoundscapeDriver _soundscape;
        private AudioFeedbackPresenter _feedback;
        private AudioFeedbackDriverState _feedbackState;

        public float BreathGain => _soundscape != null ? _soundscape.BreathGain : 0f;
        public float HunterGain => 0f; // Nonspatial proximity drone removed from the budget.
        public bool IsInitialized => _state != null;

        public void Initialize(AudioDriverConfig config)
        {
            if (_state != null) return;
            _config = config != null ? config : Resources.Load<AudioDriverConfig>(ConfigPath);
            if (_config == null)
            {
                Debug.LogWarning("Audio config is missing. Run Worsen/Audio/Create Config and Prototype Samples.", this);
                return;
            }
            _state = new AudioDriverState();
            _feedback = new AudioFeedbackPresenter(); _feedbackState = new AudioFeedbackDriverState();
            _presenter = new AudioMixPresenter();
            _sourceRoot = new GameObject("Owned Audio Sources");
            _sourceRoot.transform.SetParent(transform, false);
            _breath = CreateSource(true);
            _heartbeat = CreateSource(false);
            _breath.clip = _config.BreathLoop;
            _soundscape = _sourceRoot.AddComponent<AudioSoundscapeDriver>();
            _soundscape.Initialize(_config.Soundscape, _config);
            _soundscape.SetMasterGain(_config.MasterGain);
            _heartbeat.clip = _soundscape.HeartbeatClip;
            _breath.outputAudioMixerGroup = _heartbeat.outputAudioMixerGroup = _soundscape.Config.EffectsGroup;
            ResetRun();
        }

        public void ApplySettings(PlayerSettingsRecord settings)
        {
            if (_state == null) return;
            _presenter.ApplySettings(_state, settings);
            if (_soundscape != null) _soundscape.ApplySettings(settings, _config.MasterGain);
            ApplyGains();
        }

        public void SetPaused(bool paused)
        {
            if (_state == null) return;
            _state.Paused = paused;
            if (paused && !_state.OwnsListenerPause)
            { _state.PreviousListenerPause = AudioListener.pause; _state.OwnsListenerPause = true; AudioListener.pause = true; }
            else if (!paused && _state.OwnsListenerPause)
            { AudioListener.pause = _state.PreviousListenerPause; _state.OwnsListenerPause = false; }
            if (_soundscape != null) _soundscape.SetPaused(paused);
        }

        private void ApplyGains()
        {
            float gain = _soundscape.EffectsSourceGain;
            _breath.volume = _soundscape.BreathGain * gain;
            _heartbeat.volume = _soundscape.HeartbeatGain * gain;
        }

        public void SetOwnerEnabled(bool ownerEnabled)
        {
            _ownerEnabled = ownerEnabled;
            if (_soundscape != null) _soundscape.SetOwnerEnabled(ownerEnabled);
            if (_state == null) return;
            if (ownerEnabled && isActiveAndEnabled) StartLoops();
            else ResetRun();
        }

        public bool PlayCue(CueId cue)
        {
            if (_state == null || _state.Paused || !_ownerEnabled || !isActiveAndEnabled) return false;
            return _soundscape != null && _soundscape.PlayLocal(cue);
        }

        public bool PlayCatchSting(EntityId player)
        {
            if (_feedback == null || _state == null || !_ownerEnabled || !isActiveAndEnabled) return false;
            return _feedback.TryCatchSting(_feedbackState, player) && _soundscape != null &&
                _soundscape.PlayDeath(_feedbackState.HandDeathPlayer == player);
        }

        public void SetProximity(float closeness) { if (_state != null) _presenter.SetProximity(_state, closeness); }
        public void SetMovementState(MovementState movement) { if (_state != null) _presenter.SetMovementState(_state, movement); }
        public void SetSpeedNormalized(float speed) { if (_state != null) _presenter.SetSpeedNormalized(_state, speed); }
        public void SetInjury(float currentHealth, float maxHealth)
        {
            if (_state == null) return;
            _presenter.SetInjury(_state, currentHealth, maxHealth);
            if (_soundscape != null) _soundscape.SetAlive(_state.CurrentHealth > 0f);
        }
        public void ObserveAfterimage(FlashlightSample sample, float lifetime)
        {
            // Afterimage's continuous mist layer is outside the silence-first budget.
        }

        public void ObserveMovement(PlayerMovementSample sample)
        {
            if (_feedback == null) return;
            SetListenerPosition(sample.Position); SetMovementState(sample.MovementState); SetSpeedNormalized(sample.Velocity.magnitude / 11f);
            _feedback.Movement(_feedbackState, sample); SetAmbience(_feedbackState.Openness, _feedbackState.LocalCollapse); ApplyFeedback();
            if (sample.MovementState == MovementState.Slide && _soundscape != null) _soundscape.Play(CueId.SlideLoop, sample.Position, _feedback.SlideFrictionGain(sample.SlideTurnRateDegrees), -200000);
        }
        public void ObserveTraversal(PlayerTraversalFact fact) { if (_feedback == null) return; _feedback.Traversal(_feedbackState, fact); ApplyFeedback(); }
        public void ObserveHunterFeedback(HunterFeedbackEvent fact) { if (_soundscape != null) _soundscape.ObserveHunter(fact); }
        public void ObserveArchetype(HunterArchetypeFact fact) { if (_soundscape != null) _soundscape.ObserveArchetype(fact); }
        public void ObserveWeaver(WeaverFact fact) { if (_soundscape != null) _soundscape.ObserveWeaver(fact); }
        public void ObserveTicking(TickingSoundFact fact) { if (_soundscape != null) _soundscape.ObserveTicking(fact); }
        public void ObserveHerald(HeraldScreamFact fact) { if (_soundscape != null) _soundscape.ObserveHerald(fact); }
        public void ObserveHeraldBreath(HeraldBreathFact fact) { if (_soundscape != null) _soundscape.ObserveHeraldBreath(fact); }
        public void ObserveHeraldDeafen(HeraldDeafenFact fact) { if (_soundscape != null) _soundscape.ObserveHeraldDeafen(fact); }
        public void ObserveBlinder(BlinderSoundFact fact) { if (_soundscape != null) _soundscape.ObserveBlinder(fact); }
        public void ObserveBlinderHit(BlinderHitFact fact) { if (_soundscape != null) _soundscape.ObserveBlinderHit(fact); }
        public void ObserveRam(RamFact fact) { if (_soundscape != null) _soundscape.ObserveRam(fact); }
        public void ObserveMimic(MimicFact fact) { if (_soundscape != null) _soundscape.ObserveMimic(fact); }
        public void ObserveStare(StareFact fact) { if (_soundscape != null) _soundscape.ObserveStare(fact); }
        public void ObserveMannequin(MannequinFact fact) { if (_soundscape != null) _soundscape.ObserveMannequin(fact); }
        public void ObserveHabit(HunterHabitFact fact) { if (_soundscape != null) _soundscape.ObserveHabit(fact); }
        public void ObserveDeliberation(EntityId hunter, Vector3 position, long tick) { if (_soundscape != null) _soundscape.ObserveDeliberation(hunter, position, tick); }
        public void ObserveProgressionEvent(ProgressionEventFact fact) { if (_soundscape != null) _soundscape.ObserveProgressionEvent(fact); }
        public void ObserveHit(HunterHit fact) { if (_soundscape != null) _soundscape.ObserveHit(fact); }
        public void SetTheme(string zone) { if (_soundscape != null) _soundscape.SetTheme(zone); }
        public void SetRoomTheme(int room, string theme, string family) { if (_soundscape != null) _soundscape.SetRoomTheme(room, theme, family); }
        public void ClearSenses() { if (_soundscape != null) _soundscape.ClearSenses(); }
        public void ObservePickup(PickupCollectedFact fact, Vector3 position) { if (_feedback == null) return; _feedback.Pickup(_feedbackState, fact, position); ApplyFeedback(); }
        public void ObserveHand(CollapseHandFact fact)
        {
            if (_feedback == null) return;
            if (fact.Kind == CollapseHandEventKind.Consumed && fact.PlayerId.IsValid)
                _feedbackState.HandDeathPlayer = fact.PlayerId;
            _feedback.Hand(_feedbackState, fact); ApplyFeedback();
        }
        public void ObserveRoom(RoomDestructionSample sample, Vector3 position) { if (_feedback == null) return; if (_soundscape != null) _soundscape.ObserveDestruction(sample); _feedback.Room(_feedbackState, sample, position); ApplyFeedback(); }
        public void SetRooms(System.Collections.Generic.IReadOnlyList<GeneratedRoomSample> rooms)
        {
            if (_feedbackState == null) return;
            if (_soundscape != null) _soundscape.SetRooms(rooms);
            _feedbackState.Layout.Clear();
            if (rooms != null) foreach (GeneratedRoomSample room in rooms) _feedbackState.Layout[room.RoomId] = room;
        }
        public void SetTorchPositions(int roomId, Vector3[] positions) { if (_soundscape != null) _soundscape.SetTorchPositions(roomId, positions); }
        public void ObserveRoom(RoomDestructionSample sample)
        {
            if (_soundscape != null) _soundscape.ObserveDestruction(sample);
            if (_feedbackState != null && _feedbackState.Layout.TryGetValue(sample.RoomId, out GeneratedRoomSample room)) ObserveRoom(sample, room.Bounds.center);
        }
        public void ObserveHealth(EntityId id, float health, float maximum) { if (_feedback == null) return; SetInjury(health, maximum); _feedback.Health(_feedbackState, id, health, maximum); ApplyFeedback(); }
        public void ObserveProgression(ProgressionSnapshot sample)
        {
            if (_feedback == null) return;
            if (_feedbackState.Generation >= 0 && _feedbackState.Generation != sample.GenerationId && _soundscape != null) _soundscape.ResetRun(true);
            SetInRun(sample.Phase == ProgressionPhase.Exploring);
            _feedback.Progression(_feedbackState, sample); ApplyFeedback();
        }
        public void ObserveTransaction(ProgressionSnapshot previous, ProgressionSnapshot current, ProgressionOperation operation)
        { if (_feedback != null) { _feedback.Transaction(_feedbackState, previous, current, operation); ApplyFeedback(); } }
        public void ObserveExit(FloorDisplaySnapshot sample, Vector3 position)
        { if (_feedback != null) { _feedback.Exit(_feedbackState, sample, position, _soundscape.Config.ExitOpeningThreshold); ApplyFeedback(); } }
        public void SetWorld(LevelGraph graph, System.Collections.Generic.IReadOnlyDictionary<int, bool> doors) { if (_soundscape != null) _soundscape.SetWorld(graph, doors); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { if (_soundscape != null) _soundscape.SetActiveEffects(effects); }
        public void SetInRun(bool inRun) { if (_soundscape != null) _soundscape.SetInRun(inRun); }
        public void SetDeafening(float seconds) { if (_soundscape != null) _soundscape.SetDeafening(seconds); }
        public void SetMuffledDark(float seconds) { if (_soundscape != null) _soundscape.SetMuffledDark(seconds); }
        public void ObserveGrace(GraceWindowFact fact) { if (_soundscape != null) _soundscape.ObserveGrace(); }
        public void ObserveFlashlight(FlashlightSample sample) { if (_feedback == null) return; _feedback.Flashlight(_feedbackState, sample); ApplyFeedback(); }
        private void ApplyFeedback()
        {
            foreach (AudioFeedbackCommand command in _feedbackState.Commands)
                if (command.StopEmitter) StopEmitter(command.Emitter);
                else
                {
                    if (command.Cue == CueId.Land || command.Cue == CueId.SlideEnd) _presenter.MarkFootContact(_state, _config.MixSettings);
                    PlayCueAt(command.Cue, command.Position, command.Gain, command.Emitter);
                }
        }

        public bool PlayCueAt(CueId cue, Vector3 position, float gain = 1f, int emitterId = 0) =>
            _state != null && !_state.Paused && _ownerEnabled && isActiveAndEnabled && _soundscape != null && _soundscape.Play(cue, position, gain, emitterId);
        public void SetThreat(int id, bool chasing, float closeness) { if (_soundscape != null) _soundscape.SetThreat(id, chasing, closeness); }
        public void ObserveProximity(ProximitySample sample) { if (_soundscape != null) _soundscape.ObserveProximity(sample); }
        public void RemoveThreat(int id) { if (_soundscape != null) _soundscape.RemoveThreat(id); }
        public void SetAmbience(float openness, float collapse) { if (_soundscape != null) _soundscape.SetAmbience(openness, collapse); }
        public void SetListenerPosition(Vector3 position) { if (_soundscape != null) _soundscape.SetListenerPosition(position); }
        public void SetFootstepGain(float gain) { if (_soundscape != null) _soundscape.SetFootstepGain(gain); }
        public void StopEmitter(int emitter) { if (_soundscape != null) _soundscape.StopEmitter(emitter); }
        public void SetEmitterOcclusion(int emitter, float amount) { if (_soundscape != null) _soundscape.SetEmitterOcclusion(emitter, amount); }
        public void ResetRun(bool preserveMusicContact = false, bool preserveZones = false)
        {
            SetPaused(false);
            StopSources();
            if (_soundscape != null) _soundscape.ResetRun(preserveMusicContact, preserveZones);
            _feedbackState = new AudioFeedbackDriverState();
            if (_state == null) return;
            _presenter.Reset(_state);
            if (_ownerEnabled && isActiveAndEnabled) StartLoops();
        }

        public void Teardown()
        {
            SetPaused(false);
            StopSources();
            if (_soundscape != null) _soundscape.Teardown();
            _soundscape = null;
            if (_sourceRoot != null)
            {
                if (Application.isPlaying) Destroy(_sourceRoot);
                else DestroyImmediate(_sourceRoot);
            }
            _sourceRoot = null;
            _breath = _heartbeat = null;
            _state = null;
            _presenter = null; _feedback = null; _feedbackState = null;
            _config = null;
            _ownerEnabled = false;
        }

        private void Update()
        {
            if (_state == null || _state.Paused || !_ownerEnabled) return;
            bool step = _presenter.Tick(_state, _config.MixSettings, Time.unscaledDeltaTime);
            _soundscape.TickEmbodiment(_state.CurrentHealth > 0f && _state.CurrentHealth <= _state.MaxHealth * _config.MixSettings.CriticalHealthFraction ? _config.MixSettings.CriticalBreathGain : 0f, Time.unscaledDeltaTime);
            ApplyGains();
            if (_soundscape.Heartbeat && _heartbeat.clip != null) { _heartbeat.Stop(); _heartbeat.Play(); }
            if (_soundscape != null) _soundscape.SetAlive(_state.CurrentHealth > 0f);
            if (step) { if (_soundscape != null) _soundscape.PlayFootstep(); else PlayCue(CueId.Footstep); }
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = _sourceRoot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }


        private void StartLoops()
        {
            if (_breath != null && _breath.clip != null && !_breath.isPlaying) _breath.Play();

        }

        private void StopSources()
        {
            if (_breath != null) { _breath.Stop(); _breath.volume = 0f; }
            if (_heartbeat != null) { _heartbeat.Stop(); _heartbeat.volume = 0f; }
        }

        private void OnEnable() { if (_state != null && _ownerEnabled) { StartLoops(); if (_soundscape != null) _soundscape.SetOwnerEnabled(true); } }
        private void OnDisable()
        {
            SetPaused(false);
            if (_soundscape != null) _soundscape.SetOwnerEnabled(false);
            StopSources();
            if (_state != null) _presenter.Reset(_state);
        }
        private void OnDestroy() => Teardown();
    }
}
