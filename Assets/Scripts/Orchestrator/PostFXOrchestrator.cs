// ============================================================================
// PostFXOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed Session facts into the PostFX presentation service.
//   It keeps presentation independent of Domain types and survives explicit
//   scene assembly without relying on component startup order.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · PostFX target.
// KEY RESPONSIBILITIES:
//   - Forward Core payloads and pair event subscriptions with component lifetime.
// DEPENDENCIES:
//   - Session.Run and Presentation.PostFX; Core payloads only.
// USAGE NOTES:
//   Scene-owned; release subscriptions before the scene volume is destroyed. Setup provides serialized references before activation.
//   Handlers contain no remembered gameplay state or engine work.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;
using Worsen.Presentation.PostFX;

namespace Worsen.Orchestrator
{
    public sealed class PostFXOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private PostFXManager _postFX;
        private void OnEnable()
        {
            if (_run == null || _postFX == null) return;
            _run = RunSessionManager.Instance ?? _run;
            _postFX.Initialize();
            _run.PlayerMovementPublished += OnMovement;
            _run.CaptureStarted += OnCaptureStarted;
            _run.ProximityPublished += OnProximity;
            _run.HealthChanged += OnHealth;
            _run.IntrusionPublished += OnIntrusion;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.PlayerMovementPublished -= OnMovement;
            _run.CaptureStarted -= OnCaptureStarted;
            _run.ProximityPublished -= OnProximity;
            _run.HealthChanged -= OnHealth;
            _run.IntrusionPublished -= OnIntrusion;
        }
        private void OnMovement(PlayerMovementSample sample) => _postFX.SetLookBack(sample.LookBack);
        private void OnCaptureStarted(RunCaptureMetadata metadata) => _postFX.ResetEffects();
        private void OnProximity(ProximitySample sample) => _postFX.SetProximity(sample.Closeness);
        private void OnHealth(EntityId id, float health, float maximum) => _postFX.SetInjury(health, maximum);
        private void OnIntrusion(IntrusionSample sample) => _postFX.PlayIntrusion(sample.DurationSeconds);
    }
}

