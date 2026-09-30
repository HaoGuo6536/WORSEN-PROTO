// ============================================================================
// HunterRouteController.cs
// ============================================================================
// PURPOSE:
//   Chooses committed Hunter goals and their route targets from observed memory.
//   Search-leg budgets, reachable predictions and room exclusions remain separate
//   from sensing and attacks. No hidden player pose is used to refresh belief.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Build GOAP facts and choose bounded action commitments.
//   - Advance patrol, search and reachable interception targets.
//   - Resolve floor goal targets and closed-room exclusions.
// DEPENDENCIES:
//   - Hunter state/profile/contracts, habit tunable lookup and navigation utility.
//   - Core graph/planner values and injected Player/Level/Floor read-only views.
// USAGE NOTES:
//   Per-life pure logic. Delta time and the shared random stream are injected;
//   prediction draws occur only at the original commitment/route boundaries.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterRouteController
    {
        private readonly HunterBehaviorState _state;
        private readonly HunterProfile _profile;
        private readonly System.Random _random;
        private readonly IReadOnlyPlayerState _player;
        private readonly IReadOnlyLevelState _level;
        private readonly IHunterArchetypeController _archetype;
        private readonly HunterHabitController _habits;
        public HunterRouteController(HunterBehaviorState state, HunterProfile profile, System.Random random,
            IReadOnlyPlayerState player, IReadOnlyLevelState level, IHunterArchetypeController archetype, HunterHabitController habits)
        { _state = state; _profile = profile; _random = random; _player = player; _level = level; _archetype = archetype; _habits = habits; }
        private float Effective(HunterTunable tunable) => _habits.Effective(tunable);
        private bool Cursed(ProgressionTraits trait) => (_state.Traits & trait) != 0;
        public void SetRoomPhase(RoomPhaseChangedFact fact)
        {
            if (fact.Phase != RoomPhase.Closed || !_state.UnavailableRoomIds.Add(fact.RoomId) || _level.Graph == null) return;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (room.Id == fact.RoomId) { _state.UnavailableRooms.Add(room.Bounds); break; }
            _state.PlannedFacts = ulong.MaxValue; _state.HasPatrolTarget = false;
        }
        private bool Unavailable(Vector3 point)
        { foreach (Bounds room in _state.UnavailableRooms) if (room.Contains(point)) return true; return false; }
        public void Replan(IReadOnlyFloorState floor, float beliefAge, float attackDistance, float sightCone)
        {
            bool sharedAttack = !(_archetype is IHunterIndependentAttackRules weapon) || weapon.AllowSharedAttack;
            ulong facts = 0;
            if (_state.PlayerVisible) facts |= (ulong)HunterWorldFacts.PlayerVisible;
            if (_state.PlayerHeard) facts |= (ulong)HunterWorldFacts.PlayerHeard;
            if (_state.BeliefConfidence > 0f || _state.SearchActive) facts |= (ulong)HunterWorldFacts.HasBelief;
            if (!_state.SearchActive && _state.BeliefConfidence >= _profile.StalkMinimumConfidence &&
                beliefAge <= _profile.BeliefFreshSeconds) facts |= (ulong)HunterWorldFacts.BeliefFresh;
            bool reachableElevation = _profile.AttackStyle != HunterAttackStyle.Lunge ||
                Mathf.Abs(_state.Position.y - _player.Position.y) <= _profile.MaximumMeleeElevation;
            if (sharedAttack && _state.PlayerVisible && reachableElevation && !Unavailable(_state.Position) && !Unavailable(_player.Position) && Vector3.Distance(_state.Position, _player.Position) <= attackDistance)
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
            UpdateGoalTargets(floor);
            if (_state.CakeAvailable) facts |= (ulong)HunterWorldFacts.CakeAvailable;
            if (_state.ExitAvailable) facts |= (ulong)HunterWorldFacts.ExitOpen;
            var actions = new List<GoapActionDefinition>
            {
                Action(HunterAction.DenyCake, HunterWorldFacts.CakeAvailable, 0, HunterWorldFacts.RouteDenied, 1f),
                Action(HunterAction.ProtectExit, HunterWorldFacts.ExitOpen, 0, HunterWorldFacts.ExitProtected, 1f),
                Action(HunterAction.BreakLoop, HunterWorldFacts.LoopDetected | HunterWorldFacts.HasBelief,
                    HunterWorldFacts.PlayerVisible, HunterWorldFacts.LoopBroken, 1f),
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
                    HunterWorldFacts.InLungeRange, HunterWorldFacts.InLungeRange | HunterWorldFacts.LoopBroken, 1f),
                Action(HunterAction.Lunge, HunterWorldFacts.PlayerVisible | HunterWorldFacts.InLungeRange, 0, HunterWorldFacts.CaughtPlayer, 1f)
            };
            if (!sharedAttack) actions.RemoveAll(action => action.Id == (int)HunterAction.Lunge);
            if (_state.ActionFailed)
            {
                actions.RemoveAll(action => action.Id == (int)_state.Action);
                _state.HasPatrolTarget = false;
            }
            ulong goal = react ? (ulong)HunterWorldFacts.EscapedBeam : _state.PlayerVisible ?
                (ulong)(sharedAttack ? HunterWorldFacts.CaughtPlayer : HunterWorldFacts.InLungeRange) :
                _state.SearchActive || _state.BeliefConfidence > 0f || _state.LightMemoryRemaining > 0f ? (ulong)HunterWorldFacts.LocatedPlayer : (ulong)HunterWorldFacts.Patrolled;
            var goals = new[] {
                new GoapGoalDefinition((int)HunterGoal.LocatePrey, goal, react ? 200f : _state.PlayerVisible ? 100f :
                    _state.SearchActive ? 70f : _state.BeliefConfidence > 0f ? 20f + 80f * _state.BeliefConfidence : 10f),
                new GoapGoalDefinition((int)HunterGoal.DenyCake, (ulong)HunterWorldFacts.RouteDenied,
                    _state.CakeAvailable ? _profile.CakeGoalUtility : 0f),
                new GoapGoalDefinition((int)HunterGoal.ProtectExit, (ulong)HunterWorldFacts.ExitProtected,
                    _state.ExitAvailable ? _profile.ExitGoalUtility : 0f),
                new GoapGoalDefinition((int)HunterGoal.BreakLoop, (ulong)HunterWorldFacts.LoopBroken,
                    _state.LoopDetected && (facts & (ulong)HunterWorldFacts.InLungeRange) == 0 ? _profile.LoopGoalUtility : 0f) };
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
            }
            // Draw once per commitment, not every visible-prey sample.
            _state.Predict = chosen == HunterAction.Chase && _state.PlayerVisible &&
                _state.ObservedPlayerVelocity.sqrMagnitude > 0.0001f &&
                _level.IsReady && _level.Graph != null && _profile.PredictionChance > 0f &&
                _random.NextDouble() < _profile.PredictionChance;
            _state.PredictionRoute.Clear();
            if (_state.Predict)
            {
                _state.NavigationTarget = PursuitTarget(sightCone);
                _state.Predict = _state.NavigationTarget != _state.LastKnownPosition;
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
        public void UpdateTarget(float dt, IReadOnlyFloorState floor)
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
            {
                if (!_state.Predict) _state.NavigationTarget = _state.LastKnownPosition;
            }
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
                    floor?.ActiveCakeAnchors, floor?.ExitState == ExitState.Open, _state.LastPickupRoom, _state.UnavailableRoomIds);
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
        private Vector3 PursuitTarget(float sightCone)
        {
            if (_state.ObservedPlayerVelocity.sqrMagnitude <= 0.0001f) return _state.LastKnownPosition;
            Vector3 candidate = InterceptRoom();
            Vector3 observed = _state.LastKnownPosition - _state.Position; observed.y = 0f;
            Vector3 aim = candidate - _state.Position; aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f && Vector3.Angle(observed, aim) < sightCone * 0.5f ?
                candidate : _state.LastKnownPosition;
        }
        private Vector3 InterceptRoom()
        {
            if (!_level.IsReady || _level.Graph == null || _state.LastRoom == 0) return _state.LastKnownPosition;
            float horizon = _state.Action == HunterAction.Chase ? _profile.ChasePredictionSeconds : _profile.CutOffPredictionSeconds;
            Vector3 predicted = _state.LastKnownPosition + _state.ObservedPlayerVelocity * horizon * (Cursed(ProgressionTraits.WatcherCuttingCorners) ? 1.7f : 1f);
            Vector3 best = _state.LastKnownPosition; float score = float.PositiveInfinity; int bestRoom = 0;
            foreach (LevelRoom room in _level.Graph.Rooms)
                if (Vector3.SqrMagnitude(RoomTarget(room) - predicted) < score &&
                    HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, room.Id, _state.UnavailableRoomIds).Count > 0)
                { best = RoomTarget(room); bestRoom = room.Id; score = Vector3.SqrMagnitude(best - predicted); }
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
        private void UpdateGoalTargets(IReadOnlyFloorState floor)
        {
            _state.CakeAvailable = false; _state.ExitAvailable = false;
            if (floor == null || !floor.IsReady || !_level.IsReady || _level.Graph == null) return;
            foreach (var room in floor.RoomPhases)
                if (room.Value == RoomPhase.Closed) SetRoomPhase(new RoomPhaseChangedFact(room.Key, room.Value, _state.Tick));
            float nearest = float.PositiveInfinity;
            foreach (LevelAnchor cake in floor.ActiveCakeAnchors)
            {
                float distance = Vector3.SqrMagnitude(cake.Position - _state.Position);
                List<int> path = HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, cake.RoomId, _state.UnavailableRoomIds);
                if (distance >= nearest || path.Count == 0) continue;
                nearest = distance; _state.CakeAvailable = true;
                _state.CakeTarget = path.Count > 1 ? HunterNavigationUtility.Doorway(_level.Graph,
                    path[path.Count - 2], cake.RoomId, cake.Position) : cake.Position;
            }
            List<int> exitPath = HunterNavigationUtility.Path(_level.Graph, _state.LastRoom, _level.Graph.ExitRoomId, _state.UnavailableRoomIds);
            _state.ExitAvailable = floor.ExitState == ExitState.Open && exitPath.Count > 0;
            _state.ExitTarget = exitPath.Count > 1 ? HunterNavigationUtility.Doorway(_level.Graph,
                exitPath[exitPath.Count - 2], _level.Graph.ExitRoomId, _level.Graph.ExitPosition) : _level.Graph.ExitPosition;
        }
        private static Vector3 RoomTarget(LevelRoom room) => new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
    }
}
