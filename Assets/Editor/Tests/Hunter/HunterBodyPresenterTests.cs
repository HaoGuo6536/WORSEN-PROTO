// ============================================================================
// HunterBodyPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the pure mask math that lets Hunters ignore each other's bodies.
//   Only the route-gate and HunterBody bits may leave a query mask, so walls and
//   the player keep blocking and receiving hits; missing layers change nothing.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Remove exactly the named gate and body bits and keep every other bit.
//   - Leave the mask unchanged for a missing (-1) or out-of-range layer.
// DEPENDENCIES:
//   - HunterBodyPresenter and NUnit only; no scene, physics or project layers.
// USAGE NOTES:
//   Headless pure tier; fixed layer indices stand in for the provisioned layers.
// ============================================================================
using NUnit.Framework;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterBodyPresenterTests
    {
        private readonly HunterBodyPresenter _presenter = new HunterBodyPresenter();
        [Test] public void RemovesOnlyTheRouteGateAndHunterBodyBits()
        {
            Assert.That(_presenter.WithoutLayers(~0, 8, 9), Is.EqualTo(~0 & ~(1 << 8) & ~(1 << 9)));
            Assert.That(_presenter.WithoutLayers(1 | (1 << 9), 8, 9), Is.EqualTo(1), "Default-layer walls stay in the mask.");
            Assert.That(_presenter.WithoutLayers(1 << 9, 8, 9), Is.Zero);
            Assert.That(_presenter.WithoutLayer(1 << 31, 31), Is.Zero);
            Assert.That(_presenter.WithoutLayer(1 << 3, 9), Is.EqualTo(1 << 3), "An absent bit stays absent and others stay set.");
        }
        [Test] public void MissingOrOutOfRangeLayersRemoveNothing()
        {
            Assert.That(_presenter.WithoutLayers(~0, -1, -1), Is.EqualTo(~0));
            Assert.That(_presenter.WithoutLayer(~0, 32), Is.EqualTo(~0));
            Assert.That(_presenter.WithoutLayers(~0, -1, 9), Is.EqualTo(~(1 << 9)));
            Assert.That(_presenter.WithoutLayers(~0, 8, -1), Is.EqualTo(~(1 << 8)));
            Assert.That(_presenter.WithoutLayers(0, 8, 9), Is.Zero);
        }
    }
}
