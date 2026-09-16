// ============================================================================
// InputReplayDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains one recording and the playback cursor. It keeps source selection and failures visible without reading devices or scenes.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Store immutable Core records and explicit source/capture state.
// DEPENDENCIES:
//   - Core recording payloads only.
// USAGE NOTES:
//   - Persistent data owned by InputRecorder; no events or engine operations.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    public sealed class InputReplayDriverState
    {
        public InputSource Source;
        public RunCaptureMetadata Metadata;
        public readonly List<InputProbeRecord> Recorded = new List<InputProbeRecord>();
        public InputProbeRecord[] Playback = new InputProbeRecord[0];
        public InputProbeRecord CurrentPlaybackRecord;
        public int Cursor;
        public bool Recording;
        public bool CaptureComplete;
        public bool CaptureInterrupted;
        public long EndTick;
        public string Error = "";
    }
}
