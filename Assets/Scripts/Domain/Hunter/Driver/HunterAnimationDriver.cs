// ============================================================================
// HunterAnimationDriver.cs
// ============================================================================
// PURPOSE:
//   Applies the named Hunter engine interaction from explicit owner commands.
//   Unity physics, animation or rendering remains at this engine boundary.
//   Contacts and observable feedback return to the Manager through typed events.
// ARCHITECTURAL ROLE:
//   Sub-driver (section 7e), owned by HunterDriver - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Evaluate speed-matched gait blends and distinct ready/attack/recovery/hit clips.
//   - Retain committed module one-shots across the quantised pose cadence.
//   - Quantise manual graph evaluations, never motor time; enable playable humanoid IK callbacks.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned, no independent simulation loop. Teardown destroys only owned transient effects.
//   The manual PlayableGraph is the sole pose clock (no Animator.Update or automatic graph clock).
//   HunterDriver binds the replaceable IK backend on the Animator's own GameObject.
// ============================================================================
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private HunterAnimationDriverConfig _config;
        private HunterAnimationDriverState _state;
        private readonly HunterAnimationPresenter _presenter = new HunterAnimationPresenter();
        public bool IsReady => _state != null && _state.Graph.IsValid();
        public Animator Animator => _animator;
        public HunterAnimationDriverConfig Config => _config;
        public HunterAnimationDriverState State => _state;
        public void Initialize()
        {
            Teardown();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _config == null || _config.Idle == null) return;
            _state = new HunterAnimationDriverState { Graph = PlayableGraph.Create("Hunter creature animation"), Clips = new AnimationClipPlayable[7] };
            _state.OriginalRootMotion = _animator.applyRootMotion;
            _state.OriginalCullingMode = _animator.cullingMode;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _state.HasHumanoidRig = _animator.avatar != null && _animator.avatar.isValid && _animator.isHuman;
            _state.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _state.Mixer = AnimationMixerPlayable.Create(_state.Graph, _state.Clips.Length);
            var output = AnimationPlayableOutput.Create(_state.Graph, "Creature", _animator);
            output.SetSourcePlayable(_state.Mixer);
            var clips = new[] { _config.Idle, _config.Walk, _config.Run, _config.Windup, _config.Attack, _config.Recovery, _config.Hit };
            for (int i = 0; i < clips.Length; i++)
            {
                _state.Clips[i] = AnimationClipPlayable.Create(_state.Graph, clips[i] != null ? clips[i] : _config.Idle);
                _state.Clips[i].SetApplyFootIK(false);
                _state.Clips[i].SetApplyPlayableIK(_state.HasHumanoidRig);
                _state.Graph.Connect(_state.Clips[i], 0, _state.Mixer, i);
            }
            _state.Graph.Play();
            EvaluatePose(0f, 0f, 0, 0f);
        }
        public void Trigger(HunterAnimationPhase phase)
        { if (IsReady) _presenter.Trigger(_state, phase); }
        public void Apply(float dt, float speed, int phase, float progress, HunterAnimationPhase module = HunterAnimationPhase.None)
        {
            if (!IsReady) return;
            int steps = _presenter.PoseSteps(_state, dt, _config.SampleRate, _config.MaximumPoseSteps, out float step);
            for (int i = 0; i < steps; i++)
            {
                int resolved = _presenter.ResolvePhase(_state, step, phase, progress, module,
                    _state.Clips[3].GetAnimationClip().length, _state.Clips[4].GetAnimationClip().length,
                    _state.Clips[6].GetAnimationClip().length, out float poseProgress);
                EvaluatePose(step, speed, resolved, poseProgress);
            }
        }
        public void SetLook(Vector3 target, bool looking, bool catchActive)
        {
            if (!IsReady) return;
            bool beganCatch = catchActive && !_state.CatchActive;
            _state.LookTarget = target + Vector3.up * _config.LookTargetHeight;
            _state.Looking = looking; _state.CatchActive = catchActive;
            // An accepted catch overrides the pose cadence immediately, even after Session stops ticking.
            if (beganCatch)
            { _presenter.Look(_state, 0f, _config.LookBlendInSeconds, _config.LookBlendOutSeconds);
                _state.PoseDeltaTime = 0f; _state.IKApplied = false; _state.Graph.Evaluate(0f); }
        }
        private void EvaluatePose(float dt, float speed, int phase, float progress)
        {
            _state.PoseDeltaTime = dt;
            _state.IKApplied = false;
            _presenter.Look(_state, dt, _config.LookBlendInSeconds, _config.LookBlendOutSeconds);
            int slot = _presenter.Tick(_state, dt, speed, phase, _config.RunThreshold, _config.BlendSeconds, _config.RunHysteresis);
            for (int i = 0; i < _state.Clips.Length; i++) _state.Mixer.SetInputWeight(i, _state.Weights[i]);
            // Both gaits keep their clocks through crossfades; threshold jitter must
            // never restart a stride. Inactive phase clips stay frozen at their last pose.
            _state.Clips[0].SetSpeed(1);
            _state.Clips[1].SetSpeed(_presenter.PlaybackRate(speed, _config.WalkStrideSpeed, _config.MinimumLocomotionRate, _config.MaximumLocomotionRate));
            _state.Clips[2].SetSpeed(_presenter.PlaybackRate(speed, _config.RunStrideSpeed, _config.MinimumLocomotionRate, _config.MaximumLocomotionRate));
            for (int i = 3; i < _state.Clips.Length; i++) _state.Clips[i].SetSpeed(0);
            if (slot >= 3)
            {
                _state.Clips[slot].SetSpeed(0);
                _state.Clips[slot].SetTime(Mathf.Clamp01(progress) * _state.Clips[slot].GetAnimationClip().length);
            }

            _state.ActiveClip = slot;
            _state.Graph.Evaluate(Mathf.Max(0f, dt));
        }
        public void Teardown()
        {
            if (_state != null)
            {
                if (_state.Graph.IsValid()) _state.Graph.Destroy();
                if (_animator != null)
                { _animator.applyRootMotion = _state.OriginalRootMotion; _animator.cullingMode = _state.OriginalCullingMode; }
            }
            _state = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
