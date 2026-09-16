// ============================================================================
// InputOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects the persistent input service to the run's fixed simulation tick.
//   Scene readiness and loading gate input explicitly, so the run never depends
//   on Unity Start ordering or captures input while a scene is being replaced.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Input target. Thin event routing only.
// KEY RESPONSIBILITIES:
//   - Request one input publication before each simulation tick.
//   - Route Core input frames and scene lifecycle facts into the run.
// DEPENDENCIES:
//   - Presentation.Input publishes frames; Session.Run owns the simulation.
//   - Session.SceneFlow announces loads; TagArenaSceneRoot announces readiness.
// USAGE NOTES:
//   The canonical persistent input router also gates movement during progression
//   choice/shop screens. Scene assembly binds the persistent Progression service.
//   - Persistent, on the InputManager's own root and destroyed with duplicates.
//   - Setup supplies references. Subscriptions pair OnEnable with OnDisable.
//   - Session relays committed Player probes into recording without binding a
//     persistent router to a scene entity. Input still publishes once per tick.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Worsen.Session.Progression;

namespace Worsen.Orchestrator
{
    public sealed class InputOrchestrator : MonoBehaviour
    {
        [SerializeField] private InputManager _input;
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private SceneFlowManager _sceneFlow;
        private ProgressionSessionManager _progression;

        public void ConfigureProgression(ProgressionSessionManager progression)
        { OnDisable(); _progression = progression; if (isActiveAndEnabled) OnEnable(); }

        private void OnEnable()
        {
            if (_input == null || _run == null || _sceneFlow == null)
            {
                Debug.LogError("Input routing is unwired. Run Worsen/Scenes/1 — Build TagArena.", this);
                return;
            }
            if (_input.Initialize() != _input) return;
            _run = RunSessionManager.Instance ?? _run;
            _sceneFlow = _sceneFlow.Initialize();
            _input.FramePublished += OnFramePublished;
            _run.BeforeTick += OnBeforeTick;
            _run.CaptureStarted += OnCaptureStarted;
            _run.CaptureEnded += OnCaptureEnded;
            _run.PlayerProbeRecorded += OnProbeRecorded;
            _run.RunEnded += OnRunEnded;
            _sceneFlow.SceneLoadStarted += OnSceneLoadStarted;
            TagArenaSceneRoot.SceneReady += OnSceneReady;
            FloorLoopSceneRoot.SceneReady += OnSceneReady;
            HorrorRunSceneRoot.SceneReady += OnSceneReady;
            if (_progression != null) _progression.SnapshotChanged += OnProgressionSnapshot;
        }

        private void OnDisable()
        {
            if (_input != null) _input.FramePublished -= OnFramePublished;
            if (_run != null)
            {
                _run.BeforeTick -= OnBeforeTick;
                _run.CaptureStarted -= OnCaptureStarted;
                _run.CaptureEnded -= OnCaptureEnded;
                _run.PlayerProbeRecorded -= OnProbeRecorded;
                _run.RunEnded -= OnRunEnded;
            }
            if (_sceneFlow != null) _sceneFlow.SceneLoadStarted -= OnSceneLoadStarted;
            TagArenaSceneRoot.SceneReady -= OnSceneReady;
            FloorLoopSceneRoot.SceneReady -= OnSceneReady;
            HorrorRunSceneRoot.SceneReady -= OnSceneReady;
            if (_progression != null) _progression.SnapshotChanged -= OnProgressionSnapshot;
        }

        private void OnCaptureStarted(RunCaptureMetadata metadata) => _input.BeginRecording(metadata);
        private void OnCaptureEnded(long tick, bool complete) => _input.SaveRecording(tick, complete);
        private void OnProbeRecorded(InputProbeRecord record) => _input.RecordProbe(record);
        private void OnRunEnded(RunSummary summary) => _input.SetInputEnabled(false);

        private void OnBeforeTick() => _input.PublishFrame();
        private void OnProgressionSnapshot(ProgressionSnapshot snapshot) => _input.SetInputEnabled(snapshot.Phase == ProgressionPhase.Exploring);
        private void OnFramePublished(InputFrame frame) => _run.ReceiveInput(frame);

        private void OnSceneReady(SceneKey scene)
        {
            _run.HandleSceneReady(scene);
            _input.SetInputEnabled(true);
        }

        private void OnSceneLoadStarted(SceneKey scene)
        {
            _input.SetInputEnabled(false);
            _run.SuspendForSceneLoad();
        }
    }
}
