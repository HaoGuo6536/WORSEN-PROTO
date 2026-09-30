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
//   - Relay Hunter-local stall facts for evidence consumers without recovery commands.
//   - Publish deliberation facts and route collision-limited stumble/facing commands.
//   - Publish habit/mutation facts and route explicit accepted-catch and chase inputs.
// DEPENDENCIES:
//   - Hunter contracts, Core values and injected Player, Level and optional Floor views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned entity; Session is sole tick owner. Subscriptions pair OnEnable/OnDisable.
//   PLAN-014 stall payload stays Hunter-local pending coordinator-owned Core telemetry.
//   Habit/mutation DTOs likewise await Core promotion; no audio or Session routing is owned here.
//   BeginCatch must follow Session damage acceptance, never an unconfirmed contact.
//   A Stalk reveal hold (HoldPosition) uses the motor's stopped input to discard inertia.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
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
        private IReadOnlyLevelState _level;
        public HearingModelSettings HearingModel => _profile != null ? _profile.HearingModel : default;
        public bool IsPursuing => _state != null && !_state.PursuitSuppressed &&
            (_state.PlayerVisible || _state.BeliefConfidence > 0f);
        public EntityId Id => _state?.Id ?? EntityId.None;
        public IReadOnlyHunterState ReadOnlyState => _state;
        public string ArchetypeKey => _profile != null ? _profile.ArchetypeKey : string.Empty;
        public HunterAttackSample AttackSample => _profile != null && _profile.AttackStyle == HunterAttackStyle.Lunge ?
            _controller?.AttackSample() ?? default : default;
        public event Action<HunterHit> OnLungeHit;
        public event Action<HunterSighting> OnSighting;
        public event Action<HunterFeedbackEvent> OnFeedback;
        public event Action<HunterStallFact> OnStall;
        public event Action<EntityId, Vector3, long> OnDeliberation;
        public event Action<HunterHabitFact> OnHabit;
        public event Action<HunterMutationFact> OnMutation;
        private void Awake() { if (_driver == null) _driver = GetComponent<HunterDriver>(); }
        private void OnEnable()
        {
            if (_driver == null) _driver = GetComponent<HunterDriver>();
            _driver.OnLungeContact += HandleContact;
            _driver.OnRangedContact += HandleRangedContact;
            _driver.OnRangedMiss += HandleRangedMiss;
            _driver.OnAttackFeedback += HandleAttackFeedback;
            _driver.OnStall += HandleStall;
        }
        private void OnDisable()
        {
            if (_driver != null)
            {
                _driver.OnLungeContact -= HandleContact;
                _driver.OnRangedContact -= HandleRangedContact;
                _driver.OnRangedMiss -= HandleRangedMiss;
                _driver.OnAttackFeedback -= HandleAttackFeedback;
                _driver.OnStall -= HandleStall;
            }
            HunterRegistry.Unregister(this);
        }
        public void Initialize(HunterProfile profile, EntityContext context, IReadOnlyPlayerState player, IReadOnlyLevelState level)
        {
            if (profile == null || player == null || level == null || context.Random == null || !context.Id.IsValid)
                throw new ArgumentException("Hunter initialization requires profile, identity, shared random and typed state views.");
            if (_driver == null) _driver = GetComponent<HunterDriver>();
            _profile = profile; _player = player; _level = level; _driver.Initialize();
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
            _driver.SetEmergence(_controller.PreferEmergence, _state.LastKnownPosition, _profile.EmergenceWaypointBudget);
            _driver.Move(result.Target, result.Speed, _controller.EffectiveAcceleration, _controller.EffectiveTurnRate, dt,
                !reactionValid || !_state.IsActive || result.HoldPosition ||
                    result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery ||
                    (_profile.AttackStyle != HunterAttackStyle.Lunge && result.Phase != HunterLungePhase.None),
                result.ActiveContact && _profile.AttackStyle == HunterAttackStyle.Lunge, result.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            _driver.ApplyDecisionMotion(result.StumbleDisplacement, result.DeliberationFacing);
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            _driver.ObserveStall(dt, tick, Id, _state.CurrentAction, _state.LastRoom);
            if (!_driver.PathAvailable && !result.HoldPosition && result.Phase == HunterLungePhase.None) _controller.ReportPathFailure();
            if (!_state.CatchActive) _driver.SetLook(_controller.LookTarget, _controller.LookAtMemory, false);
            _driver.Animate(dt, _controller.AttackSample().Phase, _controller.AttackSample().Progress);
            while (_controller.TryDequeueFeedback(out HunterFeedbackEvent feedback)) OnFeedback?.Invoke(feedback);
            while (_controller.TryTakeHabit(out HunterHabitFact habit)) OnHabit?.Invoke(habit);
            if (_controller.TryTakeDeliberation(out Vector3 candidate)) OnDeliberation?.Invoke(Id, candidate, tick);
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
        private void HandleStall(HunterStallFact fact) { OnStall?.Invoke(fact); }
        private void HandleRangedMiss(int serial) { _controller?.ReportAttackMiss(serial); }
        public void SetRoomPhase(RoomPhaseChangedFact fact)
        { if (_controller == null) return; _controller.SetRoomPhase(fact); _driver.SetUnavailableRooms(_controller.UnavailableRooms); }
        public void SetTraits(ProgressionTraits traits) { _controller?.SetTraits(traits); }
        public bool ApplyMutation(HunterMutation mutation)
        {
            if (_controller == null || !_controller.ApplyMutation(mutation, out HunterMutationFact fact)) return false;
            OnMutation?.Invoke(fact); return true;
        }
        public void SetChaseActive(bool active) { _controller?.SetChaseActive(active); }
        public void BeginCatch(Vector3 playerPosition)
        {
            if (_controller == null) return;
            _controller.SetCatchActive(true); _driver.SetLook(playerPosition, true, true);
        }
        public void TickCatch(float dt, Vector3 playerPosition)
        {
            if (_controller == null || !_state.CatchActive) return;
            _driver.SetLook(playerPosition, true, true);
            _driver.Animate(dt, _controller.AttackSample().Phase, _controller.AttackSample().Progress);
        }
        public void EndCatch()
        {
            if (_controller == null) return;
            _controller.SetCatchActive(false); _driver.SetLook(_controller.LookTarget, false, false);
        }
        public void SetAfterimage(FlashlightSample sample, float lifetime) { _controller?.SetAfterimage(sample, lifetime); }
        public void HearNoise(NoiseEvent noise) { _controller?.HearNoise(noise, 1f); }
        public void SetFloorView(IReadOnlyFloorState floor) { _controller?.SetFloorView(floor); }
        public void SetClosedDoors(System.Collections.Generic.IReadOnlyDictionary<int, bool> doors) { _controller?.SetClosedDoors(doors); }
        public bool RequestRetreat() => _controller != null &&
            _controller.RequestRetreat(_driver.ProbeOccludedRooms(_level.Graph, _player.Position));
        public void ReceiveRegionHint(HintPayload hint, int roomId) { _controller?.ReceiveRegionHint(hint, roomId); }
        public void SetFlashlight(FlashlightSample sample) { _controller?.SetFlashlight(sample); }
        public void ApplyRunSpeedMultiplier(float multiplier) { _controller?.ApplyRunSpeedMultiplier(multiplier); }
        public void ReceiveHint(HintPayload hint) { _controller?.ReceiveHint(hint); }
        public void Teardown()
        {
            if (_driver != null) _driver.Teardown();
            HunterRegistry.Unregister(this);
            _controller = null; _state = null; _profile = null; _player = null; _level = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
