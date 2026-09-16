// ============================================================================
// AudioOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed run facts into the Audio presentation service.
//   Explicit subscriptions keep scene lifetimes separate from persistent services.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Audio target.
// KEY RESPONSIBILITIES:
//   - Forward supplied values and pair every subscription with teardown.
// DEPENDENCIES:
//   - Core event payloads, Session Run and the target Presentation Manager.
// USAGE NOTES:
//   Setup wires references before activation. Handlers contain routing only.
//   Persistent on the canonical Audio root; retains only persistent Session.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;
using Worsen.Presentation.Audio;
namespace Worsen.Orchestrator
{
    public sealed class AudioOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private AudioManager _audio;
        private void OnEnable()
        {
            if (_run == null) return;
            _run = RunSessionManager.Instance ?? _run;
            if (_audio == null || _audio.Initialize() != _audio) return;
            _run.CaptureStarted += OnCapture;
            _run.ChaseStarted += OnChase;
            _run.ChaseEnded += OnChaseEnd;
            _run.ProximityPublished += OnProximity;
            _run.HealthChanged += OnHealth;
            _run.PlayerMovementPublished += OnMovement;
            _run.SpeedNormalizedPublished += OnSpeed;
            _run.PlayerDied += OnDeath;
            _run.PhaseChanged += OnPhase;
            _run.RoomPhaseChanged += OnRoom;
        }
        private void OnDisable()
        {
            if (_run == null) return;
            _run.CaptureStarted -= OnCapture;
            _run.ChaseStarted -= OnChase;
            _run.ChaseEnded -= OnChaseEnd;
            _run.ProximityPublished -= OnProximity;
            _run.HealthChanged -= OnHealth;
            _run.PlayerMovementPublished -= OnMovement;
            _run.SpeedNormalizedPublished -= OnSpeed;
            _run.PlayerDied -= OnDeath;
            _run.PhaseChanged -= OnPhase;
            _run.RoomPhaseChanged -= OnRoom;
        }
        private void OnCapture(RunCaptureMetadata metadata) => _audio.ResetRun();
        private void OnChase(ChaseFact fact) => _audio.PlayCue(CueId.Detection);
        private void OnChaseEnd(ChaseFact fact) { if (fact.EndReason == ChaseEndReason.Lost) _audio.PlayCue(CueId.Lose); }
        private void OnProximity(ProximitySample sample) => _audio.SetProximity(sample.Closeness);
        private void OnHealth(EntityId id, float health, float maximum) => _audio.SetInjury(health, maximum);
        private void OnMovement(PlayerMovementSample sample) => _audio.SetMovementState(sample.MovementState);
        private void OnSpeed(float speed) => _audio.SetSpeedNormalized(speed);
        private void OnDeath(EntityId id, Vector3 position) => _audio.PlayCue(CueId.Death);
        private void OnPhase(RunPhase phase) { if (phase == RunPhase.ExitOpen) _audio.PlayCue(CueId.ExitOpen); }
        private void OnRoom(RoomPhaseChangedFact fact) { if (fact.Phase == RoomPhase.Telegraph) _audio.PlayCue(CueId.RoomTelegraph); }
    }
}

