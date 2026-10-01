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
//   - Smooth collapse facts into fog and torch-budget outputs.
//   - Own and release floor-local Blinder, Weaver, Afterglow and detached Lumen rigs.
//   - Own micro-events and republish decisions and lighting changes.
//   - Sequence atmosphere, ambience and attack-cue engine boundaries.
//   - Advance presentation with injected time and preserve run-wide startle admission.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and EntityId; its own Horror presentation stack.
//
// USAGE NOTES:
//   Scene-owned by HorrorManager. Configure camera, fog volume and daylights before Initialize.
//   The atmosphere sub-driver exclusively owns global render settings while enabled.
//   No gameplay polling, global singleton reads or vendor API leaks outside this Driver stack.
//   RunElapsedSeconds is NaN until initialized; disabled owners cannot advance the clock.
//   Attack/web materials or shaders must be serialized; missing wiring reports once and prevents startup.
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
        private BlinderProjectileDriver _blinder;
        private HorrorAfterglowDriver _afterglow;
        // DriverState (§7c): retained diagnostic, separate from resettable atmosphere state.
        private sealed class ShaderReferenceDriverState { public bool Reported; }
        private readonly ShaderReferenceDriverState _shaderState = new ShaderReferenceDriverState();
        public event Action<int, int, Vector3, float> MicroEventSelected;
        public event Action<float, bool> LightingHooksChanged;
        public float TorchCountMultiplier => _state != null ? HorrorCollapsePresenter.TorchMultiplier(_state, _config) : 1f;
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
            if (_config != null && ((_config.AttackMaterial == null && _config.AttackShader == null) ||
                (_config.WebMaterial == null && _config.WebShader == null)))
            {
                if (!_shaderState.Reported)
                {
                    _shaderState.Reported = true;
                    Debug.LogError("HorrorDriverConfig requires attack and web materials or shaders. Rebuild Horror assets.", this);
                }
                return;
            }
            if (_config == null || _outputCamera == null)
            {
                Debug.LogWarning("Horror needs its config and explicitly wired output camera.", this);
                return;
            }
            _state = new HorrorDriverState();
            var afterglowObject = new GameObject("Owned Afterglow visuals");
            afterglowObject.transform.SetParent(transform, false);
            _afterglow = afterglowObject.AddComponent<HorrorAfterglowDriver>();
            _afterglow.Initialize(_config);
            var webObject = new GameObject("Owned Weaver visuals");
            webObject.transform.SetParent(transform, false);
            _web = webObject.AddComponent<HorrorWebDriver>(); _web.Initialize(_config);
            var blinderObject = new GameObject("Owned Blinder visuals");
            blinderObject.transform.SetParent(transform, false);
            _blinder = blinderObject.AddComponent<BlinderProjectileDriver>(); _blinder.Initialize(_config);
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
                _state.CueMaterial = new Material(_config.AttackShader) { name = "Owned attack cue" };
                _state.OwnsCueMaterial = true;
            }
            if (_config.AttackGrowl == null)
                Debug.LogWarning("Horror attack growl is missing. Assign the imported monster growl in its config.", this);
            ApplyAtmosphere();
        }

        public void SetOwnerEnabled(bool value)
        {
            if (_state == null) return;
            _state.OwnerEnabled = value;
            if (_blinder != null) { _blinder.enabled = value && isActiveAndEnabled; if (!value) _blinder.Reset(); }
            if (_web != null) _web.enabled = value && isActiveAndEnabled;
            if (_afterglow != null) _afterglow.enabled = value && isActiveAndEnabled;
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
        public void SetAfterglow(InteractableState light, float safetySeconds)
        { if (_afterglow != null && _state != null && _state.OwnerEnabled && isActiveAndEnabled) _afterglow.Observe(light, safetySeconds); }

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

        public void SetCollapseRooms(IReadOnlyList<GeneratedRoomSample> rooms, int? exitRoom)
        {
            if (_state == null) return;
            HorrorCollapsePresenter.BeginFloor(_state, rooms, exitRoom);
            ApplyAtmosphere();
            LightingHooksChanged?.Invoke(TorchCountMultiplier, _state.Wick);
        }
        public void ObserveCollapse(RoomDestructionSample sample)
        { if (_state != null) HorrorCollapsePresenter.Observe(_state, sample, _config); }

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
            if (_afterglow != null) _afterglow.Tick(deltaSeconds);
            if (HorrorCollapsePresenter.Tick(_state, _config, deltaSeconds))
            { ApplyAtmosphere(); LightingHooksChanged?.Invoke(TorchCountMultiplier, _state.Wick); }
            return true;
        }

        public void SetActiveEffects(IReadOnlyActiveEffects effects)
        {
            if (_state == null) return;
            float previous = TorchCountMultiplier; bool wick = _state.Wick;
            _presenter.SetActiveEffects(_state, _config, effects);
            if (_afterglow != null) _afterglow.SetEffects(effects);
            ApplyAtmosphere();
            if (previous != TorchCountMultiplier || wick != _state.Wick)
                LightingHooksChanged?.Invoke(TorchCountMultiplier, _state.Wick);
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
            ClearProjectiles();
            if (_state == null) return;
            if (_web != null) _web.Reset();
            if (_afterglow != null) _afterglow.Clear();
            ClearCues();
            _presenter.ResetRound(_state);
            if (_micro != null) _micro.ResetFloor();
            // The authoritative rig detaches from the camera, and afterimages live under
            // Horror rather than the generated floor. Hiding only the afterimage retained
            // both old-floor components and the rig's old world-space aim across regeneration.
            _atmosphere.ResetFloor();
            ApplyAtmosphere();
            LightingHooksChanged?.Invoke(TorchCountMultiplier, _state.Wick);
        }

        public void Teardown()
        {
            if (_blinder != null) { _blinder.Teardown(); DestroyOwned(_blinder.gameObject); _blinder = null; }
            if (_afterglow != null) { _afterglow.Clear(); DestroyOwned(_afterglow.gameObject); _afterglow = null; }
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
            var settings = _config.Settings;
            if (_state.HasCollapseFloor) settings.FogNearMeters = HorrorCollapsePresenter.FogNear(_state, _config);
            _presenter.CalculateAtmosphere(_state, settings, _outputCamera.farClipPlane);
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
            if (_blinder != null) _blinder.enabled = _state != null && _state.OwnerEnabled;
            if (_afterglow != null) _afterglow.enabled = _state != null && _state.OwnerEnabled;
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
            ClearProjectiles();
            if (_blinder != null) _blinder.enabled = false;
            if (_afterglow != null) _afterglow.enabled = false;
            if (_web != null) _web.enabled = false;
            if (_micro != null) { _micro.Selected -= OnMicroEventSelected; _micro.enabled = false; }
            if (_state == null) return;
            _atmosphere.SetOwnershipEnabled(false);
            if (_ambience != null) _ambience.SetOwnerEnabled(false);
            foreach (HorrorAttackCueDriver cue in _state.Cues.Values) if (cue != null) cue.Stop();
        }
        public void LaunchProjectile(BlinderThrowFact fact)
        { if (_state != null && _state.OwnerEnabled && isActiveAndEnabled && _blinder != null) _blinder.Observe(fact); }
        public void HitProjectile(BlinderHitFact fact) { if (_blinder != null) _blinder.ObserveHit(fact); }
        public void TickProjectiles(float delta)
        { if (_state != null && _state.OwnerEnabled && isActiveAndEnabled && _blinder != null) _blinder.Tick(delta); }
        public void ClearProjectiles() { if (_blinder != null) _blinder.Reset(); }
        private void OnDestroy() => Teardown();
        private static void DestroyOwned(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
