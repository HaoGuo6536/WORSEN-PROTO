// ============================================================================
// HUDOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run facts into the HUD presentation service.
//   Explicit subscriptions keep scene lifetimes separate from persistent services.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · HUD target.
// KEY RESPONSIBILITIES:
//   - Route fixed-total counters and floor visibility through Run, including late binding.
//   - Route independent guidance, shield and unshaken camera aim to HUD.
//   - Route Progression inventory/selection to HUD and Run telemetry.
//   - Pair subscriptions with teardown and reset presentation at floor capture.
//   - Forward chase facts without computing gameplay or presentation rules.
// DEPENDENCIES:
//   - Session Progression supplies held items; the legacy Player inventory is not read.
//   - Core payloads, Session Run typed fact channels, HUD and Camera Presentation Managers.
// USAGE NOTES:
//   Setup wires references before activation. Handlers contain routing only.
//   Scene-owned; disabled before its scene publishers and views are destroyed.
//   Execution order 100 samples the camera aim after its default-order LateUpdate.
// ============================================================================
using UnityEngine;
using System.Collections.Generic;
using Worsen.Core;
using Worsen.Session.Run;
using Worsen.Session.Progression;
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
        private ProgressionSessionManager _progression;
        private void LateUpdate()
        {
            if (_hud != null && _camera != null) _hud.SetViewRotation(_camera.AimRotation);
        }
        private void OnEnable()
        {
            if (_run == null) return;
            OnDisable();
            _run = RunSessionManager.Instance ?? _run;
            if (_hud == null) return;
            _hud.Initialize();
            _run.FloorDisplayChanged += OnDisplay;
            _run.FloorFacts.GuidanceChanged += OnGuidance;
            _run.HunterFacts.TickingGuidancePublished += OnThreat;
            _run.PlayerFacts.ShieldChanged += OnShield;
            _run.PublishShieldSnapshot();
            _run.PlayerMovementPublished += OnMovement;
            BindProgression();
            _run.ChaseStarted += OnChase;
            _run.ChaseEnded += OnChaseEnd;
            _run.CaptureStarted += OnCaptureStarted;
            _run.PublishFloorSnapshot();
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.FloorDisplayChanged -= OnDisplay;
            _run.FloorFacts.GuidanceChanged -= OnGuidance;
            _run.HunterFacts.TickingGuidancePublished -= OnThreat;
            _run.PlayerFacts.ShieldChanged -= OnShield;
            if (_hud != null) { _hud.ResetRunView(); _hud.SetGuidance(null); }
            _run.PlayerMovementPublished -= OnMovement;
            if (_progression != null) _progression.ConsumablesChanged -= OnSlots;
            _progression = null;
            _run.ChaseStarted -= OnChase;
            _run.ChaseEnded -= OnChaseEnd;
            _run.CaptureStarted -= OnCaptureStarted;
        }
        private void OnDisplay(FloorDisplaySnapshot display)
        {
            _hud.SetFloorCounters(display);
            _hud.SetExitState(display.Exit);
        }
        private void OnMovement(PlayerMovementSample sample) => _hud.SetHeading(sample.HeadingDegrees);
        private void OnGuidance(IReadOnlyList<GuidanceTarget> targets) => _hud.SetGuidance(targets);
        private void OnThreat(TickingGuidanceFact fact) => _hud.SetThreat(fact);
        private void OnShield(Worsen.Core.EntityId player, float shield) => _hud.SetShield(shield);
        private void BindProgression()
        {
            if (_progression != null) return;
            _progression = ProgressionSessionManager.Instance;
            if (_progression != null) { _progression.ConsumablesChanged += OnSlots; OnSlots(_progression.Consumables); }
        }
        private void OnSlots(ConsumableInventorySnapshot snapshot) { _hud.SetConsumables(snapshot); _run.PublishInventory(snapshot); }
        private void OnChase(ChaseFact fact) => _hud.SetChaseMode(true);
        private void OnChaseEnd(ChaseFact fact) => _hud.SetChaseMode(false);
        private void OnCaptureStarted(RunCaptureMetadata metadata)
        { BindProgression(); _hud.ResetRunView(); _hud.SetGuidance(null); _run.PublishFloorSnapshot(); OnSlots(_progression != null ? _progression.Consumables : default); _run.PublishShieldSnapshot(); }
    }
}
