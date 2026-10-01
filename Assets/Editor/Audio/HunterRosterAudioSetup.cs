// ============================================================================
// HunterRosterAudioSetup.cs
// ============================================================================
// PURPOSE:
//   Resolves the reviewed path-only roster selection without copying vendor audio.
//   Explicit silence and whole-clip choices replace stale owned bindings atomically.
//   Unrelated legacy banks remain available only to compatibility hunters.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Audio.
// KEY RESPONSIBILITIES:
//   - Validate the selection table and every required clip before changing a config.
//   - Replace owned named bindings deterministically while preserving unrelated legacy banks.
//   - Save only the explicitly targeted soundscape asset, never scenes or importers.
// DEPENDENCIES:
//   - Core cue ids, Audio config, UnityEditor asset/serialization APIs and file IO.
// USAGE NOTES:
//   Coordinator runs after integration/import under its own Unity admission protocol.
//   Vendor packs must already be installed. No download, copy or fallback substitution.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Audio;
namespace Worsen.Editor.Audio
{
    public static class HunterRosterAudioSetup
    {
        public const string SelectionPath = "Assets/Audio/HunterRoster/SELECTION.md";
        public sealed class Selection
        {
            public string Id { get; }
            public CueId Bank { get; }
            public float Gain { get; }
            public string[] Paths { get; }
            public Selection(string id, CueId bank, float gain, string[] paths)
            { Id = id; Bank = bank; Gain = gain; Paths = paths; }
        }
        // Table rows are deliberately machine-readable; prose is ignored.
        public static Selection[] Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var clips = new Dictionary<string, string>(StringComparer.Ordinal);
            var rows = new List<string[]>();
            foreach (string line in text.Split('\n'))
            {
                string[] cells = line.Split('|').Select(s => s.Trim()).ToArray();
                if (cells.Length < 3) continue;
                if (cells[1].StartsWith("clip:", StringComparison.Ordinal))
                {
                    if (cells.Length != 8) throw new FormatException("Malformed clip row: " + line);
                    string path = cells[2];
                    if (!path.StartsWith("Assets/External/", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\") ||
                        !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Unsafe vendor path: " + path);
                    clips.Add(cells[1].Substring(5), path);
                }
                if (cells[1].StartsWith("cue:", StringComparison.Ordinal))
                {
                    if (cells.Length != 8) throw new FormatException("Malformed cue row: " + line);
                    rows.Add(cells);
                }
            }
            var selections = new List<Selection>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string[] row in rows)
            {
                string id = row[1].Substring(4);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new FormatException("Duplicate/empty cue: " + id);
                if (!Enum.TryParse(row[2], out CueId bank) || !Enum.IsDefined(typeof(CueId), bank)) throw new FormatException("Unknown bank: " + row[2]);
                if (!float.TryParse(row[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float gain) ||
                    float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0f || gain > 1f) throw new FormatException("Invalid gain: " + id);
                var paths = new List<string>();
                if (row[4] == "silence" || row[4] == "missing")
                {
                    if (row[5] != "-" || (row[4] == "silence" ? gain != 0f : gain == 0f))
                        throw new FormatException("Silence requires zero gain; missing requires positive gain; neither permits alternates: " + id);
                    selections.Add(new Selection(id, bank, gain, Array.Empty<string>()));
                    continue;
                }
                foreach (string key in (row[4] + "," + row[5]).Split(','))
                {
                    if (key.Trim() == "-" || key.Trim().Length == 0) continue;
                    if (!clips.TryGetValue(key.Trim(), out string path)) throw new FormatException("Unresolved clip: " + key);
                    if (paths.Contains(path)) throw new FormatException("Duplicate alternate: " + id);
                    paths.Add(path);
                }
                if (paths.Count == 0) throw new FormatException("No clip selected: " + id);
                selections.Add(new Selection(id, bank, gain, paths.ToArray()));
            }
            if (selections.Count == 0) throw new FormatException("No roster selections.");
            return selections.OrderBy(s => s.Id, StringComparer.Ordinal).ToArray();
        }
        public static AudioRosterBinding[] Resolve(Selection[] selections, Func<string, AudioClip> load)
        {
            if (selections == null || load == null) throw new ArgumentNullException();
            return selections.Select(s => {
                if (s.Paths.Length == 0) return new AudioRosterBinding(s.Id, s.Bank, true) { Gain = s.Gain, OverrideGain = true };
                var clips = s.Paths.Select(path => load(path) ?? throw new FileNotFoundException("Required roster clip missing: " + path, path)).ToArray();
                return new AudioRosterBinding(s.Id, s.Bank) { Clip = clips[0], Alternates = clips.Skip(1).ToArray(), Gain = s.Gain, OverrideGain = true };
            }).ToArray();
        }
        [MenuItem("Worsen/Audio/Assign Hunter Roster Selection")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Roster setup requires an idle Editor.");
            var config = AssetDatabase.LoadAssetAtPath<AudioSoundscapeDriverConfig>(HorrorAudioSetup.ConfigPath);
            if (config == null) throw new FileNotFoundException("Build Horror Soundscape before assigning the roster.", HorrorAudioSetup.ConfigPath);
            Configure(config, File.ReadAllText(SelectionPath));
        }
        public static void Configure(AudioSoundscapeDriverConfig config, string selectionText)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Roster setup requires an idle Editor.");
            // No asset mutation until every selection resolves successfully.
            var selected = Resolve(Parse(selectionText), AssetDatabase.LoadAssetAtPath<AudioClip>);
            var merged = new SortedDictionary<string, AudioRosterBinding>(StringComparer.Ordinal);
            var roster = new AudioRosterPresenter();
            foreach (var binding in config.RosterBindings) if (!roster.IsRosterCue(binding.Id)) merged[binding.Id] = binding;
            foreach (var binding in selected) merged[binding.Id] = binding;
            var serialized = new SerializedObject(config); var bindings = serialized.FindProperty("_rosterBindings");
            bindings.arraySize = merged.Count; int index = 0;
            foreach (var binding in merged.Values)
            {
                var item = bindings.GetArrayElementAtIndex(index++);
                item.FindPropertyRelative("Id").stringValue = binding.Id;
                item.FindPropertyRelative("Bank").intValue = (int)binding.Bank;
                item.FindPropertyRelative("Placeholder").boolValue = binding.Placeholder;
                item.FindPropertyRelative("Clip").objectReferenceValue = binding.Clip;
                item.FindPropertyRelative("Gain").floatValue = binding.Gain;
                item.FindPropertyRelative("OverrideGain").boolValue = binding.OverrideGain;
                var alternates = item.FindPropertyRelative("Alternates"); alternates.arraySize = binding.Alternates?.Length ?? 0;
                for (int i = 0; i < alternates.arraySize; i++) alternates.GetArrayElementAtIndex(i).objectReferenceValue = binding.Alternates[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("Assigned " + selected.Length + " reviewed roster bindings to " + AssetDatabase.GetAssetPath(config));
        }
    }
}
