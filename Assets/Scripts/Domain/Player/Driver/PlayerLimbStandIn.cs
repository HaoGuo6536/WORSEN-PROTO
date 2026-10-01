// ============================================================================
// PlayerLimbStandIn.cs
// ============================================================================
// PURPOSE:
//   Positions optional generated blocky arms or legacy hands in first-person view.
//   Hidden by default; the editor generator opts in only when the rigged art exists.
//   Camera rendering supplies final eye and yaw while arms hang independently of
//   head pitch/roll. Injected movement ticks drive eased walking swing. Feet stay hidden.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by PlayerDriver · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Position shoulder roots using descendant renderer bounds and the final lens.
//   - Hide all limbs on teardown, pairing render callbacks with enable/disable.
//   - Store local swing phase/envelope; delegate all pose math to the Presenter.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned. Commanded only by PlayerDriver; generated renderers have no collision shapes.
//   URP render callbacks observe only the tagged MainCamera for this solo player.
//   No camera is cached, reparented or modified; PlayerMoverDriverConfig owns offsets.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    public sealed class PlayerLimbStandIn : MonoBehaviour
    {
        [SerializeField] private bool _showHands = false;
        [SerializeField] private GameObject _leftHand;
        [SerializeField] private GameObject _rightHand;
        [SerializeField] private GameObject _leftFoot;
        [SerializeField] private GameObject _rightFoot;
        private readonly PlayerLimbPresenter _presenter = new PlayerLimbPresenter();
        private Vector3 _handOffset;
        private bool _visible;
        private Renderer[] _leftRenderers;
        private Renderer[] _rightRenderers;
        private float _swingPhase;
        private float _swingEnvelope;

        private void OnEnable() { RenderPipelineManager.beginCameraRendering += BeforeCameraRendering; }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            Apply(MovementState.Ground, 0f, Vector3.zero, Vector3.zero);
        }

        public void Apply(MovementState movement, float eyeHeight, Vector3 handOffset, Vector3 footOffset,
            float horizontalSpeed = 0f, bool crouched = false, float deltaTime = 0f, PlayerMoverDriverConfig config = null)
        {
            _visible = _showHands && eyeHeight > 0f;
            _handOffset = handOffset;
            if (!_visible || config == null) { _swingPhase = 0f; _swingEnvelope = 0f; }
            else
            {
                float target = _presenter.SwingTarget(movement, crouched, horizontalSpeed,
                    config.ArmSwingDegrees, config.ArmSwingReferenceSpeed);
                _presenter.StepSwing(_swingPhase, _swingEnvelope, target, config.ArmSwingDegrees,
                    config.ArmSwingFrequency, config.ArmSwingEaseSeconds, deltaTime, out _swingPhase, out _swingEnvelope);
            }
            if (_leftHand != null)
            {
                _leftHand.SetActive(_visible);
                if (_leftRenderers == null || _leftRenderers.Length == 0 || _leftRenderers[0] == null)
                    _leftRenderers = _leftHand.GetComponentsInChildren<Renderer>(true);
            }
            if (_rightHand != null)
            {
                _rightHand.SetActive(_visible);
                if (_rightRenderers == null || _rightRenderers.Length == 0 || _rightRenderers[0] == null)
                    _rightRenderers = _rightHand.GetComponentsInChildren<Renderer>(true);
            }
            if (_leftFoot != null) _leftFoot.SetActive(false);
            if (_rightFoot != null) _rightFoot.SetActive(false);
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_visible || camera == null || camera.cameraType != CameraType.Game || !camera.CompareTag("MainCamera")) return;
            PlaceHand(_leftHand, _leftRenderers, camera, true);
            PlaceHand(_rightHand, _rightRenderers, camera, false);
        }

        private void PlaceHand(GameObject hand, Renderer[] renderers, Camera camera, bool left)
        {
            if (hand == null) return;
            Vector3 forward = camera.transform.forward;
            Quaternion yaw = _presenter.YawRotation(forward, transform.forward);
            Quaternion rotation = _presenter.ArmRotation(yaw, _presenter.SwingAngle(_swingPhase, _swingEnvelope, left));
            Vector3 shoulder = _presenter.ShoulderOffset(_handOffset, left, yaw);
            // Set the candidate pose first: renderer.bounds must reflect this render's
            // yaw/swing, not the previous camera callback or interpolated parent pose.
            hand.transform.SetPositionAndRotation(camera.transform.position + shoulder, rotation);
            float rearExtent = 0f;
            if (renderers != null)
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    Bounds bounds = renderer.bounds;
                    renderer.enabled = !_presenter.BelowView(bounds.center - camera.transform.position,
                        bounds.extents, forward, camera.transform.up, camera.fieldOfView);
                    rearExtent = Mathf.Max(rearExtent, _presenter.RearExtent(
                        bounds.center - hand.transform.position, bounds.extents, forward));
                }
            Vector3 offset = _presenter.ClearNearPlane(shoulder, forward, camera.nearClipPlane, rearExtent);
            hand.transform.SetPositionAndRotation(camera.transform.position + offset, rotation);
        }
    }
}