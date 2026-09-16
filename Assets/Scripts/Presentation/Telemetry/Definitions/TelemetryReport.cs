// ============================================================================
// TelemetryReport.cs
// ============================================================================
// PURPOSE:
//   Carries observed measurements together with their denominators. Missing values stay null and unfinished intervals stay counted so reports cannot present missing data as successful gameplay.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Retain metrics, counts, missing streams and capture quality.
// DEPENDENCIES:
//   - System primitives only; local report, never a gameplay RunSummary.
// USAGE NOTES:
//   - Percentages are fractions in [0,1]; nearest-rank p90 uses only valid free horizontal speed.
// ============================================================================
namespace Worsen.Presentation.Telemetry
{
    public sealed class TelemetryReport
    {
        public bool Complete;
        public int RawSamples, AcceptedSamples, DuplicateSamples, InvalidSamples, OutOfOrderSamples;
        public int FreeSpeedSamples, ChaseSpeedSamples, StartedChases, CompletedChases, UnknownChases, IncompleteChases;
        public int Losses, Catches, LungeCatches, LookBacks, CatchWindowsKnown, CatchWindowsUnknown, CatchesWithinWindow;
        public int AcceptedHitCatches, PreConfirmationCatches, LegacyCatches, MatchedCatchEnds;
        public int UnknownCatchClassifications, CatchOutcomeConflicts, OverallOutcomeCount;
        public int LateChaseTransitions;
        public string CatchAccountingMode = "no-catch-evidence";
        public int FreeVaultAttempts, FreeVaultFailures, ChaseVaultAttempts, ChaseVaultFailures;
        public int IncompleteVaultAttempts;
        public int CompletedLocks, IncompleteLocks, IncompleteLookBacks, ProximitySamples;
        public int CompletedLookBacks;
        public double? TotalLookBackSeconds;
        public double? FreeSpeedP90, MedianChaseSeconds, LossRate, LungeCatchRate, LookBacksPerChase;
        public double? CatchWithinOneSecondRate, FreeVaultFailureRate, ChaseVaultFailureRate, VaultFailureRateIncrease;
        public double? MaximumInputLockSeconds, MaximumProximityGapSeconds, MaximumHeat, FloorSeconds;
        public string MissingStreams = "";
    }
}
