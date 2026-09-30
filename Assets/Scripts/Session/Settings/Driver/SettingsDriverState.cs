// ============================================================================
// SettingsDriverState.cs
// ============================================================================
// PURPOSE:
//   Remembers file compatibility and the last persistence error for the owning Driver.
//   Unsupported future files stay protected even after defaults are used in memory.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Retain write protection and diagnostics without performing file operations.
// DEPENDENCIES:
//   None.
// USAGE NOTES:
//   Persistent through the owning SettingsDriver; not serialized into save files.
// ============================================================================
namespace Worsen.Session.Settings
{
    public sealed class SettingsDriverState
    {
        public bool SettingsReadOnly, HistoryReadOnly;
        public string LastError = "";
    }
}
