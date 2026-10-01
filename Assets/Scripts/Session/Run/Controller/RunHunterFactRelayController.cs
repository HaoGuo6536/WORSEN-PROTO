// ============================================================================
// RunHunterFactRelayController.cs
// ============================================================================
// PURPOSE:
//   Publishes the Run system's hunter presentation facts without owning attacks,
//   damage admission or chase rules. RunSessionManager pairs source subscriptions
//   and supplies the current pause gate; committed facts keep their original order.
// ARCHITECTURAL ROLE:
//   Controller (§2, pure-C# dispatcher) · Session · Run.
// KEY RESPONSIBILITIES:
//   - Relay observational hunter facts synchronously with the supplied pause gate.
//   - Publish admitted facts only when commanded by the owning Run Manager.
//   - Release all upward subscribers when the owning Run is destroyed.
// DEPENDENCIES:
//   - Core fact payloads and an injected pause predicate; no foreign systems.
// USAGE NOTES:
//   Owned by the persistent Run Manager. Input subscriptions remain paired in its
//   enable/disable methods. Publish methods are assembly-internal; observers can
//   subscribe but cannot raise facts. Teardown does not replace channel identity.
// ============================================================================
using System;
using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunHunterFactRelayController
    {
        private readonly Func<bool> isPaused;
        public RunHunterFactRelayController(Func<bool> isPaused)
        { this.isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused)); }

        public event Action<HunterFeedbackEvent> HunterFeedbackPublished;
        public event Action<HunterHabitFact> HunterHabitPublished;
        public event Action<WeaverFact> WeaverFactPublished;
        public event Action<WebHitFact> WebHitPublished;
        public event Action<TickingSoundFact> TickingSoundPublished;
        public event Action<TickingGuidanceFact> TickingGuidancePublished;
        public event Action<TickingNoiseFact> TickingNoisePublished;
        public event Action<MimicFact> MimicFactPublished;
        public event Action<BlinderHitFact> BlinderHitPublished;
        public event Action<BlinderThrowFact> BlinderThrowPublished;
        public event Action<BlinderSoundFact> BlinderSoundPublished;
        public event Action<BlinderTrapPolicyFact> BlinderTrapPolicyPublished;
        public event Action<HeraldScreamFact> HeraldScreamPublished;
        public event Action<HeraldBreathFact> HeraldBreathPublished;
        public event Action<HeraldDeafenFact> HeraldDeafenPublished;
        public event Action<StareFact> StareFactPublished;
        public event Action<ProximitySample> ProximityPublished;
        public event Action<int, float> AfterglowWindowPublished;

        internal void HandleHunterFeedback(HunterFeedbackEvent fact) { if (!isPaused()) HunterFeedbackPublished?.Invoke(fact); }
        internal void HandleHunterHabit(HunterHabitFact fact) { if (!isPaused()) HunterHabitPublished?.Invoke(fact); }
        internal void HandleWeaver(WeaverFact fact) { if (!isPaused()) WeaverFactPublished?.Invoke(fact); }
        internal void HandleTickingSound(TickingSoundFact fact) { if (!isPaused()) TickingSoundPublished?.Invoke(fact); }
        internal void HandleTickingGuidance(TickingGuidanceFact fact) { if (!isPaused()) TickingGuidancePublished?.Invoke(fact); }
        internal void HandleTickingNoise(TickingNoiseFact fact) { if (!isPaused()) TickingNoisePublished?.Invoke(fact); }
        internal void HandleBlinderHit(BlinderHitFact fact) { if (!isPaused()) BlinderHitPublished?.Invoke(fact); }
        internal void HandleBlinderThrow(BlinderThrowFact fact) { if (!isPaused()) BlinderThrowPublished?.Invoke(fact); }
        internal void HandleBlinderSound(BlinderSoundFact fact) { if (!isPaused()) BlinderSoundPublished?.Invoke(fact); }
        internal void HandleHeraldScream(HeraldScreamFact fact) { if (!isPaused()) HeraldScreamPublished?.Invoke(fact); }
        internal void HandleHeraldBreath(HeraldBreathFact fact) { if (!isPaused()) HeraldBreathPublished?.Invoke(fact); }
        internal void HandleStare(StareFact fact) { if (!isPaused()) StareFactPublished?.Invoke(fact); }

        internal void PublishWebHit(WebHitFact fact) => WebHitPublished?.Invoke(fact);
        internal void PublishMimic(MimicFact fact) => MimicFactPublished?.Invoke(fact);
        internal void PublishBlinderTrapPolicy(BlinderTrapPolicyFact fact) => BlinderTrapPolicyPublished?.Invoke(fact);
        internal void PublishHeraldDeafen(HeraldDeafenFact fact) => HeraldDeafenPublished?.Invoke(fact);
        internal void PublishProximity(ProximitySample sample) => ProximityPublished?.Invoke(sample);
        internal void PublishAfterglow(int roomId, float seconds) => AfterglowWindowPublished?.Invoke(roomId, seconds);

        internal void Teardown()
        {
            HunterFeedbackPublished = null; HunterHabitPublished = null;
            WeaverFactPublished = null; WebHitPublished = null;
            TickingSoundPublished = null; TickingGuidancePublished = null; TickingNoisePublished = null;
            MimicFactPublished = null; BlinderHitPublished = null; BlinderThrowPublished = null;
            BlinderSoundPublished = null; BlinderTrapPolicyPublished = null;
            HeraldScreamPublished = null; HeraldBreathPublished = null; HeraldDeafenPublished = null;
            StareFactPublished = null; ProximityPublished = null; AfterglowWindowPublished = null;
        }
    }
}
