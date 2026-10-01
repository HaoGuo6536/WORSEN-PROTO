// ============================================================================
// HunterAnimatorIKDriver.cs
// ============================================================================
// PURPOSE:
//   Isolates Unity's humanoid inverse kinematics backend from Hunter decisions.
//   The callback lives on the Animator object, even when the animation command
//   component lives above it. Generic or missing rigs safely do nothing.
// ARCHITECTURAL ROLE:
//   Sub-driver (section 7e), owned by HunterDriver - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Apply supplied gaze weights and raycast each foot independently.
//   - Ignore the owning body, other Hunters' bodies, triggers, steep hits and saturated query buffers.
// DEPENDENCIES:
//   - Hunter animation state/config, animation and body presenters, Unity Animator/Physics only.
// USAGE NOTES:
//   Scene-owned; HunterDriver binds and unbinds it with the manual animation graph.
//   No Final IK references. A future backend can replace this component and binding.
//   Playable clips request IK callbacks; no Animator Controller IK-pass asset is required.
//   Hunters ignore each other's bodies (owner, 2026-10-01): feet never plant on another
//   Hunter, because Bind removes the HunterBody layer from the configured ground mask.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAnimatorIKDriver : MonoBehaviour
    {
        private Animator _animator;
        private Transform _bodyRoot;
        private HunterAnimationDriverConfig _config;
        private HunterAnimationDriverState _state;
        private int _groundMask;
        private readonly HunterAnimationPresenter _presenter = new HunterAnimationPresenter();
        private readonly HunterBodyPresenter _bodies = new HunterBodyPresenter();
        public void Bind(Animator animator, Transform bodyRoot, HunterAnimationDriverConfig config, HunterAnimationDriverState state)
        {
            _animator = animator; _bodyRoot = bodyRoot; _config = config; _state = state;
            _groundMask = config != null ? _bodies.WithoutLayer(config.GroundMask, LayerMask.NameToLayer("HunterBody")) : 0;
        }
        public void Unbind() { _animator = null; _bodyRoot = null; _config = null; _state = null; }
        private void OnAnimatorIK(int layerIndex)
        {
            if (_state == null || !_state.HasHumanoidRig || _animator == null || !_animator.isHuman || _config == null) return;
            _animator.SetLookAtPosition(_state.LookTarget);
            _animator.SetLookAtWeight(_state.LookWeight, _config.LookBodyWeight, _config.LookHeadWeight, 0f, _config.LookClampWeight);
            ApplyFoot(0, AvatarIKGoal.LeftFoot);
            ApplyFoot(1, AvatarIKGoal.RightFoot);
            _state.IKApplied = true;
        }
        private void ApplyFoot(int index, AvatarIKGoal goal)
        {
            Vector3 animated = _animator.GetIKPosition(goal);
            Quaternion rotation = _animator.GetIKRotation(goal);
            bool grounded = false;
            Vector3 position = animated;
            if (_config.FootIK)
            {
                int count = Physics.RaycastNonAlloc(animated + Vector3.up * _config.FootProbeUp, Vector3.down,
                    _state.FootHits, _config.FootProbeUp + _config.FootProbeDown, _groundMask, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                if (count < _state.FootHits.Length) for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = _state.FootHits[i];
                    if (hit.collider.transform.IsChildOf(_bodyRoot) || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    grounded = _presenter.FootTarget(animated, _animator.GetIKRotation(goal), hit.point, hit.normal,
                        _config.FootOffset, _config.MaximumFootOffset, _config.FootSlopeLimit, out position, out rotation);
                }
            }
            _presenter.Foot(_state, index, grounded, position, rotation, _state.IKApplied ? 0f : _state.PoseDeltaTime, _config.FootBlendSeconds);
            _animator.SetIKPositionWeight(goal, _state.FootWeights[index]);
            _animator.SetIKRotationWeight(goal, _state.FootWeights[index]);
            if (_state.FootWeights[index] <= 0f) return;
            _animator.SetIKPosition(goal, _state.FootPositions[index]);
            _animator.SetIKRotation(goal, _state.FootRotations[index]);
        }
    }
}
