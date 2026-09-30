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
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
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
            _state = new HunterAnimationDriverState { Graph = PlayableGraph.Create("Hunter creature animation"), Clips = new AnimationClipPlayable[6] };
            _state.OriginalRootMotion = _animator.applyRootMotion;
            _animator.applyRootMotion = false;
            _state.HasHumanoidRig = _animator.avatar != null && _animator.avatar.isValid && _animator.isHuman;
            _state.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _state.Mixer = AnimationMixerPlayable.Create(_state.Graph, 6);
            var output = AnimationPlayableOutput.Create(_state.Graph, "Creature", _animator);
            output.SetSourcePlayable(_state.Mixer);
            var clips = new[] { _config.Idle, _config.Walk, _config.Run, _config.Windup, _config.Attack, _config.Recovery };
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
        public void Apply(float dt, float speed, int phase, float progress)
        {
            if (!IsReady) return;
            int steps = _presenter.PoseSteps(_state, dt, _config.SampleRate, _config.MaximumPoseSteps, out float step);
            for (int i = 0; i < steps; i++) EvaluatePose(step, speed, phase, progress);
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
            int slot = _presenter.Tick(_state, dt, speed, phase, _config.RunThreshold, _config.BlendSeconds);
            for (int i = 0; i < 6; i++) _state.Mixer.SetInputWeight(i, _state.Weights[i]);
            if (slot >= 3)
            {
                _state.Clips[slot].SetSpeed(0);
                _state.Clips[slot].SetTime(Mathf.Clamp01(progress) * _state.Clips[slot].GetAnimationClip().length);
            }
            else
            {
                if (_state.ActiveClip != slot) _state.Clips[slot].SetTime(0);
                _state.Clips[slot].SetSpeed(1);
            }
            _state.ActiveClip = slot;
            _state.Graph.Evaluate(Mathf.Max(0f, dt));
        }
        public void Teardown()
        {
            if (_state != null)
            {
                if (_state.Graph.IsValid()) _state.Graph.Destroy();
                if (_animator != null) _animator.applyRootMotion = _state.OriginalRootMotion;
            }
            _state = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
