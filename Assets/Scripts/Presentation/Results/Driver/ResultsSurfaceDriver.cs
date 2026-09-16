// ============================================================================
// ResultsSurfaceDriver.cs
// ============================================================================
//
// PURPOSE:
//   Builds the run-results surface with native text and a keyboard-focusable button.
//   Vector chrome gives the summary a restrained horror treatment without bitmap assets.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e) · Presentation · Results, owned by ResultsDriver.
//
// KEY RESPONSIBILITIES:
//   - Build a centered, bounded summary and preserve the established element names.
//   - Paint panel/button chrome and pair all paint/focus callbacks with release.
//
// DEPENDENCIES:
//   - Own ResultsDriverConfig and UnityEngine.UIElements only.
//
// USAGE NOTES:
//   - Scene-owned tier (§8); ResultsDriver creates/reuses this component and owns binding.
//   - Build replaces only its owner's UIDocument children; PanelSettings/theme stay intact.
//   - Release runs on owner unbind and disable; drawing reads layout without mutating it.
//
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace Worsen.Presentation.Results
{
    [DisallowMultipleComponent]
    public sealed class ResultsSurfaceDriver : MonoBehaviour
    {
        private ResultsDriverConfig _config;
        private VisualElement _panel;
        private Button _button;
        private Label _restartLabel;

        internal void Build(VisualElement root, ResultsDriverConfig config)
        {
            Release();
            _config = config;
            root.Clear();
            root.style.flexGrow = 1f;
            var overlay = new VisualElement { name = "results" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0f;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.paddingLeft = overlay.style.paddingRight = config.ScreenMargin;
            overlay.style.paddingTop = overlay.style.paddingBottom = config.ScreenMargin;
            overlay.style.backgroundColor = config.BackdropColor;
            overlay.style.display = DisplayStyle.None;
            root.Add(overlay);

            _panel = new VisualElement { name = "results-panel" };
            _panel.style.width = config.PanelWidth;
            _panel.style.maxWidth = Length.Percent(100f);
            _panel.style.maxHeight = Length.Percent(100f);
            _panel.style.flexShrink = 1f;
            _panel.style.paddingLeft = _panel.style.paddingRight = config.PanelPadding;
            _panel.style.paddingTop = _panel.style.paddingBottom = config.PanelPadding;
            _panel.style.color = config.BoneColor;
            _panel.style.fontSize = config.FontSize;
            _panel.generateVisualContent += DrawPanel;
            overlay.Add(_panel);

            var body = new ScrollView(ScrollViewMode.Vertical);
            body.style.flexShrink = 1f;
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            body.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _panel.Add(body);
            var kicker = AddLabel(body, "results-kicker", "AFTER THE NIGHT", config.CaptionFontSize, config.MutedColor);
            kicker.style.unityTextAlign = TextAnchor.MiddleCenter;
            kicker.style.marginTop = config.RowGap;
            var title = AddLabel(body, "results-title", "", config.TitleFontSize, config.BoneColor);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            var reason = AddLabel(body, "end-reason", "", config.FontSize, config.BoneColor);
            reason.style.unityTextAlign = TextAnchor.MiddleCenter;
            reason.style.marginBottom = config.RowGap;
            AddRow(body, "TIME SURVIVED", "run-time", "CAKES FOUND", "cake-total");
            AddRow(body, "GOLDEN CAKES", "golden-cake-total", "CHASES", "chase-count");
            AddRow(body, "CHASES ESCAPED", "chase-escapes", "TIME IN CHASE", "chase-time");

            _button = new Button { name = "restart-button", text = "", focusable = true, tabIndex = 0 };
            _button.style.height = config.ButtonHeight;
            _button.style.minHeight = config.ButtonHeight;
            _button.style.marginTop = config.RowGap;
            _button.style.marginLeft = _button.style.marginRight = 0f;
            _button.style.color = config.BoneColor;
            _button.style.fontSize = config.FontSize;
            _button.style.backgroundColor = Color.clear;
            _button.style.backgroundImage = StyleKeyword.None;
            _button.style.borderLeftWidth = _button.style.borderRightWidth = 0f;
            _button.style.borderTopWidth = _button.style.borderBottomWidth = 0f;
            _button.generateVisualContent += DrawButton;
            _button.RegisterCallback<FocusInEvent>(OnFocusIn);
            _button.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _panel.Add(_button);
            _restartLabel = AddLabel(_button, "restart-label", "", config.FontSize, config.BoneColor);
            _restartLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _restartLabel.style.flexGrow = 1f;
            var hint = AddLabel(_panel, "restart-hint", "ENTER / SPACE  ·  RUN AGAIN", config.CaptionFontSize, config.MutedColor);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = config.RowGap * 0.5f;
        }

        internal void SetRestartLabel(string text)
        {
            if (_restartLabel != null) _restartLabel.text = text;
        }

        internal void Release()
        {
            if (_panel != null) _panel.generateVisualContent -= DrawPanel;
            if (_button != null)
            {
                _button.generateVisualContent -= DrawButton;
                _button.UnregisterCallback<FocusInEvent>(OnFocusIn);
                _button.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            }
            _panel = null;
            _button = null;
            _restartLabel = null;
            _config = null;
        }

        private void OnDisable() => Release();
        private void OnFocusIn(FocusInEvent evt) => _button?.MarkDirtyRepaint();
        private void OnFocusOut(FocusOutEvent evt) => _button?.MarkDirtyRepaint();

        private void AddRow(VisualElement parent, string leftCaption, string leftName, string rightCaption, string rightName)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = _config.RowGap;
            parent.Add(row);
            AddMetric(row, leftCaption, leftName);
            AddMetric(row, rightCaption, rightName);
        }

        private void AddMetric(VisualElement row, string caption, string name)
        {
            var cell = new VisualElement();
            cell.style.width = Length.Percent(50f);
            cell.style.paddingRight = _config.RowGap * 0.5f;
            row.Add(cell);
            AddLabel(cell, name + "-caption", caption, _config.CaptionFontSize, _config.MutedColor);
            AddLabel(cell, name, "", _config.FontSize, _config.BoneColor);
        }

        private static Label AddLabel(VisualElement parent, string name, string text, int size, Color color)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        private void DrawPanel(MeshGenerationContext context)
        {
            if (_panel == null || _config == null) return;
            float width = _panel.layout.width;
            float height = _panel.layout.height;
            if (width <= 0f || height <= 0f) return;
            var painter = context.painter2D;
            PaintChrome(painter, width, height, _config.PanelColor, _config.MutedColor);
            float x = width * 0.5f;
            float y = _config.PanelPadding * 0.5f;
            float radius = _config.CornerCut * 0.5f;
            painter.fillColor = _config.CrimsonColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y - radius));
            painter.LineTo(new Vector2(x + radius, y));
            painter.LineTo(new Vector2(x, y + radius));
            painter.LineTo(new Vector2(x - radius, y));
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawButton(MeshGenerationContext context)
        {
            if (_button == null || _config == null) return;
            bool focused = _button.panel?.focusController.focusedElement == _button;
            Color border = focused ? _config.BoneColor : _config.CrimsonColor;
            PaintChrome(context.painter2D, _button.layout.width, _button.layout.height, _config.PanelColor, border);
        }

        private void PaintChrome(Painter2D painter, float width, float height, Color fill, Color stroke)
        {
            if (width <= 0f || height <= 0f) return;
            float inset = _config.StrokeWidth * 0.5f;
            float cut = Mathf.Min(_config.CornerCut, Mathf.Min(width, height) * 0.25f);
            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = _config.StrokeWidth;
            painter.BeginPath();
            painter.MoveTo(new Vector2(inset + cut, inset));
            painter.LineTo(new Vector2(width - inset, inset));
            painter.LineTo(new Vector2(width - inset, height - inset - cut));
            painter.LineTo(new Vector2(width - inset - cut, height - inset));
            painter.LineTo(new Vector2(inset, height - inset));
            painter.LineTo(new Vector2(inset, inset + cut));
            painter.ClosePath();
            if (fill.a > 0f) painter.Fill();
            painter.Stroke();
        }
    }
}
