// ============================================================================
// ProceduralGenerationController.cs
// ============================================================================
// PURPOSE:
//   Tracks bounded generation attempts independently of geometry and engine calls.
//   Failed seed/reason/layout records survive teardown, and exhaustion enters an
//   explicit no-floor fallback rather than misreporting a successful generation.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Increment attempt seeds with a fixed, versioned prime and bounded budget.
//   - Record each result and preserve failure provenance for the Session owner.
// DEPENDENCIES:
//   - Own BehaviorState and System only; no engine or sibling systems.
// USAGE NOTES:
//   Three retries means four attempts including the original seed. Integer wrap
//   is deliberate and reproducible. NoFloorAwaitingSession is a fail-closed path:
//   no map or actors are admitted; Session chooses the screen/authored fallback.
//   Success is recorded only after the Manager completes physical validation.
// ============================================================================
using System;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralGenerationController
    {
        public const int SeedStride = 104729;
        private readonly ProceduralBehaviorState _state;
        public ProceduralGenerationController(ProceduralBehaviorState state)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        public void Begin(int seed, int round, int retries)
        {
            if (retries < 0 || retries > 8) throw new ArgumentOutOfRangeException(nameof(retries));
            _state.BaseSeed = seed; _state.AttemptSeed = seed; _state.AttemptIndex = 0;
            _state.RetryBudget = retries; _state.GenerationSucceeded = false; _state.UsedFallback = false;
            _state.IsReady = false; _state.Layout = null;
            _state.GenerationManifest = "generation-v1|seed=" + seed + "|round=" + round +
                "|retries=" + retries + "|stride=" + SeedStride;
        }

        public bool Fail(string reason, string layoutManifest)
        {
            RequirePending();
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An attempt needs a failure reason.");
            Record("failed:" + Uri.EscapeDataString(reason), layoutManifest);
            _state.IsReady = false; _state.Layout = null;
            if (_state.AttemptIndex == _state.RetryBudget)
            {
                _state.UsedFallback = true;
                _state.GenerationManifest += "|fallback=NoFloorAwaitingSession|generationSucceeded=false";
                return false;
            }
            _state.AttemptIndex++;
            _state.AttemptSeed = unchecked(_state.BaseSeed + _state.AttemptIndex * SeedStride);
            return true;
        }

        public void Succeed(string layoutManifest)
        {
            RequirePending();
            Record("validated", layoutManifest);
            _state.GenerationSucceeded = true;
            _state.GenerationManifest += "|fallback=none|generationSucceeded=true";
        }

        private void RequirePending()
        {
            if (_state.GenerationManifest.Length == 0 || _state.GenerationSucceeded || _state.UsedFallback)
                throw new InvalidOperationException("Generation attempt is not pending.");
        }
        private void Record(string outcome, string layout)
        {
            _state.GenerationManifest += "|attempt=" + _state.AttemptIndex + ",seed=" + _state.AttemptSeed +
                ",result=" + outcome + ",layout=" + Uri.EscapeDataString(layout ?? string.Empty);
        }
    }
}
