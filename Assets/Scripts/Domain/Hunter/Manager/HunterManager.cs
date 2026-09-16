// ============================================================================
// HunterManager.cs
// ============================================================================
// PURPOSE:
//   Coordinates one Hunter sensor, decision and engine presentation stack.
//   Raw collision contacts become entity identities here and committed feedback
//   travels upward as Core events without Hunter calling audio or player damage.
// ARCHITECTURAL ROLE:
//   Manager (section 1), Entity system - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned entity; Session is sole tick owner. Subscriptions pair OnEnable/OnDisable.
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
        public HunterAttackSample AttackSample => _profile != null && _profile.AttackStyle == HunterAttackStyle.Lunge ?
            _controller?.AttackSample() ?? default : default;
        public event Action<HunterHit> OnLungeHit;
        public event Action<HunterSighting> OnSighting;
        public event Action<HunterFeedbackEvent> OnFeedback;
        private void Awake() { if (_driver == null) _driver = GetComponent<HunterDriver>(); }
        private void OnEnable()
        {
            if (_driver == null) _driver = GetComponent<HunterDriver>();
            _driver.OnLungeContact += HandleContact;
            _driver.OnRangedContact += HandleRangedContact;
            _driver.OnRangedMiss += HandleRangedMiss;
            _driver.OnAttackFeedback += HandleAttackFeedback;
        }
        private void OnDisable()
        {
            if (_driver != null)
            {
                _driver.OnLungeContact -= HandleContact;
                _driver.OnRangedContact -= HandleRangedContact;
                _driver.OnRangedMiss -= HandleRangedMiss;
                _driver.OnAttackFeedback -= HandleAttackFeedback;
            }
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
            _driver.SetTargetFilter(IsTarget);
            _driver.ConfigureAttackFeedback(context.Id, profile.ArchetypeKey);
        }
        public void Tick(float dt, long tick)
        {
            if (_controller == null || !_state.IsActive) return;
            bool sample = _controller.ShouldProbe(tick);
            SightProbe sight = sample ? _driver.ProbeSight(_player.Position, IsTarget) : default;
            HunterLightObservation light = sample ? _driver.ProbeLight(_state.Flashlight, tick,
                _controller.EffectiveSightRange, _controller.EffectiveSightCone, _profile.SensorIntervalTicks * 2, IsTarget) : default;
            if (sample && !light.Observed && _state.AfterimageRemaining > 0f)
            {
                FlashlightSample trace = _state.Afterimage;
                var refreshed = new FlashlightSample(trace.Source, tick, trace.Enabled, trace.Origin, trace.Direction, trace.Range, trace.ConeDegrees);
                HunterLightObservation observed = _driver.ProbeLight(refreshed, tick, _controller.EffectiveSightRange, _controller.EffectiveSightCone, 0);
                light = new HunterLightObservation(observed.Observed, false, observed.Position, observed.Tick);
            }
            HunterTickResult result = _controller.Tick(sight, light, dt, tick);
            bool reactionValid = (_state.CurrentAction != HunterAction.AvoidLight && _state.CurrentAction != HunterAction.FlankLight) ||
                _driver.ValidateReactionTarget(result.Target);
            if (!reactionValid) _controller.ReportPathFailure();
            _driver.TickAttacks(dt, tick);
            if (result.BeginLunge && _profile.AttackStyle != HunterAttackStyle.Lunge)
                _driver.BeginAttackWarning(_profile.AttackStyle, _state.AttackSerial, _state.AttackTarget, _controller.EffectiveAttackDistance,
                    _profile.AttackStyle == HunterAttackStyle.GroundSpikes ? _controller.SpikeRadius : _controller.ProjectileRadius,
                    _controller.SplitBolt, _controller.ThornRing);
            if (_state.AttackBecameActive && _profile.AttackStyle != HunterAttackStyle.Lunge)
                _driver.FireAttack(_controller.ProjectileSpeed, _controller.ProjectileRadius);
            _driver.Move(result.Target, result.Speed, _profile.Acceleration, _profile.TurnRate, dt,
                !reactionValid || !_state.IsActive || result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery ||
                    (_profile.AttackStyle != HunterAttackStyle.Lunge && result.Phase != HunterLungePhase.None),
                result.ActiveContact && _profile.AttackStyle == HunterAttackStyle.Lunge, result.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (!_driver.PathAvailable && result.Phase == HunterLungePhase.None) _controller.ReportPathFailure();
            _driver.Animate(dt, _controller.AttackSample().Phase, _controller.AttackSample().Progress);
            while (_controller.TryDequeueFeedback(out HunterFeedbackEvent feedback)) OnFeedback?.Invoke(feedback);
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
        private void HandleRangedContact(Collider collider, int serial)
        {
            if (_controller == null) return;
            IEntityHandle handle = collider.GetComponentInParent<IEntityHandle>();
            if (handle != null && _controller.TryAcceptRangedContact(handle.Id, serial, out HunterHit hit)) OnLungeHit?.Invoke(hit);
        }
        private void HandleAttackFeedback(HunterFeedbackEvent feedback) { OnFeedback?.Invoke(feedback); }
        private void HandleRangedMiss(int serial) { _controller?.ReportAttackMiss(serial); }
        public void SetRoomPhase(RoomPhaseChangedFact fact)
        { if (_controller == null) return; _controller.SetRoomPhase(fact); _driver.SetUnavailableRooms(_controller.UnavailableRooms); }
        public void SetTraits(ProgressionTraits traits) { _controller?.SetTraits(traits); }
        public void SetAfterimage(FlashlightSample sample, float lifetime) { _controller?.SetAfterimage(sample, lifetime); }
        public void HearNoise(NoiseEvent noise) { _controller?.HearNoise(noise, _driver.NoiseTransmission(noise.Position)); }
        public void SetFlashlight(FlashlightSample sample) { _controller?.SetFlashlight(sample); }
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
