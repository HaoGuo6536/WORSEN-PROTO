// ============================================================================
// ResultsOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run facts into the Results presentation service.
//   Explicit subscriptions keep scene lifetimes separate from persistent services.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Results target.
// KEY RESPONSIBILITIES:
//   - Route terminal HorrorRun summaries, persisted best depth and fixed-seed restart intent.
//   - Supply expedition seed/depth to the Run summary producer at each capture boundary.
//   - Route death identity and camera completion without retaining pending summaries.
//   - Forward supplied values and pair every subscription with teardown.
// DEPENDENCIES:
//   - Core payloads, Session Run/SceneFlow/Progression/Settings, Presentation Results and Camera.
// USAGE NOTES:
//   Setup wires references before activation. Handlers contain routing only.
//   Scene-owned; disabled before its scene publishers and views are destroyed.
// ============================================================================
using UnityEngine;
using System;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.Results;
using Worsen.Presentation.Camera;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.SceneFlow;
using Worsen.Session.Progression;
using Worsen.Session.Settings;
namespace Worsen.Orchestrator
{
    public sealed class ResultsOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private ResultsManager _results;
        [SerializeField] private SceneFlowManager _sceneFlow;
        [SerializeField] private CameraManager _camera;
        private ProgressionSessionManager _progression;
        private SettingsManager _settings;
        private Action<bool, int> _restart;
        public void ConfigureHorrorRun(RunSessionManager run, ResultsManager results, CameraManager camera,
            ProgressionSessionManager progression, SettingsManager settings, Action<bool, int> restart)
        {
            OnDisable(); _run = run; _results = results; _camera = camera;
            _progression = progression; _settings = settings; _restart = restart;
            if (isActiveAndEnabled) OnEnable();
        }
        public void ConfigureCatch(CameraManager camera)
        {
            OnDisable(); _camera = camera;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            OnDisable();
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_results == null || (_sceneFlow == null && _progression == null)) return;
            if (_sceneFlow != null) _sceneFlow = _sceneFlow.Initialize();
            _results.Initialize();
            _results.Hide();
            _run.RunEnded += OnRunEnded;
            _run.PlayerDied += OnDeath;
            _run.CaptureStarted += OnCapture;
            if (_camera != null) _camera.CatchHoldEnded += OnCatchEnded;
            if (_progression == null) _results.RestartRequested += OnRestart;
            else
            {
                _results.RestartWithSeedRequested += OnRestartWithSeed;
                _progression.SnapshotChanged += OnSnapshot;
            }
            if (_settings != null) { _settings.HistoryChanged += OnHistory; OnHistory(_settings.History); }
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
            if (_settings != null) _settings.HistoryChanged -= OnHistory;
            if (_progression != null) _progression.SnapshotChanged -= OnSnapshot;
            if (_results != null)
            { _results.RestartRequested -= OnRestart; _results.RestartWithSeedRequested -= OnRestartWithSeed; _results.Hide(); }
        }
        private void OnDeath(EntityId player, Vector3 position) => _results.PrepareCatch(player);
        private void OnCatchEnded(EntityId player) => _results.EndCatch(player);
        private void OnRunEnded(RunSummary summary)
        {
            if (_progression == null || summary.EndReason == RunEndReason.Died || _progression.Snapshot.Phase == ProgressionPhase.Ended)
                _results.Show(summary);
        }
        private void OnCapture(RunCaptureMetadata metadata)
        {
            _results.Hide();
            if (_progression != null) _run.SetSummaryContext(_progression.Snapshot.Seed, _progression.Snapshot.Round);
        }
        private void OnHistory(RunHistoryRecord history) => _results.SetBestDepth(history.BestDepth);
        private void OnSnapshot(ProgressionSnapshot snapshot) { if (snapshot.Phase != ProgressionPhase.Ended) _results.Hide(); }
        private void OnRestartWithSeed(bool fixedSeed, int seed) => _restart?.Invoke(fixedSeed, seed);
        private void OnRestart() { _results.Hide(); _sceneFlow.RequestLoad(_run.Scene); }
    }
}
