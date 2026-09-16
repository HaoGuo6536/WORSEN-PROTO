// ============================================================================
// HorrorAtmosphereDriver.cs
// ============================================================================
//
// PURPOSE:
//   Owns the camera flashlight and the dark-room rendering environment during gameplay.
//   It restores the previous render, camera, daylight and Volume state when ownership ends.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Create a shadowed camera spotlight and dim close-range fill.
//   - Apply imported dither fog to a private profile and restore global lighting symmetrically.
//
// DEPENDENCIES:
//   - Unity lighting/rendering; FronkonGames.Weird.DitherFog wrapped only here.
//   - Own Horror config and restoration state; no gameplay systems.
//
// USAGE NOTES:
//   Scene-owned. This sub-driver exclusively owns RenderSettings, supplied daylight enable flags,
//   and the supplied camera's clear mode/background while ownership is active.
//   The private Volume profile/components and light rig are destroyed; shared assets are never edited.
//
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;
using FronkonGames.Weird.DitherFog;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorAtmosphereDriver : MonoBehaviour
    {
        private HorrorDriverConfig _config;
        private HorrorAtmosphereDriverState _state;
        private UnityEngine.Camera _camera;
        private Volume _volume;
        private Light[] _daylights;
        private DitherFogVolume _fog;
        public bool IsReady => _state != null && _state.Flashlight != null && _fog != null;

        public void Initialize(HorrorDriverConfig config, UnityEngine.Camera outputCamera, Volume fogVolume, Light[] daylights)
        {
            Teardown();
            _config = config;
            _camera = outputCamera;
            _volume = fogVolume;
            _daylights = daylights;
            _state = new HorrorAtmosphereDriverState();
            _state.LightRoot = new GameObject("Flashlight rig");
            _state.LightRoot.transform.SetParent(_camera.transform, false);
            _state.LightRoot.transform.localPosition = config.FlashlightLocalOffset;
            _state.Flashlight = _state.LightRoot.AddComponent<Light>();
            _state.Flashlight.type = LightType.Spot;
            _state.Flashlight.spotAngle = config.FlashlightSpotAngle;
            _state.Flashlight.innerSpotAngle = config.FlashlightInnerSpotAngle;
            _state.Flashlight.color = config.FlashlightColor;
            _state.Flashlight.shadows = LightShadows.Soft;
            _state.Flashlight.shadowBias = config.FlashlightShadowBias;
            _state.Flashlight.shadowNormalBias = config.FlashlightShadowNormalBias;
            _state.Flashlight.enabled = false;
            var fillObject = new GameObject("Dim near-field visibility");
            fillObject.transform.SetParent(_state.LightRoot.transform, false);
            _state.NearFill = fillObject.AddComponent<Light>();
            _state.NearFill.type = LightType.Point;
            _state.NearFill.range = config.NearFillRange;
            _state.NearFill.intensity = config.NearFillIntensity;
            _state.NearFill.color = config.NearFillColor;
            _state.NearFill.shadows = LightShadows.None;
            _state.NearFill.enabled = false;
            PrepareFog();
        }

        public void SetOwnershipEnabled(bool enabled)
        {
            if (_state == null) return;
            if (enabled) CaptureAndDarken();
            else Restore();
        }

        public void Apply(float fogStart, float fogEnd, float range, float intensity, bool flashlightEnabled)
        {
            if (_state == null) return;
            _state.Flashlight.range = range;
            _state.Flashlight.intensity = intensity;
            _state.Flashlight.enabled = _state.AtmosphereCaptured && flashlightEnabled;
            _state.NearFill.enabled = _state.AtmosphereCaptured;
            if (_fog != null)
            {
                _fog.fogCurveStart.Override(fogStart);
                _fog.fogCurveEnd.Override(fogEnd);
            }
        }

        private void PrepareFog()
        {
            if (_volume == null)
            {
                Debug.LogWarning("Horror dither fog Volume is missing; its renderer feature and Volume must be wired by setup.", this);
                return;
            }
            _state.PreviousFogVolumeEnabled = _volume.enabled;
            _state.PreviousFogProfile = _volume.HasInstantiatedProfile() ? _volume.profile : null;
            VolumeProfile source = _state.PreviousFogProfile != null ? _state.PreviousFogProfile : _volume.sharedProfile;
            _state.RuntimeFogProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _state.RuntimeFogProfile.name = "Owned horror dither fog";
            if (source != null)
                foreach (VolumeComponent component in source.components)
                    if (component != null) _state.RuntimeFogProfile.components.Add(Instantiate(component));
            if (!_state.RuntimeFogProfile.TryGet(out _fog))
                _fog = _state.RuntimeFogProfile.Add<DitherFogVolume>(true);
            _fog.active = true;
            _fog.intensity.Override(1f);
            _fog.fogOpacity.Override(1f);
            _fog.fogColorMode.Override(FogColorModes.Solid);
            _fog.fogColor.Override(_config.FogColor);
            _fog.curvedFog.Override(false);
            _fog.fogStart.Override(0.45f);
        }

        private void CaptureAndDarken()
        {
            if (_state.AtmosphereCaptured) return;
            _state.PreviousAmbientMode = RenderSettings.ambientMode;
            _state.PreviousAmbientLight = RenderSettings.ambientLight;
            _state.PreviousAmbientIntensity = RenderSettings.ambientIntensity;
            _state.PreviousReflectionIntensity = RenderSettings.reflectionIntensity;
            _state.PreviousSkybox = RenderSettings.skybox;
            _state.PreviousSun = RenderSettings.sun;
            _state.PreviousBuiltInFog = RenderSettings.fog;
            _state.PreviousClearFlags = _camera.clearFlags;
            _state.PreviousBackgroundColor = _camera.backgroundColor;
            _state.Daylights.Clear();
            _state.DaylightEnabled.Clear();
            if (_daylights != null) foreach (Light daylight in _daylights) CaptureDaylight(daylight);
            CaptureDaylight(_state.PreviousSun);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _config.AmbientColor;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = _config.ReflectionIntensity;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.fog = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _config.BackgroundColor;
            if (_volume != null)
            {
                _volume.profile = _state.RuntimeFogProfile;
                _volume.enabled = true;
            }
            _state.AtmosphereCaptured = true;
        }

        private void CaptureDaylight(Light daylight)
        {
            if (daylight == null || _state.Daylights.Contains(daylight)) return;
            _state.Daylights.Add(daylight);
            _state.DaylightEnabled.Add(daylight.enabled);
            daylight.enabled = false;
        }

        private void Restore()
        {
            if (_state == null || !_state.AtmosphereCaptured) return;
            RenderSettings.ambientMode = _state.PreviousAmbientMode;
            RenderSettings.ambientLight = _state.PreviousAmbientLight;
            RenderSettings.ambientIntensity = _state.PreviousAmbientIntensity;
            RenderSettings.reflectionIntensity = _state.PreviousReflectionIntensity;
            RenderSettings.skybox = _state.PreviousSkybox;
            RenderSettings.sun = _state.PreviousSun;
            RenderSettings.fog = _state.PreviousBuiltInFog;
            for (int i = 0; i < _state.Daylights.Count; i++)
                if (_state.Daylights[i] != null) _state.Daylights[i].enabled = _state.DaylightEnabled[i];
            if (_camera != null)
            {
                _camera.clearFlags = _state.PreviousClearFlags;
                _camera.backgroundColor = _state.PreviousBackgroundColor;
            }
            if (_volume != null)
            {
                _volume.profile = _state.PreviousFogProfile;
                _volume.enabled = _state.PreviousFogVolumeEnabled;
            }
            if (_state.Flashlight != null) _state.Flashlight.enabled = false;
            if (_state.NearFill != null) _state.NearFill.enabled = false;
            _state.AtmosphereCaptured = false;
        }

        public void Teardown()
        {
            if (_state == null) return;
            Restore();
            DestroyOwned(_state.LightRoot);
            if (_state.RuntimeFogProfile != null)
            {
                foreach (VolumeComponent component in _state.RuntimeFogProfile.components) DestroyOwned(component);
                DestroyOwned(_state.RuntimeFogProfile);
            }
            _state = null;
            _config = null;
            _camera = null;
            _volume = null;
            _daylights = null;
            _fog = null;
        }

        private void OnDestroy() => Teardown();
        private static void DestroyOwned(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}

