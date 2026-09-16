// ============================================================================
// AudioSoundscapeDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies pooled spatial effects, adaptive impact/danger loops and a scheduled run sequence.
//   It receives room anchors and portal evidence as facts, and never reads enemy state.
//   Four dedicated torch sources fade between nearest admissible anchors; quiet accents use the listener room only.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by AudioDriver · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Stop active enemy voices on player death and reject late enemy feedback while preserving the player's death cue.
//   - Own a bounded source pool, four torch voices, attenuation filters and five looping layers.
//   - Schedule Run 1 into Run 2 without frame-boundary gaps; cancel pending playback on end/reset.
//   - Refresh spatial loop position/gain without restarting its clip or changing pitch.
//   - Apply the pure soundscape Presenter and clear playback on disable or reset.
//   - Stop gameplay loops immediately on death while preserving ambience and one-shots.
//   - Release each stopped emitter's clip and loop state as well as its voice lease.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Persistent tier, owned by the persistent AudioDriver; no global audio settings change.
//   Own DriverConfig: AudioSoundscapeDriverConfig. All created children are destroyed on teardown.
//   Portal occlusion must be pushed by the owning Manager; empty configuration produces visible missing-bank warnings.
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
        private AudioSource[] _torches;
        private AudioLowPassFilter[] _torchFilters;
        private AudioWorldDriverState _world;
        private AudioWorldPresenter _worldPresenter;
        private GameObject _root;
        private readonly Dictionary<CueId, AudioSoundDefinition> _banks = new Dictionary<CueId, AudioSoundDefinition>();
        private readonly Dictionary<CueId, float[]> _durations = new Dictionary<CueId, float[]>();
        private float _master = 1f;
        private readonly RaycastHit[] _groundHits = new RaycastHit[12];
        public float ChaseGain => _music != null ? _music.StressGain : 0f;
        public float DangerGain => _music != null ? _music.DangerGain : 0f;
        public int OwnedVoiceCount => _voices != null ? _voices.Length : 0;
        public void Initialize(AudioSoundscapeDriverConfig config)
        {
            if (_state != null || config == null) return;
            _config = config; _state = new AudioSoundscapeDriverState();
            _world = new AudioWorldDriverState(); _worldPresenter = new AudioWorldPresenter(); _presenter = new AudioSoundscapePresenter();
            _presenter.Reset(_state, config.VoiceCount, new System.Random(76103));
            _root = new GameObject("Owned Soundscape Sources"); _root.transform.SetParent(transform, false);
            _voices = new AudioSource[_state.Voices.Length]; _filters = new AudioLowPassFilter[_voices.Length];
            for (int i = 0; i < _voices.Length; i++)
            { _voices[i] = CreateSource("Effect " + i, false, null, config.EffectsGroup); _filters[i] = _voices[i].gameObject.AddComponent<AudioLowPassFilter>(); _filters[i].cutoffFrequency = 22000f; }
            _layers = new[] { CreateSource("Tension", true, config.TensionStem, config.MusicGroup),
                CreateSource("Chase", true, config.ChaseStem, config.MusicGroup), CreateSource("Danger", true, config.DangerStem, config.MusicGroup),
                CreateSource("Interior", true, config.InteriorAmbience, config.AmbienceGroup), CreateSource("Exterior", true, config.ExteriorAmbience, config.AmbienceGroup) };
            foreach (AudioSoundDefinition bank in config.Sounds)
            {
                _banks[bank.Cue] = bank;
                float[] durations = new float[bank.Clips != null ? bank.Clips.Length : 0];
                for (int i = 0; i < durations.Length; i++) durations[i] = bank.Clips[i] != null ? bank.Clips[i].length : 0f;
                _durations[bank.Cue] = durations;
            }
            _torches = new AudioSource[4]; _torchFilters = new AudioLowPassFilter[4];
            for (int i = 0; i < 4; i++)
            {
                _torches[i] = CreateSource("Torch " + i, true, null, config.AmbienceGroup);
                _torches[i].spatialBlend = 1f; _torches[i].rolloffMode = AudioRolloffMode.Linear;
                _torches[i].minDistance = 1.8f; _torches[i].maxDistance = config.TorchAudibleDistance;
                _torchFilters[i] = _torches[i].gameObject.AddComponent<AudioLowPassFilter>();
            }
            _music = new AudioChaseMusicDriverState { ImpactPitch = config.ImpactMinimumPitch };
            _musicPresenter = new AudioChaseMusicPresenter();
            if (config.RunIntro != null || config.RunLoop != null || config.RunEnd != null)
                _runSources = new[] { CreateSource("Run Intro", false, config.RunIntro, config.MusicGroup),
                    CreateSource("Run Loop", true, config.RunLoop, config.MusicGroup), CreateSource("Run End", false, config.RunEnd, config.MusicGroup) };
        }
        public void SetOwnerEnabled(bool value)
        {
            if (_state == null) return;
            _state.OwnerEnabled = value;
            if (value && isActiveAndEnabled) StartLayers(); else ResetRun();
        }
        public bool Play(CueId cue, Vector3 position, float gain, int emitter)
        {
            if (_state == null || !_state.OwnerEnabled || !isActiveAndEnabled) return false;
            if (cue == CueId.Footstep) cue = ResolveFootstep(position);
            if (!_banks.TryGetValue(cue, out AudioSoundDefinition bank) || !_durations.TryGetValue(cue, out float[] durations) || durations.Length == 0)
            {
                if (_state.MissingWarnings.Add((int)cue)) Debug.LogWarning("Soundscape cue '" + cue + "' is not wired to a playable bank.", this);
                return false;
            }
            if (!_state.Alive && (_presenter.IsEnemyCue(cue) || bank.Loop && !bank.Ambience)) return false;
            if (!_presenter.TryPlay(_state, bank, emitter, durations, gain, out AudioPlaybackSample request)) return false;
            AudioSource source = _voices[request.Voice]; source.transform.position = position;
            if (request.ReuseLoop)
            {
                source.volume = request.Gain * _master * (bank.Ambience ? _config.AmbienceGain : _config.EffectsGain);
                return true;
            }
            source.Stop(); source.clip = bank.Clips[request.Clip]; source.loop = bank.Loop;
            source.spatialBlend = bank.Spatial ? 1f : 0f; source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.1f, bank.MinimumDistance); source.maxDistance = Mathf.Max(source.minDistance + 0.1f, bank.MaximumDistance);
            source.priority = Mathf.Clamp(256 - bank.Priority * 2, 0, 256);
            source.pitch = request.Pitch;
            source.outputAudioMixerGroup = bank.Ambience ? _config.AmbienceGroup : _config.EffectsGroup;
            source.volume = request.Gain * _master * (bank.Ambience ? _config.AmbienceGain : _config.EffectsGain);
            _filters[request.Voice].cutoffFrequency = 22000f;
            source.Play(); return true;
        }
        public bool PlayLocal(CueId cue) => _state != null && Play(cue, _state.ListenerPosition, 1f, 0);
        public CueId ResolveFootstep(Vector3 position)
        {
            if (_config == null) return CueId.Footstep;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * .25f, Vector3.down, _groundHits, 2.5f, _config.FootstepGroundMask, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _groundHits[i].collider;
                if (collider == null || collider.attachedRigidbody != null || _groundHits[i].normal.y < .35f) continue;
                if (nearest < 0 || _groundHits[i].distance < _groundHits[nearest].distance) nearest = i;
            }
            if (nearest < 0) return CueId.Footstep;
            Collider surface = _groundHits[nearest].collider;
            Renderer renderer = surface.GetComponent<Renderer>();
            string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
            return _presenter.FootstepSurface(surface.name + " " + material);
        }
        public void SetRooms(IReadOnlyList<GeneratedRoomSample> rooms) { if (_world != null) _worldPresenter.SetRooms(_world, rooms); }
        public void SetTorchPositions(int roomId, Vector3[] positions) { if (_world != null) _worldPresenter.SetTorches(_world, roomId, positions); }
        public void ObserveDestruction(RoomDestructionSample sample) { if (_world != null && sample.Phase == RoomPhase.Closed) _world.ConsumedRooms.Add(sample.RoomId); }
        public void SetThreat(int id, bool chasing, float closeness)
        { if (_state != null) _state.Threats[id] = new AudioThreatSample { Chasing = chasing, Closeness = closeness }; }
        public void RemoveThreat(int id) { if (_state != null) _state.Threats.Remove(id); }
        public void SetAmbience(float openness, float collapse)
        { if (_state != null) { _state.Openness = openness; _state.Collapse = collapse; } }
        public void SetListenerPosition(Vector3 position) { if (_state != null) _state.ListenerPosition = position; }
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
            if (_state == null) return;
            for (int i = 0; i < _state.Voices.Length; i++)
                if (_state.Voices[i].Emitter == emitter && _state.Voices[i].Remaining > 0f)
                    _filters[i].cutoffFrequency = Mathf.Lerp(22000f, 900f, Mathf.Clamp01(occlusion));
        }
        public void SetMasterGain(float gain) { _master = Mathf.Clamp01(gain); }
        public void ResetRun()
        {
            if (_state == null) return;
            StopSources(); _world = new AudioWorldDriverState(); _presenter.Reset(_state, _config.VoiceCount, new System.Random(76103));
            _music = new AudioChaseMusicDriverState { ImpactPitch = _config.ImpactMinimumPitch };
            if (_state.OwnerEnabled && isActiveAndEnabled) StartLayers();
        }
        private void Update()
        {
            if (_state == null || !_state.OwnerEnabled) return;
            _presenter.Tick(_state, Time.unscaledDeltaTime, _config.AttackSeconds, _config.ReleaseSeconds, _config.ChaseHoldSeconds);
            _musicPresenter.Tick(_music, _state.Threats.Values, _state.Alive, Time.unscaledDeltaTime,
                AudioSettings.dspTime, _config.RunIntro != null ? (double)_config.RunIntro.samples / _config.RunIntro.frequency : 0, _config);
            float music = _master * _config.MusicGain * (1f - _state.Duck);
            _layers[0].volume = _music.TensionGain * music; _layers[1].volume = _music.StressGain * music; _layers[2].volume = _music.DangerGain * music;
            _layers[0].pitch = _layers[1].pitch = _music.ImpactPitch;
            ApplyRunSequence(music);
            _layers[3].volume = _state.InteriorGain * _master * _config.AmbienceGain;
            _layers[4].volume = _state.ExteriorGain * _master * _config.AmbienceGain;
            UpdateWorld(Time.unscaledDeltaTime);
        }
        private void UpdateWorld(float dt)
        {
            _worldPresenter.SelectTorches(_world, _state.ListenerPosition, _config.TorchAudibleDistance);
            _banks.TryGetValue(CueId.TorchLoop, out AudioSoundDefinition bank);
            _worldPresenter.TickTorches(_world, dt, bank.Clips != null ? bank.Clips.Length : 0);
            for (int i = 0; i < 4; i++)
            {
                AudioTorchSlot slot = _world.Slots[i]; AudioSource source = _torches[i];
                if (slot.Changed)
                {
                    source.Stop(); source.clip = bank.Clips[slot.Clip]; source.pitch = slot.Pitch;
                    if (source.clip != null) source.Play();
                }
                if (slot.Id == 0) source.Stop();
                source.transform.position = slot.Position;
                source.volume = slot.Gain * bank.Gain * _master * _config.AmbienceGain * (slot.AcrossPortal ? .5f : 1f);
                _torchFilters[i].cutoffFrequency = slot.AcrossPortal ? 1800f : 22000f;
            }
            if (_worldPresenter.TickAccent(_world, _state.ListenerPosition, _state.Alive, dt, _config.AccentMinimumSeconds, _config.AccentMaximumSeconds, out AudioFeedbackCommand accent))
                Play(accent.Cue, accent.Position, accent.Gain, accent.Emitter);
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
            if (_torches != null) foreach (AudioSource source in _torches) if (source != null) { source.Stop(); source.volume = 0f; }
            if (_voices != null) foreach (AudioSource source in _voices) if (source != null) source.Stop();
            if (_layers != null) foreach (AudioSource source in _layers) if (source != null) { source.Stop(); source.volume = 0f; }
        }
        public void Teardown()
        {
            StopSources();
            if (_root != null) { if (Application.isPlaying) Destroy(_root); else DestroyImmediate(_root); }
            _root = null; _torches = null; _torchFilters = null; _world = null; _worldPresenter = null; _voices = null; _filters = null; _layers = null; _state = null; _config = null; _presenter = null;
            _banks.Clear(); _durations.Clear();
            _runSources = null; _music = null; _musicPresenter = null;
        }
        private void OnDisable() { if (_state != null) { StopSources(); _state.MusicStarted = false; _music = new AudioChaseMusicDriverState { ImpactPitch = _config.ImpactMinimumPitch }; } }
        private void OnEnable() { if (_state != null && _state.OwnerEnabled) StartLayers(); }
        private void OnDestroy() => Teardown();
    }
}
