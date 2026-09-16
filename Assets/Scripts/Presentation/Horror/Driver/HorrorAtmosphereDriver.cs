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
//   - Apply unshaken world-space flashlight authority and short-lived visible light traces.
//   - Create native Lumen 2 fake spotlight/fill effects without Unity Light components.
//   - Fill the vendor cone's close-range blind zone without enlarging static wall clearance.
//   - Apply imported dither fog to a private profile and restore global lighting symmetrically.
//
// DEPENDENCIES:
//   - Lumen 2, Unity rendering/physics and FronkonGames.Weird.DitherFog wrapped here.
//   - Own Horror config and restoration state; no gameplay systems.
//
// USAGE NOTES:
//   Scene-owned. This sub-driver exclusively owns RenderSettings, supplied daylight enable flags,
//   and the supplied camera's clear mode/background while ownership is active.
//   The private Volume profile/components and light rig are destroyed; shared assets are never edited.
//   Camera depth handles visible surfaces; static obstruction clamps are conservative approximations,
//   not physical shadows. Gameplay visibility remains independently authoritative.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using UnityEngine.Rendering;
using FronkonGames.Weird.DitherFog;
using DistantLands.Lumen;

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
        private readonly HorrorLumenPresenter _lumen = new HorrorLumenPresenter();
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
            _state.LightRoot.SetActive(false);
            _state.LightRoot.transform.SetParent(_camera.transform, false);
            // Camera-coincident fallback matches depth projection; authoritative poses replace it.
            _state.Flashlight = CreateFake(config.LumenFlashlightPrefab, "Lumen 2 Flashlight", true, config.FlashlightColor);
            _state.NearFill = CreateFake(config.LumenNearFillPrefab, "Lumen 2 Near Visibility", false, config.NearFillColor);
            _state.LightRoot.SetActive(true);
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
            _state.FlashlightRange = range; _state.FlashlightBrightness = intensity;
            _state.FlashlightEnabled = flashlightEnabled;
            RefreshFlashlight();
            if (_fog != null)
            {
                _fog.fogCurveStart.Override(fogStart);
                _fog.fogCurveEnd.Override(fogEnd);
            }
        }

        public void SetFlashlightPose(FlashlightSample sample)
        {
            if (_state == null || _state.LightRoot == null) return;
            // Detach the world aim from cosmetic camera roll, displacement and impulses.
            _state.LightRoot.transform.SetParent(transform, true);
            _state.LightRoot.transform.SetPositionAndRotation(sample.Origin, Quaternion.LookRotation(sample.Direction.normalized, Vector3.up));
            SetCone(_state.Flashlight, sample.ConeDegrees);
            _state.FlashlightRange = sample.Range;
            RefreshFlashlight();
        }

        public void SetAfterimage(FlashlightSample sample, float lifetime)
        {
            if (_state == null || !sample.Enabled || lifetime <= 0f) { ClearAfterimage(); return; }
            if (_state.Afterimage == null)
            {
                _state.Afterimage = CreateFake(_config.LumenFlashlightPrefab, "Lumen 2 Dying Afterimage", true, new Color(.58f, .8f, 1f));
                _state.Afterimage.transform.SetParent(transform, true);
            }
            _state.AfterimageRemaining = Mathf.Clamp(lifetime, 0f, 4f);
            _state.Afterimage.transform.SetPositionAndRotation(sample.Origin, Quaternion.LookRotation(sample.Direction.normalized, Vector3.up));
            _state.AfterimageRange = sample.Range;
            SetCone(_state.Afterimage, sample.ConeDegrees);
            Draw(_state.Afterimage, BeamRange(_state.Afterimage.transform, sample.Range), 1.25f, _state.AtmosphereCaptured);
        }

        public void ClearAfterimage()
        {
            if (_state == null) return;
            _state.AfterimageRemaining = 0f;
            if (_state.Afterimage != null) _state.Afterimage.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_state == null || _state.Afterimage == null || !_state.Afterimage.gameObject.activeSelf) return;
            _state.AfterimageRemaining = Mathf.Max(0f, _state.AfterimageRemaining - Time.deltaTime);
            Draw(_state.Afterimage, BeamRange(_state.Afterimage.transform, _state.AfterimageRange),
                Mathf.Min(1.25f, _state.AfterimageRemaining * 1.25f), _state.AtmosphereCaptured);
            if (_state.AfterimageRemaining <= 0f) ClearAfterimage();
        }

        private LumenEffectPlayer CreateFake(GameObject prefab, string label, bool spotlight, Color color)
        {
            // The temporary inactive parent also protects against an accidentally active authored prefab.
            var guard = new GameObject("Lumen initialization");
            guard.SetActive(false); guard.transform.SetParent(_state.LightRoot.transform, false);
            GameObject root = prefab != null ? Instantiate(prefab, guard.transform, false) : new GameObject(label);
            root.SetActive(false); root.name = label; root.transform.SetParent(_state.LightRoot.transform, false);
            DestroyOwned(guard);
            root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity;
            foreach (Light legacy in root.GetComponentsInChildren<Light>(true)) { legacy.enabled = false; DestroyOwned(legacy); }
            LumenEffectPlayer player = root.GetComponent<LumenEffectPlayer>();
            if (player == null) player = root.AddComponent<LumenEffectPlayer>();
            // Isolated Edit Mode tests inspect data only; never start the vendor's global renderer.
            player.enabled = Application.isPlaying;
            var profile = player.profile != null ? Instantiate(player.profile) : ScriptableObject.CreateInstance<LumenEffectProfile>();
            if (profile.layers.Count == 0) profile.layers.Add(new LumenLightLayer { range = 2f, intensity = .2f,
                smoothness = spotlight ? 1.2f : 1.5f, isSpotlight = spotlight, fluctuation = spotlight,
                fluctuationAmount = .08f, fluctuationSpeed = .4f, fluctuationScale = 1f });
            foreach (LumenEffectLayer layer in profile.layers)
                if (layer is LumenLightLayer light)
                {
                    if (!spotlight) light.smoothness = Mathf.Clamp(_config.NearFillSmoothness, .1f, 5f);
                    if (light.mesh == null)
                    {
                        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        shape.SetActive(false);
                        light.mesh = shape.GetComponent<MeshFilter>().sharedMesh;
                        DestroyOwned(shape);
                    }
                }
            _state.LightProfiles.Add(profile); player.profile = profile;
            player.color = color; player.brightness = 0f;
            player.autoAssignSun = false; player.useLumenSunScript = false;
            player.updateFrequency = LumenEffectPlayer.UpdateFrequency.ViaScripting;
            player.initializationBehavior = LumenEffectPlayer.InitializationBehavior.Immediate;
            player.deinitializationBehavior = LumenEffectPlayer.DeinitializationBehavior.Immediate;
            SetCone(player, _config.FlashlightSpotAngle);
            return player;
        }

        private void SetCone(LumenEffectPlayer player, float cone)
        {
            Vector2 angles = _lumen.ConeAngles(cone);
            foreach (LumenEffectLayer layer in player.profile.layers)
                if (layer is LumenLightLayer light && light.isSpotlight)
                { light.minSpotlightAngle = angles.x; light.maxSpotlightAngle = angles.y; }
        }

        private void RefreshFlashlight()
        {
            Draw(_state.Flashlight, BeamRange(_state.Flashlight.transform, _state.FlashlightRange),
                _state.FlashlightBrightness, _state.AtmosphereCaptured && _state.FlashlightEnabled);
            float clearance = _config.NearFillRange;
            if (Application.isPlaying && _state.AtmosphereCaptured)
            {
                Collider[] colliders = _state.NearColliders;
                int count = Physics.OverlapSphereNonAlloc(_state.NearFill.transform.position, clearance, colliders, ~0, QueryTriggerInteraction.Ignore);
                if (count == colliders.Length)
                { colliders = Physics.OverlapSphere(_state.NearFill.transform.position, clearance, ~0, QueryTriggerInteraction.Ignore); count = colliders.Length; }
                for (int index = 0; index < count; index++)
                    if (colliders[index].attachedRigidbody == null)
                        clearance = Mathf.Min(clearance, Vector3.Distance(_state.NearFill.transform.position, colliders[index].ClosestPoint(_state.NearFill.transform.position)) + .08f);
            }
            // A shallow falloff retains nearby wall detail even at the conservative clipped radius.
            // Switching off keeps only a dim navigation floor; no cone/range gameplay facts change.
            Draw(_state.NearFill, clearance, _config.NearFillIntensity *
                (_state.FlashlightEnabled ? 1f : Mathf.Clamp01(_config.NearFillOffMultiplier)), _state.AtmosphereCaptured);
        }

        private float BeamRange(Transform source, float requested)
        {
            if (!Application.isPlaying || !_state.AtmosphereCaptured) return requested;
            float nearest = requested;
            RaycastHit[] hits = _state.BeamHits;
            int count = Physics.RaycastNonAlloc(source.position, source.forward, hits, Mathf.Max(.01f, requested), ~0, QueryTriggerInteraction.Ignore);
            // A full buffer cannot establish nearest-hit completeness; preserve correctness on overflow.
            if (count == hits.Length)
            { hits = Physics.RaycastAll(source.position, source.forward, Mathf.Max(.01f, requested), ~0, QueryTriggerInteraction.Ignore); count = hits.Length; }
            for (int index = 0; index < count; index++)
                if (hits[index].collider.attachedRigidbody == null) nearest = Mathf.Min(nearest, hits[index].distance);
            return _lumen.ObstructedRange(requested, nearest);
        }

        private void Draw(LumenEffectPlayer player, float radius, float brightness, bool visible)
        {
            float layerRange = 2f;
            foreach (LumenEffectLayer layer in player.profile.layers)
                if (layer is LumenLightLayer light) { layerRange = light.range; break; }
            player.range = _lumen.RangeMultiplier(radius, layerRange);
            player.brightness = Mathf.Max(0f, brightness);
            if (player.gameObject.activeSelf != visible) player.gameObject.SetActive(visible);
            if (visible && Application.isPlaying) player.RedoEffect(false);
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
            if (_state.Flashlight != null) _state.Flashlight.gameObject.SetActive(false);
            if (_state.NearFill != null) _state.NearFill.gameObject.SetActive(false);
            ClearAfterimage();
            _state.AtmosphereCaptured = false;
        }

        public void Teardown()
        {
            if (_state == null) return;
            Restore();
            DestroyOwned(_state.LightRoot);
            if (_state.Afterimage != null) DestroyOwned(_state.Afterimage.gameObject);
            foreach (LumenEffectProfile profile in _state.LightProfiles) DestroyOwned(profile);
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
