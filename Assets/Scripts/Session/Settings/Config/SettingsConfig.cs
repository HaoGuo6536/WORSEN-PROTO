// ============================================================================
// SettingsConfig.cs
// ============================================================================
// PURPOSE:
//   Supplies first-launch preferences when no supported settings file exists.
//   These are designer defaults, never a writable container for player choices.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Expose provisional input, camera comfort and linear volume defaults.
// DEPENDENCIES:
//   Core PlayerSettingsRecord and Unity ScriptableObject only.
// USAGE NOTES:
//   Author under Resources/ScriptableObjects/Session/Settings. Sensitivity is
//   degrees per mouse pixel; field of view is horizontal degrees, matching Camera.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Session.Settings
{
    [CreateAssetMenu(fileName = "SettingsConfig", menuName = "Worsen/Settings/Defaults")]
    public sealed class SettingsConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _mouseSensitivity = .1f;
        [SerializeField] private bool _invertY = false;
        [SerializeField, Range(1f, 179f)] private float _fieldOfView = 95f;
        [SerializeField] private bool _cameraTilt = true, _cameraPunch = true, _reacquireBlur = true;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f, _musicVolume = 1f, _effectsVolume = 1f;
        public PlayerSettingsRecord Defaults => new PlayerSettingsRecord(1, _mouseSensitivity, _invertY,
            _fieldOfView, _cameraTilt, _cameraPunch, _reacquireBlur, _masterVolume, _musicVolume, _effectsVolume);
    }
}
