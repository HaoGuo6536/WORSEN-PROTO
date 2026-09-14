// ============================================================================
// DebugOverlayDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores the appearance settings for the development overlay in one designer
//   asset. Keeping these values outside the Driver lets the display move or
//   change size without changing the code that receives simulation samples.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · DebugOverlay.
//   Read-only designer data owned by DebugOverlayManager and given to its Driver.
//
// KEY RESPONSIBILITIES:
//   - Expose panel position, width, text size, and speed display precision.
//   - Keep presentation tuning separate from transient display state.
//
// DEPENDENCIES:
//   - No other project systems. Uses Unity value types and ScriptableObject.
//
// USAGE NOTES:
//   - Create the asset under Resources/ScriptableObjects/Presentation/DebugOverlay.
//   - Runtime code only reads this asset; it never stores telemetry here.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.DebugOverlay
{
    [CreateAssetMenu(fileName = "DebugOverlayDriverConfig", menuName = "Worsen/DebugOverlay/Driver Config")]
    public sealed class DebugOverlayDriverConfig : ScriptableObject
    {
        [SerializeField] private Vector2 _panelOffset = new Vector2(18f, 18f);
        [SerializeField, Min(200f)] private float _panelWidth = 320f;
        [SerializeField, Min(10)] private int _fontSize = 14;
        [SerializeField, Range(0, 3)] private int _speedDecimalPlaces = 2;

        public Vector2 PanelOffset => _panelOffset;
        public float PanelWidth => _panelWidth;
        public int FontSize => _fontSize;
        public int SpeedDecimalPlaces => _speedDecimalPlaces;
    }
}
