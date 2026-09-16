// ============================================================================
// TelemetryPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts gameplay observations to an auditable capture and derives measurements without changing game state. It retains denominators, unknown outcomes and censored windows instead of replacing missing evidence with zero.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Convert movement edges, validate/deduplicate samples, calculate rates and interval statistics.
// DEPENDENCIES:
//   - Core DTOs and own DriverState/Report only; no Domain references.
// USAGE NOTES:
//   - Chase membership and catch windows include their terminal tick. Accepted hits prove catches independently of confirmed chase intervals; unmatched known catch ends are explicit legacy fallback.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Worsen.Core;

namespace Worsen.Presentation.Telemetry
{
    public sealed class TelemetryPresenter
    {
        public void Begin(TelemetryDriverState state, RunCaptureMetadata metadata)
        {
            state.Metadata = metadata; state.Samples.Clear(); state.PreviousMovement.Clear();
            state.ChaseTransitions.Clear(); state.RecordedEventIds.Clear();
            state.LastTaggedTicks.Clear(); state.LastTaggedChases.Clear(); state.LateChaseTransitions = 0;
            state.ActiveVaults.Clear(); state.CensoredTraversals = 0;
            state.LockReasons.Clear(); state.AvailableStreams.Clear(); state.LastTick = metadata.StartTick;
            state.Active = true; state.Report = null; state.LastError = ""; state.OutputPath = "";
        }

        public void Record(TelemetryDriverState state, TelemetrySample sample)
        {
            if (!state.Active) return;
            state.Samples.Add(sample);
            state.LastTick = Math.Max(state.LastTick, sample.Tick);
            if (!ValidSample(sample, state.Metadata.StartTick, long.MaxValue)) return;
            if (sample.EventId != 0 && !state.RecordedEventIds.Add(sample.EventId)) return;
            state.AvailableStreams.Add(sample.Kind);
            if (sample.Kind != TelemetrySampleKind.ChaseStarted && sample.Kind != TelemetrySampleKind.ChaseEnded) return;
            if (state.LastTaggedTicks.TryGetValue(sample.Player, out long taggedTick) && sample.Tick <= taggedTick)
            {
                bool preservesTerminalTag = sample.Tick == taggedTick && sample.Kind == TelemetrySampleKind.ChaseEnded &&
                    state.LastTaggedChases.TryGetValue(sample.Player, out int taggedChase) && taggedChase == sample.ChaseId;
                if (!preservesTerminalTag) state.LateChaseTransitions++;
            }
            if (!state.ChaseTransitions.TryGetValue(sample.Player, out var transitions))
            { transitions = new List<TelemetrySample>(); state.ChaseTransitions.Add(sample.Player, transitions); }
            transitions.Add(sample);
        }

        public TelemetrySample[] ConvertMovement(TelemetryDriverState state, PlayerMovementSample current)
        {
            var rows = new List<TelemetrySample>();
            if (!state.Active || !current.Id.IsValid || current.Tick < state.Metadata.StartTick) return rows.ToArray();
            bool hadPrevious = state.PreviousMovement.TryGetValue(current.Id, out var previous);
            if (hadPrevious && current.Tick <= previous.Tick) return rows.ToArray();
            int chaseId = ChaseAt(state, current.Id, current.Tick);
            bool inChase = chaseId > 0;
            state.AvailableStreams.Add(TelemetrySampleKind.HorizontalSpeed);
            state.AvailableStreams.Add(TelemetrySampleKind.LookBackStarted);
            state.AvailableStreams.Add(TelemetrySampleKind.InputLockStarted);
            double x = current.Velocity.x, z = current.Velocity.z;
            rows.Add(new TelemetrySample(current.Tick, current.Id, chaseId, TelemetrySampleKind.HorizontalSpeed,
                (float)Math.Sqrt(x * x + z * z), inChase: inChase));
            if (current.LookBack != (hadPrevious && previous.LookBack))
                rows.Add(new TelemetrySample(current.Tick, current.Id, chaseId, current.LookBack ?
                    TelemetrySampleKind.LookBackStarted : TelemetrySampleKind.LookBackEnded, inChase: inChase));
            bool wasLocked = hadPrevious && previous.InputLockSeconds > 0;
            bool isLocked = current.InputLockSeconds > 0;
            if (isLocked && !wasLocked)
            {
                state.LockReasons[current.Id] = current.MovementState.ToString();
                rows.Add(new TelemetrySample(current.Tick, current.Id, chaseId, TelemetrySampleKind.InputLockStarted,
                    detail: state.LockReasons[current.Id], inChase: inChase));
            }
            else if (!isLocked && wasLocked)
            {
                rows.Add(new TelemetrySample(current.Tick, current.Id, chaseId, TelemetrySampleKind.InputLockEnded,
                    detail: state.LockReasons[current.Id], inChase: inChase));
                state.LockReasons.Remove(current.Id);
            }
            state.PreviousMovement[current.Id] = current;
            if (current.MovementState == MovementState.Vault && (!hadPrevious || previous.MovementState != MovementState.Vault))
            {
                if (state.ActiveVaults.ContainsKey(current.Id)) state.CensoredTraversals++;
                state.ActiveVaults[current.Id] = current.Tick;
            }
            return rows.ToArray();
        }

        public TelemetrySample[] ConvertTraversal(TelemetryDriverState state, PlayerTraversalFact fact)
        {
            if (!state.Active || !fact.Id.IsValid || fact.Tick < state.Metadata.StartTick ||
                (fact.Kind != TraversalKind.Vault && fact.Kind != TraversalKind.Mantle))
                return new TelemetrySample[0];
            state.ActiveVaults.Remove(fact.Id);
            int chaseId = ChaseAt(state, fact.Id, fact.Tick);
            var attempt = new TelemetrySample(fact.Tick, fact.Id, chaseId, TelemetrySampleKind.VaultAttempt,
                value: fact.Duration, detail: fact.Kind.ToString(), inChase: chaseId > 0);
            return fact.Succeeded ? new[] { attempt } : new[] { attempt,
                new TelemetrySample(fact.Tick, fact.Id, chaseId, TelemetrySampleKind.VaultFailed, detail: fact.Kind.ToString(), inChase: chaseId > 0) };
        }

        private static int ChaseAt(TelemetryDriverState state, EntityId player, long tick)
        {
            if (!state.ChaseTransitions.TryGetValue(player, out var transitions))
            { RememberChaseTag(state, player, tick, 0); return 0; }
            var active = new Dictionary<int, long>();
            var ended = new HashSet<int>();
            var terminal = new Dictionary<int, long>();
            // Sparse transition history also supports replay rows delivered out of order.
            foreach (var s in transitions.OrderBy(s => s.Tick).ThenBy(s => Priority(s.Kind)))
            {
                if (s.Tick > tick) break;
                if (s.Kind == TelemetrySampleKind.ChaseStarted)
                {
                    if (!active.ContainsKey(s.ChaseId) && !ended.Contains(s.ChaseId)) active.Add(s.ChaseId, s.Tick);
                }
                else if (active.TryGetValue(s.ChaseId, out long start))
                {
                    active.Remove(s.ChaseId); ended.Add(s.ChaseId);
                    if (s.Tick == tick) terminal[s.ChaseId] = start;
                }
            }
            foreach (var pair in terminal) active[pair.Key] = pair.Value;
            int chaseId = active.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).FirstOrDefault();
            RememberChaseTag(state, player, tick, chaseId);
            return chaseId;
        }

        private static void RememberChaseTag(TelemetryDriverState state, EntityId player, long tick, int chaseId)
        {
            if (state.LastTaggedTicks.TryGetValue(player, out long previous) && tick < previous) return;
            state.LastTaggedTicks[player] = tick;
            state.LastTaggedChases[player] = chaseId;
        }

        public TelemetryReport Finish(TelemetryDriverState state, long endTick, bool complete)
        {
            var report = new TelemetryReport { Complete = complete, RawSamples = state.Samples.Count };
            report.LateChaseTransitions = state.LateChaseTransitions;
            if (report.LateChaseTransitions > 0) report.Complete = false;
            if (endTick < state.Metadata.StartTick || endTick < state.LastTick) report.Complete = false;
            state.EndTick = endTick; state.Active = false;
            var samples = Normalize(state, endTick, report);
            var free = samples.Where(s => s.Kind == TelemetrySampleKind.HorizontalSpeed && !s.InChase).Select(s => (double)s.Value).ToList();
            report.FreeSpeedSamples = free.Count;
            report.ChaseSpeedSamples = samples.Count(s => s.Kind == TelemetrySampleKind.HorizontalSpeed && s.InChase);
            report.FreeSpeedP90 = Percentile(free, 0.9);
            var catches = Chases(samples, report, state.Metadata.FixedDeltaTime);
            Intervals(samples, report, state.Metadata.FixedDeltaTime, endTick, catches);
            Vaults(samples, report);
            report.IncompleteVaultAttempts = state.ActiveVaults.Count + state.CensoredTraversals;
            var proximity = samples.Where(s => s.Kind == TelemetrySampleKind.Proximity).ToList();
            report.ProximitySamples = proximity.Count;
            if (proximity.Count > 0)
            {
                double maximum = 0;
                foreach (var player in proximity.GroupBy(s => s.Player))
                {
                    long last = state.Metadata.StartTick;
                    foreach (var close in player.Where(s => s.Value < 20f))
                    {
                        maximum = Math.Max(maximum, (close.Tick - last) * (double)state.Metadata.FixedDeltaTime);
                        last = close.Tick;
                    }
                    maximum = Math.Max(maximum, (endTick - last) * (double)state.Metadata.FixedDeltaTime);
                }
                report.MaximumProximityGapSeconds = maximum;
            }
            var heat = samples.Where(s => s.Kind == TelemetrySampleKind.Heat).ToList();
            if (heat.Count > 0) report.MaximumHeat = heat.Max(s => (double)s.Value);
            var floor = samples.Where(s => s.Kind == TelemetrySampleKind.FloorTime).ToList();
            if (floor.Count > 0) report.FloorSeconds = floor[floor.Count - 1].Value;
            var missing = new List<string>();
            Missing(state, TelemetrySampleKind.HorizontalSpeed, "movement", missing);
            Missing(state, TelemetrySampleKind.ChaseStarted, "chase", missing);
            Missing(state, TelemetrySampleKind.LookBackStarted, "look-back", missing);
            Missing(state, TelemetrySampleKind.InputLockStarted, "input-lock", missing);
            Missing(state, TelemetrySampleKind.VaultAttempt, "vault", missing);
            Missing(state, TelemetrySampleKind.Proximity, "proximity", missing);
            Missing(state, TelemetrySampleKind.Heat, "director heat", missing);
            Missing(state, TelemetrySampleKind.FloorTime, "floor time", missing);
            report.MissingStreams = string.Join("; ", missing);
            state.Report = report;
            return report;
        }

        private static List<TelemetrySample> Normalize(TelemetryDriverState state, long endTick, TelemetryReport report)
        {
            var ids = new HashSet<long>();
            var speedTicks = new HashSet<(EntityId, long)>();
            var result = new List<TelemetrySample>();
            long previous = state.Metadata.StartTick;
            foreach (var sample in state.Samples)
            {
                if (sample.Tick < previous) report.OutOfOrderSamples++;
                previous = Math.Max(previous, sample.Tick);
                if (!ValidSample(sample, state.Metadata.StartTick, endTick))
                { report.InvalidSamples++; continue; }
                if (sample.EventId != 0 && !ids.Add(sample.EventId)) { report.DuplicateSamples++; continue; }
                if (sample.Kind == TelemetrySampleKind.HorizontalSpeed && !speedTicks.Add((sample.Player, sample.Tick)))
                { report.DuplicateSamples++; continue; }
                result.Add(sample);
            }
            result = result.OrderBy(s => s.Tick).ThenBy(s => Priority(s.Kind)).ToList();
            report.AcceptedSamples = result.Count;
            return result;
        }

        private static bool ValidSample(TelemetrySample s, long startTick, long endTick)
        {
            if (s.Tick < startTick || s.Tick > endTick || !s.Player.IsValid || !Finite(s.Value) || s.Value < 0 ||
                !Enum.IsDefined(typeof(TelemetrySampleKind), s.Kind)) return false;
            if (s.Kind == TelemetrySampleKind.ChaseStarted || s.Kind == TelemetrySampleKind.ChaseEnded)
                return s.ChaseId > 0 && Enum.IsDefined(typeof(ChaseEndReason), s.Outcome);
            if (s.Kind == TelemetrySampleKind.AcceptedHit)
                return s.ChaseId >= 0 && Enum.IsDefined(typeof(ChaseEndReason), s.Outcome) && s.Outcome != ChaseEndReason.Lost;
            return true;
        }

        private static int Priority(TelemetrySampleKind kind)
        {
            if (kind == TelemetrySampleKind.ChaseStarted || kind == TelemetrySampleKind.LookBackStarted ||
                kind == TelemetrySampleKind.InputLockStarted || kind == TelemetrySampleKind.VaultAttempt) return 0;
            return 1;
        }

        private static List<TelemetrySample> Chases(List<TelemetrySample> samples, TelemetryReport r, float dt)
        {
            var active = new Dictionary<(EntityId, int), long>();
            var ended = new HashSet<(EntityId, int)>();
            var durations = new List<double>();
            var catches = samples.Where(s => s.Kind == TelemetrySampleKind.AcceptedHit).ToList();
            var acceptedHits = catches.ToLookup(s => (s.Player, s.ChaseId, s.Tick));
            r.AcceptedHitCatches = catches.Count;
            r.PreConfirmationCatches = catches.Count(s => s.ChaseId == 0);
            r.UnknownCatchClassifications = catches.Count(s => s.Outcome == ChaseEndReason.Unknown);
            foreach (var s in samples)
            {
                var key = (s.Player, s.ChaseId);
                if (s.Kind == TelemetrySampleKind.ChaseStarted)
                {
                    if (s.ChaseId <= 0 || active.ContainsKey(key) || ended.Contains(key)) { r.InvalidSamples++; continue; }
                    active.Add(key, s.Tick); r.StartedChases++;
                }
                else if (s.Kind == TelemetrySampleKind.ChaseEnded)
                {
                    if (!active.TryGetValue(key, out long start)) { r.InvalidSamples++; continue; }
                    active.Remove(key); ended.Add(key);
                    var matchingHits = acceptedHits[(s.Player, s.ChaseId, s.Tick)].ToList();
                    if (matchingHits.Count > 0)
                    {
                        r.MatchedCatchEnds++;
                        if (s.Outcome != ChaseEndReason.Unknown && matchingHits.Any(h =>
                            s.Outcome == ChaseEndReason.Lost || (h.Outcome != ChaseEndReason.Unknown && h.Outcome != s.Outcome)))
                            r.CatchOutcomeConflicts++;
                    }
                    if (s.Outcome == ChaseEndReason.Unknown || !Enum.IsDefined(typeof(ChaseEndReason), s.Outcome))
                    { r.UnknownChases++; continue; }
                    durations.Add((s.Tick - start) * (double)dt); r.CompletedChases++;
                    // Accepted hit facts are authoritative; tuple matching suppresses only end fallback.
                    if (matchingHits.Count > 0) continue;
                    if (s.Outcome == ChaseEndReason.Lost) r.Losses++;
                    else { catches.Add(s); r.LegacyCatches++; }
                }
            }
            r.Catches = catches.Count;
            r.LungeCatches = catches.Count(s => s.Outcome == ChaseEndReason.Lunge);
            r.OverallOutcomeCount = r.Losses + r.Catches;
            r.CatchAccountingMode = r.AcceptedHitCatches > 0 ?
                (r.LegacyCatches > 0 ? "accepted-hit-with-legacy-fallback" : "accepted-hit") :
                (r.LegacyCatches > 0 ? "legacy-chase-ended" : "no-catch-evidence");
            r.IncompleteChases = active.Count;
            r.MedianChaseSeconds = Median(durations);
            r.LossRate = Ratio(r.Losses, r.OverallOutcomeCount);
            r.LungeCatchRate = Ratio(r.LungeCatches, r.Catches);
            return catches;
        }

        private static void Intervals(List<TelemetrySample> samples, TelemetryReport r, float dt, long endTick, List<TelemetrySample> catches)
        {
            var locks = new Dictionary<(EntityId, string), long>();
            var look = new Dictionary<EntityId, long>();
            var observedPlayers = new HashSet<EntityId>(samples.Where(s =>
                s.Kind == TelemetrySampleKind.ChaseStarted || s.Kind == TelemetrySampleKind.AcceptedHit).Select(s => s.Player));
            int chaseLookBacks = 0;
            foreach (var s in samples)
            {
                var key = (s.Player, s.Detail ?? "");
                if (s.Kind == TelemetrySampleKind.InputLockStarted)
                { if (locks.ContainsKey(key)) r.InvalidSamples++; else locks.Add(key, s.Tick); }
                else if (s.Kind == TelemetrySampleKind.InputLockEnded)
                {
                    if (!locks.TryGetValue(key, out long start)) { r.InvalidSamples++; continue; }
                    locks.Remove(key); r.CompletedLocks++;
                    double duration = (s.Tick - start) * (double)dt;
                    r.MaximumInputLockSeconds = Math.Max(r.MaximumInputLockSeconds ?? 0, duration);
                }
                else if (s.Kind == TelemetrySampleKind.LookBackStarted)
                {
                    if (look.ContainsKey(s.Player)) r.InvalidSamples++;
                    else { look.Add(s.Player, s.Tick); r.LookBacks++; if (s.InChase) chaseLookBacks++; }
                }
                else if (s.Kind == TelemetrySampleKind.LookBackEnded)
                {
                    if (!look.TryGetValue(s.Player, out long lookStart)) { r.InvalidSamples++; continue; }
                    look.Remove(s.Player); r.CompletedLookBacks++;
                    r.TotalLookBackSeconds = (r.TotalLookBackSeconds ?? 0) + (s.Tick - lookStart) * (double)dt;
                    bool caught = catches.Any(c => c.Player == s.Player && c.Tick >= s.Tick &&
                        (c.Tick - s.Tick) * (double)dt <= 1.0000001);
                    if (caught) { r.CatchesWithinWindow++; r.CatchWindowsKnown++; }
                    else if (observedPlayers.Contains(s.Player) && (endTick - s.Tick) * (double)dt >= 1.0) r.CatchWindowsKnown++;
                    else r.CatchWindowsUnknown++;
                }
            }
            r.IncompleteLocks = locks.Count; r.IncompleteLookBacks = look.Count;
            r.CatchWithinOneSecondRate = Ratio(r.CatchesWithinWindow, r.CatchWindowsKnown);
            r.LookBacksPerChase = Ratio(chaseLookBacks, r.StartedChases);
        }

        private static void Vaults(List<TelemetrySample> samples, TelemetryReport r)
        {
            var pending = new HashSet<(EntityId, long, bool, string)>();
            foreach (var s in samples)
            {
                var key = (s.Player, s.Tick, s.InChase, s.Detail ?? "");
                if (s.Kind == TelemetrySampleKind.VaultAttempt)
                {
                    if (!pending.Add(key)) { r.DuplicateSamples++; continue; }
                    if (s.InChase) r.ChaseVaultAttempts++; else r.FreeVaultAttempts++;
                }
                else if (s.Kind == TelemetrySampleKind.VaultFailed)
                {
                    if (!pending.Remove(key)) { r.InvalidSamples++; continue; }
                    if (s.InChase) r.ChaseVaultFailures++; else r.FreeVaultFailures++;
                }
            }
            r.FreeVaultFailureRate = Ratio(r.FreeVaultFailures, r.FreeVaultAttempts);
            r.ChaseVaultFailureRate = Ratio(r.ChaseVaultFailures, r.ChaseVaultAttempts);
            if (r.FreeVaultFailureRate.HasValue && r.ChaseVaultFailureRate.HasValue)
                r.VaultFailureRateIncrease = r.ChaseVaultFailureRate - r.FreeVaultFailureRate;
        }

        private static void Missing(TelemetryDriverState s, TelemetrySampleKind kind, string name, List<string> missing)
        { if (!s.AvailableStreams.Contains(kind)) missing.Add(name); }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static double? Ratio(int n, int d) => d == 0 ? (double?)null : (double)n / d;
        private static double? Percentile(List<double> values, double fraction)
        { if (values.Count == 0) return null; values.Sort(); return values[(int)Math.Ceiling(values.Count * fraction) - 1]; }
        private static double? Median(List<double> values)
        { if (values.Count == 0) return null; values.Sort(); int m = values.Count / 2; return values.Count % 2 == 1 ? values[m] : (values[m - 1] + values[m]) / 2; }
    }
}
