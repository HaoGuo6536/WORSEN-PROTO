// ============================================================================
// ProgressionUIVisualDriver.cs
// ============================================================================
//
// PURPOSE:
//   Builds the progression status bar and modal frame with vector chrome.
//   Choice cards are inserted by the owning Driver. This surface applies supplied
//   text and exposes continue/restart clicks without deciding progression.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by ProgressionUIDriver · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Draw responsive panels and the health gauge with Painter2D.
//   - Keep long retained lists in their own scroll area and keyboard focus visible.
//   - Preserve readable text and native keyboard/mouse buttons in a scrollable modal.
//
// DEPENDENCIES:
//   Own display state, DriverConfig and geometry Presenter; Core phase values.
//
// USAGE NOTES:
//   Scene-owned; shares the owning ProgressionUIDriverConfig (§7d).
//   No global side effects. All button and painter callbacks pair on Bind/Unbind.
//   Camera/input suspension and cursor capture remain external to this surface.
//
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;

namespace Worsen.Presentation.ProgressionUI
{
    public sealed class ProgressionUIVisualDriver : MonoBehaviour
    {
        private ProgressionUIDriverConfig _config;
        private ProgressionUIDriverState _state;
        private readonly ProgressionUIGeometryPresenter _geometry = new ProgressionUIGeometryPresenter();
        private VisualElement _root, _status, _gauge, _modal, _panel;
        private Label _round, _wallet, _health, _title, _subtitle, _burdens, _retained, _message;
        private Button _continue, _restart;
        public VisualElement CardContainer { get; private set; }
        public event Action<int> ContinueClicked;
        public event Action<int> RestartClicked;
        public event Action<CueId> Feedback;

        public void Bind(VisualElement root, ProgressionUIDriverConfig config)
        {
            Unbind();
            _root = root; _config = config;
            root.Clear(); root.styleSheets.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.display = DisplayStyle.Flex;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.color = config.TextColor;
            root.style.fontSize = config.FontSize;

            _status = Element("progression-status", root);
            _status.style.position = Position.Absolute;
            _status.style.top = _status.style.left = config.Spacing;
            _status.style.width = config.PanelWidth * .31f;
            _status.style.maxWidth = Length.Percent(38);
            _status.style.paddingLeft = _status.style.paddingRight = config.Spacing;
            _status.style.paddingTop = _status.style.paddingBottom = config.Spacing * .65f;
            _status.generateVisualContent += PaintStatus;
            _round = Text("round-number", _status, config.FontSize);
            _wallet = Text("wallet", _status, config.FontSize * .75f);
            _wallet.style.color = config.MutedColor;
            _health = Text("health", _status, config.FontSize * .65f);
            _health.style.marginTop = config.Spacing * .5f;
            _gauge = Element("health-gauge", _status);
            _gauge.style.height = config.StrokeWidth * 3;
            _gauge.style.marginTop = config.Spacing * .3f;
            _gauge.generateVisualContent += PaintHealth;

            _modal = Element("progression-modal", root);
            _modal.pickingMode = PickingMode.Position;
            _modal.style.position = Position.Absolute;
            _modal.style.left = _modal.style.right = _modal.style.top = _modal.style.bottom = 0;
            _modal.style.alignItems = Align.Center;
            _modal.style.justifyContent = Justify.Center;
            _modal.generateVisualContent += PaintScrim;
            _modal.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _modal.RegisterCallback<NavigationCancelEvent>(OnCancel);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "progression-scroll" };
            scroll.style.width = config.PanelWidth;
            scroll.style.maxWidth = Length.Percent(92);
            scroll.style.maxHeight = Length.Percent(90);
            scroll.style.flexShrink = 1;
            _modal.Add(scroll);
            _panel = Element("progression-panel", scroll);
            _panel.style.minWidth = 0;
            _panel.style.paddingLeft = _panel.style.paddingRight = config.Spacing * 1.5f;
            _panel.style.paddingTop = _panel.style.paddingBottom = config.Spacing * 1.25f;
            _panel.generateVisualContent += PaintPanel;
            var brand = Text("progression-brand", _panel, config.FontSize * .65f);
            brand.text = "W O R S E N";
            brand.style.color = config.WarningColor;
            brand.style.letterSpacing = 3;
            _title = Text("progression-title", _panel, config.FontSize * 1.55f);
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginTop = config.Spacing * .5f;
            _subtitle = Text("progression-subtitle", _panel, config.FontSize * .8f);
            _subtitle.style.color = config.MutedColor;
            _subtitle.style.marginTop = config.Spacing * .5f;
            _burdens = Text("retained-counts", _panel, config.FontSize * .65f);
            _burdens.style.marginTop = config.Spacing;
            CardContainer = Element("progression-cards", _panel);
            CardContainer.style.flexDirection = FlexDirection.Row;
            CardContainer.style.flexWrap = Wrap.Wrap;
            CardContainer.style.marginTop = config.Spacing;
            _message = Text("progression-feedback", _panel, config.FontSize * .8f);
            _message.style.marginTop = config.Spacing * .5f;
            var retainedScroll = new ScrollView(ScrollViewMode.Vertical) { name = "retained-scroll" };
            retainedScroll.style.maxHeight = config.RetainedMaximumHeight;
            retainedScroll.style.marginTop = config.Spacing;
            _panel.Add(retainedScroll);
            _retained = Text("retained-choices", retainedScroll, config.FontSize * .65f);
            _retained.style.color = config.MutedColor;
            var buttons = Element("progression-actions", _panel);
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginTop = config.Spacing;
            _continue = ActionButton("continue-button", "CONTINUE", buttons);
            _restart = ActionButton("progression-restart-button", "START AGAIN", buttons);
            _continue.clicked += OnContinue;
            _restart.clicked += OnRestart;
            var help = Text("menu-controls", _panel, config.FontSize * .6f);
            help.text = "TAB / SHIFT+TAB  FOCUS     ENTER / SPACE  CONFIRM     ESC / BACK  LEAVE SHOP";
            help.style.color = config.MutedColor;
            help.style.marginTop = config.Spacing;
        }

        public void Apply(ProgressionUIDriverState state)
        {
            _state = state;
            if (_root == null) return;
            bool visible = state.HasSnapshot && !state.Hidden && state.Phase != ProgressionPhase.Dormant;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _status.style.display = state.ModalVisible ? DisplayStyle.None : DisplayStyle.Flex;
            _modal.style.display = state.ModalVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _round.text = state.RoundText;
            _wallet.text = state.WalletText;
            _health.text = state.HealthText;
            _title.text = state.Title;
            _subtitle.text = state.RoundText + "  /  " + state.WalletText + "\n" + state.Subtitle;
            _burdens.text = state.BurdenText + "  /  " + state.HealthText;
            _retained.text = state.RetainedText;
            _message.text = state.Pending ? "Waiting for confirmation..." : state.Message;
            _continue.style.display = state.CanContinue ? DisplayStyle.Flex : DisplayStyle.None;
            _restart.style.display = state.CanRestart ? DisplayStyle.Flex : DisplayStyle.None;
            _continue.SetEnabled(!state.Pending && state.CanContinue);
            _restart.SetEnabled(!state.Pending && state.CanRestart);
            _gauge.MarkDirtyRepaint();
        }

        public bool FocusPrimary()
        {
            Button target = _state != null && _state.CanContinue ? _continue :
                _state != null && _state.CanRestart ? _restart : null;
            if (target == null) return true;
            if (target.panel == null || !target.enabledInHierarchy || target.layout.width <= 0f || target.layout.height <= 0f) return false;
            target.Focus();
            return ReferenceEquals(target.focusController?.focusedElement, target);
        }

        public void Unbind()
        {
            if (_status != null) _status.generateVisualContent -= PaintStatus;
            if (_gauge != null) _gauge.generateVisualContent -= PaintHealth;
            if (_modal != null) { _modal.generateVisualContent -= PaintScrim; _modal.UnregisterCallback<KeyDownEvent>(OnKeyDown); _modal.UnregisterCallback<NavigationCancelEvent>(OnCancel); }
            if (_panel != null) _panel.generateVisualContent -= PaintPanel;
            if (_continue != null) _continue.clicked -= OnContinue;
            if (_restart != null) _restart.clicked -= OnRestart;
            ReleaseButton(_continue); ReleaseButton(_restart);
            if (_root != null) { _root.style.display = DisplayStyle.None; _root.Clear(); }
            _root = _status = _gauge = _modal = _panel = CardContainer = null;
            _continue = _restart = null;
            _round = _wallet = _health = _title = _subtitle = _burdens = _retained = _message = null;
            _state = null;
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        private void OnContinue() { if (_state != null && _continue.enabledInHierarchy) ContinueClicked?.Invoke(_state.Revision); }
        private void OnRestart() { if (_state != null && _restart.enabledInHierarchy) RestartClicked?.Invoke(_state.Revision); }
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || _state == null || !_state.CanContinue || _state.Pending) return;
            OnContinue(); evt.StopPropagation();
        }
        private void OnCancel(NavigationCancelEvent evt)
        {
            if (_state == null || !_state.CanContinue || _state.Pending) return;
            OnContinue(); evt.StopPropagation();
        }
        private void PaintStatus(MeshGenerationContext context) => Panel(context.painter2D, _status, _config.PanelColor, _config.MutedColor);
        private void PaintPanel(MeshGenerationContext context) => Panel(context.painter2D, _panel, _config.PanelColor, _config.MutedColor);
        private void PaintScrim(MeshGenerationContext context) => Panel(context.painter2D, _modal, _config.ScrimColor, Color.clear);
        private void PaintContinue(MeshGenerationContext context) => PaintButton(context, _continue);
        private void PaintRestart(MeshGenerationContext context) => PaintButton(context, _restart);
        private void PaintButton(MeshGenerationContext context, Button button) => Panel(context.painter2D, button,
            _config.PanelColor, ReferenceEquals(button.focusController?.focusedElement, button) ? _config.TextColor : _config.WarningColor);

        private void PaintHealth(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.fillColor = _config.MutedColor;
            Path(painter, _geometry.Panel(_gauge.contentRect, 0)); painter.Fill();
            painter.fillColor = _config.WarningColor;
            Path(painter, _geometry.Panel(_geometry.Gauge(_gauge.contentRect, _state?.HealthFraction ?? 0f), 0)); painter.Fill();
        }

        private void Panel(Painter2D painter, VisualElement element, Color fill, Color stroke)
        {
            painter.fillColor = fill; painter.strokeColor = stroke; painter.lineWidth = _config.StrokeWidth;
            Path(painter, _geometry.Panel(new Rect(0, 0, element.layout.width, element.layout.height), _config.CornerCut));
            painter.Fill(); painter.Stroke();
        }

        private Button ActionButton(string name, string text, VisualElement parent)
        {
            var button = new Button { name = name, text = "", focusable = true };
            button.style.minWidth = _config.CardWidth * .65f;
            button.style.paddingLeft = button.style.paddingRight = _config.Spacing;
            button.style.paddingTop = button.style.paddingBottom = _config.Spacing * .65f;
            button.style.marginRight = _config.Spacing;
            button.style.color = _config.TextColor;
            button.style.fontSize = _config.FontSize * .8f;
            button.style.backgroundColor = Color.clear;
            button.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            button.style.borderLeftWidth = button.style.borderRightWidth = button.style.borderTopWidth = button.style.borderBottomWidth = 0;
            button.RegisterCallback<FocusInEvent>(OnFocusIn);
            button.RegisterCallback<FocusOutEvent>(OnFocusOut);
            if (name == "continue-button") button.generateVisualContent += PaintContinue;
            else button.generateVisualContent += PaintRestart;
            var label = Text(name + "-label", button, _config.FontSize * .8f);
            label.text = text;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(button);
            return button;
        }

        private void ReleaseButton(Button button)
        {
            if (button == null) return;
            button.generateVisualContent -= PaintContinue; button.generateVisualContent -= PaintRestart;
            button.UnregisterCallback<FocusInEvent>(OnFocusIn); button.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }
        private void OnFocusIn(FocusInEvent evt)
        {
            var element = evt.currentTarget as VisualElement;
            element?.MarkDirtyRepaint();
            element?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(element);
            Feedback?.Invoke(CueId.UiMove);
        }
        private void OnFocusOut(FocusOutEvent evt) => (evt.currentTarget as VisualElement)?.MarkDirtyRepaint();
        private static VisualElement Element(string name, VisualElement parent)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore }; parent.Add(element); return element;
        }
        private Label Text(string name, VisualElement parent, float size)
        {
            var label = new Label { enableRichText = false, name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = Mathf.Max(size, _config.SmallFontSize); label.style.whiteSpace = WhiteSpace.Normal; parent.Add(label); return label;
        }
        private static void Path(Painter2D painter, Vector2[] points)
        {
            painter.BeginPath(); painter.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
            painter.ClosePath();
        }
    }
}
