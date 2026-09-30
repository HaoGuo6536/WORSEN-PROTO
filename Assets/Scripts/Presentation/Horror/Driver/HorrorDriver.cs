// ============================================================================
// HorrorDriver.cs
// ============================================================================
//
// PURPOSE:
//   Sequences a private atmosphere rig, quiet ambience and enemy warning objects from pushed commands.
//   Pure calculations produce the lighting and attack outputs; sub-drivers own the engine mutations.
//   A caller-driven gameplay clock keeps intrusion timing continuous across generated floors.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Own finite-lived Weaver visuals; leave all hunter sound admission to Audio.
//   - Own the micro-event sub-driver and republish decisions and lighting hook changes.
//   - Render external light authority independently of camera shake and bank.
//   - Own atmosphere and ambience sub-drivers, attack cues, shared material and transient state.
//   - Forward fog hooks and gate intrusions; preserve the run clock and budget across floor resets.
//   - Delegate validated tick deltas to the Presenter without sampling engine time.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and EntityId; its own Horror presentation stack.
//
// USAGE NOTES:
//   Scene-owned by HorrorManager. Configure camera, fog volume and daylights before Initialize.
//   The atmosphere sub-driver exclusively owns global render settings while enabled.
//   No gameplay polling, global singleton reads or vendor API leaks outside this Driver stack.
//   RunElapsedSeconds is NaN until initialized; disabled owners cannot advance the clock.
//
// ============================================================================

using UnityEngine;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Horror
{
    [DisallowMultipleComponent]
    public sealed class HorrorDriver : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera _outputCamera;
        [SerializeField] private Volume _fogVolume;
        [SerializeField] private Light[] _daylights = new Light[0];
        private HorrorDriverConfig _config;
        private HorrorDriverState _state;
        private HorrorPresenter _presenter;
        private System.Random _startleRandom;
        private HorrorAtmosphereDriver _atmosphere;
        private HorrorAmbienceDriver _ambience;
        private HorrorMicroEventDriver _micro;
        private HorrorWebDriver _web;
        public event Action<int, int, Vector3, float> MicroEventSelected;
        public event Action<float, bool> LightingHooksChanged;
        public float TorchCountMultiplier => _state?.TorchCountMultiplier ?? 1f;
        public bool Wick => _state != null && _state.Wick;
        public bool IsReady => _state != null && _atmosphere != null && _atmosphere.IsReady
            && _state.CueMaterial != null && _config.AttackGrowl != null;
        public bool FlashlightEnabled => _state != null && _state.FlashlightEnabled;
        public float FogCurveStart => _state != null ? _state.FogCurveStart : 0f;
        public float FogCurveEnd => _state != null ? _state.FogCurveEnd : 0f;
        public float FlashlightRange => _state != null ? _state.FlashlightRange : 0f;
        public double RunElapsedSeconds => _state != null ? _state.RunElapsedSeconds : double.NaN;

        public void Initialize(HorrorDriverConfig config)
        {
            Teardown();
            _config = config != null ? config :
                Resources.Load<HorrorDriverConfig>("ScriptableObjects/Presentation/Horror/HorrorDriverConfig");
            if (_config == null || _outputCamera == null)
            {
                Debug.LogWarning("Horror needs its config and explicitly wired output camera.", this);
                return;
            }
            _state = new HorrorDriverState();
            var webObject = new GameObject("Owned Weaver visuals");
            webObject.transform.SetParent(transform, false);
            _web = webObject.AddComponent<HorrorWebDriver>(); _web.Initialize(_config);
            var microObject = new GameObject("Owned horror micro-events");
            microObject.transform.SetParent(transform, false);
            _micro = microObject.AddComponent<HorrorMicroEventDriver>();
            _presenter = new HorrorPresenter();
            _startleRandom = new System.Random();
            var atmosphereObject = new GameObject("Owned horror atmosphere");
            atmosphereObject.transform.SetParent(transform, false);
            _atmosphere = atmosphereObject.AddComponent<HorrorAtmosphereDriver>();
            _atmosphere.Initialize(_config, _outputCamera, _fogVolume, _daylights);
            var ambienceObject = new GameObject("Owned horror ambience");
            ambienceObject.transform.SetParent(transform, false);
            _ambience = ambienceObject.AddComponent<HorrorAmbienceDriver>();
            _ambience.Initialize(_config);
            _state.CueRoot = new GameObject("Owned enemy attack cues");
            _state.CueRoot.transform.SetParent(transform, false);
            _state.CueMaterial = _config.AttackMaterial;
            if (_state.CueMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _state.CueMaterial = new Material(shader) { name = "Runtime attack cue fallback" };
                    _state.OwnsCueMaterial = true;
                }
                Debug.LogWarning("Horror attack material was not assigned; assign one in setup for reliable build inclusion.", this);
            }
            if (_config.AttackGrowl == null)
                Debug.LogWarning("Horror attack growl is missing. Assign the imported monster growl in its config.", this);
            ApplyAtmosphere();
        }

        public void SetOwnerEnabled(bool value)
        {
            if (_state == null) return;
            _state.OwnerEnabled = value;
            if (_web != null) _web.enabled = value && isActiveAndEnabled;
            if (_micro != null) _micro.enabled = value && isActiveAndEnabled;
            if (value && isActiveAndEnabled) OnEnable();
            _atmosphere.SetOwnershipEnabled(value && isActiveAndEnabled);
            if (_ambience != null) _ambience.SetOwnerEnabled(value && isActiveAndEnabled);
            ApplyAtmosphere();
            if (!value)
                foreach (HorrorAttackCueDriver cue in _state.Cues.Values) if (cue != null) cue.Stop();
        }

        public void SetFlashlight(FlashlightSample sample)
        {
            if (_state == null || !_presenter.SetFlashlight(_state, sample)) return;
            ApplyAtmosphere();
        }

        public void SetAfterimage(FlashlightSample sample, float lifetime)
        { if (_atmosphere != null) _atmosphere.SetAfterimage(sample, lifetime); }

        public void ToggleFlashlight()
        {
            if (_state == null) return;
            _presenter.ToggleFlashlight(_state);
            ApplyAtmosphere();
        }

        public void SetEffects(float fogMultiplier, float flashlightMultiplier)
        {
            if (_state == null) return;
            _presenter.SetEffects(_state, fogMultiplier, flashlightMultiplier);
            ApplyAtmosphere();
        }

        public void SetAttack(HunterAttackSample sample)
        {
            if (_state == null || !_state.OwnerEnabled || !isActiveAndEnabled || !sample.Hunter.IsValid) return;
            if (!_state.Attacks.TryGetValue(sample.Hunter, out HorrorAttackDriverState attack))
            {
                if (sample.Phase < 1 || sample.Phase > 3) return;
                attack = new HorrorAttackDriverState();
                _state.Attacks.Add(sample.Hunter, attack);
            }
            HorrorAttackVisual visual = _presenter.PresentAttack(attack, sample, _config.Settings);
            visual.PlayGrowl = false; // Audio owns the budgeted windup cue, not this visual boundary.
            if (!_state.Cues.TryGetValue(sample.Hunter, out HorrorAttackCueDriver cue))
            {
                if (!visual.Visible) return;
                var cueObject = new GameObject("Attack cue " + sample.Hunter.Value);
                cueObject.transform.SetParent(_state.CueRoot.transform, false);
                cue = cueObject.AddComponent<HorrorAttackCueDriver>();
                cue.Initialize(_config, _state.CueMaterial, _presenter.BuildRing(_config.AttackRingSegments),
                    _presenter.BuildArrow(_config.AttackArrowHalfWidth, _config.AttackArrowHeadFraction));
                _state.Cues.Add(sample.Hunter, cue);
            }
            cue.Apply(visual);
        }

        public void ObserveWeaver(WeaverFact fact)
        { if (_web != null && _state != null && _state.OwnerEnabled && isActiveAndEnabled) _web.Observe(fact); }

        public bool AdvanceRunClock(float deltaSeconds)
        {
            if (_state == null || !_state.OwnerEnabled || !isActiveAndEnabled
                || !_presenter.AdvanceRunClock(_state, deltaSeconds)) return false;
            if (_state.ActiveEffects != null) SetActiveEffects(_state.ActiveEffects);
            if (_micro != null) _micro.Tick(_config, _outputCamera, _state.RunElapsedSeconds, deltaSeconds);
            if (_web != null) _web.Tick(deltaSeconds);
            return true;
        }

        public void SetActiveEffects(IReadOnlyActiveEffects effects)
        {
            if (_state == null) return;
            float previous = _state.TorchCountMultiplier; bool wick = _state.Wick;
            _presenter.SetActiveEffects(_state, _config, effects);
            ApplyAtmosphere();
            if (previous != _state.TorchCountMultiplier || wick != _state.Wick)
                LightingHooksChanged?.Invoke(_state.TorchCountMultiplier, _state.Wick);
        }
        public void SetMicroEventWorld(IReadOnlyInteractableSet world, IReadOnlyList<Vector3> unreachableAnchors)
        { if (_micro != null) _micro.SetWorld(world, unreachableAnchors); }
        public void ObservePlayerOpenedDoor(int id, Bounds bounds) { if (_micro != null) _micro.ObservePlayerOpenedDoor(id, bounds); }
        public void SetMicroEventChase(int id, bool active) { if (_micro != null) _micro.SetChase(id, active); }
        public void ObserveMicroEventProximity(ProximitySample sample) { if (_micro != null) _micro.ObserveProximity(sample); }
        public void InvalidateMicroEventChase() { if (_micro != null) _micro.InvalidateChase(); }
        public void SetCounterAvailable(bool available) { if (_micro != null) _micro.SetCounterAvailable(available); }
        public bool ShowMicroSilhouette(Vector3 position, float seconds)
            => _micro != null && _state != null && _micro.ShowSilhouette(position, seconds, _config, _state.CueMaterial);
        private void OnMicroEventSelected(int kind, int target, Vector3 position, float seconds)
            => MicroEventSelected?.Invoke(kind, target, position, seconds);

        public bool TryStartle(double runSeconds, bool earned)
            => _state != null && _state.OwnerEnabled && isActiveAndEnabled
                && _presenter.TryStartle(_state, _config, runSeconds, earned, _startleRandom);

        public void ResetRun(int seed)
        {
            if (_state == null) return;
            ResetRound();
            _presenter.ResetRun(_state);
            _startleRandom = new System.Random(seed);
            if (_micro != null) _micro.ResetRun(_config, new System.Random(seed));
            LightingHooksChanged?.Invoke(_state.TorchCountMultiplier, _state.Wick);
            ApplyAtmosphere();
        }

        public void SetLightingHooks(bool darkerFloors, bool catEyes)
        {
            if (_state == null) return;
            _presenter.SetLightingHooks(_state, _config, darkerFloors, catEyes);
            ApplyAtmosphere();
        }

        public void RemoveAttack(EntityId hunter)
        {
            if (_state == null) return;
            _state.Attacks.Remove(hunter);
            if (!_state.Cues.TryGetValue(hunter, out HorrorAttackCueDriver cue)) return;
            _state.Cues.Remove(hunter);
            if (cue != null) { cue.Teardown(); DestroyOwned(cue.gameObject); }
        }

        public void ResetRound()
        {
            if (_state == null) return;
            if (_web != null) _web.Reset();
            ClearCues();
            _presenter.ResetRound(_state);
            if (_micro != null) _micro.ResetFloor();
            _atmosphere.ClearAfterimage();
            ApplyAtmosphere();
        }

        public void Teardown()
        {
            if (_web != null) { _web.Teardown(); DestroyOwned(_web.gameObject); _web = null; }
            if (_micro != null)
            { _micro.Selected -= OnMicroEventSelected; _micro.ResetFloor(); DestroyOwned(_micro.gameObject); _micro = null; }
            if (_state != null)
            {
                ClearCues();
                DestroyOwned(_state.CueRoot);
                if (_state.OwnsCueMaterial) DestroyOwned(_state.CueMaterial);
            }
            if (_atmosphere != null) { _atmosphere.Teardown(); DestroyOwned(_atmosphere.gameObject); }
            if (_ambience != null) { _ambience.Teardown(); DestroyOwned(_ambience.gameObject); }
            _atmosphere = null;
            _ambience = null;
            _presenter = null;
            _state = null;
            _config = null;
        }

        private void ApplyAtmosphere()
        {
            if (_state == null || _outputCamera == null || _atmosphere == null) return;
            _presenter.CalculateAtmosphere(_state, _config.Settings, _outputCamera.farClipPlane);
            _atmosphere.Apply(_state.FogCurveStart, _state.FogCurveEnd, _state.FlashlightRange,
                _state.FlashlightIntensity, _state.FlashlightEnabled);
            if (_state.HasAuthoritativeFlashlight) _atmosphere.SetFlashlightPose(_state.AuthoritativeFlashlight);
        }

        private void ClearCues()
        {
            foreach (HorrorAttackCueDriver cue in _state.Cues.Values)
                if (cue != null) { cue.Teardown(); DestroyOwned(cue.gameObject); }
            _state.Cues.Clear();
            _state.Attacks.Clear();
        }

        private void OnEnable()
        {
            if (_web != null) _web.enabled = _state != null && _state.OwnerEnabled;
            if (_micro != null) { _micro.Selected -= OnMicroEventSelected; _micro.Selected += OnMicroEventSelected; }
            if (_micro != null) _micro.enabled = _state != null && _state.OwnerEnabled;
            if (_state != null && _state.OwnerEnabled)
            {
                _atmosphere.SetOwnershipEnabled(true);
                if (_ambience != null) _ambience.SetOwnerEnabled(true);
                ApplyAtmosphere();
            }
        }
        private void OnDisable()
        {
            if (_web != null) _web.enabled = false;
            if (_micro != null) { _micro.Selected -= OnMicroEventSelected; _micro.enabled = false; }
            if (_state == null) return;
            _atmosphere.SetOwnershipEnabled(false);
            if (_ambience != null) _ambience.SetOwnerEnabled(false);
            foreach (HorrorAttackCueDriver cue in _state.Cues.Values) if (cue != null) cue.Stop();
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
