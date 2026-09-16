// ============================================================================
// ProgressionUICardDriver.cs
// ============================================================================
//
// PURPOSE:
//   Renders one keyboard- and mouse-accessible choice or shop card.
//   The parent Driver supplies the complete display model and revision; this
//   component reports a click without purchasing or applying any game effect.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by ProgressionUIDriver · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Paint a cut-corner card and vector emblem with visible keyboard focus.
//   - Pair native Button and focus callbacks across rebinding and teardown.
//
// DEPENDENCIES:
//   Own display definitions, geometry Presenter and DriverConfig; UI Toolkit.
//
// USAGE NOTES:
//   Scene-owned, sharing the owning ProgressionUIDriverConfig (§7d).
//   No global side effects. All actions carry the displayed revision.
//   Disabled cards retain readable descriptions and explicit purchase status.
//
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Worsen.Presentation.ProgressionUI
{
    public sealed class ProgressionUICardDriver : MonoBehaviour
    {
        private ProgressionUIDriverConfig _config;
        private ProgressionUICard _model;
        private readonly ProgressionUIGeometryPresenter _geometry = new ProgressionUIGeometryPresenter();
        private Button _button;
        private VisualElement _emblem;
        private Label _title, _description, _detail, _action;
        private int _revision;
        private bool _focused, _hovered;

        public event Action<string, int> Activated;

        public void Bind(VisualElement container, ProgressionUIDriverConfig config)
        {
            Unbind();
            _config = config;
            _button = new Button { name = "progression-card", focusable = true };
            _button.text = "";
            _button.style.width = config.CardWidth;
            _button.style.maxWidth = Length.Percent(100);
            _button.style.flexGrow = 1;
            _button.style.flexShrink = 1;
            _button.style.minWidth = config.CardWidth * .72f;
            _button.style.marginRight = _button.style.marginBottom = config.Spacing * .5f;
            _button.style.paddingLeft = _button.style.paddingRight = config.Spacing;
            _button.style.paddingTop = _button.style.paddingBottom = config.Spacing;
            _button.style.backgroundColor = Color.clear;
            _button.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            _button.style.borderLeftWidth = _button.style.borderRightWidth = 0;
            _button.style.borderTopWidth = _button.style.borderBottomWidth = 0;
            _button.style.color = config.TextColor;
            _button.style.alignItems = Align.FlexStart;
            _button.style.whiteSpace = WhiteSpace.Normal;
            _button.generateVisualContent += PaintCard;
            _button.clicked += OnClicked;
            _button.RegisterCallback<FocusInEvent>(OnFocusIn);
            _button.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _button.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            container.Add(_button);

            _emblem = new VisualElement { name = "choice-emblem", pickingMode = PickingMode.Ignore };
            _emblem.style.width = _emblem.style.height = config.Spacing * 2;
            _emblem.style.marginBottom = config.Spacing;
            _emblem.generateVisualContent += PaintEmblem;
            _button.Add(_emblem);
            _title = Text("choice-title", config.FontSize, _button);
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = Text("choice-description", config.FontSize * .83f, _button);
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginTop = config.Spacing * .5f;
            _description.style.minHeight = config.FontSize * 3.6f;
            _description.style.flexGrow = 1;
            _detail = Text("choice-detail", config.FontSize * .65f, _button);
            _detail.style.color = config.MutedColor;
            _detail.style.marginTop = config.Spacing;
            _action = Text("choice-action", config.FontSize * .78f, _button);
            _action.style.letterSpacing = 1.5f;
            _action.style.marginTop = config.Spacing;
        }

        public void Apply(ProgressionUICard model, int revision, bool pending)
        {
            _model = model;
            _revision = revision;
            if (_button == null) return;
            _title.text = model.Title ?? "";
            _description.text = model.Description ?? "";
            _detail.text = model.Detail ?? "";
            _action.text = pending ? "CONFIRMING..." : model.Action;
            _button.tooltip = model.Description;
            _button.SetEnabled(model.Enabled && !pending);
            _button.style.opacity = model.Enabled ? 1f : .78f;
            _button.MarkDirtyRepaint();
            _emblem.MarkDirtyRepaint();
        }

        public bool FocusIfEnabled()
        {
            if (_button == null || _button.panel == null || !_button.enabledInHierarchy ||
                _button.layout.width <= 0f || _button.layout.height <= 0f) return false;
            _button.Focus();
            return ReferenceEquals(_button.focusController?.focusedElement, _button);
        }

        public void Unbind()
        {
            if (_button != null)
            {
                _button.clicked -= OnClicked;
                _button.generateVisualContent -= PaintCard;
                _button.UnregisterCallback<FocusInEvent>(OnFocusIn);
                _button.UnregisterCallback<FocusOutEvent>(OnFocusOut);
                _button.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                _button.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
                _button.RemoveFromHierarchy();
            }
            if (_emblem != null) _emblem.generateVisualContent -= PaintEmblem;
            _button = null; _emblem = null;
            _title = _description = _detail = _action = null;
            _focused = _hovered = false;
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        private void OnClicked()
        {
            if (_button != null && _button.enabledInHierarchy) Activated?.Invoke(_model.Id, _revision);
        }
        private void OnFocusIn(FocusInEvent evt) { _focused = true; _button?.MarkDirtyRepaint(); }
        private void OnFocusOut(FocusOutEvent evt) { _focused = false; _button?.MarkDirtyRepaint(); }
        private void OnPointerEnter(PointerEnterEvent evt) { _hovered = true; _button?.MarkDirtyRepaint(); }
        private void OnPointerLeave(PointerLeaveEvent evt) { _hovered = false; _button?.MarkDirtyRepaint(); }

        private void PaintCard(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.fillColor = _config.PanelColor;
            painter.strokeColor = _focused ? _config.TextColor : _hovered ? _config.WarningColor : _config.MutedColor;
            painter.lineWidth = _config.StrokeWidth * (_focused ? 2f : 1f);
            Path(painter, _geometry.Panel(new Rect(0, 0, _button.layout.width, _button.layout.height), _config.CornerCut));
            painter.Fill();
            painter.Stroke();
        }

        private void PaintEmblem(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.strokeColor = _config.WarningColor;
            painter.lineWidth = _config.StrokeWidth * 2;
            Path(painter, _geometry.Emblem(_emblem.contentRect, _model.Kind));
            painter.Stroke();
        }

        private static void Path(Painter2D painter, Vector2[] vertices)
        {
            painter.BeginPath();
            painter.MoveTo(vertices[0]);
            for (int i = 1; i < vertices.Length; i++) painter.LineTo(vertices[i]);
            painter.ClosePath();
        }

        private Label Text(string name, float size, VisualElement parent)
        {
            var label = new Label { enableRichText = false, name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = Mathf.Max(size, _config.SmallFontSize);
            parent.Add(label);
            return label;
        }
    }
}
