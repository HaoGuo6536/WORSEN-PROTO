// ============================================================================
// PostFXDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies primitive feedback outputs to an owned Universal Render Pipeline volume.
//   The Driver creates an isolated runtime profile so effects never modify shared designer assets.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Own and apply distortion, vignette, desaturation, grain and optional blur.
//   - Apply terminal tint/exposure only to the owned runtime volume, using unscaled time.
//   - Destroy the runtime profile and its components on teardown.
//   - Retain all rendering package types behind this boundary.
//
// DEPENDENCIES:
//   - Unity rendering core and Universal Render Pipeline volume APIs.
//
// USAGE NOTES:
//   - Scene-owned by PostFXManager; no global render settings are written.
//   - Its dedicated global volume affects cameras whose volume mask includes its layer.
//   - The scene coordinator must enable post-processing on the output camera.
//   - ConfigureForSetup creates a disabled volume; Initialize begins runtime effects.
//
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Worsen.Presentation.PostFX
{
    public sealed class PostFXDriver : MonoBehaviour
    {
        [SerializeField] private Volume _volume;
        private PostFXDriverConfig _config;
        private PostFXDriverState _state;
        private PostFXPresenter _presenter;
        private VolumeProfile _profile;
        private ChromaticAberration _chromatic;
        private LensDistortion _distortion;
        private Vignette _vignette;
        private ColorAdjustments _color;
        private FilmGrain _grain;
        private DepthOfField _blur;
        private bool _runtimeVolume;

        public bool IsReady => _state != null && _volume != null && _profile != null;

        public void ConfigureForSetup()
        {
            if (_volume == null) _volume = GetComponent<Volume>();
            if (_volume == null) _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.enabled = false;
        }

        public void Initialize(PostFXDriverConfig config)
        {
            Teardown();
            if (config == null)
            {
                Debug.LogWarning("PostFX config is missing. Run Worsen/PostFX/Create Config.", this);
                return;
            }
            _config = config;
            if (_volume == null)
            {
                Debug.LogWarning("PostFX volume wiring was missing; rebuilding its owned volume.", this);
                _volume = gameObject.AddComponent<Volume>();
                _runtimeVolume = true;
            }
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "PostFX Runtime Profile";
            _chromatic = _profile.Add<ChromaticAberration>(false);
            _distortion = _profile.Add<LensDistortion>(false);
            _vignette = _profile.Add<Vignette>(false);
            _color = _profile.Add<ColorAdjustments>(false);
            _grain = _profile.Add<FilmGrain>(false);
            _blur = _profile.Add<DepthOfField>(false);
            _blur.mode.Override(DepthOfFieldMode.Gaussian);
            _blur.gaussianStart.Override(0f);
            _blur.gaussianEnd.Override(1f);
            _blur.highQualitySampling.Override(true);
            _volume.profile = _profile;
            _volume.isGlobal = true;
            _volume.priority = config.VolumePriority;
            _volume.weight = 1f;
            _state = new PostFXDriverState();
            _presenter = new PostFXPresenter();
            _presenter.Tick(_state, _config, 0f);
            Apply();
            _volume.enabled = isActiveAndEnabled;
        }

        public void SetProximity(float closeness)
        {
            if (_state != null) _presenter.SetProximity(_state, closeness);
        }

        public void SetLookBack(bool held)
        {
            if (_state != null) _presenter.SetLookBack(_state, _config, held);
        }

        public void SetInjury(float currentHealth, float maxHealth)
        {
            if (_state != null) _presenter.SetInjury(_state, currentHealth, maxHealth);
        }

        public void PlayReacquireBlur()
        {
            if (_state != null) _presenter.PlayReacquireBlur(_state, _config);
        }

        public void PlayIntrusion(float seconds)
        {
            if (_state != null) _presenter.PlayIntrusion(_state, seconds);
        }

        public void PlayConsumed(float seconds)
        {
            if (_state != null) _presenter.PlayConsumed(_state, seconds);
        }

        public void ResetEffects()
        {
            if (_state == null) return;
            _presenter.Reset(_state);
            Apply();
        }

        public void Teardown()
        {
            if (_volume != null)
            {
                _volume.enabled = false;
                _volume.profile = null;
            }
            if (_profile != null)
            {
                foreach (var component in _profile.components) Destroy(component);
                Destroy(_profile);
            }
            if (_runtimeVolume && _volume != null) Destroy(_volume);
            if (_runtimeVolume) _volume = null;
            _runtimeVolume = false;
            _profile = null;
            _chromatic = null; _distortion = null; _vignette = null;
            _color = null; _grain = null; _blur = null;
            _state = null; _presenter = null; _config = null;
        }

        private void LateUpdate()
        {
            if (_state == null) return;
            _presenter.Tick(_state, _config, _state.Consumed ? Time.unscaledDeltaTime : Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            _chromatic.intensity.Override(_state.Chromatic);
            _distortion.intensity.Override(_state.Distortion);
            _vignette.intensity.Override(_state.Vignette);
            _color.saturation.Override(_state.Saturation);
            _color.colorFilter.Override(_state.SceneTint);
            _color.postExposure.Override(_state.Exposure);
            _color.colorFilter.overrideState = _state.Consumed;
            _color.postExposure.overrideState = _state.Consumed;
            _grain.intensity.Override(_state.Grain);
            _blur.active = _state.Blur > 0f;
            _blur.gaussianMaxRadius.Override(_state.BlurRadius);
        }

        private void OnEnable()
        {
            if (_state != null && _volume != null) _volume.enabled = true;
        }

        private void OnDisable()
        {
            ResetEffects();
            if (_volume != null) _volume.enabled = false;
        }
    }
}
