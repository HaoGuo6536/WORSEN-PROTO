// ============================================================================
// ResultsPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Formats authoritative run facts into readable results and manages a UI request latch.
//   Keeping duration formatting and repeat-click handling outside the Driver allows
//   both to be verified without UI Toolkit or scene-loading behavior.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Hold death summaries until the catch ends; flag a bounded missing-event fallback.
//   - Format times and counts without changing run rules or inventing missing values.
//   - Accept one restart interaction while visible; rearm only after explicit hiding.
//
// DEPENDENCIES:
//   - Worsen.Core RunSummary and RunEndReason; System numeric/culture utilities.
//
// USAGE NOTES:
//   - Stateless calculator over caller-owned ResultsDriverState. No engine calls.
//   - Repeated summary delivery while visible does not reset the restart latch.
//
// ============================================================================

using System;
using System.Globalization;
using Worsen.Core;

namespace Worsen.Presentation.Results
{
    public sealed class ResultsPresenter
    {
        public void Show(ResultsDriverState state, RunSummary summary,
            float timeoutSeconds = ResultsDriverConfig.DefaultCatchTimeoutSeconds)
        {
            if (summary.EndReason == RunEndReason.Died && !state.CatchCompleted)
            {
                if (!state.HasPendingSummary)
                    state.CatchRemaining = float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f
                        ? ResultsDriverConfig.DefaultCatchTimeoutSeconds : timeoutSeconds;
                state.PendingSummary = summary;
                state.HasPendingSummary = true;
                state.Visible = false;
                return;
            }
            state.HasPendingSummary = false;
            state.PendingSummary = default;
            state.CatchRemaining = 0f;
            PresentSummary(state, summary);
        }

        public void PrepareCatch(ResultsDriverState state, EntityId player)
        {
            if (!state.CatchCompleted && !state.CatchPlayer.IsValid) state.CatchPlayer = player;
        }

        public void EndCatch(ResultsDriverState state, EntityId player)
        {
            if (state.CatchCompleted || !player.IsValid ||
                (state.CatchPlayer.IsValid && state.CatchPlayer != player) ||
                (!state.CatchPlayer.IsValid && !state.HasPendingSummary)) return;
            ReleaseCatch(state);
        }

        public bool Tick(ResultsDriverState state, float dt)
        {
            if (!state.HasPendingSummary || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return false;
            state.CatchRemaining = Math.Max(0f, state.CatchRemaining - dt);
            if (state.CatchRemaining > 0f) return false;
            state.CatchFallbackFired = true;
            ReleaseCatch(state);
            return true;
        }

        private void ReleaseCatch(ResultsDriverState state)
        {
            state.CatchCompleted = true;
            if (state.HasPendingSummary) Show(state, state.PendingSummary);
        }

        private void PresentSummary(ResultsDriverState state, RunSummary summary)
        {
            string reason = summary.EndReason == RunEndReason.Escaped ? "Escaped through the exit" :
                summary.EndReason == RunEndReason.Died ? "You died" : "Unknown outcome";
            SetSummary(state, summary.ElapsedSeconds, summary.CakesCollected, summary.GoldenCakesCollected,
                summary.ChaseCount, summary.ChasesEscaped, summary.TotalChaseSeconds, reason);
        }

        public void SetSummary(ResultsDriverState state, double runSeconds, int cakes, int goldenCakes,
            int chases, int escapes, double chaseSeconds, string endReason)
        {
            if (!state.Visible) state.RestartIssued = false;
            state.Visible = true;
            state.RunTime = FormatDuration(runSeconds);
            state.Cakes = FormatCount(cakes);
            state.GoldenCakes = FormatCount(goldenCakes);
            state.Chases = FormatCount(chases);
            state.Escapes = FormatCount(escapes);
            state.ChaseTime = FormatDuration(chaseSeconds);
            state.EndReason = string.IsNullOrWhiteSpace(endReason) ? "Unknown outcome" : endReason;
        }

        public bool TryRestart(ResultsDriverState state)
        {
            if (!state.Visible || state.RestartIssued) return false;
            state.RestartIssued = true;
            return true;
        }

        public void Hide(ResultsDriverState state)
        {
            state.HasPendingSummary = state.CatchCompleted = state.CatchFallbackFired = false;
            state.PendingSummary = default;
            state.CatchPlayer = default;
            state.CatchRemaining = 0f;
            state.Visible = false;
            state.RestartIssued = false;
            state.RunTime = state.Cakes = state.GoldenCakes = state.Chases = state.Escapes =
                state.ChaseTime = state.EndReason = "—";
        }

        public string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d || seconds > int.MaxValue)
                return "—";
            long whole = (long)Math.Floor(seconds);
            return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FormatCount(int count) => count < 0 ? "—" : count.ToString(CultureInfo.InvariantCulture);
    }
}
