// ============================================================================
// PersistenceDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries versioned run history and player preferences across a Session save boundary.
//   Records have no file operations and never expose caller-owned mutable collections.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Persistence contracts.
// KEY RESPONSIBILITIES:
//   - Preserve schema identity and immutable save inputs for pure validation.
// DEPENDENCIES:
//   - System collections only; no persistence service or project-layer references.
// USAGE NOTES:
//   Constructors preserve raw values; use PersistenceUtility before applying them.
//   Camera comfort settings are enable switches, not designer effect magnitudes.
//   Future schema versions remain identifiable; only Session may migrate a save.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Worsen.Core
{
    /// <summary>A versioned history snapshot with lifetime counters and unlocked archetype keys.</summary>
    public readonly struct RunHistoryRecord
    {
        public const int CurrentSchemaVersion = 1;
        private readonly IReadOnlyList<string> _unlockedThreatIds;
        public RunHistoryRecord(int schemaVersion, long lifetimeRuns, int bestDepth, IEnumerable<string> unlockedThreatIds)
        {
            SchemaVersion = schemaVersion; LifetimeRuns = lifetimeRuns; BestDepth = bestDepth;
            _unlockedThreatIds = Array.AsReadOnly(unlockedThreatIds == null ? Array.Empty<string>() :
                new List<string>(unlockedThreatIds).ToArray());
        }
        public int SchemaVersion { get; }
        public long LifetimeRuns { get; }
        public int BestDepth { get; }
        public IReadOnlyList<string> UnlockedThreatIds => _unlockedThreatIds ?? Array.Empty<string>();
    }

    /// <summary>A versioned preference snapshot with degree-based field of view and linear volume gains.</summary>
    public readonly struct PlayerSettingsRecord
    {
        public const int CurrentSchemaVersion = 1;
        public PlayerSettingsRecord(int schemaVersion, float mouseSensitivity, bool invertY, float fieldOfView,
            bool cameraTilt, bool cameraPunch, bool reacquireBlur, float masterVolume, float musicVolume, float effectsVolume)
        {
            SchemaVersion = schemaVersion; MouseSensitivity = mouseSensitivity; InvertY = invertY; FieldOfView = fieldOfView;
            CameraTilt = cameraTilt; CameraPunch = cameraPunch; ReacquireBlur = reacquireBlur;
            MasterVolume = masterVolume; MusicVolume = musicVolume; EffectsVolume = effectsVolume;
        }
        public int SchemaVersion { get; }
        public float MouseSensitivity { get; }
        public bool InvertY { get; }
        public float FieldOfView { get; }
        public bool CameraTilt { get; }
        public bool CameraPunch { get; }
        public bool ReacquireBlur { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float EffectsVolume { get; }
    }
}
