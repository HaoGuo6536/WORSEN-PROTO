// ============================================================================
// MenuPauseSafety.cs
// ============================================================================
// PURPOSE:
//   Prevents an acknowledged menu pause from surviving the editor's Play Mode exit.
//   This is a last-resort guard when normal scene teardown has not released ownership.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Menu.
// KEY RESPONSIBILITIES:
//   - Release pause ownership before restoring normal editor time and log once.
// DEPENDENCIES:
//   UnityEditor play-mode notifications and Presentation Menu's engine boundary.
// USAGE NOTES:
//   Editor-only. Does not change TimeManager assets or unrelated time-scale owners.
//   The second exit notification is harmless because release consumes ownership.
// ============================================================================
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.Menu;
namespace Worsen.Editor.Menu
{
    [InitializeOnLoad]
    public static class MenuPauseSafety
    {
        static MenuPauseSafety() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode && state != PlayModeStateChange.EnteredEditMode) return;
            if (!MenuDriver.ReleasePauseTimeScale()) return;
            Time.timeScale = 1f;
            Debug.Log("Menu pause released on Play Mode exit; Time.timeScale restored to 1.");
        }
    }
}
