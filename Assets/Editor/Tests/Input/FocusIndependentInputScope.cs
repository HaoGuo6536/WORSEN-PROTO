// ============================================================================
// FocusIndependentInputScope.cs
// ============================================================================
// PURPOSE:
//   Allows synchronous synthetic input work without depending on Game view focus.
//   Restores the original settings object before returning, including on failure,
//   so no override is alive at a coroutine yield, Play Mode exit or domain reload.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test support (§11) · Presentation · Input.
// KEY RESPONSIBILITIES:
//   - Bound the unsaved focus override to a synchronous call with finally cleanup.
//   - Restore background execution and destroy only the scope's settings clone.
// DEPENDENCIES:
//   - Unity Input System and UnityEngine; no runtime project dependencies.
// USAGE NOTES:
//   Main-thread test work only. The callback must not yield, schedule asynchronous
//   work or request an editor transition. Action maps must select test devices;
//   the temporary focus policy itself is global, not a per-device policy.
//   No project asset is changed and no fixture field owns a settings clone.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Worsen.Tests.Input
{
    internal static class FocusIndependentInputScope
    {
        internal static void Run(Action work)
        {
            InputSettings original = InputSystem.settings;
            bool previousBackground = Application.runInBackground;
            InputSettings temporary = null;
            try
            {
                temporary = UnityEngine.Object.Instantiate(original);
                temporary.hideFlags = HideFlags.HideAndDontSave;
                temporary.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                InputSystem.settings = temporary;
                work();
            }
            finally
            {
                try { InputSystem.settings = original; }
                finally
                {
                    Application.runInBackground = previousBackground;
                    if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
                }
            }
        }
    }
}
