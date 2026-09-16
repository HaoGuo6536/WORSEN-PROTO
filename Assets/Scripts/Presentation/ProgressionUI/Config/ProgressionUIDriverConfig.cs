// ============================================================================
// ProgressionUIDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores the visual palette and layout for progression choices and the shop.
//   The interface reads these settings without mutating shared assets; phase,
//   prices and effects are supplied separately in Core snapshots.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Expose colors, font sizes and responsive card/panel geometry.
//
// DEPENDENCIES:
//   Unity ScriptableObject and value types only.
//
// USAGE NOTES:
//   Create under Resources/ScriptableObjects/Presentation/ProgressionUI.
//   Shared by ProgressionUIDriver and its owned drawing sub-drivers.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.ProgressionUI
{
    [CreateAssetMenu(fileName = "ProgressionUIDriverConfig", menuName = "Worsen/Progression UI/Driver Config")]
    public sealed class ProgressionUIDriverConfig : ScriptableObject
    {
        [SerializeField] private Color _panelColor = new Color(0.035f, 0.031f, 0.033f, 0.96f);
        [SerializeField] private Color _scrimColor = new Color(0.015f, 0.012f, 0.014f, 0.83f);
        [SerializeField] private Color _textColor = new Color(0.86f, 0.82f, 0.72f, 1f);
        [SerializeField] private Color _mutedColor = new Color(0.57f, 0.54f, 0.49f, 1f);
        [SerializeField] private Color _warningColor = new Color(0.50f, 0.10f, 0.12f, 1f);
        [SerializeField, Min(14)] private int _fontSize = 24;
        [SerializeField, Min(18)] private int _smallFontSize = 20;
        [SerializeField, Min(600f)] private float _panelWidth = 1120f;
        [SerializeField, Min(220f)] private float _cardWidth = 320f;
        [SerializeField, Min(0f)] private float _spacing = 24f;
        [SerializeField, Min(0f)] private float _cornerCut = 10f;
        [SerializeField, Min(1f)] private float _strokeWidth = 1.5f;
        public Color PanelColor => _panelColor;
        public Color ScrimColor => _scrimColor;
        public Color TextColor => _textColor;
        public Color MutedColor => _mutedColor;
        public Color WarningColor => _warningColor;
        public int FontSize => _fontSize;
        public int SmallFontSize => _smallFontSize;
        public float PanelWidth => _panelWidth;
        public float CardWidth => _cardWidth;
        public float Spacing => _spacing;
        public float CornerCut => _cornerCut;
        public float StrokeWidth => _strokeWidth;
    }
}
