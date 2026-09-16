// ============================================================================
// TelemetryOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed Session facts into the Telemetry presentation service.
//   It keeps presentation independent of Domain types and survives explicit
//   scene assembly without relying on component startup order.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Telemetry target.
// KEY RESPONSIBILITIES:
//   - Forward Core payloads and pair event subscriptions with component lifetime.
// DEPENDENCIES:
//   - Session.Run and Presentation.Telemetry; Core payloads only.
// USAGE NOTES:
//   Persistent on TelemetryManager's root; binds only to persistent Session. Setup provides serialized references before activation.
//   Handlers contain no remembered gameplay state or engine work.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.Telemetry;

namespace Worsen.Orchestrator
{
    public sealed class TelemetryOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private TelemetryManager _telemetry;

        private void OnEnable()
        {
            if (_run == null || _telemetry == null) return;
            if (_telemetry.Initialize() != _telemetry) return;
            _run = RunSessionManager.Instance ?? _run;
            _run.CaptureStarted += OnCaptureStarted;
            _run.CaptureEnded += OnCaptureEnded;
            _run.PlayerMovementPublished += OnMovement;
            _run.PlayerTraversalPublished += OnTraversal;
            _run.TelemetryPublished += OnTelemetry;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.CaptureStarted -= OnCaptureStarted;
            _run.CaptureEnded -= OnCaptureEnded;
            _run.PlayerMovementPublished -= OnMovement;
            _run.PlayerTraversalPublished -= OnTraversal;
            _run.TelemetryPublished -= OnTelemetry;
        }
        private void OnCaptureStarted(RunCaptureMetadata metadata) => _telemetry.BeginSession(metadata);
        private void OnCaptureEnded(long tick, bool complete) => _telemetry.EndSession(tick, complete);
        private void OnMovement(PlayerMovementSample sample) => _telemetry.RecordMovement(sample);
        private void OnTraversal(PlayerTraversalFact fact) => _telemetry.RecordTraversal(fact);
        private void OnTelemetry(TelemetrySample sample) => _telemetry.Record(sample);
    }
}
