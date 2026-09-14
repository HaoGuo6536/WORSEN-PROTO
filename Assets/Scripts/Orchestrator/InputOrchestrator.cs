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
//   - Persistent, on the InputManager's own root and destroyed with duplicates.
//   - Setup supplies references. Subscriptions pair OnEnable with OnDisable.
//   - M0 buffers input in Session; M1 supplies the Domain player consumer.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Orchestrator
{
    public sealed class InputOrchestrator : MonoBehaviour
    {
        [SerializeField] private InputManager _input;
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private SceneFlowManager _sceneFlow;

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
            _sceneFlow.SceneLoadStarted += OnSceneLoadStarted;
            TagArenaSceneRoot.SceneReady += OnSceneReady;
        }

        private void OnDisable()
        {
            if (_input != null) _input.FramePublished -= OnFramePublished;
            if (_run != null) _run.BeforeTick -= OnBeforeTick;
            if (_sceneFlow != null) _sceneFlow.SceneLoadStarted -= OnSceneLoadStarted;
            TagArenaSceneRoot.SceneReady -= OnSceneReady;
        }

        private void OnBeforeTick() => _input.PublishFrame();
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
