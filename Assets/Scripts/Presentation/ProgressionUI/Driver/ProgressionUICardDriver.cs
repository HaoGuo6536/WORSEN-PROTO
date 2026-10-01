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
//   - Paint a compact cut-corner card, an inline title emblem and visible keyboard focus.
//   - Keep unavailable offers focusable for inspection, and scroll focused cards into view.
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
using Worsen.Core;

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
        public event Action<CueId> Feedback;

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
            _button.style.minWidth = 0;
            _button.style.flexBasis = config.CardWidth;
            _button.style.marginRight = _button.style.marginBottom = config.Spacing * .5f;
            _button.style.paddingLeft = _button.style.paddingRight = config.Spacing * .75f;
            _button.style.paddingTop = _button.style.paddingBottom = config.Spacing * .75f;
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

            // The emblem sits inline with the title so a card spends its height on text, not chrome.
            var header = new VisualElement { name = "choice-header", pickingMode = PickingMode.Ignore };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.alignSelf = Align.Stretch;
            _button.Add(header);
            _emblem = new VisualElement { name = "choice-emblem", pickingMode = PickingMode.Ignore };
            _emblem.style.width = _emblem.style.height = config.Spacing;
            _emblem.style.flexShrink = 0;
            _emblem.style.marginRight = config.Spacing * .5f;
            _emblem.generateVisualContent += PaintEmblem;
            header.Add(_emblem);
            _title = Text("choice-title", config.FontSize, header);
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.flexShrink = 1;
            _title.style.whiteSpace = WhiteSpace.Normal;
            _description = Text("choice-description", config.FontSize * .83f, _button);
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginTop = config.Spacing * .35f;
            _description.style.flexGrow = 1;
            _detail = Text("choice-detail", config.FontSize * .65f, _button);
            _detail.style.color = config.MutedColor;
            _detail.style.whiteSpace = WhiteSpace.Normal;
            _detail.style.marginTop = config.Spacing * .35f;
            _action = Text("choice-action", config.FontSize * .78f, _button);
            _action.style.letterSpacing = 1.5f;
            _action.style.marginTop = config.Spacing * .5f;
        }

        public void Apply(ProgressionUICard model, int revision, bool pending)
        {
            _model = model;
            _revision = revision;
            if (_button == null) return;
            _title.text = model.Title ?? "";
            _description.text = model.Description ?? "";
            _detail.text = model.Detail ?? "";
            _detail.style.display = string.IsNullOrEmpty(model.Detail) ? DisplayStyle.None : DisplayStyle.Flex;
            _action.text = pending ? "CONFIRMING..." : model.Action;
            _button.tooltip = string.IsNullOrEmpty(model.Detail) ? model.Description : model.Description + "\n" + model.Detail;
            _button.SetEnabled(!pending);
            _button.style.opacity = model.Enabled ? 1f : .78f;
            _button.MarkDirtyRepaint();
            _emblem.MarkDirtyRepaint();
        }

        public bool FocusIfEnabled()
        {
            if (!_model.Enabled || _button == null || _button.panel == null || !_button.enabledInHierarchy ||
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
        private void OnFocusIn(FocusInEvent evt)
        {
            if (!_hovered) Feedback?.Invoke(CueId.UiMove);
            _focused = true; _button?.MarkDirtyRepaint();
            _button?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(_button);
        }
        private void OnFocusOut(FocusOutEvent evt) { _focused = false; _button?.MarkDirtyRepaint(); }
        private void OnPointerEnter(PointerEnterEvent evt) { if (!_focused) Feedback?.Invoke(CueId.UiMove); _hovered = true; _button?.MarkDirtyRepaint(); }
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
