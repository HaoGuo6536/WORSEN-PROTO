// ============================================================================
// PauseFixtureCleanup.cs
// ============================================================================
// PURPOSE:
//   Isolates pause fixtures from engine globals and destroyed Session singletons.
//   Fixtures call this from finally so a failed assertion cannot freeze later tests.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test support (§11) · Menu.
// KEY RESPONSIBILITIES:
//   - Release pause ownership, restore normal time and clear both Session backing fields.
// DEPENDENCIES:
//   UnityEngine, reflection, Presentation Menu and Session Run/Progression.
// USAGE NOTES:
//   Only for isolated test fixtures after their owned objects and subscriptions retire.
//   Does not destroy foreign objects or invoke persistent initialization.
// ============================================================================
using System.Reflection;
using UnityEngine;
using Worsen.Presentation.Menu;
using Worsen.Session.Run;
using Worsen.Session.Progression;
namespace Worsen.Tests.Menu
{
    internal static class PauseFixtureCleanup
    {
        public static void Restore()
        {
            MenuDriver.ReleasePauseTimeScale();
            Time.timeScale = 1f;
            typeof(RunSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            typeof(ProgressionSessionManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
    }
}
