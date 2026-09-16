// ============================================================================
// HorrorOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Connects physical attack facts, flashlight input and retained curses to the
//   horror presentation system. This keeps enemy decisions and progression rules
//   out of the camera lights, fog, spatial sound and warning visuals.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Horror presentation target.
// KEY RESPONSIBILITIES:
//   - Forward committed attack samples and flashlight commands.
//   - Reset transient cues when a generated floor replaces the previous one.
// DEPENDENCIES:
//   - Session Run/Progression, Presentation Horror/Input and shared Core values.
// USAGE NOTES:
//   Scene-owned. Configure is called once canonical services are initialized.
//   All subscriptions pair OnEnable/OnDisable; no gameplay state is retained.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Input;
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
        public void Configure(RunSessionManager run, ProgressionSessionManager progression, InputManager input, HorrorManager horror)
        { OnDisable(); _run = run; _progression = progression; _input = input; _horror = horror; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            if (_run == null || _progression == null || _input == null || _horror == null) return;
            _run.HunterAttackPublished += OnAttack;
            _input.FramePublished += OnInput;
            _progression.GenerationRequested += OnGeneration;
            _progression.SnapshotChanged += OnSnapshot;
        }
        private void OnDisable()
        {
            if (_run != null) _run.HunterAttackPublished -= OnAttack;
            if (_input != null) _input.FramePublished -= OnInput;
            if (_progression != null)
            { _progression.GenerationRequested -= OnGeneration; _progression.SnapshotChanged -= OnSnapshot; }
        }
        private void OnAttack(HunterAttackSample sample) => _horror.SetAttack(sample);
        private void OnInput(InputFrame frame) { if ((frame.Pressed & InputButtons.UseItem) != 0) _horror.ToggleFlashlight(); }
        private void OnGeneration(ProgressionGenerationRequest request)
        { _horror.ResetRound(); _horror.SetEffects(request.Effects.FogDensityMultiplier, request.Effects.FlashlightRangeMultiplier); }
        private void OnSnapshot(ProgressionSnapshot snapshot) => _horror.SetEffects(snapshot.Effects.FogDensityMultiplier, snapshot.Effects.FlashlightRangeMultiplier);
    }
}
