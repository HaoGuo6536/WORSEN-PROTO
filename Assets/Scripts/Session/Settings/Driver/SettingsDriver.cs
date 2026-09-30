// ============================================================================
// SettingsDriver.cs
// ============================================================================
// PURPOSE:
//   Reads and writes small versioned JSON files below the player's persistent data path.
//   Failed reads use supplied defaults with diagnostics; future schemas are never overwritten.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Session · Settings.
// KEY RESPONSIBILITIES:
//   - Own file IO, Unity JSON serialization, atomic replacement and warning logging.
//   - Preserve the prior file as a backup and report unsuccessful saves to the Manager.
// DEPENDENCIES:
//   Core records and own serialization definitions; System.IO and Unity JsonUtility.
// USAGE NOTES:
//   Persistent through SettingsManager. No tunables: filenames and schema are protocol.
//   Virtual file/log boundaries allow fake-file tests without touching player storage.
//   Files are player-settings.json and run-history.json under Application.persistentDataPath.
// ============================================================================
using System;
using System.IO;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Session.Settings
{
    public class SettingsDriver : MonoBehaviour
    {
        private readonly SettingsDriverState _state = new SettingsDriverState();
        public string LastError => _state.LastError;
        public PlayerSettingsRecord LoadSettings(PlayerSettingsRecord defaults)
        {
            _state.SettingsReadOnly = false;
            try
            {
                string json = ReadFile("player-settings.json");
                if (json == null) return defaults;
                var data = ToData(defaults);
                data.schemaVersion = 0;
                JsonUtility.FromJsonOverwrite(json, data);
                _state.SettingsReadOnly = data.schemaVersion > PlayerSettingsRecord.CurrentSchemaVersion;
                if (data.schemaVersion != PlayerSettingsRecord.CurrentSchemaVersion)
                    throw new InvalidDataException("Unsupported or missing settings schema " + data.schemaVersion);
                return new PlayerSettingsRecord(data.schemaVersion, data.mouseSensitivity, data.invertY,
                    data.fieldOfView, data.cameraTilt, data.cameraPunch, data.reacquireBlur,
                    data.masterVolume, data.musicVolume, data.effectsVolume);
            }
            catch (Exception error) when (Recoverable(error)) { Warn("Settings load fallback: " + error.Message); return defaults; }
        }
        public RunHistoryRecord LoadHistory()
        {
            _state.HistoryReadOnly = false;
            try
            {
                string json = ReadFile("run-history.json");
                if (json == null) return new RunHistoryRecord(1, 0, 0, null);
                var data = JsonUtility.FromJson<HistoryFileData>(json);
                _state.HistoryReadOnly = data != null && data.schemaVersion > RunHistoryRecord.CurrentSchemaVersion;
                if (data == null || data.schemaVersion != RunHistoryRecord.CurrentSchemaVersion)
                    throw new InvalidDataException("Unsupported or missing history schema.");
                return new RunHistoryRecord(data.schemaVersion, data.lifetimeRuns, data.bestDepth, data.unlockedThreatIds);
            }
            catch (Exception error) when (Recoverable(error))
            { Warn("History load fallback: " + error.Message); return new RunHistoryRecord(1, 0, 0, null); }
        }
        public bool SaveSettings(PlayerSettingsRecord value)
        {
            if (_state.SettingsReadOnly || value.SchemaVersion != PlayerSettingsRecord.CurrentSchemaVersion)
            { Warn("Settings save refused: unsupported schema; original file preserved."); return false; }
            return Save("player-settings.json", JsonUtility.ToJson(ToData(value), true));
        }
        public bool SaveHistory(RunHistoryRecord value)
        {
            if (_state.HistoryReadOnly || value.SchemaVersion != RunHistoryRecord.CurrentSchemaVersion)
            { Warn("History save refused: unsupported schema; original file preserved."); return false; }
            var ids = new string[value.UnlockedThreatIds.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = value.UnlockedThreatIds[i];
            return Save("run-history.json", JsonUtility.ToJson(new HistoryFileData { schemaVersion = value.SchemaVersion,
                lifetimeRuns = value.LifetimeRuns, bestDepth = value.BestDepth, unlockedThreatIds = ids }, true));
        }
        private bool Save(string name, string json)
        {
            try { WriteFile(name, json); _state.LastError = ""; return true; }
            catch (Exception error) when (Recoverable(error)) { Warn("Save failed: " + error.Message); return false; }
        }
        protected virtual string ReadFile(string name)
        {
            string path = Path.Combine(Application.persistentDataPath, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        protected virtual void WriteFile(string name, string json)
        {
            string root = Application.persistentDataPath;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name), temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        protected virtual void LogWarning(string message) => Debug.LogWarning(message, this);
        private void Warn(string message) { _state.LastError = message; LogWarning(message); }
        private static bool Recoverable(Exception error) => error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException;
        private static SettingsFileData ToData(PlayerSettingsRecord value) => new SettingsFileData {
            schemaVersion = value.SchemaVersion, mouseSensitivity = value.MouseSensitivity, invertY = value.InvertY,
            fieldOfView = value.FieldOfView, cameraTilt = value.CameraTilt, cameraPunch = value.CameraPunch,
            reacquireBlur = value.ReacquireBlur, masterVolume = value.MasterVolume,
            musicVolume = value.MusicVolume, effectsVolume = value.EffectsVolume };
    }
}
