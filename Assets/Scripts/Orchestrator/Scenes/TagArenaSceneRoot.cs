// ============================================================================
// TagArenaSceneRoot.cs
// ============================================================================
// PURPOSE:
//   Assembles the TagArena skeleton through explicitly wired persistent services.
//   Publishes readiness only after initialization succeeds; no gameplay or
//   movement is inferred from the existence of this M0 development scene.
// ARCHITECTURAL ROLE:
//   SceneRoot (§6b) · Orchestrator · TagArena scene assembly.
// KEY RESPONSIBILITIES:
//   - Initialize canonical persistent services and hand off the ready scene.
//   - Supply the designer-selected run seed and explicit no-player telemetry.
// DEPENDENCIES:
//   - Session.Run and SceneFlow; Presentation.Input and DebugOverlay.
// USAGE NOTES:
//   - Scene-owned. Setup wires all fields before the scene is played.
//   - Start is this root's assembly entry point; it explicitly initializes its
//     dependencies instead of depending on any other component's Start.
//   - Static readiness announces a scene to persistent subscribers and resets
//     at SubsystemRegistration for Enter Play Mode without domain reload.
//   - Initialize surviving scene-local references so duplicates retire; fall
//     back to canonical services when a duplicate was destroyed before Start.
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Orchestrator
{
    public sealed class TagArenaSceneRoot : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private SceneFlowManager _sceneFlow;
        [SerializeField] private InputManager _input;
        [SerializeField] private DebugOverlayManager _overlay;
        [SerializeField] private int _seed = 1;

        public static event Action<SceneKey> SceneReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneReady = null;

        private void Start()
        {
            _run = _run != null ? _run.Initialize(_seed) : RunSessionManager.Instance;
            _sceneFlow = _sceneFlow != null ? _sceneFlow.Initialize() : SceneFlowManager.Instance;
            _input = _input != null ? _input.Initialize() : InputManager.Instance;
            _overlay = _overlay != null ? _overlay.Initialize() : DebugOverlayManager.Instance;
            if (_run == null || _sceneFlow == null || _input == null || _overlay == null)
            {
                Debug.LogError("TagArena bootstrap is unwired. Run Worsen/Scenes/1 — Build TagArena.", this);
                return;
            }
            _overlay.SetPlayerUnavailable();
            SceneReady?.Invoke(SceneKey.TagArena);
        }
    }
}
