// ============================================================================
// ResultsDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains the latest formatted run summary and restart-button state.
//   The display may be rebound after a document is recreated without losing its pending request.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Keep display text and the one-request-per-summary latch as passive data.
//   - Retain the pending death summary, catch identity and bounded fallback state.
//
// DEPENDENCIES:
//   - Core RunSummary and EntityId values only.
//
// USAGE NOTES:
//   - Scene-owned through ResultsDriver; does not retain scene objects.
//
// ============================================================================

using Worsen.Core;

namespace Worsen.Presentation.Results
{
    public sealed class ResultsDriverState
    {
        public bool Visible;
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
    }
}

