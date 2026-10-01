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
//   - Present seed-specific no-floor failures immediately, with new-seed Retry and title intent.
//   - Hold death summaries until the catch ends; flag a bounded missing-event fallback.
//   - Format times and counts without changing run rules or inventing missing values.
//   - Accept one restart interaction while visible; rearm only after explicit hiding.
//   - Format every detailed HorrorRun field and validate exact signed decimal seed input.
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
            if (state.NoFloor) return;
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

        public void ShowNoFloor(ResultsDriverState state, int seed)
        {
            bool issued = state.Visible && state.RestartIssued;
            Hide(state);
            state.Visible = state.NoFloor = true;
            state.RestartIssued = issued;
            state.Title = "NO FLOOR";
            state.Seed = (state.GenerationSeed ?? seed).ToString(CultureInfo.InvariantCulture);
            state.EndReason = "Floor generation failed. Seed " + state.Seed + ".";
        }

        public bool TryReturnToTitle(ResultsDriverState state)
        {
            if (!state.NoFloor || !state.Visible || state.RestartIssued) return false;
            state.RestartIssued = true;
            return true;
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
            state.Cause = summary.EndReason != RunEndReason.Died ? "Not applicable" :
                summary.DeathCause == DeathCause.None ? "Unreported" : summary.DeathCause.ToString();
            state.Killer = summary.EndReason != RunEndReason.Died ? "Not applicable" :
                string.IsNullOrWhiteSpace(summary.KillerArchetypeId) ? "Unreported" : summary.KillerArchetypeId;
            state.GrabsEscaped = FormatCount(summary.GrabsEscaped);
            state.ExitToEscape = FormatDuration(summary.SecondsFromExitOpenToEscape);
            state.Depth = summary.DepthReached <= 0 ? "Unreported" : FormatCount(summary.DepthReached);
            state.Seed = summary.Seed.ToString(CultureInfo.InvariantCulture);
        }

        public void SetBestDepth(ResultsDriverState state, int bestDepth)
            => state.BestDepth = FormatCount(bestDepth);

        public bool SetNextSeed(ResultsDriverState state, string text)
        {
            if (state.RestartIssued || state.NoFloor) return false;
            state.NextSeedText = text ?? "";
            state.UseFixedSeed = state.NextSeedText.Length > 0;
            state.NextSeed = 0;
            bool digits = true;
            for (int i = 0; i < state.NextSeedText.Length; i++)
                if (!(i == 0 && state.NextSeedText[i] == '-') && (state.NextSeedText[i] < '0' || state.NextSeedText[i] > '9')) digits = false;
            state.SeedValid = !state.UseFixedSeed || (digits && int.TryParse(state.NextSeedText,
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out state.NextSeed));
            state.SeedError = state.SeedValid ? "" : "Seed must be a 32-bit whole number, or blank.";
            return state.SeedValid;
        }

        public void SetSummary(ResultsDriverState state, double runSeconds, int cakes, int goldenCakes,
            int chases, int escapes, double chaseSeconds, string endReason)
        {
            state.NoFloor = false;
            state.Title = "RUN COMPLETE";
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
            if (!state.Visible || state.RestartIssued || !state.SeedValid) return false;
            state.RestartIssued = true;
            return true;
        }

        public void Hide(ResultsDriverState state)
        {
            state.NoFloor = false;
            state.Title = "RUN COMPLETE";
            state.HasPendingSummary = state.CatchCompleted = state.CatchFallbackFired = false;
            state.PendingSummary = default;
            state.CatchPlayer = default;
            state.CatchRemaining = 0f;
            state.Visible = false;
            state.RestartIssued = false;
            SetNextSeed(state, "");
            state.Cause = state.Killer = state.GrabsEscaped = state.ExitToEscape = state.Depth = state.Seed = "—";
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
