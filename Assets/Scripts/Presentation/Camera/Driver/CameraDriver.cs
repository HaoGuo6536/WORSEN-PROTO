// ============================================================================
// CameraDriver.cs
// ============================================================================
//
// PURPOSE:
//   Applies the pure first-person view to a dedicated Cinemachine rig.
//   The package stays behind this boundary so gameplay and view math need no vendor types.
//
// ARCHITECTURAL ROLE:
//   Driver (Â§7a) Â· Presentation Â· Camera.
//
// KEY RESPONSIBILITIES:
//   - Bind the serialized output camera and rebuild missing owned rig components.
//   - Apply pose and lens in LateUpdate, then manually advance the owned brain.
//   - Expose unshaken aim and route explicit event shake without moving gameplay authority.
//   - Advance terminal consumption with unscaled presentation time.
//   - Generate the detection impulse and release only owned runtime objects.
//
// DEPENDENCIES:
//   - Core movement facts; Unity.Cinemachine package only in this Driver.
//
// USAGE NOTES:
//   - Scene-owned by CameraManager; owns its rig and dedicated output camera's brain.
//   - LateUpdate advances presentation time only; it never ticks gameplay.
//   - Manual brain update runs after pose assignment, avoiding script-order dependence.
//   - ConfigureForSetup is an editor wiring entry point; Initialize starts runtime work.
//   - No global time or render settings are changed.
//
// ============================================================================

using Unity.Cinemachine;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Camera
{
    public sealed class CameraDriver : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera _outputCamera;
        [SerializeField] private CinemachineCamera _rig;
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private CinemachineImpulseListener _listener;
        private CameraDriverConfig _config;
        private CameraDriverState _state;
        private CameraFeedbackPresenter _presenter;
        private bool _runtimeRig;
        private bool _runtimeBrain;
        private bool _runtimeImpulse;
        private bool _runtimeListener;
        private CinemachineBrain.UpdateMethods _previousUpdateMethod;
        private bool _previousBrainEnabled;

        public Quaternion AimRotation => _state != null ? _presenter.AimRotation(_state) : Quaternion.identity;
        public float ConsumptionSeconds => _config != null ? _presenter.ConsumptionSeconds(_config) : 0f;
        public Vector3 AimPosition => _state?.EyePosition ?? Vector3.zero;

        public bool IsReady => _state != null && _outputCamera != null && _rig != null && _brain != null
            && _impulse != null && _listener != null;

        public void ConfigureForSetup(UnityEngine.Camera outputCamera)
        {
            _outputCamera = outputCamera;
            BuildMissingRig(false);
        }

        public void Initialize(CameraDriverConfig config)
        {
            Teardown();
            if (config == null || _outputCamera == null)
            {
                Debug.LogWarning("Camera needs its config and a serialized output camera. Rebuild its CameraRigSetup wiring.", this);
                return;
            }
            _config = config;
            if (_rig == null || _brain == null || _impulse == null || _listener == null)
            {
                Debug.LogWarning("Camera rig wiring was missing; rebuilding owned Cinemachine components.", this);
                BuildMissingRig(true);
            }
            _state = new CameraDriverState();
            _presenter = new CameraFeedbackPresenter();
            _previousUpdateMethod = _brain.UpdateMethod;
            _previousBrainEnabled = _brain.enabled;
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            _rig.Lens.NearClipPlane = config.NearClip;
            _rig.Lens.FarClipPlane = config.FarClip;
            _impulse.ImpulseDefinition.ImpulseChannel = 1;
            _impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            _impulse.ImpulseDefinition.ImpulseDuration = config.ImpulseSeconds;
            _impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            _listener.ChannelMask = 1;
            _listener.ApplyAfter = CinemachineCore.Stage.Noise;
            _listener.UseCameraSpace = true;
            _listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.UseLargest;
            _rig.enabled = isActiveAndEnabled;
            _brain.enabled = isActiveAndEnabled;
        }

        public void SetMovement(PlayerMovementSample sample)
        {
            if (_state != null) _presenter.SetMovement(_state, _config, sample);
        }

        public void SetLookBack(bool held)
        {
            if (_state != null) _presenter.SetLookBack(_state, _config, held);
        }

        public void PlayDetectionBeat()
        {
            if (_state == null || _state.DeathSnapped || !isActiveAndEnabled) return;
            _presenter.PlayDetectionBeat(_state);
            _impulse.GenerateImpulseWithVelocity(_presenter.DetectionImpulseVelocity(_config));
        }

        public void PlayShake(float strength, float seconds)
        {
            if (_state != null) _presenter.PlayShake(_state, strength, seconds);
        }

        public void SetProximity(float closeness)
        {
            if (_state != null) _presenter.SetProximity(_state, closeness);
        }

        public void PlayTraversal(PlayerTraversalFact fact)
        {
            if (_state == null) return;
            _presenter.PlayTraversal(_state, fact, _config);
        }

        public void PlayDeathSnap(Vector3 killerPosition)
        {
            if (_state != null) _presenter.PlayDeathSnap(_state, killerPosition);
        }

        public void PlayConsumed(Vector3 handPosition)
        {
            if (_state != null) _presenter.PlayConsumed(_state, _config, handPosition);
        }

        public void ResetView()
        {
            if (_state != null) _presenter.Reset(_state);
            if (_listener != null) _listener.Gain = 0f;
        }

        public void Teardown()
        {
            if (_state != null && _brain != null)
            {
                _brain.UpdateMethod = _previousUpdateMethod;
                _brain.enabled = _previousBrainEnabled;
            }
            if (_rig != null) _rig.enabled = false;
            if (_listener != null) _listener.Gain = 0f;
            if (_runtimeRig && _rig != null) Destroy(_rig.gameObject);
            if (!_runtimeRig && _runtimeImpulse && _impulse != null) Destroy(_impulse);
            if (!_runtimeRig && _runtimeListener && _listener != null) Destroy(_listener);
            if (_runtimeBrain && _brain != null) Destroy(_brain);
            if (_runtimeRig) { _rig = null; _impulse = null; _listener = null; }
            if (_runtimeImpulse) _impulse = null;
            if (_runtimeListener) _listener = null;
            if (_runtimeBrain) _brain = null;
            _runtimeRig = _runtimeBrain = false;
            _runtimeImpulse = _runtimeListener = false;
            _state = null;
            _presenter = null;
            _config = null;
        }

        private void BuildMissingRig(bool runtime)
        {
            if (_outputCamera == null) return;
            if (_brain == null) _brain = _outputCamera.GetComponent<CinemachineBrain>();
            if (_brain == null)
            {
                _brain = _outputCamera.gameObject.AddComponent<CinemachineBrain>();
                _runtimeBrain = runtime;
            }
            if (_rig == null)
            {
                var rigObject = new GameObject("First Person Rig");
                rigObject.transform.SetParent(transform, false);
                _rig = rigObject.AddComponent<CinemachineCamera>();
                _runtimeRig = runtime;
            }
            if (_impulse == null) _impulse = _rig.GetComponent<CinemachineImpulseSource>();
            if (_impulse == null)
            {
                _impulse = _rig.gameObject.AddComponent<CinemachineImpulseSource>();
                _runtimeImpulse = runtime;
            }
            if (_listener == null) _listener = _rig.GetComponent<CinemachineImpulseListener>();
            if (_listener == null)
            {
                _listener = _rig.gameObject.AddComponent<CinemachineImpulseListener>();
                _runtimeListener = runtime;
            }
            _rig.enabled = false;
        }

        private void LateUpdate()
        {
            if (_state == null || !_state.HasMovement) return;
            _presenter.Tick(_state, _config, _state.Consumed ? Time.unscaledDeltaTime : Time.deltaTime, _outputCamera.aspect);
            _rig.transform.SetPositionAndRotation(_state.Position, _state.Rotation);
            _rig.Lens.FieldOfView = _state.VerticalFieldOfView;
            _listener.Gain = _state.DeathSnapped ? 0f : _config.PunchIntensity * _config.ShakeIntensity;
            _brain.ManualUpdate();
        }

        private void OnEnable()
        {
            if (_state == null) return;
            if (_rig != null) _rig.enabled = true;
            if (_brain != null) _brain.enabled = true;
        }

        private void OnDisable()
        {
            ResetView();
            if (_state == null) return;
            if (_rig != null) _rig.enabled = false;
            if (_brain != null) _brain.enabled = false;
        }
    }
}
