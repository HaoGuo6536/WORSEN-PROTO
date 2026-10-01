// ============================================================================
// RunPlayerFactRelayControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises player recovery and snapshot channels independently of actors.
//   Explicit snapshots remain available during pause, as in the original Manager.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Verify pairing, once-only delivery, payload ordering and teardown.
//   - Keep pause rejection on observations, not explicit snapshot commands.
// DEPENDENCIES:
//   - Session Run, NUnit and shared managed relay test support.
// USAGE NOTES:
//   Pure managed fixture; reflection only accesses internal publication methods.
// ============================================================================
using NUnit.Framework;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunPlayerFactRelayControllerTests
    {
        [TestCase("HandleGraceStarted", "OnGraceStarted", true)]
        [TestCase("HandleGraceEnded", "OnGraceEnded", true)]
        [TestCase("HandleTraversalProgress", "TraversalProgressed", true)]
        [TestCase("HandleStumbled", "PlayerStumbled", true)]
        [TestCase("HandleShield", "ShieldChanged", true)]
        [TestCase("PublishShield", "ShieldChanged", false)]
        [TestCase("PublishEmptyItemSlots", "EmptyItemSlotsChanged", false)]
        [TestCase("PublishSpeedNormalized", "SpeedNormalizedPublished", false)]
        public void FactsPreserveOrderPairingAndTeardown(string input, string output, bool gated)
        {
            bool paused = false;
            var relay = new RunPlayerFactRelayController(() => paused);
            RunFactRelayTestUtility.VerifyDelivery(relay, value => paused = value, input, output, gated);
        }
    }
}
