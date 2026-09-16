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
//   - Build a responsive named UI Toolkit tree and draw chrome, gauge and icons.
//   - Pair all vector callbacks when binding, unbinding or replacing a document.
//   - Paint a faceted white compass needle in three dimensions without a caption or render target.
//
// DEPENDENCIES:
//   Own HUDDriverConfig, HUDDriverState and pure HUDGeometryPresenter only.
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
        private readonly HUDCompassPresenter _compass = new HUDCompassPresenter();
        private VisualElement _root, _panel, _gauge, _extra, _directionGroup, _arrow, _slots;
        private Label _count, _exit, _overflow, _warning;

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
            _panel.style.paddingLeft = _panel.style.paddingRight = config.ScreenMargin;
            _panel.style.paddingTop = _panel.style.paddingBottom = config.FontSize;
            _panel.generateVisualContent += PaintPanel;
            var title = Text("objective-title", "RECOVER & ESCAPE", _panel);
            title.style.fontSize = config.SmallFontSize;
            title.style.color = config.MutedColor;
            title.style.letterSpacing = 2;
            _count = Text("cake-count", "Cakes: —", _panel);
            _count.style.fontSize = config.FontSize * 1.35f;
            _count.style.marginTop = config.FontSize * 0.4f;
            _gauge = Element("cake-gauge", _panel);
            _gauge.style.height = config.StrokeWidth * 3;
            _gauge.style.marginTop = _gauge.style.marginBottom = config.FontSize * 0.6f;
            _gauge.generateVisualContent += PaintGauge;
            _exit = Text("exit-state", "Exit: —", _panel);
            _exit.style.fontSize = config.SmallFontSize;
            _warning = Text("chase-warning", "HUNTED", _panel);
            _warning.style.color = config.WarningColor;
            _warning.style.unityFontStyleAndWeight = FontStyle.Bold;
            _warning.style.marginTop = config.FontSize * 0.5f;

            _extra = Element("hud-extra", root);
            _extra.style.position = Position.Absolute;
            _extra.style.left = _extra.style.right = _extra.style.top = _extra.style.bottom = 0;
            _directionGroup = Element("direction-group", _extra);
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
            var controls = Text("controls-hint", "SHIFT  RUN     SPACE  JUMP / CANCEL SLIDE     C  SLIDE     Q  HOLD FREE LOOK     F  FLASHLIGHT", _extra);
            controls.style.position = Position.Absolute;
            controls.style.left = config.ScreenMargin;
            controls.style.bottom = config.ScreenMargin;
            controls.style.maxWidth = Length.Percent(72);
            controls.style.whiteSpace = WhiteSpace.Normal;
            controls.style.color = config.MutedColor;
            controls.style.fontSize = config.SmallFontSize;
        }

        public void Apply(HUDDriverState state)
        {
            if (_root == null || state == null) return;
            _state = state;
            _count.text = state.CountText;
            _exit.text = state.ExitText;
            _exit.style.color = state.ExitOpen ? _config.TextColor : _config.MutedColor;
            _warning.style.display = state.ChaseMode ? DisplayStyle.Flex : DisplayStyle.None;
            _extra.style.display = state.ChaseMode ? DisplayStyle.None : DisplayStyle.Flex;
            _extra.style.opacity = state.ExtraOpacity;
            _directionGroup.style.display = state.DirectionVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _overflow.text = state.SlotOverflowText;
            _slots.style.width = state.DisplayedSlots * (_config.SlotSize + _config.SlotGap);
            _panel.MarkDirtyRepaint();
            _gauge.MarkDirtyRepaint();
            _slots.MarkDirtyRepaint();
            _arrow.MarkDirtyRepaint();
        }

        public void Unbind()
        {
            if (_panel != null) _panel.generateVisualContent -= PaintPanel;
            if (_gauge != null) _gauge.generateVisualContent -= PaintGauge;
            if (_arrow != null) _arrow.generateVisualContent -= PaintArrow;
            if (_slots != null) _slots.generateVisualContent -= PaintSlots;
            if (_root != null) { _root.style.display = DisplayStyle.None; _root.Clear(); }
            _root = _panel = _gauge = _extra = _directionGroup = _arrow = _slots = null;
            _count = _exit = _overflow = _warning = null;
            _state = null;
            _config = null;
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        private void PaintPanel(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.fillColor = _config.PanelColor;
            painter.strokeColor = _state != null && _state.ChaseMode ? _config.WarningColor : _config.MutedColor;
            painter.lineWidth = _config.StrokeWidth;
            Path(painter, _geometry.Panel(new Rect(0, 0, _panel.layout.width, _panel.layout.height), _config.CornerCut));
            painter.Fill();
            painter.Stroke();
        }

        private void PaintGauge(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.fillColor = _config.MutedColor;
            Path(painter, _geometry.Panel(_gauge.contentRect, 0));
            painter.Fill();
            painter.fillColor = _state != null && _state.ChaseMode ? _config.WarningColor : _config.TextColor;
            Path(painter, _geometry.Panel(_geometry.Gauge(_gauge.contentRect, _state?.CountFraction ?? 0f), 0));
            painter.Fill();
        }

        private void PaintArrow(MeshGenerationContext context)
        {
            if (_state == null || !_state.DirectionVisible) return;
            var painter = context.painter2D;
            painter.fillColor = new Color(0f, 0f, 0f, .2f);
            painter.strokeColor = new Color(1f, 1f, 1f, .3f);
            painter.lineWidth = 1f;
            Path(painter, _compass.BaseRing(_arrow.contentRect));
            painter.Fill(); painter.Stroke();
            foreach (HUDCompassFace face in _compass.Needle(_arrow.contentRect, _state.ViewDirection))
            {
                painter.fillColor = face.Color;
                painter.BeginPath(); painter.MoveTo(face.A); painter.LineTo(face.B); painter.LineTo(face.C);
                painter.ClosePath(); painter.Fill();
            }
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
