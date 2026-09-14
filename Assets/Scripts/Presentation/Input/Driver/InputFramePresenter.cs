// ============================================================================
// InputFramePresenter.cs
// ============================================================================
//
// PURPOSE:
//   Converts device values into a single tick's immutable input frame.
//   It preserves short taps and accumulates look motion until publication,
//   then consumes only transient values so later ticks cannot repeat them.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Input.
//   Pure calculations over InputDriverState, owned by PlayerInputDriver.
//
// KEY RESPONSIBILITIES:
//   - Buffer independent button press and release edges.
//   - Normalize movement and scale mouse displacement and gamepad look rate.
//   - Clear pending input on focus loss or an input gate closing.
//
// DEPENDENCIES:
//   - Core InputFrame/InputButtons and the owning InputDriverState.
//
// USAGE NOTES:
//   - No engine calls, time reads, or random values; elapsed time is an argument.
//   - InputFramePresenterTests covers buffering, gates, and nonrepeating motion.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    public sealed class InputFramePresenter
    {
        public bool IsAcceptingInput(InputDriverState state)
        {
            return state.InputEnabled && state.OwnerEnabled && state.HasFocus;
        }

        public void SetMove(InputDriverState state, Vector2 move)
        {
            if (IsAcceptingInput(state))
                state.Move = Vector2.ClampMagnitude(move, 1f);
        }

        public void SetGamepadLook(InputDriverState state, Vector2 look)
        {
            if (IsAcceptingInput(state))
                state.GamepadLook = Vector2.ClampMagnitude(look, 1f);
        }

        public void AccumulateMouseLook(InputDriverState state, Vector2 delta,
            float degreesPerPixel, bool invertY)
        {
            if (!IsAcceptingInput(state))
                return;
            state.LookDelta += new Vector2(delta.x, invertY ? -delta.y : delta.y)
                * Mathf.Max(0f, degreesPerPixel);
        }

        public void AccumulateGamepadLook(InputDriverState state, float deltaTime,
            float degreesPerSecond, bool invertY)
        {
            if (!IsAcceptingInput(state))
                return;
            Vector2 look = state.GamepadLook;
            state.LookDelta += new Vector2(look.x, invertY ? -look.y : look.y)
                * Mathf.Max(0f, deltaTime) * Mathf.Max(0f, degreesPerSecond);
        }

        public void SetButton(InputDriverState state, InputButtons button, bool held)
        {
            if (!IsAcceptingInput(state))
                return;
            if (held)
            {
                state.Pressed |= button & ~state.Held;
                state.Held |= button;
            }
            else
            {
                state.Released |= button & state.Held;
                state.Held &= ~button;
            }
        }

        public InputFrame Flush(InputDriverState state)
        {
            if (!IsAcceptingInput(state))
            {
                Reset(state);
                return default;
            }

            var frame = new InputFrame(state.Move, state.LookDelta,
                state.Held, state.Pressed, state.Released);
            state.LookDelta = Vector2.zero;
            state.Pressed = InputButtons.None;
            state.Released = InputButtons.None;
            return frame;
        }

        public void SetInputEnabled(InputDriverState state, bool enabled)
        {
            state.InputEnabled = enabled;
            if (!enabled)
                Reset(state);
        }

        public void SetOwnerEnabled(InputDriverState state, bool enabled)
        {
            state.OwnerEnabled = enabled;
            if (!enabled)
                Reset(state);
        }

        public void SetFocus(InputDriverState state, bool focused)
        {
            state.HasFocus = focused;
            if (!focused)
                Reset(state);
        }

        public void Reset(InputDriverState state)
        {
            state.Move = Vector2.zero;
            state.LookDelta = Vector2.zero;
            state.GamepadLook = Vector2.zero;
            state.Held = InputButtons.None;
            state.Pressed = InputButtons.None;
            state.Released = InputButtons.None;
        }
    }
}

