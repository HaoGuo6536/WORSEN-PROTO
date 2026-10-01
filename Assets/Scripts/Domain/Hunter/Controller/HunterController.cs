// ============================================================================
// HunterController.cs
// ============================================================================
// PURPOSE:
//   Coordinates Hunter sensing, interruptions and the ordered decision tick.
//   Focused route, attack and habit Controllers own goal commitments, contact
//   admission and learnable beats while this entry point preserves their ordering.
// ARCHITECTURAL ROLE:
//   Controller (section 2) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Maintain observable sight, hearing, light and imperfect pursuit memory.
//   - Coordinate route commitments and retreat with attack and habit decisions.
//   - Sequence archetypes, with an opt-in replay bypass of sensing/planning/reactions.
//   - Route mutations and contact admission to their focused Controllers.
//   - Publish bounded movement decisions and Core attack/feedback facts.
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
//   Kinematic replay has no lunge or catch hold. Contacts remain ordinary Player hit
//   candidates; Player owns post-hit grace and the replay never changes its clock.
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
        private readonly HunterAttackController _attack;
        private readonly HunterHabitController _habits;
        private readonly HunterRouteController _routes;
        public bool CatchActive => _state.CatchActive;
        public IHunterKinematicReplayRules KinematicReplay => _archetype as IHunterKinematicReplayRules;
        public bool PursuitSuppressed => _state.PursuitSuppressed;
        public HunterPlayerView PlayerView => _state.PlayerView;
        private IHunterObservationRules ObservationRules => _archetype as IHunterObservationRules;
        public bool Silent => (ObservationRules?.Silent ?? false) || _archetype is IHunterContactRules;
        public bool NeedsViewObservation => ObservationRules != null;
        public bool ArchetypeHeld => ObservationRules?.Hold ?? false;
        public bool PlayerRevivalProtected => _attack.PlayerRevivalProtected;
        public void ObservePlayerView(bool clear) { _state.PlayerViewClear = clear; }
        public bool Dormant => (_archetype as IHunterDormancyRules)?.Dormant ?? false;
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
            _floor, KinematicReplay != null ? _closedDoors : ArchetypeDoors(), _interactables, _effects, _state.DeltaTime, _state.Tick,
            !_state.ReactionHeld && !_state.CatchActive && _state.LungePhase == HunterLungePhase.None,
            Effective(HunterTunable.ChaseSpeedMultiplier) * _state.RunSpeedMultiplier, _state.UnavailableRooms);
        private IReadOnlyDictionary<int, bool> ArchetypeDoors()
        {
            if (_closedDoors == null || _interactables == null || _state.WorldView?.JammedDoors == null) return _closedDoors;
            Dictionary<int, bool> doors = null;
            foreach (HunterDoorJam jam in _state.WorldView.JammedDoors)
                if (_interactables.TryGet(jam.DoorId, out InteractableState door) && door.EdgeId >= 0)
                {
                    if (doors == null) { doors = new Dictionary<int, bool>(); foreach (var pair in _closedDoors) doors.Add(pair.Key, pair.Value); }
                    doors[door.EdgeId] = false; // Shared reaction gate owns this crossing, not replay truncation.
                }
            return doors ?? _closedDoors;
        }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { _effects = effects; }
        public void SetWorldView(IReadOnlyHunterWorldView world) { _state.WorldView = world; }
        public void SetPlayerView(HunterPlayerView view) { _state.PlayerView = view; }
        public void SetWickActive(bool active)
        {
            _state.WickActive = active;
            ObservationRules?.Observe(_state.PlayerView, _state.PlayerViewClear, _state.DirectlyIlluminated, _state.WorldView, active);
        }
        public bool ReactionHeld => _state.ReactionHeld;
        public void ApplyStun(float seconds, float strength)
        {
            if (KinematicReplay != null) return;
            if (!Finite(seconds) || !Finite(strength) || seconds <= 0f || strength <= 0f) return;
            // Full strength freezes; weaker hits are proportionally shorter flinches.
            _state.StunRemaining = Mathf.Max(_state.StunRemaining, seconds * Mathf.Clamp01(strength));
            CancelReactionAttack();
        }
        public void ApplySlip(float seconds)
        {
            if (KinematicReplay != null) return;
            if (!Finite(seconds) || seconds <= 0f) return;
            _state.SlipRemaining = Mathf.Max(_state.SlipRemaining, seconds);
            CancelReactionAttack();
        }
        private void CancelReactionAttack()
        {
            _state.Velocity = Vector3.zero; _state.LungePhase = HunterLungePhase.None;
            _state.PhaseSeconds = 0f; _state.AttackBecameActive = false;
            _state.FiredRangedAttacks.Clear(); _state.Feedback.Clear();
            _state.PlannedFacts = ulong.MaxValue;
        }
        public HunterTickResult HoldMotion()
        {
            CancelReactionAttack();
            return new HunterTickResult(_state.Position, 0f, HunterLungePhase.None, Vector3.zero, false, false, true);
        }
        public IReadOnlyList<Vector3> ReactionPath(HunterTickResult result, IReadOnlyList<Vector3> route)
        {
            if (result.Phase != HunterLungePhase.None)
                return new[] { _state.Position, _state.Position + result.LungeDirection * EffectiveAttackDistance };
            if (_archetype.ReplayPath == null) return route;
            var points = new List<Vector3> { _state.Position }; points.AddRange(_archetype.ReplayPath); return points;
        }
        public bool BlockJammedPath(IReadOnlyList<Vector3> path, float dt, Vector3? intendedTarget = null)
        {
            if (KinematicReplay != null) return false;
            if (!Finite(dt) || dt <= 0f || _state.CatchActive) return false;
            var jams = _state.WorldView?.JammedDoors;
            bool retained = false;
            if (jams != null) foreach (HunterDoorJam jam in jams)
                if (jam.DoorId == _state.BreakingDoor && jam.Revision == _state.DoorRevision) { retained = true; break; }
            if (!retained) _state.BreakingDoor = 0;
            HunterDoorJam selected = default;
            if (jams != null) foreach (HunterDoorJam jam in jams)
            {
                if (jam.DoorId <= 0 || !Finite(jam.BreakSeconds) || jam.BreakSeconds < 0f || !Finite(jam.Bounds.center) || !Finite(jam.Bounds.size)) continue;
                if (_state.BreakingDoor == jam.DoorId && _state.DoorRevision == jam.Revision)
                { selected = jam; break; }
                if (_state.BreakingDoor != 0 || path == null || path.Count < 2 ||
                    Vector3.Distance(_state.Position, jam.Bounds.ClosestPoint(_state.Position)) > _profile.DoorBreakReach) continue;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector3 delta = path[i] - path[i - 1];
                    if (jam.Bounds.Contains(path[i - 1]) || jam.Bounds.Contains(path[i]) ||
                        (delta.sqrMagnitude > 0f && jam.Bounds.IntersectRay(new Ray(path[i - 1], delta.normalized), out float distance) && distance <= delta.magnitude))
                    { selected = jam; break; }
                }
                // A carved/closed door can terminate a partial path on its near face.
                if (selected.DoorId == 0 && intendedTarget.HasValue &&
                    Vector3.Distance(path[path.Count - 1], jam.Bounds.ClosestPoint(path[path.Count - 1])) <= _profile.DoorBreakReach)
                {
                    Vector3 end = path[path.Count - 1], delta = intendedTarget.Value - end;
                    if (delta.sqrMagnitude > 0f && jam.Bounds.IntersectRay(new Ray(end, delta.normalized), out float distance) && distance <= delta.magnitude) selected = jam;
                }
                if (selected.DoorId != 0) break;
            }
            if (selected.DoorId == 0)
            { _state.BreakingDoor = 0; _state.DoorBreakPublished = false; return false; }
            if (_state.BreakingDoor == 0)
            {
                _state.BreakingDoor = selected.DoorId; _state.DoorRevision = selected.Revision;
                _state.DoorBreakRemaining = selected.BreakSeconds; _state.DoorBreakPublished = false;
            }
            if (!_state.ReactionHeld) _state.DoorBreakRemaining = Mathf.Max(0f, _state.DoorBreakRemaining - dt);
            if (!_state.DoorBreakPublished && _state.DoorBreakRemaining <= 0.000001f)
            {
                _state.DoorBreakPublished = true;
                _state.DoorBreakFacts.Enqueue(new HunterDoorBreakFact(_state.Id, selected.DoorId, selected.Revision, _state.Tick));
            }
            CancelReactionAttack(); return true;
        }
        public bool TryTakeDoorBreak(out HunterDoorBreakFact fact)
        { fact = default; if (_state.DoorBreakFacts.Count == 0) return false; fact = _state.DoorBreakFacts.Dequeue(); return true; }
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
            _archetype = archetype ?? new HunterArchetypeController();
            _attack = new HunterAttackController(state, profile, random, player, _archetype);
            _habits = new HunterHabitController(state, profile, player, level, _archetype);
            _routes = new HunterRouteController(state, profile, random, player, level, _archetype, _habits);
        }
        public void Reset(EntityId id, Vector3 position, Vector3 forward)
        {
            _state.StunRemaining = _state.SlipRemaining = _state.DoorBreakRemaining = 0f;
            _state.ReactionHeld = _state.WickActive = _state.DoorBreakPublished = false;
            _state.BreakingDoor = 0; _state.DoorBreakFacts.Clear(); _state.WorldView = null; _state.PlayerView = default;
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
            if (KinematicReplay != null) _state.IsActive = KinematicReplay.ReplayActive;
            if (_archetype.NeverLoses) { _state.LossSeconds = float.PositiveInfinity; _state.LossDistance = float.PositiveInfinity; }
            RefreshDormancy();
        }
        public bool ShouldProbe(long tick) => KinematicReplay == null && !_state.CatchActive &&
            (!_state.SensorInitialized || tick % Math.Max(1, _profile.SensorIntervalTicks) == 0);
        public void ClearBelief()
        {
            _state.PlayerVisible = false; _state.PlayerHeard = false; _state.HasHint = false;
            _state.LastKnownPosition = _state.Position; _state.LastKnownTick = 0;
            _state.BeliefConfidence = 0f; _state.BeliefInitialConfidence = 0f;
            _state.BeliefReferenceTick = _state.Tick; _state.BeliefAgeAtReference = 0f;
            _state.SearchRoute.Clear(); _state.SearchActive = false; _state.SearchIndex = 0;
            _state.SearchSeconds = 0f; _state.SearchLegBudget = 0f;
            _state.PredictionRoute.Clear(); _state.PredictionIndex = 0; _state.Predict = false;
            _state.ObservedPlayerRoom = 0; _state.PreviousPlayerRoom = 0; _state.ObservedPlayerVelocity = Vector3.zero;
            _state.PendingNoiseDecision = false; _state.PendingNoise = default; _state.PendingNoiseLoudness = 0f;
            _state.DeliberationRemaining = 0f; _state.DeliberationFactPending = false; _state.DeliberationTarget = _state.Position;
            _state.CommitmentRemaining = 0f; _state.ThresholdPauseRemaining = 0f; _state.HabitFacts.Clear();
            _state.ChaseActive = false; _state.LossHabitObserved = false; _state.LastPickupRoom = 0;
            _state.RecentRooms.Clear(); _state.LoopDetected = false;
            _state.LightMemoryRemaining = 0f; _state.LightReactionRemaining = 0f; _state.LightExposure = 0f;
            _state.LightObserved = false; _state.DirectlyIlluminated = false; _state.LastLightPosition = _state.Position;
            _state.Afterimage = default; _state.AfterimageRemaining = 0f;
            _state.PursuitSuppressed = false; _state.RetreatRemaining = 0f;
            _state.NavigationTarget = _state.Position; _state.HasPatrolTarget = false;
            _state.PlannedFacts = ulong.MaxValue; _state.ActionFailed = false; _state.CurrentGoal = HunterGoal.LocatePrey;
            if (_state.LungePhase == HunterLungePhase.None) _state.Action = HunterAction.Patrol;
            // Keep heard-noise deduplication: old Player.RecentNoises must not recreate the erased belief.
            // Attack phase, direction, serial, contact admission and recovery remain untouched.
        }
        public HunterTickResult Tick(SightProbe probe, float dt, long tick)
            => Tick(probe, default, dt, tick);
        public HunterTickResult Tick(SightProbe probe, HunterLightObservation light, float dt, long tick)
        {
            if (!(dt > 0f) || float.IsNaN(dt) || float.IsInfinity(dt)) return default;
            if (KinematicReplay != null)
            {
                _state.Tick = tick; _state.DeltaTime = dt;
                _archetype.Tick(ArchetypeContext);
                _state.IsActive = KinematicReplay.ReplayActive;
                _state.NavigationTarget = KinematicReplay.ReplayPose.Position;
                return new HunterTickResult(_state.NavigationTarget, 0f, HunterLungePhase.None,
                    Vector3.zero, false, false, !_state.IsActive);
            }
            float beliefAgeBefore = BeliefAge(_state.DeltaTime, _state.Tick);
            _state.AttackBecameActive = false;
            _state.Tick = tick; _state.DeltaTime = dt;
            _state.ReactionHeld = _state.StunRemaining > 0f || _state.SlipRemaining > 0f;
            _state.StunRemaining = Mathf.Max(0f, _state.StunRemaining - dt);
            _state.SlipRemaining = Mathf.Max(0f, _state.SlipRemaining - dt);
            ObservationRules?.Observe(_state.PlayerView, _state.PlayerViewClear,
                light.Tick == tick && light.Illuminated, _state.WorldView, _state.WickActive);
            _archetype.Tick(ArchetypeContext); // Record prey even while an interruption prevents playback.
            if (_state.ReactionHeld)
            {
                // Move the memory reference forward so the interruption does not age it out.
                _state.BeliefAgeAtReference = beliefAgeBefore; _state.BeliefReferenceTick = tick;
                return HoldMotion();
            }
            if (ObservationRules != null)
            {
                _state.LossSeconds = Effective(HunterTunable.LossSeconds) * ObservationRules.LossMultiplier;
                _state.LossDistance = Effective(HunterTunable.LossDistance) * ObservationRules.LossMultiplier;
            }
            RefreshDormancy();
            if (ArchetypeHeld) return HoldMotion();
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
            Vector3 stumble = _attack.Advance(dt, out float stumbleSeconds);
            if (_state.LungePhase == HunterLungePhase.None && !_state.AttackBecameActive && stumbleSeconds <= 0f)
            {
                Replan();
                if (!_archetype.TryMovement(out Vector3 movementTarget, out _)) UpdateTarget(dt);
                else _state.NavigationTarget = movementTarget;
                begin = _attack.TryBegin();
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
            return new HunterTickResult(_state.NavigationTarget, speed * _state.RunSpeedMultiplier * (ObservationRules?.SpeedMultiplier ?? 1f), _state.LungePhase,
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
        public float EffectiveSightRange => _profile.SightRange * (Cursed(ProgressionTraits.WatcherUnquietGaze) ? 1.25f : 1f) *
            (_state.WickActive ? _profile.WickSightMultiplier : 1f);
        public float EffectiveSightCone => _profile.SightConeDegrees * (Cursed(ProgressionTraits.LurkerDarkAdaptation) ? 1.3f : 1f);
        public float WindupDuration => _attack.Duration(HunterLungePhase.Windup);
        public float ProjectileSpeed => _profile.ProjectileSpeed * (Cursed(ProgressionTraits.HexerLingeringHex) ? 0.65f : 1f);
        public float ProjectileRadius => _profile.ProjectileRadius * (Cursed(ProgressionTraits.HexerLingeringHex) ? 1.7f : 1f);
        public float SpikeRadius => _profile.SpikeRadius * (Cursed(ProgressionTraits.ThorncallerReachingRoots) ? 1.4f : 1f);
        public bool SplitBolt => Cursed(ProgressionTraits.HexerSplitBolt);
        public bool ThornRing => Cursed(ProgressionTraits.ThorncallerThornRing);
        public IReadOnlyList<Bounds> UnavailableRooms => _state.UnavailableRooms;
        public float EffectiveAcceleration => Effective(HunterTunable.Acceleration);
        public float EffectiveTurnRate => Effective(HunterTunable.TurnRate);
        public bool ActiveChase => _habits.ActiveChase;
        public bool PreferEmergence => _profile.EmergenceBias && !ActiveChase && !_state.CatchActive &&
            _state.LungePhase == HunterLungePhase.None && (_state.Action == HunterAction.Stalk ||
            _state.Action == HunterAction.InvestigateHint || _state.Action == HunterAction.SearchLastKnown);
        public bool LookAtMemory => _state.IsDeliberating || _state.Action == HunterAction.SearchLastKnown || _state.SearchActive;
        public Vector3 LookTarget => _state.IsDeliberating ? _state.DeliberationTarget : _state.LastKnownPosition;
        public void SetChaseActive(bool active) => _habits.SetChaseActive(active);
        public void SetCatchActive(bool active) { if (KinematicReplay == null) _habits.SetCatchActive(active); }
        public bool TryTakeHabit(out HunterHabitFact fact) => _habits.TryTakeHabit(out fact);
        private void BeginLossBeat() => _habits.BeginLossBeat();
        public float Effective(HunterTunable tunable) => _habits.Effective(tunable);
        public bool ApplyMutation(HunterMutation mutation, out HunterMutationFact fact)
            => _habits.ApplyMutation(mutation, out fact);
        public void SetRoomPhase(RoomPhaseChangedFact fact) => _routes.SetRoomPhase(fact);
        public void SetTraits(ProgressionTraits traits)
        {
            // Legacy flags belong only to the default compatibility machinery. A
            // borrowed legacy profile/prefab must not leak flags into a new module.
            if (_profile.ArchetypeRules != null || !(_archetype is IHunterLegacyRules legacy) || !legacy.AllowsLegacyTraits)
                traits = ProgressionTraits.None;
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
        public bool HearNoise(NoiseEvent noise, float transmission, bool floorWide = false)
        {
            if (KinematicReplay != null) return false;
            if (!HunterHearingUtility.Allows(noise)) return false;
            if (!floorWide && (_archetype.OwnsPursuit || _state.PursuitSuppressed || noise.Tick > _state.Tick)) return false;
            if (!_state.IsActive || noise.Source == _state.Id || noise.Tick < 0 || _state.HeardNoises.Contains(noise) ||
                !Finite(noise.Position) || !Finite(noise.Loudness) || !Finite(transmission)) return false;
            float age = (_state.Tick - noise.Tick) * _state.DeltaTime;
            if (age - Mathf.Abs(age) * 1.1920929e-7f > _profile.NoiseMaxAgeSeconds * (Cursed(ProgressionTraits.RusherBloodScent) ? 2f : 1f)) return false;
            float range = _profile.HearingRange * (Cursed(ProgressionTraits.RusherBloodScent) ? 1.35f : 1f);
            float loudness = noise.Loudness;
            if (!floorWide)
            {
                if (!_level.IsReady || _level.Graph == null || Vector3.Distance(noise.Position, _state.Position) >= range) return false;
                HearingSample heard = AcousticOcclusionUtility.Sample(_level.Graph, HunterNavigationUtility.RoomAt(_level.Graph, noise.Position),
                    noise.Position, HunterNavigationUtility.RoomAt(_level.Graph, _state.Position), _state.Position,
                    noise.Loudness * Mathf.Clamp01(transmission), _profile.HearingModel, _closedDoors);
                if (!heard.Audible) return false;
                loudness = heard.PerceivedLoudness;
            }
            _state.HeardNoises.Add(noise); _state.LastNoiseTick = noise.Tick;
            // Hearing does not override Echo playback or wind a dormant Ticking down.
            if (_archetype.OwnsPursuit || _state.PursuitSuppressed) return true;
            if (_state.PlayerVisible || _state.LungePhase != HunterLungePhase.None) return true;
            if (_state.PendingNoiseDecision && loudness <= _state.PendingNoiseLoudness) return true;
            _state.PendingNoise = noise; _state.PendingNoiseLoudness = loudness;
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
        private void BeginDeliberation(Vector3 target) => _habits.BeginDeliberation(target);
        public bool TryTakeDeliberation(out Vector3 candidate) => _habits.TryTakeDeliberation(out candidate);
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
            => _attack.TryAcceptRangedContact(target, attackSerial, out hit);
        public void ReportAttackMiss(int attackSerial)
            => _attack.ReportAttackMiss(attackSerial);
        public void SetFlashlight(FlashlightSample sample)
        { if (sample.Source == _state.TargetId) _state.Flashlight = sample; }
        public bool TryDequeueFeedback(out HunterFeedbackEvent feedback)
        {
            feedback = default;
            if (!_player.IsAlive || Silent) { _state.Feedback.Clear(); return false; }
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
        public float LungeSpeed => _profile.LungeSpeed * _state.RunSpeedMultiplier * (ObservationRules?.SpeedMultiplier ?? 1f);
        public void ApplyRunSpeedMultiplier(float multiplier)
        {
            if (!Finite(multiplier) || multiplier <= 0f || !Finite(_profile.PatrolSpeed * multiplier)
                || !Finite(_profile.InvestigateSpeed * multiplier)
                || !Finite(_player.SprintSpeed * Effective(HunterTunable.ChaseSpeedMultiplier) * multiplier)
                || !Finite(_profile.LungeSpeed * multiplier))
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Hunter run speed must be finite and positive.");
            _state.RunSpeedMultiplier = multiplier;
        }
        public HunterAttackSample AttackSample() => _attack.AttackSample();
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
            // A failed guess does not disprove the route to visible prey. Try the
            // observed position for a full commitment before excluding Chase, even
            // if the failed guess was reported on the last tick of its commitment.
            if (_state.Action == HunterAction.Chase && _state.Predict && _state.PlayerVisible)
            {
                _state.Predict = false; _state.PredictionRoute.Clear();
                _state.NavigationTarget = _state.LastKnownPosition;
                _state.CommitmentRemaining = Effective(HunterTunable.ActionCommitmentSeconds);
                return;
            }
            _state.ActionFailed = true;
            if (_state.Action == HunterAction.AvoidLight || _state.Action == HunterAction.FlankLight)
            { _state.LightReactionRemaining = 0f; _state.LightExposure = 0f; }
        }
        public bool TryAcceptContact(EntityId target, out HunterHit hit)
        {
            if (KinematicReplay == null) return _attack.TryAcceptContact(target, out hit);
            hit = default;
            if (!_state.IsActive || !_player.IsAlive || PlayerRevivalProtected || target != _state.TargetId ||
                !KinematicReplay.ContactReady) return false;
            KinematicReplay.CommitContact();
            hit = new HunterHit(_state.Id, target, _profile.LungeDamage, _state.Tick, _state.Position,
                ChaseEndReason.Unknown, HitSeverity.Heavy, HitSource.Other);
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
                foreach (NoiseEvent noise in _player.RecentNoises)
                    if (noise.SourceKind != NoiseSourceKind.Heartbeat) HearNoise(noise, 1f);
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

        private void Replan() => _routes.Replan(_floor, BeliefAge(_state.DeltaTime, _state.Tick), EffectiveAttackDistance, EffectiveSightCone);
        private void UpdateTarget(float dt) => _routes.UpdateTarget(dt, _floor);
        private void UpdateFloorMemory() => _habits.UpdateFloorMemory(_floor);
        private void TrackRooms() => _habits.TrackRooms();
    }
}
