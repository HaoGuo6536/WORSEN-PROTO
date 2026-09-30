// ============================================================================
// PersistenceUtility.cs
// ============================================================================
// PURPOSE:
//   Sanitizes immutable persistence records without reading or writing files.
//   Invalid floating values use caller-supplied preferences rather than hidden tuning.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Core · shared Persistence validation.
// KEY RESPONSIBILITIES:
//   - Clamp counters and numeric settings, and canonicalize unlocked threat sets.
// DEPENDENCIES:
//   - Core Persistence records and System collections only.
// USAGE NOTES:
//   Nonpositive schema numbers become version one; future versions are preserved,
//   not declared supported. Session must reject or migrate unsupported versions.
//   FOV is clamped to 1..179 degrees to avoid degenerate projections, sensitivity
//   is nonnegative (zero disables mouse motion), and linear gains are in [0,1].
//   These are validity bounds, not menu tuning; Config owns the supplied fallback.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Worsen.Core
{
    /// <summary>Pure normalization of save snapshots, without schema migration or file operations.</summary>
    public static class PersistenceUtility
    {
        public static RunHistoryRecord ClampHistory(RunHistoryRecord value)
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in value.UnlockedThreatIds)
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            return new RunHistoryRecord(Math.Max(RunHistoryRecord.CurrentSchemaVersion, value.SchemaVersion),
                Math.Max(0L, value.LifetimeRuns), Math.Max(0, value.BestDepth), ids);
        }

        public static PlayerSettingsRecord ClampSettings(PlayerSettingsRecord value, PlayerSettingsRecord fallback)
        {
            if (!Valid(fallback.MouseSensitivity, 0f, float.MaxValue) || !Valid(fallback.FieldOfView, 1f, 179f) ||
                !Valid(fallback.MasterVolume, 0f, 1f) || !Valid(fallback.MusicVolume, 0f, 1f) || !Valid(fallback.EffectsVolume, 0f, 1f))
                throw new ArgumentException("Fallback preferences must have valid finite numbers.", nameof(fallback));
            return new PlayerSettingsRecord(Math.Max(PlayerSettingsRecord.CurrentSchemaVersion, value.SchemaVersion),
                Clamp(value.MouseSensitivity, 0f, float.MaxValue, fallback.MouseSensitivity), value.InvertY,
                Clamp(value.FieldOfView, 1f, 179f, fallback.FieldOfView), value.CameraTilt, value.CameraPunch, value.ReacquireBlur,
                Clamp(value.MasterVolume, 0f, 1f, fallback.MasterVolume), Clamp(value.MusicVolume, 0f, 1f, fallback.MusicVolume),
                Clamp(value.EffectsVolume, 0f, 1f, fallback.EffectsVolume));
        }

        private static bool Valid(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;

        private static float Clamp(float value, float minimum, float maximum, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(maximum, value));
    }
}
