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
//   - Keep numerical shield/health information off the in-run surface.
//   - Pair vector callbacks across document binding, replacement and teardown.
//   - Apply chrome visibility and restoration without suppressing guidance.
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
using System.Collections.Generic;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDVisualDriver : MonoBehaviour
    {
        private HUDDriverConfig _config;
        private HUDDriverState _state;
        private readonly HUDGeometryPresenter _geometry = new HUDGeometryPresenter();
        private readonly HUDGuidancePresenter _guidance = new HUDGuidancePresenter();
        private readonly HUDInventoryPresenter _inventory = new HUDInventoryPresenter();
        private readonly Label[] _itemLabels = new Label[HUDInventoryPresenter.ItemSlotCount];
        private VisualElement _flashlight, _charge, _aim;
        private Label _flashlightTitle, _flashlightStatus;
        private VisualElement _root, _panel, _extra, _directionGroup, _arrow, _slots;
        private VisualElement _goldenDirectionGroup, _goldenArrow;
        private VisualElement _exitDirectionGroup, _exitArrow;
        private Label _count, _overflow, _selected;

        private VisualElement _threatGroup;
        private readonly Dictionary<EntityId, Label> _threatArrows = new Dictionary<EntityId, Label>();

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
            _count.style.fontSize = config.FontSize;
            _count.style.unityTextAlign = TextAnchor.MiddleCenter;

            _threatGroup = Element("threat-directions", root);
            _threatGroup.style.position = Position.Absolute;
            _threatGroup.style.left = Length.Percent(50);
            _threatGroup.style.bottom = config.ScreenMargin * 3 + config.CompassSize;
            _threatGroup.style.flexDirection = FlexDirection.Row;


            _extra = Element("hud-extra", root);
            _extra.style.position = Position.Absolute;
            _extra.style.left = _extra.style.right = _extra.style.top = _extra.style.bottom = 0;
            _directionGroup = Element("direction-group", root);
            _directionGroup.style.position = Position.Absolute;
            _directionGroup.style.left = Length.Percent(50);
            _directionGroup.style.marginLeft = -config.PanelWidth * 0.25f;
            _directionGroup.style.bottom = config.ScreenMargin * 3;
            _directionGroup.style.width = config.PanelWidth * 0.5f;
            _directionGroup.style.alignItems = Align.Center;
            _directionGroup.Add(_panel);
            _arrow = Element("direction-cue", _directionGroup);
            _arrow.style.width = _arrow.style.height = config.CompassSize;
            _arrow.generateVisualContent += PaintArrow;
            _goldenDirectionGroup = Element("golden-direction-group", root);
            _goldenDirectionGroup.style.position = Position.Absolute;
            _goldenDirectionGroup.style.left = Length.Percent(50);
            _goldenDirectionGroup.style.marginLeft = config.CompassSize;
            _goldenDirectionGroup.style.bottom = config.ScreenMargin * 3;
            _goldenDirectionGroup.style.width = config.CompassSize;
            _goldenArrow = Element("golden-direction-cue", _goldenDirectionGroup);
            _goldenArrow.style.width = _goldenArrow.style.height = config.CompassSize;
            _goldenArrow.generateVisualContent += PaintGoldenArrow;
            _exitDirectionGroup = Element("exit-sense-group", root);
            _exitDirectionGroup.style.position = Position.Absolute;
            _exitDirectionGroup.style.left = Length.Percent(50);
            _exitDirectionGroup.style.marginLeft = -config.CompassSize * 2f;
            _exitDirectionGroup.style.bottom = config.ScreenMargin * 3;
            _exitArrow = Element("exit-sense-cue", _exitDirectionGroup);
            _exitArrow.style.width = _exitArrow.style.height = config.CompassSize;
            _exitArrow.generateVisualContent += PaintExitArrow;

            var inventory = Element("inventory-panel", _extra);
            inventory.style.position = Position.Absolute;
            inventory.style.left = config.ScreenMargin;
            inventory.style.bottom = config.ScreenMargin * 3 + config.CompassSize;
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
            _selected.style.left = config.ScreenMargin;
            _selected.style.bottom = config.ScreenMargin + config.CompassSize;
            _selected.style.fontSize = config.SmallFontSize;

        }

        public void Apply(HUDDriverState state)
        {
            if (_root == null || state == null) return;
            _state = state;
            _count.text = _guidance.CountText(state);
            _count.style.color = _guidance.CountTint(state, _config.GoldenSenseColor);
            var countParent = _guidance.UsesGoldenCount(state) ? _goldenDirectionGroup : _directionGroup;
            if (_panel.parent != countParent) countParent.Add(_panel);

            foreach (var id in new List<EntityId>(_threatArrows.Keys))
                if (!state.Threats.ContainsKey(id)) { _threatArrows[id].RemoveFromHierarchy(); _threatArrows.Remove(id); }
            foreach (var pair in state.Threats)
            {
                if (!_threatArrows.TryGetValue(pair.Key, out var arrow))
                {
                    arrow = Text("threat-" + pair.Key.Value, "▲", _threatGroup);
                    arrow.style.fontSize = _config.CompassSize * .5f;
                    arrow.style.width = arrow.style.height = _config.CompassSize;
                    arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                    arrow.style.color = _config.ThreatArrowColor;
                    _threatArrows.Add(pair.Key, arrow);
                }
                arrow.style.display = pair.Value.Visible ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.style.rotate = new Rotate(new Angle(pair.Value.ArrowDegrees, AngleUnit.Degree));
            }
            _panel.style.display = _guidance.CountVisible(state) ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.style.opacity = state.ExtraOpacity;
            _extra.style.display = state.ChromeVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _extra.style.opacity = state.ExtraOpacity;
            _directionGroup.style.display = state.DirectionVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _goldenDirectionGroup.style.display = state.GoldenSenseVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _goldenArrow.style.rotate = new Rotate(new Angle(state.DisplayGoldenArrowDegrees, AngleUnit.Degree));
            _goldenArrow.MarkDirtyRepaint();
            _exitDirectionGroup.style.display = state.ExitSenseVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _exitArrow.style.rotate = new Rotate(new Angle(state.ExitSenseArrowDegrees, AngleUnit.Degree));
            _exitArrow.MarkDirtyRepaint();
            _overflow.text = state.SlotOverflowText;
            _overflow.style.display = string.IsNullOrEmpty(state.SlotOverflowText) ? DisplayStyle.None : DisplayStyle.Flex;
            _selected.text = state.SelectedSlotText;
            _selected.style.display = string.IsNullOrEmpty(state.SelectedSlotText) ? DisplayStyle.None : DisplayStyle.Flex;
            ApplyInventory(state);
            _arrow.style.rotate = new Rotate(new Angle(state.DisplayArrowDegrees, AngleUnit.Degree));
            _arrow.MarkDirtyRepaint();
        }

        public void Unbind()
        {
            _threatArrows.Clear(); _threatGroup = null;

            if (_arrow != null) _arrow.generateVisualContent -= PaintArrow;
            if (_goldenArrow != null) _goldenArrow.generateVisualContent -= PaintGoldenArrow;
            _goldenArrow = _goldenDirectionGroup = null;
            if (_exitArrow != null) _exitArrow.generateVisualContent -= PaintExitArrow;
            _exitArrow = _exitDirectionGroup = null;
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
            if (_state == null || !_state.DirectionVisible) return;
            var painter = context.painter2D;
            painter.fillColor = Color.white;
            Path(painter, _geometry.Arrow(_arrow.contentRect));
            painter.Fill();
        }

        private void PaintGoldenArrow(MeshGenerationContext context)
        {
            if (_state == null || !_state.GoldenSenseVisible) return;
            var painter = context.painter2D;
            painter.fillColor = _config.GoldenSenseColor;
            Path(painter, _geometry.Arrow(_goldenArrow.contentRect)); painter.Fill();
        }

        private void PaintExitArrow(MeshGenerationContext context)
        {
            if (_state == null || !_state.ExitSenseVisible) return;
            var painter = context.painter2D;
            painter.fillColor = _config.ExitSenseColor;
            Path(painter, _geometry.Arrow(_exitArrow.contentRect)); painter.Fill();
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
                label.text = (i + 1) + (selected ? "  SELECTED\n" : "\n") + state.SlotLabels[i];
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
