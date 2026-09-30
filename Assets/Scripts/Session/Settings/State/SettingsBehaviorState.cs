// ============================================================================
// SettingsBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Holds immutable preference and lifetime-history snapshots for this process.
//   Only the Settings owner replaces them; consumers receive Core value records.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Retain sanitized runtime overrides and best-depth history, not assets.
// DEPENDENCIES:
//   Core persistence records only.
// USAGE NOTES:
//   Persistent through SettingsManager ownership; contains no scene references.
// ============================================================================
using Worsen.Core;
namespace Worsen.Session.Settings
{
    public sealed class SettingsBehaviorState
    {
        public PlayerSettingsRecord Settings;
        public RunHistoryRecord History;
    }
}
