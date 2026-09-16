// ============================================================================
// CameraOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed Session facts into the Camera presentation service.
//   It keeps presentation independent of Domain types and survives explicit
//   scene assembly without relying on component startup order.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Camera target.
// KEY RESPONSIBILITIES:
//   - Forward Core payloads and pair event subscriptions with component lifetime.
// DEPENDENCIES:
//   - Session.Run and Presentation.Camera; Core payloads only.
// USAGE NOTES:
//   Scene-owned; release subscriptions before scene cameras are destroyed. Setup provides serialized references before activation.
//   Handlers contain no remembered gameplay state or engine work.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;
using Worsen.Presentation.Camera;

namespace Worsen.Orchestrator
{
    public sealed class CameraOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private CameraManager _camera;
        private void OnEnable()
        {
            if (_run == null || _camera == null) return;
            _run = RunSessionManager.Instance ?? _run;
            _camera.Initialize();
            _run.PlayerMovementPublished += OnMovement;
            _run.PlayerTraversalPublished += OnTraversal;
            _run.CaptureStarted += OnCaptureStarted;
            _run.ChaseStarted += OnChaseStarted;
            _run.ProximityPublished += OnProximity;
            _run.PlayerDied += OnDeath;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.PlayerMovementPublished -= OnMovement;
            _run.PlayerTraversalPublished -= OnTraversal;
            _run.CaptureStarted -= OnCaptureStarted;
            _run.ChaseStarted -= OnChaseStarted;
            _run.ProximityPublished -= OnProximity;
            _run.PlayerDied -= OnDeath;
        }
        private void OnMovement(PlayerMovementSample sample) => _camera.SetMovement(sample);
        private void OnTraversal(PlayerTraversalFact fact) => _camera.PlayTraversal(fact);
        private void OnCaptureStarted(RunCaptureMetadata metadata) => _camera.ResetView();
        private void OnChaseStarted(ChaseFact fact) => _camera.PlayDetectionBeat();
        private void OnProximity(ProximitySample sample) => _camera.SetProximity(sample.Closeness);
        private void OnDeath(EntityId id, Vector3 killer) => _camera.PlayDeathSnap(killer);
    }
}

