// ============================================================================
// HUDOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run facts into the HUD presentation service.
//   Explicit subscriptions keep scene lifetimes separate from persistent services.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · HUD target.
// KEY RESPONSIBILITIES:
//   - Forward supplied values and pair every subscription with teardown.
//   - Reset chase presentation when a new generated floor capture begins.
//   - Forward the current unshaken camera aim after camera LateUpdate for the 3D compass.
// DEPENDENCIES:
//   - Core event payloads, Session Run, HUD and Camera Presentation Managers.
// USAGE NOTES:
//   Setup wires references before activation. Handlers contain routing only.
//   Scene-owned; disabled before its scene publishers and views are destroyed.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Camera;
namespace Worsen.Orchestrator
{
    [DefaultExecutionOrder(100)]
    public sealed class HUDOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private HUDManager _hud;
        [SerializeField] private CameraManager _camera;
        private void LateUpdate()
        {
            if (_hud != null && _camera != null) _hud.SetViewRotation(_camera.AimRotation);
        }
        private void OnEnable()
        {
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_hud == null) return;
            _hud.Initialize();
            _run.FloorDisplayChanged += OnDisplay;
            _run.PlayerMovementPublished += OnMovement;
            _run.EmptyItemSlotsChanged += OnSlots;
            _run.ChaseStarted += OnChase;
            _run.ChaseEnded += OnChaseEnd;
            _run.CaptureStarted += OnCaptureStarted;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.FloorDisplayChanged -= OnDisplay;
            _run.PlayerMovementPublished -= OnMovement;
            _run.EmptyItemSlotsChanged -= OnSlots;
            _run.ChaseStarted -= OnChase;
            _run.ChaseEnded -= OnChaseEnd;
            _run.CaptureStarted -= OnCaptureStarted;
        }
        private void OnDisplay(FloorDisplaySnapshot display)
        {
            _hud.SetCount(display.Collected, display.Required);
            _hud.SetExitState(display.Exit);
            _hud.SetDirection(display.CueDirection, display.HasCue);
        }
        private void OnMovement(PlayerMovementSample sample) => _hud.SetHeading(sample.HeadingDegrees);
        private void OnSlots(int count) => _hud.SetItemSlots(count);
        private void OnChase(ChaseFact fact) => _hud.SetChaseMode(true);
        private void OnChaseEnd(ChaseFact fact) => _hud.SetChaseMode(false);
        private void OnCaptureStarted(RunCaptureMetadata metadata) => _hud.ResetRunView();
    }
}
