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
//   - Forward confirmed consumption before terminal presentation; preserve ordinary injury.
//   - Pair subscriptions and clear presentation through the existing capture reset.
// DEPENDENCIES:
//   - Session.Run and Presentation.PostFX; Core payloads only.
//   - CameraManager supplies a configured consumption duration only during Configure.
// USAGE NOTES:
//   Scene-owned; release subscriptions before the scene volume is destroyed. Setup provides serialized references before activation.
//   Initialize Camera before Configure to synchronize its configured duration.
//   Serialized duration preserves older setup paths. No runtime sequence state lives here.
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Camera;

namespace Worsen.Orchestrator
{
    public sealed class PostFXOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private PostFXManager _postFX;
        [SerializeField, Range(0.1f, 2f)] private float _consumptionSeconds = 0.9f;
        public void Configure(RunSessionManager run, PostFXManager postFX, CameraManager camera = null)
        {
            OnDisable(); _run = run; _postFX = postFX;
            if (camera != null) _consumptionSeconds = camera.ConsumptionSeconds;
            if (isActiveAndEnabled) OnEnable();
        }
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
            _run.CollapseHandPublished += OnCollapseHand;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.PlayerMovementPublished -= OnMovement;
            _run.CaptureStarted -= OnCaptureStarted;
            _run.ProximityPublished -= OnProximity;
            _run.HealthChanged -= OnHealth;
            _run.IntrusionPublished -= OnIntrusion;
            _run.CollapseHandPublished -= OnCollapseHand;
        }
        private void OnMovement(PlayerMovementSample sample) => _postFX.SetLookBack(sample.LookBack);
        private void OnCaptureStarted(RunCaptureMetadata metadata) => _postFX.ResetEffects();
        private void OnProximity(ProximitySample sample) => _postFX.SetProximity(sample.Closeness);
        private void OnHealth(EntityId id, float health, float maximum) => _postFX.SetInjury(health, maximum);
        private void OnIntrusion(IntrusionSample sample) => _postFX.PlayIntrusion(sample.DurationSeconds);
        private void OnCollapseHand(CollapseHandFact fact)
        {
            if (fact.Kind == CollapseHandEventKind.Consumed) _postFX.PlayConsumed(_consumptionSeconds);
        }
    }
}

