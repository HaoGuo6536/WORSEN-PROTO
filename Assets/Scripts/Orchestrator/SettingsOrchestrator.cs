// ============================================================================
// SettingsOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Fans out one sanitized Settings snapshot to runtime presentation consumers.
//   This keeps preferences out of shared designer assets and leaves each target's
//   gameplay event router independent of the preference service.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Settings source.
// KEY RESPONSIBILITIES:
//   - Pair the single SettingsChanged subscription and forward Core overrides.
// DEPENDENCIES:
//   Session Settings; Presentation Input, Camera, PostFX and Audio; Core records.
// USAGE NOTES:
//   Scene-owned. Configure after all consumers initialize, then PublishCurrent.
//   One-source fan-out is explicitly delegated for PLAN-020; no runtime state.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Settings;
using Worsen.Presentation.Input;
using Worsen.Presentation.Camera;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Audio;
namespace Worsen.Orchestrator
{
    public sealed class SettingsOrchestrator : MonoBehaviour
    {
        private SettingsManager _settings;
        private InputManager _input;
        private CameraManager _camera;
        private PostFXManager _postFX;
        private AudioManager _audio;
        public void Configure(SettingsManager settings, InputManager input, CameraManager camera,
            PostFXManager postFX, AudioManager audio)
        {
            OnDisable(); _settings = settings; _input = input; _camera = camera; _postFX = postFX; _audio = audio;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            if (_settings == null) return;
            _settings.SettingsChanged -= OnSettings;
            _settings.SettingsChanged += OnSettings;
        }
        private void OnDisable() { if (_settings != null) _settings.SettingsChanged -= OnSettings; }
        private void OnSettings(PlayerSettingsRecord value)
        {
            _input.ApplySettings(value); _camera.ApplySettings(value);
            _postFX.SetReacquireBlurEnabled(value.ReacquireBlur); _audio.ApplySettings(value);
        }
    }
}
