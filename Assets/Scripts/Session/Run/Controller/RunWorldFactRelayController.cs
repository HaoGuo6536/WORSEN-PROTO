// ============================================================================
// RunWorldFactRelayController.cs
// ============================================================================
// PURPOSE:
//   Preserves the single observational noise stream shared by shrines, pickups,
//   hands and traps. Separating it from gameplay hearing prevents presentation
//   noise from acquiring an accidental Director or Hunter delivery path.
// ARCHITECTURAL ROLE:
//   Controller (§2, pure-C# dispatcher) · Session · Run.
// KEY RESPONSIBILITIES:
//   - Relay shrine and floor noise through the supplied pause gate.
//   - Publish trap noise only after the Manager stamps its trusted origin.
//   - Clear upward subscribers at Run teardown.
// DEPENDENCIES:
//   - Core NoiseEvent and an injected pause predicate only.
// USAGE NOTES:
//   Owned by the persistent Run Manager. Shrine and floor input subscriptions
//   are paired by that Manager. This channel never sends stimuli to gameplay.
// ============================================================================
using System;
using Worsen.Core;

namespace Worsen.Session.Run
{
    public sealed class RunWorldFactRelayController
    {
        private readonly Func<bool> isPaused;
        public RunWorldFactRelayController(Func<bool> isPaused)
        { this.isPaused = isPaused ?? throw new ArgumentNullException(nameof(isPaused)); }

        public event Action<NoiseEvent> WorldNoisePublished;
        internal void HandleShrineNoise(NoiseEvent noise) { if (!isPaused()) WorldNoisePublished?.Invoke(noise); }
        internal void HandlePickupNoise(NoiseEvent noise) { if (!isPaused()) WorldNoisePublished?.Invoke(noise); }
        internal void PublishNoise(NoiseEvent noise) => WorldNoisePublished?.Invoke(noise);
        internal void Teardown() => WorldNoisePublished = null;
    }
}
