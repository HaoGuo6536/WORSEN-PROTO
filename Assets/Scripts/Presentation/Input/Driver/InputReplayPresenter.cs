// ============================================================================
// InputReplayPresenter.cs
// ============================================================================
// PURPOSE:
//   Chooses and advances exactly one input source. It validates tick-aligned records and restores live selection after playback without modifying focus or scene gates.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Copy validated playback data; preserve exact frame values; enforce capture ordering.
// DEPENDENCIES:
//   - Core InputProbeRecord and RunCaptureMetadata, own InputReplayDriverState.
// USAGE NOTES:
//   - No engine calls; gating is supplied explicitly. Sprint remains the compatible hold-for-precision mask.
// ============================================================================
using System;
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    public sealed class InputReplayPresenter
    {
        public void BeginRecording(InputReplayDriverState state, RunCaptureMetadata metadata)
        {
            StopPlayback(state);
            state.Metadata = metadata;
            state.Recorded.Clear();
            state.Error = "";
            state.Recording = true;
            state.CaptureComplete = false;
            state.CaptureInterrupted = false;
        }

        public bool Record(InputReplayDriverState state, InputProbeRecord record)
        {
            if (!state.Recording || state.Source != InputSource.Live)
                return false;
            long expected = state.Recorded.Count == 0 ? state.Metadata.StartTick + 1 :
                state.Recorded[state.Recorded.Count - 1].Tick + 1;
            if (!ValidRecord(record) || record.Tick != expected ||
                record.DeltaTime != state.Metadata.FixedDeltaTime)
            {
                state.Error = "Recording requires contiguous ticks, supported schema, finite input/probe values and the captured fixed timestep.";
                state.Recording = false;
                state.CaptureComplete = false;
                state.CaptureInterrupted = true;
                return false;
            }
            state.Recorded.Add(record);
            return true;
        }

        public bool StartPlayback(InputReplayDriverState state, RunCaptureMetadata metadata,
            IReadOnlyList<InputProbeRecord> records, bool gatesOpen)
        {
            StopPlayback(state);
            if (state.Recording) state.CaptureInterrupted = true;
            state.Recording = false;
            state.Error = "";
            if (!gatesOpen || !ValidMetadata(metadata) || records == null || records.Count == 0)
                return Fail(state, "Playback needs open scene/owner/focus gates, provenance and at least one record.");
            var copy = new InputProbeRecord[records.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                var record = records[i];
                if (!ValidRecord(record) || record.Tick != metadata.StartTick + i + 1 ||
                    record.DeltaTime != metadata.FixedDeltaTime)
                    return Fail(state, "Playback rejected unsupported, nonfinite, discontinuous or timestep-mismatched records.");
                copy[i] = record;
            }
            state.Playback = copy;
            state.Cursor = 0;
            state.Source = InputSource.Playback;
            return true;
        }

        public bool TryReadPlayback(InputReplayDriverState state, bool gatesOpen, out InputFrame frame)
        {
            frame = default;
            if (state.Source != InputSource.Playback)
                return false;
            if (!gatesOpen || state.Cursor >= state.Playback.Length)
            {
                StopPlayback(state);
                return false;
            }
            state.CurrentPlaybackRecord = state.Playback[state.Cursor++];
            frame = state.CurrentPlaybackRecord.Input;
            if (state.Cursor == state.Playback.Length)
                state.Source = InputSource.Live;
            return true;
        }

        public void StopPlayback(InputReplayDriverState state)
        {
            state.Source = InputSource.Live;
            state.Cursor = 0;
            state.Playback = new InputProbeRecord[0];
        }

        public bool FinishRecording(InputReplayDriverState state, long endTick, bool complete)
        {
            state.Recording = false;
            state.EndTick = endTick;
            state.CaptureComplete = complete && !state.CaptureInterrupted && state.Error.Length == 0 && state.Recorded.Count > 0 &&
                state.Recorded[state.Recorded.Count - 1].Tick == endTick;
            return state.CaptureComplete;
        }

        public bool ValidMetadata(RunCaptureMetadata metadata)
        {
            return !string.IsNullOrWhiteSpace(metadata.SessionId) && metadata.StartTick >= 0 &&
                Finite(metadata.FixedDeltaTime) && metadata.FixedDeltaTime > 0 &&
                !string.IsNullOrWhiteSpace(metadata.SourceRevision) &&
                !string.IsNullOrWhiteSpace(metadata.ConfigSnapshotHash) &&
                !string.IsNullOrWhiteSpace(metadata.RandomConsumptionOrder);
        }

        private static bool Fail(InputReplayDriverState state, string error)
        {
            state.Error = error;
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(UnityEngine.Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(UnityEngine.Vector2 value) => Finite(value.x) && Finite(value.y);

        private static bool ValidRecord(InputProbeRecord record)
        {
            var p = record.Probe;
            var input = record.Input;
            const InputButtons known = InputButtons.Sprint | InputButtons.Jump | InputButtons.Crouch |
                InputButtons.LookBack | InputButtons.Interact | InputButtons.UseItem;
            return record.SchemaVersion == 1 && record.Tick > 0 && Finite(record.DeltaTime) && record.DeltaTime > 0 &&
                Finite(input.Move) && Finite(input.LookDelta) &&
                ((input.Held | input.Pressed | input.Released) & ~known) == InputButtons.None &&
                Finite(p.GroundNormal) && Finite(p.WallDistance) && Finite(p.WallNormal) &&
                Finite(p.WallAngleDegrees) && Finite(p.VaultHeight) && Finite(p.VaultClearance) && Finite(p.VaultTarget) &&
                (!record.Resolution.Present || (Finite(record.Resolution.Position) &&
                    Finite(record.Resolution.Velocity) && Finite(record.Resolution.EyePosition)));
        }
    }
}
