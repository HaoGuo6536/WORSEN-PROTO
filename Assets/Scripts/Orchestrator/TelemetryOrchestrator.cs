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
//   - Route accepted progression transactions and generation requests without retaining snapshots.
//   - Translate Hunter-local stalls into Core observations with the current Run seed.
// DEPENDENCIES:
//   - Session.Run/Progression and Presentation.Telemetry; Core payloads downstream.
//   - Domain.Hunter registry and stall facts are translated at this top-layer boundary.
// USAGE NOTES:
//   Persistent on TelemetryManager's root. Setup provides serialized references before activation.
//   ConfigureProgression must run before StartRun; it rebinds via OnDisable/OnEnable.
//   Hunter subscriptions are capture-scoped wiring, refreshed at readiness and released at capture end.
//   Handlers contain no remembered gameplay state or engine work.
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using Worsen.Presentation.Telemetry;

namespace Worsen.Orchestrator
{
    public sealed class TelemetryOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private TelemetryManager _telemetry;
        [SerializeField] private ProgressionSessionManager _progression;
        private HunterManager[] _hunters = Array.Empty<HunterManager>();

        public void ConfigureProgression(ProgressionSessionManager progression)
        {
            OnDisable();
            _progression = progression;
            if (isActiveAndEnabled) OnEnable();
        }

        private void OnEnable()
        {
            OnDisable();
            if (_run == null || _telemetry == null) return;
            if (_telemetry.Initialize() != _telemetry) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_telemetry.CaptureActive)
                _hunters = new System.Collections.Generic.List<HunterManager>(HunterRegistry.Items).ToArray();
            _run.CaptureStarted += OnCaptureStarted;
            _run.CaptureEnded += OnCaptureEnded;
            _run.PlayerMovementPublished += OnMovement;
            _run.PlayerTraversalPublished += OnTraversal;
            _run.TelemetryPublished += OnTelemetry;
            if (_progression != null)
            {
                _progression.TransactionCommitted += OnProgression;
                _progression.GenerationRequested += OnGeneration;
            }
            foreach (HunterManager hunter in _hunters) if (hunter != null) hunter.OnStall += OnStall;
        }
        private void OnDisable()
        {
            foreach (HunterManager hunter in _hunters) if (hunter != null) hunter.OnStall -= OnStall;
            _hunters = Array.Empty<HunterManager>();
            if (_progression != null)
            {
                _progression.TransactionCommitted -= OnProgression;
                _progression.GenerationRequested -= OnGeneration;
            }
            if (_run == null) return;
            _run.CaptureStarted -= OnCaptureStarted;
            _run.CaptureEnded -= OnCaptureEnded;
            _run.PlayerMovementPublished -= OnMovement;
            _run.PlayerTraversalPublished -= OnTraversal;
            _run.TelemetryPublished -= OnTelemetry;
        }
        private void OnCaptureStarted(RunCaptureMetadata metadata)
        { _telemetry.BeginSession(metadata); OnEnable(); }
        private void OnCaptureEnded(long tick, bool complete)
        { _telemetry.EndSession(tick, complete); OnEnable(); }
        private void OnProgression(ProgressionSnapshot before, ProgressionSnapshot after, string operation, string choiceId)
            => _telemetry.RecordProgression(before, after, operation, choiceId, _run.Tick);
        private void OnGeneration(ProgressionGenerationRequest request) => _telemetry.RecordGeneration(request, _run.Tick);
        private void OnStall(HunterStallFact fact)
            => _telemetry.RecordObservation(TranslateStall(fact,
                HunterRegistry.TryGet(fact.Hunter, out var hunter) ? hunter.ArchetypeKey : string.Empty, _run.Seed));
        public static TelemetrySample TranslateStall(HunterStallFact fact, string archetypeKey, int seed)
            => new TelemetryObservationPresenter().Stall(fact.Hunter, archetypeKey, fact.Tick, fact.Position, fact.RoomId,
                fact.RemainingDistance, fact.AgentRadius, fact.CapsuleRadius, fact.MotorRadius, fact.Action.ToString(),
                fact.PathCorners, fact.NearestObstaclePoint, seed);
        private void OnMovement(PlayerMovementSample sample) => _telemetry.RecordMovement(sample);
        private void OnTraversal(PlayerTraversalFact fact) => _telemetry.RecordTraversal(fact);
        private void OnTelemetry(TelemetrySample sample) => _telemetry.Record(sample);
    }
}
