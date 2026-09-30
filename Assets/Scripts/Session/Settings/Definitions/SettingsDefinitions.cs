// ============================================================================
// SettingsDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines the private JSON envelopes used by the Session persistence boundary.
//   Core records remain immutable and independent of Unity serialization fields.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Carry schema-tagged settings and history as serialization-only fields.
// DEPENDENCIES:
//   System serialization attributes only.
// USAGE NOTES:
//   Not cross-system contracts; only SettingsDriver reads or writes these envelopes.
// ============================================================================
using System;
namespace Worsen.Session.Settings
{
    [Serializable]
    public sealed class SettingsFileData
    {
        public int schemaVersion;
        public float mouseSensitivity, fieldOfView, masterVolume, musicVolume, effectsVolume;
        public bool invertY, cameraTilt, cameraPunch, reacquireBlur;
    }
    [Serializable]
    public sealed class HistoryFileData
    {
        public int schemaVersion, bestDepth;
        public long lifetimeRuns;
        public string[] unlockedThreatIds;
    }
}
