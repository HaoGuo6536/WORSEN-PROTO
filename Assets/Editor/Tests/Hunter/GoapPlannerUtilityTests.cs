// ============================================================================
// GoapPlannerUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks Hunter planning against small graphs with independently known routes.
//   The fixtures distinguish cheapest plans, blocked actions and unreachable goals
//   so fixed action sequences or incomplete searches cannot masquerade as success.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify action requirements/effects, optimal cost and deterministic ties.
//   - Verify explicit bounded/unreachable results and malformed-input rejection.
//   - Verify Stalk fact requirements, preserved action ordinals and loop-free plans.
//   - Verify utility ordering shares one expansion budget across candidate goals.
// DEPENDENCIES:
//   - Hunter pure planner and definitions; NUnit test framework.
// USAGE NOTES:
//   No scene objects, engine queries, wall-clock time or random source are needed.
//   Goal priorities and replan triggers are owned by HunterController's test suite.
// ============================================================================
using System;
using NUnit.Framework;
using Worsen.Domain.Hunter;

namespace Worsen.Tests.Hunter
{
    public sealed class GoapPlannerUtilityTests
    {
        private const ulong Visible = 1, Near = 2, Caught = 4, Hint = 8;

        [Test] public void UtilitySelectsHighestReachableGoalAndBoundsTotalSearch()
        {
            var actions = new[] { new GoapActionDefinition(1, 0, 0, Near, 0, 1f) };
            var goals = new[] { new GoapGoalDefinition(10, Near, 2f), new GoapGoalDefinition(20, Caught, 3f) };
            var result = GoapPlannerUtility.Select(0, goals, actions, out int goal, 8);
            Assert.That(goal, Is.EqualTo(10)); Assert.That(result.ActionIds, Is.EqualTo(new[] { 1 }));
            var bounded = GoapPlannerUtility.Select(0, goals, actions, out _, 1);
            Assert.That(bounded.Status, Is.EqualTo(GoapPlanStatus.SearchLimitReached));
            Assert.That(bounded.ExpandedStates, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void StalkIsAppendedWithoutChangingRecordedActionOrdinals()
        {
            var original = new[] { HunterAction.Patrol, HunterAction.InvestigateHint, HunterAction.Chase,
                HunterAction.Lunge, HunterAction.SearchLastKnown, HunterAction.CutOff, HunterAction.InvestigateLight,
                HunterAction.AvoidLight, HunterAction.FlankLight };
            for (int i = 0; i < original.Length; i++) Assert.That((int)original[i], Is.EqualTo(i));
            Assert.That((int)HunterAction.Stalk, Is.EqualTo(original.Length));
        }

        [TestCase(HunterWorldFacts.HasBelief | HunterWorldFacts.BeliefFresh, HunterAction.Stalk)]
        [TestCase(HunterWorldFacts.HasBelief, HunterAction.SearchLastKnown)]
        [TestCase(HunterWorldFacts.HasBelief | HunterWorldFacts.BeliefFresh | HunterWorldFacts.PlayerVisible, HunterAction.Chase)]
        public void ApproachPlanUsesStalkOnlyForFreshUnseenBelief(HunterWorldFacts facts, HunterAction expected)
        {
            var actions = ApproachActions();
            bool visible = (facts & HunterWorldFacts.PlayerVisible) != 0;
            ulong goal = (ulong)(visible ? HunterWorldFacts.CaughtPlayer : HunterWorldFacts.LocatedPlayer);
            GoapPlanResult result = GoapPlannerUtility.Plan((ulong)facts, goal, 0, actions);
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.Found));
            Assert.That(result.ActionIds, Is.EqualTo(visible ? new[] { (int)expected, (int)HunterAction.Lunge } : new[] { (int)expected }));
            Assert.That(result.ActionIds, Is.Unique);
            Assert.That(result.ExpandedStates, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void StalkingCannotInventSightAndUnreachableCatchTerminates()
        {
            GoapPlanResult result = GoapPlannerUtility.Plan((ulong)(HunterWorldFacts.HasBelief | HunterWorldFacts.BeliefFresh),
                (ulong)HunterWorldFacts.CaughtPlayer, 0, ApproachActions());
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.Unreachable));
            Assert.That(result.ActionIds, Is.Empty);
            Assert.That(result.ExpandedStates, Is.EqualTo(2), "Repeated Stalk/search effects must not produce a plan loop.");
            Assert.That(GoapPlannerUtility.Plan((ulong)HunterWorldFacts.BeliefFresh,
                (ulong)HunterWorldFacts.LocatedPlayer, 0, ApproachActions()).Status, Is.EqualTo(GoapPlanStatus.Unreachable));
        }

        private static GoapActionDefinition[] ApproachActions() => new[]
        {
            new GoapActionDefinition((int)HunterAction.Stalk, (ulong)(HunterWorldFacts.HasBelief | HunterWorldFacts.BeliefFresh),
                (ulong)HunterWorldFacts.PlayerVisible, (ulong)HunterWorldFacts.LocatedPlayer, 0, 0.5f),
            new GoapActionDefinition((int)HunterAction.SearchLastKnown, (ulong)HunterWorldFacts.HasBelief,
                (ulong)HunterWorldFacts.PlayerVisible, (ulong)HunterWorldFacts.LocatedPlayer, 0, 2f),
            new GoapActionDefinition((int)HunterAction.Chase, (ulong)HunterWorldFacts.PlayerVisible,
                (ulong)HunterWorldFacts.InLungeRange, (ulong)HunterWorldFacts.InLungeRange, 0, 2f),
            new GoapActionDefinition((int)HunterAction.Lunge, (ulong)(HunterWorldFacts.PlayerVisible | HunterWorldFacts.InLungeRange),
                0, (ulong)HunterWorldFacts.CaughtPlayer, 0, 1f)
        };

        [Test]
        public void AlreadySatisfiedGoalHasNoActionsOrCost()
        {
            GoapPlanResult result = GoapPlannerUtility.Plan(Caught, Caught, 0, Array.Empty<GoapActionDefinition>());
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.AlreadySatisfied));
            Assert.That(result.ActionIds, Is.Empty);
            Assert.That(result.TotalCost, Is.Zero);
            Assert.That(result.ExpandedStates, Is.Zero);
        }

        [Test]
        public void ChoosesCheaperTwoActionRouteOverFirstExpensiveAction()
        {
            var actions = new[]
            {
                new GoapActionDefinition(99, Visible, 0, Caught, 0, 12f),
                new GoapActionDefinition(10, Visible, 0, Near, 0, 2f),
                new GoapActionDefinition(20, Visible | Near, 0, Caught, 0, 1f)
            };
            GoapPlanResult result = GoapPlannerUtility.Plan(Visible, Caught, 0, actions);
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.Found));
            Assert.That(result.ActionIds, Is.EqualTo(new[] { 10, 20 }));
            Assert.That(result.TotalCost, Is.EqualTo(3f));
        }

        [Test]
        public void ImprovedOpenStateReplacesItsPreviouslyExpensiveParent()
        {
            var actions = new[]
            {
                new GoapActionDefinition(1, 0, 0, Near, 0, 9f),
                new GoapActionDefinition(2, 0, 0, Hint, 0, 1f),
                new GoapActionDefinition(3, Hint, 0, Near, Hint, 1f),
                new GoapActionDefinition(4, Near, 0, Caught, 0, 1f)
            };
            GoapPlanResult result = GoapPlannerUtility.Plan(0, Caught, 0, actions);
            Assert.That(result.ActionIds, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(result.TotalCost, Is.EqualTo(3f));
        }

        [Test]
        public void NegativePreconditionRequiresClearingFactBeforeAction()
        {
            var actions = new[]
            {
                new GoapActionDefinition(1, Near, Hint, Caught, 0, 1f),
                new GoapActionDefinition(2, Hint, 0, 0, Hint, 1f)
            };
            GoapPlanResult result = GoapPlannerUtility.Plan(Near | Hint, Caught, 0, actions);
            Assert.That(result.ActionIds, Is.EqualTo(new[] { 2, 1 }));
        }

        [Test]
        public void NegativeGoalRequiresClearEffect()
        {
            var actions = new[] { new GoapActionDefinition(3, Visible, 0, Caught, Visible, 2f) };
            GoapPlanResult result = GoapPlannerUtility.Plan(Visible, Caught, Visible, actions);
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.Found));
            Assert.That(result.ActionIds, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void MissingPreconditionReturnsUnreachableWithoutPartialPlan()
        {
            var actions = new[] { new GoapActionDefinition(1, Near, 0, Caught, 0, 1f) };
            GoapPlanResult result = GoapPlannerUtility.Plan(Visible, Caught, 0, actions);
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.Unreachable));
            Assert.That(result.ActionIds, Is.Empty);
            Assert.That(result.ExpandedStates, Is.EqualTo(1));
        }

        [Test]
        public void EmptyActionSetIsUnreachableWhenGoalIsMissing()
        {
            Assert.That(GoapPlannerUtility.Plan(0, Caught, 0, Array.Empty<GoapActionDefinition>()).Status,
                Is.EqualTo(GoapPlanStatus.Unreachable));
        }

        [Test]
        public void ExpansionBudgetReturnsExplicitLimitInsteadOfUnreachable()
        {
            var actions = new[]
            {
                new GoapActionDefinition(1, Visible, 0, Near, 0, 1f),
                new GoapActionDefinition(2, Near, 0, Caught, 0, 1f)
            };
            GoapPlanResult result = GoapPlannerUtility.Plan(Visible, Caught, 0, actions, 1);
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.SearchLimitReached));
            Assert.That(result.ActionIds, Is.Empty);
            Assert.That(result.ExpandedStates, Is.EqualTo(1));
        }

        [Test]
        public void GoalReachedAtExpansionBudgetStillSucceeds()
        {
            var actions = new[] { new GoapActionDefinition(1, 0, 0, Caught, 0, 1f) };
            Assert.That(GoapPlannerUtility.Plan(0, Caught, 0, actions, 1).Status, Is.EqualTo(GoapPlanStatus.Found));
        }

        [Test]
        public void EqualCostAlternativesUseInputOrderDeterministically()
        {
            var actions = new[]
            {
                new GoapActionDefinition(90, 0, 0, Caught, 0, 1f),
                new GoapActionDefinition(10, 0, 0, Caught, 0, 1f)
            };
            for (int i = 0; i < 10; i++)
                Assert.That(GoapPlannerUtility.Plan(0, Caught, 0, actions).ActionIds, Is.EqualTo(new[] { 90 }));
        }

        [Test]
        public void ZeroCostCyclesAndNoOpActionsTerminateWithoutInventingGoal()
        {
            var actions = new[]
            {
                new GoapActionDefinition(1, 0, 0, Hint, 0, 0f),
                new GoapActionDefinition(2, Hint, 0, 0, Hint, 0f),
                new GoapActionDefinition(3, 0, 0, 0, 0, 0f)
            };
            GoapPlanResult result = GoapPlannerUtility.Plan(0, Caught, 0, actions);
            Assert.That(result.Status, Is.EqualTo(GoapPlanStatus.Unreachable));
            Assert.That(result.ExpandedStates, Is.EqualTo(2));
        }

        [Test]
        public void SupportsHighestBitInFactMask()
        {
            const ulong high = 1UL << 63;
            var actions = new[] { new GoapActionDefinition(7, Visible, 0, high, 0, 0.25f) };
            Assert.That(GoapPlannerUtility.Plan(Visible, high, 0, actions).ActionIds, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void RejectsConflictingMasksDuplicateIdsAndInvalidCosts()
        {
            var valid = new GoapActionDefinition(1, 0, 0, Caught, 0, 1f);
            Assert.Throws<ArgumentException>(() => GoapPlannerUtility.Plan(0, Caught, Caught, new[] { valid }));
            Assert.Throws<ArgumentException>(() => GoapPlannerUtility.Plan(0, Caught, 0, new[] { valid, valid }));
            foreach (float cost in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentException>(() => GoapPlannerUtility.Plan(0, Caught, 0,
                    new[] { new GoapActionDefinition(1, 0, 0, Caught, 0, cost) }));
            Assert.Throws<ArgumentException>(() => GoapPlannerUtility.Plan(0, Caught, 0,
                new[] { new GoapActionDefinition(1, Hint, Hint, Caught, 0, 1f) }));
            Assert.Throws<ArgumentException>(() => GoapPlannerUtility.Plan(0, Caught, 0,
                new[] { new GoapActionDefinition(1, 0, 0, Caught, Caught, 1f) }));
        }
    }
}
