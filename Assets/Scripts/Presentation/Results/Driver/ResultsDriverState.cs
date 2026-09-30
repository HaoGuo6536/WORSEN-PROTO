// ============================================================================
// ResultsDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains formatted gameplay results, no-floor evidence and the navigation-button latch.
//   The display may be rebound after a document is recreated without losing its pending request.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Distinguish no-floor failures from gameplay outcomes and share the navigation latch.
//   - Keep display text and the one-request-per-summary latch as passive data.
//   - Retain the pending death summary, catch identity and bounded fallback state.
//   - Store detailed outcomes, persisted best depth and validated next-run seed input.
//
// DEPENDENCIES:
//   - Core RunSummary and EntityId values only.
//
// USAGE NOTES:
//   - Scene-owned through ResultsDriver; does not retain scene objects.
//   - GenerationSeed is the requested floor seed, not the expedition's root seed.
//
// ============================================================================

using Worsen.Core;

namespace Worsen.Presentation.Results
{
    public sealed class ResultsDriverState
    {
        public bool Visible;
        public bool NoFloor;
        public int? GenerationSeed;
        public bool RestartIssued;
        public bool HasPendingSummary, CatchCompleted, CatchFallbackFired;
        public EntityId CatchPlayer;
        public RunSummary PendingSummary;
        public float CatchRemaining;
        public string Title = "RUN COMPLETE";
        public string RunTime = "—";
        public string Cakes = "—";
        public string GoldenCakes = "—";
        public string Chases = "—";
        public string Escapes = "—";
        public string ChaseTime = "—";
        public string EndReason = "—";
        public string Cause = "—", Killer = "—", GrabsEscaped = "—", ExitToEscape = "—", Depth = "—", Seed = "—", BestDepth = "—";
        public string NextSeedText = "", SeedError = "";
        public bool SeedValid = true, UseFixedSeed;
        public int NextSeed;
    }
}

