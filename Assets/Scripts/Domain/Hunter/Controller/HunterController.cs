// ============================================================================
// HunterController.cs
// ============================================================================
// PURPOSE:
//   Decides Hunter behavior from observable sight, light, noises and memory.
//   Pure rules select goals and committed attack phases while preserving distinct
//   archetype curses, reachable melee elevation, occasional attack vocals and bounded light reactions.
// ARCHITECTURAL ROLE:
//   Controller (section 2) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Admit only this archetype's curse bits while retaining general run traits.
//   - Walk to uncertain clues and stalk fresh beliefs, holding when the player looks toward the hunter.
//   - Weigh competing goals, remember pickups/search order, deliberate and withdraw on request.
//   - Emit gated habit facts and validate run-long single-rule overrides without editing assets.
//   - Bound prediction by observed motion and sight; budget walking travel separately from search.
//   - Consult injected archetype rules without branching on specialised archetype names.
//   - Honor optional dormancy before planning and contact acceptance, cancelling stale attacks.
// DEPENDENCIES:
//   - Hunter state, profile, action definitions and pure GOAP planner; Core event values.
//   - Injected Player, Level and optional Floor views supply observable clues and topology.
//   - Core AcousticOcclusionUtility supplies hearing; closed-door state is injected separately.
// USAGE NOTES:
//   Time and randomness are injected. Default rules never use hidden player position
//   as a clue; recording archetypes explicitly own their alternative perception rule.
//   Stalk uses player pose only for the reveal gate, never to update its belief target.
//   The Player view has no camera direction: use planar heading plus LookBack's 180
//   degrees, without pitch, head scan or an occlusion query. Manager must forward
//   HoldPosition to the motor's stopped input to discard existing movement inertia.
//   Last pickup is inferred by diffing ActiveCakeAnchors ids each tick. Simultaneous
//   removals use lowest id; collapse removals cannot be distinguished from pickups.
//   Missing Floor disables collection goals; missing topology suppresses hearing.
//   Retreat requires Driver-observed occluded rooms, never an assumed wall between rooms.
//   Connected-room entry approximates thresholds until Level supplies portal crossings.
//   Reset is a floor reset and retains overrides; Manager.Initialize creates a new run/spawn state.
//   Session supplies authoritative chase/catch state; raw contacts are not accepted catches.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterController
    {
        private readonly HunterBehaviorState _state;
        private readonly HunterProfile _profile;
        private readonly System.Random _random;
        private readonly IReadOnlyPlayerState _player;
        private readonly IReadOnlyLevelState _level;
        private IReadOnlyFloorState _floor;
        private IReadOnlyDictionary<int, bool> _closedDoors;
        private IReadOnlyInteractableSet _interactables;
        private IReadOnlyActiveEffects _effects;
        private readonly IHunterArchetypeController _archetype;
        public bool Dormant => (_archetype as Archetypes.Ticking.IHunterDormancyRules)?.Dormant ?? false;
        public void RefreshDormancy()
        {
            if (!Dormant) return;
            _state.PursuitSuppressed = true; _state.RetreatRemaining = 0f; _state.ChaseActive = false;
            _state.PlayerVisible = false; _state.PlayerHeard = false; _state.HasHint = false;
            _state.BeliefConfidence = _state.BeliefInitialConfidence = 0f;
            _state.LungePhase = HunterLungePhase.None; _state.PhaseSeconds = 0f; _state.AttackBecameActive = false;
            _state.FiredRangedAttacks.Clear(); _state.Feedback.Clear();
            _state.SearchActive = false; _state.SearchRoute.Clear(); _state.PendingNoiseDecision = false;
            _state.DeliberationFactPending = false;
            _state.DeliberationRemaining = 0f; _state.LightReactionRemaining = 0f; _state.LightMemoryRemaining = 0f;
            _state.Action = HunterAction.Patrol; _state.SensorInitialized = false; _state.PlannedFacts = ulong.MaxValue;
        }
        public IReadOnlyList<Vector3> ReplayPath => _archetype.ReplayPath;
        private HunterArchetypeContext ArchetypeContext => new HunterArchetypeContext(_state, _player, _level,
            _floor, _closedDoors, _interactables, _effects, _state.DeltaTime, _state.Tick,
            !_state.CatchActive && _state.LungePhase == HunterLungePhase.None,
            Effective(HunterTunable.ChaseSpeedMultiplier) * _state.RunSpeedMultiplier, _state.UnavailableRooms);
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { _effects = effects; }
        public void SetInteractables(IReadOnlyInteractableSet interactables) { _interactables = interactables; }
        public void CommitReplay(int reachedPoints, bool unreachable = false) { _archetype.CommitReplay(reachedPoints, unreachable); }
        public bool TryTakeArchetypeFact(out HunterArchetypeFact fact) => _archetype.TryTakeFact(out fact);
        public HunterController(HunterBehaviorState state, HunterProfile profile, System.Random random,
            IReadOnlyPlayerState player, IReadOnlyLevelState level, IHunterArchetypeController archetype = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _archetype = archetype ?? new Archetypes.Default.DefaultHunterController();
        }
        public void Reset(EntityId id, Vector3 position, Vector3 forward)
        {
            _state.LossSeconds = Effective(HunterTunable.LossSeconds); _state.LossDistance = Effective(HunterTunable.LossDistance);
            _state.CatchActive = false; _state.ChaseActive = false; _state.LossHabitObserved = false;
            _state.ThresholdPauseRemaining = 0f; _state.HabitFacts.Clear(); _state.CakePositions.Clear();
            _state.PursuitSuppressed = false; _state.CurrentGoal = HunterGoal.LocatePrey;
            _state.CommitmentRemaining = 0f; _state.DeliberationRemaining = 0f; _state.RetreatRemaining = 0f;
            _state.DeliberationFactPending = false; _state.PendingNoiseDecision = false;
            _state.HeardNoises.Clear(); _state.CakeRooms.Clear(); _state.LastPickupRoom = 0;
            _state.SearchRoute.Clear(); _state.SearchActive = false; _state.SearchIndex = 0; _state.SearchLegBudget = 0f;
            _state.ObservedPlayerRoom = 0; _state.PreviousPlayerRoom = 0; _state.ObservedPlayerVelocity = Vector3.zero;
            _state.PredictionRoute.Clear(); _state.PredictionIndex = 0; _state.Predict = false;
            _state.CakeAvailable = false; _state.ExitAvailable = false;
            _state.UnavailableRoomIds.Clear(); _state.UnavailableRooms.Clear();
            _state.Afterimage = default; _state.AfterimageRemaining = 0f; _state.Traits = ProgressionTraits.None;
            _state.FiredRangedAttacks.Clear(); _state.AcceptedRangedAttacks.Clear(); _state.AttackSerial = 0; _state.AttackBecameActive = false;
            _state.StepDistance = 0f; _state.FootstepCooldown = 0f;
            _state.Flashlight = default; _state.LightObserved = false; _state.DirectlyIlluminated = false;
            _state.LastLightPosition = position; _state.LightMemoryRemaining = 0f;
            _state.LightExposure = 0f; _state.LightReactionRemaining = 0f;
            _state.LightReactionCooldown = 0f; _state.ScreamCooldown = 0f; _state.Feedback.Clear();
            _state.Id = id; _state.TargetId = _player.Id; _state.Position = position;
            _state.Forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            _state.Velocity = Vector3.zero; _state.Tick = 0; _state.IsActive = true;
            _state.RunSpeedMultiplier = 1f;
            _state.PlayerVisible = false; _state.PlayerHeard = false; _state.HasHint = false;
            _state.LastKnownPosition = position; _state.LastKnownTick = 0;
            _state.BeliefConfidence = 0f; _state.BeliefInitialConfidence = 0f;
            _state.BeliefReferenceTick = 0; _state.BeliefAgeAtReference = 0f;
            _state.LastNoiseTick = -1; _state.SensorInitialized = false;
            _state.LungePhase = HunterLungePhase.None; _state.PhaseSeconds = 0f;
            _state.LungeHitAccepted = false; _state.LungeDirection = Vector3.zero;
            _state.HasPatrolTarget = false; _state.NavigationTarget = position; _state.SearchSeconds = 0f;
            _state.PlannedFacts = ulong.MaxValue; _state.Action = HunterAction.Patrol;
            _state.ActionFailed = false; _state.ReplanCount = 0; _state.LastRoom = 0;
            _state.LoopDetected = false; _state.RecentRooms.Clear(); _state.DeltaTime = 0f;
            _state.DuplicateIndex = 0;
            _archetype.Reset(ArchetypeContext);
            if (_archetype.NeverLoses) { _state.LossSeconds = float.PositiveInfinity; _state.LossDistance = float.PositiveInfinity; }
            RefreshDormancy();
        }
        public bool ShouldProbe(long tick) => !_state.CatchActive && (!_state.SensorInitialized || tick % Math.Max(1, _profile.SensorIntervalTicks) == 0);
        public HunterTickResult Tick(SightProbe probe, float dt, long tick)
            => Tick(probe, default, dt, tick);
        public HunterTickResult Tick(SightProbe probe, HunterLightObservation light, float dt, long tick)
        {
            if (!(dt > 0f) || float.IsNaN(dt) || float.IsInfinity(dt)) return default;
            _state.AttackBecameActive = false;
            _state.Tick = tick; _state.DeltaTime = dt;
            _archetype.Tick(ArchetypeContext);
            RefreshDormancy();
            UpdateFloorMemory(); // Consume removals even during a catch; never replay them afterward.
            if (_state.CatchActive)
                return new HunterTickResult(_state.Position, 0f, HunterLungePhase.None, Vector3.zero, false, false, true);
            _state.ThresholdPauseRemaining = Mathf.Max(0f, _state.ThresholdPauseRemaining - dt);
            _state.CommitmentRemaining = Mathf.Max(0f, _state.CommitmentRemaining - dt);
            if (_state.IsDeliberating && _state.LungePhase == HunterLungePhase.None)
            {
                _state.DeliberationRemaining = Mathf.Max(0f, _state.DeliberationRemaining - dt);
                if (_state.DeliberationRemaining < 0.000001f) _state.DeliberationRemaining = 0f;
                if (!_state.IsDeliberating) ResolveNoise();
            }
            _state.HeardNoises.RemoveAll(noise => (tick - noise.Tick) * dt > MemoryDuration);
            if (!_player.IsAlive)
            {
                _state.PlayerVisible = false; _state.IsActive = false;
                _state.LungePhase = HunterLungePhase.None; _state.PhaseSeconds = 0f;
                _state.Feedback.Clear(); return default;
            }
            _state.FootstepCooldown = Mathf.Max(0f, _state.FootstepCooldown - dt);
            if (Dormant)
            {
                TrackRooms();
                _archetype.TryMovement(out Vector3 dormantTarget, out float dormantSpeed);
                bool holdDormant = dormantSpeed <= 0f || _state.ThresholdPauseRemaining > 0f;
                _state.NavigationTarget = dormantTarget;
                return new HunterTickResult(dormantTarget, holdDormant ? 0f : dormantSpeed,
                    HunterLungePhase.None, Vector3.zero, false, false, holdDormant);
            }
            _state.ScreamCooldown = Mathf.Max(0f, _state.ScreamCooldown - dt);
            UpdateLightTimers(dt);
            if (_state.PursuitSuppressed)
            {
                _state.RetreatRemaining -= dt;
                bool arrived = Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius;
                if (_state.RetreatRemaining > 0f && !arrived)
                    return new HunterTickResult(_state.NavigationTarget, _profile.InvestigateSpeed * _state.RunSpeedMultiplier,
                        HunterLungePhase.None, Vector3.zero, false, false);
                _state.PursuitSuppressed = false; _state.PlannedFacts = ulong.MaxValue;
                _state.Action = HunterAction.Patrol;
                _state.SensorInitialized = false; _state.HasPatrolTarget = false;
            }
            if (ShouldProbe(tick))
            {
                bool wasVisible = _state.PlayerVisible;
                Sense(probe, dt, tick);
                if (!_archetype.OwnsPursuit) SenseLight(light, tick);
                if (_state.PlayerVisible != wasVisible)
                {
                    _state.Feedback.Enqueue(_state.PlayerVisible ? HunterFeedbackKind.Detected : HunterFeedbackKind.LostTarget);
                    _state.CommitmentRemaining = 0f;
                    if (!_state.PlayerVisible && !_archetype.OwnsPursuit)
                    {
                        _state.SearchRoute.Clear();
                        _state.SearchRoute.AddRange(HunterNavigationUtility.Search(_level.Graph, _state.LastKnownPosition,
                            _state.ObservedPlayerVelocity, _state.PreviousPlayerRoom, _state.ObservedPlayerRoom,
                            _profile.SearchExpansionMeters, _state.UnavailableRoomIds));
                        _state.SearchIndex = 0; _state.SearchSeconds = 0f;
                        // A running target that breaks sight starts the learnable walk/search,
                        // not the faster first-reveal Stalk approach around its next cut.
                        _state.SearchActive = _state.ObservedPlayerVelocity.magnitude > _profile.InvestigateSpeed;
                        BeginLossBeat();
                    }
                    else { _state.SearchActive = false; _state.DeliberationRemaining = 0f; _state.PendingNoiseDecision = false;
                        _state.LossHabitObserved = false; _state.ThresholdPauseRemaining = 0f; }
                }
            }
            if (_state.DirectlyIlluminated) _state.LightExposure += dt;
            else _state.LightExposure = 0f;
            if (_archetype.NeverLoses) _state.BeliefConfidence = _state.BeliefInitialConfidence = 1f;
            else DecayBelief(dt, tick);
            TrackRooms();
            bool begin = false;
            float recoveryBefore = _state.LungePhase == HunterLungePhase.Recovery ? _state.PhaseSeconds : 0f;
            float untilRecovery = _state.LungePhase == HunterLungePhase.Windup ?
                Duration(HunterLungePhase.Windup) - _state.PhaseSeconds + Duration(HunterLungePhase.Active) :
                _state.LungePhase == HunterLungePhase.Active ? Duration(HunterLungePhase.Active) - _state.PhaseSeconds : 0f;
            bool wasAttacking = _state.LungePhase != HunterLungePhase.None;
            if (_state.LungePhase != HunterLungePhase.None)
            {
                _state.PhaseSeconds += dt;
                while (_state.LungePhase != HunterLungePhase.None &&
                    _state.PhaseSeconds + 0.000001f >= Duration(_state.LungePhase))
                {
                    _state.PhaseSeconds = Mathf.Max(0f, _state.PhaseSeconds - Duration(_state.LungePhase));
                    _state.LungePhase = _state.LungePhase == HunterLungePhase.Windup ? HunterLungePhase.Active :
                        _state.LungePhase == HunterLungePhase.Active ? HunterLungePhase.Recovery : HunterLungePhase.None;
                    if (_state.LungePhase == HunterLungePhase.Active)
                    {
                        _state.AttackBecameActive = true;
                        if (_profile.AttackStyle != HunterAttackStyle.Lunge) _state.FiredRangedAttacks.Add(_state.AttackSerial);
                        _state.Feedback.Enqueue(HunterFeedbackKind.AttackSwing);
                    }
                    if (_state.LungePhase == HunterLungePhase.Recovery)
                    {
                        if (_profile.AttackStyle == HunterAttackStyle.Lunge && !_state.LungeHitAccepted) _state.Feedback.Enqueue(HunterFeedbackKind.AttackMiss);
                        _state.Feedback.Enqueue(HunterFeedbackKind.AttackRecovery);
                    }
                    if (_state.LungePhase == HunterLungePhase.None)
                    { _state.PlannedFacts = ulong.MaxValue; _state.PhaseSeconds = 0f; }
                }
            }
            float stumbleSeconds = !_archetype.OwnsPursuit && wasAttacking && !_state.LungeHitAccepted && _profile.AttackStyle == HunterAttackStyle.Lunge ?
                Mathf.Clamp(recoveryBefore + dt - untilRecovery, 0f, _profile.MissStaggerSeconds) -
                Mathf.Clamp(recoveryBefore, 0f, _profile.MissStaggerSeconds) : 0f;
            Vector3 stumble = _state.LungeDirection * (stumbleSeconds * _profile.MissStumbleMeters / _profile.MissStaggerSeconds);
            if (_state.LungePhase == HunterLungePhase.None && !_state.AttackBecameActive && stumbleSeconds <= 0f)
            {
                Replan();
                if (!_archetype.TryMovement(out Vector3 movementTarget, out _)) UpdateTarget(dt);
                else _state.NavigationTarget = movementTarget;
                if (_state.Action == HunterAction.Lunge && _state.PlayerVisible && !_state.IsDeliberating)
                {
                    _state.LungePhase = HunterLungePhase.Windup; _state.PhaseSeconds = 0f;
                    _state.AttackSerial++; _state.AttackTarget = _player.Position;
                    _state.AcceptedRangedAttacks.RemoveWhere(serial => serial < _state.AttackSerial - 8);
                    _state.FiredRangedAttacks.RemoveWhere(serial => serial < _state.AttackSerial - 8);
                    Vector3 direction = _player.Position - _state.Position; direction.y = 0f;
                    _state.LungeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : _state.Forward;
                    _state.LungeHitAccepted = false; begin = true;
                    if ((_profile.AttackScreamsEnabled || Cursed(ProgressionTraits.WatcherUnquietGaze)) &&
                        _state.ScreamCooldown <= 0f && _random.NextDouble() < _profile.AttackScreamChance)
                    { _state.Feedback.Enqueue(HunterFeedbackKind.Scream); _state.ScreamCooldown = _profile.ScreamCooldownSeconds; }
                    else _state.Feedback.Enqueue(HunterFeedbackKind.AttackWindup);
                }
            }
            bool thinking = _state.IsDeliberating && _state.LungePhase == HunterLungePhase.None;
            if (ActiveChase || _state.LungePhase != HunterLungePhase.None) _state.ThresholdPauseRemaining = 0f;
            bool hold = thinking || _state.ThresholdPauseRemaining > 0f || stumbleSeconds > 0f || (_state.Action == HunterAction.Stalk && InPlayerRevealCone());
            Vector3 face = Vector3.zero;
            if (thinking)
            {
                Vector3 direction = _state.DeliberationTarget - _state.Position; direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    face = Quaternion.RotateTowards(Quaternion.LookRotation(_state.Forward), Quaternion.LookRotation(direction),
                        EffectiveTurnRate * dt) * Vector3.forward;
            }
            float speed = hold ? 0f : MovementSpeed();
            if (_state.LungePhase == HunterLungePhase.None && _archetype.TryMovement(out Vector3 target, out float overrideSpeed))
            { _state.NavigationTarget = target; speed = overrideSpeed; hold = false; face = Vector3.zero; }
            return new HunterTickResult(_state.NavigationTarget, speed * _state.RunSpeedMultiplier, _state.LungePhase,
                _state.LungeDirection, begin, _state.LungePhase == HunterLungePhase.Active, hold, stumble, face);
        }
        private float MovementSpeed()
        {
            switch (_state.Action)
            {
                case HunterAction.Patrol: return _profile.PatrolSpeed;
                case HunterAction.Stalk: return _player.SprintSpeed * Effective(HunterTunable.ChaseSpeedMultiplier) * _profile.StalkSpeedMultiplier;
                case HunterAction.Chase:
                case HunterAction.CutOff:
                case HunterAction.Lunge: return _player.SprintSpeed * Effective(HunterTunable.ChaseSpeedMultiplier);
                default: return _profile.InvestigateSpeed;
            }
        }
        private bool InPlayerRevealCone()
        {
            Vector3 offset = _state.Position - _player.Position;
            if (offset.sqrMagnitude > _profile.StalkRevealDistance * _profile.StalkRevealDistance) return false;
            offset.y = 0f;
            if (offset.sqrMagnitude <= 0.0001f) return true;
            float heading = _player.HeadingDegrees + (_player.LookBack ? 180f : 0f);
            Vector3 view = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            return Vector3.Dot(view, offset.normalized) + 0.000001f >=
                Mathf.Cos(_profile.StalkViewHalfAngleDegrees * Mathf.Deg2Rad);
        }
        public float EffectiveAttackDistance => _profile.AttackStyle == HunterAttackStyle.Lunge ?
            _profile.LungeDistance * (Cursed(ProgressionTraits.RusherLongStride) ? 1.35f : 1f) : _profile.RangedAttackDistance;
        public float EffectiveSightRange => _profile.SightRange * (Cursed(ProgressionTraits.WatcherUnquietGaze) ? 1.25f : 1f);
        public float EffectiveSightCone => _profile.SightConeDegrees * (Cursed(ProgressionTraits.LurkerDarkAdaptation) ? 1.3f : 1f);
        public float WindupDuration => Duration(HunterLungePhase.Windup);
        public float ProjectileSpeed => _profile.ProjectileSpeed * (Cursed(ProgressionTraits.HexerLingeringHex) ? 0.65f : 1f);
        public float ProjectileRadius => _profile.ProjectileRadius * (Cursed(ProgressionTraits.HexerLingeringHex) ? 1.7f : 1f);
        public float SpikeRadius => _profile.SpikeRadius * (Cursed(ProgressionTraits.ThorncallerReachingRoots) ? 1.4f : 1f);
        public bool SplitBolt => Cursed(ProgressionTraits.HexerSplitBolt);
        public bool ThornRing => Cursed(ProgressionTraits.ThorncallerThornRing);
        public IReadOnlyList<Bounds> UnavailableRooms => _state.UnavailableRooms;
        public float EffectiveAcceleration => Effective(HunterTunable.Acceleration);
        public float EffectiveTurnRate => Effective(HunterTunable.TurnRate);
        public bool ActiveChase => !Dormant && (_state.ChaseActive || _state.PlayerVisible);
        public bool PreferEmergence => _profile.EmergenceBias && !ActiveChase && !_state.CatchActive &&
            _state.LungePhase == HunterLungePhase.None && (_state.Action == HunterAction.Stalk ||
            _state.Action == HunterAction.InvestigateHint || _state.Action == HunterAction.SearchLastKnown);
        public bool LookAtMemory => _state.IsDeliberating || _state.Action == HunterAction.SearchLastKnown || _state.SearchActive;
        public Vector3 LookTarget => _state.IsDeliberating ? _state.DeliberationTarget : _state.LastKnownPosition;
        private bool HabitsAllowed => _state.IsActive && _player.IsAlive && !_state.CatchActive &&
            _state.LungePhase == HunterLungePhase.None && !_state.AttackBecameActive;
        public void SetChaseActive(bool active)
        {
            _state.ChaseActive = active;
            if (!active) return;
            _state.ThresholdPauseRemaining = 0f; _state.DeliberationRemaining = 0f;
            _state.DeliberationFactPending = false; _state.PendingNoiseDecision = false;
            _state.HabitFacts.Clear();
        }
        public void SetCatchActive(bool active)
        {
            _state.CatchActive = active;
            _state.HabitFacts.Clear(); _state.ThresholdPauseRemaining = 0f;
            _state.DeliberationRemaining = 0f; _state.DeliberationFactPending = false;
            _state.PendingNoiseDecision = false;
        }
        private HunterHabitData Habit(HunterHabitKind kind)
        {
            if (_profile.Habits != null) foreach (HunterHabitData habit in _profile.Habits)
                if (habit != null && habit.Kind == kind) return habit;
            return null;
        }
        private bool HabitEnabled(HunterHabitKind kind)
        {
            HunterTunable rule = kind == HunterHabitKind.ThresholdPause ? HunterTunable.ThresholdPauseEnabled :
                kind == HunterHabitKind.TurnToFace ? HunterTunable.TurnToFaceEnabled : HunterTunable.CakeReactionEnabled;
            return Habit(kind) != null && Effective(rule) == 1f;
        }
        private void EmitHabit(HunterHabitKind kind, Vector3 position)
        { if (HabitsAllowed) _state.HabitFacts.Enqueue(new HunterHabitFact(_state.Id, kind, position, _state.Tick)); }
        public bool TryTakeHabit(out HunterHabitFact fact)
        {
            fact = default;
            if (!HabitsAllowed) _state.HabitFacts.Clear();
            if (_state.HabitFacts.Count == 0) return false;
            fact = _state.HabitFacts.Dequeue(); return true;
        }
        private void BeginLossBeat()
        {
            if (_state.LossHabitObserved) return;
            _state.LossHabitObserved = true;
            if (!HabitEnabled(HunterHabitKind.TurnToFace) || !HabitsAllowed || ActiveChase) return;
            BeginDeliberation(_state.LastKnownPosition);
            if (_state.IsDeliberating) EmitHabit(HunterHabitKind.TurnToFace, _state.LastKnownPosition);
        }
        public float Effective(HunterTunable tunable)
        {
            if (_state.Mutations.TryGetValue(tunable, out float value)) return value;
            switch (tunable)
            {
                case HunterTunable.Acceleration: return _profile.Acceleration;
                case HunterTunable.TurnRate: return _profile.TurnRate;
                case HunterTunable.ChaseSpeedMultiplier: return _profile.ChaseSpeedMultiplier;
                case HunterTunable.ActionCommitmentSeconds: return _profile.ActionCommitmentSeconds;
                case HunterTunable.LossSeconds: return _profile.LossSeconds;
                case HunterTunable.LossDistance: return _profile.LossDistance;
                case HunterTunable.ThresholdPauseEnabled: return Habit(HunterHabitKind.ThresholdPause)?.Enabled == true ? 1f : 0f;
                case HunterTunable.TurnToFaceEnabled: return Habit(HunterHabitKind.TurnToFace)?.Enabled == true ? 1f : 0f;
                case HunterTunable.CakeReactionEnabled: return Habit(HunterHabitKind.CakeReaction)?.Enabled == true ? 1f : 0f;
                default: throw new ArgumentOutOfRangeException(nameof(tunable));
            }
        }
        public bool ApplyMutation(HunterMutation mutation, out HunterMutationFact fact)
        {
            fact = default;
            if (_archetype.NeverLoses && (mutation.Tunable == HunterTunable.LossSeconds || mutation.Tunable == HunterTunable.LossDistance)) return false;
            if (!Enum.IsDefined(typeof(HunterTunable), mutation.Tunable) || !Finite(mutation.Value) ||
                string.IsNullOrWhiteSpace(mutation.TellId) || mutation.Value < 0f) return false;
            bool rule = mutation.Tunable >= HunterTunable.ThresholdPauseEnabled;
            if (rule && mutation.Value != 0f && mutation.Value != 1f) return false;
            if (rule && Habit((HunterHabitKind)((int)mutation.Tunable - (int)HunterTunable.ThresholdPauseEnabled)) == null) return false;
            if (!rule && mutation.Tunable <= HunterTunable.ActionCommitmentSeconds && mutation.Value <= 0f) return false;
            if (mutation.Tunable == HunterTunable.ActionCommitmentSeconds && mutation.Value < 0.1f) return false;
            if (mutation.Tunable == HunterTunable.ChaseSpeedMultiplier &&
                !Finite(_player.SprintSpeed * mutation.Value * _state.RunSpeedMultiplier)) return false;
            if (Effective(mutation.Tunable) == mutation.Value) return false; // Reapplication is idempotent.
            _state.Mutations[mutation.Tunable] = mutation.Value;
            _state.LossSeconds = _archetype.NeverLoses ? float.PositiveInfinity : Effective(HunterTunable.LossSeconds);
            _state.LossDistance = _archetype.NeverLoses ? float.PositiveInfinity : Effective(HunterTunable.LossDistance);
            if (mutation.Tunable == HunterTunable.ThresholdPauseEnabled && mutation.Value == 0f) _state.ThresholdPauseRemaining = 0f;
            fact = new HunterMutationFact(_state.Id, _profile.ArchetypeKey, mutation.TellId, _state.Tick);
            return true;
        }
        public void SetRoomPhase(RoomPhaseChangedFact fact)
        {
            if (fact.Phase != RoomPhase.Closed || !_state.UnavailableRoomIds.Add(fact.RoomId) || _level.Graph == null) return;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (room.Id == fact.RoomId) { _state.UnavailableRooms.Add(room.Bounds); break; }
            _state.PlannedFacts = ulong.MaxValue; _state.HasPatrolTarget = false;
        }
        private bool Unavailable(Vector3 point)
        { foreach (Bounds room in _state.UnavailableRooms) if (room.Contains(point)) return true; return false; }
        public void SetTraits(ProgressionTraits traits)
        {
            const ProgressionTraits rusher = ProgressionTraits.RusherLongStride | ProgressionTraits.RusherSecondWind | ProgressionTraits.RusherBloodScent;
            const ProgressionTraits lurker = ProgressionTraits.LurkerDarkAdaptation | ProgressionTraits.LurkerCrookedStep | ProgressionTraits.LurkerStolenSilence;
            const ProgressionTraits watcher = ProgressionTraits.WatcherLongMemory | ProgressionTraits.WatcherCuttingCorners | ProgressionTraits.WatcherUnquietGaze;
            const ProgressionTraits hexer = ProgressionTraits.HexerSplitBolt | ProgressionTraits.HexerHastyScript | ProgressionTraits.HexerLingeringHex;
            const ProgressionTraits thorncaller = ProgressionTraits.ThorncallerThornRing | ProgressionTraits.ThorncallerQuickRoots | ProgressionTraits.ThorncallerReachingRoots;
            const ProgressionTraits specific = rusher | lurker | watcher | hexer | thorncaller;
            ProgressionTraits own = ProgressionTraits.None;
            switch (_profile.ArchetypeKey)
            {
                case "rusher": own = rusher; break;
                case "lurker": own = lurker; break;
                case "watcher": own = watcher; break;
                case "hexer": own = hexer; break;
                case "thorncaller": own = thorncaller; break;
            }
            _state.Traits = traits & (~specific | own);
            _state.PlannedFacts = ulong.MaxValue;
        }
        private bool Cursed(ProgressionTraits trait) => (_state.Traits & trait) != 0;
        public void SetAfterimage(FlashlightSample sample, float lifetime)
        {
            if (sample.Source != _state.TargetId) return;
            _state.Afterimage = sample; _state.AfterimageRemaining = Finite(lifetime) ? Mathf.Clamp(lifetime, 0f, 4f) : 0f;
        }
        public bool HearNoise(NoiseEvent noise, float transmission)
        {
            if (_archetype.OwnsPursuit) return false;
            if (_state.PursuitSuppressed || noise.Source == _state.Id || noise.Tick > _state.Tick || _state.HeardNoises.Contains(noise) ||
                !Finite(noise.Position) || !Finite(noise.Loudness) || !Finite(transmission)) return false;
            float age = (_state.Tick - noise.Tick) * _state.DeltaTime;
            if (age - Mathf.Abs(age) * 1.1920929e-7f > _profile.NoiseMaxAgeSeconds * (Cursed(ProgressionTraits.RusherBloodScent) ? 2f : 1f)) return false;
            float range = _profile.HearingRange * (Cursed(ProgressionTraits.RusherBloodScent) ? 1.35f : 1f);
            if (!_level.IsReady || _level.Graph == null || Vector3.Distance(noise.Position, _state.Position) >= range) return false;
            HearingSample heard = AcousticOcclusionUtility.Sample(_level.Graph, HunterNavigationUtility.RoomAt(_level.Graph, noise.Position),
                noise.Position, HunterNavigationUtility.RoomAt(_level.Graph, _state.Position), _state.Position,
                noise.Loudness * Mathf.Clamp01(transmission), _profile.HearingModel, _closedDoors);
            if (!heard.Audible) return false;
            _state.HeardNoises.Add(noise); _state.LastNoiseTick = noise.Tick;
            if (_state.PlayerVisible || _state.LungePhase != HunterLungePhase.None) return true;
            if (_state.PendingNoiseDecision && heard.PerceivedLoudness <= _state.PendingNoiseLoudness) return true;
            _state.PendingNoise = noise; _state.PendingNoiseLoudness = heard.PerceivedLoudness;
            _state.PendingNoiseDecision = true; BeginDeliberation(noise.Position);
            if (!_state.IsDeliberating) ResolveNoise();
            return true;
        }
        private void ResolveNoise()
        {
            if (!_state.PendingNoiseDecision) return;
            _state.PendingNoiseDecision = false;
            float threshold = _state.CurrentGoal == HunterGoal.ProtectExit ? _profile.ExitNoiseThreshold : _profile.InvestigateNoiseThreshold;
            if (_state.PendingNoiseLoudness < threshold || _state.PlayerVisible || _state.PursuitSuppressed ||
                _state.PendingNoise.Tick < _state.LastKnownTick) return;
            _state.PlayerHeard = true;
            Observe(_state.PendingNoise.Position, _state.PendingNoise.Tick, _state.PendingNoiseLoudness);
            _state.SearchRoute.Clear(); _state.SearchIndex = 0; _state.SearchActive = false; _state.SearchSeconds = 0f;
            _state.CommitmentRemaining = 0f; _state.PlannedFacts = ulong.MaxValue;
        }
        private void BeginDeliberation(Vector3 target)
        {
            if (_state.LungePhase != HunterLungePhase.None || _state.CatchActive || _state.ChaseActive) return;
            _state.DeliberationTarget = target;
            if (_state.IsDeliberating) return; // Fresh sounds may change facing, never indefinitely restart the beat.
            _state.DeliberationRemaining = _profile.DeliberationSeconds;
            _state.DeliberationFactPending = _state.IsDeliberating;
        }
        public bool TryTakeDeliberation(out Vector3 candidate)
        {
            candidate = _state.DeliberationTarget;
            bool pending = _state.DeliberationFactPending;
            _state.DeliberationFactPending = false; return pending;
        }
        public void SetFloorView(IReadOnlyFloorState floor)
        { if (ReferenceEquals(_floor, floor)) return; _floor = floor; _state.CakeRooms.Clear(); _state.CakePositions.Clear(); _state.LastPickupRoom = 0; }
        public void SetClosedDoors(IReadOnlyDictionary<int, bool> doors) { _closedDoors = doors; }
        public bool RequestRetreat(IReadOnlyList<int> occludedRooms)
        {
            if (_archetype.OwnsPursuit) return false;
            if (!_state.IsActive || !_player.IsAlive || _state.PursuitSuppressed || _state.LungePhase != HunterLungePhase.None ||
                _level.Graph == null || occludedRooms == null || _state.BeliefConfidence <= 0f) return false;
            int start = HunterNavigationUtility.RoomAt(_level.Graph, _state.Position);
            Vector3 target = _state.Position; float farthest = Vector3.SqrMagnitude(_state.Position - _state.LastKnownPosition);
            bool found = false;
            foreach (int room in occludedRooms)
            {
                if (room == start || HunterNavigationUtility.Path(_level.Graph, start, room, _state.UnavailableRoomIds).Count == 0) continue;
                Vector3 point = HunterNavigationUtility.RoomTarget(_level.Graph, room, _state.Position);
                float distance = Vector3.SqrMagnitude(point - _state.LastKnownPosition);
                if (distance <= farthest) continue;
                target = point; farthest = distance; found = true;
            }
            if (!found) return false;
            _state.Action = HunterAction.Retreat; _state.NavigationTarget = target;
            _state.PursuitSuppressed = true; _state.RetreatRemaining = _profile.RetreatTimeoutSeconds;
            _state.PlayerVisible = false; _state.BeliefConfidence = 0f; _state.BeliefInitialConfidence = 0f;
            _state.HasHint = false; _state.SearchActive = false; _state.SearchRoute.Clear();
            _state.DeliberationRemaining = 0f; _state.PendingNoiseDecision = false; _state.DeliberationFactPending = false;
            _state.LightMemoryRemaining = 0f; _state.LightReactionRemaining = 0f;
            return true;
        }
        public bool TryAcceptRangedContact(EntityId target, int attackSerial, out HunterHit hit)
        {
            hit = default;
            if (Dormant || _profile.AttackStyle == HunterAttackStyle.Lunge || target != _state.TargetId || !_player.IsAlive || !_state.IsActive ||
                attackSerial <= 0 || !_state.FiredRangedAttacks.Contains(attackSerial) || attackSerial > _state.AttackSerial || attackSerial < _state.AttackSerial - 8 ||
                !_state.AcceptedRangedAttacks.Add(attackSerial)) return false;
            _state.Feedback.Enqueue(HunterFeedbackKind.AttackHit);
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position,
                _profile.AttackStyle == HunterAttackStyle.Projectile ? ChaseEndReason.Projectile : ChaseEndReason.GroundSpike);
            return true;
        }
        public void ReportAttackMiss(int attackSerial)
        {
            if (!_state.AcceptedRangedAttacks.Contains(attackSerial)) _state.Feedback.Enqueue(HunterFeedbackKind.AttackMiss);
        }
        public void SetFlashlight(FlashlightSample sample)
        { if (sample.Source == _state.TargetId) _state.Flashlight = sample; }
        public bool TryDequeueFeedback(out HunterFeedbackEvent feedback)
        {
            feedback = default;
            if (!_player.IsAlive) { _state.Feedback.Clear(); return false; }
            if (_state.Feedback.Count == 0) return false;
            feedback = new HunterFeedbackEvent(_state.Id, _profile.ArchetypeKey, _state.Feedback.Dequeue(), _state.Position, _state.Tick);
            return true;
        }
        private void UpdateLightTimers(float dt)
        {
            _state.AfterimageRemaining = Mathf.Max(0f, _state.AfterimageRemaining - dt);
            _state.LightMemoryRemaining = Mathf.Max(0f, _state.LightMemoryRemaining - dt);
            _state.LightReactionCooldown = Mathf.Max(0f, _state.LightReactionCooldown - dt);
            if (_state.LightReactionRemaining > 0f)
            {
                _state.LightReactionRemaining = Mathf.Max(0f, _state.LightReactionRemaining - dt);
                if (_state.LightReactionRemaining <= 0f) _state.PlannedFacts = ulong.MaxValue;
            }
        }
        private void SenseLight(HunterLightObservation observation, long tick)
        {
            bool valid = observation.Tick == tick && Finite(observation.Position);
            _state.LightObserved = valid && observation.Observed;
            _state.DirectlyIlluminated = valid && observation.Illuminated;
            if (!_state.LightObserved && !_state.DirectlyIlluminated) return;
            _state.LastLightPosition = observation.Position;
            _state.LightMemoryRemaining = _profile.LightMemorySeconds * (Cursed(ProgressionTraits.WatcherLongMemory) ? 1.75f : 1f);
        }
        public float LungeSpeed => _profile.LungeSpeed * _state.RunSpeedMultiplier;
        public void ApplyRunSpeedMultiplier(float multiplier)
        {
            if (!Finite(multiplier) || multiplier <= 0f || !Finite(_profile.PatrolSpeed * multiplier)
                || !Finite(_profile.InvestigateSpeed * multiplier)
                || !Finite(_player.SprintSpeed * Effective(HunterTunable.ChaseSpeedMultiplier) * multiplier)
                || !Finite(_profile.LungeSpeed * multiplier))
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Hunter run speed must be finite and positive.");
            _state.RunSpeedMultiplier = multiplier;
        }
        public HunterAttackSample AttackSample()
        {
            bool attacking = _state.IsActive && _state.LungePhase != HunterLungePhase.None;
            return new HunterAttackSample(_state.Id, _state.Position,
                attacking ? _state.LungeDirection : _state.Forward,
                attacking ? (int)_state.LungePhase : 0,
                attacking ? Mathf.Clamp01(_state.PhaseSeconds / Mathf.Max(0.0001f, Duration(_state.LungePhase))) : 0f);
        }
        public void CommitPose(Vector3 position, Vector3 velocity, Vector3 forward)
        {
            Vector3 displacement = position - _state.Position; displacement.y = 0f;
            if (!_archetype.OwnsPursuit && _state.LungePhase == HunterLungePhase.None) _state.StepDistance += displacement.magnitude;
            _state.Position = position; _state.Velocity = velocity; _state.Forward = forward;
            if (_state.StepDistance >= 2.2f && _state.FootstepCooldown <= 0f)
            { _state.StepDistance = 0f; _state.FootstepCooldown = 0.18f; _state.Feedback.Enqueue(HunterFeedbackKind.Footstep); }
        }
        public HunterSighting Sighting() => new HunterSighting(_state.Id, _state.TargetId, _state.Tick,
            _state.PlayerVisible, _state.Position, Vector3.Distance(_state.Position, _player.Position));
        public void ReportPathFailure()
        {
            _state.ActionFailed = true;
            if (_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight)
            { _state.LightReactionRemaining = 0f; _state.LightExposure = 0f; }
        }
        public bool TryAcceptContact(EntityId target, out HunterHit hit)
        {
            hit = default;
            if (Dormant || _state.LungePhase != HunterLungePhase.Active || _state.LungeHitAccepted ||
                target != _state.TargetId || !_player.IsAlive || !_state.IsActive) return false;
            _state.LungeHitAccepted = true;
            _state.Feedback.Enqueue(HunterFeedbackKind.AttackHit);
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position);
            return true;
        }
        public bool ReceiveHint(HintPayload hint)
        {
            if (_archetype.OwnsPursuit) return false;
            if (_state.PursuitSuppressed || hint.Hunter != _state.Id || hint.Player != _state.TargetId || hint.ObservedTick > hint.DeliveredTick ||
                hint.DeliveredTick != _state.Tick || !Finite(hint.Position) ||
                !Finite(hint.AgeSeconds) || !Finite(hint.Radius) || !Finite(hint.Confidence) ||
                hint.AgeSeconds < 0f || hint.Radius < 0f ||
                hint.AgeSeconds >= MemoryDuration || _state.PlayerVisible ||
                (_state.BeliefConfidence > 0f && hint.ObservedTick < _state.LastKnownTick)) return false;
            double angle = _random.NextDouble() * Math.PI * 2.0;
            float radius = Mathf.Sqrt((float)_random.NextDouble()) * hint.Radius;
            _state.LastKnownPosition = hint.Position + new Vector3((float)Math.Cos(angle) * radius, 0f, (float)Math.Sin(angle) * radius);
            _state.LastKnownTick = hint.ObservedTick;
            _state.BeliefReferenceTick = hint.DeliveredTick; _state.BeliefAgeAtReference = hint.AgeSeconds;
            _state.BeliefInitialConfidence = Mathf.Clamp01(hint.Confidence);
            _state.BeliefConfidence = _state.BeliefInitialConfidence *
                Mathf.Clamp01(1f - hint.AgeSeconds / Mathf.Max(0.0001f, MemoryDuration));
            _state.HasHint = _state.BeliefConfidence > 0f; _state.PlannedFacts = ulong.MaxValue;
            if (_state.HasHint)
            {
                _state.SearchActive = false; _state.SearchRoute.Clear(); _state.SearchIndex = 0; _state.SearchSeconds = 0f;
                BeginDeliberation(_state.LastKnownPosition);
            }
            return _state.HasHint;
        }
        public bool ReceiveRegionHint(HintPayload hint, int roomId)
        {
            if (_level.Graph == null) return false;
            foreach (LevelRoom room in _level.Graph.Rooms)
            {
                if (room.Id != roomId || _state.UnavailableRoomIds.Contains(roomId)) continue;
                if (!ReceiveHint(hint)) return false;
                _state.LastKnownPosition = room.Bounds.ClosestPoint(_state.LastKnownPosition);
                _state.DeliberationTarget = _state.LastKnownPosition; return true;
            }
            return false;
        }
        private void Sense(SightProbe probe, float dt, long tick)
        {
            _state.SensorInitialized = true;
            Vector3 offset = _player.Position - _state.Position;
            float distance = offset.magnitude;
            Vector3 planar = offset; planar.y = 0f;
            float dot = planar.sqrMagnitude <= 0.0001f ? 1f : Vector3.Dot(_state.Forward.normalized, planar.normalized);
            _state.PlayerVisible = (probe.HeadVisible || probe.ChestVisible || probe.HipsVisible) &&
                distance <= EffectiveSightRange && dot + 0.000001f >= Mathf.Cos(EffectiveSightCone * 0.5f * Mathf.Deg2Rad);
            _state.PlayerVisible = _archetype.FilterVisibility(_state.PlayerVisible, probe, ArchetypeContext);
            _state.PlayerHeard = false;
            if (_state.PlayerVisible)
            {
                Observe(_player.Position, tick, 1f);
                int room = HunterNavigationUtility.RoomAt(_level.Graph, _player.Position);
                if (room != 0 && room != _state.ObservedPlayerRoom)
                { _state.PreviousPlayerRoom = _state.ObservedPlayerRoom; _state.ObservedPlayerRoom = room; _state.PredictionRoute.Clear(); }
                _state.ObservedPlayerVelocity = _player.Velocity;
            }
            if (_player.RecentNoises != null)
                foreach (NoiseEvent noise in _player.RecentNoises) HearNoise(noise, 1f);
        }

        private void Observe(Vector3 position, long tick, float confidence)
        {
            _state.LastKnownPosition = position; _state.LastKnownTick = tick;
            _state.BeliefInitialConfidence = confidence; _state.BeliefConfidence = confidence;
            _state.BeliefReferenceTick = tick; _state.BeliefAgeAtReference = 0f;
            _state.HasHint = false;
        }
        private void DecayBelief(float dt, long tick)
        {
            float before = _state.BeliefConfidence;
            _state.BeliefConfidence = _state.BeliefInitialConfidence *
                Mathf.Clamp01(1f - Mathf.Max(0f, BeliefAge(dt, tick)) /
                Mathf.Max(0.0001f, MemoryDuration));
            if (_state.BeliefConfidence <= 0f)
            {
                _state.HasHint = false;
                if (before > 0f && !_state.SearchActive) BeginLossBeat();
            }
        }
        private float BeliefAge(float dt, long tick) => _state.BeliefAgeAtReference + (tick - _state.BeliefReferenceTick) * dt;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private float MemoryDuration => _profile.MemoryDecaySeconds * (Cursed(ProgressionTraits.WatcherLongMemory) ? 1.75f : 1f);
        private float Duration(HunterLungePhase phase)
        {
            if (phase == HunterLungePhase.Windup)
                return Mathf.Max(0.15f, _profile.LungeWindupSeconds *
                    (Cursed(ProgressionTraits.LurkerStolenSilence) || Cursed(ProgressionTraits.HexerHastyScript) || Cursed(ProgressionTraits.ThorncallerQuickRoots) ? 0.7f : 1f));
            if (phase == HunterLungePhase.Active) return Mathf.Max(0.05f, _profile.LungeActiveSeconds);
            float recovery = Mathf.Max(0.15f, _profile.LungeRecoverySeconds * (Cursed(ProgressionTraits.RusherSecondWind) ? 0.65f : 1f));
            return !_state.LungeHitAccepted && _profile.AttackStyle == HunterAttackStyle.Lunge ? Mathf.Max(recovery, _profile.MissStaggerSeconds) : recovery;
        }
        private void Replan()
        {
            ulong facts = 0;
            if (_state.PlayerVisible) facts |= (ulong)HunterWorldFacts.PlayerVisible;
            if (_state.PlayerHeard) facts |= (ulong)HunterWorldFacts.PlayerHeard;
            if (_state.BeliefConfidence > 0f || _state.SearchActive) facts |= (ulong)HunterWorldFacts.HasBelief;
            if (!_state.SearchActive && _state.BeliefConfidence >= _profile.StalkMinimumConfidence &&
                BeliefAge(_state.DeltaTime, _state.Tick) <= _profile.BeliefFreshSeconds)
                facts |= (ulong)HunterWorldFacts.BeliefFresh;
            bool reachableElevation = _profile.AttackStyle != HunterAttackStyle.Lunge ||
                Mathf.Abs(_state.Position.y - _player.Position.y) <= _profile.MaximumMeleeElevation;
            if (_state.PlayerVisible && reachableElevation && !Unavailable(_state.Position) && !Unavailable(_player.Position) && Vector3.Distance(_state.Position, _player.Position) <= EffectiveAttackDistance)
                facts |= (ulong)HunterWorldFacts.InLungeRange;
            if (_state.LoopDetected || (_profile.LightResponse == HunterLightResponse.Flank && _state.PlayerVisible)) facts |= (ulong)HunterWorldFacts.LoopDetected;
            if (_state.HasHint) facts |= (ulong)HunterWorldFacts.HasHint;
            if (_state.LightObserved) facts |= (ulong)HunterWorldFacts.LightObserved;
            if (_state.DirectlyIlluminated) facts |= (ulong)HunterWorldFacts.DirectlyIlluminated;
            if (_state.LightMemoryRemaining > 0f) facts |= (ulong)HunterWorldFacts.LightMemoryFresh;
            bool react = _profile.LightResponse != HunterLightResponse.Investigate &&
                ((_state.LightExposure >= _profile.LightExposureSeconds && _state.LightReactionCooldown <= 0f) || _state.LightReactionRemaining > 0f);
            if (react) facts |= (ulong)HunterWorldFacts.LightReactionReady;
            bool urgent = ((facts ^ _state.PlannedFacts) & (ulong)(HunterWorldFacts.PlayerVisible | HunterWorldFacts.InLungeRange |
                HunterWorldFacts.LightReactionReady | HunterWorldFacts.HasBelief | HunterWorldFacts.BeliefFresh |
                HunterWorldFacts.LightMemoryFresh | HunterWorldFacts.LoopDetected)) != 0;
            if (_state.CommitmentRemaining > 0.000001f && !urgent) return;
            _state.CommitmentRemaining = Effective(HunterTunable.ActionCommitmentSeconds);
            UpdateGoalTargets();
            if (_state.CakeAvailable) facts |= (ulong)HunterWorldFacts.CakeAvailable;
            if (_state.ExitAvailable) facts |= (ulong)HunterWorldFacts.ExitOpen;
            var actions = new List<GoapActionDefinition>
            {
                Action(HunterAction.DenyCake, HunterWorldFacts.CakeAvailable, 0, HunterWorldFacts.RouteDenied, 1f),
                Action(HunterAction.ProtectExit, HunterWorldFacts.ExitOpen, 0, HunterWorldFacts.ExitProtected, 1f),
                Action(HunterAction.BreakLoop, HunterWorldFacts.LoopDetected | HunterWorldFacts.HasBelief, 0, HunterWorldFacts.LoopBroken, 1f),
                Action(HunterAction.InvestigateLight, HunterWorldFacts.LightMemoryFresh, HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 0.75f),
                Action(_profile.LightResponse == HunterLightResponse.Avoid ? HunterAction.AvoidLight : HunterAction.FlankLight,
                    HunterWorldFacts.LightReactionReady, 0, HunterWorldFacts.EscapedBeam, 0.5f),
                Action(HunterAction.Patrol, 0, 0, HunterWorldFacts.Patrolled, 4f),
                Action(HunterAction.Stalk, HunterWorldFacts.HasBelief | HunterWorldFacts.BeliefFresh,
                    HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 0.5f),
                Action(HunterAction.InvestigateHint, HunterWorldFacts.HasHint, HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 1f),
                Action(HunterAction.SearchLastKnown, HunterWorldFacts.HasBelief, HunterWorldFacts.PlayerVisible, HunterWorldFacts.LocatedPlayer, 2f),
                Action(HunterAction.Chase, HunterWorldFacts.PlayerVisible, HunterWorldFacts.InLungeRange, HunterWorldFacts.InLungeRange, 2f),
                Action(HunterAction.CutOff, HunterWorldFacts.PlayerVisible | HunterWorldFacts.LoopDetected,
                    HunterWorldFacts.InLungeRange, HunterWorldFacts.InLungeRange, 1f),
                Action(HunterAction.Lunge, HunterWorldFacts.PlayerVisible | HunterWorldFacts.InLungeRange, 0, HunterWorldFacts.CaughtPlayer, 1f)
            };
            if (_state.ActionFailed)
            {
                actions.RemoveAll(action => action.Id == (int)_state.Action);
                _state.HasPatrolTarget = false;
            }
            ulong goal = react ? (ulong)HunterWorldFacts.EscapedBeam : _state.PlayerVisible ? (ulong)HunterWorldFacts.CaughtPlayer :
                _state.SearchActive || _state.BeliefConfidence > 0f || _state.LightMemoryRemaining > 0f ? (ulong)HunterWorldFacts.LocatedPlayer : (ulong)HunterWorldFacts.Patrolled;
            var goals = new[] {
                new GoapGoalDefinition((int)HunterGoal.LocatePrey, goal, react ? 200f : _state.PlayerVisible ? 100f :
                    _state.SearchActive ? 70f : _state.BeliefConfidence > 0f ? 20f + 80f * _state.BeliefConfidence : 10f),
                new GoapGoalDefinition((int)HunterGoal.DenyCake, (ulong)HunterWorldFacts.RouteDenied,
                    _state.CakeAvailable ? _profile.CakeGoalUtility : 0f),
                new GoapGoalDefinition((int)HunterGoal.ProtectExit, (ulong)HunterWorldFacts.ExitProtected,
                    _state.ExitAvailable ? _profile.ExitGoalUtility : 0f),
                new GoapGoalDefinition((int)HunterGoal.BreakLoop, (ulong)HunterWorldFacts.LoopBroken,
                    _state.LoopDetected ? _profile.LoopGoalUtility : 0f) };
            for (int i = 0; i < goals.Length; i++)
                goals[i] = new GoapGoalDefinition(goals[i].Id, goals[i].Facts,
                    _archetype.GoalUtility((HunterGoal)goals[i].Id, goals[i].Utility));
            GoapPlanResult plan = GoapPlannerUtility.Select(facts, goals, actions, out int selectedGoal);
            HunterAction chosen = plan.ActionIds.Length > 0 ? (HunterAction)plan.ActionIds[0] : HunterAction.Patrol;
            bool changed = chosen != _state.Action || selectedGoal != (int)_state.CurrentGoal || facts != _state.PlannedFacts || _state.ActionFailed;
            _state.CurrentGoal = selectedGoal < 0 ? HunterGoal.LocatePrey : (HunterGoal)selectedGoal;
            if (changed)
            {
                _state.Action = chosen; _state.HasPatrolTarget = false; _state.ReplanCount++;
                // Attack commitment must not consume an unrelated prediction draw.
                _state.Predict = chosen == HunterAction.Chase && _state.PlayerVisible &&
                    _level.IsReady && _level.Graph != null && _profile.PredictionChance > 0f &&
                    _random.NextDouble() < _profile.PredictionChance;
                _state.PredictionRoute.Clear();
            }
            if ((_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight) && _state.LightReactionRemaining <= 0f)
            {
                Vector3 away = _state.Position - _state.LastLightPosition; away.y = 0f;
                away = away.sqrMagnitude > 0.001f ? away.normalized : -_state.Forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, away) * ((_state.Id.Value & 1) == 0 ? 1f : -1f);
                _state.LightReactionTarget = _state.Position +
                    (_state.Action == HunterAction.AvoidLight ? (away + lateral).normalized : lateral) * _profile.LightReactionDistance * (Cursed(ProgressionTraits.LurkerCrookedStep) ? 1.5f : 1f);
                _state.LightReactionRemaining = _profile.LightReactionSeconds * (Cursed(ProgressionTraits.LurkerCrookedStep) ? 1.3f : 1f);
                _state.LightReactionCooldown = _profile.LightReactionSeconds + _profile.LightReactionCooldownSeconds;
                _state.Feedback.Enqueue(HunterFeedbackKind.LightReaction);
            }
            _state.PlannedFacts = facts;
            _state.ActionFailed = false;
        }
        private static GoapActionDefinition Action(HunterAction id, HunterWorldFacts required, HunterWorldFacts absent, HunterWorldFacts effect, float cost)
            => new GoapActionDefinition((int)id, (ulong)required, (ulong)absent, (ulong)effect, 0, cost);
        private void UpdateTarget(float dt)
        {
            if (_state.Action == HunterAction.DenyCake) _state.NavigationTarget = _state.CakeTarget;
            else if (_state.Action == HunterAction.ProtectExit) _state.NavigationTarget = _state.ExitTarget;
            else if (_state.Action == HunterAction.BreakLoop) _state.NavigationTarget = InterceptRoom();
            else if (_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight)
                _state.NavigationTarget = _state.LightReactionTarget;
            else if (_state.Action == HunterAction.InvestigateLight)
            {
                _state.NavigationTarget = _state.LastLightPosition;
                if (Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius)
                { _state.LightMemoryRemaining = 0f; _state.PlannedFacts = ulong.MaxValue; }
            }
            else if (_state.Action == HunterAction.Chase)
                _state.NavigationTarget = _state.Predict ? PursuitTarget() : _player.Position;
            else if (_state.Action == HunterAction.Lunge)
                _state.NavigationTarget = _player.Position;
            else if (_state.Action == HunterAction.CutOff) _state.NavigationTarget = InterceptRoom();
            else if (_state.Action == HunterAction.Stalk) _state.NavigationTarget = _state.LastKnownPosition;
            else if (_state.Action == HunterAction.InvestigateHint || _state.Action == HunterAction.SearchLastKnown)
            {
                if (_state.SearchRoute.Count == 0)
                    _state.SearchRoute.AddRange(HunterNavigationUtility.Search(_level.Graph, _state.LastKnownPosition,
                        _state.ObservedPlayerVelocity, 0, HunterNavigationUtility.RoomAt(_level.Graph, _state.LastKnownPosition),
                        _profile.SearchExpansionMeters, _state.UnavailableRoomIds));
                _state.SearchActive = true;
                _state.NavigationTarget = _state.SearchRoute[_state.SearchIndex];
                if (!_state.IsDeliberating)
                {
                    if (_state.SearchSeconds == 0f)
                    {
                        float speed = _profile.InvestigateSpeed * _state.RunSpeedMultiplier;
                        float travel = speed > 0f ? Vector3.Distance(_state.Position, _state.NavigationTarget) / speed : 0f;
                        _state.SearchLegBudget = Mathf.Min(_profile.SearchMaximumLegSeconds,
                            _profile.SearchLegTimeoutSeconds + travel * _profile.SearchTravelAllowance);
                    }
                    _state.SearchSeconds += dt;
                    if (Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius ||
                        _state.SearchSeconds >= _state.SearchLegBudget)
                    {
                        _state.SearchSeconds = 0f;
                        if (++_state.SearchIndex >= _state.SearchRoute.Count)
                        {
                            _state.SearchActive = false; _state.SearchRoute.Clear(); _state.SearchIndex = 0;
                            _state.BeliefInitialConfidence = 0f; _state.BeliefConfidence = 0f; _state.HasHint = false;
                            _state.PlannedFacts = ulong.MaxValue; _state.CommitmentRemaining = 0f;
                            _state.Action = HunterAction.Patrol; _state.HasPatrolTarget = false;
                        }
                    }
                }
            }
            else if (!_state.HasPatrolTarget || Vector3.Distance(_state.Position, _state.NavigationTarget) <= _profile.ArrivalRadius)
            {
                int preferred = HunterNavigationUtility.PreferredRoom(_level.Graph, _state.LastRoom, _state.Position,
                    _floor?.ActiveCakeAnchors, _floor?.ExitState == ExitState.Open, _state.LastPickupRoom, _state.UnavailableRoomIds);
                if (preferred == 0 && _level.Graph != null)
                {
                    var available = new List<int>();
                    foreach (LevelRoom room in _level.Graph.Rooms)
                        if (room.Id != _state.LastRoom && HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, room.Id, _state.UnavailableRoomIds).Count > 0)
                            available.Add(room.Id);
                    if (available.Count > 0) preferred = available[_random.Next(available.Count)];
                }
                _state.NavigationTarget = HunterNavigationUtility.RoomTarget(_level.Graph, preferred, _state.Position);
                _state.HasPatrolTarget = true;
            }
        }
        private Vector3 PursuitTarget()
        {
            if (_state.ObservedPlayerVelocity.sqrMagnitude <= 0.0001f) return _state.LastKnownPosition;
            Vector3 candidate = InterceptRoom();
            Vector3 observed = _state.LastKnownPosition - _state.Position; observed.y = 0f;
            Vector3 aim = candidate - _state.Position; aim.y = 0f;
            // Do not steer away from visible prey merely to pursue an extrapolation.
            // The physical motor still owns turn inertia and obstacle clearance.
            return aim.sqrMagnitude > 0.0001f && Vector3.Angle(observed, aim) < EffectiveSightCone * 0.5f ?
                candidate : _state.LastKnownPosition;
        }
        private Vector3 InterceptRoom()
        {
            if (!_level.IsReady || _level.Graph == null || _state.LastRoom == 0) return _state.LastKnownPosition;
            Vector3 predicted = _state.LastKnownPosition + _state.ObservedPlayerVelocity * _profile.CutOffPredictionSeconds * (Cursed(ProgressionTraits.WatcherCuttingCorners) ? 1.7f : 1f);
            Vector3 best = _state.LastKnownPosition; float score = float.PositiveInfinity; int bestRoom = 0;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (Vector3.SqrMagnitude(RoomTarget(room) - predicted) < score &&
                    HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, room.Id, _state.UnavailableRoomIds).Count > 0)
                { best = RoomTarget(room); bestRoom = room.Id; score = Vector3.SqrMagnitude(best - predicted); }
            // A detected loop retains the explicit reachable-room intercept contract.
            if (_state.Action == HunterAction.CutOff || _state.Action == HunterAction.BreakLoop) return best;
            if (bestRoom == _state.LastRoom) return predicted;
            if (_state.PredictionRoute.Count == 0)
            {
                if (_random.NextDouble() < _profile.ParallelCorridorChance)
                    _state.PredictionRoute.AddRange(HunterNavigationUtility.ParallelPath(_level.Graph, _state.LastRoom, bestRoom, _state.UnavailableRoomIds));
                if (_state.PredictionRoute.Count == 0) _state.PredictionRoute.Add(bestRoom);
                _state.PredictionIndex = _state.PredictionRoute.Count > 1 ? 1 : 0;
            }
            if (_state.PredictionRoute.Count == 1 && bestRoom == _state.ObservedPlayerRoom) return predicted;
            if (_state.PredictionIndex < _state.PredictionRoute.Count)
            {
                Vector3 waypoint = HunterNavigationUtility.RoomTarget(_level.Graph, _state.PredictionRoute[_state.PredictionIndex], best);
                if (Vector3.Distance(_state.Position, waypoint) > _profile.ArrivalRadius) return waypoint;
                _state.PredictionIndex++;
            }
            if (bestRoom == _state.ObservedPlayerRoom) return _state.LastKnownPosition;
            return best;
        }
        private void UpdateFloorMemory()
        {
            if (_floor == null || !_floor.IsReady) return;
            var present = new HashSet<int>();
            foreach (LevelAnchor cake in _floor.ActiveCakeAnchors) present.Add(cake.Id);
            int removed = int.MaxValue;
            foreach (var pair in _state.CakeRooms)
                if (!present.Contains(pair.Key) && pair.Key < removed)
                { removed = pair.Key; _state.LastPickupRoom = pair.Value; _state.HasPatrolTarget = false; }
            if (removed != int.MaxValue && HabitEnabled(HunterHabitKind.CakeReaction) &&
                _state.CakePositions.TryGetValue(removed, out Vector3 pickup) &&
                Vector3.Distance(_state.Position, pickup) <= Habit(HunterHabitKind.CakeReaction).Radius)
                EmitHabit(HunterHabitKind.CakeReaction, pickup);
            _state.CakeRooms.Clear();
            _state.CakePositions.Clear();
            foreach (LevelAnchor cake in _floor.ActiveCakeAnchors)
            { _state.CakeRooms[cake.Id] = cake.RoomId; _state.CakePositions[cake.Id] = cake.Position; }
        }
        private void UpdateGoalTargets()
        {
            _state.CakeAvailable = false; _state.ExitAvailable = false;
            if (_floor == null || !_floor.IsReady || !_level.IsReady || _level.Graph == null) return;
            foreach (var room in _floor.RoomPhases)
                if (room.Value == RoomPhase.Closed) SetRoomPhase(new RoomPhaseChangedFact(room.Key, room.Value, _state.Tick));
            float nearest = float.PositiveInfinity;
            foreach (LevelAnchor cake in _floor.ActiveCakeAnchors)
            {
                float distance = Vector3.SqrMagnitude(cake.Position - _state.Position);
                List<int> path = HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, cake.RoomId, _state.UnavailableRoomIds);
                if (distance >= nearest || path.Count == 0) continue;
                nearest = distance; _state.CakeAvailable = true;
                _state.CakeTarget = path.Count > 1 ? HunterNavigationUtility.Doorway(_level.Graph,
                    path[path.Count - 2], cake.RoomId, cake.Position) : cake.Position;
            }
            List<int> exitPath = HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, _level.Graph.ExitRoomId, _state.UnavailableRoomIds);
            _state.ExitAvailable = _floor.ExitState == ExitState.Open && exitPath.Count > 0;
            _state.ExitTarget = exitPath.Count > 1 ? HunterNavigationUtility.Doorway(_level.Graph,
                exitPath[exitPath.Count - 2], _level.Graph.ExitRoomId, _level.Graph.ExitPosition) : _level.Graph.ExitPosition;
        }
        private static Vector3 RoomTarget(LevelRoom room) => new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
        private void TrackRooms()
        {
            if (!_level.IsReady || _level.Graph == null) return;
            foreach (LevelRoom room in _level.Graph.Rooms)
            {
                if (!room.Bounds.Contains(_state.Position)) continue;
                // Match RoomAt's first-containing-room rule at shared boundaries; never
                // alternate between overlapping bounds while a threshold pause holds still.
                if (room.Id == _state.LastRoom) break;
                if (HabitEnabled(HunterHabitKind.ThresholdPause) && !ActiveChase && HabitsAllowed)
                    foreach (LevelEdge edge in _level.Graph.Edges)
                        if ((edge.Access & TraversalAccess.Hunter) != 0 &&
                            ((edge.FromRoomId == _state.LastRoom && edge.ToRoomId == room.Id) ||
                            (edge.Bidirectional && edge.ToRoomId == _state.LastRoom && edge.FromRoomId == room.Id)))
                        {
                            _state.ThresholdPauseRemaining = Habit(HunterHabitKind.ThresholdPause).PauseSeconds;
                            EmitHabit(HunterHabitKind.ThresholdPause, _state.Position); break;
                        }
                _state.LastRoom = room.Id;
                // Following the first loss leg still follows prey through occlusion;
                // patrol, hint travel and the later sweep/return legs do not prove a loop.
                if (!_state.PlayerVisible && (_state.BeliefConfidence <= 0f || _state.HasHint ||
                    (_state.SearchActive && _state.SearchIndex > 0) ||
                    _state.Action == HunterAction.Patrol || _state.Action == HunterAction.Retreat)) break;
                _state.RecentRooms.Add(room.Id);
                if (_state.RecentRooms.Count > 8) _state.RecentRooms.RemoveAt(0);
                int visits = 0; foreach (int id in _state.RecentRooms) if (id == room.Id) visits++;
                _state.LoopDetected = visits >= 3; break;
            }
        }
    }
}
