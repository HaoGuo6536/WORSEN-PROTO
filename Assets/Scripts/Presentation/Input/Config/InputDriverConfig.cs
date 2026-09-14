// ============================================================================
// InputDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores input feel settings independently from device capture and gameplay.
//   Designers can tune mouse sensitivity and gamepad turn rate in the Inspector
//   while runtime code reads the asset through getters only.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Input.
//
// KEY RESPONSIBILITIES:
//   - Provide mouse degrees per pixel, gamepad degrees per second, and Y inversion.
//
// DEPENDENCIES:
//   - UnityEngine.ScriptableObject and Inspector attributes only.
//
// USAGE NOTES:
//   - Asset: Resources/ScriptableObjects/Presentation/Input/InputDriverConfig.asset.
//   - PlayerInputDriver first uses its serialized reference, then Resources.
//   - M0 bindings are fixed in PlayerInputDriver; recording and rebinding are later work.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.Input
{
    [CreateAssetMenu(fileName = "InputDriverConfig", menuName = "Worsen/Input/Input Driver Config")]
    public sealed class InputDriverConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _mouseDegreesPerPixel = 0.1f;
        [SerializeField, Min(0f)] private float _gamepadDegreesPerSecond = 180f;
        [SerializeField] private bool _invertLookY;

        public float MouseDegreesPerPixel => _mouseDegreesPerPixel;
        public float GamepadDegreesPerSecond => _gamepadDegreesPerSecond;
        public bool InvertLookY => _invertLookY;
    }
}

