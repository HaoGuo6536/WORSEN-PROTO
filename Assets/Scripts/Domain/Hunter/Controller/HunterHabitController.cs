// ============================================================================
// HunterHabitController.cs
// ============================================================================
// PURPOSE:
//   Owns the learnable pauses, facing beats and pickup reactions of a Hunter.
//   Mutation lookup and admission live beside the habits they can enable. The
//   coordinator retains tick ordering while all per-life data remains in its state.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Resolve authored values and admit bounded, idempotent mutations.
//   - Gate habit facts during chase, catch and committed attacks.
//   - Track floor pickups and connected-room threshold/loop observations.
//   - Start and consume bounded deliberation and loss beats.
// DEPENDENCIES:
//   - Hunter state/config/contracts, Core facts and injected Player/Level/Floor views.
// USAGE NOTES:
//   Pure, per-life logic. No engine calls, clock reads or random draws. Floor
//   removals remain observations, not proof of a pickup rather than collapse.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterHabitController
    {
        private readonly HunterBehaviorState _state;
        private readonly HunterProfile _profile;
        private readonly IReadOnlyPlayerState _player;
        private readonly IReadOnlyLevelState _level;
        private readonly IHunterArchetypeController _archetype;
        public HunterHabitController(HunterBehaviorState state, HunterProfile profile, IReadOnlyPlayerState player,
            IReadOnlyLevelState level, IHunterArchetypeController archetype)
        { _state = state; _profile = profile; _player = player; _level = level; _archetype = archetype; }
        public bool ActiveChase => !((_archetype as IHunterDormancyRules)?.Dormant ?? false) && (_state.ChaseActive || _state.PlayerVisible);
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
        public void BeginLossBeat()
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
            if (Effective(mutation.Tunable) == mutation.Value) return false;
            _state.Mutations[mutation.Tunable] = mutation.Value;
            _state.LossSeconds = _archetype.NeverLoses ? float.PositiveInfinity : Effective(HunterTunable.LossSeconds);
            _state.LossDistance = _archetype.NeverLoses ? float.PositiveInfinity : Effective(HunterTunable.LossDistance);
            if (mutation.Tunable == HunterTunable.ThresholdPauseEnabled && mutation.Value == 0f) _state.ThresholdPauseRemaining = 0f;
            fact = new HunterMutationFact(_state.Id, _profile.ArchetypeKey, mutation.TellId, _state.Tick, mutation);
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void BeginDeliberation(Vector3 target)
        {
            if (_state.LungePhase != HunterLungePhase.None || _state.CatchActive || _state.ChaseActive) return;
            _state.DeliberationTarget = target;
            if (_state.IsDeliberating) return;
            _state.DeliberationRemaining = _profile.DeliberationSeconds;
            _state.DeliberationFactPending = _state.IsDeliberating;
        }
        public bool TryTakeDeliberation(out Vector3 candidate)
        {
            candidate = _state.DeliberationTarget;
            bool pending = _state.DeliberationFactPending;
            _state.DeliberationFactPending = false; return pending;
        }
        public void UpdateFloorMemory(IReadOnlyFloorState floor)
        {
            if (floor == null || !floor.IsReady) return;
            var present = new HashSet<int>();
            foreach (LevelAnchor cake in floor.ActiveCakeAnchors) present.Add(cake.Id);
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
            foreach (LevelAnchor cake in floor.ActiveCakeAnchors)
            { _state.CakeRooms[cake.Id] = cake.RoomId; _state.CakePositions[cake.Id] = cake.Position; }
        }
        public void TrackRooms()
        {
            if (!_level.IsReady || _level.Graph == null) return;
            foreach (LevelRoom room in _level.Graph.Rooms)
            {
                if (!room.Bounds.Contains(_state.Position)) continue;
                // Match RoomAt's first-containing-room rule at shared boundaries.
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
