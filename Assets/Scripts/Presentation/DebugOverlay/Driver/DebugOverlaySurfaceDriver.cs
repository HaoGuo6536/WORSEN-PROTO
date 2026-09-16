// ============================================================================
// DebugOverlaySurfaceDriver.cs
// ============================================================================
//
// PURPOSE:
//   Builds a compact diagnostic ribbon that leaves the playfield and HUD readable.
//   Native labels retain supplied diagnostics over a procedural vector backing.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e) · Presentation · DebugOverlay, owned by DebugOverlayDriver.
//
// KEY RESPONSIBILITIES:
//   - Place the four established diagnostic labels at the bottom right.
//   - Paint vector backing and a small signal glyph, pairing callbacks at release.
//
// DEPENDENCIES:
//   - Own DebugOverlayDriverConfig and UnityEngine.UIElements only.
//
// USAGE NOTES:
//   - Persistent tier (§8) through DebugOverlayDriver; holds no scene/gameplay objects.
//   - Build replaces only the owner's document children and retains its PanelSettings.
//   - PanelOffset measures right/bottom inset; RibbonWidth supersedes legacy PanelWidth.
//   - Painting reads layout only. Every element ignores pointer picking.
//
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace Worsen.Presentation.DebugOverlay
{
    [DisallowMultipleComponent]
    public sealed class DebugOverlaySurfaceDriver : MonoBehaviour
    {
        private DebugOverlayDriverConfig _config;
        private VisualElement _panel;

        internal void Build(VisualElement root, DebugOverlayDriverConfig config)
        {
            Release();
            _config = config;
            root.Clear();
            root.style.flexGrow = 1f;
            _panel = new VisualElement { name = "debug-overlay", pickingMode = PickingMode.Ignore };
            _panel.style.position = Position.Absolute;
            _panel.style.right = config.PanelOffset.x;
            _panel.style.bottom = config.PanelOffset.y;
            _panel.style.width = config.RibbonWidth;
            _panel.style.maxWidth = Length.Percent(config.MaximumScreenFraction * 100f);
            _panel.style.flexDirection = FlexDirection.Row;
            _panel.style.alignItems = Align.Center;
            _panel.style.paddingLeft = config.Padding * 2f;
            _panel.style.paddingRight = config.Padding;
            _panel.style.paddingTop = _panel.style.paddingBottom = config.Padding * 0.5f;
            _panel.generateVisualContent += DrawRibbon;
            root.Add(_panel);
            AddField("tick-value", 1f, config.MutedColor);
            AddField("phase-value", 1.1f, config.MutedColor);
            AddField("speed-value", 1.35f, config.BoneColor);
            AddField("movement-value", 2.2f, config.BoneColor);
        }

        internal void Release()
        {
            if (_panel != null) _panel.generateVisualContent -= DrawRibbon;
            _panel = null;
            _config = null;
        }

        private void OnDisable() => Release();

        private void AddField(string name, float weight, Color color)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.color = color;
            label.style.fontSize = _config.FontSize;
            label.style.flexGrow = weight;
            label.style.flexShrink = 1f;
            label.style.flexBasis = 0f;
            label.style.minWidth = 0f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = _config.FieldGap;
            _panel.Add(label);
        }

        private void DrawRibbon(MeshGenerationContext context)
        {
            if (_panel == null || _config == null) return;
            float width = _panel.layout.width;
            float height = _panel.layout.height;
            if (width <= 0f || height <= 0f) return;
            var painter = context.painter2D;
            float inset = _config.StrokeWidth * 0.5f;
            float cut = Mathf.Min(_config.CornerCut, Mathf.Min(width, height) * 0.25f);
            painter.lineWidth = _config.StrokeWidth;
            painter.fillColor = _config.PanelColor;
            painter.strokeColor = _config.MutedColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(inset + cut, inset));
            painter.LineTo(new Vector2(width - inset, inset));
            painter.LineTo(new Vector2(width - inset, height - inset));
            painter.LineTo(new Vector2(inset, height - inset));
            painter.LineTo(new Vector2(inset, inset + cut));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
            float x = _config.Padding;
            float y = height * 0.5f;
            painter.strokeColor = _config.CrimsonColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x + cut * 0.5f, y));
            painter.LineTo(new Vector2(x + cut, y - cut));
            painter.LineTo(new Vector2(x + cut * 1.5f, y + cut));
            painter.LineTo(new Vector2(x + cut * 2f, y));
            painter.Stroke();
        }
    }
}
