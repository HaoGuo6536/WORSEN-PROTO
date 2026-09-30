// ============================================================================
// AudioSoundscapeDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies pooled spatial effects, adaptive impact/danger loops and a scheduled run sequence.
//   It receives room anchors and portal evidence as facts, and never reads enemy state.
//   Every world voice uses shared acoustic attenuation without duplicate Unity rolloff.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by AudioDriver · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Apply runtime music/effects gains to all pooled, scheduled and ambience sources.
//   - Stop active enemy voices on player death and reject late enemy feedback while preserving the player's death cue.
//   - Enforce category limits, timed mix hooks, protected tells and rare false positives.
//   - Own a bounded source pool, attenuation filters and five non-diegetic bed/music layers.
//   - Schedule Run 1 into Run 2 without frame-boundary gaps; cancel pending playback on end/reset.
//   - Refresh spatial loop position/gain without restarting its clip or changing pitch.
//   - Apply the pure soundscape Presenter and clear playback on disable or reset.
//   - Supply the seeded cosmetic random source to music loss-episode decisions.
//   - Stop gameplay loops immediately on death while preserving ambience and one-shots.
//   - Release each stopped emitter's clip and loop state as well as its voice lease.
//   - Replace aggregate chase snapshots so obsolete hunters cannot retain music belief.
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
        private bool _ownsConfig;
        private GameObject _root;
        private readonly Dictionary<CueId, AudioSoundDefinition> _banks = new Dictionary<CueId, AudioSoundDefinition>();
        private readonly Dictionary<CueId, float[]> _durations = new Dictionary<CueId, float[]>();
        private float _master = 1f;
        public float EffectsSourceGain => _master * (_state != null ? _state.RuntimeEffects : 1f);
        public float BreathGain => _state != null ? _state.WorldMix.BreathGain : 0f;
        public bool Heartbeat => _state != null && _state.WorldMix.Heartbeat;
        public float HeartbeatGain => _state != null ? _state.WorldMix.HeartbeatStrength * _config.HeartbeatGain : 0f;
        public AudioClip HeartbeatClip => _config.HeartbeatClip != null ? _config.HeartbeatClip :
            _banks.TryGetValue(CueId.Land, out var bank) && bank.Clips != null && bank.Clips.Length > 0 ? bank.Clips[0] : null;
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
        {
            if (_state == null || _state.Paused || !_state.OwnerEnabled || !isActiveAndEnabled) return false;
            if (!_catalogue.Admits(cue, _state.InRun)) return false;
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
            AudioSource source = _voices[request.Voice]; source.transform.position = position;
            _state.Voices[request.Voice].Position = position;
            _state.Voices[request.Voice].PresentationOnly = presentationOnly;
            _state.VoiceGains[request.Voice] = request.Gain;
            if (request.ReuseLoop)
            {
                ApplyVoiceGain(request.Voice);
                return true;
            }
            source.Stop(); source.clip = bank.Clips[request.Clip]; source.loop = bank.Loop;
            source.spatialBlend = bank.Spatial ? 1f : 0f; source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0f, 1f, 1f, 1f));
            source.minDistance = Mathf.Max(0.1f, bank.MinimumDistance); source.maxDistance = Mathf.Max(source.minDistance + 0.1f, bank.MaximumDistance);
            source.priority = Mathf.Clamp(256 - bank.Priority * 2, 0, 256);
            source.pitch = request.Pitch;
            source.outputAudioMixerGroup = bank.Ambience ? _config.AmbienceGroup : _config.EffectsGroup;
            ApplyVoiceGain(request.Voice);
            if (request.Delay > 0f) source.PlayScheduled(AudioSettings.dspTime + request.Delay); else source.Play();
            return true;
        }
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
                if (_presenter.IsEnemyCue((CueId)_state.Voices[i].Cue) ||
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
            bool master = wired && _config.Mixer.SetFloat(_config.MasterParameter, volume.Decibels(settings.MasterVolume));
            bool music = wired && _config.Mixer.SetFloat(_config.MusicParameter, volume.Decibels(settings.MusicVolume));
            bool effects = wired && _config.Mixer.SetFloat(_config.EffectsParameter, volume.Decibels(settings.EffectsVolume));
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
            _voices[i].volume = voice.Remaining <= 0f ? 0f : _state.VoiceGains[i] * EffectsSourceGain * _config.EffectsGain *
                _worldPresenter.Gain(_state.WorldMix, voice.Catalogue, voice.Position, _config);
            _filters[i].cutoffFrequency = _worldPresenter.Cutoff(_state.WorldMix, voice.Catalogue.Protected, _config);
        }
        public void ResetRun(bool preserveMusicContact = false)
        {
            if (_state == null) return;
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
            _ownsConfig = false;
            _root = null; _worldPresenter = null; _voices = null; _filters = null; _layers = null; _state = null; _config = null; _presenter = null;
            _banks.Clear(); _durations.Clear();
            _runSources = null; _music = null; _musicPresenter = null;
        }
        private void OnDisable() { if (_state != null) { StopSources(); _state.MusicStarted = false; _music = new AudioChaseMusicDriverState { ImpactPitch = _config.ImpactMinimumPitch }; } }
        private void OnEnable() { if (_state != null && _state.OwnerEnabled) StartLayers(); }
        private void OnDestroy() => Teardown();
    }
}
