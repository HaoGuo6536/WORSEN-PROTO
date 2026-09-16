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
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned, no independent simulation loop. Teardown destroys only owned transient effects.
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
        public void Initialize()
        {
            Teardown();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _config == null || _config.Idle == null) return;
            _animator.applyRootMotion = false;
            _state = new HunterAnimationDriverState { Graph = PlayableGraph.Create("Hunter creature animation"), Clips = new AnimationClipPlayable[6] };
            _state.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _state.Mixer = AnimationMixerPlayable.Create(_state.Graph, 6);
            var output = AnimationPlayableOutput.Create(_state.Graph, "Creature", _animator);
            output.SetSourcePlayable(_state.Mixer);
            var clips = new[] { _config.Idle, _config.Walk, _config.Run, _config.Windup, _config.Attack, _config.Recovery };
            for (int i = 0; i < clips.Length; i++)
            {
                _state.Clips[i] = AnimationClipPlayable.Create(_state.Graph, clips[i] != null ? clips[i] : _config.Idle);
                _state.Clips[i].SetApplyFootIK(false);
                _state.Graph.Connect(_state.Clips[i], 0, _state.Mixer, i);
            }
            _state.Graph.Play();
            Apply(0f, 0f, 0, 0f);
        }
        public void Apply(float dt, float speed, int phase, float progress)
        {
            if (!IsReady) return;
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
        { if (_state != null && _state.Graph.IsValid()) _state.Graph.Destroy(); _state = null; }
        private void OnDestroy() { Teardown(); }
    }
}
