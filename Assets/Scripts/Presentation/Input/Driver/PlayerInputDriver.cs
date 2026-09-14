// ============================================================================
// PlayerInputDriver.cs
// ============================================================================
//
// PURPOSE:
//   Owns the Input System action map and converts its callbacks into Core data.
//   Button callbacks preserve every transition, while mouse deltas are sampled
//   once after each input update to avoid counting cumulative device deltas twice.
//
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Input.
//   Owned only by InputManager; calculation and buffering use InputFramePresenter.
//
// KEY RESPONSIBILITIES:
//   - Create and dispose the gameplay map without editing the Unity template asset.
//   - Capture device facts and ask the Presenter to buffer or publish one frame.
//   - Pair Input System subscriptions and clear pending input on disable or focus loss.
//
// DEPENDENCIES:
//   - Core InputFrame/InputButtons; Unity Input System; the Input presentation stack.
//
// USAGE NOTES:
//   - Lifecycle tier: Persistent with its owning InputManager; Initialize is explicit.
//   - Owns its action map only; it changes no global Input System settings or cursor state.
//   - Bindings: WASD/arrows or left stick move; mouse/right stick look; left Shift/left
//   - stick press sprint; Space/south jump; left Ctrl/east crouch; Q/right shoulder look
//   - back; E/west interact; F/left shoulder use item. The template asset is untouched.
//   - Serialized _config wins; Resources fallback warns and uses ephemeral defaults if absent.
//   - Gamepad turn rate uses the render elapsed time passed to the Presenter.
//   - Mouse deltas are already accumulated by Input System; sample once after its update.
//   - Subsystem hooks are paired in OnEnable/OnDisable; manager teardown disposes the map.
//
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        [SerializeField] private InputDriverConfig _config;
        private InputActionMap _actions;
        private InputAction _look;
        private InputDriverState _state;
        private InputFramePresenter _presenter;
        private bool _initialized;
        private bool _subscribed;
        private bool _ownsFallbackConfig;

        public event Action<InputFrame> FrameCaptured;

        public void Initialize()
        {
            if (_initialized)
                return;
            if (_config == null)
                _config = Resources.Load<InputDriverConfig>(
                    "ScriptableObjects/Presentation/Input/InputDriverConfig");
            if (_config == null)
            {
                Debug.LogWarning("InputDriverConfig is missing. Run Worsen/Scenes/1 — Build TagArena. " +
                    "Input is using temporary defaults.", this);
                _config = ScriptableObject.CreateInstance<InputDriverConfig>();
                _ownsFallbackConfig = true;
            }

            _state = new InputDriverState();
            _presenter = new InputFramePresenter();
            BuildActionMap();
            _initialized = true;
            if (isActiveAndEnabled)
                OnEnable();
        }

        public void SetInputEnabled(bool enabled)
        {
            if (!_initialized)
                return;
            _presenter.SetInputEnabled(_state, enabled);
            RefreshActions();
        }

        public void SetOwnerEnabled(bool enabled)
        {
            if (!_initialized)
                return;
            _presenter.SetOwnerEnabled(_state, enabled);
            RefreshActions();
        }

        public void FlushFrame()
        {
            if (_initialized)
            {
                InputFrame frame = _presenter.Flush(_state);
                FrameCaptured?.Invoke(frame);
            }
        }

        public void Teardown()
        {
            if (!_initialized)
                return;
            OnDisable();
            _actions.Dispose();
            _actions = null;
            _look = null;
            if (_ownsFallbackConfig)
            {
                Destroy(_config);
                _config = null;
                _ownsFallbackConfig = false;
            }
            _initialized = false;
        }

        private void OnEnable()
        {
            if (!_initialized || _subscribed)
                return;
            _actions.actionTriggered += HandleActionTriggered;
            InputSystem.onAfterUpdate += CaptureMouseLook;
            _subscribed = true;
            _presenter.SetFocus(_state, Application.isFocused);
            RefreshActions();
        }

        private void OnDisable()
        {
            if (!_subscribed)
                return;
            _actions.actionTriggered -= HandleActionTriggered;
            InputSystem.onAfterUpdate -= CaptureMouseLook;
            _subscribed = false;
            _actions.Disable();
            _presenter.Reset(_state);
        }

        private void OnDestroy()
        {
            Teardown();
            FrameCaptured = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!_initialized)
                return;
            _presenter.SetFocus(_state, focused);
            RefreshActions();
        }

        private void Update()
        {
            if (_initialized)
                _presenter.AccumulateGamepadLook(_state, Time.unscaledDeltaTime,
                    _config.GamepadDegreesPerSecond, _config.InvertLookY);
        }

        private void RefreshActions()
        {
            if (_subscribed && _presenter.IsAcceptingInput(_state))
                _actions.Enable();
            else
                _actions.Disable();
        }

        private void CaptureMouseLook()
        {
            if (!_actions.enabled || InputState.currentUpdateType == InputUpdateType.Editor ||
                InputState.currentUpdateType == InputUpdateType.BeforeRender)
                return;

            foreach (InputControl control in _look.controls)
            {
                if (control is DeltaControl delta)
                    _presenter.AccumulateMouseLook(_state, delta.ReadValue(),
                        _config.MouseDegreesPerPixel, _config.InvertLookY);
            }
        }

        private void HandleActionTriggered(InputAction.CallbackContext context)
        {
            if (context.action.name == "Move")
            {
                _presenter.SetMove(_state, context.ReadValue<Vector2>());
                return;
            }
            if (context.action == _look)
            {
                if (context.control.device is Gamepad)
                    _presenter.SetGamepadLook(_state, context.ReadValue<Vector2>());
                return;
            }

            InputButtons button;
            switch (context.action.name)
            {
                case "Sprint": button = InputButtons.Sprint; break;
                case "Jump": button = InputButtons.Jump; break;
                case "Crouch": button = InputButtons.Crouch; break;
                case "LookBack": button = InputButtons.LookBack; break;
                case "Interact": button = InputButtons.Interact; break;
                case "UseItem": button = InputButtons.UseItem; break;
                default: return;
            }
            if (context.performed)
                _presenter.SetButton(_state, button, true);
            else if (context.canceled)
                _presenter.SetButton(_state, button, false);
        }

        private void BuildActionMap()
        {
            _actions = new InputActionMap("WorsenGameplay");
            InputAction move = _actions.AddAction("Move", InputActionType.Value,
                expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            _look = _actions.AddAction("Look", InputActionType.PassThrough,
                expectedControlLayout: "Vector2");
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");
            AddButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            AddButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton("Crouch", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
            AddButton("LookBack", "<Keyboard>/q", "<Gamepad>/rightShoulder");
            AddButton("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            AddButton("UseItem", "<Keyboard>/f", "<Gamepad>/leftShoulder");
        }

        private void AddButton(string name, string keyboardPath, string gamepadPath)
        {
            InputAction action = _actions.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
        }
    }
}
