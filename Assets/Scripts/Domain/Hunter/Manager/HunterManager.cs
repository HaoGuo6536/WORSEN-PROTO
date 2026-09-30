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
//   - Own per-life controller, archetype/facet construction and paired driver subscriptions.
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
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Herald;
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
        private Archetypes.Ram.RamController _ram;
        private Archetypes.Skip.SkipController _skip;
        private Archetypes.Mimic.MimicController _mimic;
        private BlinderController _blinder;
        private BlinderConfig _blinderConfig;
        private HeraldController _herald;
        private Archetypes.Mannequin.MannequinController _mannequin;
        private Archetypes.Stare.StareController _stare;
        private Archetypes.Stare.StareConfig _stareConfig;
        private HunterProfile _profile;
        private Archetypes.Ticking.TickingManager _ticking;
        public Archetypes.Ticking.TickingManager Ticking => _ticking;
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
        public bool BeginSkipFloor(long generation) => _skip?.BeginFloor(generation) ?? false;
        public bool RecordSkipUse(SkipTraversalUse use) => _skip?.RecordUse(use) ?? false;
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
            if (_mimic != null) { _mimic.Teardown(); PublishRosterFacts(); }
            if (_ticking != null) _ticking.Teardown();
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
            else if (profile.ArchetypeRules is Archetypes.Ticking.TickingConfig ticking)
                archetype = new Archetypes.Ticking.TickingController(ticking, context.Random);
            else if (profile.ArchetypeRules is Archetypes.Ram.RamConfig ram)
                archetype = new Archetypes.Ram.RamController(ram, profile);
            else if (profile.ArchetypeRules is Archetypes.Skip.SkipConfig skip)
                archetype = new Archetypes.Skip.SkipController(skip, profile, context.Random);
            else if (profile.ArchetypeRules is Archetypes.Mimic.MimicConfig mimic)
                archetype = new Archetypes.Mimic.MimicController(mimic, context.Random);
            else if (profile.ArchetypeRules is BlinderConfig blinder)
                archetype = new BlinderController(new BlinderBehaviorState(), blinder, profile);
            else if (profile.ArchetypeRules is HeraldConfig herald)
                archetype = new HeraldController(new HeraldBehaviorState(), herald, context.Random);
            else if (profile.ArchetypeRules is Archetypes.Mannequin.MannequinConfig mannequin)
                archetype = new Archetypes.Mannequin.MannequinController(mannequin, context.Random);
            else if (profile.ArchetypeRules is Archetypes.Stare.StareConfig stare)
                archetype = new Archetypes.Stare.StareController(stare, context.Random);
            else if (profile.ArchetypeRules != null) throw new ArgumentException("Unregistered Hunter rules config.");
            if (_mimic != null) { _mimic.Teardown(); PublishRosterFacts(); }
            _ram = archetype as Archetypes.Ram.RamController;
            _skip = archetype as Archetypes.Skip.SkipController;
            _mimic = archetype as Archetypes.Mimic.MimicController;
            _profile = profile; _player = player; _level = level; _driver.Initialize(profile.MotorOverride);
            _weaver = archetype as WeaverController; _weaverConfig = profile.ArchetypeRules as WeaverConfig;
            _mannequin = archetype as Archetypes.Mannequin.MannequinController;
            _stare = archetype as Archetypes.Stare.StareController; _stareConfig = profile.ArchetypeRules as Archetypes.Stare.StareConfig;
            if (_stare != null) _driver.ConfigureStare();
            if (_weaver != null) _driver.ConfigureWeaver(_weaverConfig.DriverConfig);
            _blinder = archetype as BlinderController; _blinderConfig = profile.ArchetypeRules as BlinderConfig;
            _herald = archetype as HeraldController;
            if (_blinder != null)
            {
                if (_blinderConfig.SweepConfig == null) throw new ArgumentException("Build Blinder profile and sweep config first.");
                _driver.ConfigureWeaver(_blinderConfig.SweepConfig);
            }
            _state = new HunterBehaviorState();
            _controller = new HunterController(_state, profile, context.Random, player, level, archetype);
            _controller.Reset(context.Id, _driver.Position, _driver.Forward);
            _state.DuplicateIndex = duplicateIndex;
            _driver.SetTargetFilter(IsTarget);
            _driver.ConfigureAttackFeedback(context.Id, profile.ArchetypeKey);
            if (_ticking != null) _ticking.Teardown();
            if (archetype is Archetypes.Ticking.TickingController clock)
            {
                if (_ticking == null) _ticking = GetComponent<Archetypes.Ticking.TickingManager>();
                if (_ticking == null) _ticking = gameObject.AddComponent<Archetypes.Ticking.TickingManager>();
                _ticking.Initialize(clock, _controller, (Archetypes.Ticking.TickingConfig)profile.ArchetypeRules, _state, player);
            }
        }
        public void Tick(float dt, long tick)
        {
            if (_controller == null || !_state.IsActive || !(dt > 0f) || float.IsInfinity(dt)) return;
            if (_blinder != null)
            {
                if (!(dt > 0f) || float.IsInfinity(dt) || tick <= _blinder.LastTick) return;
                foreach (var contact in _driver.TickWebs(dt))
                {
                    IEntityHandle handle = contact.Key.GetComponentInParent<IEntityHandle>();
                    if (handle != null && _blinder.TryHit(handle.Id, contact.Value, tick, out BlinderHitFact hit)) OnBlinderHit?.Invoke(hit);
                }
                _blinder.Observe(_driver.ProbeWeaver(_blinder.Aim(_driver.WeaverShotHeight), _blinder.Radius, _blinderConfig.Range, tick));
            }
            if (_herald != null && (!(dt > 0f) || float.IsInfinity(dt) || tick <= _herald.LastTick)) return;
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
            if (_ticking != null) _ticking.PrepareTick();
            bool sample = _controller.NeedsViewObservation || _controller.ShouldProbe(tick);
            if (_controller.NeedsViewObservation)
                _controller.ObservePlayerView(_driver.PlayerViewClear(_state.PlayerView, _state.Position,
                    _stareConfig != null ? _stareConfig.ObservationHeight : ((Archetypes.Mannequin.MannequinConfig)_profile.ArchetypeRules).ObservationHeight));
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
            if (_blinder != null)
            {
                if (result.Phase != HunterLungePhase.None || _state.CatchActive || _state.PursuitSuppressed) _blinder.SuspendAttack();
                if (_blinder.Fire) _blinder.CommitLaunch(_driver.LaunchWeb(_blinder.Origin, _blinder.Target,
                    _blinder.Radius, _blinderConfig.ProjectileSpeed, _blinderConfig.Range, _blinder.Serial + 1));
            }
            _herald?.ResolveAfterSensing();
            if ((_state.WorldView?.JammedDoors?.Count > 0 || _state.BreakingDoor != 0) && !_controller.ArchetypeHeld && (!result.HoldPosition || _state.BreakingDoor != 0) &&
                _controller.BlockJammedPath(_controller.ReactionPath(result, _driver.ProbeReactionPath(result.Target)), dt, result.Target)) result = _controller.HoldMotion();
            while (_controller.TryTakeDoorBreak(out HunterDoorBreakFact broken)) OnDoorBreakCompleted?.Invoke(broken);
            if (_stare != null)
            {
                if (!_controller.ReactionHeld && _stare.NeedsPlacement)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        bool valid = _driver.ProbeStare(_stare.Candidate(attempt), _player.Position, _state.PlayerView, out Vector3 point);
                        if (!_stare.AcceptPlacement(point, valid, valid && _driver.PlayerViewClear(_state.PlayerView, point, _stareConfig.ObservationHeight))) continue;
                        _driver.PlaceStare(point); placed = true; break;
                    }
                    if (!placed) _stare.PlacementFailed();
                }
                _driver.SetStarePresent(_stare.Present);
                if (result.BeginLunge) _stare.AttackCue();
            }
            if (_weaver != null)
            {
                if (result.HoldPosition || result.Phase != HunterLungePhase.None || _state.CatchActive || _state.PursuitSuppressed) _weaver.SuspendAttack();
                _driver.SetWeaverCeiling(_weaver.CeilingHeight, _weaver.Ceiling && result.Phase == HunterLungePhase.None && !_state.CatchActive);
                if (_weaver.Fire) _weaver.CommitLaunch(_driver.LaunchWeb(_weaver.WarnedOrigin, _weaver.WarnedTarget,
                    _weaver.WarnedRadius, _weaverConfig.ProjectileSpeed, _weaverConfig.ShotRange, _weaver.Serial + 1));
            }
            if (_ticking != null) _ticking.PublishTick();
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
            else if (_ram != null && _ram.OwnsPursuit && !_state.CatchActive && _state.IsActive)
            {
                bool blocked = _driver.MoveCharge(_ram.Motion, _ram.Direction, dt, out Collider blocker);
                IRamBreakableHandle partition = blocker != null ? blocker.GetComponentInParent<IRamBreakableHandle>() : null;
                _ram.CommitMotion(_driver.Position, blocked, partition?.RamBreakableId ?? -1);
            }
            else if (_skip != null && _skip.TryTeleport(out Vector3 arrival))
                _skip.CommitTeleport(_driver.TryTeleport(arrival), arrival);
            else if (_controller.ReplayPath != null && !result.HoldPosition && result.Phase == HunterLungePhase.None && !_state.CatchActive && _state.IsActive)
            {
                int reached = _driver.MoveRecording(_controller.ReplayPath, dt, out bool unreachable);
                _controller.CommitReplay(reached, unreachable);
            }
            else _driver.Move(result.Target, result.Speed, _controller.EffectiveAcceleration, _controller.EffectiveTurnRate, dt,
                !reactionValid || !_state.IsActive || result.HoldPosition || (_weaver?.Hold ?? false) ||
                    (_blinder?.Hold ?? false) || (_herald?.Hold ?? false) ||
                    result.Phase == HunterLungePhase.Windup || result.Phase == HunterLungePhase.Recovery ||
                    (_profile.AttackStyle != HunterAttackStyle.Lunge && result.Phase != HunterLungePhase.None),
                result.ActiveContact && _profile.AttackStyle == HunterAttackStyle.Lunge, result.LungeDirection, _controller.LungeSpeed, _controller.EffectiveAttackDistance);
            if (!(_ram?.OwnsPursuit ?? false)) _driver.ApplyDecisionMotion(result.StumbleDisplacement, result.DeliberationFacing);
            _controller.CommitPose(_driver.Position, _driver.Velocity, _driver.Forward);
            if (!_state.CatchActive) _driver.ProbeBodyContact();
            if (_mimic != null && _mimic.Posed && !_state.CatchActive) _driver.ProbeMimicTouch(_mimic.TouchRadius);
            PublishRosterFacts();
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
            PublishBlinderFacts();
            if (_herald != null)
            {
                while (_herald.TakeScream(out HeraldScreamFact scream)) OnHeraldScream?.Invoke(scream);
                while (_herald.TakeBreath(out HeraldBreathFact breath)) OnHeraldBreath?.Invoke(breath);
                while (_herald.TakeHit(out HeraldDeafenFact hit)) OnHeraldDeafen?.Invoke(hit);
            }
            if (_mannequin != null) while (_mannequin.TakeFact(out MannequinFact mannequin)) OnMannequinFact?.Invoke(mannequin);
            if (_stare != null) while (_stare.TakeFact(out StareFact stare)) OnStareFact?.Invoke(stare);
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
            if (_ram != null)
            { if (_ram.TryHit(handle.Id, normal, out HunterHit charge)) OnLungeHit?.Invoke(charge);
                else OnBodyContact?.Invoke(Id, handle.Id, normal); return; }
            if (_mimic != null)
            {
                if (_mimic.Touch(handle.Id, out HunterHit bite))
                    OnLungeHit?.Invoke(new HunterHit(bite.Hunter, bite.Target, bite.Damage, bite.Tick, bite.HunterPosition,
                        bite.Reason, bite.Severity, bite.Source, normal));
                else OnBodyContact?.Invoke(Id, handle.Id, normal);
                PublishRosterFacts(); return;
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
        private void PublishRosterFacts()
        {
            if (_ram != null) while (_ram.TakeFact(out RamFact fact)) OnRamFact?.Invoke(fact);
            if (_skip != null) while (_skip.TakeFact(out SkipFact fact)) OnSkipFact?.Invoke(fact);
            if (_mimic != null) while (_mimic.TakeFact(out MimicFact fact)) OnMimicFact?.Invoke(fact);
        }
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
            _ram?.Won(); _mimic?.Won(); PublishRosterFacts();
            _controller.SetCatchActive(true); _driver.SetLook(playerPosition, true, true);
            if (_mannequin != null && _mannequin.TryCatch()) OnMannequinCatch?.Invoke(Id, _state.Position, _state.Tick);
            _blinder?.BeginCatch(); PublishBlinderFacts(); _herald?.SuspendAttack();
            if (_stare != null)
            { _stare.CatchCue(); while (_stare.TakeFact(out StareFact fact)) OnStareFact?.Invoke(fact); }
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
        public void HearNoise(NoiseEvent noise) { if (HunterHearingUtility.Allows(noise)) _controller?.HearNoise(noise, 1f); }
        public bool HearFloorWideNoise(NoiseEvent noise) => HunterHearingUtility.Allows(noise) &&
            (_controller?.HearNoise(noise, 1f, floorWide: true) ?? false);
        public void ClearBelief() { _controller?.ClearBelief(); }
        public void SetFloorView(IReadOnlyFloorState floor) { _controller?.SetFloorView(floor); }
        public void SetClosedDoors(System.Collections.Generic.IReadOnlyDictionary<int, bool> doors) { _controller?.SetClosedDoors(doors); }
        public void SetInteractables(IReadOnlyInteractableSet interactables) { _controller?.SetInteractables(interactables); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects)
        { _controller?.SetActiveEffects(effects); _blinder?.SetEffects(effects); _mannequin?.SetEffects(effects); }
        public float BeginAfterglow(int roomId) => _mannequin?.BeginAfterglow(roomId) ?? 0f;
        public bool RequestRetreat() => _controller != null &&
            _controller.RequestRetreat(_driver.ProbeOccludedRooms(_level.Graph, _player.Position));
        public void ReceiveRegionHint(HintPayload hint, int roomId) { _controller?.ReceiveRegionHint(hint, roomId); }
        public void SetFlashlight(FlashlightSample sample) { _controller?.SetFlashlight(sample); }
        public void ApplyRunSpeedMultiplier(float multiplier) { _controller?.ApplyRunSpeedMultiplier(multiplier); }
        public void ReceiveHint(HintPayload hint) { _controller?.ReceiveHint(hint); }
        public void ReportBlinderTrapTick(Vector3 position, long tick)
        { _blinder?.ReportTrapTick(position, tick); PublishBlinderFacts(); }
        public bool TryGetBlinderTrapPolicy(out BlinderTrapPolicyFact fact)
        { fact = _blinder != null ? _blinder.TrapPolicy : default; return _blinder != null; }
        private void PublishBlinderFacts()
        {
            if (_blinder == null) return;
            while (_blinder.TakeSound(out BlinderSoundFact sound)) OnBlinderSound?.Invoke(sound);
            while (_blinder.TakeTrapPolicy(out BlinderTrapPolicyFact policy)) OnBlinderTrapPolicy?.Invoke(policy);
            while (_blinder.TakeThrow(out BlinderThrowFact shot)) OnBlinderThrow?.Invoke(shot);
        }
        public void Teardown()
        {
            if (_mimic != null) { _mimic.Teardown(); PublishRosterFacts(); }
            if (_ticking != null) _ticking.Teardown();
            if (_driver != null) _driver.Teardown();
            HunterRegistry.Unregister(this);
            _controller = null; _state = null; _profile = null; _player = null; _level = null;
            _weaver = null; _weaverConfig = null;
            _ram = null; _skip = null; _mimic = null;
            _blinder = null; _blinderConfig = null; _herald = null;
            _mannequin = null; _stare = null; _stareConfig = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
