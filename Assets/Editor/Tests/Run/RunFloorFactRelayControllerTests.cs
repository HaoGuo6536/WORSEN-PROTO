// ============================================================================
// RunFloorFactRelayControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises every floor fact channel using only managed inputs. Collection and
//   phase publication retain Manager admission; observational facts use its pause gate.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Verify paired observers, payloads, synchronous ordering and teardown.
//   - Preserve the distinction between observations and committed floor facts.
// DEPENDENCIES:
//   - Session Run, NUnit and shared managed relay test support.
// USAGE NOTES:
//   No Unity objects, global clock or random source are used.
// ============================================================================
using NUnit.Framework;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunFloorFactRelayControllerTests
    {
        [TestCase("HandleRoomDestruction", "RoomDestructionPublished", true)]
        [TestCase("HandleGuidance", "GuidanceChanged", true)]
        [TestCase("HandleCakeLost", "CakeLost", true)]
        [TestCase("PublishPickup", "PickupCollected", false)]
        [TestCase("PublishRoomPhase", "RoomPhaseChanged", false)]
        public void FactsPreserveOrderPairingAndTeardown(string input, string output, bool gated)
        {
            bool paused = false;
            var relay = new RunFloorFactRelayController(() => paused);
            RunFactRelayTestUtility.VerifyDelivery(relay, value => paused = value, input, output, gated);
        }
    }
}
