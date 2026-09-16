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
//   - Own InputRecorder; select one source and clear stale live input on every switch.
//   - Pair Input System subscriptions and clear pending input on disable or focus loss.
//   - Capture the cursor only for ready, focused live gameplay; release it for UI.
//
// DEPENDENCIES:
//   - Core InputFrame/InputButtons; Unity Input System; the Input presentation stack.
//
// USAGE NOTES:
//   - Lifecycle tier: Persistent with its owning InputManager; Initialize is explicit.
//   - Owns its action map and cursor state from Initialize through Teardown; captures
//     the previous cursor lock/visibility once and restores both when that lifetime ends.
//   - Load/end gates, focus loss, disable and playback release/show the cursor;
//     returning to ready live input locks/hides it. UI action maps remain independent.
//   - Changes no global Input System settings; an uninitialized duplicate owns no cursor.
//   - Bindings: WASD/arrows or left stick move; mouse/right stick look; left Shift/left
//   - stick press hold to sprint; Space/south jump or cancel slide; C/east crouch/slide; Q/right shoulder look
//   - back; E/west interact; F/left shoulder use item. The template asset is untouched.
//   - Serialized _config wins; Resources fallback warns and uses ephemeral defaults if absent.
//   - Gamepad turn rate uses the render elapsed time passed to the Presenter.
//   - Mouse deltas are already accumulated by Input System; sample once after its update.
//   - Subsystem hooks are paired in OnEnable/OnDisable; manager teardown disposes the map.
//
// ============================================================================

using System;
using System.Collections.Generic;
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
        private InputRecorder _recorder;
        private InputAction _look;
        private InputDriverState _state;
        private InputFramePresenter _presenter;
        private bool _initialized;
        private bool _subscribed;
        private bool _ownsFallbackConfig;

        public event Action<InputFrame> FrameCaptured;
        public InputSource Source => _recorder == null ? InputSource.Live : _recorder.Source;
        public InputProbeRecord CurrentPlaybackRecord => _recorder == null ? default : _recorder.CurrentPlaybackRecord;
        public string LastRecordingPath => _recorder == null ? "" : _recorder.LastRecordingPath;
        public string LastRecordingError => _recorder == null ? "Input is not initialized." : _recorder.LastError;

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

            _state = new InputDriverState
            {
                OwnsCursorState = true,
                PreviousCursorLockMode = Cursor.lockState,
                PreviousCursorVisible = Cursor.visible
            };
            _presenter = new InputFramePresenter();
            _recorder = GetComponent<InputRecorder>();
            if (_recorder == null)
                _recorder = gameObject.AddComponent<InputRecorder>();
            BuildActionMap();
            _initialized = true;
            if (isActiveAndEnabled)
                OnEnable();
        }

        public void SetInputEnabled(bool enabled)
        {
            if (!_initialized)
                return;
            if (!enabled) _recorder.Interrupt();
            _presenter.SetInputEnabled(_state, enabled);
            RefreshActions();
        }

        public void SetOwnerEnabled(bool enabled)
        {
            if (!_initialized)
                return;
            if (!enabled) _recorder.Interrupt();
            _presenter.SetOwnerEnabled(_state, enabled);
            RefreshActions();
        }

        public void FlushFrame()
        {
            if (_initialized)
            {
                InputFrame frame;
                if (Source == InputSource.Playback)
                {
                    _recorder.TryReadPlayback(_presenter.IsAcceptingInput(_state), out frame);
                    _presenter.Reset(_state);
                    RefreshActions();
                }
                else
                    frame = _presenter.Flush(_state);
                FrameCaptured?.Invoke(frame);
            }
        }

        public bool StartPlayback(RunCaptureMetadata metadata, IReadOnlyList<InputProbeRecord> records)
        {
            if (!_initialized) return false;
            _presenter.Reset(_state);
            bool loaded = _recorder.StartPlayback(metadata, records, _presenter.IsAcceptingInput(_state));
            RefreshActions();
            return loaded;
        }

        public bool LoadPlayback(string absolutePath)
        {
            if (!_initialized) return false;
            _presenter.Reset(_state);
            bool loaded = _recorder.LoadPlayback(absolutePath, _presenter.IsAcceptingInput(_state));
            RefreshActions();
            return loaded;
        }

        public bool SetSource(InputSource source)
        {
            if (!_initialized) return false;
            if (source == InputSource.Playback) return Source == InputSource.Playback;
            if (source != InputSource.Live) return false;
            _recorder.StopPlayback();
            _presenter.Reset(_state);
            RefreshActions();
            return true;
        }

        public void BeginRecording(RunCaptureMetadata metadata)
        {
            if (!_initialized) return;
            _presenter.Reset(_state);
            _recorder.BeginRecording(metadata);
            RefreshActions();
        }
        public bool RecordProbe(InputProbeRecord record) => _initialized && _recorder.RecordProbe(record);
        public bool SaveRecording(long endTick, bool complete) => _initialized && _recorder.SaveRecording(endTick, complete);

        public void Teardown()
        {
            if (!_initialized)
                return;
            OnDisable();
            _recorder.Interrupt();
            _actions.Dispose();
            _actions = null;
            _look = null;
            if (_ownsFallbackConfig)
            {
                Destroy(_config);
                _config = null;
                _ownsFallbackConfig = false;
            }
            RestoreCursor();
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
            SetCursorCaptured(false);
            if (!_subscribed)
                return;
            _actions.actionTriggered -= HandleActionTriggered;
            InputSystem.onAfterUpdate -= CaptureMouseLook;
            _subscribed = false;
            _actions.Disable();
            _recorder.Interrupt();
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
            if (!focused) _recorder.Interrupt();
            _presenter.SetFocus(_state, focused);
            RefreshActions();
        }

        private void Update()
        {
            if (_initialized && Source == InputSource.Live)
                _presenter.AccumulateGamepadLook(_state, Time.unscaledDeltaTime,
                    _config.GamepadDegreesPerSecond, _config.InvertLookY);
        }

        private void RefreshActions()
        {
            bool acceptingLiveInput = _subscribed && isActiveAndEnabled &&
                Source == InputSource.Live && _presenter.IsAcceptingInput(_state);
            SetCursorCaptured(acceptingLiveInput);
            if (acceptingLiveInput)
                _actions.Enable();
            else
                _actions.Disable();
        }

        private void SetCursorCaptured(bool captured)
        {
            if (_state == null || !_state.OwnsCursorState)
                return;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void RestoreCursor()
        {
            if (_state == null || !_state.OwnsCursorState)
                return;
            Cursor.lockState = _state.PreviousCursorLockMode;
            Cursor.visible = _state.PreviousCursorVisible;
            _state.OwnsCursorState = false;
        }

        private void CaptureMouseLook()
        {
            if (Source != InputSource.Live || !_actions.enabled || InputState.currentUpdateType == InputUpdateType.Editor ||
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
            if (Source != InputSource.Live) return;
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
            AddButton("Crouch", "<Keyboard>/c", "<Gamepad>/buttonEast");
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
