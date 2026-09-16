// ============================================================================
// ResultsDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores designer settings for the run-results screen.
//   The scene-owned Driver reads this asset without writing transient UI state into it.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Expose responsive panel dimensions, typography, spacing and vector palette.
//   - Keep shared asset values read-only at runtime.
//
// DEPENDENCIES:
//   - Unity ScriptableObject and value types only.
//
// USAGE NOTES:
//   - Lives in Resources/ScriptableObjects/Presentation/Results.
//   - The ResultsSetup tool creates missing assets while preserving existing tunings.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.Results
{
    [CreateAssetMenu(fileName = "ResultsDriverConfig", menuName = "Worsen/Results/Driver Config")]
    public sealed class ResultsDriverConfig : ScriptableObject
    {
        [SerializeField, Min(10)] private int _fontSize = 20;
        [SerializeField, Min(240f)] private float _panelWidth = 460f;
        [SerializeField, Min(0f)] private float _screenMargin = 24f;
        [SerializeField, Min(0f)] private float _panelPadding = 28f;
        [SerializeField, Min(0f)] private float _rowGap = 14f;
        [SerializeField, Min(10)] private int _titleFontSize = 32;
        [SerializeField, Min(10)] private int _captionFontSize = 12;
        [SerializeField, Min(1f)] private float _buttonHeight = 48f;
        [SerializeField, Min(0f)] private float _cornerCut = 12f;
        [SerializeField, Min(0.5f)] private float _strokeWidth = 1f;
        [SerializeField] private Color _panelColor = new Color(0.035f, 0.031f, 0.033f, 0.92f);
        [SerializeField] private Color _boneColor = new Color(0.86f, 0.82f, 0.72f, 1f);
        [SerializeField] private Color _mutedColor = new Color(0.57f, 0.54f, 0.49f, 1f);
        [SerializeField] private Color _crimsonColor = new Color(0.50f, 0.10f, 0.12f, 1f);
        [SerializeField] private Color _backdropColor = new Color(0.015f, 0.012f, 0.016f, 0.72f);
        public int FontSize => _fontSize;
        public float PanelWidth => _panelWidth;
        public float ScreenMargin => _screenMargin;
        public float PanelPadding => _panelPadding;
        public float RowGap => _rowGap;
        public int TitleFontSize => _titleFontSize;
        public int CaptionFontSize => _captionFontSize;
        public float ButtonHeight => _buttonHeight;
        public float CornerCut => _cornerCut;
        public float StrokeWidth => _strokeWidth;
        public Color PanelColor => _panelColor;
        public Color BoneColor => _boneColor;
        public Color MutedColor => _mutedColor;
        public Color CrimsonColor => _crimsonColor;
        public Color BackdropColor => _backdropColor;
    }
}
