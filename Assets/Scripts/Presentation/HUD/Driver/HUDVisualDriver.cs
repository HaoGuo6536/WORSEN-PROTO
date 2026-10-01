// ============================================================================
// HUDVisualDriver.cs
// ============================================================================
//
// PURPOSE:
//   Draws a compact survival HUD using scalable vector paths and ordinary labels.
//   The scene's HUDDriver owns this surface and supplies prepared state; this
//   component never queries gameplay or changes the owner's display facts.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HUDDriver · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Reuse one remaining number above its white/golden arrow, honoring floor hiding.
//   - Render three labeled physical slots and a separate flashlight inside chase-hidden chrome.
//   - Keep health readable during chases and modal screens, without shield text.
//   - Pair vector callbacks across document binding, replacement and teardown.
//   - Apply safe-area layout and modal suppression to the single guidance arrow.
//
// DEPENDENCIES:
//   Own HUDDriverConfig/State and geometry, guidance and inventory Presenters only.
//
// USAGE NOTES:
//   Scene-owned through HUDDriver, sharing its HUDDriverConfig (§7d).
//   No global side effects, no UXML dependency and no texture assets.
//   Painter callbacks read visual bounds only; repaint requests occur outside them.
//
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;


namespace Worsen.Presentation.HUD
{
    public sealed class HUDVisualDriver : MonoBehaviour
    {
        private HUDDriverConfig _config;
        private HUDDriverState _state;
        private readonly HUDGeometryPresenter _geometry = new HUDGeometryPresenter();
        private readonly HUDGuidancePresenter _guidance = new HUDGuidancePresenter();
        private readonly HUDInventoryPresenter _inventory = new HUDInventoryPresenter();
        private readonly HUDLayoutPresenter _layout = new HUDLayoutPresenter();
        private readonly Label[] _itemLabels = new Label[HUDInventoryPresenter.ItemSlotCount];
        private VisualElement _flashlight, _charge, _aim;
        private Label _flashlightTitle, _flashlightStatus;
        private VisualElement _root, _panel, _extra, _directionGroup, _arrow, _slots;
        private VisualElement _inventoryPanel, _health, _healthFill;
        private Label _healthText;
        private Label _count, _overflow, _selected;



        public void Bind(VisualElement root, HUDDriverConfig config)
        {
            Unbind();
            _root = root;
            _config = config;
            root.Clear();
            root.styleSheets.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.display = DisplayStyle.Flex;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.color = config.TextColor;
            root.style.fontSize = config.FontSize;

            _panel = Element("hud", root);
            _panel.style.position = Position.Absolute;
            _panel.style.left = _panel.style.right = 0;
            _panel.style.bottom = _guidance.CounterBottom(config.CompassSize, config.CounterArrowGap);
            _count = Text("cake-count", "", _panel);
            _count.style.fontSize = config.CountFontSize;
            _count.style.unityTextAlign = TextAnchor.MiddleCenter;


            _extra = Element("hud-extra", root);
            _extra.style.position = Position.Absolute;
            _extra.style.left = _extra.style.right = _extra.style.top = _extra.style.bottom = 0;
            _directionGroup = Element("direction-group", root);
            _directionGroup.style.position = Position.Absolute;

            _directionGroup.style.alignItems = Align.Center;
            _directionGroup.Add(_panel);
            _arrow = Element("direction-cue", _directionGroup);
            _arrow.style.width = _arrow.style.height = config.CompassSize;
            _arrow.generateVisualContent += PaintArrow;
            var inventory = _inventoryPanel = Element("inventory-panel", _extra);
            inventory.style.position = Position.Absolute;

            _slots = Element("item-slots", inventory);
            _slots.style.height = config.InventorySlotHeight;
            for (int i = 0; i < _itemLabels.Length; i++)
            {
                _itemLabels[i] = Text("item-slot-" + (i + 1), "", _slots);
                _itemLabels[i].style.position = Position.Absolute;
                _itemLabels[i].style.backgroundColor = config.PanelColor;
                _itemLabels[i].style.unityTextAlign = TextAnchor.MiddleCenter;
                _itemLabels[i].style.fontSize = config.FontSize;
                _itemLabels[i].style.overflow = Overflow.Hidden;
                _itemLabels[i].style.textOverflow = TextOverflow.Ellipsis;
            }
            _flashlight = Element("flashlight-slot", inventory);
            _flashlight.style.position = Position.Absolute;
            _flashlight.style.left = _inventory.FlashlightLeft(config.InventorySlotWidth, config.InventorySlotGap, config.FlashlightSlotGap);
            _flashlight.style.top = 0;
            _flashlight.style.width = config.FlashlightSlotWidth;
            _flashlight.style.height = config.InventorySlotHeight;
            _flashlight.style.backgroundColor = config.PanelColor;
            _flashlightTitle = Text("flashlight-state", "", _flashlight);
            _flashlightStatus = Text("flashlight-status", "", _flashlight);
            _flashlightTitle.style.fontSize = _flashlightStatus.style.fontSize = config.FontSize;
            _flashlightTitle.style.unityTextAlign = _flashlightStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
            _charge = Element("flashlight-charge", _flashlight);
            _aim = Element("flashlight-aim", _flashlight);
            _charge.style.height = _aim.style.height = config.StrokeWidth * 3f;
            _charge.style.backgroundColor = config.FlashlightColor;
            _aim.style.backgroundColor = config.SelectionColor;
            _overflow = Text("slot-overflow", "", inventory);
            _overflow.style.color = config.MutedColor;
            _overflow.style.fontSize = config.SmallFontSize;
            _selected = Text("selected-consumable", "", _extra);
            _selected.style.position = Position.Absolute;
            _selected.style.fontSize = config.SmallFontSize;
            _selected.style.overflow = Overflow.Hidden;
            _selected.style.textOverflow = TextOverflow.Ellipsis;
            _health = Element("health", root);
            _health.style.position = Position.Absolute;
            _healthText = Text("health-text", "— / —", _health);
            _healthText.style.fontSize = config.SmallFontSize;
            var track = Element("health-track", _health);
            track.style.height = config.HealthBarHeight;
            track.style.backgroundColor = config.PanelColor;
            _healthFill = Element("health-fill", track);
            _healthFill.style.height = Length.Percent(100);
            _healthFill.style.backgroundColor = config.HealthColor;
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ApplyLayout();
        }

        public void Apply(HUDDriverState state)
        {
            if (_root == null || state == null) return;
            _state = state;
            _count.text = _guidance.CountText(state);
            _count.style.color = _guidance.ArrowTint(state, _config.GoldenSenseColor, _config.ExitSenseColor);
            _panel.style.display = _guidance.CountVisible(state) ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.style.opacity = state.ExtraOpacity;
            _extra.style.display = state.ChromeVisible && !state.ModalOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _extra.style.opacity = state.ExtraOpacity;
            _directionGroup.style.display = _guidance.ArrowVisible(state) ? DisplayStyle.Flex : DisplayStyle.None;
            _healthText.text = state.HealthText;
            _healthFill.style.width = Length.Percent(state.HealthFraction * 100f);
            _overflow.text = state.SlotOverflowText;
            _overflow.style.display = string.IsNullOrEmpty(state.SlotOverflowText) ? DisplayStyle.None : DisplayStyle.Flex;
            _selected.text = state.SelectedSlotText;
            _selected.style.display = string.IsNullOrEmpty(state.SelectedSlotText) ? DisplayStyle.None : DisplayStyle.Flex;
            ApplyInventory(state);
            _arrow.style.rotate = new Rotate(new Angle(_guidance.ActiveDegrees(state), AngleUnit.Degree));
            _arrow.MarkDirtyRepaint();
        }

        public void Unbind()
        {
            if (_arrow != null) _arrow.generateVisualContent -= PaintArrow;
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _inventoryPanel = _health = _healthFill = null;
            _healthText = null;
            System.Array.Clear(_itemLabels, 0, _itemLabels.Length);
            _flashlight = _charge = _aim = null;
            _flashlightTitle = _flashlightStatus = null;
            if (_root != null) { _root.style.display = DisplayStyle.None; _root.Clear(); }
            _root = _panel = _extra = _directionGroup = _arrow = _slots = null;
            _count = _overflow = _selected = null;
            _state = null;
            _config = null;
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        private void PaintArrow(MeshGenerationContext context)
        {
            if (_state == null || !_guidance.ArrowVisible(_state)) return;
            var painter = context.painter2D;
            painter.fillColor = _guidance.ArrowTint(_state, _config.GoldenSenseColor, _config.ExitSenseColor);
            Path(painter, _geometry.Arrow(_arrow.contentRect));
            painter.Fill();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplyLayout();

        private void ApplyLayout()
        {
            float width = _root.contentRect.width, height = _root.contentRect.height;
            if (!(width > 0f) || !(height > 0f)) return;
            Rect safe = _layout.SafeRect(width, height, _config.SafeInset);
            float rowWidth = _inventory.FlashlightLeft(_config.InventorySlotWidth, _config.InventorySlotGap, _config.FlashlightSlotGap) + _config.FlashlightSlotWidth;
            Place(_directionGroup, _layout.Arrow(safe, _config.CompassSize));
            Place(_inventoryPanel, _layout.Inventory(safe, rowWidth, _config.InventorySlotHeight,
                _config.CompassSize, _guidance.CounterBottom(_config.CompassSize, _config.CounterArrowGap), _config.CountFontSize, _config.InventorySlotGap));
            Place(_selected, _layout.Caption(safe, rowWidth, _config.FontSize * 1.5f, _config.CompassSize));
            Place(_health, _layout.Health(safe, _config.HealthWidth, _config.FontSize * 1.5f + _config.HealthBarHeight));
        }

        private static void Place(VisualElement element, Rect rect)
        {
            element.style.left = rect.x; element.style.top = rect.y;
            element.style.width = rect.width; element.style.height = rect.height;
        }

        private void ApplyInventory(HUDDriverState state)
        {
            _slots.style.display = DisplayStyle.Flex;
            _slots.style.width = _inventory.FlashlightLeft(_config.InventorySlotWidth, _config.InventorySlotGap, _config.FlashlightSlotGap)
                + _config.FlashlightSlotWidth;
            for (int i = 0; i < _itemLabels.Length; i++)
            {
                bool selected = i == state.SelectedDisplaySlot;
                Rect rect = _inventory.SlotRect(i, selected, _config.InventorySlotWidth, _config.InventorySlotHeight,
                    _config.InventorySlotGap, _config.SelectedSlotScale);
                var label = _itemLabels[i];
                label.text = (i + 1) + "\n" + state.SlotLabels[i];
                label.style.left = rect.x; label.style.top = rect.y;
                label.style.width = rect.width; label.style.height = rect.height;
                label.style.opacity = _inventory.SlotOpacity(selected, _config.UnselectedSlotOpacity);
                label.style.color = selected ? _config.SelectionColor : _config.TextColor;
                Border(label, selected ? _config.SelectionColor : _config.MutedColor, _config.StrokeWidth * (selected ? 2f : 1f));
            }
            _flashlightTitle.text = state.FlashlightText;
            _flashlightStatus.text = state.FlashlightStatusText;
            _flashlightTitle.style.color = _config.FlashlightColor;
            _charge.style.width = Length.Percent(state.FlashlightCharge * 100f);
            _aim.style.width = Length.Percent(state.FlashlightAim * 100f);
            Border(_flashlight, _config.FlashlightColor, _config.StrokeWidth * (1f + _inventory.ReadyPulse(state)));
        }

        private static void Border(VisualElement element, Color color, float width)
        {
            element.style.borderLeftColor = element.style.borderRightColor = element.style.borderTopColor = element.style.borderBottomColor = color;
            element.style.borderLeftWidth = element.style.borderRightWidth = element.style.borderTopWidth = element.style.borderBottomWidth = width;
        }

        private static void Path(Painter2D painter, Vector2[] vertices)
        {
            painter.BeginPath();
            painter.MoveTo(vertices[0]);
            for (int index = 1; index < vertices.Length; index++) painter.LineTo(vertices[index]);
            painter.ClosePath();
        }

        private static VisualElement Element(string name, VisualElement parent)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            parent.Add(element);
            return element;
        }

        private static Label Text(string name, string value, VisualElement parent)
        {
            var label = new Label(value) { enableRichText = false, name = name, pickingMode = PickingMode.Ignore };
            parent.Add(label);
            return label;
        }
    }
}
