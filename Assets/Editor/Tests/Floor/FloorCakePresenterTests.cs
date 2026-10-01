// ============================================================================
// FloorCakePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks the cake candle and audible tick math without a running engine.
//   Deterministic outputs keep the visual tell reproducible with the injected clock.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Verify flicker bounds, repeatability and non-silent bounded tick samples.
// DEPENDENCIES:
//   NUnit and FloorCakePresenter only.
// USAGE NOTES:
//   No scene, engine clock, asset or random source is used.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using Worsen.Domain.Floor;

namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorCakePresenterTests
    {
        [Test]
        public void CandleFlickerIsBoundedRepeatableAndOnlyChangesWithSuppliedTime()
        {
            var presenter = new FloorCakePresenter();
            var values = Enumerable.Range(0, 100).Select(i => presenter.Flicker(i * 0.01f, 7f, 0.2f)).ToArray();
            Assert.That(values.All(v => v >= 0.8f && v <= 1.2f), Is.True);
            Assert.That(values.Distinct().Count(), Is.GreaterThan(1));
            Assert.That(values, Is.EqualTo(Enumerable.Range(0, 100).Select(i => presenter.Flicker(i * 0.01f, 7f, 0.2f))));
            Assert.That(presenter.Flicker(42f, 7f, 0f), Is.EqualTo(1f));
        }
        [Test]
        public void PlaceholderTickIsDeterministicNonSilentAndBounded()
        {
            var presenter = new FloorCakePresenter(); var samples = presenter.TickSamples(22050, 0.06f, 900f);
            Assert.That(samples, Is.EqualTo(presenter.TickSamples(22050, 0.06f, 900f)));
            Assert.That(samples.Any(v => v > 0.01f), Is.True);
            Assert.That(samples.All(v => v >= -1f && v <= 1f), Is.True);
            Assert.That(samples[0], Is.Zero);
        }
    }
}
