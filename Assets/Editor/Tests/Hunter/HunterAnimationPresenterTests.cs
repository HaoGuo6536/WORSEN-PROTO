// ============================================================================
// HunterAnimationPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using NUnit.Framework;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterAnimationPresenterTests
    {
        [TestCase(0f, 0, 0)] [TestCase(2f, 0, 1)] [TestCase(8f, 0, 2)]
        [TestCase(0f, 1, 3)] [TestCase(18f, 2, 4)] [TestCase(0f, 3, 5)]
        public void SelectsCorrectRole(float speed, int phase, int expected)
        { Assert.That(new HunterAnimationPresenter().Tick(new HunterAnimationDriverState(), 0.02f, speed, phase, 4f, 0.1f), Is.EqualTo(expected)); }
        [Test] public void RepeatedTransitionsKeepWeightsNormalizedAndSettle()
        {
            var presenter = new HunterAnimationPresenter(); var state = new HunterAnimationDriverState();
            presenter.Tick(state, 0.1f, 8f, 0, 4f, 0.1f);
            for (int i = 0; i < 20; i++)
            {
                presenter.Tick(state, 0.02f, 0f, 1, 4f, 0.1f);
                float total = 0; foreach (float weight in state.Weights) total += weight;
                Assert.That(total, Is.EqualTo(1f).Within(0.0001f));
            }
            Assert.That(state.Weights[3], Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
