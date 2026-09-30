// ============================================================================
// RunHunterFactRelayControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises every hunter channel without constructing Unity objects. The relay
//   must preserve synchronous order and pause admission without deduplicating facts.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Run.
// KEY RESPONSIBILITIES:
//   - Verify once-only delivery, observer pairing, payloads and teardown per fact.
//   - Distinguish pause-gated observations from already committed publications.
// DEPENDENCIES:
//   - Session Run, NUnit and shared managed relay test support.
// USAGE NOTES:
//   Reflection calls assembly-internal ingress; public observers cannot publish.
// ============================================================================
using NUnit.Framework;
using Worsen.Session.Run;

namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunHunterFactRelayControllerTests
    {
        [TestCase("HandleHunterFeedback", "HunterFeedbackPublished", true)]
        [TestCase("HandleHunterHabit", "HunterHabitPublished", true)]
        [TestCase("HandleWeaver", "WeaverFactPublished", true)]
        [TestCase("HandleTickingSound", "TickingSoundPublished", true)]
        [TestCase("HandleTickingGuidance", "TickingGuidancePublished", true)]
        [TestCase("HandleTickingNoise", "TickingNoisePublished", true)]
        [TestCase("HandleBlinderHit", "BlinderHitPublished", true)]
        [TestCase("HandleBlinderThrow", "BlinderThrowPublished", true)]
        [TestCase("HandleBlinderSound", "BlinderSoundPublished", true)]
        [TestCase("HandleHeraldScream", "HeraldScreamPublished", true)]
        [TestCase("HandleHeraldBreath", "HeraldBreathPublished", true)]
        [TestCase("HandleStare", "StareFactPublished", true)]
        [TestCase("PublishWebHit", "WebHitPublished", false)]
        [TestCase("PublishMimic", "MimicFactPublished", false)]
        [TestCase("PublishBlinderTrapPolicy", "BlinderTrapPolicyPublished", false)]
        [TestCase("PublishHeraldDeafen", "HeraldDeafenPublished", false)]
        [TestCase("PublishProximity", "ProximityPublished", false)]
        [TestCase("PublishAfterglow", "AfterglowWindowPublished", false)]
        public void FactsPreserveOrderPairingAndTeardown(string input, string output, bool gated)
        {
            bool paused = false;
            var relay = new RunHunterFactRelayController(() => paused);
            RunFactRelayTestUtility.VerifyDelivery(relay, value => paused = value, input, output, gated);
        }
    }
}
