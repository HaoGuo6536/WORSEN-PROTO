// ============================================================================
// HunterManager.cs
// ============================================================================
// PURPOSE:
//   Connects one hunter's sensors, decisions and physical movement in a fixed step.
//   Contact colliders become entity identities here, and accepted lunge facts flow
//   upward so the Run Session can apply damage without Hunter calling Player.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Hunter (Entity system).
// KEY RESPONSIBILITIES:
//   - Initialize/reset the owned stacks and sequence probe, decision and pose commit.
//   - Resolve engine identities and publish sight and hit observations.
//   - Apply aggregate run speed through instance state and expose Core attack presentation facts.
// DEPENDENCIES:
//   - Reads injected Player and Level read-only views; Core identity and event facts.
// USAGE NOTES:
//   Scene-owned. Initialize fully resets each life; Factory supplies all dependencies.
//   Session is the sole tick owner. Driver subscriptions pair OnEnable/OnDisable.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    [RequireComponent(typeof(HunterDriver))]
    public sealed class HunterManager : MonoBehaviour, IEntityHandle
    {
        [SerializeField] private HunterDriver _driver;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private HunterProfile _profile;
        private IReadOnlyPlayerState _player;
        public EntityId Id => _state?.Id ?? EntityId.None;
        public IReadOnlyHunterState ReadOnlyState => _state;
        public HunterAttackSample AttackSample => _controller?.AttackSample() ?? default;
        public event Action<HunterHit> OnLungeHit;
        public event Action<HunterSighting> OnSighting;
        private void Awake() { if (_driver == null) _driver = GetComponent<HunterDriver>(); }
        private void OnEnable()
        {
            if (_driver == null) _driver = GetComponent<HunterDriver>();
            _driver.OnLungeContact += HandleContact;
        }
        private void OnDisable()
        {
            if (_driver != null) _driver.OnLungeContact -= HandleContact;
            HunterRegistry.Unregister(this);
        }
        public void Initialize(HunterProfile profile, EntityContext context, IReadOnlyPlayerState player, IReadOnlyLevelState level)
        {
            if (profile == null || player == null || level == null || context.Random == null || !context.Id.IsValid)
                throw new ArgumentException("Hunter initialization requires profile, identity, shared random and typed state views.");
            if (_driver == null) _driver = GetComponent<HunterDriver>();
            _profile = profile; _player = player; _driver.Initialize();
            _state = new HunterBehaviorState();
            _controller = new HunterController(_state, profile, context.Random, player, level);
            _controller.Reset(context.Id, _driver.Position, _driver.Forward);
        }
        public void Tick(float dt, long tick)
        {
            if (_controller == null || !_state.IsActive) return;
            bool sample = _controller.ShouldProbe(tick);
            SightProbe sight = sample ? _driver.ProbeSight(_player.Position, IsTarget) : default;
            HunterTickResult result = _controller.Tick(sight, dt, tick);
            _driver.Move(result.Target, result.Speed, _profile.Acceleration, _profile.TurnRate, dt,
                !_state.IsActive || result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery,
                result.ActiveContact, result.LungeDirection, _controller.LungeSpeed, _profile.LungeDistance);
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (!_driver.PathAvailable && result.Phase == HunterLungePhase.None) _controller.ReportPathFailure();
            if (sample) OnSighting?.Invoke(_controller.Sighting());
        }
        private bool IsTarget(Collider collider)
        {
            IEntityHandle handle = collider.GetComponentInParent<IEntityHandle>();
            return handle != null && handle.Id == _state.TargetId;
        }
        private void HandleContact(Collider collider)
        {
            if (_controller == null) return;
            IEntityHandle handle = collider.GetComponentInParent<IEntityHandle>();
            if (handle == null) return;
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (_controller.TryAcceptContact(handle.Id, out HunterHit hit)) OnLungeHit?.Invoke(hit);
        }
        public void ApplyRunSpeedMultiplier(float multiplier) { _controller?.ApplyRunSpeedMultiplier(multiplier); }
        public void ReceiveHint(HintPayload hint) { _controller?.ReceiveHint(hint); }
        public void Teardown()
        {
            if (_driver != null) _driver.Teardown();
            HunterRegistry.Unregister(this);
            _controller = null; _state = null; _profile = null; _player = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
