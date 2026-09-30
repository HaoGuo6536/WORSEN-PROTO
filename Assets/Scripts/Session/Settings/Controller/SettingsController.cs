// ============================================================================
// SettingsController.cs
// ============================================================================
// PURPOSE:
//   Validates loaded preferences and updates lifetime history using explicit facts.
//   Persistence and applying overrides to other systems remain outside this logic.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Sanitize supported settings and increase best depth only when exceeded.
//   - Count explicit expedition starts without counting each floor as another run.
// DEPENDENCIES:
//   Core persistence records/utility and own SettingsBehaviorState only.
// USAGE NOTES:
//   Pure logic; schema one is supported, other versions are rejected before use.
// ============================================================================
using System;
using Worsen.Core;
namespace Worsen.Session.Settings
{
    public sealed class SettingsController
    {
        private readonly SettingsBehaviorState _state;
        private readonly PlayerSettingsRecord _defaults;
        public SettingsController(SettingsBehaviorState state, PlayerSettingsRecord defaults)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            if (defaults.SchemaVersion != PlayerSettingsRecord.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported settings defaults.", nameof(defaults));
            _defaults = PersistenceUtility.ClampSettings(defaults, defaults);
            _state.Settings = _defaults;
            _state.History = new RunHistoryRecord(1, 0, 0, null);
        }
        public bool Apply(PlayerSettingsRecord value)
        {
            if (value.SchemaVersion != PlayerSettingsRecord.CurrentSchemaVersion) return false;
            _state.Settings = PersistenceUtility.ClampSettings(value, _defaults);
            return true;
        }
        public bool RestoreHistory(RunHistoryRecord value)
        {
            if (value.SchemaVersion != RunHistoryRecord.CurrentSchemaVersion) return false;
            _state.History = PersistenceUtility.ClampHistory(value);
            return true;
        }
        public void RecordRunStarted()
        {
            var h = _state.History;
            _state.History = new RunHistoryRecord(1, h.LifetimeRuns == long.MaxValue ? long.MaxValue : h.LifetimeRuns + 1,
                h.BestDepth, h.UnlockedThreatIds);
        }
        public bool RecordBestDepth(int depth)
        {
            var h = _state.History;
            if (depth <= h.BestDepth) return false;
            _state.History = new RunHistoryRecord(1, h.LifetimeRuns, depth, h.UnlockedThreatIds);
            return true;
        }
    }
}
