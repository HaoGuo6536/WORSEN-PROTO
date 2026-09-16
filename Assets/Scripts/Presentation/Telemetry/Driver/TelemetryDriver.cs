// ============================================================================
// TelemetryDriver.cs
// ============================================================================
// PURPOSE:
//   Owns one CSV file per capture under the persistent application directory. It delegates calculations to pure presenters and exposes creation/flush failures instead of claiming a missing file was recorded.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Open unique files, write raw facts and provenance, flush completed or interrupted captures.
// DEPENDENCIES:
//   - Core values, own Presenter/DriverState/Config, Unity paths and System.IO.
// USAGE NOTES:
//   - Persistent with TelemetryManager. Serialized config first, Resources fallback, temporary defaults with warning. No global engine settings; writer closes on owner teardown.
// ============================================================================
using System;
using System.IO;
using System.Text;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Telemetry
{
    [DisallowMultipleComponent]
    public sealed class TelemetryDriver : MonoBehaviour
    {
        [SerializeField] private TelemetryDriverConfig _config;
        private readonly TelemetryDriverState _state = new TelemetryDriverState();
        private readonly TelemetryPresenter _presenter = new TelemetryPresenter();
        private readonly TelemetryCsvPresenter _csv = new TelemetryCsvPresenter();
        private StreamWriter _writer;
        private Func<string, Stream> _openOutput;
        private bool _ownsConfig;
        public string LastError => _state.LastError;
        public string LastOutputPath => _state.OutputPath;

        public void Initialize(Func<string, Stream> openOutput = null)
        {
            _openOutput = openOutput ?? OpenOutput;
            if (_config != null) return;
            _config = Resources.Load<TelemetryDriverConfig>("ScriptableObjects/Presentation/Telemetry/TelemetryDriverConfig");
            if (_config == null)
            {
                Debug.LogWarning("Telemetry config missing; run Worsen/Telemetry/Create Missing Config. Using temporary defaults.", this);
                _config = ScriptableObject.CreateInstance<TelemetryDriverConfig>();
                _ownsConfig = true;
            }
        }

        public void BeginSession(RunCaptureMetadata metadata, string outputDirectory = null)
        {
            if (_state.Active) EndSession(_state.LastTick, false);
            _presenter.Begin(_state, metadata);
            if (metadata.StartTick < 0 || float.IsNaN(metadata.FixedDeltaTime) || float.IsInfinity(metadata.FixedDeltaTime) ||
                metadata.FixedDeltaTime <= 0 || string.IsNullOrWhiteSpace(metadata.SessionId) ||
                string.IsNullOrWhiteSpace(metadata.SourceRevision) || string.IsNullOrWhiteSpace(metadata.ConfigSnapshotHash) ||
                string.IsNullOrWhiteSpace(metadata.RandomConsumptionOrder))
            {
                Fail("Capture requires timing and source/config/random-order provenance.");
                return;
            }
            try
            {
                string folder = outputDirectory ?? Path.Combine(Application.persistentDataPath, "Telemetry");
                Directory.CreateDirectory(folder);
                _state.OutputPath = Path.Combine(folder, "session-" + Guid.NewGuid().ToString("N") + ".csv");
                _writer = new StreamWriter(_openOutput(_state.OutputPath),
                    new UTF8Encoding(false));
                _writer.WriteLine(_csv.Header);
                foreach (string row in _csv.Metadata(metadata)) _writer.WriteLine(row);
                _writer.Flush();
            }
            catch (Exception exception) when (IsFileFailure(exception)) { Fail(exception.Message); }
        }

        public void Record(TelemetrySample sample)
        {
            if (!_state.Active) return;
            _presenter.Record(_state, sample);
            if (_writer == null) return;
            try
            {
                _writer.WriteLine(_csv.Raw(sample));
                if (_state.Samples.Count % _config.FlushEverySamples == 0) _writer.Flush();
            }
            catch (Exception exception) when (IsFileFailure(exception)) { Fail(exception.Message); }
        }

        public void RecordMovement(PlayerMovementSample sample)
        {
            if (!_state.Active) return;
            foreach (var row in _presenter.ConvertMovement(_state, sample)) Record(row);
        }
        public void RecordTraversal(PlayerTraversalFact fact)
        {
            if (!_state.Active) return;
            _state.AvailableStreams.Add(TelemetrySampleKind.VaultAttempt);
            foreach (var row in _presenter.ConvertTraversal(_state, fact)) Record(row);
        }

        public bool EndSession(long endTick, bool complete)
        {
            if (!_state.Active) return false;
            var report = _presenter.Finish(_state, endTick, complete && _state.LastError.Length == 0);
            if (_writer == null) return false;
            try
            {
                foreach (string row in _csv.Summary(report, endTick)) _writer.WriteLine(row);
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
                return true;
            }
            catch (Exception exception) when (IsFileFailure(exception)) { Fail(exception.Message); return false; }
        }

        public void Teardown()
        {
            Suspend();
            if (_ownsConfig) { Destroy(_config); _config = null; _ownsConfig = false; }
        }
        public void Suspend() { if (_state.Active) EndSession(_state.LastTick, false); }
        private void OnDestroy() => Teardown();
        private static Stream OpenOutput(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        private static bool IsFileFailure(Exception e) => e is IOException || e is UnauthorizedAccessException || e is ArgumentException;
        private void Fail(string error)
        {
            _state.LastError = "Telemetry write failed: " + error;
            _state.OutputPath = "";
            try { _writer?.Dispose(); } catch (IOException) { }
            _writer = null;
            Debug.LogError(_state.LastError, this);
        }
    }
}
