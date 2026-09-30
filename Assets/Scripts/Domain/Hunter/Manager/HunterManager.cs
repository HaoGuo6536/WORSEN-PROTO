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
//   - Construct per-life archetype rules and acknowledge recording motion before facts.
//   - Route Weaver sweep evidence, web contacts and ceiling commands without Player writes.
// DEPENDENCIES:
//   - Hunter contracts, Core values and injected Player, Level and optional Floor views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned entity; Session is sole tick owner. Subscriptions pair OnEnable/OnDisable.
//   PLAN-014 stall payload stays Hunter-local pending coordinator-owned Core telemetry.
//   Habit/mutation DTOs likewise await Core promotion; no audio or Session routing is owned here.
//   BeginCatch must follow Session damage acceptance, never an unconfirmed contact.
//   A Stalk reveal hold (HoldPosition) uses the motor's stopped input to discard inertia.
//   Weaver facts remain Hunter-local pending coordinator Core promotion and routing.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    [RequireComponent(typeof(HunterDriver))]
    public sealed class HunterManager : MonoBehaviour, IEntityHandle
    {
        [SerializeField] private HunterDriver _driver;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private WeaverController _weaver;
        private WeaverConfig _weaverConfig;
        private HunterProfile _profile;
        private IReadOnlyPlayerState _player;
        private IReadOnlyLevelState _level;
        public HearingModelSettings HearingModel => _profile != null ? _profile.HearingModel : default;
        public bool IsPursuing => _state != null && !_state.PursuitSuppressed &&
            (_state.PlayerVisible || _state.BeliefConfidence > 0f);
        public EntityId Id => _state?.Id ?? EntityId.None;
        public int DuplicateIndex => _state?.DuplicateIndex ?? 0;
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
        public event Action<HunterArchetypeFact> OnArchetypeFact;
        public event Action<WebHitFact> OnWebHit;
        public event Action<WeaverFact> OnWeaverFact;
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
        public void Initialize(HunterProfile profile, EntityContext context, IReadOnlyPlayerState player, IReadOnlyLevelState level, int duplicateIndex = 0)
        {
            if (profile == null || player == null || level == null || context.Random == null || !context.Id.IsValid)
                throw new ArgumentException("Hunter initialization requires profile, identity, shared random and typed state views.");
            if (_driver == null) _driver = GetComponent<HunterDriver>();
            if (duplicateIndex < 0) throw new ArgumentOutOfRangeException(nameof(duplicateIndex));
            IHunterArchetypeController archetype = new Archetypes.Default.DefaultHunterController();
            if (profile.ArchetypeRules is Archetypes.Echo.EchoConfig echo)
                archetype = new Archetypes.Echo.EchoController(echo);
            else if (profile.ArchetypeRules is WeaverConfig weaver)
                archetype = new WeaverController(new WeaverBehaviorState(), weaver, profile, context.Random);
            else if (profile.ArchetypeRules != null) throw new ArgumentException("Unregistered Hunter rules config.");
            _profile = profile; _player = player; _level = level; _driver.Initialize(profile.MotorOverride);
            _weaver = archetype as WeaverController; _weaverConfig = profile.ArchetypeRules as WeaverConfig;
            if (_weaver != null) _driver.ConfigureWeaver(_weaverConfig.DriverConfig);
            _state = new HunterBehaviorState();
            _controller = new HunterController(_state, profile, context.Random, player, level, archetype);
            _controller.Reset(context.Id, _driver.Position, _driver.Forward);
            _state.DuplicateIndex = duplicateIndex;
            _driver.SetTargetFilter(IsTarget);
            _driver.ConfigureAttackFeedback(context.Id, profile.ArchetypeKey);
        }
        public void Tick(float dt, long tick)
        {
            if (_controller == null || !_state.IsActive) return;
            if (_weaver != null)
            {
                if (!(dt > 0f) || float.IsInfinity(dt) || tick <= _weaver.LastTick) return;
                foreach (var contact in _driver.TickWebs(dt))
                {
                    IEntityHandle handle = contact.Key.GetComponentInParent<IEntityHandle>();
                    if (handle != null && _weaver.TryHit(handle.Id, contact.Value, tick, out WebHitFact hit)) OnWebHit?.Invoke(hit);
                }
                _weaver.Observe(_driver.ProbeWeaver(_weaver.Aim(_driver.WeaverShotHeight),
                    _weaver.Warning ? _weaver.WarnedRadius : _weaver.Radius, _weaverConfig.ShotRange, tick));
            }
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
            if (_weaver != null)
            {
                if (result.Phase != HunterLungePhase.None || _state.CatchActive || _state.PursuitSuppressed) _weaver.SuspendAttack();
                _driver.SetWeaverCeiling(_weaver.CeilingHeight, _weaver.Ceiling && result.Phase == HunterLungePhase.None && !_state.CatchActive);
                if (_weaver.Fire) _weaver.CommitLaunch(_driver.LaunchWeb(_weaver.WarnedOrigin, _weaver.WarnedTarget,
                    _weaver.WarnedRadius, _weaverConfig.ProjectileSpeed, _weaverConfig.ShotRange, _weaver.Serial + 1));
            }
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
            if (_controller.ReplayPath != null && result.Phase == HunterLungePhase.None && !_state.CatchActive && _state.IsActive)
            {
                int reached = _driver.MoveRecording(_controller.ReplayPath, dt, out bool unreachable);
                _controller.CommitReplay(reached, unreachable);
            }
            else _driver.Move(result.Target, result.Speed, _controller.EffectiveAcceleration, _controller.EffectiveTurnRate, dt,
                !reactionValid || !_state.IsActive || result.HoldPosition || (_weaver?.Hold ?? false) ||
                    result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery ||
                    (_profile.AttackStyle != HunterAttackStyle.Lunge && result.Phase != HunterLungePhase.None),
                result.ActiveContact && _profile.AttackStyle == HunterAttackStyle.Lunge, result.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            _driver.ApplyDecisionMotion(result.StumbleDisplacement, result.DeliberationFacing);
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (_weaver != null)
            {
                _driver.SetWeaverCeiling(_weaver.CeilingHeight, _weaver.Ceiling && result.Phase == HunterLungePhase.None && !_state.CatchActive);
                while (_weaver.TryTakeWeaverFact(out WeaverFact web))
                { _driver.AddWeaverNest(web); OnWeaverFact?.Invoke(web); }
            }
            _driver.ObserveStall(dt, tick, Id, _state.CurrentAction, _state.LastRoom);
            if (!_driver.PathAvailable && !result.HoldPosition && result.Phase == HunterLungePhase.None) _controller.ReportPathFailure();
            if (!_state.CatchActive) _driver.SetLook(_controller.LookTarget, _controller.LookAtMemory, false);
            _driver.Animate(dt, _controller.AttackSample().Phase, _controller.AttackSample().Progress);
            while (_controller.TryDequeueFeedback(out HunterFeedbackEvent feedback)) OnFeedback?.Invoke(feedback);
            while (_controller.TryTakeHabit(out HunterHabitFact habit)) OnHabit?.Invoke(habit);
            while (_controller.TryTakeArchetypeFact(out HunterArchetypeFact fact)) OnArchetypeFact?.Invoke(fact);
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
            if (_weaver != null) { _weaver.SuspendAttack(); _driver.SetWeaverCeiling(_weaver.CeilingHeight, false); }
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
        public void SetInteractables(IReadOnlyInteractableSet interactables) { _controller?.SetInteractables(interactables); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { _controller?.SetActiveEffects(effects); }
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
            _weaver = null; _weaverConfig = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
