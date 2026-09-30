// ============================================================================
// HorrorOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects physical attack facts, authoritative flashlight state and retained curses to the
//   horror presentation system. This keeps enemy decisions and progression rules
//   out of the camera lights, fog, spatial sound and warning visuals.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Horror presentation target.
// KEY RESPONSIBILITIES:
//   - Forward committed attack samples and HorrorEffects' flashlight state; never consume UseItem.
//   - Reset transient cues when a generated floor replaces the previous one.
//   - Reset the run budget once per committed StartRun, including same-seed restarts.
// DEPENDENCIES:
//   - Session Run/Progression/HorrorEffects, Presentation Horror/Input/Camera and Core.
//   Authoritative aim is sampled before the Session tick; cosmetic shake never changes it.
// USAGE NOTES:
//   Scene-owned. Configure is called once canonical services are initialized.
//   All subscriptions pair OnEnable/OnDisable; no gameplay state is retained.
//   HorrorEffects consumes input through the Expedition tick. Without that service,
//   this route does not provide an independent flashlight toggle fallback.
//   Progression publishes StartRun transactions before generation (and before initial choices).
//   Generation requests reset only round cues; generation identities and seeds do not identify a run.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Input;
using Worsen.Presentation.Camera;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Run;
using Worsen.Session.Progression;
namespace Worsen.Orchestrator
{
    public sealed class HorrorOrchestrator : MonoBehaviour
    {
        private RunSessionManager _run;
        private ProgressionSessionManager _progression;
        private InputManager _input;
        private HorrorManager _horror;
        private HorrorEffectsManager _effects;
        private CameraManager _camera;
        public void Configure(RunSessionManager run, ProgressionSessionManager progression, InputManager input, HorrorManager horror, HorrorEffectsManager effects = null, CameraManager camera = null)
        { OnDisable(); _run = run; _progression = progression; _input = input; _horror = horror; _effects = effects; _camera = camera; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            if (_run == null || _progression == null || _input == null || _horror == null) return;
            _run.HunterAttackPublished += OnAttack;
            if (_effects != null) { _effects.FlashlightChanged += OnLight; _effects.AfterimageChanged += OnAfterimage; _run.PlayerMovementPublished += OnMovement; }
            _progression.GenerationRequested += OnGeneration;
            _progression.SnapshotChanged += OnSnapshot;
            _progression.TransactionCommitted += OnTransaction;
        }
        private void OnDisable()
        {
            if (_run != null) _run.HunterAttackPublished -= OnAttack;
            if (_run != null) _run.PlayerMovementPublished -= OnMovement;
            if (_effects != null) { _effects.FlashlightChanged -= OnLight; _effects.AfterimageChanged -= OnAfterimage; }
            if (_progression != null)
            { _progression.GenerationRequested -= OnGeneration; _progression.SnapshotChanged -= OnSnapshot; _progression.TransactionCommitted -= OnTransaction; }
        }
        private void OnLight(FlashlightSample sample) => _horror.SetFlashlight(sample);
        private void OnAfterimage(FlashlightSample sample, float seconds) => _horror.SetAfterimage(sample, seconds);
        private void OnMovement(PlayerMovementSample sample)
        {
            if (_camera != null) _effects.ObserveAim(new FlashlightSample(sample.Id, sample.Tick, true,
                _camera.AimPosition, _camera.AimRotation * Vector3.forward, 18f, 52f));
        }
        private void OnAttack(HunterAttackSample sample) => _horror.SetAttack(sample);
        private void OnTransaction(ProgressionSnapshot previous, ProgressionSnapshot current, string operation, string choiceId)
        {
            if (operation == nameof(ProgressionSessionManager.StartRun)) _horror.ResetRun(current.Seed);
        }
        private void OnGeneration(ProgressionGenerationRequest request)
        { _horror.ResetRound(); _horror.SetEffects(request.Effects.FogDensityMultiplier, request.Effects.FlashlightRangeMultiplier); }
        private void OnSnapshot(ProgressionSnapshot snapshot) => _horror.SetEffects(snapshot.Effects.FogDensityMultiplier, snapshot.Effects.FlashlightRangeMultiplier);
    }
}
