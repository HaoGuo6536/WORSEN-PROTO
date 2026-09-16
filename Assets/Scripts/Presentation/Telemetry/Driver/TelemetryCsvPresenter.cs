// ============================================================================
// TelemetryCsvPresenter.cs
// ============================================================================
// PURPOSE:
//   Formats raw observations and derived reports into one stable CSV schema. It escapes text and preserves invariant numeric values so the same file can be inspected independently of the current editor locale.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Format metadata, raw rows and denominator-bearing summaries.
// DEPENDENCIES:
//   - Core payloads and local TelemetryReport; System formatting only.
// USAGE NOTES:
//   - Blank numeric cells mean unavailable. Vault attempts are resolved outcomes; unfinished traversal is censored. No file operations.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using Worsen.Core;

namespace Worsen.Presentation.Telemetry
{
    public sealed class TelemetryCsvPresenter
    {
        public string Header => "row_type,tick,player,chase_id,kind,value,detail,in_chase,outcome,event_id";
        public IEnumerable<string> Metadata(RunCaptureMetadata m)
        {
            yield return Meta("schema_version", m.SchemaVersion);
            yield return Meta("session_id", m.SessionId);
            yield return Meta("seed", m.Seed);
            yield return Meta("fixed_delta_time", m.FixedDeltaTime);
            yield return Meta("source_revision", m.SourceRevision);
            yield return Meta("config_snapshot_hash", m.ConfigSnapshotHash);
            yield return Meta("provenance_scope", "Source/config hashes describe the supplied build snapshot; Inspector or tuning edits require rebuild and current-hash verification.");
            yield return Meta("random_consumption_order", m.RandomConsumptionOrder);
            yield return Meta("start_tick", m.StartTick);
            yield return Meta("capture_order_rules", "Committed chase transitions precede same-tick movement/traversal conversion; a same-tick end matching the already-tagged chase preserves inclusive terminal labels. Other LateChaseTransitions mark already-emitted tags as incomplete; raw rows are never silently rewritten. Legacy end-only captures cannot observe pre-confirmation catches.");
            yield return Meta("measurement_rules", "Free speed excludes vertical/chase motion; chase membership inclusive start through end tick; loss denominator=Losses+Catches including pre-confirmation accepted hits; lunge denominator=all catches including unknown subtypes; accepted hits plus unmatched legacy catch ends, matched player/chase/tick ends counted once; confirmed chase counts/durations exclude standalone hits; missing streams unavailable; vault attempts=resolved outcomes, unfinished traversal censored; catch window inclusive release through release+1s; proximity gaps include censored capture edges, no unsampled-path guarantee.");
        }
        public string Raw(TelemetrySample s) => Row("raw", s.Tick, s.Player.Value, s.ChaseId,
            s.Kind, s.Value, s.Detail, s.InChase, s.Outcome, s.EventId);
        public IEnumerable<string> Summary(TelemetryReport r, long endTick)
        {
            yield return Meta("end_tick", endTick);
            yield return Row("summary", endTick, null, null, nameof(r.Complete), r.Complete, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.RawSamples), r.RawSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.AcceptedSamples), r.AcceptedSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.DuplicateSamples), r.DuplicateSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.InvalidSamples), r.InvalidSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.OutOfOrderSamples), r.OutOfOrderSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.FreeSpeedSamples), r.FreeSpeedSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.ChaseSpeedSamples), r.ChaseSpeedSamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.StartedChases), r.StartedChases, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CompletedChases), r.CompletedChases, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.UnknownChases), r.UnknownChases, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.IncompleteChases), r.IncompleteChases, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.Losses), r.Losses, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.Catches), r.Catches, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LungeCatches), r.LungeCatches, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.AcceptedHitCatches), r.AcceptedHitCatches, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.PreConfirmationCatches), r.PreConfirmationCatches, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LegacyCatches), r.LegacyCatches, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.MatchedCatchEnds), r.MatchedCatchEnds, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.UnknownCatchClassifications), r.UnknownCatchClassifications, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CatchOutcomeConflicts), r.CatchOutcomeConflicts, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.OverallOutcomeCount), r.OverallOutcomeCount, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CatchAccountingMode), r.CatchAccountingMode, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LateChaseTransitions), r.LateChaseTransitions, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LookBacks), r.LookBacks, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CatchWindowsKnown), r.CatchWindowsKnown, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CatchWindowsUnknown), r.CatchWindowsUnknown, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CatchesWithinWindow), r.CatchesWithinWindow, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.FreeVaultAttempts), r.FreeVaultAttempts, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.FreeVaultFailures), r.FreeVaultFailures, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.ChaseVaultAttempts), r.ChaseVaultAttempts, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.ChaseVaultFailures), r.ChaseVaultFailures, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.IncompleteVaultAttempts), r.IncompleteVaultAttempts, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CompletedLocks), r.CompletedLocks, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.IncompleteLocks), r.IncompleteLocks, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.IncompleteLookBacks), r.IncompleteLookBacks, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.ProximitySamples), r.ProximitySamples, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CompletedLookBacks), r.CompletedLookBacks, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.TotalLookBackSeconds), r.TotalLookBackSeconds, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.FreeSpeedP90), r.FreeSpeedP90, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.MedianChaseSeconds), r.MedianChaseSeconds, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LossRate), r.LossRate, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LungeCatchRate), r.LungeCatchRate, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.LookBacksPerChase), r.LookBacksPerChase, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.CatchWithinOneSecondRate), r.CatchWithinOneSecondRate, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.FreeVaultFailureRate), r.FreeVaultFailureRate, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.ChaseVaultFailureRate), r.ChaseVaultFailureRate, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.VaultFailureRateIncrease), r.VaultFailureRateIncrease, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.MaximumInputLockSeconds), r.MaximumInputLockSeconds, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.MaximumProximityGapSeconds), r.MaximumProximityGapSeconds, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.MaximumHeat), r.MaximumHeat, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.FloorSeconds), r.FloorSeconds, null, null, null, null);
            yield return Row("summary", endTick, null, null, nameof(r.MissingStreams), r.MissingStreams, null, null, null, null);
        }
        private static string Meta(string name, object value) => Row("metadata", null, null, null, name, value, null, null, null, null);
        private static string Row(params object[] fields)
        {
            var cells = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                object value = fields[i];
                string text = value == null ? "" : value is float f ? f.ToString("R", CultureInfo.InvariantCulture) :
                    value is double d ? d.ToString("R", CultureInfo.InvariantCulture) :
                    value is IFormattable number ? number.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
                cells[i] = "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return string.Join(",", cells);
        }
    }
}
