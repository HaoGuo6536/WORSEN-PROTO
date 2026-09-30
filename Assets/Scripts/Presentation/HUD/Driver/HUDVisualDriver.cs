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
//   - Build quiet cake and golden counts without panel chrome, title or controls hints.
//   - Pair all vector callbacks when binding, unbinding or replacing a document.
//   - Paint one flat white arrow without a ring, shaded facets or caption.
//   - Apply chrome visibility and restoration without suppressing the independent compass.
//
// DEPENDENCIES:
//   Own HUDDriverConfig, HUDDriverState and HUDGeometryPresenter only.
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
        private VisualElement _root, _panel, _extra, _directionGroup, _arrow, _slots;
        private Label _count, _golden, _overflow;

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
            _panel.style.right = config.ScreenMargin;
            _panel.style.top = config.ScreenMargin;
            _panel.style.width = config.PanelWidth;
            _panel.style.maxWidth = Length.Percent(42);
            _count = Text("cake-count", "Cakes: —", _panel);
            _count.style.fontSize = config.FontSize;
            _golden = Text("golden-count", "Golden: —", _panel);
            _golden.style.fontSize = config.FontSize;
            _golden.style.color = config.MutedColor;


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
            _arrow = Element("direction-cue", _directionGroup);
            _arrow.style.width = _arrow.style.height = config.CompassSize;
            _arrow.generateVisualContent += PaintArrow;

            var inventory = Element("inventory-panel", _extra);
            inventory.style.position = Position.Absolute;
            inventory.style.left = config.ScreenMargin;
            inventory.style.bottom = config.ScreenMargin * 3;
            _slots = Element("item-slots", inventory);
            _slots.style.height = config.SlotSize;
            _slots.generateVisualContent += PaintSlots;
            _overflow = Text("slot-overflow", "", inventory);
            _overflow.style.color = config.MutedColor;
            _overflow.style.fontSize = config.SmallFontSize;

        }

        public void Apply(HUDDriverState state)
        {
            if (_root == null || state == null) return;
            _state = state;
            _count.text = state.CountText;
            _golden.text = state.GoldenText;
            _panel.style.display = state.ChromeVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.style.opacity = state.ExtraOpacity;
            _extra.style.display = state.ChromeVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _extra.style.opacity = state.ExtraOpacity;
            _directionGroup.style.display = state.DirectionVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _overflow.text = state.SlotOverflowText;
            _slots.style.width = state.DisplayedSlots * (_config.SlotSize + _config.SlotGap);
            _slots.style.display = state.DisplayedSlots > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _arrow.style.rotate = new Rotate(new Angle(state.ArrowDegrees, AngleUnit.Degree));
            _slots.MarkDirtyRepaint();
            _arrow.MarkDirtyRepaint();
        }

        public void Unbind()
        {

            if (_arrow != null) _arrow.generateVisualContent -= PaintArrow;
            if (_slots != null) _slots.generateVisualContent -= PaintSlots;
            if (_root != null) { _root.style.display = DisplayStyle.None; _root.Clear(); }
            _root = _panel = _extra = _directionGroup = _arrow = _slots = null;
            _count = _golden = _overflow = null;
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

        private void PaintSlots(MeshGenerationContext context)
        {
            if (_state == null) return;
            var painter = context.painter2D;
            painter.strokeColor = _config.MutedColor;
            painter.fillColor = _config.PanelColor;
            painter.lineWidth = _config.StrokeWidth;
            for (int index = 0; index < _state.DisplayedSlots; index++)
            {
                Path(painter, _geometry.Panel(_geometry.Slot(index, _config.SlotSize, _config.SlotGap), _config.CornerCut));
                painter.Fill();
                painter.Stroke();
            }
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
