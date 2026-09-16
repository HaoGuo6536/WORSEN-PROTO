// ============================================================================
// HorrorDriver.cs
// ============================================================================
//
// PURPOSE:
//   Sequences a private atmosphere rig, quiet ambience and enemy warning objects from pushed commands.
//   Pure calculations produce the lighting and attack outputs; sub-drivers own the engine mutations.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Own atmosphere and ambience sub-drivers, attack cues, shared material and transient state.
//   - Forward camera and volume wiring, apply modifiers, and completely reset warning objects.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and EntityId; its own Horror presentation stack.
//
// USAGE NOTES:
//   Scene-owned by HorrorManager. Configure camera, fog volume and daylights before Initialize.
//   The atmosphere sub-driver exclusively owns global render settings while enabled.
//   No gameplay polling, global singleton reads or vendor API leaks outside this Driver stack.
//
// ============================================================================

using UnityEngine;
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
        private HorrorAtmosphereDriver _atmosphere;
        private HorrorAmbienceDriver _ambience;
        public bool IsReady => _state != null && _atmosphere != null && _atmosphere.IsReady
            && _state.CueMaterial != null && _config.AttackGrowl != null;
        public bool FlashlightEnabled => _state != null && _state.FlashlightEnabled;
        public float FogCurveStart => _state != null ? _state.FogCurveStart : 0f;
        public float FogCurveEnd => _state != null ? _state.FogCurveEnd : 0f;
        public float FlashlightRange => _state != null ? _state.FlashlightRange : 0f;

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
            _presenter = new HorrorPresenter();
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
            _atmosphere.SetOwnershipEnabled(value && isActiveAndEnabled);
            if (_ambience != null) _ambience.SetOwnerEnabled(value && isActiveAndEnabled);
            ApplyAtmosphere();
            if (!value)
                foreach (HorrorAttackCueDriver cue in _state.Cues.Values) if (cue != null) cue.Stop();
        }

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
            ClearCues();
            _presenter.ResetRound(_state);
            ApplyAtmosphere();
        }

        public void Teardown()
        {
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
            if (_state != null && _state.OwnerEnabled)
            {
                _atmosphere.SetOwnershipEnabled(true);
                if (_ambience != null) _ambience.SetOwnerEnabled(true);
                ApplyAtmosphere();
            }
        }
        private void OnDisable()
        {
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
