// ============================================================================
// MenuPresenter.cs
// ============================================================================
// PURPOSE:
//   Admits title/pause interactions and sanitizes a mutable preference draft.
//   Acknowledgements, not clicks alone, decide whether the simulation is paused.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Menu.
// KEY RESPONSIBILITIES:
//   - Debounce starts and pause/resume requests until the owner acknowledges them.
//   - Return immutable runtime overrides without any designer-asset mutation.
// DEPENDENCIES:
//   Core persistence records/utility and own MenuDriverState only.
// USAGE NOTES:
//   Pure calculator. SetSettings receives already valid store output; edits clamp
//   against that snapshot. No Time.timeScale or Session reference is used.
// ============================================================================
using Worsen.Core;
namespace Worsen.Presentation.Menu
{
    public sealed class MenuPresenter
    {
        public void ShowTitle(MenuDriverState state)
        { state.TitleVisible = true; state.CanPause = state.Paused = state.Pending = false; }
        public void SetRunState(MenuDriverState state, bool canPause, bool paused)
        {
            state.TitleVisible = false; state.CanPause = canPause;
            state.Paused = canPause && paused; state.Pending = false;
        }
        public bool TryStart(MenuDriverState state)
        {
            if (!state.TitleVisible || state.Pending) return false;
            state.Pending = true; return true;
        }
        public bool TryTogglePause(MenuDriverState state, out bool pause)
        {
            pause = !state.Paused;
            if (state.TitleVisible || !state.CanPause || state.Pending) return false;
            state.Pending = true; return true;
        }
        public bool SetSettings(MenuDriverState state, PlayerSettingsRecord value)
        {
            if (value.SchemaVersion != PlayerSettingsRecord.CurrentSchemaVersion) return false;
            state.Settings = state.Draft = PersistenceUtility.ClampSettings(value, value);
            state.SettingsReady = true; return true;
        }
        public bool EditSettings(MenuDriverState state, PlayerSettingsRecord value)
        {
            if (!state.SettingsReady || !state.Paused || state.Pending || value.SchemaVersion != PlayerSettingsRecord.CurrentSchemaVersion) return false;
            state.Draft = PersistenceUtility.ClampSettings(value, state.Settings);
            state.Message = "Unsaved changes"; return true;
        }
        public bool TryApply(MenuDriverState state, out PlayerSettingsRecord overrides)
        {
            overrides = state.Draft;
            if (!state.SettingsReady || !state.Paused || state.Pending) return false;
            overrides = PersistenceUtility.ClampSettings(state.Draft, state.Settings);
            state.Message = "Applying settings…"; return true;
        }
        public void SetSaveResult(MenuDriverState state, bool saved, string message)
            => state.Message = saved ? "Settings saved." : "Active for this session; not saved. " + message;
    }
}
