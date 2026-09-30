// ============================================================================
// MenuDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains menu visibility, pending actions and a settings draft across UI rebinds.
//   Simulation authority remains external; this is only a copy of acknowledged state.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Menu.
// KEY RESPONSIBILITIES:
//   - Retain the previous engine time scale while an acknowledged pause owns it.
//   - Store title, pause and settings state without clocks or engine calls.
// DEPENDENCIES:
//   Core PlayerSettingsRecord only.
// USAGE NOTES:
//   Scene-owned through MenuDriver. Title blocks startup until explicitly acknowledged.
// ============================================================================
using Worsen.Core;
namespace Worsen.Presentation.Menu
{
    public sealed class MenuDriverState
    {
        public bool TitleVisible, CanPause, Paused, Pending, SettingsReady;
        public bool OwnsTimeScale;
        public float PreviousTimeScale;
        public PlayerSettingsRecord Settings, Draft;
        public string Message = "";
    }
}
