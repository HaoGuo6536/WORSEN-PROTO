// ============================================================================
// HUDDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores designer settings for the in-run interface.
//   The scene-owned Driver reads this asset without writing transient UI state into it.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Expose restoration, display limits and the vector interface palette and geometry.
//   - Keep shared asset values read-only at runtime.
//   - Size the white three-dimensional compass independently of inventory slots.
//
// DEPENDENCIES:
//   - Unity ScriptableObject and value types only.
//
// USAGE NOTES:
//   - Lives in Resources/ScriptableObjects/Presentation/HUD.
//   - The HUDSetup tool creates missing assets while preserving existing tunings.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.HUD
{
    [CreateAssetMenu(fileName = "HUDDriverConfig", menuName = "Worsen/HUD/Driver Config")]
    public sealed class HUDDriverConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _restoreSeconds = 0.5f;
        [SerializeField, Min(10)] private int _fontSize = 18;
        [SerializeField, Min(18)] private int _smallFontSize = 21;
        [SerializeField, Min(1)] private int _maximumDisplayedSlots = 8;
        [SerializeField] private Color _panelColor = new Color(0.035f, 0.031f, 0.033f, 0.92f);
        [SerializeField] private Color _textColor = new Color(0.86f, 0.82f, 0.72f, 1f);
        [SerializeField] private Color _mutedColor = new Color(0.57f, 0.54f, 0.49f, 1f);
        [SerializeField] private Color _warningColor = new Color(0.50f, 0.10f, 0.12f, 1f);
        [SerializeField, Min(200f)] private float _panelWidth = 292f;
        [SerializeField, Min(0f)] private float _screenMargin = 24f;
        [SerializeField, Min(0f)] private float _cornerCut = 8f;
        [SerializeField, Min(1f)] private float _strokeWidth = 1.5f;
        [SerializeField, Min(20f)] private float _slotSize = 32f;
        [SerializeField, Min(0f)] private float _slotGap = 8f;
        [SerializeField, Min(48f)] private float _compassSize = 84f;
        public Color PanelColor => _panelColor;
        public Color TextColor => _textColor;
        public Color MutedColor => _mutedColor;
        public Color WarningColor => _warningColor;
        public float PanelWidth => _panelWidth;
        public float ScreenMargin => _screenMargin;
        public float CornerCut => _cornerCut;
        public float StrokeWidth => _strokeWidth;
        public float SlotSize => _slotSize;
        public float SlotGap => _slotGap;
        public float CompassSize => _compassSize >= 48f ? _compassSize : 84f;
        public float RestoreSeconds => _restoreSeconds;
        public int FontSize => _fontSize;
        public int SmallFontSize => _smallFontSize;
        public int MaximumDisplayedSlots => _maximumDisplayedSlots;
    }
}
