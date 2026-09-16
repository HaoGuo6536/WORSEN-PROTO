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
//   - Expose bottom-right ribbon placement, vector palette and speed display precision.
//   - Keep presentation tuning separate from transient display state.
//
// DEPENDENCIES:
//   - No other project systems. Uses Unity value types and ScriptableObject.
//
// USAGE NOTES:
//   - Create the asset under Resources/ScriptableObjects/Presentation/DebugOverlay.
//   - Runtime code only reads this asset; it never stores telemetry here.
//   - PanelOffset now measures right/bottom inset. RibbonWidth controls the new surface;
//     the serialized PanelWidth field/getter is retained for existing callers and assets.
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
        [SerializeField, Min(200f)] private float _ribbonWidth = 760f;
        [SerializeField, Range(0.2f, 1f)] private float _maximumScreenFraction = 0.62f;
        [SerializeField, Min(0f)] private float _padding = 10f;
        [SerializeField, Min(0f)] private float _fieldGap = 12f;
        [SerializeField, Min(0f)] private float _cornerCut = 6f;
        [SerializeField, Min(0.5f)] private float _strokeWidth = 1f;
        [SerializeField] private Color _panelColor = new Color(0.035f, 0.031f, 0.033f, 0.92f);
        [SerializeField] private Color _boneColor = new Color(0.86f, 0.82f, 0.72f, 1f);
        [SerializeField] private Color _mutedColor = new Color(0.57f, 0.54f, 0.49f, 1f);
        [SerializeField] private Color _crimsonColor = new Color(0.50f, 0.10f, 0.12f, 1f);

        public Vector2 PanelOffset => _panelOffset;
        public float PanelWidth => _panelWidth;
        public int FontSize => _fontSize;
        public int SpeedDecimalPlaces => _speedDecimalPlaces;
        public float RibbonWidth => _ribbonWidth;
        public float MaximumScreenFraction => _maximumScreenFraction;
        public float Padding => _padding;
        public float FieldGap => _fieldGap;
        public float CornerCut => _cornerCut;
        public float StrokeWidth => _strokeWidth;
        public Color PanelColor => _panelColor;
        public Color BoneColor => _boneColor;
        public Color MutedColor => _mutedColor;
        public Color CrimsonColor => _crimsonColor;
    }
}
