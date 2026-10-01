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
//   - Own per-life controllers, factory-created module and paired driver subscriptions.
//   - Sequence sensing, navigation, committed motion and presentation commands.
//   - Gate shared and specialised contacts on Player revival protection before acceptance.
//   - Route accepted catch/chase (including Mannequin snap), reactions, effects and world inputs.
//   - Publish archetype, attack, habit, mutation and navigation evidence facts.
// DEPENDENCIES:
//   - Hunter contracts, Core values and injected Player, Level and optional Floor views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned entity; Session is sole tick owner. Subscriptions pair OnEnable/OnDisable.
//   PLAN-014 stall payload stays Hunter-local pending coordinator-owned Core telemetry.
//   Habit/mutation and archetype DTOs are Core values; Session owns their outward routing.
//   BeginCatch must follow Session damage acceptance, never an unconfirmed contact.
//   A Stalk reveal hold (HoldPosition) uses the motor's stopped input to discard inertia.
//   Mutation restoration uses announce=false; only newly accepted mutations publish tells.
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
    public sealed class HunterManager : MonoBehaviour, IEntityHandle, IHunterModuleEvents
    {
        [SerializeField] private HunterDriver _driver;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private IHunterArchetypeModule _module;
        private HunterProfile _profile;
        public IHunterTickingModule Ticking => _module?.Ticking;
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
        public event Action<RamFact> OnRamFact;
        public event Action<SkipFact> OnSkipFact;
        public event Action<MimicFact> OnMimicFact;
        public event Action<EntityId, EntityId, Vector3> OnBodyContact;
        public bool BeginSkipFloor(long generation) => _module?.BeginFloor(generation) ?? false;
        public bool RecordSkipUse(SkipTraversalUse use) => _module?.RecordUse(use) ?? false;
        public event Action<BlinderHitFact> OnBlinderHit;
        public event Action<BlinderThrowFact> OnBlinderThrow;
        public event Action<BlinderSoundFact> OnBlinderSound;
        public event Action<BlinderTrapPolicyFact> OnBlinderTrapPolicy;
        public event Action<HeraldScreamFact> OnHeraldScream;
        public event Action<HeraldBreathFact> OnHeraldBreath;
        public event Action<HeraldDeafenFact> OnHeraldDeafen;
        public event Action<HunterDoorBreakFact> OnDoorBreakCompleted;
        public event Action<MannequinFact> OnMannequinFact;
        // Accepted catch fact: Audio substitutes snap/crunch for the shared death sting.
        public event Action<EntityId, Vector3, long> OnMannequinCatch;
        public event Action<StareFact> OnStareFact;
        public void ApplyStun(float seconds, float strength)
        { _controller?.ApplyStun(seconds, strength); if (_state != null && _state.StunRemaining > 0f) _driver.RemoveMomentum(); }
        public void ApplySlip(float seconds)
        { _controller?.ApplySlip(seconds); if (_state != null && _state.SlipRemaining > 0f) _driver.RemoveMomentum(); }
        public void SetWickActive(bool active) { _controller?.SetWickActive(active); }
        public void SetWorldView(IReadOnlyHunterWorldView world) { _controller?.SetWorldView(world); }
        public void SetPlayerView(HunterPlayerView view) { _controller?.SetPlayerView(view); }
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
            _module?.TeardownModule();
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
            HunterArchetypeFactory factory = HunterArchetypeFactory.BuiltIn;
            IHunterArchetypeController archetype = factory.CreateRules(profile, context.Random);
            _module?.TeardownModule();
            _profile = profile; _player = player; _level = level; _driver.Initialize(profile.MotorOverride);
            _state = new HunterBehaviorState();
            _controller = new HunterController(_state, profile, context.Random, player, level, archetype);
            _controller.Reset(context.Id, _driver.Position, _driver.Forward);
            _state.DuplicateIndex = duplicateIndex;
            _driver.SetTargetFilter(IsTarget);
            _driver.ConfigureAttackFeedback(context.Id, profile.ArchetypeKey);
            _module = factory.CreateModule(gameObject, profile.ArchetypeRules);
            _module.InitializeModule(archetype, profile, _driver, _controller, _state, player, this);
        }
        public void Tick(float dt, long tick)
        {
            if (_controller == null || !_state.IsActive || !(dt > 0f) || float.IsInfinity(dt)) return;
            if (_module != null && !_module.PrepareTick(dt, tick)) return;
            bool sample = _controller.NeedsViewObservation || _controller.ShouldProbe(tick);
            if (_controller.NeedsViewObservation)
                _controller.ObservePlayerView(_driver.PlayerViewClear(_state.PlayerView, _state.Position,
                    _module.ObservationHeight));
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
            _module?.AfterSensing(result);
            if ((_state.WorldView?.JammedDoors?.Count > 0 || _state.BreakingDoor != 0) && !_controller.ArchetypeHeld && (!result.HoldPosition || _state.BreakingDoor != 0) &&
                _controller.BlockJammedPath(_controller.ReactionPath(result, _driver.ProbeReactionPath(result.Target)), dt, result.Target)) result = _controller.HoldMotion();
            while (_controller.TryTakeDoorBreak(out HunterDoorBreakFact broken)) OnDoorBreakCompleted?.Invoke(broken);
            _module?.AfterReaction(result);
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
            if (_controller.ReactionHeld || _controller.ArchetypeHeld || _state.BreakingDoor != 0) _driver.RemoveMomentum();
            else if (_module?.Move(dt) ?? false) { }
            else if (_controller.ReplayPath != null && !result.HoldPosition && result.Phase == HunterLungePhase.None && !_state.CatchActive && _state.IsActive)
            {
                int reached = _driver.MoveRecording(_controller.ReplayPath, dt, out bool unreachable);
                _controller.CommitReplay(reached, unreachable);
            }
            else _driver.Move(result.Target, result.Speed, _controller.EffectiveAcceleration, _controller.EffectiveTurnRate, dt,
                !reactionValid || !_state.IsActive || result.HoldPosition || (_module?.Hold ?? false) ||
                    result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery ||
                    (_profile.AttackStyle != HunterAttackStyle.Lunge && result.Phase != HunterLungePhase.None),
                result.ActiveContact && _profile.AttackStyle == HunterAttackStyle.Lunge, result.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            if (!(_module?.OwnsDecisionMotion ?? false)) _driver.ApplyDecisionMotion(result.StumbleDisplacement, result.DeliberationFacing);
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (!_state.CatchActive) _driver.ProbeBodyContact();
            _module?.AfterPose(result);
            _driver.ObserveStall(dt, tick, Id, _state.CurrentAction, _state.LastRoom);
            if (!_driver.PathAvailable && !result.HoldPosition && result.Phase == HunterLungePhase.None) _controller.ReportPathFailure();
            if (!_state.CatchActive) _driver.SetLook(_controller.LookTarget, _controller.LookAtMemory, false);
            _driver.Animate(dt, _controller.AttackSample().Phase, _controller.AttackSample().Progress);
            while (_controller.TryDequeueFeedback(out HunterFeedbackEvent feedback)) OnFeedback?.Invoke(feedback);
            while (_controller.TryTakeHabit(out HunterHabitFact habit)) OnHabit?.Invoke(habit);
            while (_controller.TryTakeArchetypeFact(out HunterArchetypeFact fact)) OnArchetypeFact?.Invoke(fact);
            _module?.FinishTick();
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
            if (_controller == null || _controller.PlayerRevivalProtected) return;
            IEntityHandle handle = collider.GetComponentInParent<IEntityHandle>();
            if (handle == null) return;
            Vector3 normal = _driver.ContactNormal(collider);
            if (_module?.HandlesContact ?? false)
            {
                if (_module.TryContact(handle.Id, normal, out HunterHit specialised)) OnLungeHit?.Invoke(specialised);
                else OnBodyContact?.Invoke(Id, handle.Id, normal);
                _module.AfterContact(); return;
            }
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (_controller.TryAcceptContact(handle.Id, out HunterHit hit))
                OnLungeHit?.Invoke(new HunterHit(hit.Hunter, hit.Target, hit.Damage, hit.Tick, hit.HunterPosition,
                    hit.Reason, hit.Severity, hit.Source, normal));
            else OnBodyContact?.Invoke(Id, handle.Id, normal);
        }
        private void HandleRangedContact(Collider collider, int serial)
        {
            if (_controller == null) return;
            IEntityHandle handle = collider.GetComponentInParent<IEntityHandle>();
            if (handle != null && _controller.TryAcceptRangedContact(handle.Id, serial, out HunterHit hit)) OnLungeHit?.Invoke(hit);
        }
        private void HandleAttackFeedback(HunterFeedbackEvent feedback) { if (!(_controller?.Silent ?? false)) OnFeedback?.Invoke(feedback); }

        private void HandleStall(HunterStallFact fact) { OnStall?.Invoke(fact); }
        private void HandleRangedMiss(int serial) { _controller?.ReportAttackMiss(serial); }
        public void SetRoomPhase(RoomPhaseChangedFact fact)
        { if (_controller == null) return; _controller.SetRoomPhase(fact); _driver.SetUnavailableRooms(_controller.UnavailableRooms); }
        public void SetTraits(ProgressionTraits traits) { _controller?.SetTraits(traits); }
        public bool ApplyMutation(HunterMutation mutation, bool announce = true)
        {
            if (_controller == null || !_controller.ApplyMutation(mutation, out HunterMutationFact fact)) return false;
            if (announce) OnMutation?.Invoke(fact); return true;
        }
        public bool HasMutation(HunterMutation mutation) => _controller != null &&
            Enum.IsDefined(typeof(HunterTunable), mutation.Tunable) && _controller.Effective(mutation.Tunable) == mutation.Value;
        public void SetChaseActive(bool active) { _controller?.SetChaseActive(active); }
        public void BeginCatch(Vector3 playerPosition)
        {
            if (_controller == null || _state.CatchActive) return;
            _module?.BeforeCatch();
            _controller.SetCatchActive(true); _driver.SetLook(playerPosition, true, true);
            _module?.BeginCatch();
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
        public void HearNoise(NoiseEvent noise) { if (HunterHearingUtility.Allows(noise)) _controller?.HearNoise(noise, 1f); }
        public bool HearFloorWideNoise(NoiseEvent noise) => HunterHearingUtility.Allows(noise) &&
            (_controller?.HearNoise(noise, 1f, floorWide: true) ?? false);
        public void ClearBelief() { _controller?.ClearBelief(); }
        public void SetFloorView(IReadOnlyFloorState floor) { _controller?.SetFloorView(floor); }
        public void SetClosedDoors(System.Collections.Generic.IReadOnlyDictionary<int, bool> doors) { _controller?.SetClosedDoors(doors); }
        public void SetInteractables(IReadOnlyInteractableSet interactables) { _controller?.SetInteractables(interactables); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects)
        { _controller?.SetActiveEffects(effects); _module?.SetEffects(effects); }
        public float BeginAfterglow(int roomId) => _module?.BeginAfterglow(roomId) ?? 0f;
        public bool RequestRetreat() => _controller != null &&
            _controller.RequestRetreat(_driver.ProbeOccludedRooms(_level.Graph, _player.Position));
        public void ReceiveRegionHint(HintPayload hint, int roomId) { _controller?.ReceiveRegionHint(hint, roomId); }
        public void SetFlashlight(FlashlightSample sample) { _controller?.SetFlashlight(sample); }
        public void ApplyRunSpeedMultiplier(float multiplier) { _controller?.ApplyRunSpeedMultiplier(multiplier); }
        public void ReceiveHint(HintPayload hint) { _controller?.ReceiveHint(hint); }
        public void ReportBlinderTrapTick(Vector3 position, long tick)
        { _module?.ReportTrapTick(position, tick); }
        public bool TryGetBlinderTrapPolicy(out BlinderTrapPolicyFact fact)
        { fact = default; return _module != null && _module.TryGetTrapPolicy(out fact); }
        void IHunterModuleEvents.Publish(WebHitFact fact) => OnWebHit?.Invoke(fact);
        void IHunterModuleEvents.Publish(WeaverFact fact) => OnWeaverFact?.Invoke(fact);
        void IHunterModuleEvents.Publish(RamFact fact) => OnRamFact?.Invoke(fact);
        void IHunterModuleEvents.Publish(SkipFact fact) => OnSkipFact?.Invoke(fact);
        void IHunterModuleEvents.Publish(MimicFact fact) => OnMimicFact?.Invoke(fact);
        void IHunterModuleEvents.Publish(BlinderHitFact fact) => OnBlinderHit?.Invoke(fact);
        void IHunterModuleEvents.Publish(BlinderThrowFact fact) => OnBlinderThrow?.Invoke(fact);
        void IHunterModuleEvents.Publish(BlinderSoundFact fact) => OnBlinderSound?.Invoke(fact);
        void IHunterModuleEvents.Publish(BlinderTrapPolicyFact fact) => OnBlinderTrapPolicy?.Invoke(fact);
        void IHunterModuleEvents.Publish(HeraldScreamFact fact) => OnHeraldScream?.Invoke(fact);
        void IHunterModuleEvents.Publish(HeraldBreathFact fact) => OnHeraldBreath?.Invoke(fact);
        void IHunterModuleEvents.Publish(HeraldDeafenFact fact) => OnHeraldDeafen?.Invoke(fact);
        void IHunterModuleEvents.Publish(MannequinFact fact) => OnMannequinFact?.Invoke(fact);
        void IHunterModuleEvents.Publish(StareFact fact) => OnStareFact?.Invoke(fact);
        void IHunterModuleEvents.PublishMannequinCatch(EntityId hunter, Vector3 position, long tick) => OnMannequinCatch?.Invoke(hunter, position, tick);
        public void Teardown()
        {
            _module?.TeardownModule();
            if (_driver != null) _driver.Teardown();
            HunterRegistry.Unregister(this);
            _controller = null; _state = null; _profile = null; _player = null; _level = null;
            _module = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
