// ============================================================================
// AudioDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies pure mixer decisions to Unity Audio sources on the persistent service.
//   Two cue voices allow interruption fades while movement and proximity layers
//   stay independent, so a footstep cannot cut off an important chase cue.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Audio.
//   AudioManager alone commands this engine boundary.
//
// KEY RESPONSIBILITIES:
//   - Own, play and tear down two cue sources, footsteps and two loop sources.
//   - Resolve the mirrored config and report unavailable clips before playback.
//   - Preserve fractional health facts and reset voices across run changes and owner disable.
//
// DEPENDENCIES:
//   - Core CueId and MovementState; Unity Audio only, with no gameplay queries.
//
// USAGE NOTES:
//   - Persistent tier; all sources are on owned child objects, never scene targets.
//   - Own DriverConfig: AudioDriverConfig; no global audio settings are changed.
//   - Presentation Update uses unscaled time; Session remains the gameplay tick owner.
//   - Teardown destroys all created sources; disable stops playback immediately.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

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
        private AudioSource[] _cueSources;
        private AudioSource _footsteps;
        private AudioSource _breath;
        private AudioSource _hunter;
        private bool _ownerEnabled;

        public float BreathGain => _state != null ? _state.BreathGain : 0f;
        public float HunterGain => _state != null ? _state.HunterGain : 0f;
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
            _presenter = new AudioMixPresenter();
            _sourceRoot = new GameObject("Owned Audio Sources");
            _sourceRoot.transform.SetParent(transform, false);
            _cueSources = new[] { CreateSource(false), CreateSource(false) };
            _footsteps = CreateSource(false);
            _breath = CreateSource(true);
            _hunter = CreateSource(true);
            _breath.clip = _config.BreathLoop;
            _hunter.clip = _config.HunterLoop;
            if (_breath.clip == null || _hunter.clip == null)
                Debug.LogWarning("Audio proximity layers need BreathLoop and HunterLoop clips. Run Worsen/Audio/Create Config and Prototype Samples.", this);
            ResetRun();
        }

        public void SetOwnerEnabled(bool ownerEnabled)
        {
            _ownerEnabled = ownerEnabled;
            if (_state == null) return;
            if (ownerEnabled && isActiveAndEnabled) StartLoops();
            else ResetRun();
        }

        public bool PlayCue(CueId cue)
        {
            if (_state == null || !_ownerEnabled || !isActiveAndEnabled) return false;
            if (!TryFindCue(cue, out AudioCueDefinition definition)) return false;
            if (cue == CueId.Footstep)
            {
                _footsteps.PlayOneShot(definition.Clip, _presenter.ClampGain(definition.Gain) * _presenter.ClampGain(_config.MasterGain));
                return true;
            }
            if (!_presenter.TryCue(_state, (int)cue, definition.Priority, definition.Clip.length, definition.Gain, definition.FadeSeconds)) return false;
            int incoming = 1 - _state.VoiceIndex;
            _cueSources[incoming].Stop();
            _cueSources[incoming].clip = definition.Clip;
            _cueSources[incoming].volume = _state.CueGain * _presenter.ClampGain(_config.MasterGain);
            _cueSources[incoming].Play();
            _state.VoiceIndex = incoming;
            return true;
        }

        public void SetProximity(float closeness) { if (_state != null) _presenter.SetProximity(_state, closeness); }
        public void SetMovementState(MovementState movement) { if (_state != null) _presenter.SetMovementState(_state, movement); }
        public void SetSpeedNormalized(float speed) { if (_state != null) _presenter.SetSpeedNormalized(_state, speed); }
        public void SetInjury(float currentHealth, float maxHealth) { if (_state != null) _presenter.SetInjury(_state, currentHealth, maxHealth); }

        public void ResetRun()
        {
            StopSources();
            if (_state == null) return;
            _presenter.Reset(_state);
            if (_ownerEnabled && isActiveAndEnabled) StartLoops();
        }

        public void Teardown()
        {
            StopSources();
            if (_sourceRoot != null)
            {
                if (Application.isPlaying) Destroy(_sourceRoot);
                else DestroyImmediate(_sourceRoot);
            }
            _sourceRoot = null;
            _cueSources = null;
            _footsteps = _breath = _hunter = null;
            _state = null;
            _presenter = null;
            _config = null;
            _ownerEnabled = false;
        }

        private void Update()
        {
            if (_state == null || !_ownerEnabled) return;
            bool step = _presenter.Tick(_state, _config.MixSettings, Time.unscaledDeltaTime);
            float master = _presenter.ClampGain(_config.MasterGain);
            _cueSources[_state.VoiceIndex].volume = _state.CueGain * master;
            _cueSources[1 - _state.VoiceIndex].volume = _state.OutgoingCueGain * master;
            if (_state.OutgoingCueGain <= 0f) _cueSources[1 - _state.VoiceIndex].Stop();
            _breath.volume = _state.BreathGain * master;
            _hunter.volume = _state.HunterGain * master;
            if (step) PlayCue(CueId.Footstep);
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

        private bool TryFindCue(CueId cue, out AudioCueDefinition definition)
        {
            if (_config.Cues != null)
            {
                foreach (AudioCueDefinition entry in _config.Cues)
                {
                    if (entry.Cue != cue) continue;
                    if (entry.Clip != null)
                    {
                        definition = entry;
                        return true;
                    }
                    break;
                }
            }
            definition = default;
            if (_state.MissingCueWarnings.Add((int)cue))
                Debug.LogWarning("Audio cue '" + cue + "' has no playable clip; playback was not started. Rebuild Audio config or assign a clip.", this);
            return false;
        }

        private void StartLoops()
        {
            if (_breath != null && _breath.clip != null && !_breath.isPlaying) _breath.Play();
            if (_hunter != null && _hunter.clip != null && !_hunter.isPlaying) _hunter.Play();
        }

        private void StopSources()
        {
            if (_cueSources != null)
                foreach (AudioSource source in _cueSources) if (source != null) { source.Stop(); source.volume = 0f; }
            if (_footsteps != null) { _footsteps.Stop(); _footsteps.volume = 1f; }
            if (_breath != null) { _breath.Stop(); _breath.volume = 0f; }
            if (_hunter != null) { _hunter.Stop(); _hunter.volume = 0f; }
        }

        private void OnEnable() { if (_state != null && _ownerEnabled) StartLoops(); }
        private void OnDisable()
        {
            StopSources();
            if (_state != null) _presenter.Reset(_state);
        }
        private void OnDestroy() => Teardown();
    }
}
