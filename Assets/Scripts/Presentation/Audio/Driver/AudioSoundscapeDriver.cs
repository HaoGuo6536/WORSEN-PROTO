// ============================================================================
// AudioSoundscapeDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies pooled spatial effects, adaptive impact/danger loops and a scheduled run sequence.
//   It receives room anchors and portal evidence as facts, and never reads enemy state.
//   World voices use Unity distance rolloff and shared portal loss, never both distance models.
//   Missing roster clips and unknown catch identities stay silent, never borrowing legacy vocals.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by AudioDriver · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Apply roster cues, exact tells, zone filters and confirmed hand versus hunter death cues.
//   - Enforce category budgets and spatial attenuation through the pure presentation stack.
//   - Own pooled sources and scheduled adaptive music, releasing playback on reset/teardown.
//   - Apply runtime gains, pause and living-state guards without altering configs.
//   - Replace chase snapshots and supply seeded cosmetic timing to music decisions.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Persistent tier, owned by the persistent AudioDriver; no global audio settings change.
//   Own DriverConfig: AudioSoundscapeDriverConfig. All created children are destroyed on teardown.
//   Portal occlusion must be pushed by the owning Manager; empty configuration produces visible missing-bank warnings.
//   Floor reset retains only music contact/floor and the cosmetic random stream;
//   full run reset clears them. AudioThreatSample.Chasing carries belief for live snapshots.
//   MixerVolumeRequest records actual SetFloat arguments/results, not an audible gain
//   or a snapshot value; editor readback must not be used to infer whether a call occurred.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioSoundscapeDriver : MonoBehaviour
    {
        private AudioSoundscapeDriverConfig _config;
        private AudioSoundscapeDriverState _state;
        private AudioSoundscapePresenter _presenter;
        private AudioSource[] _voices;
        private AudioLowPassFilter[] _filters;
        private AudioSource[] _layers;
        private AudioSource[] _runSources;
        private AudioChaseMusicDriverState _music;
        private AudioChaseMusicPresenter _musicPresenter;
        private AudioLowPassFilter[] _layerFilters;
        private AudioLowPassFilter[] _runFilters;
        private AudioWorldMixPresenter _worldPresenter;
        private readonly AudioCueCataloguePresenter _catalogue = new AudioCueCataloguePresenter();
        private readonly AudioRosterPresenter _rosterPresenter = new AudioRosterPresenter();
        private readonly AudioRosterDriverState _roster = new AudioRosterDriverState();
        private bool _ownsConfig;
        private bool _missingHeartbeatWarned;
        private GameObject _root;
        private readonly Dictionary<CueId, AudioSoundDefinition> _banks = new Dictionary<CueId, AudioSoundDefinition>();
        private readonly Dictionary<CueId, float[]> _durations = new Dictionary<CueId, float[]>();
        private float _master = 1f;
        public float EffectsSourceGain => _master * (_state != null ? _state.RuntimeEffects : 1f);
        public (AudioMixer Mixer, float Master, float Music, float Effects,
            bool MasterAccepted, bool MusicAccepted, bool EffectsAccepted)? MixerVolumeRequest => _state?.MixerVolumeRequest;
        public float BreathGain => _state != null ? _state.WorldMix.BreathGain : 0f;
        public bool Heartbeat => _state != null && _state.WorldMix.Heartbeat;
        public float HeartbeatEnvelope => _state != null && _state.OwnerEnabled && _state.InRun && _state.Alive && !_state.Paused && isActiveAndEnabled ? _state.WorldMix.HeartbeatEnvelope : 0f;
        public float HeartbeatGain => _state != null ? _state.WorldMix.HeartbeatStrength * _config.HeartbeatGain : 0f;
        public AudioClip HeartbeatClip => _config != null ? _config.HeartbeatClip : null;
        public AudioSoundscapeDriverConfig Config => _config;
        public float ChaseGain => _music != null ? _music.StressGain : 0f;
        public float DangerGain => _music != null ? _music.DangerGain : 0f;
        public int OwnedVoiceCount => _voices != null ? _voices.Length : 0;
        public void Initialize(AudioSoundscapeDriverConfig config, AudioDriverConfig legacy = null)
        {
            if (_state != null) return;
            if (config == null) { config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>(); _ownsConfig = true; }
            _config = config; _state = new AudioSoundscapeDriverState();
            _worldPresenter = new AudioWorldMixPresenter(); _presenter = new AudioSoundscapePresenter();
            _presenter.Reset(_state, config.VoiceCount, new System.Random(76103));
            _root = new GameObject("Owned Soundscape Sources"); _root.transform.SetParent(transform, false);
            _voices = new AudioSource[_state.Voices.Length]; _filters = new AudioLowPassFilter[_voices.Length];
            _state.VoiceGains = new float[_voices.Length];
            for (int i = 0; i < _voices.Length; i++)
            { _voices[i] = CreateSource("Effect " + i, false, null, config.EffectsGroup); _filters[i] = _voices[i].gameObject.AddComponent<AudioLowPassFilter>(); _filters[i].cutoffFrequency = 22000f; }
            _layers = new[] { CreateSource("Tension", true, config.TensionStem, config.MusicGroup),
                CreateSource("Chase", true, config.ChaseStem, config.MusicGroup), CreateSource("Danger", true, config.DangerStem, config.MusicGroup),
                CreateSource("Interior", true, config.InteriorAmbience, config.AmbienceGroup), CreateSource("Exterior", true, config.ExteriorAmbience, config.AmbienceGroup) };
            foreach (AudioSoundDefinition bank in config.Sounds) RegisterBank(bank);
            if (_ownsConfig && legacy != null) foreach (var cue in legacy.Cues)
                RegisterBank(new AudioSoundDefinition { Cue = cue.Cue, Clips = new[] { cue.Clip }, Gain = cue.Gain,
                    Priority = cue.Priority, PitchMinimum = 1f, PitchMaximum = 1f, MaxConcurrent = 1 });
            _layerFilters = AddFilters(_layers);
            _music = new AudioChaseMusicDriverState { ImpactPitch = config.ImpactMinimumPitch };
            _musicPresenter = new AudioChaseMusicPresenter();
            if (config.RunIntro != null || config.RunLoop != null || config.RunEnd != null)
                _runSources = new[] { CreateSource("Run Intro", false, config.RunIntro, config.MusicGroup),
                    CreateSource("Run Loop", true, config.RunLoop, config.MusicGroup), CreateSource("Run End", false, config.RunEnd, config.MusicGroup) };
            _runFilters = AddFilters(_runSources);
        }
        private void RegisterBank(AudioSoundDefinition bank)
        {
            if (!_catalogue.TryGet(bank.Cue, out var entry)) return;
            bank.Cue = _catalogue.Canonical(bank.Cue);
            if (_banks.ContainsKey(bank.Cue)) return;
            bank.Spatial = entry.Noise.HasValue;
            bank.Loop = bank.Cue == CueId.SlideLoop || bank.Cue == CueId.PlayerCritical;
            bank.Ambience = false;
            _banks[bank.Cue] = bank;
            float[] durations = new float[bank.Clips != null ? bank.Clips.Length : 0];
            for (int i = 0; i < durations.Length; i++) durations[i] = bank.Clips[i] != null ? bank.Clips[i].length : 0f;
            _durations[bank.Cue] = durations;
        }
        private AudioLowPassFilter[] AddFilters(AudioSource[] sources)
        {
            var filters = new AudioLowPassFilter[sources != null ? sources.Length : 0];
            for (int i = 0; i < filters.Length; i++) filters[i] = sources[i].gameObject.AddComponent<AudioLowPassFilter>();
            return filters;
        }
        public void SetOwnerEnabled(bool value)
        {
            if (_state == null) return;
            _state.OwnerEnabled = value;
            if (value && isActiveAndEnabled) StartLayers(); else ResetRun();
        }
        public bool Play(CueId cue, Vector3 position, float gain, int emitter, bool presentationOnly = false)
            => PlayBank(cue, position, gain, emitter, presentationOnly, false);
        private bool PlayBank(CueId cue, Vector3 position, float gain, int emitter, bool presentationOnly, bool handDeath)
        {
            if (_state == null || _state.Paused || !_state.OwnerEnabled || !isActiveAndEnabled) return false;
            if (!_catalogue.Admits(cue, _state.InRun)) return false;
            if (_catalogue.TryGet(cue, out var category) && category.Category == CueCategory.Hunter &&
                !(handDeath && cue == CueId.Death) && !_rosterPresenter.AllowsSharedEmitter(_roster, emitter))
            {
                string missing = "shared:" + emitter + ":" + cue;
                if (_roster.Missing.Add(missing)) Debug.LogWarning("Hunter cue '" + cue + "' requires an identified legacy emitter or a named roster binding; silent.", this);
                return false;
            }
            if (presentationOnly && (!_catalogue.FalsePositiveExempt(cue) || _state.WorldMix.InChase)) return false;
            cue = _catalogue.Canonical(cue);
            if (cue == CueId.PlayerCritical) return false; // The dedicated breathing source owns this slot.
            if (!_banks.TryGetValue(cue, out AudioSoundDefinition bank) || !_durations.TryGetValue(cue, out float[] durations) || !System.Array.Exists(durations, duration => duration > 0f))
            {
                if (_state.MissingWarnings.Add((int)cue)) Debug.LogWarning("Soundscape cue '" + cue + "' is not wired to a playable bank.", this);
                return false;
            }
            if (!_state.Alive && (_presenter.IsEnemyCue(cue) || bank.Loop && !bank.Ambience)) return false;
            if (!_presenter.TryPlay(_state, bank, emitter, durations, gain, out AudioPlaybackSample request, _config.TimingJitterSeconds)) return false;
            ApplyPlayback(bank, request, position, presentationOnly);
            return true;
        }
        private void ApplyPlayback(AudioSoundDefinition bank, AudioPlaybackSample request, Vector3 position, bool presentationOnly = false)
        {
            AudioSource source = _voices[request.Voice]; source.transform.position = position;
            _state.Voices[request.Voice].Position = position;
            _state.Voices[request.Voice].PresentationOnly = presentationOnly;
            _state.VoiceGains[request.Voice] = request.Gain;
            if (request.ReuseLoop)
            {
                ApplyVoiceGain(request.Voice);
                return;
            }
            source.Stop(); source.clip = bank.Clips[request.Clip]; source.loop = bank.Loop;
            source.spatialBlend = bank.Spatial ? 1f : 0f; source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.spatialize = bank.Spatial && !string.IsNullOrEmpty(AudioSettings.GetSpatializerPluginName());
            source.minDistance = Mathf.Max(0.1f, bank.MinimumDistance); source.maxDistance = Mathf.Max(source.minDistance + 0.1f, bank.MaximumDistance);
            source.priority = Mathf.Clamp(256 - bank.Priority * 2, 0, 256);
            source.pitch = request.Pitch;
            source.outputAudioMixerGroup = bank.Ambience ? _config.AmbienceGroup : _config.EffectsGroup;
            ApplyVoiceGain(request.Voice);
            if (request.Delay > 0f) source.PlayScheduled(AudioSettings.dspTime + request.Delay); else source.Play();
        }
        public void ObserveArchetype(HunterArchetypeFact fact)
        { if (RosterReady && _rosterPresenter.Archetype(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveWeaver(WeaverFact fact)
        { if (RosterReady && _rosterPresenter.Weaver(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveTicking(TickingSoundFact fact)
        { if (RosterReady && _rosterPresenter.Ticking(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveHerald(HeraldScreamFact fact)
        { if (RosterReady && _rosterPresenter.Herald(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveHeraldBreath(HeraldBreathFact fact)
        { if (RosterReady && _rosterPresenter.HeraldBreath(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveHeraldDeafen(HeraldDeafenFact fact)
        { if (RosterReady && _rosterPresenter.AcceptHeraldDeafen(_roster, fact)) { _worldPresenter.HeraldDeafen(_state.WorldMix, fact, _config.EarPlugsDurationMultiplier); ApplyGains(); } }
        public void ObserveBlinder(BlinderSoundFact fact)
        { if (RosterReady && _rosterPresenter.Blinder(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveBlinderHit(BlinderHitFact fact)
        { if (RosterReady && _rosterPresenter.AcceptBlinderHit(_roster, fact)) { _worldPresenter.BlinderHit(_state.WorldMix, fact, _config.MirrorSkinDurationMultiplier); ApplyGains(); } }
        public void ObserveRam(RamFact fact)
        { if (RosterReady && _rosterPresenter.Ram(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveMimic(MimicFact fact)
        { if (RosterReady && _rosterPresenter.Mimic(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveStare(StareFact fact)
        { if (RosterReady && _rosterPresenter.Stare(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveMannequin(MannequinFact fact)
        { if (RosterReady) _rosterPresenter.Mannequin(_roster, fact); }
        public void ObserveHabit(HunterHabitFact fact)
        { if (RosterReady && _rosterPresenter.Habit(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveDeliberation(EntityId hunter, Vector3 position, long tick)
        { if (RosterReady && _rosterPresenter.Deliberation(_roster, hunter, position, tick, out var command)) PlayRoster(command); }
        public void ObserveHunter(HunterFeedbackEvent fact)
        { if (RosterReady && _rosterPresenter.Feedback(_roster, fact, out var command)) PlayRoster(command); }
        public void ObserveProgressionEvent(ProgressionEventFact fact)
        { if (_state != null) { _rosterPresenter.Mutation(_roster, fact); FlushTells(); } }
        public void ObserveHit(HunterHit fact) { _roster.LastAttacker = fact.Hunter; }
        public bool PlayDeath(bool handDeath = false)
        {
            if (handDeath) return _state != null && PlayBank(CueId.Death, _state.ListenerPosition, 1f, 0, false, true);
            return PlayRoster(new AudioRosterCommand { Hunter = _roster.LastAttacker, Id = _rosterPresenter.DeathId(_roster), Slot = HunterCueSlot.DeathSting, Gain = 1f, Pitch = 1f, Exact = true });
        }
        private bool RosterReady => _state != null && _state.Alive && !_state.Paused && _state.OwnerEnabled && isActiveAndEnabled;
        private void FlushTells()
        {
            if (!RosterReady || !_state.InRun) return;
            foreach (var voice in _state.Voices)
                if (voice.Remaining > 0f && voice.Emitter == 0 && voice.Catalogue.Category == CueCategory.Hunter &&
                    voice.Catalogue.Slot == (int)HunterCueSlot.Presence) return;
            while (_roster.PendingTells.Count > 0)
            {
                var command = _roster.PendingTells[0];
                bool played = PlayRoster(command);
                if (!played && !_roster.Missing.Contains(command.Id)) return; // Retry budget pressure, not missing assets.
                _roster.PendingTells.RemoveAt(0);
                if (played) return;
            }
        }
        private bool PlayRoster(AudioRosterCommand command)
        {
            if (_state == null || _state.Paused || !_state.OwnerEnabled || !isActiveAndEnabled) return false;
            if (command.Stop)
            {
                for (int i = 0; i < _state.Voices.Length; i++)
                    if (_state.Voices[i].Emitter == command.Hunter.Value && _state.Voices[i].Catalogue.Category == CueCategory.Hunter) StopVoice(i);
                return true;
            }
            AudioRosterBinding? binding = _rosterPresenter.OwnsCommand(_roster, command) ?
                _rosterPresenter.Binding(_config, command.Id) : null;
            // Authored silence is not a missing asset and must not spend a voice.
            if (binding.HasValue && binding.Value.Placeholder && binding.Value.OverrideGain && binding.Value.Gain == 0f) return true;
            AudioSoundDefinition bank = default;
            if (!binding.HasValue || binding.Value.Clip == null && (binding.Value.Placeholder ||
                !_rosterPresenter.AllowsSharedBank(command.Id) || !_banks.TryGetValue(_catalogue.Canonical(binding.Value.Bank), out bank)))
            {
                if (_roster.Missing.Add(command.Id)) Debug.LogWarning("Roster cue '" + command.Id + "' has no clip; silent placeholder.", this);
                return false;
            }
            if (binding.Value.Clip != null)
            {
                var clips = new AudioClip[1 + (binding.Value.Alternates?.Length ?? 0)]; clips[0] = binding.Value.Clip;
                if (binding.Value.Alternates != null) System.Array.Copy(binding.Value.Alternates, 0, clips, 1, binding.Value.Alternates.Length);
                bank = new AudioSoundDefinition { Cue = binding.Value.Bank, Clips = clips,
                    Gain = binding.Value.OverrideGain ? binding.Value.Gain : _config.RosterClipGain,
                    MinimumDistance = _config.Hearing.ReferenceDistance, MaximumDistance = _config.RosterMaximumDistance,
                    Priority = _config.RosterClipPriority, PitchMinimum = 1f - _config.RosterPitchVariation,
                    PitchMaximum = 1f + _config.RosterPitchVariation, GainVariation = _config.RosterGainVariation };
            }
            bool local = !command.Hunter.IsValid || command.Slot == HunterCueSlot.DeathSting;
            bool protect = command.Slot == HunterCueSlot.Presence || command.Slot == HunterCueSlot.AttackTiming;
            var entry = new AudioCueCatalogueEntry(CueCategory.Hunter, (int)command.Slot,
                local ? (NoiseSourceKind?)null : NoiseSourceKind.Other, protect, command.Exact);
            bank.Loop = false; bank.Spatial = !local;
            if (command.Exact) { bank.PitchMinimum = bank.PitchMaximum = command.Pitch; bank.GainVariation = 0f; bank.Cooldown = 0f; }
            var durations = new float[bank.Clips != null ? bank.Clips.Length : 0];
            for (int i = 0; i < durations.Length; i++) durations[i] = bank.Clips[i] != null ? bank.Clips[i].length : 0f;
            if (!System.Array.Exists(durations, duration => duration > 0f))
            {
                if (_roster.Missing.Add(command.Id)) Debug.LogWarning("Roster cue '" + command.Id + "' has no playable clip; silent placeholder.", this);
                return false;
            }
            if (!_presenter.TryPlay(_state, bank, command.Hunter.Value, durations, command.Gain, out var request,
                _config.TimingJitterSeconds, entry, command.Exact)) return false;
            if (command.Interval > 0f) _state.Voices[request.Voice].Remaining = Mathf.Min(_state.Voices[request.Voice].Remaining, command.Interval);
            ApplyPlayback(bank, request, local ? _state.ListenerPosition : command.Position);
            return true;
        }
        public void SetTheme(string zone) { _rosterPresenter.Theme(_roster, zone); if (_state != null) ApplyGains(); }
        public void SetRoomTheme(int room, string theme, string family)
        { if (_config != null) { _rosterPresenter.RoomTheme(_roster, _config, room, theme, family); if (_state != null) ApplyGains(); } }
        public void ClearSenses()
        { if (_state != null) { _state.WorldMix.DeafenedRemaining = _state.WorldMix.MuffledRemaining = 0f; ApplyGains(); } }
        public bool PlayLocal(CueId cue) => _state != null && Play(cue, _state.ListenerPosition, 1f, 0);
        public CueId ResolveFootstep(Vector3 position)
        {
            return CueId.Footstep; // Surface variants wait until they affect hunter hearing.
        }
        public void SetRooms(IReadOnlyList<GeneratedRoomSample> rooms) { } // Topology is supplied separately.
        public void SetTorchPositions(int roomId, Vector3[] positions) { } // Continuous torch bed removed.
        public void ObserveDestruction(RoomDestructionSample sample) { if (_state != null) _state.WorldMix.Rooms[sample.RoomId] = sample; }
        public void SetWorld(LevelGraph graph, IReadOnlyDictionary<int, bool> doors) { if (_state != null) _worldPresenter.SetWorld(_state.WorldMix, graph, doors); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { if (_state != null) { _worldPresenter.SetEffects(_state.WorldMix, effects); ApplyGains(); } }
        public void SetInRun(bool inRun)
        {
            if (_state == null) return;
            _state.InRun = inRun;
            FlushTells();
            for (int i = 0; i < _state.Voices.Length; i++)
                if (_state.Voices[i].Remaining > 0f && (_state.Voices[i].Catalogue.Category == CueCategory.Interface) == inRun) StopVoice(i);
        }
        public void SetDeafening(float seconds) { if (_state != null) _worldPresenter.Deafening(_state.WorldMix, seconds); }
        public void SetMuffledDark(float seconds) { if (_state != null) _worldPresenter.MuffledDark(_state.WorldMix, seconds); }
        public void ObserveGrace() { if (_state != null) _worldPresenter.Grace(_state.WorldMix, _config); }
        public void TickEmbodiment(float injuryBreath, float dt)
        {
            if (_state == null) return;
            _worldPresenter.Tick(_state.WorldMix, _config, _state.InRun, _state.Alive, injuryBreath, dt);
            if (_state.WorldMix.Heartbeat && HeartbeatClip == null && !_missingHeartbeatWarned)
            {
                _missingHeartbeatWarned = true;
                Debug.LogWarning("Heartbeat clip is missing; heartbeat audio is silent (envelope retained).", this);
            }
            foreach (var command in _state.WorldMix.Commands) Play(command.Cue, command.Position, command.Gain, command.Emitter);
        }
        public void SetThreat(int id, bool chasing, float closeness)
        { if (_state != null) _state.Threats[id] = new AudioThreatSample { Chasing = chasing, Closeness = closeness }; }
        public void RemoveThreat(int id) { if (_state != null) _state.Threats.Remove(id); }
        public void ObserveProximity(ProximitySample sample)
        {
            if (_state == null) return;
            _state.Threats.Clear();
            _state.WorldMix.InChase = sample.InChase;
            _state.WorldMix.Closeness = sample.ActualCloseness;
            if (sample.Hunter.IsValid) SetThreat(sample.Hunter.Value, sample.HasBelief, sample.ActualCloseness);
        }
        public void SetAmbience(float openness, float collapse)
        { if (_state != null) { _state.Openness = openness; _state.Collapse = collapse; } }
        public void SetListenerPosition(Vector3 position) { if (_state != null) { _state.ListenerPosition = position; _state.WorldMix.Listener = position; } }
        public void SetFootstepGain(float gain) { if (_state != null) _state.FootstepGain = Mathf.Clamp01(gain); }
        public void SetAlive(bool alive)
        {
            if (_state == null || _state.Alive == alive) return;
            _state.Alive = alive;
            if (alive) return;
            StopRunSources();
            _music = new AudioChaseMusicDriverState { ImpactPitch = _config.ImpactMinimumPitch };
            for (int i = 0; i < 3; i++) _layers[i].volume = 0f;
            for (int i = 0; i < _state.Voices.Length; i++)
                if (_state.Voices[i].Catalogue.Category == CueCategory.Hunter && _state.Voices[i].Catalogue.Slot != (int)HunterCueSlot.DeathSting ||
                    _presenter.IsEnemyCue((CueId)_state.Voices[i].Cue) ||
                    _state.Voices[i].Loop && _banks.TryGetValue((CueId)_state.Voices[i].Cue, out AudioSoundDefinition bank) && !bank.Ambience)
                    StopVoice(i);
        }
        public void StopCueEmitter(CueId cue, int emitter)
        {
            if (_state == null) return;
            cue = _catalogue.Canonical(cue);
            for (int i = 0; i < _state.Voices.Length; i++)
                if (_state.Voices[i].Cue == (int)cue && _state.Voices[i].Emitter == emitter) StopVoice(i);
        }
        private void StopVoice(int index)
        { _voices[index].Stop(); _voices[index].clip = null; _voices[index].loop = false; _state.Voices[index] = default; }
        public bool PlayFootstep() => _state != null && Play(CueId.Footstep, _state.ListenerPosition, _state.FootstepGain, 0);
        public void StopEmitter(int emitter)
        {
            if (_state == null) return;
            for (int i = 0; i < _state.Voices.Length; i++)
                if (_state.Voices[i].Emitter == emitter) StopVoice(i);
        }
        public void SetEmitterOcclusion(int emitter, float occlusion)
        {
            // Compatibility entry point: graph/door state is now the sole occlusion authority.
        }
        public void SetMasterGain(float gain) { _master = Mathf.Clamp01(gain); }
        public void ApplySettings(PlayerSettingsRecord settings, float designerMaster)
        {
            var volume = new AudioVolumePresenter();
            bool wired = _config.Mixer != null && _config.EffectsGroup != null && _config.MusicGroup != null &&
                _config.AmbienceGroup != null && _config.EffectsGroup.audioMixer == _config.Mixer &&
                _config.MusicGroup.audioMixer == _config.Mixer && _config.AmbienceGroup.audioMixer == _config.Mixer;
            float masterDb = volume.Decibels(settings.MasterVolume), musicDb = volume.Decibels(settings.MusicVolume),
                effectsDb = volume.Decibels(settings.EffectsVolume);
            bool master = wired && _config.Mixer.SetFloat(_config.MasterParameter, masterDb);
            bool music = wired && _config.Mixer.SetFloat(_config.MusicParameter, musicDb);
            bool effects = wired && _config.Mixer.SetFloat(_config.EffectsParameter, effectsDb);
            _state.MixerVolumeRequest = wired ? (_config.Mixer, masterDb, musicDb, effectsDb, master, music, effects) :
                ((AudioMixer, float, float, float, bool, bool, bool)?)null;
            SetRuntimeGains(volume.Linear(designerMaster) * volume.SourceGain(settings.MasterVolume, master),
                volume.SourceGain(settings.MusicVolume, music), volume.SourceGain(settings.EffectsVolume, effects));
        }
        public void SetPaused(bool paused) { if (_state != null) _state.Paused = paused; }
        public void SetRuntimeGains(float master, float music, float effects)
        {
            if (_state == null) return;
            _master = master; _state.RuntimeMusic = music; _state.RuntimeEffects = effects;
            ApplyGains();
        }
        private void ApplyGains()
        {
            float mask = _worldPresenter.Mask(_state.WorldMix, false, _config);
            float music = _master * _state.RuntimeMusic * _config.MusicGain * (1f - _state.Duck) * mask;
            _layers[0].volume = _music.TensionGain * music; _layers[1].volume = _music.StressGain * music; _layers[2].volume = _music.DangerGain * music;
            _layers[3].volume = _state.InteriorGain * _master * _state.RuntimeEffects * _config.AmbienceGain * mask;
            _layers[4].volume = _state.ExteriorGain * _master * _state.RuntimeEffects * _config.AmbienceGain * mask;
            if (_runSources != null) foreach (AudioSource source in _runSources) source.volume = _state.Alive ? music * _config.RunGain : 0f;
            for (int i = 0; i < _voices.Length; i++) ApplyVoiceGain(i);
            float cutoff = _worldPresenter.Cutoff(_state.WorldMix, false, _config);
            foreach (var filter in _layerFilters) filter.cutoffFrequency = cutoff;
            foreach (var filter in _runFilters) filter.cutoffFrequency = cutoff;
        }
        private void ApplyVoiceGain(int i)
        {
            var voice = _state.Voices[i];
            if (voice.Catalogue.Category == CueCategory.Hunter && voice.Catalogue.Slot == (int)HunterCueSlot.Presence)
                _voices[i].minDistance = _config.Hearing.ReferenceDistance * (_state.WorldMix.KeenEars ? _config.KeenEarsRangeMultiplier : 1f);
            _voices[i].volume = voice.Remaining <= 0f ? 0f : _state.VoiceGains[i] * EffectsSourceGain * _config.EffectsGain *
                _worldPresenter.Gain(_state.WorldMix, voice.Catalogue, voice.Position, _config, true);
            float zone = _rosterPresenter.ZoneCutoff(_roster, _config, _worldPresenter.Room(_state.WorldMix.Graph, _state.ListenerPosition));
            _filters[i].cutoffFrequency = voice.Catalogue.Protected ? 22000f : Mathf.Min(zone, _worldPresenter.Cutoff(_state.WorldMix, false, _config));
        }
        public void ResetRun(bool preserveMusicContact = false, bool preserveZones = false)
        {
            if (_state == null) return;
            _rosterPresenter.Reset(_roster, preserveMusicContact, preserveZones);
            StopSources();
            var prior = _state.WorldMix;
            _state.WorldMix = new AudioWorldMixDriverState { FalsePositiveDue = preserveMusicContact ? prior.FalsePositiveDue : -1f,
                Time = preserveMusicContact ? prior.Time : 0f };
            _presenter.Reset(_state, _config.VoiceCount, preserveMusicContact ? _state.CosmeticRandom : new System.Random(76103));
            _music = new AudioChaseMusicDriverState { ImpactPitch = _config.ImpactMinimumPitch,
                HasContact = preserveMusicContact && _music.HasContact,
                TensionGain = preserveMusicContact ? _music.TensionGain : 0f };
            if (_state.OwnerEnabled && isActiveAndEnabled) StartLayers();
        }
        private void Update()
        {
            if (_state == null || _state.Paused || !_state.OwnerEnabled) return;
            _presenter.Tick(_state, Time.unscaledDeltaTime, _config.AttackSeconds, _config.ReleaseSeconds, _config.ChaseHoldSeconds);
            _musicPresenter.Tick(_music, _state.Threats.Values, _state.Alive, Time.unscaledDeltaTime,
                AudioSettings.dspTime, _config.RunIntro != null ? (double)_config.RunIntro.samples / _config.RunIntro.frequency : 0, _config, _state.CosmeticRandom);
            float music = _master * _state.RuntimeMusic * _config.MusicGain * (1f - _state.Duck);
            _layers[0].pitch = _layers[1].pitch = _music.ImpactPitch;
            ApplyRunSequence(music);
            if (_worldPresenter.FalsePositive(_state.WorldMix, _config, _state.InRun, _state.Alive, _state.CosmeticRandom, out var falsePositive))
                Play(falsePositive.Cue, falsePositive.Position, falsePositive.Gain, falsePositive.Emitter, true);
            for (int i = 0; i < _state.Voices.Length; i++)
                if (_state.Voices[i].Remaining <= 0f || _state.WorldMix.InChase && _state.Voices[i].PresentationOnly) StopVoice(i);
            FlushTells();
            ApplyGains();
        }
        private AudioSource CreateSource(string label, bool loop, AudioClip clip, AudioMixerGroup group)
        {
            var child = new GameObject(label); child.transform.SetParent(_root.transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop;
            source.clip = clip; source.volume = 0f; source.dopplerLevel = 0f; source.outputAudioMixerGroup = group;
            return source;
        }
        private void StartLayers()
        {
            if (_state.MusicStarted) return;
            double start = AudioSettings.dspTime + 0.15;
            foreach (AudioSource source in _layers)
                if (source.clip != null) { source.pitch = 1f; source.timeSamples = 0; source.PlayScheduled(start); }
            _state.MusicStarted = true;
        }
        private void ApplyRunSequence(float music)
        {
            if (_runSources == null) return;
            if (_music.StopRun || _music.StartRun || _music.EndRun) StopRunSources();
            foreach (AudioSource source in _runSources) source.volume = _state.Alive ? music * _config.RunGain : 0f;
            if (_music.StartRun)
            {
                if (_runSources[0].clip != null) _runSources[0].PlayScheduled(_music.IntroStart);
                if (_runSources[1].clip != null) _runSources[1].PlayScheduled(_music.LoopStart);
            }
            else if (_music.EndRun && _runSources[2].clip != null) _runSources[2].PlayScheduled(_music.EndingStart);
        }
        private void StopRunSources()
        {
            if (_runSources != null) foreach (AudioSource source in _runSources)
                if (source != null) { source.Stop(); if (source.clip != null) source.timeSamples = 0; source.volume = 0f; }
        }
        private void StopSources()
        {
            StopRunSources();

            if (_voices != null) foreach (AudioSource source in _voices) if (source != null) { source.Stop(); source.clip = null; source.loop = false; }
            if (_layers != null) foreach (AudioSource source in _layers) if (source != null) { source.Stop(); source.volume = 0f; }
        }
        public void Teardown()
        {
            StopSources();
            if (_root != null) { if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root); }
            if (_ownsConfig && _config != null) { if (Application.isPlaying) Destroy(_config); else DestroyImmediate(_config); }
            _ownsConfig = false; _missingHeartbeatWarned = false;
            _rosterPresenter.Reset(_roster, false);
            _root = null; _worldPresenter = null; _voices = null; _filters = null; _layers = null; _state = null; _config = null; _presenter = null;
            _banks.Clear(); _durations.Clear();
            _runSources = null; _music = null; _musicPresenter = null;
        }
        private void OnDisable() { if (_state != null) { StopSources(); _state.MusicStarted = false; _music = new AudioChaseMusicDriverState { ImpactPitch = _config.ImpactMinimumPitch }; } }
        private void OnEnable() { if (_state != null && _state.OwnerEnabled) StartLayers(); }
        private void OnDestroy() => Teardown();
    }
}
