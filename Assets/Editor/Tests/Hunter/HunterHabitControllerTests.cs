// ============================================================================
// HunterHabitControllerTests.cs
// ============================================================================
// PURPOSE:
//   Tests extracted habits directly with explicit managed profile inputs.
//   The fixture checks that interruption gates and once-only observations survive
//   separation from the shared tick without constructing any engine object.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify loss beats, catch suppression, mutation admission and threshold facts.
// DEPENDENCIES:
//   - Hunter habit logic, Core values, Player state and shared managed test inputs.
// USAGE NOTES:
//   No Unity object construction. Existing HunterHabitMutationTests retain their
//   original assertions and remain the coordinator's native-default regression suite.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using static Worsen.Tests.Hunter.HunterAttackControllerTests;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterHabitControllerTests
    {
        private HunterBehaviorState state;
        private HunterProfile profile;
        private HunterHabitController habits;
        [SetUp] public void Setup()
        {
            state = State(); profile = Profile(); Set(state, "PlayerVisible", false);
            var world = new EchoControllerTests.World(); world.TwoRooms();
            habits = new HunterHabitController(state, profile, new PlayerBehaviorState { Health = 100, SprintSpeed = 8 }, world, new HunterArchetypeController());
        }
        [Test] public void LossBeatEmitsOnceAndCatchClearsPendingHabits()
        {
            Set(state, "LastKnownPosition", Vector3.right * 3); habits.BeginLossBeat();
            Assert.That(habits.TryTakeHabit(out var fact), Is.True); Assert.That(fact.Kind, Is.EqualTo(HunterHabitKind.TurnToFace));
            Assert.That(habits.TryTakeDeliberation(out var point), Is.True); Assert.That(point, Is.EqualTo(Vector3.right * 3));
            habits.BeginLossBeat(); Assert.That(habits.TryTakeHabit(out _), Is.False);
            habits.SetCatchActive(true); Assert.That(state.IsDeliberating, Is.False);
            Assert.That(habits.TryTakeDeliberation(out _), Is.False);
        }
        [Test] public void FreshDeliberationChangesFacingWithoutRestartingTimer()
        {
            habits.BeginDeliberation(Vector3.right); Set(state, "DeliberationRemaining", .2f);
            habits.BeginDeliberation(Vector3.left);
            Assert.That(Get<float>(state, "DeliberationRemaining"), Is.EqualTo(.2f));
            Assert.That(habits.TryTakeDeliberation(out var point), Is.True); Assert.That(point, Is.EqualTo(Vector3.left));
        }
        [Test] public void MutationIsIdempotentAndRejectsNonFiniteValues()
        {
            var mutation = new HunterMutation(HunterTunable.Acceleration, 30f, "fixture-tell");
            Assert.That(habits.ApplyMutation(mutation, out _), Is.True); Assert.That(habits.Effective(HunterTunable.Acceleration), Is.EqualTo(30f));
            Assert.That(habits.ApplyMutation(mutation, out _), Is.False); Assert.That(profile.Acceleration, Is.EqualTo(20f));
            Assert.That(habits.ApplyMutation(new HunterMutation(HunterTunable.Acceleration, float.NaN, "fixture-tell"), out _), Is.False);
        }
        [Test] public void ConnectedThresholdEmitsOnlyOnEntryAndNotDuringChase()
        {
            Set(state, "LastRoom", 1); Set(state, "Position", Vector3.right * 4);
            habits.TrackRooms(); Assert.That(habits.TryTakeHabit(out var fact), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(HunterHabitKind.ThresholdPause));
            habits.TrackRooms(); Assert.That(habits.TryTakeHabit(out _), Is.False);
            habits.SetChaseActive(true); Set(state, "Position", Vector3.zero); habits.TrackRooms();
            Assert.That(habits.TryTakeHabit(out _), Is.False); Assert.That(Get<float>(state, "ThresholdPauseRemaining"), Is.Zero);
        }
    }
}
