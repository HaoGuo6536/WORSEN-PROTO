// ============================================================================
// RunWorldFactRelayControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that shrine and floor presentation noise retain one ordered stream.
//   This channel only reports facts and has no path to Director or Hunter hearing.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Verify paired observers, pause admission and once-only noise delivery.
//   - Verify explicit committed noise and teardown without native objects.
// DEPENDENCIES:
//   - Session Run, NUnit and shared managed relay test support.
// USAGE NOTES:
//   WorldFacts intentionally includes shrine noise in the existing shared stream.
// ============================================================================
using NUnit.Framework;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunWorldFactRelayControllerTests
    {
        [TestCase("HandleShrineNoise", true)]
        [TestCase("HandlePickupNoise", true)]
        [TestCase("PublishNoise", false)]
        public void FactsPreserveOrderPairingAndTeardown(string input, bool gated)
        {
            bool paused = false;
            var relay = new RunWorldFactRelayController(() => paused);
            RunFactRelayTestUtility.VerifyDelivery(relay, value => paused = value, input, "WorldNoisePublished", gated);
        }
    }
}
