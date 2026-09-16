// ============================================================================
// InputRecorder.cs
// ============================================================================
// PURPOSE:
//   Owns recording files and the selected replay stream. It uses pure presenters for validation/serialization and makes output failures visible to its owning Driver.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by PlayerInputDriver · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Persist one input/probe recording; report failure; serve exact playback frames.
// DEPENDENCIES:
//   - Core payloads, own replay/recording presenters, System.IO and Unity Application path.
// USAGE NOTES:
//   - Persistent with PlayerInputDriver; no global input changes. Files use unique names under persistentDataPath/InputRecordings; partial files are marked incomplete.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    [DisallowMultipleComponent]
    public sealed class InputRecorder : MonoBehaviour
    {
        private readonly InputReplayDriverState _state = new InputReplayDriverState();
        private readonly InputReplayPresenter _replay = new InputReplayPresenter();
        private readonly InputRecordingPresenter _format = new InputRecordingPresenter();
        public InputSource Source => _state.Source;
        public InputProbeRecord CurrentPlaybackRecord => _state.CurrentPlaybackRecord;
        public string LastError => _state.Error;
        public string LastRecordingPath { get; private set; } = "";

        public void BeginRecording(RunCaptureMetadata metadata)
        {
            _replay.BeginRecording(_state, metadata);
            LastRecordingPath = "";
            if (!_replay.ValidMetadata(metadata))
            {
                _state.Error = "Recording requires valid timing and source/config/random-order provenance.";
                _state.Recording = false;
            }
        }
        public bool RecordProbe(InputProbeRecord record) => _replay.Record(_state, record);
        public bool StartPlayback(RunCaptureMetadata metadata, IReadOnlyList<InputProbeRecord> records, bool gatesOpen) =>
            _replay.StartPlayback(_state, metadata, records, gatesOpen);
        public bool TryReadPlayback(bool gatesOpen, out InputFrame frame) => _replay.TryReadPlayback(_state, gatesOpen, out frame);
        public void StopPlayback() => _replay.StopPlayback(_state);

        public bool SaveRecording(long endTick, bool complete)
        {
            _replay.FinishRecording(_state, endTick, complete);
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "InputRecordings");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "input-" + Guid.NewGuid().ToString("N") + ".winput");
                byte[] bytes = _format.Encode(_state);
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                LastRecordingPath = path;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                _state.Error = "Recording write failed: " + exception.Message;
                LastRecordingPath = "";
                Debug.LogError(_state.Error, this);
                return false;
            }
        }

        public bool LoadPlayback(string absolutePath, bool gatesOpen)
        {
            _replay.StopPlayback(_state);
            try
            {
                byte[] bytes = File.ReadAllBytes(absolutePath);
                if (!_format.TryDecode(bytes, out var metadata, out var records, out string error))
                {
                    _state.Error = error;
                    return false;
                }
                return _replay.StartPlayback(_state, metadata, records, gatesOpen);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                _state.Error = "Playback load failed: " + exception.Message;
                return false;
            }
        }

        public void Interrupt()
        {
            _replay.StopPlayback(_state);
            if (_state.Recording)
            {
                _state.Recording = false;
                _state.CaptureComplete = false;
                _state.CaptureInterrupted = true;
                _state.Error = "Capture interrupted by an input, focus, owner or scene gate.";
            }
        }

        private void OnDestroy() => Interrupt();
    }
}
