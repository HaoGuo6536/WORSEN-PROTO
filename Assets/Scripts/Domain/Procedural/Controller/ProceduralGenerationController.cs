// ============================================================================
// ProceduralGenerationController.cs
// ============================================================================
// PURPOSE:
//   Tracks bounded generation attempts independently of geometry and engine calls.
//   Failed seed/reason/layout records survive teardown. Template exhaustion gets
//   a bounded organic recovery stage before failure can become a no-floor screen.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Increment attempt seeds with a fixed, versioned prime and bounded budget.
//   - Record each result and preserve failure provenance for the Session owner.
//   - Restart the same seed sequence once for organic recovery after template failure.
// DEPENDENCIES:
//   - Own BehaviorState and System only; no engine or sibling systems.
// USAGE NOTES:
//   Three retries means four attempts per stage including the original seed.
//   Integer wrap is deliberate and reproducible. NoFloorAwaitingSession is fail-closed:
//   no map or actors are admitted; Session chooses the screen/authored fallback.
//   Success is recorded only after the Manager completes physical validation.
//   UsedFallback retains its existing no-floor meaning for Session/UI consumers;
//   successful organic recovery is explicitly reported in the generation journal.
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
            _state.TemplateFailureReason = string.Empty; _state.OrganicFallbackReason = string.Empty;
            _state.IsReady = false; _state.Layout = null;
            _state.GenerationManifest = "generation-v1|seed=" + seed + "|round=" + round +
                "|retries=" + retries + "|stride=" + SeedStride;
        }

        public bool Fail(string reason, string layoutManifest, bool templateAttempt = false)
        {
            RequirePending();
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An attempt needs a failure reason.");
            Record("failed:" + Uri.EscapeDataString(reason), layoutManifest);
            if (templateAttempt) _state.TemplateFailureReason = reason;
            _state.IsReady = false; _state.Layout = null;
            if (_state.AttemptIndex == _state.RetryBudget)
            {
                if (_state.OrganicFallbackReason.Length == 0 && _state.TemplateFailureReason.Length != 0)
                {
                    _state.OrganicFallbackReason = "template-attempts-exhausted:" + _state.TemplateFailureReason;
                    _state.AttemptIndex = 0; _state.AttemptSeed = _state.BaseSeed;
                    _state.GenerationManifest += "|stage=organic|reason=" + Uri.EscapeDataString(_state.OrganicFallbackReason);
                    return true;
                }
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
            _state.GenerationManifest += (_state.OrganicFallbackReason.Length == 0 ? "|fallback=none" : "|fallback=Organic") +
                "|generationSucceeded=true";
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
