// ============================================================================
// ResultsOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run facts into the Results presentation service.
//   Explicit subscriptions keep scene lifetimes separate from persistent services.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Results target.
// KEY RESPONSIBILITIES:
//   - Route death identity and camera completion without retaining pending summaries.
//   - Forward supplied values and pair every subscription with teardown.
// DEPENDENCIES:
//   - Core payloads, Session Run/SceneFlow, Presentation Results and Camera.
// USAGE NOTES:
//   Setup wires references before activation. Handlers contain routing only.
//   Scene-owned; disabled before its scene publishers and views are destroyed.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.Results;
using Worsen.Presentation.Camera;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.SceneFlow;
namespace Worsen.Orchestrator
{
    public sealed class ResultsOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private ResultsManager _results;
        [SerializeField] private SceneFlowManager _sceneFlow;
        [SerializeField] private CameraManager _camera;
        public void ConfigureCatch(CameraManager camera)
        {
            OnDisable(); _camera = camera;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_results == null || _sceneFlow == null) return;
            _sceneFlow = _sceneFlow.Initialize();
            _results.Initialize();
            _results.Hide();
            _run.RunEnded += OnRunEnded;
            _run.PlayerDied += OnDeath;
            _run.CaptureStarted += OnCapture;
            if (_camera != null) _camera.CatchHoldEnded += OnCatchEnded;
            _results.RestartRequested += OnRestart;
        }
        private void OnDisable()
        {
            if (_run != null)
            {
                _run.RunEnded -= OnRunEnded;
                _run.PlayerDied -= OnDeath;
                _run.CaptureStarted -= OnCapture;
            }
            if (_camera != null) _camera.CatchHoldEnded -= OnCatchEnded;
            if (_results != null) { _results.RestartRequested -= OnRestart; _results.Hide(); }
        }
        private void OnDeath(EntityId player, Vector3 position) => _results.PrepareCatch(player);
        private void OnCatchEnded(EntityId player) => _results.EndCatch(player);
        private void OnRunEnded(RunSummary summary) => _results.Show(summary);
        private void OnCapture(RunCaptureMetadata metadata) => _results.Hide();
        private void OnRestart() { _results.Hide(); _sceneFlow.RequestLoad(_run.Scene); }
    }
}
