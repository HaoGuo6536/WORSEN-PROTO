// ============================================================================
// DebugOverlayOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Sends run tick and phase facts to the development overlay. The overlay has
//   no Session dependency and receives only primitive display values here.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · DebugOverlay target.
// KEY RESPONSIBILITIES:
//   - Pair hand-fact routing and clear prior-floor diagnostics at capture/load boundaries.
//   - Forward completed simulation ticks and phase changes to the overlay.
// DEPENDENCIES:
//   - Session.Run publishes facts; Presentation.DebugOverlay displays them.
// USAGE NOTES:
//   - Persistent on DebugOverlayManager's own root, with serialized wiring.
//   - Paired OnEnable/OnDisable subscriptions; no scene-owned references.
//   - Committed Player samples replace the former unavailable placeholder.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.DebugOverlay;
using Worsen.Session.Run;

namespace Worsen.Orchestrator
{
    public sealed class DebugOverlayOrchestrator : MonoBehaviour
    {
        [SerializeField] private DebugOverlayManager _overlay;
        [SerializeField] private RunSessionManager _run;

        private void OnEnable()
        {
            if (_overlay == null || _run == null)
            {
                Debug.LogError("Debug overlay routing is unwired. Rebuild TagArena.", this);
                return;
            }
            if (_overlay.Initialize() != _overlay) return;
            OnDisable();
            _run = RunSessionManager.Instance ?? _run;
            _run.TickAdvanced += OnTickAdvanced;
            _run.PlayerMovementPublished += OnMovement;
            _run.PhaseChanged += OnPhaseChanged;
            _run.CollapseHandPublished += OnHand;
            _run.CaptureStarted += OnCapture;
        }

        private void OnDisable()
        {
            if (_run == null) return;
            _run.TickAdvanced -= OnTickAdvanced;
            _run.PlayerMovementPublished -= OnMovement;
            _run.PhaseChanged -= OnPhaseChanged;
            _run.CollapseHandPublished -= OnHand;
            _run.CaptureStarted -= OnCapture;
            if (_overlay != null) _overlay.ResetHands();
        }

        private void OnMovement(PlayerMovementSample sample)
            => _overlay.SetPlayerStatus(new Vector2(sample.Velocity.x, sample.Velocity.z).magnitude, sample.MovementState.ToString());

        private void OnTickAdvanced(InputFrame input, float deltaTime, long tick)
            => _overlay.SetRunStatus(tick, _run.Phase.ToString());

        private void OnPhaseChanged(RunPhase phase)
        {
            _overlay.SetRunStatus(_run.Tick, phase.ToString());
            if (phase == RunPhase.Boot) _overlay.ResetHands();
        }
        private void OnHand(CollapseHandFact fact) => _overlay.SetCollapseHand(fact);
        private void OnCapture(RunCaptureMetadata metadata) => _overlay.ResetHands();
    }
}
