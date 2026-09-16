// ============================================================================
// ResultsOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run facts into the Results presentation service.
//   Explicit subscriptions keep scene lifetimes separate from persistent services.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Results target.
// KEY RESPONSIBILITIES:
//   - Forward supplied values and pair every subscription with teardown.
// DEPENDENCIES:
//   - Core event payloads, Session Run and the target Presentation Manager.
// USAGE NOTES:
//   Setup wires references before activation. Handlers contain routing only.
//   Scene-owned; disabled before its scene publishers and views are destroyed.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.Results;
using Worsen.Session.SceneFlow;
namespace Worsen.Orchestrator
{
    public sealed class ResultsOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private ResultsManager _results;
        [SerializeField] private SceneFlowManager _sceneFlow;
        private void OnEnable()
        {
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_results == null || _sceneFlow == null) return;
            _sceneFlow = _sceneFlow.Initialize();
            _results.Initialize();
            _results.Hide();
            _run.RunEnded += OnRunEnded;
            _run.CaptureStarted += OnCapture;
            _results.RestartRequested += OnRestart;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.RunEnded -= OnRunEnded;
            _run.CaptureStarted -= OnCapture;
            _results.RestartRequested -= OnRestart;
        }
        private void OnRunEnded(RunSummary summary) => _results.Show(summary);
        private void OnCapture(RunCaptureMetadata metadata) => _results.Hide();
        private void OnRestart() => _sceneFlow.RequestLoad(_run.Scene);
    }
}
