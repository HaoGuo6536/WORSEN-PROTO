// ============================================================================
// HorrorMicroEventPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Simulates many seeded runs to quantify rare anomaly admission rather than claim feel.
//   Conservative geometry tests keep door closures behind the player and outside the view.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Verify deterministic schedules, low frequency, cap, spacing and chase exclusion.
// DEPENDENCIES:
//   - Core interactables, Horror presentation and NUnit.
// USAGE NOTES:
//   Pure Edit Mode admission tests; unreachable anchor certification remains a world-owner gate.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;

namespace Worsen.Tests.Horror
{
    public sealed class HorrorMicroEventPresenterTests
    {
        private HorrorDriverConfig _config;
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);
        private HorrorMicroEventDriverState Run(int seed)
        {
            var state = new HorrorMicroEventDriverState();
            HorrorMicroEventPresenter.ResetRun(state, _config, new System.Random(seed)); state.ChaseKnown = true; return state;
        }

        [Test] public void SeededRunsHaveLowFrequencyAndNeverExceedSpacingOrBudget()
        {
            int total = 0;
            for (int seed = 0; seed < 2000; seed++)
            {
                var a = Run(seed); var b = Run(seed); double previous = double.NegativeInfinity;
                for (int second = 0; second <= 900; second++)
                {
                    int kind = HorrorMicroEventPresenter.Select(a, _config, second, true, true, true);
                    Assert.That(HorrorMicroEventPresenter.Select(b, _config, second, true, true, true), Is.EqualTo(kind));
                    if (kind == 0) continue;
                    Assert.That(second - previous, Is.GreaterThanOrEqualTo(60d)); previous = second; total++;
                }
                Assert.That(a.Used, Is.LessThanOrEqualTo(2));
            }
            Assert.That(total / 2000d, Is.InRange(1d, 2d), "900-second fully eligible runs; shorter/occluded runs produce fewer.");
            TestContext.WriteLine("Micro-events per 900-second eligible run: " + total / 2000d);
        }

        [Test] public void ChaseAndMissingCandidatesNeverSpendBudgetOrBuildABacklog()
        {
            var state = Run(22); state.Chases.Add(1); state.Chases.Add(2);
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, 10000d, true, true, true), Is.Zero);
            state.Chases.Remove(1);
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, 20000d, true, true, true), Is.Zero);
            state.Chases.Clear();
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, 20000d, true, true, true), Is.Zero);
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, 30000d, false, false, false), Is.Zero);
            Assert.That(state.Used, Is.Zero);
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, 40000d, false, false, true), Is.EqualTo(3));
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, double.NaN, true, true, true), Is.Zero);
        }

        [Test] public void DoorMustBePlayerOpenedStillOpenWhollyBehindAndOutOfView()
        {
            var bounds = new Bounds(Vector3.back * 10f, Vector3.one * 2f);
            var door = new InteractableState(1, InteractableKind.Door, 1, bounds.center, InteractableStateValue.Open);
            Assert.That(Eligible(door, bounds, true, false), Is.True);
            Assert.That(Eligible(door, bounds, false, false), Is.False);
            Assert.That(Eligible(door, bounds, true, true), Is.False);
            Assert.That(Eligible(door, new Bounds(Vector3.forward * 10f, Vector3.one), true, false), Is.False);
            Assert.That(Eligible(door, new Bounds(Vector3.back, Vector3.one), true, false), Is.False);
            Assert.That(Eligible(new InteractableState(1, InteractableKind.Door, 1, bounds.center, InteractableStateValue.Broken), bounds, true, false), Is.False);
        }

        [Test] public void SilhouetteOnlyAdmitsInsideTheHorizontalEdgeOfView()
        {
            Assert.That(HorrorMicroEventPresenter.AtViewEdge(new Vector3(.05f, .5f, 10f), .12f), Is.True);
            Assert.That(HorrorMicroEventPresenter.AtViewEdge(new Vector3(.5f, .5f, 10f), .12f), Is.False);
            Assert.That(HorrorMicroEventPresenter.AtViewEdge(new Vector3(.95f, .5f, -1f), .12f), Is.False);
        }

        [Test] public void UnknownChaseStateFailsClosedUntilFreshAggregateProximity()
        {
            var state = Run(22); state.ChaseKnown = false;
            Assert.That(HorrorMicroEventPresenter.Select(state, _config, 10000d, true, true, true), Is.Zero);
            Assert.That(state.Used, Is.Zero);
        }
        private bool Eligible(InteractableState door, Bounds bounds, bool opened, bool visible)
            => HorrorMicroEventPresenter.DoorEligible(door, bounds, opened, visible, Vector3.zero, Vector3.forward, _config.MicroEventMinimumDistance);
    }
}
