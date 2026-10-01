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
//   - Tint the single guidance arrow for normal, Golden Sense and Exit Sense targets.
//   - Expose restoration, inventory emphasis and the vector interface palette and geometry.
//   - Tune persistent health and safe-area placement without gameplay dependencies.
//   - Keep shared asset values read-only at runtime.
//   - Tune flat-arrow turning and number clearance independently of inventory slots.
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
        [SerializeField, Min(10)] private int _smallFontSize = 16;
        [SerializeField, Min(19)] private int _countFontSize = 26;
        [SerializeField, Range(0f, .15f)] private float _safeInset = .05f;
        [SerializeField, Min(80f)] private float _healthWidth = 180f;
        [SerializeField, Min(4f)] private float _healthBarHeight = 8f;
        [SerializeField] private Color _healthColor = new Color(.65f, .82f, .60f, 1f);
        public int CountFontSize => System.Math.Max(FontSize + 1, _countFontSize);
        public float SafeInset => _safeInset;
        public float HealthWidth => _healthWidth;
        public float HealthBarHeight => _healthBarHeight;
        public Color HealthColor => _healthColor;
        [SerializeField, Min(64f)] private float _inventorySlotWidth = 112f;
        [SerializeField, Min(48f)] private float _inventorySlotHeight = 72f;
        [SerializeField, Min(0f)] private float _inventorySlotGap = 14f;
        [SerializeField, Range(1f, 1.2f)] private float _selectedSlotScale = 1.08f;
        [SerializeField, Range(0f, 1f)] private float _unselectedSlotOpacity = 0.55f;
        [SerializeField, Min(0f)] private float _flashlightSlotGap = 32f;
        [SerializeField, Min(112f)] private float _flashlightSlotWidth = 168f;
        [SerializeField, Min(0.1f)] private float _readyPulseSeconds = 1.2f;
        [SerializeField] private Color _selectionColor = new Color(1f, 0.9f, 0.55f, 1f);
        [SerializeField] private Color _flashlightColor = new Color(0.35f, 0.9f, 1f, 1f);
        public float InventorySlotWidth => _inventorySlotWidth;
        public float InventorySlotHeight => _inventorySlotHeight;
        public float InventorySlotGap => _inventorySlotGap;
        public float SelectedSlotScale => _selectedSlotScale;
        public float UnselectedSlotOpacity => _unselectedSlotOpacity;
        public float FlashlightSlotGap => _flashlightSlotGap;
        public float FlashlightSlotWidth => _flashlightSlotWidth;
        public float ReadyPulseSeconds => _readyPulseSeconds;
        public Color SelectionColor => _selectionColor;
        public Color FlashlightColor => _flashlightColor;
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
        [SerializeField, Min(1f)] private float _arrowTurnDegreesPerSecond = 360f;
        [SerializeField, Min(0f)] private float _counterArrowGap = 4f;
        public float ArrowTurnDegreesPerSecond => _arrowTurnDegreesPerSecond;
        public float CounterArrowGap => _counterArrowGap;
        [SerializeField] private Color _goldenSenseColor = new Color(1f, 0.75f, 0.15f, 1f);
        [SerializeField] private Color _exitSenseColor = new Color(0.3f, 0.85f, 1f, 1f);
        public Color ExitSenseColor => _exitSenseColor;
        [SerializeField] private Color _threatArrowColor = new Color(0.9f, 0.3f, 0.25f, 1f);
        public Color ThreatArrowColor => _threatArrowColor;
        public Color GoldenSenseColor => _goldenSenseColor;
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
        public int SmallFontSize => System.Math.Min(_smallFontSize, FontSize);
        public int MaximumDisplayedSlots => 3; // Owner contract, not designer capacity.
    }
}
